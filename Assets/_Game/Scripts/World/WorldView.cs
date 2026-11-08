using System.Collections.Generic;
using NG.Core;
using NG.Data;
using NG.Game;
using NG.State;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace NG.World
{
    /// <summary>
    /// Visar spelets tillstånd i scenen. Skapar ett prefab-exemplar per PlacedObjectState (via Addressables),
    /// lyssnar på händelser från spellogiken och uppdaterar varianter, gråhet, målarfärg och position.
    /// Innehåller inga regler: allt den gör går att räkna ut från PlayerState.
    /// </summary>
    public class WorldView : MonoBehaviour
    {
        [Tooltip("Material för låsta områden (grå mark). Sätts av NG > Setup > Build Game Scene.")]
        public Material lockedAreaMaterial;

        GameSession _s;
        Transform _objectsRoot;
        Transform _overlayRoot;
        readonly Dictionary<string, PlaceableView> _views = new Dictionary<string, PlaceableView>();
        readonly HashSet<string> _loading = new HashSet<string>();
        readonly Dictionary<string, GameObject> _overlays = new Dictionary<string, GameObject>();

        public void Bind(GameSession session)
        {
            _s = session;
            _objectsRoot = new GameObject("Objects").transform;
            _objectsRoot.SetParent(transform, false);
            _overlayRoot = new GameObject("LockedAreas").transform;
            _overlayRoot.SetParent(transform, false);

            EventBus.Subscribe<ObjectPlaced>(OnPlaced);
            EventBus.Subscribe<ObjectMoved>(OnMoved);
            EventBus.Subscribe<ObjectRemoved>(OnRemoved);
            EventBus.Subscribe<ObjectChanged>(OnChanged);
            EventBus.Subscribe<ExpansionUnlocked>(OnExpansionUnlocked);

            foreach (var o in _s.World.Objects) Spawn(o);
            foreach (var e in _s.Config.expansions)
                if (!_s.World.IsExpansionUnlocked(e)) CreateOverlay(e);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<ObjectPlaced>(OnPlaced);
            EventBus.Unsubscribe<ObjectMoved>(OnMoved);
            EventBus.Unsubscribe<ObjectRemoved>(OnRemoved);
            EventBus.Unsubscribe<ObjectChanged>(OnChanged);
            EventBus.Unsubscribe<ExpansionUnlocked>(OnExpansionUnlocked);
        }

        public bool TryGetView(string instanceId, out PlaceableView view) =>
            _views.TryGetValue(instanceId, out view) && view != null;

        /// <summary>Punkt ovanför objektet där UI-bubblor ritas.</summary>
        public bool TryGetTop(string instanceId, out Vector3 top)
        {
            top = default;
            if (!TryGetView(instanceId, out var v)) return false;
            var col = v.GetComponent<Collider>();
            top = col != null ? new Vector3(v.transform.position.x, col.bounds.max.y, v.transform.position.z)
                              : v.transform.position + Vector3.up;
            return true;
        }

        public void SetVisible(string instanceId, bool visible)
        {
            if (TryGetView(instanceId, out var v)) v.gameObject.SetActive(visible);
        }

        // ---------- Händelser ----------

        void OnPlaced(ObjectPlaced e)
        {
            var o = _s.World.Get(e.InstanceId);
            if (o != null) Spawn(o, popIn: true);
        }

        void OnMoved(ObjectMoved e)
        {
            var o = _s.World.Get(e.InstanceId);
            if (o != null) Refresh(o, false);
        }

        void OnChanged(ObjectChanged e)
        {
            var o = _s.World.Get(e.InstanceId);
            if (o != null) Refresh(o, e.Animate);
        }

        void OnRemoved(ObjectRemoved e)
        {
            _loading.Remove(e.InstanceId);
            if (_views.TryGetValue(e.InstanceId, out var v))
            {
                _views.Remove(e.InstanceId);
                if (v != null) StartCoroutine(Poof(v.gameObject));
            }
        }

        /// <summary>Krymper och snurrar bort ett objekt (ogräs som rensas, något som säljs) i stället för att det bara försvinner.</summary>
        System.Collections.IEnumerator Poof(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
            var start = go.transform.localScale;
            float t = 0f;
            while (t < 1f && go != null)
            {
                t += Time.unscaledDeltaTime / 0.25f;
                float k = 1f - Mathf.Clamp01(t);
                go.transform.localScale = start * (k * (1f + 0.3f * Mathf.Sin(k * Mathf.PI)));
                go.transform.Rotate(0f, 720f * Time.unscaledDeltaTime, 0f);
                yield return null;
            }
            if (go != null && !Addressables.ReleaseInstance(go)) Destroy(go);
        }

        void OnExpansionUnlocked(ExpansionUnlocked e)
        {
            if (_overlays.TryGetValue(e.ExpansionId, out var go))
            {
                Destroy(go);
                _overlays.Remove(e.ExpansionId);
            }
        }

        // ---------- Skapa och uppdatera ----------

        void Spawn(PlacedObjectState o, bool popIn = false)
        {
            var def = _s.World.Def(o);
            if (def == null) return;
            var pos = _s.World.CellToWorld(def, o.x, o.y, o.rotation);
            var rot = WorldService.RotationOf(o.rotation);

            if (def.prefab == null || !def.prefab.RuntimeKeyIsValid())
            {
                Debug.LogWarning($"[WorldView] {def.Id} saknar prefab. Visar en grå kub.");
                Attach(o, CreateFallback(def, pos, rot), popIn);
                return;
            }

            string id = o.instanceId;
            _loading.Add(id);
            var handle = def.prefab.InstantiateAsync(pos, rot, _objectsRoot);
            handle.Completed += h =>
            {
                if (!_loading.Remove(id))
                {
                    // Objektet togs bort medan det laddades.
                    if (h.Status == AsyncOperationStatus.Succeeded) Addressables.ReleaseInstance(h.Result);
                    return;
                }
                if (h.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"[WorldView] Kunde inte ladda {def.Id}: {h.OperationException}");
                    return;
                }
                var state = _s.World.Get(id);
                if (state != null) Attach(state, h.Result, popIn);
            };
        }

        void Attach(PlacedObjectState o, GameObject go, bool popIn = false)
        {
            // Inte "??": Unitys GetComponent kan ge ett falskt null-objekt i editorn.
            if (!go.TryGetComponent<PlaceableView>(out var view)) view = go.AddComponent<PlaceableView>();
            if (!go.TryGetComponent<WorldObject>(out var wo)) wo = go.AddComponent<WorldObject>();
            wo.InstanceId = o.instanceId;
            _views[o.instanceId] = view;
            Refresh(o, false);
            if (popIn) view.PopIn();
        }

        GameObject CreateFallback(PlaceableDefinition def, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject($"Missing_{def.Id}");
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.SetParent(_objectsRoot, true);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(go.transform, false);
            cube.transform.localScale = new Vector3(def.footprint.x * PlotGrid.CellSize, 1f, def.footprint.y * PlotGrid.CellSize);
            cube.transform.localPosition = Vector3.up * 0.5f;
            return go;
        }

        /// <summary>Gör vyn lik tillståndet: position, variant, byggnadsställning, gråhet och färg.</summary>
        public void Refresh(PlacedObjectState o, bool animate)
        {
            if (!TryGetView(o.instanceId, out var v)) return;
            var def = _s.World.Def(o);
            if (def == null) return;

            v.transform.SetPositionAndRotation(
                _s.World.CellToWorld(def, o.x, o.y, o.rotation), WorldService.RotationOf(o.rotation));

            bool building = o.build != null;

            if (_s.Renovation.IsRenovatable(o))
            {
                v.ShowVariant(_s.Renovation.CurrentVariant(o), building || _s.Renovation.IsInProgress(o));
                if (_s.Renovation.IsComplete(o))
                {
                    v.CompleteRenovation(animate);
                    if (!string.IsNullOrEmpty(o.renovation.paintHex) &&
                        ColorUtility.TryParseHtmlString("#" + o.renovation.paintHex, out var paint))
                        v.SetPaint(paint);
                    else
                        v.SetPaint(Color.white, 0f);
                }
                else
                {
                    float decay = _s.Renovation.CurrentDecay(o);
                    if (animate) v.AnimateDecay(decay);
                    else v.SetDecayImmediate(decay);
                }
                return;
            }

            if (def is PlantDefinition)
                v.ShowVariant(GrowthService.StageVariants[_s.Growth.Stage(o)], building);
            else
                v.ShowVariant(null, building); // visar bara byggnadsställningen om den finns

            if (o.decayed) v.SetDecayImmediate(1f);
            else v.CompleteRenovation(animate);
        }

        // ---------- Låsta områden ----------

        void CreateOverlay(MapExpansion e)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Locked_{e.id}";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_overlayRoot, false);
            var origin = _s.Config.MapOriginWorld;
            var size = new Vector3(e.area.width * PlotGrid.CellSize, 0.02f, e.area.height * PlotGrid.CellSize);
            go.transform.position = origin + new Vector3(
                e.area.x * PlotGrid.CellSize + size.x * 0.5f, 0.01f, e.area.y * PlotGrid.CellSize + size.z * 0.5f);
            go.transform.localScale = size;
            if (lockedAreaMaterial != null) go.GetComponent<Renderer>().sharedMaterial = lockedAreaMaterial;
            _overlays[e.id] = go;
        }
    }
}
