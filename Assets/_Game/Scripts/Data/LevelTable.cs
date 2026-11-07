using System;
using System.Collections.Generic;
using UnityEngine;

namespace NG.Data
{
    /// <summary>XP-kurvan från GDD:n: XP till nästa nivå = round(50 * L^1.55 + 25 * L).</summary>
    public static class XpFormula
    {
        public static int XpToNext(int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            return (int)Math.Round(50.0 * Math.Pow(level, 1.55) + 25.0 * level);
        }
    }

    [Serializable]
    public class LevelRow
    {
        public int level;
        public int xpToNext;
        public int coinReward;
        public int gemReward;
        [Tooltip("Id:n på definitioner som låses upp när spelaren NÅR denna nivå.")]
        public List<string> unlockIds = new List<string>();
    }

    /// <summary>En rad per nivå. Fylls från Assets/_Game/Data/levels.csv via NG > Balance > Import Levels.</summary>
    [CreateAssetMenu(menuName = "NG/Level Table", fileName = "LevelTable")]
    public class LevelTable : ScriptableObject
    {
        public List<LevelRow> rows = new List<LevelRow>();

        public int MaxLevel => rows.Count == 0 ? 0 : rows[rows.Count - 1].level;

        /// <summary>Raden för en nivå. Tabellen får börja på nivå 0 (tutorialen).</summary>
        public LevelRow Get(int level)
        {
            foreach (var r in rows)
                if (r.level == level) return r;
            return null;
        }

        /// <summary>XP till nästa nivå; faller tillbaka på formeln bortom tabellen så att spelet aldrig tar slut.</summary>
        public int XpToNext(int level)
        {
            var row = Get(level);
            return row != null && row.xpToNext > 0 ? row.xpToNext : XpFormula.XpToNext(System.Math.Max(1, level));
        }
    }
}
