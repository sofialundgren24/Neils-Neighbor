using System.Linq;
using NG.CameraControl;
using NG.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NG.EditorTools
{
    /// <summary>
    /// Engångsuppsättning för fas 0: URP-asset, material och stiltestscenen med kamera, ljus och post-processing
    /// enligt GDD:ns värden. Kör via menyn NG > Setup > Phase 0 (Everything).
    /// </summary>
    public static class Phase0Setup
    {
        const string ScenePath = NGEditorPaths.Scenes + "/Phase0_StyleTest.unity";

        [MenuItem("NG/Setup/Phase 0 (Everything)", priority = 0)]
        public static void RunAll()
        {
            CreateRenderPipeline();
            NGEditorPaths.EnsureMaterials();
            NGAssetImporter.ProcessIncomingNow();
            NGAssetImporter.RebuildAll();
            BuildStyleTestScene();
        }

        [MenuItem("NG/Setup/1. Render Pipeline (URP)")]
        public static void CreateRenderPipeline()
        {
            NGEditorPaths.EnsureFolder(NGEditorPaths.Settings);
            string rendererPath = NGEditorPaths.Settings + "/URP_Renderer.asset";
            string assetPath = NGEditorPaths.Settings + "/URP_Desktop.asset";

            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, assetPath);
            }

            urp.supportsHDR = true;
            urp.msaaSampleCount = 4;
            urp.shadowDistance = 40f;
            urp.shadowCascadeCount = 2;
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log("[NG Setup] URP aktiverat. Skapa senare separata Mobile Low/High-assets med lägre skugg- och MSAA-värden.");
        }

        [MenuItem("NG/Setup/2. Build Style Test Scene")]
        public static void BuildStyleTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            NGEditorPaths.EnsureFolder(NGEditorPaths.Scenes);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Ljus: varm sol + gradient-ambient (GDD: Visuell stil > Belysning)
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = NGEditorPaths.Hex("#FFF1D6");
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = NGEditorPaths.Hex("#BFE6FF");
            RenderSettings.ambientEquatorColor = NGEditorPaths.Hex("#FFE7C2");
            RenderSettings.ambientGroundColor = NGEditorPaths.Hex("#7DBF5A");
            RenderSettings.fog = false;

            // --- Kamera: ortografisk, X 30° / Y 45°
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 12f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = NGEditorPaths.Hex("#BFE6FF");
            camGo.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            camGo.transform.position = new Vector3(6f, 0f, 0f) - camGo.transform.forward * 80f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camGo.AddComponent<IsoCameraController>();
            camGo.AddComponent<AudioListener>();

            // --- Post-processing
            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = CreateVolumeProfile();

            // --- Mark och gång
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(NGEditorPaths.GroundMaterial);

            var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path.name = "Path";
            path.transform.position = new Vector3(0f, 0.01f, -5f);
            path.transform.localScale = new Vector3(60f, 0.02f, 2.5f);
            path.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(NGEditorPaths.PathMaterial);
            Object.DestroyImmediate(path.GetComponent<BoxCollider>());

            PlaceTestPrefabs();

            new GameObject("StyleTestDemo").AddComponent<StyleTestDemo>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (buildScenes.All(s => s.path != ScenePath))
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
            Debug.Log($"[NG Setup] Stiltestscen sparad: {ScenePath}. Tryck Play.");
        }

        static VolumeProfile CreateVolumeProfile()
        {
            NGEditorPaths.EnsureFolder(NGEditorPaths.Settings);
            string profilePath = NGEditorPaths.Settings + "/VP_Stylized.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (existing != null) return existing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);

            var tonemap = profile.Add<Tonemapping>(true);
            tonemap.mode.Override(TonemappingMode.Neutral);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.35f);

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(10f);
            color.postExposure.Override(0f);
            color.contrast.Override(5f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.5f);

            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Ställer upp alla importerade prefabs: hus på rad bakom gången, växter i rabatter framför husen.</summary>
        static void PlaceTestPrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { NGEditorPaths.Prefabs });
            var prefabs = guids.Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null && p.GetComponent<PlaceableView>() != null)
                .OrderBy(p => p.name)
                .ToList();

            var parent = new GameObject("Placeables").transform;
            float houseX = -8f;
            float plantX = -9f;
            float treeX = -12f;
            foreach (var prefab in prefabs)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                var size = go.TryGetComponent<BoxCollider>(out var box) ? box.size : Vector3.one;
                if (prefab.name.StartsWith("PF_House"))
                {
                    go.transform.position = new Vector3(houseX + size.x * 0.5f, 0f, 2f);
                    houseX += size.x + 3f;
                }
                else if (prefab.name.StartsWith("PF_Tree"))
                {
                    go.transform.position = new Vector3(treeX, 0f, 6f);
                    treeX += 4f;
                }
                else
                {
                    // Små växter: en liten rabatt (3 x 4) per art framför husen.
                    for (int i = 0; i < 12; i++)
                    {
                        var copy = i == 0 ? go : (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                        copy.transform.position = new Vector3(plantX + (i % 3) * 0.5f, 0f, -2.2f + (i / 3) * 0.5f);
                        copy.transform.rotation = Quaternion.Euler(0f, (i * 73) % 360, 0f);
                    }
                    plantX += 2.5f;
                }
            }
        }
    }
}
