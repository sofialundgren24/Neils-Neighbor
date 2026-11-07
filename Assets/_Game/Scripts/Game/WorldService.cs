using System.Collections.Generic;
using System.Linq;
using NG.Core;
using NG.Data;
using NG.State;
using NG.World;
using UnityEngine;

namespace NG.Game
{
    /// <summary>
    /// Kartan: vad som står var, och reglerna för att köpa, placera, flytta, sälja och bygga.
    /// Också områden (MapExpansion) som låses upp med nivå + mynt, så kartan växer med spelet.
    /// </summary>
    public sealed class WorldService
    {
        readonly GameSession _s;
        readonly Dictionary<string, PlacedObjectState> _byId = new Dictionary<string, PlacedObjectState>();
        readonly Dictionary<string, PlaceableDefinition> _defs = new Dictionary<string, PlaceableDefinition>();
        PlotGrid _grid;

        public WorldService(GameSession s) { _s = s; }

        public IReadOnlyList<PlacedObjectState> Objects => _s.State.objects;
        public PlotGrid Grid => _grid;

        public PlacedObjectState Get(string instanceId) =>
            instanceId != null && _byId.TryGetValue(instanceId, out var o) ? o : null;

        public PlaceableDefinition Def(PlacedObjectState o) => o == null ? null : Def(o.definitionId);

        public PlaceableDefinition Def(string definitionId)
        {
            if (definitionId == null) return null;
            if (!_defs.TryGetValue(definitionId, out var def))
            {
                def = _s.Content.Get<PlaceableDefinition>(definitionId);
                _defs[definitionId] = def;
            }
            return def;
        }

        public int CountOf(PlaceableDefinition def) => Objects.Count(o => o.definitionId == def.Id);

        // ---------- Rutnät och koordinater ----------

        public void RebuildGrid()
        {
            _grid = new PlotGrid(_s.Config.mapSize.x, _s.Config.mapSize.y);
            _byId.Clear();
            foreach (var o in Objects)
            {
                _byId[o.instanceId] = o;
                var def = Def(o);
                if (def == null) continue;
                if (!_grid.IsFree(o.x, o.y, def.footprint, o.rotation))
                    Debug.LogWarning($"[World] {o.definitionId} på ({o.x},{o.y}) överlappar något annat.");
                _grid.Occupy(o.instanceId, o.x, o.y, def.footprint, o.rotation);
            }
        }

        /// <summary>Världsposition för objektets pivot (fotavtryckets mitt på marken).</summary>
        public Vector3 CellToWorld(PlaceableDefinition def, int x, int y, int rotation) =>
            _s.Config.MapOriginWorld + PlotGrid.CellToLocal(x, y, def != null ? def.footprint : Vector2Int.one, rotation);

        public Vector2Int WorldToCell(Vector3 world) => PlotGrid.LocalToCell(world - _s.Config.MapOriginWorld);

        /// <summary>Cellen (nedre vänstra hörnet) som centrerar fotavtrycket kring en världspunkt.</summary>
        public Vector2Int CenteredCell(PlaceableDefinition def, int rotation, Vector3 world)
        {
            var size = PlotGrid.Rotated(def.footprint, rotation);
            var local = world - _s.Config.MapOriginWorld;
            return new Vector2Int(
                Mathf.RoundToInt(local.x / PlotGrid.CellSize - size.x * 0.5f),
                Mathf.RoundToInt(local.z / PlotGrid.CellSize - size.y * 0.5f));
        }

        public static Quaternion RotationOf(int rotation) => Quaternion.Euler(0f, 90f * rotation, 0f);

        // ---------- Områden ----------

        public bool IsExpansionUnlocked(MapExpansion e) => _s.State.unlockedExpansions.Contains(e.id);

        public bool IsCellUnlocked(int x, int y)
        {
            var cell = new Vector2Int(x, y);
            foreach (var e in _s.Config.expansions)
                if (e.area.Contains(cell) && IsExpansionUnlocked(e)) return true;
            return false;
        }

        /// <summary>Låst område som cellen ligger i, annars null.</summary>
        public MapExpansion LockedExpansionAt(Vector2Int cell) =>
            _s.Config.expansions.FirstOrDefault(e => e.area.Contains(cell) && !IsExpansionUnlocked(e));

        public Result UnlockExpansion(MapExpansion e)
        {
            if (e == null || IsExpansionUnlocked(e)) return Result.Fail("Området är redan upplåst.");
            if (_s.Progression.Level < e.requiredLevel) return Result.Fail($"Kräver nivå {e.requiredLevel}.");
            if (!_s.Wallet.TrySpend(CurrencyType.Coins, e.coinCost)) return Result.Fail("Inte tillräckligt med mynt.");
            _s.State.unlockedExpansions.Add(e.id);
            EventBus.Publish(new ExpansionUnlocked(e.id));
            return Result.Success;
        }

        // ---------- Placering ----------

        /// <summary>Kontrollerar bara platsen (inom kartan, upplåst område, ledigt).</summary>
        public bool IsSpotFree(PlaceableDefinition def, int x, int y, int rotation, string ignoreInstanceId, out string reason)
        {
            reason = null;
            if (!_grid.IsFree(x, y, def.footprint, rotation, ignoreInstanceId))
            {
                reason = "Platsen är upptagen.";
                return false;
            }
            var size = PlotGrid.Rotated(def.footprint, rotation);
            for (int i = x; i < x + size.x; i++)
                for (int j = y; j < y + size.y; j++)
                    if (!IsCellUnlocked(i, j))
                    {
                        reason = "Området är inte upplåst än.";
                        return false;
                    }
            return true;
        }

