using System;
using System.Collections.Generic;
using UnityEngine;

namespace NG.Core
{
    /// <summary>
    /// Spelardata: vad spelarna gör, var de fastnar och när de slutar. Händelserna skickas redan från spellogiken;
    /// just nu loggas de bara i Console (i editorn). När Unity Analytics kopplas in sätts Sink till den,
    /// utan att något annat i koden behöver ändras.
    ///
    /// Viktiga händelser: session_start, terms_accepted, character_chosen, quest_complete (tutorialstegen!),
    /// level_up, place, house_renovated, sell, storage_upgrade, expansion_unlocked.
    /// </summary>
    public static class Analytics
    {
        /// <summary>Mottagaren av händelserna (Unity Analytics, Firebase ...). Null = bara logg.</summary>
        public static Action<string, Dictionary<string, object>> Sink;

        public static bool LogToConsole = Application.isEditor;

        public static void Event(string name, string key = null, object value = null, string key2 = null, object value2 = null)
        {
            var data = new Dictionary<string, object>();
            if (key != null) data[key] = value;
            if (key2 != null) data[key2] = value2;
            if (LogToConsole)
                Debug.Log($"[Analytics] {name}" + (key != null ? $" {key}={value}" : "") + (key2 != null ? $" {key2}={value2}" : ""));
            try { Sink?.Invoke(name, data); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
