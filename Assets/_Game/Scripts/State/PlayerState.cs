using System;
using System.Collections.Generic;
using NG.Data;

namespace NG.State
{
    /// <summary>
    /// Spelarens hela tillstånd som ren data (inga Unity-objekt) så att det kan serialiseras,
    /// testas och synkas till molnet. Referenser till innehåll sker alltid via stabila id:n.
    /// Reglerna som ändrar datan finns i Scripts/Game (GameSession och dess tjänster).
    /// </summary>
    [Serializable]
    public class PlayerState
    {
        public const int CurrentSchemaVersion = 3;

        public int schemaVersion = CurrentSchemaVersion;
        public long savedAtUnix;

        /// <summary>Falskt tills startgatan har lagts ut första gången.</summary>
        public bool initialized;

        /// <summary>Vilken version av villkoren spelaren har godkänt (0 = inte godkänt än).</summary>
        public int termsAcceptedVersion;
        /// <summary>Spelarens karaktär: "neil" eller "sofia". Tomt = inte valt än.</summary>
        public string character;

        /// <summary>Nivå 0 = tutorialen (rensa ogräs, vattna gräs).</summary>
        public int level;
        public long xp;

        public Dictionary<CurrencyType, long> wallet = new Dictionary<CurrencyType, long>();

        /// <summary>Resurs-id -> antal i lagret.</summary>
        public Dictionary<string, int> storage = new Dictionary<string, int>();

        /// <summary>Allt som står på kartan.</summary>
        public List<PlacedObjectState> objects = new List<PlacedObjectState>();

        public List<string> unlockedExpansions = new List<string>();

        public List<OrderState> orders = new List<OrderState>();
        public long nextOrderUnix;

        /// <summary>Pågående uppdrag.</summary>
        public List<QuestProgress> quests = new List<QuestProgress>();
        public List<string> completedQuests = new List<string>();

        public DailyRewardState daily = new DailyRewardState();
    }

    [Serializable]
    public class QuestProgress
    {
        public string questId;
        /// <summary>Räknare per mål (bara för mål som räknas med händelser: skörd, produktion, leveranser).</summary>
        public List<int> counters = new List<int>();
        /// <summary>Sant när spelaren har sett grannens introduktion.</summary>
        public bool introSeen;
    }

    [Serializable]
    public class DailyRewardState
    {
        /// <summary>Antal dagar i rad (1..7, börjar om efter 7).</summary>
        public int streak;
        /// <summary>UTC-dagnummer (Unix-sekunder / 86400) då belöningen senast hämtades.</summary>
        public long lastClaimDay = -1;
    }

    [Serializable]
    public class PlacedObjectState
    {
        public string instanceId = Guid.NewGuid().ToString("N");
        public string definitionId;
        public int x;
        public int y;
        public int rotation;                  // 0..3 (x 90°)

        /// <summary>Byggtimer. Null när objektet är färdigbyggt.</summary>
        public TimerState build;

        /// <summary>Grå och sliten (bara objekt på startgatan). Försvinner för gott när närmaste hus är renoverat.</summary>
        public bool decayed;
        /// <summary>Grå tills ALLA hus är renoverade (gatan, trottoarer, lyktor, stora träd).</summary>
        public bool restoreWithStreet;

        /// <summary>Kvartersbyggnader (lekpark, skola): tid tills nästa intäkt kan hämtas.</summary>
        public TimerState income;

        /// <summary>Bara hus (RenovatableDefinition).</summary>
        public RenovationState renovation;

        /// <summary>Bara växter: tillväxttimer sedan sådd eller senaste skörd. Null = fullvuxen.</summary>
        public TimerState growth;

        /// <summary>Bara produktionsbyggnader.</summary>
        public ProductionState production;
    }

    [Serializable]
    public class RenovationState
    {
        /// <summary>0 = förfallet, 1..N = antal klara faser.</summary>
        public int phase;
        /// <summary>Pågående fas. Null om ingen pågår.</summary>
        public TimerState active;
        /// <summary>Sant när sista fasen är klar. Då har huset full färg resten av spelet.</summary>
        public bool complete;
        /// <summary>Spelarens målarfärg som hex ("RRGGBB"). Tom = originalfärg.</summary>
        public string paintHex;
        /// <summary>Vald fasad (FacadeDefinition-id). Tom = originalfasaden.</summary>
        public string facadeId;
    }

    [Serializable]
    public class ProductionState
    {
        /// <summary>Recept-id i kö. Det första är på gång och startade vid currentStartUnix.</summary>
        public List<string> queue = new List<string>();
        public long currentStartUnix;
        /// <summary>Färdiga recept som väntar på att hämtas.</summary>
        public List<string> ready = new List<string>();
    }

    [Serializable]
    public class OrderState
    {
        public string orderId = Guid.NewGuid().ToString("N");
        /// <summary>Grannen som vill ha varorna (NeighborDefinition-id).</summary>
        public string neighborId;
        public List<ResourceStack> items = new List<ResourceStack>();
        public int coins;
        public int xp;
    }

    [Serializable]
    public class ResourceStack
    {
        public string resourceId;
        public int amount;

        public ResourceStack() { }
        public ResourceStack(string resourceId, int amount) { this.resourceId = resourceId; this.amount = amount; }
    }

    /// <summary>Timer som absolut UTC-start + längd – aldrig en nedräkning. Fortsätter när appen är stängd.</summary>
    [Serializable]
    public class TimerState
    {
        public long startUnix;
        public int durationSec;

        public TimerState() { }
        public TimerState(long startUnix, int durationSec) { this.startUnix = startUnix; this.durationSec = durationSec; }
    }
}
