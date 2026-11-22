using System.Collections.Generic;
using UnityEngine;

namespace NG.Core
{
    /// <summary>
    /// Språk: svenska och engelska. Koden skriver texterna på svenska, och Loc slår upp den engelska
    /// översättningen i Assets/_Game/Data/translations.csv (kolumnerna sv, en).
    ///   Loc.T("Bygg")                       -> "Build"
    ///   Loc.F("Kräver nivå {0}.", 3)        -> "Requires level 3."
    /// Saknas en översättning visas svenskan, så spelet går aldrig sönder av en glömd rad.
    /// Namn på hus, blommor, uppdrag osv. översätts i sina egna CSV-filer (kolumner som slutar på _en).
    /// Ny text i koden? Kör "python Tools/loc/loc_convert.py table" för att se vad som saknar översättning.
    /// </summary>
    public static class Loc
    {
        public enum Lang { Sv, En }

        const string PrefKey = "ng_language";
        static readonly Dictionary<string, string> English = new Dictionary<string, string>();

        public static Lang Current { get; private set; } = Detect();
        public static bool IsEnglish => Current == Lang.En;

        /// <summary>Sparat val, annars telefonens språk: svenska för svenska telefoner, annars engelska.</summary>
        public static Lang Detect()
        {
            int saved = PlayerPrefs.GetInt(PrefKey, -1);
            if (saved >= 0) return (Lang)saved;
            return Application.systemLanguage == SystemLanguage.Swedish ? Lang.Sv : Lang.En;
        }

        public static void Set(Lang lang)
        {
            Current = lang;
            PlayerPrefs.SetInt(PrefKey, (int)lang);
            PlayerPrefs.Save();
        }

        /// <summary>Fyller översättningstabellen (anropas när innehållet har laddats).</summary>
        public static void Load(IEnumerable<KeyValuePair<string, string>> svToEn)
        {
            English.Clear();
            foreach (var kv in svToEn)
                if (!string.IsNullOrEmpty(kv.Key) && !string.IsNullOrEmpty(kv.Value))
                    English[kv.Key] = kv.Value;
        }

        public static string T(string sv)
        {
            if (Current == Lang.Sv || sv == null) return sv;
            return English.TryGetValue(sv, out var en) ? en : sv;
        }

        public static string F(string sv, params object[] args) => string.Format(T(sv), args);

        /// <summary>Väljer rätt språk för text som står i innehållsfilerna (name/name_en).</summary>
        public static string Pick(string sv, string en) => IsEnglish && !string.IsNullOrEmpty(en) ? en : sv;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Current = Detect();
    }
}
