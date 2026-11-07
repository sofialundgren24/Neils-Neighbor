using System;
using System.Globalization;
using System.Linq;
using NG.Data;
using UnityEditor;
using UnityEngine;

namespace NG.EditorTools
{
    /// <summary>
    /// Importerar Assets/_Game/Data/levels.csv (exporterad från balanskalkylarket) till LevelTable.asset.
    /// Kolumner: level,xp_to_next,coin_reward,gem_reward,unlock_ids  (unlock_ids separeras med ';').
    /// </summary>
    public static class BalanceImporter
    {
        const string CsvPath = NGEditorPaths.GameRoot + "/Data/levels.csv";
        const string TablePath = NGEditorPaths.GameRoot + "/Content/Progression/LevelTable.asset";

        [MenuItem("NG/Balance/Import levels.csv")]
        public static void ImportLevels()
        {
            var csv = AssetDatabase.LoadAssetAtPath<TextAsset>(CsvPath);
            if (csv == null)
            {
                Debug.LogError($"[NG Balance] Hittar inte {CsvPath}");
                return;
            }

            var table = AssetDatabase.LoadAssetAtPath<LevelTable>(TablePath);
            if (table == null)
            {
                NGEditorPaths.EnsureFolder(System.IO.Path.GetDirectoryName(TablePath).Replace('\\', '/'));
                table = ScriptableObject.CreateInstance<LevelTable>();
                AssetDatabase.CreateAsset(table, TablePath);
            }

            table.rows.Clear();
            var lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++) // rad 0 = rubriker
            {
                var cols = lines[i].Split(',');
                if (cols.Length < 4) continue;
                var row = new LevelRow
                {
                    level = Parse(cols[0]),
                    xpToNext = Parse(cols[1]),
                    coinReward = Parse(cols[2]),
                    gemReward = Parse(cols[3]),
                    unlockIds = cols.Length > 4
                        ? cols[4].Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList()
                        : new System.Collections.Generic.List<string>()
                };
                if (row.level != table.rows.Count + 1)
                {
                    Debug.LogError($"[NG Balance] Rad {i + 1}: nivå {row.level} kommer i fel ordning (väntade {table.rows.Count + 1}). Avbryter.");
                    return;
                }
                table.rows.Add(row);
            }

            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NG Balance] Importerade {table.rows.Count} nivåer till {TablePath}.", table);
        }

        static int Parse(string s) => int.Parse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
}
