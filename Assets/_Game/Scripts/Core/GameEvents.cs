namespace NG.Core
{
    public readonly struct ObjectPlaced
    {
        public readonly string PlotId;
        public readonly string InstanceId;
        public ObjectPlaced(string plotId, string instanceId) { PlotId = plotId; InstanceId = instanceId; }
    }

    public readonly struct RenovationPhaseCompleted
    {
        public readonly string PlotId;
        public readonly int Phase;
        public RenovationPhaseCompleted(string plotId, int phase) { PlotId = plotId; Phase = phase; }
    }

    public readonly struct WalletChanged
    {
        public readonly NG.Data.CurrencyType Currency;
        public readonly long NewAmount;
        public WalletChanged(NG.Data.CurrencyType currency, long newAmount) { Currency = currency; NewAmount = newAmount; }
    }

    public readonly struct LevelUp
    {
        public readonly int NewLevel;
        public LevelUp(int newLevel) { NewLevel = newLevel; }
    }
}
