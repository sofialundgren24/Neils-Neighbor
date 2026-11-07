using NG.Core;
using NG.Data;
using NG.State;
using UnityEngine;

namespace NG.Game
{
    /// <summary>
    /// Husens renovering: Phase0_Decayed -> Phase1_Repaired -> Phase2_Painted -> Phase3_Decorated.
    /// Varje fas kostar mynt (och ev. material) och tar tid. När sista fasen är klar låses huset i full färg
    /// för gott, och allt grått inom GameConfig.restoreRadius får också tillbaka färgen.
    /// </summary>
    public sealed class RenovationService
    {
        /// <summary>Hur grått ett orenoverat hus är efter 0, 1, 2 ... klara faser.</summary>
        static readonly float[] DecayByPhase = { 1f, 0.7f, 0.4f, 0.2f };

        readonly GameSession _s;
        public RenovationService(GameSession s) { _s = s; }

        public RenovatableDefinition Def(PlacedObjectState o) => _s.World.Def(o) as RenovatableDefinition;

        public bool IsRenovatable(PlacedObjectState o) => o?.renovation != null && Def(o) != null;

        public bool IsComplete(PlacedObjectState o) => o?.renovation == null || o.renovation.complete;

        public bool IsInProgress(PlacedObjectState o) => o?.renovation?.active != null;

        public int PhaseCount(PlacedObjectState o) => Def(o)?.phases.Length ?? 0;

        /// <summary>Nästa fas som kan startas, eller null om huset är klart eller en fas pågår.</summary>
        public RenovationPhase NextPhase(PlacedObjectState o)
        {
            var def = Def(o);
            if (def == null || IsComplete(o) || IsInProgress(o)) return null;
            int i = o.renovation.phase;
            return i >= 0 && i < def.phases.Length ? def.phases[i] : null;
        }

        /// <summary>Barnobjektet i modellen som ska synas just nu.</summary>
        public string CurrentVariant(PlacedObjectState o)
        {
            var def = Def(o);
            if (def == null) return null;
            int phase = o.renovation.phase;
            if (phase <= 0) return def.decayedVariant;
            return def.phases[Mathf.Clamp(phase - 1, 0, def.phases.Length - 1)].variantName;
        }

        /// <summary>Gråhet för vyn: 0 när huset är klart, annars enligt DecayByPhase.</summary>
        public float CurrentDecay(PlacedObjectState o)
        {
            if (IsComplete(o)) return 0f;
            int phase = Mathf.Clamp(o.renovation.phase, 0, DecayByPhase.Length - 1);
            return DecayByPhase[phase];
        }

        public bool CanStartNext(PlacedObjectState o, out string reason)
        {
            reason = null;
            var def = Def(o);
            var next = NextPhase(o);
            if (def == null || next == null) { reason = IsInProgress(o) ? "Renovering pågår redan." : "Huset är redan klart."; return false; }
            if (_s.Progression.Level < def.unlockLevel) { reason = $"Kräver nivå {def.unlockLevel}."; return false; }
            if (_s.Wallet.Get(next.cost.currency) < next.cost.amount) { reason = $"Behöver {next.cost.amount} {WalletService.Name(next.cost.currency)}."; return false; }
            if (!_s.Inventory.Has(next.materials)) { reason = $"Behöver {InventoryService.Format(next.materials)}."; return false; }
            return true;
        }

        public Result StartNext(PlacedObjectState o)
        {
            if (!CanStartNext(o, out var reason)) return Result.Fail(reason);
            var next = NextPhase(o);
            _s.Wallet.TrySpend(next.cost.currency, next.cost.amount);
            _s.Inventory.TryRemove(next.materials);
            o.renovation.active = Timers.StartNow(next.durationSec);
            EventBus.Publish(new ObjectChanged(o.instanceId, false));
            Tick(); // faser med 0 sekunder blir klara direkt
            return Result.Success;
        }

        public int SpeedUpCost(PlacedObjectState o) =>
            IsInProgress(o) ? TimeService.SpeedUpGemCost(Timers.Remaining(o.renovation.active)) : 0;

        public Result SpeedUp(PlacedObjectState o)
        {
            if (!IsInProgress(o)) return Result.Fail("Ingen renovering pågår.");
            if (!_s.Wallet.TrySpend(CurrencyType.Gems, SpeedUpCost(o))) return Result.Fail("Inte tillräckligt med diamanter.");
            Timers.Finish(o.renovation.active);
            Tick();
            return Result.Success;
        }

        /// <summary>Målarfärg på ett färdigt hus. Null = originalfärgen.</summary>
        public Result SetPaint(PlacedObjectState o, Color? color)
        {
            if (!IsComplete(o) || o.renovation == null) return Result.Fail("Renovera klart huset först.");
            o.renovation.paintHex = color.HasValue ? ColorUtility.ToHtmlStringRGB(color.Value) : null;
            EventBus.Publish(new ObjectChanged(o.instanceId, false));
            return Result.Success;
        }

        public void Tick()
        {
            foreach (var o in _s.World.Objects)
            {
                if (o.renovation?.active == null || !Timers.IsDone(o.renovation.active)) continue;
                FinishPhase(o);
            }
        }

        void FinishPhase(PlacedObjectState o)
        {
            var def = Def(o);
            o.renovation.active = null;
            o.renovation.phase++;
            if (def != null && o.renovation.phase - 1 < def.phases.Length)
                _s.Progression.AddXp(def.phases[o.renovation.phase - 1].xpReward);

            if (def == null || o.renovation.phase >= def.phases.Length)
            {
                o.renovation.complete = true;
                o.decayed = false;
                RestoreAround(o);
                EventBus.Publish(new ObjectChanged(o.instanceId, true));
                EventBus.Publish(new RenovationCompleted(o.instanceId));
                GameSession.Message($"{def?.DisplayName ?? "Huset"} är färdigrenoverat!");
            }
            else
            {
                EventBus.Publish(new ObjectChanged(o.instanceId, true));
            }
        }

        /// <summary>Allt grått nära huset får tillbaka färgen för gott (trädgård, staket, träd).</summary>
        void RestoreAround(PlacedObjectState house)
        {
            var center = _s.World.CellToWorld(_s.World.Def(house), house.x, house.y, house.rotation);
            float r2 = _s.Config.restoreRadius * _s.Config.restoreRadius;
            foreach (var o in _s.World.Objects)
            {
                if (!o.decayed || o == house) continue;
                var pos = _s.World.CellToWorld(_s.World.Def(o), o.x, o.y, o.rotation);
                if ((pos - center).sqrMagnitude > r2) continue;
                o.decayed = false;
                if (_s.World.Def(o) is PlantDefinition plant && o.growth == null)
                    o.growth = Timers.StartNow(plant.growTimeSec);
                EventBus.Publish(new ObjectChanged(o.instanceId, true));
            }
        }
    }
}
