using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Troia.EditorTools
{
    public enum FireSize { Tocha, Pequeno, Medio, Grande }

    public class HousePrefab
    {
        public GameObject Prefab;
        public HouseType Type;
        public Vector3 FireLocal;
    }

    /// <summary>Cria (ou atualiza mantendo os GUIDs) texturas, materiais, céu, pós-processamento e prefabs.</summary>
    public static class TroiaAssets
    {
        public const string Root = "Assets/Troia";
        public const string Gen = Root + "/Gerado";
        public const string TexDir = Gen + "/Texturas";
        public const string MatDir = Gen + "/Materiais";
        public const string MeshDir = Gen + "/Malhas";
        public const string PrefabDir = Gen + "/Prefabs";
        public const string TerrainDir = Gen + "/Terreno";
        public const string ConfigDir = Gen + "/Configuracoes";
        public const string SceneDir = Root + "/Cenas";
        public const string ScenePath = SceneDir + "/Fase1_NoiteDaInvasao.unity";

        /// <summary>Direção (do chão para) a lua: noroeste, ~30° de altura.</summary>
        public static readonly Vector3 MoonDir = new Vector3(-0.42f, 0.5f, 0.76f).normalized;

        public const StaticEditorFlags StaticFlags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                                                     StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;

        public enum Col { None, Box, Mesh }

        public static Material[] Pal;
        public static Material FireMat, SmokeMat, EmberMat, SkyMat, TerrainMat;
        public static Texture2D TexEarth, TexEarthN, TexCobble, TexCobbleN, TexGrass, TexGrassN, TexSand, TexSandN;
        public static VolumeProfile Profile;

        public static readonly List<HousePrefab> Houses = new List<HousePrefab>();
        public static readonly Dictionary<string, GameObject> Props = new Dictionary<string, GameObject>();
        public static readonly Dictionary<string, GameObject> Fires = new Dictionary<string, GameObject>();

        // =====================================================================================
        public static void CreateAll(Action<string, float> progress)
        {
            progress("Texturas e materiais", 0.05f);
            CreateMaterials();
            progress("Céu noturno", 0.25f);
            CreateSky();
            CreateVolumeProfile();
            progress("Efeitos de fogo", 0.32f);
            CreateFirePrefabs();
            progress("Casas e adereços", 0.38f);
            CreateHousePrefabs();
            CreatePropPrefabs();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ util
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static void EnsureFolders()
        {
            foreach (var p in new[]
                     {
                         TexDir, MatDir, MeshDir, PrefabDir + "/Casas", PrefabDir + "/Aderecos", PrefabDir + "/Efeitos",
                         TerrainDir, ConfigDir, SceneDir
                     })
                EnsureFolder(p);
        }

        /// <summary>Salva o asset; se já existir, copia por cima (mantém o GUID e as referências).</summary>
        public static T SaveAsset<T>(T obj, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null && existing != obj)
            {
                EditorUtility.CopySerialized(obj, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(obj);
                return existing;
            }
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        public static Mesh SaveMesh(Mesh m, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(m, path);
                return m;
            }
            existing.Clear();
            existing.indexFormat = m.indexFormat;
            existing.SetVertices(m.vertices);
            existing.SetNormals(m.normals);
            existing.SetTangents(m.tangents);
            existing.SetUVs(0, m.uv);
            existing.subMeshCount = m.subMeshCount;
            for (int i = 0; i < m.subMeshCount; i++) existing.SetTriangles(m.GetTriangles(i), i, false);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(m);
            return existing;
        }

        public static void AddCollider(GameObject go, Mesh mesh, Col col)
        {
            if (col == Col.Mesh) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            else if (col == Col.Box)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.center = mesh.bounds.center;
                bc.size = mesh.bounds.size;
            }
        }

        static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        // ------------------------------------------------------------------ texturas
        static Texture2D ImportTex(string path, bool normal, TextureWrapMode wrap = TextureWrapMode.Repeat, int max = 1024,
            bool mips = true, bool alpha = false, bool uncompressed = false)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal;
            ti.wrapMode = wrap;
            ti.mipmapEnabled = mips;
            ti.maxTextureSize = max;
            ti.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = alpha;
            ti.anisoLevel = 4;
            ti.textureCompression = uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D MakeTex(string name, TexGen.Px fn, int size, float normalStrength, out Texture2D normal)
        {
            TexGen.Bake(size, fn, out var col, out var hgt);
            string p = $"{TexDir}/{name}.png";
            TexGen.SavePng(p, col, size, size);
            var tex = ImportTex(p, false);
            normal = null;
            if (normalStrength > 0f)
            {
                string pn = $"{TexDir}/{name}_N.png";
                TexGen.SavePng(pn, TexGen.NormalMap(hgt, size, normalStrength), size, size);
                normal = ImportTex(pn, true);
            }
            return tex;
        }

        static Texture2D AlphaTex(string name, Color[] px, int s)
        {
            string p = $"{TexDir}/{name}.png";
            TexGen.SavePng(p, px, s, s);
            return ImportTex(p, false, TextureWrapMode.Clamp, 256, true, true);
        }

        // ------------------------------------------------------------------ materiais
        static Material Lit(string name, Texture2D albedo, Texture2D normal, Color tint, float smooth, float metal = 0f, float bump = 1f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            if (albedo != null)
            {
                m.SetTexture("_BaseMap", albedo);
                m.SetTexture("_MainTex", albedo);
            }
            m.SetColor("_BaseColor", tint);
            m.SetColor("_Color", tint);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", bump);
            }
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.enableInstancing = true;
            BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords, null);
            return SaveAsset(m, $"{MatDir}/{name}.mat");
        }

        static Material ParticleMat(string name, Texture2D tex, Color color, bool additive)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            BaseShaderGUI.SetMaterialKeywords(m, null, ParticleGUI.SetMaterialKeywords);
            return SaveAsset(m, $"{MatDir}/{name}.mat");
        }

        public static void CreateMaterials()
        {
            var stone = MakeTex("T_PedraMuralha", TexGen.Stone, 512, 5f, out var stoneN);
            var brick = MakeTex("T_Adobe", TexGen.MudBrick, 512, 4f, out var brickN);
            var plaster = MakeTex("T_Reboco", TexGen.Plaster, 512, 2f, out var plasterN);
            var wood = MakeTex("T_Madeira", TexGen.Wood, 512, 3f, out var woodN);
            TexEarth = MakeTex("T_Terra", TexGen.Earth, 512, 3f, out TexEarthN);
            var tile = MakeTex("T_Telha", TexGen.RoofTile, 512, 4f, out var tileN);
            TexCobble = MakeTex("T_Calcamento", TexGen.Cobble, 512, 5f, out TexCobbleN);
            var marble = MakeTex("T_Marmore", TexGen.Marble, 512, 1f, out var marbleN);
            TexGrass = MakeTex("T_Grama", TexGen.Grass, 512, 2f, out TexGrassN);
            TexSand = MakeTex("T_Areia", TexGen.Sand, 512, 1.5f, out TexSandN);
            var charred = MakeTex("T_Carvao", TexGen.Charred, 512, 3f, out var charredN);
            var cloth = MakeTex("T_Tecido", TexGen.Cloth, 256, 1f, out var clothN);

            Color white = Color.white;
            Pal = new Material[(int)M.Count];
            Pal[(int)M.Stone] = Lit("M_PedraMuralha", stone, stoneN, white, 0.15f);
            Pal[(int)M.MudBrick] = Lit("M_Adobe", brick, brickN, white, 0.1f);
            Pal[(int)M.Plaster] = Lit("M_Reboco", plaster, plasterN, C(0.92f, 0.86f, 0.76f), 0.12f);
            Pal[(int)M.PlasterOchre] = Lit("M_Reboco_Ocre", plaster, plasterN, C(0.95f, 0.76f, 0.5f), 0.12f);
            Pal[(int)M.PlasterRed] = Lit("M_Reboco_Vermelho", plaster, plasterN, C(0.75f, 0.36f, 0.28f), 0.12f);
            Pal[(int)M.PlasterWhite] = Lit("M_Reboco_Branco", plaster, plasterN, C(1f, 0.98f, 0.95f), 0.15f);
            Pal[(int)M.Terracotta] = Lit("M_Terracota", plaster, plasterN, C(0.72f, 0.38f, 0.24f), 0.25f);
            Pal[(int)M.Straw] = Lit("M_Palha", plaster, plasterN, C(0.85f, 0.72f, 0.42f), 0.05f);
            Pal[(int)M.Wood] = Lit("M_Madeira", wood, woodN, white, 0.2f);
            Pal[(int)M.WoodDark] = Lit("M_Madeira_Escura", wood, woodN, C(0.55f, 0.5f, 0.48f), 0.2f);
            Pal[(int)M.WoodPainted] = Lit("M_Madeira_Pintada", wood, woodN, C(0.78f, 0.2f, 0.14f), 0.3f);
            Pal[(int)M.Roof] = Lit("M_Telhado_Terra", TexEarth, TexEarthN, C(0.85f, 0.8f, 0.75f), 0.05f);
            Pal[(int)M.RoofTile] = Lit("M_Telha", tile, tileN, white, 0.25f);
            Pal[(int)M.Cobble] = Lit("M_Calcamento", TexCobble, TexCobbleN, white, 0.2f);
            Pal[(int)M.Marble] = Lit("M_Marmore", marble, marbleN, white, 0.45f);
            Pal[(int)M.Bronze] = Lit("M_Bronze", plaster, plasterN, C(0.72f, 0.5f, 0.28f), 0.55f, 0.85f);
            Pal[(int)M.Gold] = Lit("M_Ouro", plaster, null, C(1f, 0.78f, 0.34f), 0.7f, 1f);
            Pal[(int)M.Cloth] = Lit("M_Tecido_Vermelho", cloth, clothN, C(0.6f, 0.12f, 0.1f), 0.1f);
            Pal[(int)M.ClothBlue] = Lit("M_Tecido_Azul", cloth, clothN, C(0.18f, 0.25f, 0.5f), 0.1f);
            Pal[(int)M.Dark] = Lit("M_Abertura_Escura", null, null, C(0.02f, 0.018f, 0.016f), 0f);
            Pal[(int)M.Charred] = Lit("M_Carbonizado", charred, charredN, white, 0.08f);
            Pal[(int)M.Rope] = Lit("M_Corda", cloth, clothN, C(0.55f, 0.45f, 0.3f), 0.05f);
            Pal[(int)M.Foliage] = Lit("M_Folhagem", TexGrass, TexGrassN, C(0.45f, 0.6f, 0.35f), 0.15f);
            Pal[(int)M.Water] = Lit("M_Agua", null, null, C(0.02f, 0.04f, 0.07f), 0.95f);

            var flame = AlphaTex("T_Particula_Chama", TexGen.Flame(128), 128);
            var smoke = AlphaTex("T_Particula_Fumaca", TexGen.Smoke(128), 128);
            var spark = AlphaTex("T_Particula_Brasa", TexGen.Spark(64), 64);
            FireMat = ParticleMat("M_Fogo", flame, C(2.6f, 1.25f, 0.42f), true);
            SmokeMat = ParticleMat("M_Fumaca", smoke, C(1f, 1f, 1f, 1f), false);
            EmberMat = ParticleMat("M_Brasa", spark, C(5f, 2f, 0.5f), true);

            var tm = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "M_Terreno" };
            tm.EnableKeyword("_NORMALMAP");
            TerrainMat = SaveAsset(tm, $"{MatDir}/M_Terreno.mat");
        }

        // ------------------------------------------------------------------ céu
        public static void CreateSky()
        {
            const int W = 2048, H = 1024;
            var px = new Color[W * H];
            Vector3 md = MoonDir;
            Vector3 mt = Vector3.Cross(Vector3.up, md).normalized;
            Vector3 mb = Vector3.Cross(md, mt);
            const float moonR = 0.03f;
            Color zenith = C(0.02f, 0.026f, 0.06f), horizon = C(0.1f, 0.1f, 0.15f);
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                float lat = (1f - v) * Mathf.PI;
                float sl = Mathf.Sin(lat), cl = Mathf.Cos(lat);
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W;
                    float lon = (0.5f - u) * Mathf.PI * 2f;
                    var d = new Vector3(sl * Mathf.Cos(lon), cl, sl * Mathf.Sin(lon));
                    float gv = TexGen.Fbm(u, 0.5f, 6, 1, 3, 7);
                    Color c;
                    if (d.y >= 0f)
                    {
                        c = Color.Lerp(zenith, horizon, Mathf.Pow(1f - d.y, 3f));
                        // brilho alaranjado da cidade em chamas no horizonte
                        c += C(0.42f, 0.13f, 0.045f) * Mathf.Exp(-d.y * 8f) * (0.45f + 0.9f * gv);
                        // estrelas
                        if (TexGen.Hash(x, y, 999) < 0.0035f * sl)
                        {
                            float br = (Mathf.Pow(TexGen.Hash(x, y, 1000), 5f) * 1.6f + 0.18f) * TexGen.Smooth(0.03f, 0.3f, d.y);
                            float tint = TexGen.Hash(x, y, 1001);
                            c += C(0.85f + 0.15f * tint, 0.9f, 1f - 0.15f * tint) * br;
                        }
                        // halo e disco da lua
                        float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, md), -1f, 1f));
                        c += C(0.25f, 0.28f, 0.36f) * (Mathf.Exp(-ang / 0.05f) * 0.7f) + C(0.06f, 0.07f, 0.1f) * Mathf.Exp(-ang / 0.3f);
                        if (ang < moonR)
                        {
                            float a = Vector3.Dot(d, mt) / moonR, bb = Vector3.Dot(d, mb) / moonR;
                            float limb = Mathf.Sqrt(Mathf.Max(0f, 1f - (a * a + bb * bb)));
                            float maria = Mathf.PerlinNoise(a * 2.2f + 10f, bb * 2.2f + 3f);
                            Color moon = C(0.98f, 0.96f, 0.88f) * (0.75f + 0.25f * limb) * (0.78f + 0.3f * maria);
                            c = Color.Lerp(c, moon, TexGen.Smooth(moonR, moonR * 0.9f, ang));
                        }
                        // nuvens de fumaça iluminadas por baixo pelo incêndio
                        float cloud = TexGen.Smooth(0.5f, 0.78f, TexGen.Fbm(u, v, 8, 4, 5, 13)) * TexGen.Smooth(-0.02f, 0.15f, d.y);
                        Color cc = C(0.035f, 0.03f, 0.035f) + C(0.3f, 0.09f, 0.03f) * Mathf.Exp(-d.y * 5f) * (0.5f + gv);
                        c = Color.Lerp(c, cc, cloud * 0.85f);
                    }
                    else
                    {
                        Color hz = horizon + C(0.42f, 0.13f, 0.045f) * (0.45f + 0.9f * gv);
                        c = Color.Lerp(hz, C(0.03f, 0.025f, 0.028f), TexGen.Smooth(0f, 0.25f, -d.y));
                    }
                    float dither = (TexGen.Hash(x, y, 77) - 0.5f) / 255f;
                    px[y * W + x] = new Color(c.r + dither, c.g + dither, c.b + dither, 1f);
                }
            }
            string path = $"{TexDir}/T_CeuNoturno.png";
            TexGen.SavePng(path, px, W, H);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;
            ti.wrapModeU = TextureWrapMode.Repeat;
            ti.wrapModeV = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            var sky = new Material(Shader.Find("Skybox/Panoramic")) { name = "M_CeuNoturno" };
            sky.SetTexture("_MainTex", tex);
            sky.SetFloat("_Mapping", 1f);
            sky.SetFloat("_ImageType", 0f);
            sky.SetFloat("_Exposure", 1f);
            sky.SetFloat("_Rotation", 0f);
            sky.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            sky.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            SkyMat = SaveAsset(sky, $"{MatDir}/M_CeuNoturno.mat");
        }

        // ------------------------------------------------------------------ pós-processamento
        public static void CreateVolumeProfile()
        {
            string path = $"{ConfigDir}/PosProcessamento_Noite.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var bloom = p.Add<Bloom>();
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.7f);
            bloom.tint.Override(new Color(1f, 0.82f, 0.7f));
            var tm = p.Add<Tonemapping>();
            tm.mode.Override(TonemappingMode.ACES);
            var ca = p.Add<ColorAdjustments>();
            ca.postExposure.Override(0.6f);
            ca.contrast.Override(12f);
            ca.saturation.Override(-4f);
            var wb = p.Add<WhiteBalance>();
            wb.temperature.Override(-10f);
            var vg = p.Add<Vignette>();
            vg.intensity.Override(0.3f);
            vg.smoothness.Override(0.45f);
            var fg = p.Add<FilmGrain>();
            fg.type.Override(FilmGrainLookup.Thin1);
            fg.intensity.Override(0.12f);
            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            Profile = p;
        }

        // ------------------------------------------------------------------ fogo
        static Gradient Grad((Color c, float t)[] cols, (float a, float t)[] alphas)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[cols.Length];
            for (int i = 0; i < cols.Length; i++) ck[i] = new GradientColorKey(cols[i].c, cols[i].t);
            var ak = new GradientAlphaKey[alphas.Length];
            for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i].a, alphas[i].t);
            g.SetKeys(ck, ak);
            return g;
        }

        static ParticleSystem NewPS(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 1.5f;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ps;
        }

        public static GameObject CreateFireObject(string name, FireSize size, bool light, bool smoke, bool embers)
        {
            float s = size == FireSize.Tocha ? 0.28f : size == FireSize.Pequeno ? 0.45f : size == FireSize.Medio ? 0.85f : 1.4f;
            var root = new GameObject(name);

            // chamas
            {
                var ps = NewPS("Chamas", root.transform, FireMat);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f + 0.2f * s, 0.8f + 0.35f * s);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f + 0.8f * s, 1.3f + 1.4f * s);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f + 0.7f * s, 0.45f + 1.4f * s);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.5f), new Color(1f, 0.5f, 0.2f));
                main.maxParticles = 150;
                main.gravityModifier = -0.05f;
                var em = ps.emission;
                em.rateOverTime = 12f + 26f * s;
                var sh = ps.shape;
                sh.enabled = true;
                if (size == FireSize.Grande)
                {
                    sh.shapeType = ParticleSystemShapeType.Box;
                    sh.scale = new Vector3(2.4f, 2.4f, 0.2f);
                }
                else
                {
                    sh.shapeType = ParticleSystemShapeType.Cone;
                    sh.angle = 8f;
                    sh.radius = 0.12f + 0.5f * s;
                }
                sh.rotation = new Vector3(-90f, 0f, 0f);
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(Grad(
                    new[] { (new Color(1f, 0.95f, 0.75f), 0f), (new Color(1f, 0.6f, 0.2f), 0.35f), (new Color(0.75f, 0.2f, 0.06f), 0.75f), (new Color(0.3f, 0.06f, 0.02f), 1f) },
                    new[] { (0f, 0f), (1f, 0.12f), (0.8f, 0.5f), (0f, 1f) }));
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.25f)));
                var no = ps.noise;
                no.enabled = true;
                no.strength = 0.35f + 0.2f * s;
                no.frequency = 1.2f;
                no.scrollSpeed = 0.8f;
                var rol = ps.rotationOverLifetime;
                rol.enabled = true;
                rol.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            }

            if (smoke)
            {
                var ps = NewPS("Fumaca", root.transform, SmokeMat);
                ps.transform.localPosition = new Vector3(0f, 0.8f + 1.2f * s, 0f);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f + 0.6f * s, 2.2f + 1.2f * s);
                main.startSize = new ParticleSystem.MinMaxCurve(1.2f + 1.2f * s, 2.5f + 2.5f * s);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.14f, 0.13f, 0.55f), new Color(0.25f, 0.22f, 0.2f, 0.45f));
                main.maxParticles = 60;
                var em = ps.emission;
                em.rateOverTime = 2.5f + 4f * s;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 12f;
                sh.radius = 0.3f + 0.5f * s;
                sh.rotation = new Vector3(-90f, 0f, 0f);
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(Grad(
                    new[] { (new Color(1f, 0.6f, 0.38f), 0f), (new Color(0.55f, 0.48f, 0.45f), 0.3f), (new Color(0.38f, 0.36f, 0.35f), 1f) },
                    new[] { (0f, 0f), (0.75f, 0.12f), (0.5f, 0.5f), (0f, 1f) }));
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 2.2f)));
                var vol = ps.velocityOverLifetime;
                vol.enabled = true;
                vol.space = ParticleSystemSimulationSpace.World;
                vol.x = new ParticleSystem.MinMaxCurve(0.7f);
                vol.y = new ParticleSystem.MinMaxCurve(0.3f);
                vol.z = new ParticleSystem.MinMaxCurve(0.25f);
                var no = ps.noise;
                no.enabled = true;
                no.strength = 0.5f;
                no.frequency = 0.25f;
                ps.GetComponent<ParticleSystemRenderer>().sortingFudge = 20f;
            }

            if (embers)
            {
                var ps = NewPS("Brasas", root.transform, EmberMat);
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f + s, 3f + 2f * s);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.45f, 0.15f));
                main.maxParticles = 60;
                main.gravityModifier = -0.06f;
                var em = ps.emission;
                em.rateOverTime = 4f + 8f * s;
                var sh = ps.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 25f;
                sh.radius = 0.2f + 0.5f * s;
                sh.rotation = new Vector3(-90f, 0f, 0f);
                var no = ps.noise;
                no.enabled = true;
                no.strength = 1.0f;
                no.frequency = 1.5f;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(Grad(
                    new[] { (Color.white, 0f), (Color.white, 1f) },
                    new[] { (1f, 0f), (0.9f, 0.6f), (0f, 1f) }));
            }

            if (light)
            {
                var lg = new GameObject("Luz");
                lg.transform.SetParent(root.transform, false);
                lg.transform.localPosition = new Vector3(0f, 0.5f + 0.7f * s, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.55f, 0.24f);
                l.shadows = LightShadows.None;
                switch (size)
                {
                    case FireSize.Tocha: l.range = 8f; l.intensity = 3f; break;
                    case FireSize.Pequeno: l.range = 12f; l.intensity = 6f; break;
                    case FireSize.Medio: l.range = 16f; l.intensity = 9f; break;
                    default: l.range = 22f; l.intensity = 14f; break;
                }
                var fl = lg.AddComponent<Troia.FireFlicker>();
                fl.baseIntensity = l.intensity;
                fl.intensityVariation = l.intensity * 0.3f;
                fl.baseRange = l.range;
                fl.rangeVariation = l.range * 0.06f;
            }
            return root;
        }

        static void SaveFire(string name, FireSize size, bool light, bool smoke, bool embers)
        {
            var go = CreateFireObject(name, size, light, smoke, embers);
            Fires[name] = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/Efeitos/{name}.prefab");
            Object.DestroyImmediate(go);
        }

        static void CreateFirePrefabs()
        {
            Fires.Clear();
            SaveFire("Fogo_Tocha", FireSize.Tocha, false, false, false);
            SaveFire("Fogo_Tocha_Luz", FireSize.Tocha, true, false, false);
            SaveFire("Fogo_Pequeno", FireSize.Pequeno, false, true, true);
            SaveFire("Fogo_Pequeno_Luz", FireSize.Pequeno, true, true, true);
            SaveFire("Fogo_Medio", FireSize.Medio, false, true, true);
            SaveFire("Fogo_Medio_Luz", FireSize.Medio, true, true, true);
            SaveFire("Fogo_Grande", FireSize.Grande, false, true, true);
            SaveFire("Fogo_Grande_Luz", FireSize.Grande, true, true, true);
            SaveFire("Fogo_Distante", FireSize.Grande, false, false, false);
        }

        // ------------------------------------------------------------------ prefabs de malha
        public static GameObject MeshPrefab(string name, string folder, MeshBuilder b, Col col, Action<GameObject> extra = null)
        {
            var mesh = SaveMesh(b.ToMesh(name, Pal, out var mats), $"{MeshDir}/{name}.asset");
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            AddCollider(go, mesh, col);
            GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);
            extra?.Invoke(go);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{folder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static void CreateHousePrefabs()
        {
            Houses.Clear();
            var specs = new (HouseType t, int n)[]
            {
                (HouseType.Terrea, 3), (HouseType.Escada, 2), (HouseType.Sobrado, 2),
                (HouseType.Alta, 2), (HouseType.Loja, 2), (HouseType.Queimada, 2)
            };
            foreach (var (t, n) in specs)
            {
                for (int i = 0; i < n; i++)
                {
                    var b = TroiaMeshes.House(t, i, out Vector3 fire);
                    var pf = MeshPrefab($"Casa_{t}_{i + 1}", "Casas", b, Col.Mesh);
                    Houses.Add(new HousePrefab { Prefab = pf, Type = t, FireLocal = fire });
                }
            }
        }

        static void AttachFire(GameObject go, FireSize size, bool light, bool smoke, Vector3 local)
        {
            var f = CreateFireObject("Fogo", size, light, smoke, true);
            f.transform.SetParent(go.transform, false);
            f.transform.localPosition = local;
        }

        static void CreatePropPrefabs()
        {
            Props.Clear();
            Props["Caixote"] = MeshPrefab("Caixote", "Aderecos", TroiaMeshes.Crate(), Col.Box);
            Props["Anfora"] = MeshPrefab("Anfora", "Aderecos", TroiaMeshes.AmphoraProp(), Col.Box);
            Props["Cesto"] = MeshPrefab("Cesto", "Aderecos", TroiaMeshes.BasketProp(), Col.Box);
            Props["Mesa_Banquete"] = MeshPrefab("Mesa_Banquete", "Aderecos", TroiaMeshes.Table(), Col.Box);
            Props["Banco"] = MeshPrefab("Banco", "Aderecos", TroiaMeshes.Bench(), Col.Box);
            Props["Carroca"] = MeshPrefab("Carroca", "Aderecos", TroiaMeshes.Cart(false), Col.Box);
            Props["Carroca_Feno"] = MeshPrefab("Carroca_Feno", "Aderecos", TroiaMeshes.Cart(true), Col.Box);
            Props["Barraca_Vermelha"] = MeshPrefab("Barraca_Vermelha", "Aderecos", TroiaMeshes.Stall(M.Cloth), Col.Box);
            Props["Barraca_Azul"] = MeshPrefab("Barraca_Azul", "Aderecos", TroiaMeshes.Stall(M.ClothBlue), Col.Box);
            Props["Braseiro"] = MeshPrefab("Braseiro", "Aderecos", TroiaMeshes.Brazier(), Col.Box,
                go => AttachFire(go, FireSize.Pequeno, true, true, new Vector3(0f, 1.15f, 0f)));
            Props["Tocha"] = MeshPrefab("Tocha", "Aderecos", TroiaMeshes.TorchPole(), Col.Box,
                go => AttachFire(go, FireSize.Tocha, false, false, new Vector3(0f, 2.5f, 0f)));
            Props["Tocha_Luz"] = MeshPrefab("Tocha_Luz", "Aderecos", TroiaMeshes.TorchPole(), Col.Box,
                go => AttachFire(go, FireSize.Tocha, true, false, new Vector3(0f, 2.5f, 0f)));
            Props["Escombros_Pequenos"] = MeshPrefab("Escombros_Pequenos", "Aderecos", TroiaMeshes.Rubble(11, 1.6f, 14), Col.Mesh);
            Props["Escombros_Grandes"] = MeshPrefab("Escombros_Grandes", "Aderecos", TroiaMeshes.Rubble(23, 3.2f, 30), Col.Mesh);
            Props["Barricada"] = MeshPrefab("Barricada", "Aderecos", TroiaMeshes.Barricade(), Col.Box);
            Props["Estatua_Bronze"] = MeshPrefab("Estatua_Bronze", "Aderecos", TroiaMeshes.Statue(true), Col.Box);
            Props["Estatua_Marmore"] = MeshPrefab("Estatua_Marmore", "Aderecos", TroiaMeshes.Statue(false), Col.Box);
            Props["Suporte_Armas"] = MeshPrefab("Suporte_Armas", "Aderecos", TroiaMeshes.WeaponRack(), Col.Box);
            Props["Altar_Pequeno"] = MeshPrefab("Altar_Pequeno", "Aderecos", TroiaMeshes.SmallAltar(), Col.Box,
                go => AttachFire(go, FireSize.Pequeno, true, true, new Vector3(0f, 1.3f, 0f)));
            Props["Navio_Grego"] = MeshPrefab("Navio_Grego", "Aderecos", TroiaMeshes.Ship(), Col.None);
        }
    }
}
