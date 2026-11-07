using System;
using System.Collections.Generic;
using NG.Data;

namespace NG.State
{
    /// <summary>
    /// Spelarens hela tillstånd som ren data (inga Unity-objekt) så att det kan serialiseras,
    /// testas och synkas till molnet. Referenser till innehåll sker alltid via stabila id:n.
    /// </summary>
    [Serializable]
    public class PlayerState
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public long savedAtUnix;

        public int level = 1;
        public long xp;

        public Dictionary<CurrencyType, long> wallet = new Dictionary<CurrencyType, long>
        {
            { CurrencyType.Coins, 500 },
            { CurrencyType.Gems, 20 }
        };

        /// <summary>Resurs-id -> antal.</summary>
        public Dictionary<string, int> storage = new Dictionary<string, int>();

        public List<string> unlockedDistricts = new List<string>();

        /// <summary>Tomt-id -> tomtens tillstånd.</summary>
        public Dictionary<string, PlotState> plots = new Dictionary<string, PlotState>();

        public List<ProductionSlotState> production = new List<ProductionSlotState>();
    }

    [Serializable]
    public class PlotState
    {
        public string plotId;
        public int renovationPhase;          // 0 = förfallen, 1..N = klar fas
        public TimerState activeRenovation;  // null om ingen pågår
        public string paintSchemeId;
        public float decay = 1f;             // 1 = grå/sliten, 0 = full färg
        public int stars;
        public List<PlacedObjectState> objects = new List<PlacedObjectState>();
    }

    [Serializable]
    public class PlacedObjectState
    {
        public string instanceId = Guid.NewGuid().ToString("N");
        public string definitionId;
        public int x;
        public int y;
        public int rotation;                 // 0..3 (x 90°)
        public string colorVariant;
        public TimerState growth;            // växter: tillväxttimer
        public int growthStage;              // 0 Sprout, 1 Growing, 2 Bloom
    }

    [Serializable]
    public class ProductionSlotState
    {
        public string buildingId;
        public List<string> queue = new List<string>();   // recept-id
        public TimerState current;
    }

    /// <summary>Timer som absolut UTC-start + längd – aldrig en nedräkning.</summary>
    [Serializable]
    public class TimerState
    {
        public long startUnix;
        public int durationSec;

        public TimerState() { }
        public TimerState(long startUnix, int durationSec) { this.startUnix = startUnix; this.durationSec = durationSec; }
    }
}
