using System.Linq;
using NG.CameraControl;
using NG.Data;
using NG.Game;
using NG.UI;
using NG.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NG.EditorTools
{
    /// <summary>
    /// Bygger spelscenen Assets/_Game/Scenes/Game.unity: ljus, isometrisk kamera, mark i kartans storlek
    /// och objektet "Game" med GameBootstrap, WorldView, WorldInteraction och GameHud.
    /// All spellogik laddas vid start från innehållet, så scenen behöver sällan byggas om.
    /// </summary>
    public static class GameSceneBuilder
    {
        const string ScenePath = NGEditorPaths.Scenes + "/Game.unity";
        const string UiFolder = NGEditorPaths.GameRoot + "/UI";

        /// <summary>Mjukt, genomskinligt partikelmaterial (URP Particles/Unlit) för alla effekter.</summary>
        static Material EnsureParticleMaterial()
        {
            string path = NGEditorPaths.Materials + "/M_Particles.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[NG Setup] Hittar inte URP:s partikelshader. Effekterna stängs av.");
                return null;
            }
            mat = new Material(shader);
            mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>PanelSettings för UI:t: skalar efter höjden 720 så att det ser likadant ut på telefon och dator.</summary>
        static PanelSettings EnsurePanelSettings()
        {
            string path = UiFolder + "/NG_PanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
            {
                NGEditorPaths.EnsureFolder(UiFolder);
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(ps, path);
            }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UiFolder + "/NGTheme.tss");
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1280, 720);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 1f;
            EditorUtility.SetDirty(ps);
            AssetDatabase.SaveAssets();
            return ps;
        }

        /// <summary>Allt i rätt ordning. Kör efter att ha ändrat CSV-filerna eller lagt till modeller.</summary>
        [MenuItem("NG/Setup/Build Game (Everything)", priority = -10)]
        public static void BuildEverything()
        {
            Phase0Setup.CreateRenderPipeline();
            NGEditorPaths.EnsureMaterials();
            NGAssetImporter.ProcessIncomingNow();
            NGAssetImporter.RebuildAll();
            BalanceImporter.ImportLevels();
            ContentImporter.ImportAll();
            BuildGameScene();
        }

        [MenuItem("NG/Setup/Build Game Scene", priority = -9)]
        public static void BuildGameScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(NGEditorPaths.GameConfigPath);
            if (config == null)
            {
                Debug.LogError("[NG Setup] GameConfig saknas. Kör NG > Content > Import All först.");
                return;
            }

            NGEditorPaths.EnsureFolder(NGEditorPaths.Scenes);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Ljus: varm sol + gradient-ambient.
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

            // Kamera: ortografisk, X 30° / Y 45°.
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = NGEditorPaths.Hex(NGEditorPaths.GroundColor);
            camGo.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            camGo.transform.position = -camGo.transform.forward * 80f;
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var camController = camGo.AddComponent<IsoCameraController>();
            var camSo = new SerializedObject(camController);
            camSo.FindProperty("minSize").floatValue = 4f;   // närmare inzoomning
            camSo.FindProperty("maxSize").floatValue = 13f;  // inte så långt ut att allt blir pyttesmått
            camSo.ApplyModifiedPropertiesWithoutUndo();
            camGo.AddComponent<AudioListener>();

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = Phase0Setup.CreateVolumeProfile();

            // Mark i kartans storlek (Unitys Plane är 10 x 10 m).
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(
                config.mapSize.x * GameConfig.CellSize * 3f / 10f, 1f, config.mapSize.y * GameConfig.CellSize * 3f / 10f);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(NGEditorPaths.GroundMaterial);
            Object.DestroyImmediate(ground.GetComponent<Collider>()); // marken träffas med matematik, inte fysik

            // Spelet.
            var game = new GameObject("Game");
            var bootstrap = game.AddComponent<GameBootstrap>();
            var world = game.AddComponent<WorldView>();
            var interaction = game.AddComponent<WorldInteraction>();
            var juice = game.AddComponent<Juice>();
            juice.particleMaterial = EnsureParticleMaterial();
            var hud = game.AddComponent<GameHud>();
            hud.enabled = false; // reserv: slå på den och stäng av GameUI om det nya UI:t krånglar

            // UI Toolkit: UIDocument + PanelSettings (skalar efter 1280 x 720) + stilfilen NG.uss.
            var doc = game.AddComponent<UIDocument>();
            doc.panelSettings = EnsurePanelSettings();
            var ui = game.AddComponent<GameUI>();
            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("styleSheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>(UiFolder + "/NG.uss");
            uiSo.ApplyModifiedPropertiesWithoutUndo();
            world.lockedAreaMaterial = PlaceholderBuilder.MaterialFor(NGEditorPaths.Hex(NGEditorPaths.LockedGroundColor));
            interaction.indicatorMaterial = PlaceholderBuilder.MaterialFor(Color.white);

            var so = new SerializedObject(bootstrap);
            so.FindProperty("worldView").objectReferenceValue = world;
            so.FindProperty("interaction").objectReferenceValue = interaction;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("ui").objectReferenceValue = ui;
            so.FindProperty("cameraController").objectReferenceValue = camController;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[NG Setup] Spelscen sparad: {ScenePath}. Tryck Play.");
        }
    }
}
