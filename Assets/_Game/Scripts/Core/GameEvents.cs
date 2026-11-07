using NG.Data;

namespace NG.Core
{
    // Händelser som spellogiken (Scripts/Game) publicerar på EventBus och som vyer och UI lyssnar på.
    // Logiken vet aldrig vem som lyssnar, så grafik och UI kan bytas utan att reglerna ändras.

    /// <summary>Ett nytt objekt har lagts på kartan.</summary>
    public readonly struct ObjectPlaced
    {
        public readonly string InstanceId;
        public ObjectPlaced(string instanceId) { InstanceId = instanceId; }
    }

    public readonly struct ObjectMoved
    {
        public readonly string InstanceId;
        public ObjectMoved(string instanceId) { InstanceId = instanceId; }
    }

    public readonly struct ObjectRemoved
    {
        public readonly string InstanceId;
        public ObjectRemoved(string instanceId) { InstanceId = instanceId; }
    }

    /// <summary>Objektets tillstånd har ändrats (byggt klart, ny fas, nytt tillväxtstadium, skördat ...).</summary>
    public readonly struct ObjectChanged
    {
        public readonly string InstanceId;
        /// <summary>Sant om vyn ska animera ändringen (färgvåg osv.).</summary>
        public readonly bool Animate;
        public ObjectChanged(string instanceId, bool animate) { InstanceId = instanceId; Animate = animate; }
    }

    public readonly struct RenovationCompleted
    {
        public readonly string InstanceId;
        public RenovationCompleted(string instanceId) { InstanceId = instanceId; }
    }

    public readonly struct WalletChanged
    {
        public readonly CurrencyType Currency;
        public readonly long NewAmount;
        public WalletChanged(CurrencyType currency, long newAmount) { Currency = currency; NewAmount = newAmount; }
    }

    public readonly struct InventoryChanged
    {
        public readonly string ResourceId;
        public readonly int NewCount;
        public InventoryChanged(string resourceId, int newCount) { ResourceId = resourceId; NewCount = newCount; }
    }

    public readonly struct XpChanged
    {
        public readonly long Xp;
        public readonly int Level;
        public XpChanged(long xp, int level) { Xp = xp; Level = level; }
    }

    public readonly struct LevelUp
    {
        public readonly int NewLevel;
        public LevelUp(int newLevel) { NewLevel = newLevel; }
    }

    public readonly struct OrdersChanged { }

    public readonly struct ExpansionUnlocked
    {
        public readonly string ExpansionId;
        public ExpansionUnlocked(string expansionId) { ExpansionId = expansionId; }
    }

    /// <summary>Kort meddelande till spelaren, t.ex. "Inte tillräckligt med mynt".</summary>
    public readonly struct GameMessage
    {
        public readonly string Text;
        public GameMessage(string text) { Text = text; }
    }
}
