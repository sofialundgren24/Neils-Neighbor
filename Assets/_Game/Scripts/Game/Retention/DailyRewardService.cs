using NG.Core;
using NG.Data;

namespace NG.Game
{
    /// <summary>
    /// Daglig belöning: en gåva per UTC-dygn. Dagar i rad ger större gåvor (dag 7 är störst), en missad dag börjar om.
    /// Beloppen står i GameConfig (dailyCoins / dailyGems) och kan ändras i inspektorn.
    /// </summary>
    public sealed class DailyRewardService
    {
        readonly GameSession _s;
        public DailyRewardService(GameSession s) { _s = s; }

        static long Today => TimeService.UnixNow / 86400;

        public bool CanClaim => Today > _s.State.daily.lastClaimDay;

        /// <summary>Vilken dag (1-7) nästa gåva gäller.</summary>
        public int NextDay
        {
            get
            {
                var d = _s.State.daily;
                bool continues = d.lastClaimDay == Today - 1;
                int day = continues ? d.streak + 1 : 1;
                return day > 7 ? 1 : day;
            }
        }

        public int CoinsFor(int day) => Pick(_s.Config.dailyCoins, day);
        public int GemsFor(int day) => Pick(_s.Config.dailyGems, day);

        static int Pick(int[] table, int day) =>
            table == null || table.Length == 0 ? 0 : table[(day - 1) % table.Length];

        public Result Claim()
        {
            if (!CanClaim) return Result.Fail("Du har redan hämtat dagens gåva.");
            int day = NextDay;
            _s.Wallet.Add(CurrencyType.Coins, CoinsFor(day));
            _s.Wallet.Add(CurrencyType.Gems, GemsFor(day));
            _s.State.daily.streak = day;
            _s.State.daily.lastClaimDay = Today;
            return Result.Success;
        }
    }
}
