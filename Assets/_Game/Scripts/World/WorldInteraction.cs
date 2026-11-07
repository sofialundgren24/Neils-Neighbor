using NG.Core;
using NG.Data;
using NG.Game;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace NG.World
{
    /// <summary>
    /// Allt spelaren gör med fingret/musen i världen:
    ///  - Tryck på ett objekt -> markera det (UI:t visar vad man kan göra).
    ///  - Tryck på ett låst område -> markera området (UI:t visar priset).
    ///  - Byggläge: ett "spöke" av objektet följer rutnätet. Dra spöket för att flytta det, dra någon
    ///    annanstans för att panorera. Grön ruta = går att placera, röd = går inte.
    /// Själva köpet/flytten görs av WorldService; den här klassen bestämmer bara var.
    /// </summary>
    public class WorldInteraction : MonoBehaviour
    {
        public enum Mode { Idle, Placing }

        [Tooltip("Material för markeringsrutan under objekt. Sätts av NG > Setup > Build Game Scene.")]
        public Material indicatorMaterial;

        [SerializeField] float tapSlopPixels = 14f;
        [SerializeField] float tapMaxSeconds = 0.45f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color ValidColor = new Color(0.35f, 0.95f, 0.45f);
        static readonly Color InvalidColor = new Color(1f, 0.35f, 0.3f);
        static readonly Color SelectedColor = new Color(1f, 0.85f, 0.3f);

        GameSession _s;
        WorldView _world;
        Camera _cam;
        readonly Plane _ground = new Plane(Vector3.up, Vector3.zero);

        public Mode CurrentMode { get; private set; }
        public string SelectedId { get; private set; }
        public MapExpansion SelectedExpansion { get; private set; }

        // Byggläge
        public PlaceableDefinition PlacingDef { get; private set; }
        public string MovingId { get; private set; }
        public bool PlacementValid { get; private set; }
        public string PlacementReason { get; private set; }
        int _rotation;
        Vector2Int _cell;
        GameObject _ghost;
        GameObject _placeIndicator;
        GameObject _selectIndicator;
        MaterialPropertyBlock _mpb;

        // Pekare
        bool _pressing;
        bool _draggingGhost;
        Vector2 _pressPos;
        float _pressTime;

        public void Bind(GameSession session, WorldView world)
        {
            _s = session;
            _world = world;
            _cam = Camera.main;
            _mpb = new MaterialPropertyBlock();
            _placeIndicator = CreateIndicator("PlacementIndicator");
            _selectIndicator = CreateIndicator("SelectionIndicator");
            EventBus.Subscribe<ObjectRemoved>(OnRemoved);
        }

        void OnDestroy() => EventBus.Unsubscribe<ObjectRemoved>(OnRemoved);

        void OnRemoved(ObjectRemoved e)
        {
            if (SelectedId == e.InstanceId) ClearSelection();
        }

        // ---------- Publikt API för UI:t ----------

        public void Select(string instanceId)
        {
            SelectedId = instanceId;
            SelectedExpansion = null;
        }

        public void ClearSelection()
        {
            SelectedId = null;
            SelectedExpansion = null;
        }

        /// <summary>Börja placera ett nytt objekt från byggmenyn. Spöket hamnar mitt på skärmen.</summary>
        public void BeginPlace(PlaceableDefinition def)
        {
            CancelPlacing();
            ClearSelection();
            PlacingDef = def;
            MovingId = null;
            _rotation = 0;
            _cell = _s.World.CenteredCell(def, _rotation, ScreenCenterOnGround());
            CurrentMode = Mode.Placing;
            CreateGhost(def);
        }

        /// <summary>Flytta ett objekt som redan står på kartan.</summary>
        public void BeginMove(string instanceId)
        {
            var o = _s.World.Get(instanceId);
            if (o == null) return;
            CancelPlacing();
            PlacingDef = _s.World.Def(o);
            MovingId = instanceId;
            _rotation = o.rotation;
            _cell = new Vector2Int(o.x, o.y);
            CurrentMode = Mode.Placing;
            _world.SetVisible(instanceId, false);
            ClearSelection();
            CreateGhost(PlacingDef);
        }

        public void Rotate()
        {
            if (CurrentMode != Mode.Placing) return;
            _rotation = (_rotation + 1) & 3;
        }

        public void Confirm()
        {
            if (CurrentMode != Mode.Placing || PlacingDef == null) return;
            Result result;
            if (MovingId != null)
            {
                var o = _s.World.Get(MovingId);
                result = o != null ? _s.World.Move(o, _cell.x, _cell.y, _rotation) : Result.Fail("Objektet finns inte längre.");
                if (result.Ok)
                {
                    string moved = MovingId;
                    MovingId = null; // så att CancelPlacing inte "ångrar" flytten
                    CancelPlacing();
                    _world.SetVisible(moved, true);
                    Select(moved);
                }
            }
            else
            {
                var def = PlacingDef;
                result = _s.World.Place(def, _cell.x, _cell.y, _rotation);
                // Staket, gångar och växter placeras ofta många i rad: stanna kvar i byggläget.
                bool keepGoing = result.Ok && IsRepeatable(def) && _s.World.CanBuy(def, out _);
                if (result.Ok && !keepGoing) CancelPlacing();
            }
            if (!result.Ok) GameSession.Message(result.Reason);
        }

        public void CancelPlacing()
        {
            if (MovingId != null) _world.SetVisible(MovingId, true);
            MovingId = null;
            PlacingDef = null;
            CurrentMode = Mode.Idle;
            InputGate.WorldDragCaptured = false;
            _draggingGhost = false;
            DestroyGhost();
            if (_placeIndicator != null) _placeIndicator.SetActive(false);
        }

        static bool IsRepeatable(PlaceableDefinition def) =>
            def.category == PlaceableCategory.Path || def.category == PlaceableCategory.Fence ||
            def.category == PlaceableCategory.Plant || def.category == PlaceableCategory.Hedge;

        // ---------- Uppdatering ----------

        void Update()
        {
            if (_s == null) return;
            if (_cam == null) _cam = Camera.main;
            HandlePointer();
            if (CurrentMode == Mode.Placing) UpdatePlacement();
            UpdateSelectionIndicator();
        }

        void HandlePointer()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 pos = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                _pressing = !InputGate.IsOverUi(pos);
                _pressPos = pos;
                _pressTime = Time.unscaledTime;
                if (_pressing && CurrentMode == Mode.Placing && IsOverGhost(pos))
                {
                    _draggingGhost = true;
                    InputGate.WorldDragCaptured = true;
                }
            }

            if (_pressing && _draggingGhost && TryGround(pos, out var world))
                _cell = _s.World.CenteredCell(PlacingDef, _rotation, world);

            if (pointer.press.wasReleasedThisFrame && _pressing)
            {
                _pressing = false;
                bool tap = (pos - _pressPos).magnitude <= tapSlopPixels && Time.unscaledTime - _pressTime <= tapMaxSeconds;
                if (_draggingGhost)
                {
                    _draggingGhost = false;
                    InputGate.WorldDragCaptured = false;
                }
                else if (tap)
                {
                    OnTap(pos);
                }
            }
        }

        void OnTap(Vector2 screenPos)
        {
            if (CurrentMode == Mode.Placing)
            {
                // Tryck någonstans i byggläget = flytta spöket dit.
                if (TryGround(screenPos, out var p)) _cell = _s.World.CenteredCell(PlacingDef, _rotation, p);
                return;
            }

            var ray = _cam.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hit, 1000f))
            {
                var wo = hit.collider.GetComponentInParent<WorldObject>();
                if (wo != null)
                {
                    Select(wo.InstanceId);
                    return;
                }
            }

            if (TryGround(screenPos, out var ground))
            {
                var locked = _s.World.LockedExpansionAt(_s.World.WorldToCell(ground));
                if (locked != null)
                {
                    SelectedId = null;
                    SelectedExpansion = locked;
                    return;
                }
            }
            ClearSelection();
        }

        void UpdatePlacement()
        {
            if (PlacingDef == null) return;
            bool spotOk = _s.World.IsSpotFree(PlacingDef, _cell.x, _cell.y, _rotation, MovingId, out var reason);
            string buyReason = null;
            bool buyOk = MovingId != null || _s.World.CanBuy(PlacingDef, out buyReason);
            PlacementValid = spotOk && buyOk;
            PlacementReason = !spotOk ? reason : buyReason;

            var pos = _s.World.CellToWorld(PlacingDef, _cell.x, _cell.y, _rotation);
            if (_ghost != null)
                _ghost.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, WorldService.RotationOf(_rotation));
            PlaceIndicator(_placeIndicator, PlacingDef, _cell, _rotation, PlacementValid ? ValidColor : InvalidColor);
        }

        void UpdateSelectionIndicator()
        {
            var o = CurrentMode == Mode.Idle ? _s.World.Get(SelectedId) : null;
            if (o == null)
            {
                _selectIndicator.SetActive(false);
                return;
            }
            PlaceIndicator(_selectIndicator, _s.World.Def(o), new Vector2Int(o.x, o.y), o.rotation, SelectedColor);
        }

        // ---------- Spöke och markeringsrutor ----------

        void CreateGhost(PlaceableDefinition def)
        {
            DestroyGhost();
            if (def.prefab == null || !def.prefab.RuntimeKeyIsValid()) return;
            var handle = def.prefab.InstantiateAsync(transform);
            handle.Completed += h =>
            {
                if (h.Status != AsyncOperationStatus.Succeeded) return;
                if (PlacingDef != def || _ghost != null)
                {
                    Addressables.ReleaseInstance(h.Result);
                    return;
                }
                _ghost = h.Result;
                _ghost.name = "Ghost";
                foreach (var c in _ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
                if (_ghost.TryGetComponent<PlaceableView>(out var view))
                {
                    view.CompleteRenovation(false);
                    // Visa den färdiga varianten (sista fasen/stadiet) i butiken.
                    if (def is RenovatableDefinition rd && rd.phases.Length > 0) view.ShowVariant(rd.phases[rd.phases.Length - 1].variantName);
                    else if (def is PlantDefinition) view.ShowVariant(GrowthService.StageVariants[2]);
                    else view.ShowVariant(null);
                }
            };
        }

        void DestroyGhost()
        {
            if (_ghost == null) return;
            if (!Addressables.ReleaseInstance(_ghost)) Destroy(_ghost);
            _ghost = null;
        }

        GameObject CreateIndicator(string objectName)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objectName;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            if (indicatorMaterial != null) go.GetComponent<Renderer>().sharedMaterial = indicatorMaterial;
            go.SetActive(false);
            return go;
        }

        void PlaceIndicator(GameObject indicator, PlaceableDefinition def, Vector2Int cell, int rotation, Color color)
        {
            if (indicator == null || def == null) return;
            var size = PlotGrid.Rotated(def.footprint, rotation);
            indicator.SetActive(true);
            indicator.transform.position = _s.World.CellToWorld(def, cell.x, cell.y, rotation) + Vector3.up * 0.02f;
            indicator.transform.rotation = Quaternion.identity;
            indicator.transform.localScale = new Vector3(size.x * PlotGrid.CellSize, 0.03f, size.y * PlotGrid.CellSize);
            var r = indicator.GetComponent<Renderer>();
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, color);
            r.SetPropertyBlock(_mpb);
        }

        // ---------- Hjälpare ----------

        bool IsOverGhost(Vector2 screenPos)
        {
            if (PlacingDef == null || !TryGround(screenPos, out var p)) return false;
            var cell = _s.World.WorldToCell(p);
            var size = PlotGrid.Rotated(PlacingDef.footprint, _rotation);
            const int margin = 1;
            return cell.x >= _cell.x - margin && cell.x < _cell.x + size.x + margin &&
                   cell.y >= _cell.y - margin && cell.y < _cell.y + size.y + margin;
        }

        Vector3 ScreenCenterOnGround() =>
            TryGround(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out var p) ? p : Vector3.zero;

        bool TryGround(Vector2 screenPos, out Vector3 point)
        {
            point = default;
            if (_cam == null) return false;
            var ray = _cam.ScreenPointToRay(screenPos);
            if (!_ground.Raycast(ray, out float enter)) return false;
            point = ray.GetPoint(enter);
            return true;
        }
    }
}
