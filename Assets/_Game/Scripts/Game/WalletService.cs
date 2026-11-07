using System.Collections.Generic;
using System.Text;
using NG.Core;
using NG.Data;

namespace NG.Game
{
    /// <summary>Mynt (mjuk valuta) och diamanter (hård valuta).</summary>
    public sealed class WalletService
    {
        readonly GameSession _s;
        public WalletService(GameSession s) { _s = s; }

        public long Get(CurrencyType currency) =>
            _s.State.wallet.TryGetValue(currency, out var v) ? v : 0;

        public bool CanAfford(IEnumerable<CurrencyCost> costs)
        {
            if (costs == null) return true;
            foreach (var c in costs)
                if (Get(c.currency) < c.amount) return false;
            return true;
        }

        public bool CanAfford(CurrencyType currency, long amount) => Get(currency) >= amount;

        public bool TrySpend(IEnumerable<CurrencyCost> costs)
        {
            if (!CanAfford(costs)) return false;
            if (costs != null)
                foreach (var c in costs) Add(c.currency, -c.amount);
            return true;
        }

        public bool TrySpend(CurrencyType currency, long amount)
        {
            if (!CanAfford(currency, amount)) return false;
            Add(currency, -amount);
            return true;
        }

        public void Add(CurrencyType currency, long amount)
        {
            if (amount == 0) return;
            long value = Get(currency) + amount;
            _s.State.wallet[currency] = value;
            EventBus.Publish(new WalletChanged(currency, value));
        }

        public static string Name(CurrencyType c) => c switch
        {
            CurrencyType.Coins => "mynt",
            CurrencyType.Gems => "diamanter",
            _ => c.ToString()
        };

        /// <summary>"120 mynt + 5 diamanter", eller "Gratis".</summary>
        public static string Format(IEnumerable<CurrencyCost> costs)
        {
            var sb = new StringBuilder();
            if (costs != null)
                foreach (var c in costs)
                {
                    if (c.amount <= 0) continue;
                    if (sb.Length > 0) sb.Append(" + ");
                    sb.Append(c.amount).Append(' ').Append(Name(c.currency));
                }
            return sb.Length == 0 ? "Gratis" : sb.ToString();
        }
    }
}
