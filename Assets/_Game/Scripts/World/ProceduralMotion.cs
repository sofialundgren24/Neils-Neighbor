using System.Collections.Generic;
using UnityEngine;

namespace NG.World
{
    /// <summary>
    /// Rörelse utan animationsfiler: döp delar i Blender med ett prefix så rör sig de i spelet.
    ///   Swing_*   pendlar fram och tillbaka (gungsäten, skyltar, grindar). Origo = gångjärnet/upphängningen.
    ///   Spin_*    snurrar runt sin egen uppåtaxel (väderflöjlar, fontänens vatten, väderkvarnar).
    ///   Bob_*     guppar upp och ner (fåglar, näckrosor, ballonger).
    ///   Sway_*    vajar svagt i vinden (blommor, flaggor, tvättlinor). Origo = där den sitter fast.
    /// Hela träd och buskar vajar dessutom svagt om swayWhole är på (sätts automatiskt för växter).
    /// Varje exemplar får en egen slumpad takt, så att inte allt rör sig i exakt samma rytm.
    /// </summary>
    [DisallowMultipleComponent]
    public class ProceduralMotion : MonoBehaviour
    {
        [Tooltip("Hela objektet vajar svagt (träd, buskar, blommor).")]
        public bool swayWhole;
        [SerializeField] float swayDegrees = 1.5f;
        [SerializeField] float swingDegrees = 22f;
        [SerializeField] float spinDegreesPerSec = 90f;
        [SerializeField] float bobHeight = 0.08f;

        struct Part
        {
            public Transform t;
            public Quaternion rot;
            public Vector3 pos;
            public char kind;
        }

        readonly List<Part> _parts = new List<Part>();
        Transform _model;
        Quaternion _modelRot;
        float _phase;
        float _speed;

        void Awake()
        {
            _phase = Random.value * 10f;
            _speed = Random.Range(0.85f, 1.15f);
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                char kind = t.name.StartsWith("Swing_") ? 'w' : t.name.StartsWith("Spin_") ? 's'
                    : t.name.StartsWith("Bob_") ? 'b' : t.name.StartsWith("Sway_") ? 'v' : '\0';
                if (kind != '\0') _parts.Add(new Part { t = t, rot = t.localRotation, pos = t.localPosition, kind = kind });
            }
            if (swayWhole && transform.childCount > 0)
            {
                _model = transform.GetChild(0);
                _modelRot = _model.localRotation;
            }
            enabled = _parts.Count > 0 || _model != null;
        }

        void Update()
        {
            float time = Time.time * _speed + _phase;
            if (_model != null)
            {
                float x = Mathf.Sin(time * 1.3f) * swayDegrees;
                float z = Mathf.Sin(time * 0.9f + 1.7f) * swayDegrees * 0.7f;
                _model.localRotation = _modelRot * Quaternion.Euler(x, 0f, z);
            }
            foreach (var p in _parts)
            {
                if (p.t == null) continue;
                switch (p.kind)
                {
                    case 'w': p.t.localRotation = p.rot * Quaternion.Euler(Mathf.Sin(time * 2.2f) * swingDegrees, 0f, 0f); break;
                    case 's': p.t.localRotation = p.rot * Quaternion.Euler(0f, (time * spinDegreesPerSec) % 360f, 0f); break;
                    case 'b': p.t.localPosition = p.pos + Vector3.up * (Mathf.Sin(time * 2f) * bobHeight); break;
                    case 'v': p.t.localRotation = p.rot * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.8f) * swayDegrees * 3f); break;
                }
            }
        }
    }
}
