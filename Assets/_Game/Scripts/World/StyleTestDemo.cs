using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NG.World
{
    /// <summary>
    /// Prototyp för fas 0: renovera husen fas för fas och se grå -> färg-vågen spridas över hela tomten.
    /// Visar också FPS så att grinden "30 fps på mobil" kan mätas på enhet. Tas bort efter fas 0.
    /// </summary>
    public class StyleTestDemo : MonoBehaviour
    {
        static readonly string[] HousePhases = { "Phase0_Decayed", "Phase1_Repaired", "Phase2_Painted", "Phase3_Decorated" };

        [SerializeField] Color[] paintOptions =
        {
            new Color(0.56f, 0.83f, 1f),   // facade_sky
            new Color(1f, 0.71f, 0.64f),   // facade_peach
            new Color(1f, 0.89f, 0.48f),   // facade_butter
            new Color(0.64f, 0.36f, 1f)    // lila
        };

        readonly List<PlaceableView> _houses = new List<PlaceableView>();
        readonly List<PlaceableView> _all = new List<PlaceableView>();
        int _phase;
        int _paint = -1;
        float _fpsSmoothed = 60f;
        GUIStyle _style;

        void Start()
        {
            _all.AddRange(FindObjectsByType<PlaceableView>(FindObjectsSortMode.None));
            _houses.AddRange(_all.Where(v => v.Variants.Any(t => t.name.StartsWith("Phase"))));
            ResetAll();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / dt, 0.05f);
        }

        void ResetAll()
        {
            _phase = 0;
            _paint = -1;
            foreach (var h in _houses) h.ShowVariant(HousePhases[0]);
            foreach (var v in _all) { v.SetPaint(Color.white, 0f); v.SetDecayImmediate(1f); }
        }

        void NextPhase()
        {
            if (_phase >= HousePhases.Length - 1) return;
            _phase++;
            foreach (var h in _houses) h.ShowVariant(HousePhases[_phase]);
            // Färgen återvänder stegvis; sista fasen spelar hela vågen till full färg.
            float target = _phase == HousePhases.Length - 1 ? 0f : 1f - _phase * 0.25f;
            foreach (var v in _all) v.AnimateDecay(target);
        }

        void CyclePaint()
        {
            _paint = (_paint + 1) % paintOptions.Length;
            foreach (var h in _houses) h.SetPaint(paintOptions[_paint]);
        }

        void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.dpi / 160f);
            _style ??= new GUIStyle(GUI.skin.button);
            _style.fontSize = Mathf.RoundToInt(16 * scale);
            float w = 220 * scale, h = 48 * scale, pad = 12 * scale;

            GUI.Label(new Rect(pad, pad, w, h), $"<size={_style.fontSize}><b>{_fpsSmoothed:0} FPS</b></size>");
            float y = pad + h;
            string label = _phase < HousePhases.Length - 1 ? $"Renovera ({HousePhases[_phase + 1]})" : "Klart!";
            if (GUI.Button(new Rect(pad, y, w, h), label, _style)) NextPhase();
            y += h + pad;
            if (GUI.Button(new Rect(pad, y, w, h), "Byt målarfärg", _style)) CyclePaint();
            y += h + pad;
            if (GUI.Button(new Rect(pad, y, w, h), "Återställ", _style)) ResetAll();
        }
    }
}
