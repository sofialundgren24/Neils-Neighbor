using System.IO;
using NG.Data;
using UnityEditor;
using UnityEngine;

namespace NG.EditorTools
{
    /// <summary>Gemensamma sökvägar och hjälpare för editorverktygen.</summary>
    public static class NGEditorPaths
    {
        public const string GameRoot = "Assets/_Game";
        public const string ArtRoot = GameRoot + "/Art";
        public const string Incoming = ArtRoot + "/Incoming";
        public const string Models = ArtRoot + "/Models";
        public const string Materials = ArtRoot + "/Materials";
        public const string Shaders = ArtRoot + "/Shaders";
        public const string PaletteTexture = ArtRoot + "/Textures/T_Palette_256.png";
        public const string ModelTextures = ArtRoot + "/Textures/Models";
        public const string ModelMaterials = ArtRoot + "/Materials/Models";
        public const string Prefabs = GameRoot + "/Prefabs";
        public const string Placeables = GameRoot + "/Content/Placeables";
        public const string Goods = GameRoot + "/Content/Goods";
        public const string Recipes = GameRoot + "/Content/Recipes";
        public const string GameConfigPath = GameRoot + "/Content/GameConfig.asset";
        public const string LevelTablePath = GameRoot + "/Content/Progression/LevelTable.asset";
        public const string DataFolder = GameRoot + "/Data";
        public const string PlaceholderMaterials = GameRoot + "/Art/Materials/Placeholder";
        public const string Settings = GameRoot + "/Settings";
        public const string Scenes = GameRoot + "/Scenes";

        public const string StylizedMaterial = Materials + "/M_Stylized_Palette.mat";
        public const string FoliageMaterial = Materials + "/M_Foliage.mat";
        public const string GroundMaterial = Materials + "/M_Ground.mat";
        public const string PathMaterial = Materials + "/M_Path.mat";

        public static string CategoryFolder(PlaceableCategory category) => category switch
        {
            PlaceableCategory.House => "Houses",
            PlaceableCategory.Plant => "Plants",
            PlaceableCategory.Tree => "Plants",
            PlaceableCategory.Hedge => "Plants",
            PlaceableCategory.Fence => "Fences",
            PlaceableCategory.Water => "Water",
            PlaceableCategory.Production => "Buildings",
            PlaceableCategory.Community => "Buildings",
            PlaceableCategory.Clutter => "Clutter",
            PlaceableCategory.Path => "Ground",
            _ => "Decor"
        };

        public static bool IsFoliage(PlaceableCategory c) =>
            c == PlaceableCategory.Plant || c == PlaceableCategory.Tree || c == PlaceableCategory.Hedge;

        /// <summary>"House" -> House, "Flower" -> Plant osv. Okänt blir Decor.</summary>
        public static PlaceableCategory ParseCategory(string token) => token.ToLowerInvariant() switch
        {
            "house" or "shed" => PlaceableCategory.House,
            "production" or "shop" or "store" or "building" => PlaceableCategory.Production,
            "community" or "play" => PlaceableCategory.Community,
            "clutter" or "weed" => PlaceableCategory.Clutter,
            "plant" or "flower" or "crop" => PlaceableCategory.Plant,
            "tree" => PlaceableCategory.Tree,
            "hedge" or "bush" => PlaceableCategory.Hedge,
            "fence" => PlaceableCategory.Fence,
            "water" or "pool" or "pond" => PlaceableCategory.Water,
            "path" or "tile" or "road" or "ground" or "patio" or "sidewalk" => PlaceableCategory.Path,
            _ => PlaceableCategory.Decor
        };

        /// <summary>Triangelbudget för LOD0 på mobil, enligt GDD:ns tabell.</summary>
        public static int TriangleBudget(PlaceableCategory c) => c switch
        {
            PlaceableCategory.House => 6000,
            PlaceableCategory.Production => 6000,
            PlaceableCategory.Community => 4000,
            PlaceableCategory.Clutter => 200,
            PlaceableCategory.Tree => 1500,
            PlaceableCategory.Hedge => 500,
            PlaceableCategory.Plant => 200,
            PlaceableCategory.Fence => 300,
            PlaceableCategory.Water => 1500,
            _ => 800
        };

        public static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            string parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolder));
        }

        /// <summary>Skapar M_Stylized_Palette, M_Foliage, M_Ground och M_Path om de saknas.</summary>
        public static void EnsureMaterials()
        {
            var shader = Shader.Find("NG/Stylized");
            if (shader == null)
            {
                Debug.LogError("[NG] Hittar inte shadern NG/Stylized – är URP installerat och shadern kompilerad?");
                return;
            }
            EnsureFolder(Materials);
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTexture);

            CreateMaterial(StylizedMaterial, shader, m => m.SetTexture("_BaseMap", palette));
            CreateMaterial(FoliageMaterial, shader, m =>
            {
                m.SetTexture("_BaseMap", palette);
                m.SetFloat("_Wind", 1f);
                m.EnableKeyword("_WIND");
                m.SetFloat("_Cull", 0f); // dubbelsidig för blad
            });
            CreateMaterial(GroundMaterial, shader, m =>
            {
                m.SetColor("_BaseColor", Hex("#4CC22E"));
                m.SetFloat("_AOStrength", 0f);
                m.SetFloat("_RimStrength", 0f);
            });
            CreateMaterial(PathMaterial, shader, m =>
            {
                m.SetColor("_BaseColor", Hex("#F2C98A"));
                m.SetFloat("_AOStrength", 0f);
                m.SetFloat("_RimStrength", 0f);
            });
            // Markfärgen uppdateras varje gång (mjukare grön än första versionen).
            var ground = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterial);
            if (ground != null)
            {
                ground.SetColor("_BaseColor", Hex(GroundColor));
                EditorUtility.SetDirty(ground);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Gräsets färg. Samma färg används som kamerans bakgrund så att kartan inte "flyter" i luften.</summary>
        public const string GroundColor = "#7CC456";
        /// <summary>Låsta områden: dovare gräs, så att de syns men inte tar över bilden.</summary>
        public const string LockedGroundColor = "#93AE7E";

        static void CreateMaterial(string path, Shader shader, System.Action<Material> setup)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var mat = new Material(shader) { enableInstancing = true };
            setup(mat);
            AssetDatabase.CreateAsset(mat, path);
        }

        /// <summary>Lägger en asset i en Addressables-grupp med adress och etikett.</summary>
        public static void RegisterAddressable(string assetPath, string groupName, string address, string label)
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(groupName) ?? settings.CreateGroup(groupName, false, false, true, null,
                typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema),
                typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.ContentUpdateGroupSchema));
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), group);
            entry.address = address;
            entry.SetLabel(label, true, true);
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
