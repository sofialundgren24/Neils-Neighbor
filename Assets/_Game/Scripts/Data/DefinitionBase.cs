using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;

namespace NG.Data
{
    /// <summary>
    /// Bas för allt spelinnehåll. Id:t är stabilt och används i sparfiler – byt ALDRIG id på något som är släppt.
    /// </summary>
    public abstract class DefinitionBase : ScriptableObject
    {
        [SerializeField] string id;
        public LocalizedString displayName;
        public Sprite icon;

        public string Id => id;

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
