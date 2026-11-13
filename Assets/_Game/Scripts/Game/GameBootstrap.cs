using System;
using System.Linq;
using NG.CameraControl;
using NG.Core;
using NG.Data;
using NG.State;
using NG.UI;
using NG.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NG.Game
{
    /// <summary>
    /// Startpunkten i spelscenen. Ordningen:
    ///  1. välj grafiknivå (DeviceTier),
    ///  2. ladda allt innehåll (ContentDatabase via Addressables) och GameConfig,
    ///  3. ladda sparfilen (eller skapa ny) och starta GameSession,
    ///  4. koppla vyn (WorldView), interaktionen (WorldInteraction) och UI:t (GameHud).
    /// Sedan: Tick() fyra gånger per sekund och sparning var 30:e sekund och när appen pausas/stängs.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] WorldView worldView;
        [SerializeField] WorldInteraction interaction;
        [Tooltip("Spelets UI (UI Toolkit).")]
        [SerializeField] GameUI ui;
        [Tooltip("Gamla platshållar-UI:t (IMGUI). Används bara om GameUI är avstängt.")]
        [SerializeField] GameHud hud;
        [SerializeField] IsoCameraController cameraController;
        [SerializeField] float tickInterval = 0.25f;
        [SerializeField] float autosaveInterval = 30f;

        public static GameSession Session { get; private set; }

        ContentDatabase _content;
        float _nextTick;
        float _nextSave;
        bool _skipSave;

        async void Start()
        {
            try
            {
                var cam = Camera.main;
                DeviceTier.ApplySaved(cam);

                _content = new ContentDatabase();
                await _content.LoadAsync();
                var config = _content.AllOf<GameConfig>().FirstOrDefault();
                if (config == null)
                {
                    Debug.LogError("[Game] Hittar ingen GameConfig. Kör NG > Setup > Build Game (Everything) i editorn.");
                    return;
                }

                var state = SaveService.Load();
                Session = new GameSession(_content, config, state);
                bool newPlayer = !state.initialized;
                Session.StartNewGameIfNeeded();
                NG.Core.Analytics.Event("session_start", "new_player", newPlayer, "level", state.level);
                Session.Tick();

                SetupCamera(config);
                worldView.Bind(Session);
                interaction.Bind(Session, worldView);
                if (TryGetComponent<Juice>(out var juice)) juice.Bind(Session, worldView);
                if (ui != null && ui.isActiveAndEnabled) ui.Bind(Session, interaction, worldView);
                else if (hud != null) { hud.enabled = true; hud.Bind(Session, interaction, worldView); }
                _nextSave = Time.unscaledTime + autosaveInterval;
                Debug.Log($"[Game] Startat. Nivå {state.level}, {state.objects.Count} objekt på kartan.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void SetupCamera(GameConfig config)
        {
            if (cameraController == null) return;
            var origin = config.MapOriginWorld;
            var size = new Vector3(config.mapSize.x, 0f, config.mapSize.y) * GameConfig.CellSize;
            cameraController.SetBounds(new Vector2(origin.x, origin.z), new Vector2(origin.x + size.x, origin.z + size.z));

            // Börja med blicken på startgatan (första huset i startlayouten).
            var firstHouse = config.startLayout.FirstOrDefault(p => p.definition is RenovatableDefinition);
            if (firstHouse != null)
            {
                var focus = Session.World.CellToWorld(firstHouse.definition, firstHouse.cell.x, firstHouse.cell.y, firstHouse.rotation);
                cameraController.LookAt(focus + new Vector3(6f, 0f, 0f));
            }
        }

        void Update()
        {
            if (Session == null) return;
            if (Time.unscaledTime >= _nextTick)
            {
                _nextTick = Time.unscaledTime + tickInterval;
                Session.Tick();
            }
            if (Time.unscaledTime >= _nextSave)
            {
                _nextSave = Time.unscaledTime + autosaveInterval;
                Save();
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        void OnApplicationQuit() => Save();

        void OnDestroy()
        {
            Save();
            Session?.Dispose();
            Session = null;
            _content?.Release();
        }

        public void Save()
        {
            if (Session == null || _skipSave) return;
            try { SaveService.Save(Session.State); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Debug: radera sparfilen och starta om scenen.</summary>
        public void ResetSaveAndReload()
        {
            _skipSave = true;
            SaveService.Delete();
            Session?.Dispose();
            Session = null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
