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
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }

            // Tre nivåer så att spelet flyter även på svaga telefoner (se DeviceTier.cs).
            var low = CreateUrpAsset("URP_Mobile_Low", renderer, hdr: false, msaa: 1, renderScale: 0.85f,
                shadowDistance: 25f, cascades: 1, shadowRes: 1024);
            var high = CreateUrpAsset("URP_Mobile_High", renderer, hdr: false, msaa: 2, renderScale: 1f,
                shadowDistance: 35f, cascades: 1, shadowRes: 2048);
            var desktop = CreateUrpAsset("URP_Desktop", renderer, hdr: true, msaa: 4, renderScale: 1f,
                shadowDistance: 40f, cascades: 2, shadowRes: 2048);

            GraphicsSettings.defaultRenderPipeline = desktop;
            int current = QualitySettings.GetQualityLevel();
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                // Nivå 0-1 = svaga mobiler, 2-3 = vanliga mobiler, resten = dator/surfplatta.
                QualitySettings.renderPipeline = i <= 1 ? low : i <= 3 ? high : desktop;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NG Setup] URP aktiverat med tre nivåer: Mobile Low, Mobile High och Desktop ({levels} kvalitetsnivåer).");
        }

        static UniversalRenderPipelineAsset CreateUrpAsset(string name, UniversalRendererData renderer, bool hdr, int msaa,
            float renderScale, float shadowDistance, int cascades, int shadowRes)
        {
            string path = $"{NGEditorPaths.Settings}/{name}.asset";
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, path);
            }
            urp.supportsHDR = hdr;
            urp.msaaSampleCount = msaa;
            urp.renderScale = renderScale;
            urp.shadowDistance = shadowDistance;
            urp.shadowCascadeCount = cascades;
            urp.mainLightShadowmapResolution = shadowRes;
            EditorUtility.SetDirty(urp);
            return urp;
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
            camGo.transform.position = new Vector3(0f, 0f, 0f) - camGo.transform.forward * 80f;
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

        internal static VolumeProfile CreateVolumeProfile()
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

        const int StreetHouseCount = 5;

        /// <summary>
        /// Bygger den nedgångna startgatan: fem hus på rad bakom gången, en liten rabatt framför varje hus
        /// (räknas som husets trädgård) och gatuträd mellan husen. Finns flera hustyper roteras de.
        /// </summary>
        static void PlaceTestPrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { NGEditorPaths.Prefabs });
            var prefabs = guids.Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null && p.GetComponent<PlaceableView>() != null)
                .OrderBy(p => p.name)
                .ToList();
            var houses = prefabs.Where(p => p.name.StartsWith("PF_House")).ToList();
            var trees = prefabs.Where(p => p.name.StartsWith("PF_Tree")).ToList();
            var plants = prefabs.Except(houses).Except(trees).ToList();
            if (houses.Count == 0)
            {
                Debug.LogWarning("[NG Setup] Inga hus-prefabs hittades. Lägg ett SM_House_*.fbx i Art/Incoming först.");
                return;
            }

            var parent = new GameObject("Street").transform;
            const float lotWidth = 8f;
            float startX = -lotWidth * (StreetHouseCount - 1) * 0.5f;

            for (int i = 0; i < StreetHouseCount; i++)
            {
                float x = startX + i * lotWidth;
                var lot = new GameObject($"Lot_{i + 1}").transform;
                lot.SetParent(parent, false);

                var house = (GameObject)PrefabUtility.InstantiatePrefab(houses[i % houses.Count], lot);
                house.transform.position = new Vector3(x, 0f, 2f);

                // Rabatt framför huset: tre rader, en växtart per rad.
                for (int row = 0; row < plants.Count && row < 3; row++)
                {
                    for (int c = 0; c < 6; c++)
                    {
                        var plant = (GameObject)PrefabUtility.InstantiatePrefab(plants[(row + i) % plants.Count], lot);
                        plant.transform.position = new Vector3(x - 1.25f + c * 0.5f, 0f, -2.4f + row * 0.5f);
                        plant.transform.rotation = Quaternion.Euler(0f, (c * 73 + row * 31) % 360, 0f);
                    }
                }

                // Gatuträd mellan husen, bakom gången. Hör inte till något enskilt hus.
                if (trees.Count > 0 && i < StreetHouseCount - 1)
                {
                    var tree = (GameObject)PrefabUtility.InstantiatePrefab(trees[i % trees.Count], parent);
                    tree.transform.position = new Vector3(x + lotWidth * 0.5f, 0f, -3.0f);
                }
            }
        }
    }
}
