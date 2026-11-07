using UnityEngine;
using UnityEngine.Localization;

namespace NG.Data
{
    /// <summary>
    /// Bas för allt spelinnehåll. Id:t är stabilt och används i sparfiler – byt ALDRIG id på något som är släppt.
    /// </summary>
    public abstract class DefinitionBase : ScriptableObject
    {
        [SerializeField] string id;

        [Tooltip("Namn som visas tills översättningar finns. Fylls från CSV-filerna i Assets/_Game/Data.")]
        public string fallbackName;

        public LocalizedString displayName;
        public Sprite icon;

        public string Id => id;

        /// <summary>Namn att visa i UI. Byts mot displayName när lokaliseringen kopplas in.</summary>
        public string DisplayName => string.IsNullOrEmpty(fallbackName) ? name : fallbackName;

#if UNITY_EDITOR
        public void EditorSetId(string newId) => id = newId;

        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
                id = name.ToLowerInvariant().Replace(' ', '_');
        }
#endif
    }
}
