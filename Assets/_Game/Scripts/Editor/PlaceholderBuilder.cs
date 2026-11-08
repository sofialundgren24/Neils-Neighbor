using System.Collections.Generic;
using NG.World;
using UnityEditor;
using UnityEngine;

namespace NG.EditorTools
{
    /// <summary>
    /// Bygger enkla platshållare av Unitys grundformer (kub, sfär, cylinder) så att varje objekt i content.csv
    /// syns och går att spela med innan den riktiga modellen finns. Platshållarna har samma varianter
    /// (Phase0_Decayed ..., Stage0_Sprout ..., Scaffold) som de riktiga modellerna ska ha.
    /// </summary>
    public static class PlaceholderBuilder
    {
        static readonly string[] HousePhases = { "Phase0_Decayed", "Phase1_Repaired", "Phase2_Painted", "Phase3_Decorated" };
        static readonly string[] Stages = { "Stage0_Sprout", "Stage1_Growing", "Stage2_Bloom" };
        static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        static readonly Color Leaf = NGEditorPaths.Hex("#4CB04A");
        static readonly Color Soil = NGEditorPaths.Hex("#7A5235");
        static readonly Color Wood = NGEditorPaths.Hex("#C89B5E");
        static readonly Color Trunk = NGEditorPaths.Hex("#7A5235");

        public struct Spec
        {
            public string id;
            public string type;          // house, production, crop, flower, tree, bush, fence, decor, ground, water
            public Vector2Int footprint; // celler à 0,5 m
            public float height;         // meter
            public Color color;
            public Color? accent;        // t.ex. äpplen på äppelträdet
        }

        /// <summary>Skapar eller skriver över platshållar-prefaben på prefabPath.</summary>
        public static GameObject Build(Spec spec, string prefabPath)
        {
            float w = spec.footprint.x * PlotGrid.CellSize;
            float d = spec.footprint.y * PlotGrid.CellSize;
            float h = Mathf.Max(0.05f, spec.height);

            var root = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            switch (spec.type)
            {
                case "house": BuildHouse(model, w, d, h, spec.color); break;
                case "production": BuildProduction(model, w, d, h, spec.color); break;
                case "crop": BuildCrop(model, w, d, h, spec.color); break;
                case "flower": BuildFlower(model, w, d, h, spec.color); break;
                case "tree": BuildTree(model, w, d, h, spec.color, spec.accent); break;
                case "bush": BuildBush(model, w, d, h, spec.color, spec.footprint); break;
                case "fence": BuildFence(model, w, d, h, spec.color); break;
                case "ground": Prim(PrimitiveType.Cube, model, "Tile", new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), spec.color); break;
                case "water": BuildWater(model, w, d, h, spec.color); break;
                case "clutter": BuildClutter(model, w, d, h, spec.color, spec.id); break;
                case "community":
                    if (h >= 3f) BuildProduction(model, w, d, h, spec.color);
                    else BuildPlayground(model, w, d, h, spec.color);
                    break;
                default: BuildDecor(model, w, d, h, spec.color); break;
            }

            NGAssetImporter.SetInitialVariant(model, "Phase", "Phase0_Decayed");
            NGAssetImporter.SetInitialVariant(model, "Stage", "Stage2_Bloom");
            var scaffold = model.Find("Scaffold");
            if (scaffold != null) scaffold.gameObject.SetActive(false);

            float colliderH = Mathf.Max(h, 0.15f);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, colliderH * 0.5f, 0);
            box.size = new Vector3(w, colliderH, d);

            var view = root.AddComponent<PlaceableView>();
            view.definitionId = spec.id;
            root.AddComponent<PlaceholderMarker>();
            var motion = root.AddComponent<ProceduralMotion>();
            motion.swayWhole = spec.type == "tree" || spec.type == "bush" || spec.type == "flower";

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------- Typer ----------

