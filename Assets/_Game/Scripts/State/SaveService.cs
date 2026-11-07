using System;
using System.IO;
using NG.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace NG.State
{
    /// <summary>
    /// Lokal sparning som JSON med schemaversion och migrering. Molnsynk (Cloud Save) läggs ovanpå i alpha.
    /// Skriver atomärt via temporär fil så att en krasch mitt i sparningen aldrig förstör sparfilen.
    /// </summary>
    public static class SaveService
    {
        const string FileName = "player.json";

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        static string PathOnDisk => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        // Webbversionen (WebGL) har inget vanligt filsystem: där sparas allt i webbläsarens lagring via PlayerPrefs.
#if UNITY_WEBGL && !UNITY_EDITOR
        const string WebKey = "ng_player_state";
        static bool HasSave => PlayerPrefs.HasKey(WebKey);
        static string ReadSave() => PlayerPrefs.GetString(WebKey);
        static void WriteSave(string json) { PlayerPrefs.SetString(WebKey, json); PlayerPrefs.Save(); }
        static void DeleteSave() => PlayerPrefs.DeleteKey(WebKey);
#else
        static bool HasSave => File.Exists(PathOnDisk);
        static string ReadSave() => File.ReadAllText(PathOnDisk);
        static void WriteSave(string json)
        {
            var tmp = PathOnDisk + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(PathOnDisk)) File.Replace(tmp, PathOnDisk, null);
            else File.Move(tmp, PathOnDisk);
        }
        static void DeleteSave()
        {
            if (File.Exists(PathOnDisk)) File.Delete(PathOnDisk);
        }
#endif

        public static PlayerState Load()
        {
            try
            {
                if (!HasSave) return new PlayerState();
                var json = ReadSave();
                var state = JsonConvert.DeserializeObject<PlayerState>(json, Settings) ?? new PlayerState();
                return Migrate(state);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Kunde inte läsa sparfil, behåller kopia och startar nytt: {e.Message}");
                TryBackupCorrupt();
                return new PlayerState();
            }
        }

        public static void Save(PlayerState state)
        {
            state.schemaVersion = PlayerState.CurrentSchemaVersion;
            state.savedAtUnix = TimeService.UnixNow;
            WriteSave(JsonConvert.SerializeObject(state, Settings));
        }

        static PlayerState Migrate(PlayerState state)
        {
            // Lägg till ett steg per schemaversion, t.ex.:
            // if (state.schemaVersion < 3) { ...; state.schemaVersion = 3; }
            if (state.schemaVersion < 3)
            {
                // Schema 1-2 fanns bara i prototyperna (bondgårdsinnehållet). Börja om.
                Debug.LogWarning($"[SaveService] Sparfil från en prototyp (schema {state.schemaVersion}). Startar nytt spel.");
                return new PlayerState();
            }
            if (state.schemaVersion > PlayerState.CurrentSchemaVersion)
                Debug.LogWarning("[SaveService] Sparfilen är nyare än klienten.");
            return state;
        }

        /// <summary>Raderar sparfilen (debug: "Ny sparfil").</summary>
        public static void Delete() => DeleteSave();

        static void TryBackupCorrupt()
        {
            try { File.Copy(PathOnDisk, PathOnDisk + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", true); }
            catch { /* inget att göra */ }
        }
    }
}
