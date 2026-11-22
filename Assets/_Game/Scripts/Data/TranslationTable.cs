using System;
using System.Collections.Generic;
using UnityEngine;

namespace NG.Data
{
    /// <summary>
    /// Översättningar av texterna i koden (svenska -> engelska). Skapas från Assets/_Game/Data/translations.csv
    /// av NG > Content > Import All och laddas in i Loc när spelet startar.
    /// </summary>
    public class TranslationTable : DefinitionBase
    {
        [Serializable]
        public struct Entry
        {
            [TextArea] public string sv;
            [TextArea] public string en;
        }

        public List<Entry> entries = new List<Entry>();

        public IEnumerable<KeyValuePair<string, string>> Pairs()
        {
            foreach (var e in entries) yield return new KeyValuePair<string, string>(e.sv, e.en);
        }
    }
}
