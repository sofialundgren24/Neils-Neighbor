using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NG.EditorTools
{
    /// <summary>
    /// Byggen för alla plattformar med rätt inställningar, så man slipper klicka i Player Settings:
    ///   NG > Build > Android (AAB)   app-paket för Google Play (kräver Android Build Support i Unity Hub)
    ///   NG > Build > Android (APK)   fil att installera direkt på en testtelefon
    ///   NG > Build > iOS (Xcode)     Xcode-projekt; byggs och skickas till App Store på en Mac eller i molnet
    ///   NG > Build > WebGL           webbversion, komprimerad
    /// Byggen hamnar i mappen Builds/ (ignoreras av git).
    /// Innan varje bygge byggs Addressables-innehållet om, annars saknas modellerna i spelet.
    /// </summary>
    public static class BuildMenu
    {
        // ---- Ändra här när företag och app-id är bestämda (måste matcha App Store / Google Play) ----
        const string CompanyName = "Neil's Neighbor";
        const string ProductName = "Neil's Neighbor";
        const string AppId = "com.neilsneighbor.game";
        const string Version = "0.1.0";
        const int BuildNumber = 1;

        const string GameScene = "Assets/_Game/Scenes/Game.unity";

        [MenuItem("NG/Build/Apply Player Settings (all platforms)", priority = 100)]
        public static void ApplySettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;

            // Liggande läge på telefon (gatan är bred), båda hållen.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.runInBackground = false;

            // Android
            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, AppId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.bundleVersionCode = BuildNumber;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // iOS
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(ios, AppId);
            PlayerSettings.iOS.buildNumber = BuildNumber.ToString();
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;

            // WebGL: komprimerat och med reserv om servern inte skickar rätt headers.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;

            EnsureScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[NG Build] Inställningar satta: {ProductName} {Version} ({AppId}).");
        }

        [MenuItem("NG/Build/Android (AAB for Google Play)", priority = 110)]
        public static void BuildAndroidBundle() => BuildAndroid(true);

        [MenuItem("NG/Build/Android (APK for test phone)", priority = 111)]
        public static void BuildAndroidApk() => BuildAndroid(false);

        static void BuildAndroid(bool bundle)
        {
            EditorUserBuildSettings.buildAppBundle = bundle;
            string file = bundle ? "NeilsNeighbor.aab" : "NeilsNeighbor.apk";
            Build(BuildTarget.Android, BuildTargetGroup.Android, $"Builds/Android/{file}");
        }

        [MenuItem("NG/Build/iOS (Xcode project)", priority = 120)]
        public static void BuildIos() => Build(BuildTarget.iOS, BuildTargetGroup.iOS, "Builds/iOS");

        [MenuItem("NG/Build/WebGL", priority = 130)]
        public static void BuildWebGL() => Build(BuildTarget.WebGL, BuildTargetGroup.WebGL, "Builds/WebGL");

        static void Build(BuildTarget target, BuildTargetGroup group, string path)
        {
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                EditorUtility.DisplayDialog("Plattformen saknas",
                    $"{target} Build Support är inte installerat.\n\nUnity Hub > Installs > kugghjulet > Add modules.", "OK");
                return;
            }
            ApplySettings();
            if (EditorUserBuildSettings.activeBuildTarget != target &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                Debug.LogError($"[NG Build] Kunde inte byta till {target}.");
                return;
            }

            // Addressables måste byggas för den plattform som byggs.
            AddressableAssetSettings.BuildPlayerContent(out var addressables);
            if (!string.IsNullOrEmpty(addressables.Error))
            {
                Debug.LogError($"[NG Build] Addressables misslyckades: {addressables.Error}");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? "Builds");
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[NG Build] Klart: {path} ({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime.TotalMinutes:0.0} min)");
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                Debug.LogError($"[NG Build] {target} misslyckades: {report.summary.result}, {report.summary.totalErrors} fel. Se Console.");
            }
        }

        /// <summary>Spelscenen först i byggen; stiltestscenen tas inte med.</summary>
        static void EnsureScenes()
        {
            if (!File.Exists(GameScene))
            {
                Debug.LogWarning("[NG Build] Game.unity saknas. Kör NG > Setup > Build Game (Everything) först.");
                return;
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(GameScene, true) };
        }
    }
}
