using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NG.Game
{
    /// <summary>
    /// Väljer grafiknivå efter enhetens styrka, så att spelet flyter även på billiga telefoner.
    /// Kvalitetsnivåerna (Project Settings > Quality) pekar på olika URP-assets, som skapas av
    /// NG > Setup > 1. Render Pipeline:
    ///   Low    (nivå 0-1)  URP_Mobile_Low:  85 % upplösning, ingen MSAA, enkla skuggor, ingen post-processing, 30 fps
    ///   Medium (nivå 2-3)  URP_Mobile_High: full upplösning, 2x MSAA, mjukare skuggor, 30 fps
    ///   High   (nivå 4-5)  URP_Desktop:     4x MSAA, HDR, två skuggkaskader, 60 fps
    /// </summary>
    public static class DeviceTier
    {
        public enum Tier { Low, Medium, High }

        public static Tier Current { get; private set; } = Tier.High;

        const string PrefKey = "ng_graphics";

        /// <summary>Spelarens val i Inställningar: null = automatiskt efter enheten.</summary>
        public static Tier? Override
        {
            get
            {
                int v = PlayerPrefs.GetInt(PrefKey, -1);
                return v < 0 ? (Tier?)null : (Tier)v;
            }
            set
            {
                PlayerPrefs.SetInt(PrefKey, value.HasValue ? (int)value.Value : -1);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Använder spelarens val om det finns, annars automatiskt.</summary>
        public static void ApplySaved(Camera camera) => Apply(Override ?? Detect(), camera);

        public static Tier Detect()
        {
            if (!Application.isMobilePlatform)
                return SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 1500 ? Tier.Medium : Tier.High;

            int ramMb = SystemInfo.systemMemorySize;
            int cores = SystemInfo.processorCount;
            if (ramMb < 3500 || cores <= 4) return Tier.Low;
            if (ramMb < 6500) return Tier.Medium;
            return Tier.High;
        }

        public static void Apply(Tier tier, Camera camera)
        {
            Current = tier;
            int levels = QualitySettings.names.Length;
            int index = tier switch
            {
                Tier.Low => 0,
                Tier.Medium => Mathf.Min(2, levels - 1),
                _ => levels - 1
            };
            QualitySettings.SetQualityLevel(index, true);

            // Mobil: lås till 30 fps på svaga enheter för batteri och värme. Stilen är lugn, 30 räcker.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Application.isMobilePlatform && tier != Tier.High ? 30 : 60;

            if (camera != null && camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                data.renderPostProcessing = tier != Tier.Low;

            Debug.Log($"[DeviceTier] {tier} (kvalitetsnivå {QualitySettings.names[index]}, {SystemInfo.systemMemorySize} MB RAM, {SystemInfo.processorCount} kärnor)");
        }
    }
}