        /// <summary>Kontrollerar allt för ett köp: nivå, antal, pengar, material och plats.</summary>
        public bool CanBuy(PlaceableDefinition def, out string reason)
        {
            reason = null;
            if (def == null) { reason = "Okänt objekt."; return false; }
            if (!_s.Progression.IsUnlocked(def)) { reason = $"Kräver nivå {def.unlockLevel}."; return false; }
            if (def.maxCount > 0 && CountOf(def) >= def.maxCount) { reason = "Du har redan max antal."; return false; }
            if (!_s.Wallet.CanAfford(def.cost)) { reason = "Inte tillräckligt med mynt."; return false; }
            if (!_s.Inventory.Has(def.materials)) { reason = $"Behöver {InventoryService.Format(def.materials)}."; return false; }
            return true;
        }

        public Result Place(PlaceableDefinition def, int x, int y, int rotation)
        {
            if (!CanBuy(def, out var reason)) return Result.Fail(reason);
            if (!IsSpotFree(def, x, y, rotation, null, out reason)) return Result.Fail(reason);

            _s.Wallet.TrySpend(def.cost);
            _s.Inventory.TryRemove(def.materials);

            var o = CreateState(def, x, y, rotation, decayed: false);
            if (def.buildTimeSec > 0) o.build = Timers.StartNow(def.buildTimeSec);
            else _s.Progression.AddXp(def.xpReward);
            Add(o, def);
            return Result.Success;
        }

        /// <summary>Lägger ut ett objekt från startlayouten (gratis, kan vara grått).</summary>
        public void AddFromLayout(StartPlacement p)
        {
            if (p.definition == null) return;
            if (!_grid.IsFree(p.cell.x, p.cell.y, p.definition.footprint, p.rotation))
                Debug.LogWarning($"[World] Startlayout: {p.definition.Id} på {p.cell} överlappar.");
            var o = CreateState(p.definition, p.cell.x, p.cell.y, p.rotation, p.decayed);
            Add(o, p.definition);
        }

        PlacedObjectState CreateState(PlaceableDefinition def, int x, int y, int rotation, bool decayed)
        {
            var o = new PlacedObjectState
            {
                definitionId = def.Id,
                x = x,
                y = y,
                rotation = rotation & 3,
                decayed = decayed && !(def is RenovatableDefinition)
            };
            switch (def)
            {
                case RenovatableDefinition _:
                    // Hus på startgatan börjar förfallna; hus som köps senare är nya och färdiga.
                    o.renovation = new RenovationState { complete = !decayed };
                    if (!decayed) o.renovation.phase = ((RenovatableDefinition)def).phases.Length;
                    break;
                case PlantDefinition plant:
                    if (!decayed) o.growth = Timers.StartNow(plant.growTimeSec);
                    break;
                case ProductionDefinition _:
                    o.production = new ProductionState();
                    break;
            }
            return o;
        }

        void Add(PlacedObjectState o, PlaceableDefinition def)
        {
            _s.State.objects.Add(o);
            _byId[o.instanceId] = o;
            _grid.Occupy(o.instanceId, o.x, o.y, def.footprint, o.rotation);
            EventBus.Publish(new ObjectPlaced(o.instanceId));
        }

        public Result Move(PlacedObjectState o, int x, int y, int rotation)
        {
            var def = Def(o);
            if (def == null) return Result.Fail("Okänt objekt.");
            if (!IsSpotFree(def, x, y, rotation, o.instanceId, out var reason)) return Result.Fail(reason);
            _grid.Free(o.x, o.y, def.footprint, o.rotation);
            o.x = x;
            o.y = y;
            o.rotation = rotation & 3;
            _grid.Occupy(o.instanceId, o.x, o.y, def.footprint, o.rotation);
            EventBus.Publish(new ObjectMoved(o.instanceId));
            return Result.Success;
        }

        public bool CanSell(PlacedObjectState o)
        {
            var def = Def(o);
            return def != null && def.inShop && o.build == null;
        }

        public int SellValue(PlacedObjectState o)
        {
            var def = Def(o);
            if (def == null) return 0;
            int coins = def.cost.Where(c => c.currency == CurrencyType.Coins).Sum(c => c.amount);
            return Mathf.FloorToInt(coins * _s.Config.sellRefund);
        }

        public Result Sell(PlacedObjectState o)
        {
            if (!CanSell(o)) return Result.Fail("Det här går inte att sälja.");
            var def = Def(o);
            _s.Wallet.Add(CurrencyType.Coins, SellValue(o));
            _grid.Free(o.x, o.y, def.footprint, o.rotation);
            _s.State.objects.Remove(o);
            _byId.Remove(o.instanceId);
            EventBus.Publish(new ObjectRemoved(o.instanceId));
            return Result.Success;
        }

        // ---------- Byggtid ----------

        public bool IsBuilding(PlacedObjectState o) => o.build != null;

        public Result SpeedUpBuild(PlacedObjectState o)
        {
            if (o.build == null) return Result.Fail("Bygget är redan klart.");
            int gems = TimeService.SpeedUpGemCost(Timers.Remaining(o.build));
            if (!_s.Wallet.TrySpend(CurrencyType.Gems, gems)) return Result.Fail("Inte tillräckligt med diamanter.");
            Timers.Finish(o.build);
            Tick();
            return Result.Success;
        }

        public void Tick()
        {
            foreach (var o in Objects)
            {
                if (o.build == null || !Timers.IsDone(o.build)) continue;
                o.build = null;
                var def = Def(o);
                if (def is PlantDefinition plant) o.growth = Timers.StartNow(plant.growTimeSec);
                _s.Progression.AddXp(def != null ? def.xpReward : 0);
                EventBus.Publish(new ObjectChanged(o.instanceId, true));
            }
        }
    }
}
