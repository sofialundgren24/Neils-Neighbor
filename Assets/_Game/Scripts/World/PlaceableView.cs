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

        [Tooltip("1 = grå och sliten. Används bara för objekt som tillhör den nedgångna startgatan och ännu inte är renoverade. " +
                 "Allt som placeras senare i spelet har 0 och full färg.")]
        [Range(0f, 1f)] [SerializeField] float decay;
        [SerializeField] Color paint = new Color(1, 1, 1, 0);
        [SerializeField] float transformSeconds = 1.2f;

        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        readonly List<Transform> _variants = new List<Transform>();
        bool _colorLocked;

        public float Decay => decay;

        /// <summary>
        /// Sant när objektet är färdigrenoverat. Då är förfallet borta för gott: alla försök att sätta decay ignoreras.
        /// Motsvarar RenovationState.complete i sparfilen.
        /// </summary>
        public bool IsColorLocked => _colorLocked;

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

        /// <summary>Sätter förfallet direkt. Ignoreras om objektet redan är färdigrenoverat.</summary>
        public void SetDecayImmediate(float value)
        {
            if (_colorLocked) return;
            decay = Mathf.Clamp01(value);
            Apply();
        }

        /// <summary>
        /// Grå -> färg under en pågående renovering (till exempel delvis färg efter varje fas).
        /// Ignoreras om objektet redan är färdigrenoverat. Använder unscaled time så att den spelas även i pausade menyer.
        /// </summary>
        public Coroutine AnimateDecay(float target)
        {
            if (_colorLocked) return null;
            StopAllCoroutines();
            return StartCoroutine(AnimateDecayRoutine(Mathf.Clamp01(target), false));
        }

        /// <summary>
        /// Avslutar renoveringen: spelar färgvågen till full färg och låser sedan färgen permanent.
        /// Anropas en gång när sista fasen är klar, och direkt (animate = false) när en sparfil med
        /// renovationComplete laddas.
        /// </summary>
        public void CompleteRenovation(bool animate = true)
        {
            if (_colorLocked) return;
            StopAllCoroutines();
            if (animate && isActiveAndEnabled)
            {
                StartCoroutine(AnimateDecayRoutine(0f, true));
            }
            else
            {
                decay = 0f;
                _colorLocked = true;
                Apply();
            }
        }

        /// <summary>Bara för utveckling och tester: gör objektet grått och olåst igen.</summary>
        public void ResetToDecayedForDebug(float value = 1f)
        {
            StopAllCoroutines();
            _colorLocked = false;
            decay = Mathf.Clamp01(value);
            Apply();
        }

        IEnumerator AnimateDecayRoutine(float target, bool lockWhenDone)
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
            if (lockWhenDone)
            {
                decay = 0f;
                _colorLocked = true;
                Apply();
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