        static void BuildHouse(Transform model, float w, float d, float h, Color color)
        {
            float bodyH = h * 0.6f;
            for (int i = 0; i < HousePhases.Length; i++)
            {
                var g = Group(model, HousePhases[i]);
                Color body = i == 0 ? NGEditorPaths.Hex("#8E8E8A") : i == 1 ? Color.Lerp(color, Color.white, 0.55f) : color;
                Color roof = i == 0 ? NGEditorPaths.Hex("#66666B") : i == 1 ? NGEditorPaths.Hex("#A0705A") : NGEditorPaths.Hex("#E8604C");
                HouseShell(g, w, d, bodyH, body, roof, boarded: i == 0);

                if (i == 0)
                    Prim(PrimitiveType.Cube, g, "LoosePlank", new Vector3(w * 0.2f, bodyH * 0.35f, -d * 0.38f - 0.06f),
                        new Vector3(w * 0.5f, 0.12f, 0.05f), NGEditorPaths.Hex("#6E6458"), new Vector3(0, 0, 18));
                if (i == 3)
                {
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        Prim(PrimitiveType.Cube, g, "FlowerBox", new Vector3(sx * w * 0.22f, bodyH * 0.42f, -d * 0.38f - 0.12f),
                            new Vector3(0.7f, 0.16f, 0.22f), NGEditorPaths.Hex("#FF7FB0"));
                        Prim(PrimitiveType.Sphere, g, "Bush", new Vector3(sx * w * 0.42f, 0.3f, -d * 0.45f),
                            Vector3.one * 0.6f, Leaf);
                    }
                }
            }
            Scaffold(model, w, d, h);
        }

        static void BuildProduction(Transform model, float w, float d, float h, Color color)
        {
            var g = model;
            float bodyH = h * 0.6f;
            HouseShell(g, w, d, bodyH, color, Color.Lerp(color, Color.black, 0.35f), boarded: false);
            Prim(PrimitiveType.Cube, g, "Awning", new Vector3(0, bodyH * 0.78f, -d * 0.38f - 0.35f),
                new Vector3(w * 0.75f, 0.1f, 0.8f), Color.white, new Vector3(-18, 0, 0));
            Prim(PrimitiveType.Cube, g, "Sign", new Vector3(0, bodyH + 0.1f, -d * 0.38f - 0.05f),
                new Vector3(w * 0.4f, 0.45f, 0.08f), NGEditorPaths.Hex("#FFF4D6"));
            Scaffold(model, w, d, h);
        }

        static void HouseShell(Transform g, float w, float d, float bodyH, Color body, Color roof, bool boarded)
        {
            float bw = w * 0.8f, bd = d * 0.75f;
            Prim(PrimitiveType.Cube, g, "Body", new Vector3(0, bodyH * 0.5f, 0), new Vector3(bw, bodyH, bd), body);
            float s = bw / Mathf.Sqrt(2f);
            Prim(PrimitiveType.Cube, g, "Roof", new Vector3(0, bodyH, 0), new Vector3(s, s, bd * 1.1f), roof, new Vector3(0, 0, 45));
            float front = -bd * 0.5f - 0.03f;
            Prim(PrimitiveType.Cube, g, "Door", new Vector3(0, 0.55f, front), new Vector3(0.55f, 1.1f, 0.06f),
                boarded ? NGEditorPaths.Hex("#55504A") : NGEditorPaths.Hex("#7A4E36"));
            foreach (float sx in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, g, "Window", new Vector3(sx * bw * 0.28f, bodyH * 0.6f, front), new Vector3(0.6f, 0.6f, 0.06f),
                    boarded ? NGEditorPaths.Hex("#3A3A3A") : NGEditorPaths.Hex("#CFEFFF"));
        }

        static void Scaffold(Transform model, float w, float d, float h)
        {
            var g = Group(model, "Scaffold");
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cylinder, g, "Post", new Vector3(sx * w * 0.46f, h * 0.5f, sz * d * 0.44f),
                        new Vector3(0.1f, h * 0.5f, 0.1f), Wood);
            foreach (float sz in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, g, "Plank", new Vector3(0, h * 0.45f, sz * d * 0.44f), new Vector3(w * 0.95f, 0.08f, 0.25f), Wood);
        }

        static void BuildCrop(Transform model, float w, float d, float h, Color color)
        {
            Prim(PrimitiveType.Cube, model, "Soil", new Vector3(0, 0.04f, 0), new Vector3(w * 0.95f, 0.08f, d * 0.95f), Soil);
            var spots = new[] { new Vector2(-0.25f, -0.25f), new Vector2(0.25f, -0.25f), new Vector2(-0.25f, 0.25f), new Vector2(0.25f, 0.25f) };
            for (int st = 0; st < Stages.Length; st++)
            {
                var g = Group(model, Stages[st]);
                foreach (var p in spots)
                {
                    var pos = new Vector3(p.x * w, 0.08f, p.y * d);
                    if (st == 0) Prim(PrimitiveType.Sphere, g, "Sprout", pos + Vector3.up * 0.05f, Vector3.one * 0.12f, Leaf);
                    else
                    {
                        float stem = h * (st == 1 ? 0.5f : 0.8f);
                        Prim(PrimitiveType.Cylinder, g, "Stem", pos + Vector3.up * stem * 0.5f, new Vector3(0.06f, stem * 0.5f, 0.06f), Leaf);
                        Prim(PrimitiveType.Sphere, g, st == 1 ? "Leaves" : "Fruit", pos + Vector3.up * stem,
                            Vector3.one * (st == 1 ? 0.16f : 0.24f), st == 1 ? Leaf : color);
                    }
                }
            }
        }

        static void BuildFlower(Transform model, float w, float d, float h, Color color)
        {
            for (int st = 0; st < Stages.Length; st++)
            {
                var g = Group(model, Stages[st]);
                if (st == 0) { Prim(PrimitiveType.Sphere, g, "Sprout", new Vector3(0, 0.05f, 0), Vector3.one * 0.1f, Leaf); continue; }
                float stem = h * (st == 1 ? 0.55f : 0.85f);
                Prim(PrimitiveType.Cylinder, g, "Stem", new Vector3(0, stem * 0.5f, 0), new Vector3(0.04f, stem * 0.5f, 0.04f), Leaf);
                Prim(PrimitiveType.Sphere, g, "Leaf", new Vector3(0.06f, stem * 0.35f, 0), new Vector3(0.12f, 0.05f, 0.08f), Leaf);
                Prim(PrimitiveType.Sphere, g, st == 1 ? "Bud" : "Bloom", new Vector3(0, stem, 0),
                    Vector3.one * Mathf.Min(w, 0.5f) * (st == 1 ? 0.25f : 0.5f), st == 1 ? Leaf : color);
            }
        }

        static void BuildTree(Transform model, float w, float d, float h, Color canopy, Color? accent)
        {
            float[] scale = { 0.35f, 0.65f, 1f };
            for (int st = 0; st < Stages.Length; st++)
            {
                var g = Group(model, Stages[st]);
                float s = scale[st];
                float trunkH = h * 0.45f * s;
                Prim(PrimitiveType.Cylinder, g, "Trunk", new Vector3(0, trunkH * 0.5f, 0), new Vector3(0.25f * s, trunkH * 0.5f, 0.25f * s), Trunk);
                float c = Mathf.Min(w, d) * 0.9f * s;
                var top = new Vector3(0, trunkH + c * 0.4f, 0);
                Prim(PrimitiveType.Sphere, g, "Canopy", top, new Vector3(c, c * 0.85f, c), canopy);
                if (st == 2 && accent.HasValue)
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        Prim(PrimitiveType.Sphere, g, "Fruit", top + new Vector3(Mathf.Cos(a) * c * 0.42f, -c * 0.1f, Mathf.Sin(a) * c * 0.42f),
                            Vector3.one * 0.18f, accent.Value);
                    }
            }
        }

        static void BuildBush(Transform model, float w, float d, float h, Color color, Vector2Int fp)
        {
            float[] scale = { 0.4f, 0.7f, 1f };
            bool hedge = fp.x != fp.y;
            for (int st = 0; st < Stages.Length; st++)
            {
                var g = Group(model, Stages[st]);
                float s = scale[st];
                if (hedge) Prim(PrimitiveType.Cube, g, "Hedge", new Vector3(0, h * s * 0.5f, 0), new Vector3(w * 0.95f, h * s, d * 0.85f), color);
                else Prim(PrimitiveType.Sphere, g, "Bush", new Vector3(0, h * s * 0.5f, 0), new Vector3(w * 0.9f * s, h * s, d * 0.9f * s), color);
            }
        }

        static void BuildFence(Transform model, float w, float d, float h, Color color)
        {
            int posts = Mathf.Max(2, Mathf.RoundToInt(w / 0.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float x = -w * 0.5f + 0.05f + i * (w - 0.1f) / (posts - 1);
                Prim(PrimitiveType.Cube, model, "Post", new Vector3(x, h * 0.5f, 0), new Vector3(0.1f, h, 0.1f), color);
            }
            foreach (float y in new[] { 0.35f, 0.75f })
                Prim(PrimitiveType.Cube, model, "Rail", new Vector3(0, h * y, 0), new Vector3(w, 0.08f, 0.06f), color);
        }

        static void BuildWater(Transform model, float w, float d, float h, Color color)
        {
            Prim(PrimitiveType.Cube, model, "Rim", new Vector3(0, 0.06f, 0), new Vector3(w, 0.12f, d), NGEditorPaths.Hex("#B9B4A8"));
            Prim(PrimitiveType.Cube, model, "Water", new Vector3(0, 0.07f, 0), new Vector3(w * 0.85f, 0.13f, d * 0.85f), color);
            if (h > 1f)
            {
                // Springbrunn: pelare med en snurrande skål överst.
                Prim(PrimitiveType.Cylinder, model, "Pillar", new Vector3(0, h * 0.4f, 0), new Vector3(0.3f, h * 0.4f, 0.3f), NGEditorPaths.Hex("#E6E6E6"));
                var spin = Group(model, "Spin_Top");
                spin.localPosition = new Vector3(0, h * 0.8f, 0);
                Prim(PrimitiveType.Cylinder, spin, "Bowl", Vector3.zero, new Vector3(1.2f, 0.08f, 1.2f), NGEditorPaths.Hex("#E6E6E6"));
                Prim(PrimitiveType.Cube, spin, "Spray", new Vector3(0.35f, 0.15f, 0), new Vector3(0.12f, 0.3f, 0.12f), color);
            }
        }

        static void BuildClutter(Transform model, float w, float d, float h, Color color, string id)
        {
            if (id.Contains("grass"))
            {
                // Torr gräsplätt: platt brun ruta med några torra tuvor.
                Prim(PrimitiveType.Cube, model, "Patch", new Vector3(0, 0.02f, 0), new Vector3(w * 0.95f, 0.04f, d * 0.95f), color);
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Sphere, model, "Tuft", new Vector3((i - 1) * w * 0.28f, 0.06f, (i % 2 - 0.5f) * d * 0.3f),
                        new Vector3(0.18f, 0.1f, 0.18f), Color.Lerp(color, Color.yellow, 0.25f));
                return;
            }
            // Ogräs: spretiga tuvor.
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                Prim(PrimitiveType.Cube, model, "Leaf", new Vector3(Mathf.Cos(a) * 0.06f, h * 0.45f, Mathf.Sin(a) * 0.06f),
                    new Vector3(0.05f, h * 0.9f, 0.12f), color, new Vector3(0, i * 45f, (i % 2 == 0 ? 20 : -20)));
            }
            Prim(PrimitiveType.Sphere, model, "Root", new Vector3(0, 0.04f, 0), new Vector3(0.25f, 0.08f, 0.25f), Soil);
        }

        static void BuildPlayground(Transform model, float w, float d, float h, Color color)
        {
            // Lekredskap: ram av stolpar + färgad del (gungsäte/rutschkana/sandlådekant).
            Prim(PrimitiveType.Cube, model, "Base", new Vector3(0, 0.05f, 0), new Vector3(w * 0.9f, 0.1f, d * 0.9f), NGEditorPaths.Hex("#E8D6A8"));
            foreach (float sx in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, model, "Post", new Vector3(sx * w * 0.4f, h * 0.5f, 0), new Vector3(0.1f, h * 0.5f, 0.1f), Wood);
            Prim(PrimitiveType.Cube, model, "Beam", new Vector3(0, h, 0), new Vector3(w * 0.85f, 0.1f, 0.1f), Wood);
            var swing = Group(model, "Swing_Seat");
            swing.localPosition = new Vector3(0, h, 0);
            foreach (float sx in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, swing, "Rope", new Vector3(sx * w * 0.15f, -h * 0.32f, 0), new Vector3(0.03f, h * 0.64f, 0.03f), Color.white);
            Prim(PrimitiveType.Cube, swing, "Seat", new Vector3(0, -h * 0.65f, 0), new Vector3(w * 0.4f, 0.1f, d * 0.3f), color);
        }

        static void BuildDecor(Transform model, float w, float d, float h, Color color)
        {
            bool tallAndThin = h > 2f * Mathf.Max(w, d);
            if (tallAndThin)
            {
                // Lyktor, skyltar, fågelholkar: stolpe + topp.
                Prim(PrimitiveType.Cylinder, model, "Post", new Vector3(0, h * 0.42f, 0), new Vector3(0.12f, h * 0.42f, 0.12f), NGEditorPaths.Hex("#3A3A46"));
                Prim(PrimitiveType.Cube, model, "Top", new Vector3(0, h * 0.9f, 0), Vector3.one * Mathf.Min(0.45f, Mathf.Max(w, d)), color);
                return;
            }
            Prim(PrimitiveType.Cube, model, "Base", new Vector3(0, h * 0.4f, 0), new Vector3(w * 0.75f, h * 0.8f, d * 0.75f), color);
            Prim(PrimitiveType.Cube, model, "Top", new Vector3(0, h * 0.9f, 0), new Vector3(w * 0.45f, h * 0.2f, d * 0.45f),
                Color.Lerp(color, Color.white, 0.4f));
        }

        // ---------- Byggstenar ----------

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Color color, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
            return go;
        }

        /// <summary>Ett material per färg (NG/Stylized med _BaseColor), delas mellan alla platshållare.</summary>
        public static Material MaterialFor(Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            if (MaterialCache.TryGetValue(hex, out var cached) && cached != null) return cached;

            NGEditorPaths.EnsureFolder(NGEditorPaths.PlaceholderMaterials);
            string path = $"{NGEditorPaths.PlaceholderMaterials}/M_PH_{hex}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("NG/Stylized");
                mat = new Material(shader) { enableInstancing = true };
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_AOStrength", 0f);
                AssetDatabase.CreateAsset(mat, path);
            }
            MaterialCache[hex] = mat;
            return mat;
        }
    }
}
