using NG.Core;
using NG.Data;
using NG.State;

namespace NG.Game
{
    /// <summary>
    /// Kvartersbyggnader (lekparken, skolan): när de är byggda ger de mynt och XP med jämna mellanrum.
    /// Spelaren hämtar intäkten med ett tryck, sedan börjar nästa period.
    /// </summary>
    public sealed class CommunityService
    {
        readonly GameSession _s;
        public CommunityService(GameSession s) { _s = s; }

        public CommunityDefinition Def(PlacedObjectState o) => _s.World.Def(o) as CommunityDefinition;

        public bool IsCommunity(PlacedObjectState o) => Def(o) != null;

        public bool CanCollect(PlacedObjectState o) =>
            o.build == null && o.income != null && Timers.IsDone(o.income) && Def(o) != null;

        public double Remaining(PlacedObjectState o) => o.income == null ? 0 : Timers.Remaining(o.income);

        public Result Collect(PlacedObjectState o)
        {
            if (!CanCollect(o)) return Result.Fail("Inte redo än.");
            var def = Def(o);
            _s.Wallet.Add(CurrencyType.Coins, def.incomeCoins);
            _s.Progression.AddXp(def.incomeXp);
            o.income = Timers.StartNow(def.incomeIntervalSec);
            EventBus.Publish(new ObjectChanged(o.instanceId, false));
            _s.Float(o, $"+{def.incomeCoins} mynt");
            return Result.Success;
        }

        /// <summary>Startar intäktstimern för en byggnad som just blivit klar.</summary>
        public void StartIncome(PlacedObjectState o)
        {
            var def = Def(o);
            if (def != null && o.income == null) o.income = Timers.StartNow(def.incomeIntervalSec);
        }
    }
}
