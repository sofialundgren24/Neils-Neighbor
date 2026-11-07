using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NG.World
{
    /// <summary>
    /// Visuell sida av ett placerat objekt. Styr shaderparametrarna i NG/Stylized via
    /// MaterialPropertyBlock (bryter inte SRP-batchning av materialet) och växlar fas-/stadievarianter
    /// som är barnobjekt med namn som "Phase2_Painted" eller "Stage1_Growing".
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaceableView : MonoBehaviour
    {
        static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");
        static readonly int DirtId = Shader.PropertyToID("_DirtAmount");
        static readonly int PaintId = Shader.PropertyToID("_PaintColor");

        [Tooltip("Id på PlaceableDefinition som skapade objektet. Fylls av importeraren.")]
        public string definitionId;

        [Range(0f, 1f)] [SerializeField] float decay;
        [SerializeField] Color paint = new Color(1, 1, 1, 0);
        [SerializeField] float transformSeconds = 1.2f;

        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        readonly List<Transform> _variants = new List<Transform>();

        public float Decay => decay;

        void Awake()
        {
            CacheVariants();
            Apply();
        }

        void OnValidate()
        {
            if (!Application.isPlaying) return;
            Apply();
        }

        void CacheVariants()
        {
            _variants.Clear();
            _variants.AddRange(FindVariants(transform));
        }

        static readonly Regex VariantName = new Regex(@"^(Phase\d+_\w+|Stage\d+_\w+|Wilted|Scaffold)$");

        public static bool IsVariantName(string objectName) => VariantName.IsMatch(objectName);

        /// <summary>
        /// Hittar fas-/stadievarianter var som helst under roten (importerade modeller ligger ett steg ner).
        /// En variants egna barn räknas inte, så "Phase0_Decayed/Phase0_Walls" ger bara en variant.
        /// </summary>
        public static List<Transform> FindVariants(Transform root)
        {
            var result = new List<Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root || !IsVariantName(t.name)) continue;
                bool insideVariant = false;
                for (var p = t.parent; p != null && p != root; p = p.parent)
                    if (IsVariantName(p.name)) { insideVariant = true; break; }
                if (!insideVariant) result.Add(t);
            }
            return result;
        }

        public IReadOnlyList<Transform> Variants
        {
            get
            {
                if (_variants.Count == 0) CacheVariants();
                return _variants;
            }
        }

        /// <summary>Visar bara barnobjektet med detta namn bland fas-/stadievarianterna (byggnadsställning kan visas parallellt).</summary>
        public void ShowVariant(string variantName, bool showScaffold = false)
        {
            if (_variants.Count == 0) CacheVariants();
            foreach (var v in _variants)
                v.gameObject.SetActive(v.name == variantName || (showScaffold && v.name == "Scaffold"));
            _renderers = null; // aktiva renderare har ändrats
            Apply();
        }

        public void SetPaint(Color color, float amount = 1f)
        {
            paint = new Color(color.r, color.g, color.b, amount);
            Apply();
        }

        public void SetDecayImmediate(float value)
        {
            decay = Mathf.Clamp01(value);
            Apply();
        }

        /// <summary>Grå -> färg-vågen. Använder unscaled time så att den spelas även i pausade menyer.</summary>
        public Coroutine AnimateDecay(float target)
        {
            StopAllCoroutines();
            return StartCoroutine(AnimateDecayRoutine(Mathf.Clamp01(target)));
        }

        IEnumerator AnimateDecayRoutine(float target)
        {
            float start = decay;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, transformSeconds);
                float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // ease-out cubic
                decay = Mathf.Lerp(start, target, eased);
                Apply();
                yield return null;
            }
        }

        void Apply()
        {
            _renderers ??= GetComponentsInChildren<Renderer>(true);
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(DesaturateId, decay);
                _mpb.SetFloat(DirtId, decay * 0.6f);
                _mpb.SetColor(PaintId, paint);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
