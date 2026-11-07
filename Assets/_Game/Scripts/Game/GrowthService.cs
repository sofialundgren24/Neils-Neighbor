using System.Collections.Generic;
using NG.Core;
using NG.Data;
using NG.State;

namespace NG.Game
{
    /// <summary>
    /// Växter: Stage0_Sprout -> Stage1_Growing (halva tiden) -> Stage2_Bloom (klar).
    /// Grödor och blommor med skörd kan skördas i Stage2 och börjar sedan växa igen.
    /// </summary>
    public sealed class GrowthService
    {
        public static readonly string[] StageVariants = { "Stage0_Sprout", "Stage1_Growing", "Stage2_Bloom" };

        readonly GameSession _s;
        readonly Dictionary<string, int> _lastStage = new Dictionary<string, int>();

        public GrowthService(GameSession s) { _s = s; }

        public PlantDefinition Def(PlacedObjectState o) => _s.World.Def(o) as PlantDefinition;

        public int Stage(PlacedObjectState o)
        {
            if (o.growth == null) return 2;
            float p = Timers.Progress(o.growth);
            return p >= 1f ? 2 : p >= 0.5f ? 1 : 0;
        }

        public bool IsGrowing(PlacedObjectState o) => o.growth != null && !Timers.IsDone(o.growth);

        public bool CanHarvest(PlacedObjectState o)
        {
            var def = Def(o);
            return def != null && def.IsHarvestable && !o.decayed && o.build == null && Stage(o) == 2;
        }

        public Result Harvest(PlacedObjectState o)
        {
            if (!CanHarvest(o)) return Result.Fail("Inte redo att skördas än.");
            var def = Def(o);
            _s.Inventory.Add(def.harvest.resource.Id, def.harvest.amount);
            EventBus.Publish(new ItemsHarvested(def.harvest.resource.Id, def.harvest.amount));
            _s.Progression.AddXp(def.harvest.amount);
            o.growth = Timers.StartNow(def.growTimeSec);
            _lastStage[o.instanceId] = 0;
            EventBus.Publish(new ObjectChanged(o.instanceId, false));
            return Result.Success;
        }

        public int SpeedUpCost(PlacedObjectState o) =>
            IsGrowing(o) ? TimeService.SpeedUpGemCost(Timers.Remaining(o.growth)) : 0;

        public Result SpeedUp(PlacedObjectState o)
        {
            if (!IsGrowing(o)) return Result.Fail("Växten är redan fullvuxen.");
            if (!_s.Wallet.TrySpend(CurrencyType.Gems, SpeedUpCost(o))) return Result.Fail("Inte tillräckligt med diamanter.");
            Timers.Finish(o.growth);
            Tick();
            return Result.Success;
        }

        /// <summary>Säger till vyn när en växt byter stadium så att rätt variant visas.</summary>
        public void Tick()
        {
            foreach (var o in _s.World.Objects)
            {
                if (o.growth == null) continue;
                int stage = Stage(o);
                if (_lastStage.TryGetValue(o.instanceId, out var last) && last == stage) continue;
                bool known = _lastStage.ContainsKey(o.instanceId);
                _lastStage[o.instanceId] = stage;
                if (known) EventBus.Publish(new ObjectChanged(o.instanceId, false));
            }
        }
    }
}
