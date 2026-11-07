using NG.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace NG.CameraControl
{
    /// <summary>
    /// Ortografisk isometrisk kamera (X 30°, Y 45°) med samma beteende för touch och mus/tangentbord:
    /// en-finger-drag / vänster-drag panorerar, nyp / scrollhjul zoomar, WASD/pilar panorerar, +/- zoomar.
    /// Panorering räknas mot markplanet så att marken "följer fingret" exakt.
    /// Tryck som börjar på UI:t (InputGate) eller drag av ett objekt i byggläget panorerar inte.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsoCameraController : MonoBehaviour
    {
        [Header("Zoom (orthographicSize)")]
        [SerializeField] float minSize = 6f;
        [SerializeField] float maxSize = 22f;
        [SerializeField] float scrollZoomStep = 1.2f;
        [SerializeField] float keyZoomSpeed = 12f;

        [Header("Panorering")]
        [SerializeField] float keyPanSpeed = 14f;
        [SerializeField] Vector2 boundsMin = new Vector2(-60, -60);
        [SerializeField] Vector2 boundsMax = new Vector2(60, 60);

        Camera _cam;
        Plane _ground = new Plane(Vector3.up, Vector3.zero);
        Vector2 _lastMouse;
        bool _mouseDragging;
        bool _dragAllowed;
        bool _touchAllowed;

        /// <summary>Sätter vilket område (i världens x/z) kameran får titta på.</summary>
        public void SetBounds(Vector2 min, Vector2 max)
        {
            boundsMin = min;
            boundsMax = max;
        }

        /// <summary>Flyttar kameran så att den tittar på en punkt på marken.</summary>
        public void LookAt(Vector3 groundPoint)
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (TryGround(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out var focus))
                transform.position += new Vector3(groundPoint.x, 0f, groundPoint.z) - focus;
        }
        float _lastPinchDistance;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
        }

        void OnEnable() => EnhancedTouchSupport.Enable();
        void OnDisable() => EnhancedTouchSupport.Disable();

        void LateUpdate()
        {
            if (Touch.activeTouches.Count > 0) HandleTouch();
            else HandleMouseAndKeyboard();
            ClampPosition();
        }

        void HandleTouch()
        {
            var touches = Touch.activeTouches;
            if (touches[0].phase == UnityEngine.InputSystem.TouchPhase.Began)
                _touchAllowed = !InputGate.IsOverUi(touches[0].screenPosition);
            if (!_touchAllowed || InputGate.WorldDragCaptured) { _lastPinchDistance = 0f; return; }
            if (touches.Count == 1)
            {
                var t = touches[0];
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                    PanScreen(t.screenPosition - t.delta, t.screenPosition);
                _lastPinchDistance = 0f;
            }
            else
            {
                var a = touches[0];
                var b = touches[1];
                float distance = Vector2.Distance(a.screenPosition, b.screenPosition);
                Vector2 mid = (a.screenPosition + b.screenPosition) * 0.5f;
                Vector2 prevMid = mid - (a.delta + b.delta) * 0.5f;
                PanScreen(prevMid, mid);
                if (_lastPinchDistance > 0f && distance > 0f)
                    ZoomAround(mid, _cam.orthographicSize * (_lastPinchDistance / distance));
                _lastPinchDistance = distance;
            }
        }

        void HandleMouseAndKeyboard()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                bool dragButton = mouse.leftButton.isPressed || mouse.middleButton.isPressed;
                if (mouse.leftButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame)
                    _dragAllowed = !InputGate.IsOverUi(pos);
                if (dragButton && _mouseDragging && _dragAllowed && !InputGate.WorldDragCaptured) PanScreen(_lastMouse, pos);
                _mouseDragging = dragButton;
                _lastMouse = pos;

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !InputGate.IsOverUi(pos))
                {
                    float factor = scroll > 0 ? 1f / scrollZoomStep : scrollZoomStep;
                    ZoomAround(pos, _cam.orthographicSize * factor);
                }
            }

            var kb = Keyboard.current;
            if (kb == null) return;

            Vector2 move = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;
            if (move != Vector2.zero)
            {
                Vector3 right = Flat(transform.right);
                Vector3 forward = Flat(transform.forward);
                float speed = keyPanSpeed * (_cam.orthographicSize / 12f);
                transform.position += (right * move.x + forward * move.y).normalized * speed * Time.unscaledDeltaTime;
            }

            float zoom = 0f;
            if (kb.equalsKey.isPressed || kb.numpadPlusKey.isPressed) zoom -= 1;
            if (kb.minusKey.isPressed || kb.numpadMinusKey.isPressed) zoom += 1;
            if (zoom != 0f)
                SetSize(_cam.orthographicSize + zoom * keyZoomSpeed * Time.unscaledDeltaTime);
        }

        void PanScreen(Vector2 from, Vector2 to)
        {
            if (!TryGround(from, out var a) || !TryGround(to, out var b)) return;
            transform.position += a - b;
        }

        /// <summary>Zoomar så att punkten under fingret/muspekaren ligger kvar.</summary>
        void ZoomAround(Vector2 screenPoint, float newSize)
        {
            bool hadBefore = TryGround(screenPoint, out var before);
            SetSize(newSize);
            if (hadBefore && TryGround(screenPoint, out var after))
                transform.position += before - after;
        }

        void SetSize(float size) => _cam.orthographicSize = Mathf.Clamp(size, minSize, maxSize);

        bool TryGround(Vector2 screen, out Vector3 point)
        {
            var ray = _cam.ScreenPointToRay(screen);
            if (_ground.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        void ClampPosition()
        {
            // Begränsa punkten kameran tittar på, inte kamerans egen position.
            if (!TryGround(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out var focus)) return;
            var clamped = new Vector3(
                Mathf.Clamp(focus.x, boundsMin.x, boundsMax.x), 0f,
                Mathf.Clamp(focus.z, boundsMin.y, boundsMax.y));
            transform.position += clamped - focus;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0f ? v.normalized : Vector3.zero;
        }
    }
}
