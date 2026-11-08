using NG.Core;
using NG.Game;
using UnityEngine;

namespace NG.World
{
    /// <summary>
    /// "Juice": småeffekter som får spelet att kännas levande. Lyssnar på händelser från spellogiken och
    /// skapar partiklar: damm när ogräs rensas, vattenstänk när gräs vattnas, glitter när ett hus blir klart,
    /// konfetti vid ny nivå. Allt byggs i kod med ett och samma partikelmaterial, så inga extra filer behövs.
    /// (Ljud kopplas in här senare: samma händelser.)
    /// </summary>
    public class Juice : MonoBehaviour
    {
        [Tooltip("URP Particles/Unlit-material. Sätts av NG > Setup > Build Game Scene.")]
        public Material particleMaterial;

        GameSession _s;
        WorldView _world;
        Camera _cam;

        static readonly Color Dust1 = new Color(0.62f, 0.5f, 0.35f);
        static readonly Color Dust2 = new Color(0.82f, 0.74f, 0.58f);
        static readonly Color Water1 = new Color(0.45f, 0.75f, 1f);
        static readonly Color Water2 = new Color(0.85f, 0.95f, 1f);
        static readonly Color Leaf = new Color(0.45f, 0.8f, 0.35f);
        static readonly Color Gold = new Color(1f, 0.84f, 0.3f);
        static readonly Color Pink = new Color(1f, 0.6f, 0.78f);

        public void Bind(GameSession session, WorldView world)
        {
            _s = session;
            _world = world;
            _cam = Camera.main;
            EventBus.Subscribe<ClutterCleared>(OnClutter);
            EventBus.Subscribe<RenovationCompleted>(OnRenovated);
            EventBus.Subscribe<LevelUp>(OnLevelUp);
        }

        void OnDestroy()
        {
            EventBus.Unsubscribe<ClutterCleared>(OnClutter);
            EventBus.Unsubscribe<RenovationCompleted>(OnRenovated);
            EventBus.Unsubscribe<LevelUp>(OnLevelUp);
        }

        void OnClutter(ClutterCleared e)
        {
            if (e.Watered)
                Burst(e.Position + Vector3.up * 0.3f, Water1, Water2, 18, 2.2f, 1.6f, 0.14f, 0.6f, 0.3f);
            else
            {
                Burst(e.Position + Vector3.up * 0.15f, Dust1, Dust2, 14, 1.4f, 0.4f, 0.22f, 0.7f, 0.25f);
                Burst(e.Position + Vector3.up * 0.2f, Leaf, Leaf, 6, 1.8f, 1.2f, 0.1f, 0.6f, 0.1f);
            }
        }

        void OnRenovated(RenovationCompleted e)
        {
            var o = _s.World.Get(e.InstanceId);
            if (o == null) return;
            var pos = _s.World.CellToWorld(_s.World.Def(o), o.x, o.y, o.rotation);
            Burst(pos + Vector3.up * 2f, Gold, Pink, 60, 4f, 0.6f, 0.2f, 1.4f, 2f);
            Burst(pos + Vector3.up * 0.5f, Leaf, Gold, 30, 2.5f, 0.8f, 0.15f, 1.2f, 3f);
        }

        void OnLevelUp(LevelUp e)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            var ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float d)) return;
            var center = ray.GetPoint(d);
            Burst(center + Vector3.up * 4f, Gold, Pink, 120, 6f, 1.2f, 0.25f, 2f, 3f);
            Burst(center + Vector3.up * 4f, new Color(0.5f, 0.8f, 1f), new Color(0.6f, 1f, 0.6f), 80, 5f, 1.2f, 0.25f, 2f, 3f);
        }

        /// <summary>Ett engångsutbrott av partiklar som städar bort sig själv.</summary>
        public void Burst(Vector3 position, Color a, Color b, int count, float speed, float gravity, float size, float life, float radius)
        {
            if (particleMaterial == null) return;
            var go = new GameObject("FX");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.useUnscaledTime = true;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.05f, radius);

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
            ps.Play();
        }
    }
}
