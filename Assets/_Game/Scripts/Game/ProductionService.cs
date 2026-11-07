using System.Collections.Generic;
using System.Linq;
using NG.Core;
using NG.Data;
using NG.State;

namespace NG.Game
{
    /// <summary>
    /// Produktionsbyggnader (affär, trädgårdsbutik). Spelaren lägger recept i kö; varorna dras direkt,
    /// recepten görs ett i taget och de färdiga hämtas med ett tryck. Kön räknas på absoluta tider,
    /// så den fortsätter medan appen är stängd.
    /// </summary>
    public sealed class ProductionService
    {
        readonly GameSession _s;
        public ProductionService(GameSession s) { _s = s; }

        public ProductionDefinition Def(PlacedObjectState o) => _s.World.Def(o) as ProductionDefinition;

        public RecipeDefinition Recipe(string id) => _s.Content.Get<RecipeDefinition>(id);

        public IEnumerable<RecipeDefinition> Recipes(PlacedObjectState o) =>
            Def(o)?.recipes.Where(r => r != null) ?? Enumerable.Empty<RecipeDefinition>();

        public int ReadyCount(PlacedObjectState o) => o.production?.ready.Count ?? 0;

        public bool IsWorking(PlacedObjectState o) => o.production != null && o.production.queue.Count > 0;

        public bool CanEnqueue(PlacedObjectState o, RecipeDefinition r, out string reason)
        {
            reason = null;
            var def = Def(o);
            if (def == null || o.production == null) { reason = "Ingen produktion här."; return false; }
            if (o.build != null) { reason = "Byggnaden är inte klar än."; return false; }
            if (!_s.Progression.IsUnlocked(r)) { reason = $"Kräver nivå {r.unlockLevel}."; return false; }
            if (o.production.queue.Count >= def.queueSize) { reason = "Kön är full."; return false; }
            if (!_s.Inventory.Has(r.inputs)) { reason = $"Behöver {InventoryService.Format(r.inputs)}."; return false; }
            return true;
        }

        public Result Enqueue(PlacedObjectState o, RecipeDefinition r)
        {
            if (!CanEnqueue(o, r, out var reason)) return Result.Fail(reason);
            _s.Inventory.TryRemove(r.inputs);
            if (o.production.queue.Count == 0) o.production.currentStartUnix = TimeService.UnixNow;
            o.production.queue.Add(r.Id);
            EventBus.Publish(new ObjectChanged(o.instanceId, false));
            return Result.Success;
        }

        /// <summary>Sekunder kvar på receptet som görs just nu.</summary>
        public double CurrentRemaining(PlacedObjectState o)
        {
            if (!IsWorking(o)) return 0;
            var r = Recipe(o.production.queue[0]);
            return r == null ? 0 : TimeService.Remaining(o.production.currentStartUnix, r.durationSec);
        }

        public float CurrentProgress(PlacedObjectState o)
        {
            if (!IsWorking(o)) return 0f;
            var r = Recipe(o.production.queue[0]);
            return r == null ? 1f : TimeService.Progress(o.production.currentStartUnix, r.durationSec);
        }

        public Result Collect(PlacedObjectState o)
        {
            if (ReadyCount(o) == 0) return Result.Fail("Inget att hämta.");
            foreach (var id in o.production.ready)
            {
                var r = Recipe(id);
                if (r == null || r.output.resource == null) continue;
                _s.Inventory.Add(r.output.resource.Id, r.output.amount);
                EventBus.Publish(new ItemsProduced(r.output.resource.Id, r.output.amount));
                _s.Progression.AddXp(r.xpReward);
            }
            o.production.ready.Clear();
            EventBus.Publish(new ObjectChanged(o.instanceId, false));
            return Result.Success;
        }

        public int SpeedUpCost(PlacedObjectState o) => IsWorking(o) ? TimeService.SpeedUpGemCost(CurrentRemaining(o)) : 0;

        public Result SpeedUp(PlacedObjectState o)
        {
            if (!IsWorking(o)) return Result.Fail("Inget pågår.");
            if (!_s.Wallet.TrySpend(CurrencyType.Gems, SpeedUpCost(o))) return Result.Fail("Inte tillräckligt med diamanter.");
            var r = Recipe(o.production.queue[0]);
            o.production.currentStartUnix = TimeService.UnixNow - (r?.durationSec ?? 0);
            Tick();
            return Result.Success;
        }

        public void Tick()
        {
            long now = TimeService.UnixNow;
            foreach (var o in _s.World.Objects)
            {
                var p = o.production;
                if (p == null || p.queue.Count == 0) continue;
                bool changed = false;
                while (p.queue.Count > 0)
                {
                    var r = Recipe(p.queue[0]);
                    long end = p.currentStartUnix + (r?.durationSec ?? 0);
                    if (now < end) break;
                    p.ready.Add(p.queue[0]);
                    p.queue.RemoveAt(0);
                    p.currentStartUnix = end; // nästa i kön startade när det förra blev klart
                    changed = true;
                }
                if (changed) EventBus.Publish(new ObjectChanged(o.instanceId, false));
            }
        }
    }
}
