using System;
using UnityEngine;

namespace NG.Data
{
    public enum PlaceableCategory
    {
        House,
        Plant,
        Tree,
        Hedge,
        Fence,
        Decor,
        Water,
        Path,
        Production
    }

    public enum CurrencyType
    {
        Coins,
        Gems,
        EventPetals
    }

    [Flags]
    public enum StyleTag
    {
        None = 0,
        Romantic = 1 << 0,
        Modern = 1 << 1,
        Water = 1 << 2,
        Japanese = 1 << 3,
        Mediterranean = 1 << 4,
        Cottage = 1 << 5,
        Wild = 1 << 6,
        Kitchen = 1 << 7
    }

    [Flags]
    public enum BloomSeason
    {
        None = 0,
        Spring = 1 << 0,
        Summer = 1 << 1,
        Autumn = 1 << 2,
        Winter = 1 << 3,
        All = Spring | Summer | Autumn | Winter
    }

    [Serializable]
    public struct CurrencyCost
    {
        public CurrencyType currency;
        [Min(0)] public int amount;
    }

    [Serializable]
    public struct ResourceAmount
    {
        public ResourceDefinition resource;
        [Min(1)] public int amount;
    }

    /// <summary>En fas i en renovering. Variantnamnet matchar barnobjektet i FBX:en (t.ex. "Phase1_Repaired").</summary>
    [Serializable]
    public class RenovationPhase
    {
        public string variantName = "Phase1_Repaired";
        [Min(0)] public int durationSec = 120;
        public CurrencyCost cost;
        public ResourceAmount[] materials = Array.Empty<ResourceAmount>();
        [Min(0)] public int xpReward = 10;
    }

    [Serializable]
    public class PaintScheme
    {
        public string id = "classic";
        public Color color = Color.white;
        [Min(1)] public int unlockLevel = 1;
    }
}
