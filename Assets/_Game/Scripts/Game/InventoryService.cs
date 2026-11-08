using System.Collections.Generic;
using System.Text;
using NG.Core;
using NG.Data;

namespace NG.Game
{
    /// <summary>Lagret: skördade och förädlade varor, räknat per resurs-id.</summary>
    public sealed class InventoryService
    {
        readonly GameSession _s;
        public InventoryService(GameSession s) { _s = s; }

        public IReadOnlyDictionary<string, int> All => _s.State.storage;

        // ---------- Förrådets storlek ----------

        public int Capacity => _s.Config.storageBaseCapacity + _s.State.storageLevel * _s.Config.storagePerUpgrade;

        public int Total
        {
            get
            {
                int sum = 0;
                foreach (var kv in _s.State.storage) sum += kv.Value;
                return sum;
            }
        }

        public bool HasRoom(int amount) => Total + amount <= Capacity;

        public int UpgradeCost => _s.Config.storageUpgradeCost * (_s.State.storageLevel + 1);

        public Result UpgradeStorage()
        {
            if (!_s.Wallet.TrySpend(CurrencyType.Coins, UpgradeCost)) return Result.Fail("Inte tillräckligt med mynt.");
            _s.State.storageLevel++;
            EventBus.Publish(new InventoryChanged(null, 0));
            GameSession.Message($"Förrådet rymmer nu {Capacity} varor.");
            return Result.Success;
        }

        // ---------- Sälja ----------

        public int PriceOf(string resourceId) =>
            _s.Content.TryGet<ResourceDefinition>(resourceId, out var r) ? r.sellPrice : 0;

        /// <summary>Säljer varor ur förrådet för mynt (grundpriset i resources.csv).</summary>
        public Result Sell(string resourceId, int amount)
        {
            amount = System.Math.Min(amount, Count(resourceId));
            if (amount <= 0) return Result.Fail("Du har inga sådana varor.");
            int coins = PriceOf(resourceId) * amount;
            Add(resourceId, -amount);
            _s.Wallet.Add(CurrencyType.Coins, coins);
            GameSession.Message($"Sålde {amount} {NameOf(resourceId)} för {coins} mynt.");
            return Result.Success;
        }

        public int Count(string resourceId) =>
            resourceId != null && _s.State.storage.TryGetValue(resourceId, out var n) ? n : 0;

        public bool Has(IEnumerable<ResourceAmount> amounts)
        {
            if (amounts == null) return true;
            foreach (var a in amounts)
                if (a.resource != null && Count(a.resource.Id) < a.amount) return false;
            return true;
        }

        public bool TryRemove(IEnumerable<ResourceAmount> amounts)
        {
            if (!Has(amounts)) return false;
            if (amounts != null)
                foreach (var a in amounts)
                    if (a.resource != null) Add(a.resource.Id, -a.amount);
            return true;
        }

        public void Add(string resourceId, int amount)
        {
            if (string.IsNullOrEmpty(resourceId) || amount == 0) return;
            int value = Count(resourceId) + amount;
            if (value <= 0) _s.State.storage.Remove(resourceId);
            else _s.State.storage[resourceId] = value;
            EventBus.Publish(new InventoryChanged(resourceId, value < 0 ? 0 : value));
        }

        public string NameOf(string resourceId) =>
            _s.Content.Get<ResourceDefinition>(resourceId)?.DisplayName ?? resourceId;

        /// <summary>"3 Vete, 2 Äpple".</summary>
        public static string Format(IEnumerable<ResourceAmount> amounts)
        {
            var sb = new StringBuilder();
            if (amounts != null)
                foreach (var a in amounts)
                {
                    if (a.resource == null) continue;
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(a.amount).Append(' ').Append(a.resource.DisplayName);
                }
            return sb.ToString();
        }
    }
}
