using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace NG.Data
{
    /// <summary>
    /// Laddar alla definitioner med Addressables-etiketten "content" och slår upp dem via id.
    /// Nya objekt kräver ingen kodändring: importeraren märker dem automatiskt.
    /// </summary>
    public sealed class ContentDatabase
    {
        public const string ContentLabel = "content";

        readonly Dictionary<string, DefinitionBase> _byId = new Dictionary<string, DefinitionBase>();
        AsyncOperationHandle<IList<DefinitionBase>> _handle;

        public bool IsLoaded { get; private set; }
        public IReadOnlyCollection<DefinitionBase> All => _byId.Values;

        public async Task LoadAsync()
        {
            _handle = Addressables.LoadAssetsAsync<DefinitionBase>(ContentLabel, Register);
            await _handle.Task;
            if (_handle.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException($"Kunde inte ladda innehåll: {_handle.OperationException}");
            IsLoaded = true;
            Debug.Log($"[ContentDatabase] {_byId.Count} definitioner laddade.");
        }

        void Register(DefinitionBase def)
        {
            if (def == null) return;
            if (_byId.TryGetValue(def.Id, out var existing) && existing != def)
            {
                Debug.LogError($"[ContentDatabase] Dubblett-id '{def.Id}': {existing.name} och {def.name}");
                return;
            }
            _byId[def.Id] = def;
        }

        public T Get<T>(string id) where T : DefinitionBase
        {
            if (id != null && _byId.TryGetValue(id, out var def) && def is T typed) return typed;
            Debug.LogWarning($"[ContentDatabase] Saknar {typeof(T).Name} med id '{id}'.");
            return null;
        }

        public IEnumerable<T> AllOf<T>() where T : DefinitionBase
        {
            foreach (var def in _byId.Values)
                if (def is T typed) yield return typed;
        }

        public void Release()
        {
            if (_handle.IsValid()) Addressables.Release(_handle);
            _byId.Clear();
            IsLoaded = false;
        }
    }
}
