using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NG.Data;
using NG.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace NG.EditorTools
{
    /// <summary>
    /// Bygger allt spelinnehåll från kalkylark (CSV) i Assets/_Game/Data:
    ///   resources.csv     varor (vete, bröd ...)
    ///   content.csv       allt som kan placeras: hus, butiker, växter, träd, dekor, staket, mark
    ///   recipes.csv       recept i affären och trädgårdsbutiken
    ///   start_layout.csv  den nedgångna startgatan
    ///   expansions.csv    områden som låses upp med nivåer
    /// För varje rad skapas/uppdateras en definition (DEF_/RES_/RCP_), och för objekt utan riktig modell
    /// en platshållar-prefab. CSV:en äger balansvärdena: ändra i kalkylarket och kör importen igen.
    /// Riktiga modeller (SM_*.fbx) ersätter platshållaren automatiskt via NGAssetImporter.
    /// </summary>
    public static class ContentImporter
    {
        [MenuItem("NG/Content/Import All (CSV -> definitions + placeholders)", priority = 10)]
        public static void ImportAll()
        {
            NGEditorPaths.EnsureMaterials();
            var resources = ImportResources();
            var placeables = ImportPlaceables(resources);
            var recipes = ImportRecipes(resources);
            LinkRecipes(placeables, recipes);
            ImportGameConfig(placeables);
            var neighbors = ImportNeighbors(placeables);
            int quests = ImportQuests(neighbors, placeables, resources);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NG Content] Klart: {resources.Count} varor, {placeables.Count} objekt, {recipes.Count} recept, " +
                      $"{neighbors.Count} grannar, {quests} uppdrag.");
        }

        // ---------- Grannar och uppdrag ----------

        static Dictionary<string, NeighborDefinition> ImportNeighbors(Dictionary<string, PlaceableDefinition> placeables)
        {
            var result = new Dictionary<string, NeighborDefinition>();
            foreach (var row in ReadCsv("neighbors.csv"))
            {
                string id = row["id"];
                var def = LoadOrCreate<NeighborDefinition>($"{NGEditorPaths.GameRoot}/Content/Neighbors/NB_{Title(id)}.asset", typeof(NeighborDefinition));
                def.EditorSetId(id);
                def.fallbackName = row["name"];
                def.house = placeables.TryGetValue(row["house"], out var h) ? h as RenovatableDefinition : null;
                if (def.house == null) Debug.LogWarning($"[NG Content] neighbors.csv: {id} bor i okänt hus '{row["house"]}'.");
                def.color = NGEditorPaths.Hex(row["color"]);
                def.bio = row["bio"];
                Register(def, $"neighbor/{id}");
                result[id] = def;
            }
            return result;
        }

        static int ImportQuests(Dictionary<string, NeighborDefinition> neighbors,
            Dictionary<string, PlaceableDefinition> placeables, Dictionary<string, ResourceDefinition> resources)
        {
            var ids = new HashSet<string>();
            var rows = ReadCsv("quests.csv");
            foreach (var row in rows) ids.Add(row["id"]);
            foreach (var row in rows)
            {
                string id = row["id"];
                var def = LoadOrCreate<QuestDefinition>($"{NGEditorPaths.GameRoot}/Content/Quests/Q_{Title(id)}.asset", typeof(QuestDefinition));
                def.EditorSetId(id);
                def.fallbackName = row["title"];
                def.neighbor = neighbors.TryGetValue(row["neighbor"], out var nb) ? nb : null;
                def.order = Int(row, "order", 0);
                def.unlockLevel = Int(row, "unlock", 1);
                def.requiresQuest = row["requires"];
                if (!string.IsNullOrEmpty(def.requiresQuest) && !ids.Contains(def.requiresQuest))
                    Debug.LogWarning($"[NG Content] quests.csv: {id} kräver okänt uppdrag '{def.requiresQuest}'.");
                def.intro = row["intro"];
                def.outro = row["outro"];
                def.rewardCoins = Int(row, "coins", 0);
                def.rewardGems = Int(row, "gems", 0);
                def.rewardXp = Int(row, "xp", 0);
                def.focus = row["focus"];
                def.objectives = row["objectives"].Split('|').Where(s => s.Trim().Length > 0)
                    .Select(s => Objective(s, id, placeables, resources)).ToArray();
                Register(def, $"quest/{id}");
            }
            return rows.Count;
        }

        /// <summary>"harvest:wheat:6" -> skörda 6 vete. Typer: renovate, own, harvest, produce, deliver, level, collect.</summary>
        static QuestObjective Objective(string text, string questId,
            Dictionary<string, PlaceableDefinition> placeables, Dictionary<string, ResourceDefinition> resources)
        {
            var p = text.Trim().Split(':');
            var o = new QuestObjective();
            switch (p[0].ToLowerInvariant())
            {
                case "renovate": o.type = ObjectiveType.Renovate; break;
                case "own": o.type = ObjectiveType.Own; break;
                case "harvest": o.type = ObjectiveType.Harvest; break;
                case "produce": o.type = ObjectiveType.Produce; break;
                case "deliver": o.type = ObjectiveType.Deliver; break;
                case "level": o.type = ObjectiveType.ReachLevel; break;
                case "collect": o.type = ObjectiveType.Collect; break;
                default: Debug.LogWarning($"[NG Content] quests.csv: {questId} har okänd måltyp '{p[0]}'."); break;
            }
            if (o.type == ObjectiveType.Deliver || o.type == ObjectiveType.ReachLevel)
            {
                o.amount = p.Length > 1 ? int.Parse(p[p.Length - 1], CultureInfo.InvariantCulture) : 1;
                return o;
            }
            o.targetId = p.Length > 1 ? p[1] : "*";
            o.amount = p.Length > 2 ? int.Parse(p[2], CultureInfo.InvariantCulture) : 1;
            bool known = o.targetId == "*" ||
                         (o.type == ObjectiveType.Renovate || o.type == ObjectiveType.Own ? placeables.ContainsKey(o.targetId) : resources.ContainsKey(o.targetId));
            if (!known) Debug.LogWarning($"[NG Content] quests.csv: {questId} pekar på okänt id '{o.targetId}'.");
            return o;
        }

        // ---------- Varor ----------

        static Dictionary<string, ResourceDefinition> ImportResources()
        {
            var result = new Dictionary<string, ResourceDefinition>();
            foreach (var row in ReadCsv("resources.csv"))
            {
                string id = row["id"];
                var def = LoadOrCreate<ResourceDefinition>($"{NGEditorPaths.Goods}/RES_{Title(id)}.asset", typeof(ResourceDefinition));
                def.EditorSetId(id);
                def.fallbackName = row["name"];
                def.unlockLevel = Int(row, "unlock", 1);
                def.sellPrice = Int(row, "price", 1);
                def.color = NGEditorPaths.Hex(row["color"]);
                Register(def, $"res/{id}");
                result[id] = def;
            }
            return result;
        }

        // ---------- Placerbara objekt ----------

        static Dictionary<string, PlaceableDefinition> ImportPlaceables(Dictionary<string, ResourceDefinition> resources)
        {
            var result = new Dictionary<string, PlaceableDefinition>();
            foreach (var row in ReadCsv("content.csv"))
            {
                string id = row["id"];
                string type = row["type"].ToLowerInvariant();
                var category = CategoryFor(type);
                string title = Title(id);
                string folder = NGEditorPaths.CategoryFolder(category);
                string defPath = $"{NGEditorPaths.Placeables}/{folder}/DEF_{title}.asset";
                string prefabPath = $"{NGEditorPaths.Prefabs}/{folder}/PF_{title}.prefab";
                var p = Params(row);

                var def = LoadOrCreate<PlaceableDefinition>(defPath, ClassFor(type));
                def.EditorSetId(id);
                def.fallbackName = row["name"];
                def.category = category;
                def.unlockLevel = Int(row, "unlock", 1);
                def.cost = Costs(Int(row, "coins", 0), Int(row, "gems", 0));
                def.buildTimeSec = Int(row, "build_sec", 0);
                def.xpReward = Int(row, "xp", 0);
                def.beautyScore = Int(row, "beauty", 0);
                def.inShop = Int(row, "shop", 1) != 0;
                def.maxCount = p.TryGetValue("max", out var max) ? int.Parse(max, CultureInfo.InvariantCulture) : 0;

                switch (def)
                {
                    case RenovatableDefinition house:
                        ConfigureHouse(house, p);
                        break;
                    case PlantDefinition plant:
                        plant.growTimeSec = p.TryGetValue("grow", out var grow) ? int.Parse(grow, CultureInfo.InvariantCulture) : 60;
                        plant.harvest = p.TryGetValue("yield", out var y) ? Amount(y, resources) : default;
                        break;
                    case ProductionDefinition prod:
                        prod.queueSize = p.TryGetValue("queue", out var q) ? int.Parse(q, CultureInfo.InvariantCulture) : 3;
                        break;
                }

                // Platshållare bara om det inte finns en riktig modell.
                NGEditorPaths.EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (existing == null || existing.GetComponent<PlaceholderMarker>() != null)
                {
                    var spec = new PlaceholderBuilder.Spec
                    {
                        id = id,
                        type = type,
                        footprint = new Vector2Int(Int(row, "w", 2), Int(row, "h", 2)),
                        height = Float(row, "height", 1f),
                        color = NGEditorPaths.Hex(row["color"]),
                        accent = def is PlantDefinition pd && pd.harvest.resource != null && type == "tree"
                            ? pd.harvest.resource.color : (Color?)null
                    };
                    PlaceholderBuilder.Build(spec, prefabPath);
                    def.footprint = spec.footprint;
                }

                var prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
                NGEditorPaths.RegisterAddressable(prefabPath, "Placeables", $"placeable/{id}", "placeables");
                def.prefab = new AssetReferenceGameObject(prefabGuid);
                Register(def, $"def/{id}");
                result[id] = def;
            }
            return result;
        }

        static void ConfigureHouse(RenovatableDefinition house, Dictionary<string, string> p)
        {
            string[] variants = { "Phase1_Repaired", "Phase2_Painted", "Phase3_Decorated" };
            int[] secs = IntList(p, "phases", new[] { 60, 300, 900 });
            int[] coins = IntList(p, "phase_coins", new[] { 50, 150, 300 });
            int[] xp = IntList(p, "phase_xp", new[] { 10, 20, 40 });
            house.decayedVariant = "Phase0_Decayed";
            house.phases = new RenovationPhase[variants.Length];
            for (int i = 0; i < variants.Length; i++)
            {
                house.phases[i] = new RenovationPhase
                {
                    variantName = variants[i],
                    durationSec = secs[Mathf.Min(i, secs.Length - 1)],
                    cost = new CurrencyCost { currency = CurrencyType.Coins, amount = coins[Mathf.Min(i, coins.Length - 1)] },
                    xpReward = xp[Mathf.Min(i, xp.Length - 1)]
                };
            }
        }

        // ---------- Recept ----------

        static Dictionary<string, (RecipeDefinition recipe, string building)> ImportRecipes(Dictionary<string, ResourceDefinition> resources)
        {
            var result = new Dictionary<string, (RecipeDefinition, string)>();
            foreach (var row in ReadCsv("recipes.csv"))
            {
                string id = row["id"];
                var def = LoadOrCreate<RecipeDefinition>($"{NGEditorPaths.Recipes}/RCP_{Title(id)}.asset", typeof(RecipeDefinition));
                def.EditorSetId(id);
                def.fallbackName = row["name"];
                def.unlockLevel = Int(row, "unlock", 1);
                def.inputs = row["inputs"].Split('/').Where(s => s.Length > 0).Select(s => Amount(s, resources)).ToArray();
                def.output = Amount(row["output"], resources);
                def.durationSec = Int(row, "sec", 60);
                def.xpReward = Int(row, "xp", 1);
                Register(def, $"recipe/{id}");
                result[id] = (def, row["building"]);
            }
            return result;
        }

        static void LinkRecipes(Dictionary<string, PlaceableDefinition> placeables,
            Dictionary<string, (RecipeDefinition recipe, string building)> recipes)
        {
            foreach (var prod in placeables.Values.OfType<ProductionDefinition>())
            {
                prod.recipes = recipes.Values.Where(r => r.building == prod.Id)
                    .Select(r => r.recipe).OrderBy(r => r.unlockLevel).ToArray();
                EditorUtility.SetDirty(prod);
            }
            foreach (var r in recipes.Values.Where(r => !placeables.ContainsKey(r.building)))
                Debug.LogWarning($"[NG Content] Receptet {r.recipe.Id} pekar på okänd byggnad '{r.building}'.");
        }

        // ---------- Karta ----------

        static void ImportGameConfig(Dictionary<string, PlaceableDefinition> placeables)
        {
            var config = LoadOrCreate<GameConfig>(NGEditorPaths.GameConfigPath, typeof(GameConfig));
            config.EditorSetId("game_config");
            config.fallbackName = "Spelinställningar";
            config.levelTable = AssetDatabase.LoadAssetAtPath<LevelTable>(NGEditorPaths.LevelTablePath);
            if (config.levelTable == null)
                Debug.LogWarning("[NG Content] LevelTable saknas. Kör NG > Balance > Import levels.csv.");

            config.startLayout.Clear();
            foreach (var row in ReadCsv("start_layout.csv"))
            {
                if (!placeables.TryGetValue(row["id"], out var def))
                {
                    Debug.LogWarning($"[NG Content] start_layout.csv: okänt id '{row["id"]}'.");
                    continue;
                }
                config.startLayout.Add(new StartPlacement
                {
                    definition = def,
                    cell = new Vector2Int(Int(row, "x", 0), Int(row, "y", 0)),
                    rotation = Int(row, "rot", 0),
                    decayed = Int(row, "decayed", 0) != 0
                });
            }

            config.expansions.Clear();
            foreach (var row in ReadCsv("expansions.csv"))
            {
                config.expansions.Add(new MapExpansion
                {
                    id = row["id"],
                    name = row["name"],
                    area = new RectInt(Int(row, "x", 0), Int(row, "y", 0), Int(row, "w", 1), Int(row, "h", 1)),
                    requiredLevel = Int(row, "level", 1),
                    coinCost = Int(row, "coins", 0)
                });
            }
            Register(config, "config/game");
            ValidateLayout(config);
        }

        static void ValidateLayout(GameConfig config)
        {
            var grid = new PlotGrid(config.mapSize.x, config.mapSize.y);
            foreach (var p in config.startLayout)
            {
                if (!grid.IsFree(p.cell.x, p.cell.y, p.definition.footprint, p.rotation))
                    Debug.LogWarning($"[NG Content] start_layout: {p.definition.Id} på ({p.cell.x},{p.cell.y}) överlappar eller hamnar utanför kartan.");
                grid.Occupy(p.definition.Id, p.cell.x, p.cell.y, p.definition.footprint, p.rotation);
            }
        }

        // ---------- Hjälpare ----------

        static PlaceableCategory CategoryFor(string type) => type switch
        {
            "house" => PlaceableCategory.House,
            "production" => PlaceableCategory.Production,
            "crop" or "flower" => PlaceableCategory.Plant,
            "tree" => PlaceableCategory.Tree,
            "bush" => PlaceableCategory.Hedge,
            "fence" => PlaceableCategory.Fence,
            "ground" => PlaceableCategory.Path,
            "water" => PlaceableCategory.Water,
            _ => PlaceableCategory.Decor
        };

        static Type ClassFor(string type) => type switch
        {
            "house" => typeof(RenovatableDefinition),
            "production" => typeof(ProductionDefinition),
            "crop" or "flower" or "tree" or "bush" => typeof(PlantDefinition),
            _ => typeof(PlaceableDefinition)
        };

        /// <summary>Laddar en asset av exakt rätt klass, annars skapas den (och en med fel klass ersätts).</summary>
        static T LoadOrCreate<T>(string path, Type exactType) where T : ScriptableObject
        {
            NGEditorPaths.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (existing != null && existing.GetType() == exactType) return (T)existing;
            if (existing != null)
            {
                Debug.LogWarning($"[NG Content] {path} var {existing.GetType().Name}, byts mot {exactType.Name}.");
                AssetDatabase.DeleteAsset(path);
            }
            var created = (T)ScriptableObject.CreateInstance(exactType);
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        static void Register(ScriptableObject asset, string address)
        {
            EditorUtility.SetDirty(asset);
            NGEditorPaths.RegisterAddressable(AssetDatabase.GetAssetPath(asset), "Content", address, ContentDatabase.ContentLabel);
        }

        /// <summary>"house_cottage" -> "House_Cottage". Samma namn som modellfilen SM_House_Cottage.fbx ska ha.</summary>
        public static string Title(string id) =>
            string.Join("_", id.Split('_').Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p.Substring(1)));

        static CurrencyCost[] Costs(int coins, int gems)
        {
            var list = new List<CurrencyCost>();
            if (coins > 0) list.Add(new CurrencyCost { currency = CurrencyType.Coins, amount = coins });
            if (gems > 0) list.Add(new CurrencyCost { currency = CurrencyType.Gems, amount = gems });
            return list.ToArray();
        }

        /// <summary>"wheat:3" -> 3 Vete.</summary>
        static ResourceAmount Amount(string text, Dictionary<string, ResourceDefinition> resources)
        {
            var parts = text.Split(':');
            string id = parts[0].Trim();
            int amount = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 1;
            if (!resources.TryGetValue(id, out var res))
                Debug.LogWarning($"[NG Content] Okänd vara '{id}'. Lägg till den i resources.csv.");
            return new ResourceAmount { resource = res, amount = amount };
        }

        /// <summary>Kolumnen "params": "grow=60;yield=wheat:2" -> {grow: 60, yield: wheat:2}.</summary>
        static Dictionary<string, string> Params(Dictionary<string, string> row)
        {
            var result = new Dictionary<string, string>();
            if (!row.TryGetValue("params", out var text)) return result;
            foreach (var pair in text.Split(';'))
            {
                int eq = pair.IndexOf('=');
                if (eq > 0) result[pair.Substring(0, eq).Trim()] = pair.Substring(eq + 1).Trim();
            }
            return result;
        }

        static int[] IntList(Dictionary<string, string> p, string key, int[] fallback) =>
            p.TryGetValue(key, out var text)
                ? text.Split('/').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray()
                : fallback;

        static int Int(Dictionary<string, string> row, string key, int fallback) =>
            row.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

        static float Float(Dictionary<string, string> row, string key, float fallback) =>
            row.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;

        /// <summary>
        /// CSV som Excel/Sheets sparar den: kommatecken mellan kolumner, text med kommatecken inom "citattecken"
        /// ("" = ett citattecken). Rader som börjar med # hoppas över.
        /// </summary>
        static List<Dictionary<string, string>> ReadCsv(string fileName)
        {
            var rows = new List<Dictionary<string, string>>();
            string path = Path.Combine(NGEditorPaths.DataFolder, fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[NG Content] Hittar inte {path}.");
                return rows;
            }
            var lines = File.ReadAllLines(path)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#"))
                .ToList();
            if (lines.Count == 0) return rows;
            var header = SplitCsvLine(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToArray();
            for (int i = 1; i < lines.Count; i++)
            {
                var cols = SplitCsvLine(lines[i]);
                var row = new Dictionary<string, string>();
                for (int c = 0; c < header.Length; c++)
                    row[header[c]] = c < cols.Count ? cols[c].Trim() : "";
                rows.Add(row);
            }
            return rows;
        }

        static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (quoted)
                {
                    if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (ch == '"') quoted = false;
                    else sb.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            result.Add(sb.ToString());
            return result;
        }
    }
}
