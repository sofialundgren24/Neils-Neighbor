using System.Collections.Generic;
using System.IO;
using System.Linq;
using NG.Data;
using NG.World;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace NG.EditorTools
{
    /// <summary>
    /// Automatisk 3D-pipeline. Släpp en FBX/GLB med namnet SM_&lt;Kategori&gt;_&lt;Namn&gt; i Art/Incoming
    /// (och gärna dess målade textur T_&lt;Kategori&gt;_&lt;Namn&gt;.png bredvid) så:
    ///  1. sätts importinställningar (skala 1, ingen materialimport, inga kameror/ljus),
    ///  2. får meshar utan vertexfärg en neutral standard (AO = 1, ingen vind, ingen målarmask),
    ///  3. flyttas modellen till Art/Models/&lt;Kategori&gt;/,
    ///  4. skapas prefab PF_... med rätt material, collider och PlaceableView,
    ///  5. skapas en definition (DEF_...) om den saknas – befintliga speldata skrivs aldrig över,
    ///  6. registreras prefab och definition i Addressables,
    ///  7. valideras triangelbudget, pivot och materialantal mot GDD:n (varningar i konsolen).
    /// </summary>
    public class NGAssetImporter : AssetPostprocessor
    {
        static readonly string[] ModelExtensions = { ".fbx", ".glb", ".gltf", ".obj" };
        static readonly HashSet<string> Pending = new HashSet<string>();
        static readonly HashSet<string> PendingTextures = new HashSet<string>();

        // ---------- Importinställningar ----------

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(NGEditorPaths.ArtRoot)) return;
            var mi = (ModelImporter)assetImporter;
            if (!mi.importSettingsMissing) return; // första importen – manuella justeringar senare respekteras

            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off; // kompression kan flytta palett-UV:er till grannrutan
            mi.addCollider = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.importVisibility = false;
            mi.importNormals = ModelImporterNormals.Import;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            bool animated = Path.GetFileNameWithoutExtension(assetPath).Contains("_Anim");
            mi.importAnimation = animated;
            mi.animationType = animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
        }

        void OnPreprocessTexture()
        {
            if (IsModelTexturePath(assetPath))
            {
                // Målade texturer från Blender: max 1024 px och komprimerade, så de räcker även för svaga telefoner.
                var mt = (TextureImporter)assetImporter;
                if (!mt.importSettingsMissing) return;
                mt.sRGBTexture = true;
                mt.mipmapEnabled = true;
                mt.maxTextureSize = 1024;
                mt.textureCompression = TextureImporterCompression.Compressed;
                mt.wrapMode = TextureWrapMode.Clamp;
                mt.filterMode = FilterMode.Bilinear;
                return;
            }
            if (!assetPath.Contains("/T_Palette")) return;
            var ti = (TextureImporter)assetImporter;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(NGEditorPaths.ArtRoot)) return;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || mesh.colors32.Length == mesh.vertexCount) continue;
                var fill = new Color32[mesh.vertexCount];
                for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(255, 0, 0, 0);
                mesh.colors32 = fill;
                Debug.LogWarning($"[NG Import] {Path.GetFileName(assetPath)}/{mf.name}: saknar vertexfärg – fyllde i standard (ingen AO/vind/målarmask).", root);
            }
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                if (!path.StartsWith(NGEditorPaths.Incoming + "/")) continue;
                if (IsModelTexturePath(path)) { PendingTextures.Add(path); continue; }
                if (!ModelExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())) continue;
                Pending.Add(path);
            }
            if (Pending.Count > 0 || PendingTextures.Count > 0)
            {
                EditorApplication.delayCall -= ProcessPending;
                EditorApplication.delayCall += ProcessPending;
            }
        }

        static bool IsModelTexturePath(string path)
        {
            string file = Path.GetFileName(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return file.StartsWith("T_") && !file.StartsWith("T_Palette") && (ext == ".png" || ext == ".tga" || ext == ".jpg")
                   && (path.StartsWith(NGEditorPaths.Incoming + "/") || path.StartsWith(NGEditorPaths.ModelTextures + "/"));
        }

        static void ProcessPending()
        {
            var paths = Pending.ToList();
            Pending.Clear();
            NGEditorPaths.EnsureMaterials();

            // Texturer först, så att modellen som importeras samtidigt får sitt material direkt.
            var textures = PendingTextures.ToList();
            PendingTextures.Clear();
            foreach (var tex in textures)
            {
                string baseName = MoveTexture(tex);
                if (baseName == null) continue;
                // Ny textur till en modell som redan finns? Bygg om dess prefab.
                string modelName = "SM_" + baseName;
                if (paths.Any(p => Path.GetFileNameWithoutExtension(p) == modelName)) continue;
                var existingModel = AssetDatabase.FindAssets(modelName + " t:Model", new[] { NGEditorPaths.Models })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == modelName);
                if (existingModel != null) BuildPlaceable(existingModel);
            }

            foreach (var path in paths)
            {
                var moved = MoveToModels(path);
                if (moved != null) BuildPlaceable(moved);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- Meny ----------

        [MenuItem("NG/Assets/Rebuild All Prefabs From Models")]
        public static void RebuildAll()
        {
            NGEditorPaths.EnsureMaterials();
            var guids = AssetDatabase.FindAssets("t:Model", new[] { NGEditorPaths.Models });
            foreach (var guid in guids) BuildPlaceable(AssetDatabase.GUIDToAssetPath(guid));
            AssetDatabase.SaveAssets();
            Debug.Log($"[NG Import] Byggde om {guids.Length} prefabs.");
        }

        [MenuItem("NG/Assets/Process Incoming Folder")]
        public static void ProcessIncomingNow()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { NGEditorPaths.Incoming }))
                Pending.Add(AssetDatabase.GUIDToAssetPath(guid));
            ProcessPending();
        }

        // ---------- Pipeline ----------

        static bool TryParseName(string fileName, out PlaceableCategory category, out string displayName)
        {
            category = PlaceableCategory.Decor;
            displayName = null;
            var parts = fileName.Split('_');
            if (parts.Length < 3 || parts[0] != "SM") return false;
            category = NGEditorPaths.ParseCategory(parts[1]);
            displayName = string.Join("_", parts.Skip(1));
            return true;
        }

        /// <summary>Flyttar T_Namn.png från Incoming till Art/Textures/Models. Returnerar "Namn".</summary>
        static string MoveTexture(string path)
        {
            string file = Path.GetFileNameWithoutExtension(path);
            string baseName = file.Substring(2);
            if (!path.StartsWith(NGEditorPaths.Incoming + "/")) return baseName;
            NGEditorPaths.EnsureFolder(NGEditorPaths.ModelTextures);
            string target = NGEditorPaths.ModelTextures + "/" + Path.GetFileName(path);
            if (AssetDatabase.LoadAssetAtPath<Object>(target) != null)
            {
                File.Copy(path, target, true);
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                return baseName;
            }
            var error = AssetDatabase.MoveAsset(path, target);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError("[NG Import] Kunde inte flytta " + path + ": " + error);
                return null;
            }
            return baseName;
        }

        /// <summary>
        /// Material för en modell med egen målad textur: kopia av basmaterialet med texturen som _BaseMap.
        /// Utan textur delar alla modeller palettmaterialet (bäst för prestanda).
        /// </summary>
        static Material MaterialFor(string baseName, Material baseMaterial)
        {
            if (baseMaterial == null) return null;
            Texture2D tex = null;
            foreach (var ext in new[] { ".png", ".tga", ".jpg" })
            {
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(NGEditorPaths.ModelTextures + "/T_" + baseName + ext);
                if (tex != null) break;
            }
            if (tex == null) return baseMaterial;

            NGEditorPaths.EnsureFolder(NGEditorPaths.ModelMaterials);
            string suffix = baseMaterial.name.Contains("Foliage") ? "_Foliage" : "";
            string path = NGEditorPaths.ModelMaterials + "/M_" + baseName + suffix + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(baseMaterial);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static string MoveToModels(string path)
        {
            var file = Path.GetFileNameWithoutExtension(path);
            if (!TryParseName(file, out var category, out _))
            {
                Debug.LogWarning($"[NG Import] '{file}' följer inte namnkonventionen SM_<Kategori>_<Namn>. Ligger kvar i Incoming.");
                return null;
            }
            var folder = $"{NGEditorPaths.Models}/{NGEditorPaths.CategoryFolder(category)}";
            NGEditorPaths.EnsureFolder(folder);
            var target = $"{folder}/{Path.GetFileName(path)}";
            if (AssetDatabase.LoadAssetAtPath<Object>(target) != null)
            {
                // Ny version av en befintlig modell: ersätt filen men behåll GUID så alla referenser lever kvar.
                File.Copy(path, target, true);
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                return target;
            }
            var error = AssetDatabase.MoveAsset(path, target);
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"[NG Import] Kunde inte flytta {path}: {error}");
                return null;
            }
            return target;
        }

        static void BuildPlaceable(string modelPath)
        {
            var file = Path.GetFileNameWithoutExtension(modelPath);
            if (!TryParseName(file, out var category, out var baseName)) return;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return;

            string id = baseName.ToLowerInvariant();
            string folderName = NGEditorPaths.CategoryFolder(category);
            string prefabFolder = $"{NGEditorPaths.Prefabs}/{folderName}";
            string defFolder = $"{NGEditorPaths.Placeables}/{folderName}";
            NGEditorPaths.EnsureFolder(prefabFolder);
            NGEditorPaths.EnsureFolder(defFolder);
            string prefabPath = $"{prefabFolder}/PF_{baseName}.prefab";
            string defPath = $"{defFolder}/DEF_{baseName}.asset";

            // --- Bygg prefab: tom rot (pivot) + modellen som nästlad prefab, så omimport uppdaterar den.
            var root = new GameObject($"PF_{baseName}");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, false);

            var material = MaterialFor(baseName, AssetDatabase.LoadAssetAtPath<Material>(
                NGEditorPaths.IsFoliage(category) ? NGEditorPaths.FoliageMaterial : NGEditorPaths.StylizedMaterial));
            var foliage = MaterialFor(baseName, AssetDatabase.LoadAssetAtPath<Material>(NGEditorPaths.FoliageMaterial));

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (var r in renderers)
            {
                // Löv/blommor i hus-FBX:er kan märkas med "_Foliage" i objektnamnet för att få vind.
                var mat = r.name.Contains("_Foliage") && foliage != null ? foliage : material;
                var mats = r.sharedMaterials;
                if (mats.Length > 1)
                    Debug.LogWarning($"[NG Import] {file}/{r.name} har {mats.Length} material. Använd ett material mot paletten.", model);
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }

            ValidateModel(file, category, instance, bounds, hasBounds, model);

            if (hasBounds)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }

            // Startvariant: hus visas förfallna, växter i blom.
            SetInitialVariant(instance.transform, "Phase", "Phase0_Decayed");
            SetInitialVariant(instance.transform, "Stage", "Stage2_Bloom");

            var view = root.AddComponent<PlaceableView>();
            view.definitionId = id;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            // --- Definition: skapa bara om den saknas, så designerns värden aldrig skrivs över.
            var def = AssetDatabase.LoadAssetAtPath<PlaceableDefinition>(defPath);
            if (def == null)
            {
                def = CreateDefinitionFor(category, instance: prefab);
                def.EditorSetId(id);
                def.category = category;
                AssetDatabase.CreateAsset(def, defPath);
            }
            if (hasBounds)
            {
                def.footprint = new Vector2Int(
                    Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / PlotGrid.CellSize - 0.01f)),
                    Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / PlotGrid.CellSize - 0.01f)));
            }

            // --- Addressables
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            var prefabEntry = settings.CreateOrMoveEntry(prefabGuid, GetGroup(settings, "Placeables"));
            prefabEntry.address = $"placeable/{id}";
            prefabEntry.SetLabel("placeables", true, true);

            var defEntry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(defPath), GetGroup(settings, "Content"));
            defEntry.address = $"def/{id}";
            defEntry.SetLabel(ContentDatabase.ContentLabel, true, true);

            def.prefab = new AssetReferenceGameObject(prefabGuid);
            EditorUtility.SetDirty(def);
            Debug.Log($"[NG Import] {file} -> {prefabPath} ({category}, fotavtryck {def.footprint.x}x{def.footprint.y} celler)", prefab);
        }

        static PlaceableDefinition CreateDefinitionFor(PlaceableCategory category, GameObject instance)
        {
            bool hasStages = PlaceableView.FindVariants(instance.transform).Any(t => t.name.StartsWith("Stage"));
            if (category == PlaceableCategory.House) return ScriptableObject.CreateInstance<RenovatableDefinition>();
            if (category == PlaceableCategory.Production) return ScriptableObject.CreateInstance<ProductionDefinition>();
            if (category == PlaceableCategory.Community) return ScriptableObject.CreateInstance<CommunityDefinition>();
            if (category == PlaceableCategory.Clutter) return ScriptableObject.CreateInstance<ClutterDefinition>();
            if (hasStages || NGEditorPaths.IsFoliage(category)) return ScriptableObject.CreateInstance<PlantDefinition>();
            return ScriptableObject.CreateInstance<PlaceableDefinition>();
        }

        internal static void SetInitialVariant(Transform modelRoot, string prefix, string visible)
        {
            var variants = PlaceableView.FindVariants(modelRoot)
                .Where(t => t.name.StartsWith(prefix)).ToList();
            if (variants.Count == 0) return;
            bool found = variants.Any(v => v.name == visible);
            for (int i = 0; i < variants.Count; i++)
                variants[i].gameObject.SetActive(found ? variants[i].name == visible : i == 0);
        }

        static AddressableAssetGroup GetGroup(AddressableAssetSettings settings, string name)
        {
            var group = settings.FindGroup(name);
            if (group != null) return group;
            return settings.CreateGroup(name, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        }

        // ---------- Validering mot GDD:ns budget ----------

        static void ValidateModel(string file, PlaceableCategory category, GameObject instance, Bounds bounds, bool hasBounds, Object context)
        {
            int budget = NGEditorPaths.TriangleBudget(category);

            // Varje fas/stadie/LOD0 är en egen "vy" – budgeten gäller per vy, inte summan.
            var groups = PlaceableView.FindVariants(instance.transform).Where(t => t.name != "Scaffold").ToList();
            if (groups.Count == 0) groups.Add(instance.transform);

            foreach (var g in groups)
            {
                int tris = 0;
                foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    if (mf.name.Contains("_LOD") && !mf.name.Contains("_LOD0")) continue;
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                        tris += (int)(mf.sharedMesh.GetIndexCount(s) / 3);
                }
                if (tris > budget)
                    Debug.LogWarning($"[NG Import] {file}/{g.name}: {tris} trianglar > mobilbudget {budget} för {category}.", context);
            }

            if (!hasBounds) return;
            if (Mathf.Abs(bounds.min.y) > 0.05f)
                Debug.LogWarning($"[NG Import] {file}: underkant ligger på y = {bounds.min.y:0.00}. Pivot ska vara på marken (z = 0 i Blender).", context);
            var offset = new Vector2(bounds.center.x, bounds.center.z);
            if (offset.magnitude > Mathf.Max(0.25f, bounds.extents.magnitude * 0.25f))
                Debug.LogWarning($"[NG Import] {file}: pivot ligger {offset.magnitude:0.00} m från fotavtryckets mitt.", context);
        }
    }
}
