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

        public static PlayerState Load()
        {
            try
            {
                if (!File.Exists(PathOnDisk)) return new PlayerState();
                var json = File.ReadAllText(PathOnDisk);
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
            var json = JsonConvert.SerializeObject(state, Settings);
            var tmp = PathOnDisk + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(PathOnDisk)) File.Replace(tmp, PathOnDisk, null);
            else File.Move(tmp, PathOnDisk);
        }

        static PlayerState Migrate(PlayerState state)
        {
            // Lägg till ett steg per schemaversion, t.ex.:
            // if (state.schemaVersion < 2) { ...; state.schemaVersion = 2; }
            if (state.schemaVersion > PlayerState.CurrentSchemaVersion)
                Debug.LogWarning("[SaveService] Sparfilen är nyare än klienten.");
            return state;
        }

        static void TryBackupCorrupt()
        {
            try { File.Copy(PathOnDisk, PathOnDisk + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", true); }
            catch { /* inget att göra */ }
        }
    }
}
