using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NG.World
{
    /// <summary>
    /// Prototyp för fas 0 av visionsloopen:
    ///  1. Start: en nedgången gata där alla hus och deras trädgårdar är grå.
    ///  2. Renovering: varje hus går igenom faserna och färgen kommer tillbaka stegvis.
    ///  3. Klart: när sista fasen är klar spelas färgvågen och huset med trädgård låses i full färg för gott.
    /// Visar också FPS så att grinden "30 fps på mobil" kan mätas på enhet. Tas bort efter fas 0.
    /// </summary>
    public class StyleTestDemo : MonoBehaviour
    {
        static readonly string[] HousePhases = { "Phase0_Decayed", "Phase1_Repaired", "Phase2_Painted", "Phase3_Decorated" };

        /// <summary>Förfall efter varje fas medan renoveringen pågår. Sista fasen låser full färg.</summary>
        static readonly float[] DecayPerPhase = { 1f, 0.7f, 0.4f, 0f };

        [Tooltip("Växter och dekor inom detta avstånd (m) från ett hus räknas till husets trädgård.")]
        [SerializeField] float gardenRadius = 6f;

        [SerializeField] Color[] paintOptions =
        {
            new Color(0.56f, 0.83f, 1f),   // facade_sky
            new Color(1f, 0.71f, 0.64f),   // facade_peach
            new Color(1f, 0.89f, 0.48f),   // facade_butter
            new Color(0.64f, 0.36f, 1f)    // lila
        };

        class House
        {
            public PlaceableView view;
            public readonly List<PlaceableView> garden = new List<PlaceableView>();
            public int phase;
            public int paint = -1;
            public bool Done => phase >= HousePhases.Length - 1;
        }

        readonly List<House> _houses = new List<House>();
        readonly List<PlaceableView> _unowned = new List<PlaceableView>();
        float _fpsSmoothed = 60f;
        GUIStyle _style;

        void Start()
        {
            var all = FindObjectsByType<PlaceableView>(FindObjectsSortMode.None).ToList();
            foreach (var v in all.Where(v => v.Variants.Any(t => t.name.StartsWith("Phase")))
                                 .OrderBy(v => v.transform.position.x))
                _houses.Add(new House { view = v });

            // Varje växt/dekor hör till närmaste hus inom gardenRadius. Övriga (gatans träd) blir grå
            // tills hela gatan är klar.
            foreach (var v in all.Where(v => _houses.All(h => h.view != v)))
            {
                var nearest = _houses
                    .OrderBy(h => Vector3.Distance(h.view.transform.position, v.transform.position))
                    .FirstOrDefault();
                if (nearest != null && Vector3.Distance(nearest.view.transform.position, v.transform.position) <= gardenRadius)
                    nearest.garden.Add(v);
                else
                    _unowned.Add(v);
            }
            ResetAll();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / dt, 0.05f);
        }

        /// <summary>Gör hela gatan grå och orenoverad igen. Bara för utveckling.</summary>
        void ResetAll()
        {
            foreach (var h in _houses)
            {
                h.phase = 0;
                h.paint = -1;
                h.view.ShowVariant(HousePhases[0]);
                h.view.SetPaint(Color.white, 0f);
                h.view.ResetToDecayedForDebug();
                foreach (var g in h.garden) g.ResetToDecayedForDebug();
            }
            foreach (var v in _unowned) v.ResetToDecayedForDebug();
        }

        House NextHouse() => _houses.FirstOrDefault(h => !h.Done);

        void RenovateNext()
        {
            var h = NextHouse();
            if (h == null) return;
            h.phase++;
            h.view.ShowVariant(HousePhases[h.phase]);

            if (h.Done)
            {
                // Sista fasen: färgvågen och permanent full färg för huset och trädgården.
                h.view.CompleteRenovation();
                foreach (var g in h.garden) g.CompleteRenovation();
                if (NextHouse() == null)
                    foreach (var v in _unowned) v.CompleteRenovation();
            }
            else
            {
                float target = DecayPerPhase[h.phase];
                h.view.AnimateDecay(target);
                foreach (var g in h.garden) g.AnimateDecay(target);
            }
        }

        void CyclePaint()
        {
            // Målarfärg går bara att välja på färdiga hus.
            foreach (var h in _houses.Where(h => h.Done))
            {
                h.paint = (h.paint + 1) % paintOptions.Length;
                h.view.SetPaint(paintOptions[h.paint]);
            }
        }

        void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.dpi / 160f);
            _style ??= new GUIStyle(GUI.skin.button);
            _style.fontSize = Mathf.RoundToInt(16 * scale);
            float w = 260 * scale, h = 48 * scale, pad = 12 * scale;

            int done = _houses.Count(x => x.Done);
            GUI.Label(new Rect(pad, pad, w * 2, h),
                $"<size={_style.fontSize}><b>{_fpsSmoothed:0} FPS   ·   {done}/{_houses.Count} hus klara</b></size>");
            float y = pad + h;

            var next = NextHouse();
            string label = next == null
                ? "Hela gatan är klar!"
                : $"Renovera hus {_houses.IndexOf(next) + 1}: {HousePhases[next.phase + 1]}";
            if (GUI.Button(new Rect(pad, y, w, h), label, _style)) RenovateNext();
            y += h + pad;
            GUI.enabled = done > 0;
            if (GUI.Button(new Rect(pad, y, w, h), "Byt målarfärg", _style)) CyclePaint();
            GUI.enabled = true;
            y += h + pad;
            if (GUI.Button(new Rect(pad, y, w, h), "Återställ (debug)", _style)) ResetAll();
        }
    }
}
