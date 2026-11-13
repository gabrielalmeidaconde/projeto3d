using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using P = Troia.EditorTools.PatrocloMeshes;
using F = Troia.EditorTools.PatrocloFit;

namespace Troia.EditorTools
{
    /// <summary>
    /// Gera o Pátroclo: corpo realista do MakeHuman (CC0) com armadura feita sob medida em cima dele,
    /// materiais com texturas escaneadas do Poly Haven (CC0), prefab com esqueleto e cena-vitrine.
    /// Menu: Troia > Personagem > Gerar Pátroclo.
    /// </summary>
    public static class PatrocloBuilder
    {
        public const string Root = "Assets/Troia/Personagens/Patroclo";
        const string TexDir = Root + "/Texturas", MatDir = Root + "/Materiais", MeshDir = Root + "/Malhas";
        const string PhDir = TexDir + "/PolyHaven", MhTexDir = TexDir + "/MakeHuman";
        const string JsonPath = Root + "/Fonte/patroclo_mh.json";
        public const string PrefabPath = Root + "/Patroclo.prefab";
        public const string ScenePath = Root + "/Patroclo_Vitrine.unity";

        static Material[] _pal;
        static Material _skin, _eyes, _brows, _lashes, _hair;

        [MenuItem("Troia/Personagem/Gerar Pátroclo", priority = 40)]
        static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void BuildFromCommandLine() => Build();

        public static void Build()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // malhas e materiais são 100% gerados: recria do zero
            AssetDatabase.DeleteAsset(MeshDir);
            AssetDatabase.DeleteAsset(MatDir);
            foreach (var p in new[] { TexDir, MatDir, MeshDir }) TroiaAssets.EnsureFolder(p);
            CreateMaterials();
            var root = BuildCharacter();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            BuildShowroom(prefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Troia] Pátroclo gerado: {PrefabPath} e {ScenePath}");
        }

        // =====================================================================================
        // Materiais
        // =====================================================================================
        enum TexKind { Color, ColorAlpha, Normal, Linear }

        static Texture2D Tex(string path, TexKind kind, int maxSize = 4096)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ti = (TextureImporter)AssetImporter.GetAtPath(path);
            }
            var type = kind == TexKind.Normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool srgb = kind == TexKind.Color || kind == TexKind.ColorAlpha;
            var alpha = kind == TexKind.ColorAlpha || kind == TexKind.Linear ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            bool transp = kind == TexKind.ColorAlpha;
            if (ti.textureType != type || ti.sRGBTexture != srgb || ti.alphaSource != alpha || ti.alphaIsTransparency != transp ||
                ti.wrapMode != TextureWrapMode.Repeat || ti.anisoLevel != 16 || ti.maxTextureSize != maxSize ||
                ti.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                ti.textureType = type;
                ti.sRGBTexture = srgb;
                ti.alphaSource = alpha;
                ti.alphaIsTransparency = transp;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.anisoLevel = 16;
                ti.maxTextureSize = maxSize;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D Procedural(string name, TexGen.Px fn, int size, float normal, out Texture2D n)
        {
            TexGen.Bake(size, fn, out var col, out var hgt);
            string p = $"{TexDir}/{name}.png", pn = $"{TexDir}/{name}_N.png";
            TexGen.SavePng(p, col, size, size);
            TexGen.SavePng(pn, TexGen.NormalMap(hgt, size, normal), size, size);
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(pn, ImportAssetOptions.ForceUpdate);
            n = Tex(pn, TexKind.Normal);
            return Tex(p, TexKind.Color);
        }

        static Material Lit(string name, Texture2D albedo, Texture2D normal, Texture2D ms, Color c, float smooth, float metal = 0f,
            bool emissive = false, float bump = 1f, bool alphaClip = false, float cutoff = 0.5f, bool twoSided = false, bool transparent = false,
            bool character = true)
        {
            // peças do personagem usam o Troia/Personagem (Lit + oclusão assada nos vértices + desgaste de batalha)
            var charShader = character && !transparent ? Shader.Find("Troia/Personagem") : null;
            var m = new Material(charShader != null ? charShader : Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            if (charShader != null)
            {
                // a quantidade de sujeira e de sangue é o painel de design que define (PatrocloDesign.Apply)
                m.SetTexture("_GrimeMap", _grime);
                m.SetFloat("_OcclusionStrength", 0f);
                m.SetFloat("_Parallax", 0f);
            }
            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", bump);
            }
            if (ms != null) m.SetTexture("_MetallicGlossMap", ms);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (alphaClip)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", cutoff);
            }
            if (twoSided) m.SetFloat("_Cull", 0f);
            if (transparent)
            {
                // mistura suave por alfa (pelos finos: sobrancelhas e cílios)
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_ZWrite", 0f);
            }
            // emissão "ligada" com valor quase zero: o painel de design acende por MaterialPropertyBlock
            m.SetColor("_EmissionColor", emissive ? new Color(0.002f, 0.002f, 0.002f) : Color.black);
            m.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords, null);
            return TroiaAssets.SaveAsset(m, $"{MatDir}/{name}.mat");
        }

        static void CreateMaterials()
        {
            // texturas antigas do personagem procedural que não são mais usadas
            foreach (var old in new[] { "T_Pat_BronzeMartelado", "T_Pat_Couro", "T_Pat_Linho", "T_Pat_Pele", "T_Pat_Cabelo" })
            {
                AssetDatabase.DeleteAsset($"{TexDir}/{old}.png");
                AssetDatabase.DeleteAsset($"{TexDir}/{old}_N.png");
            }
            _grime = Tex($"{TexDir}/Desgaste/sujeira.png", TexKind.Linear, 1024);
            var crina = Procedural("T_Pat_Crina", TexGen.HairStrands, 512, 3f, out var crinaN);
            Procedural("T_Pat_Poros", TexGen.SkinPores, 512, 1.5f, out var porosN);

            Texture2D Ph(string n, TexKind k) => Tex($"{PhDir}/{n}", k);
            var brA = Ph("bronze_albedo.jpg", TexKind.Color);
            var brN = Ph("bronze_normal.jpg", TexKind.Normal);
            var brM = Ph("bronze_ms.png", TexKind.Linear);
            var cuA = Ph("couro_albedo.jpg", TexKind.Color);
            var cuN = Ph("couro_normal.jpg", TexKind.Normal);
            var cuM = Ph("couro_ms.png", TexKind.Linear);
            var liA = Ph("linho_albedo.jpg", TexKind.Color);
            var liN = Ph("linho_normal.jpg", TexKind.Normal);
            var liM = Ph("linho_ms.png", TexKind.Linear);
            var laA = Ph("la_albedo.jpg", TexKind.Color);
            var laN = Ph("la_normal.jpg", TexKind.Normal);
            var laM = Ph("la_ms.png", TexKind.Linear);
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>(TroiaAssets.TexDir + "/T_Madeira.png");

            var tmp = new GameObject("tmp");
            var d = tmp.AddComponent<PatrocloDesign>();
            _pal = new Material[(int)M.Count];
            _pal[(int)M.PBronze] = Lit("M_Pat_Bronze", brA, brN, brM, d.bronze, 1f, bump: 1f);
            _pal[(int)M.PBronzeDark] = Lit("M_Pat_BronzeEscuro", brA, brN, brM, d.bronzeEscuro, 0.9f, bump: 1f);
            _pal[(int)M.PGold] = Lit("M_Pat_Ouro", brA, brN, brM, d.ouro, 1f, emissive: true, bump: 0.5f);
            _pal[(int)M.PBlade] = Lit("M_Pat_Lamina", brA, brN, brM, d.lamina, 1f, emissive: true, bump: 0.4f);
            _pal[(int)M.PCrimson] = Lit("M_Pat_Crista", crina, crinaN, null, d.carmesim, 0.32f, emissive: true);
            _pal[(int)M.PCrimsonDark] = Lit("M_Pat_Capa", laA, laN, laM, d.carmesimEscuro, 1f);
            _pal[(int)M.PLinen] = Lit("M_Pat_Linho", liA, liN, liM, d.linho, 1f);
            _pal[(int)M.PLeather] = Lit("M_Pat_Couro", cuA, cuN, cuM, d.couro, 1f);
            _pal[(int)M.PEmblem] = Lit("M_Pat_Emblema", brA, brN, null, d.emblema, 0.45f, 0.2f, true, 0.5f);
            _pal[(int)M.PWood] = Lit("M_Pat_Madeira", wood, null, null, new Color(0.62f, 0.5f, 0.42f), 0.2f);
            _pal[(int)M.PMark] = Lit("M_Pat_Marca", null, null, null, new Color(0.08f, 0.08f, 0.08f), 0.6f, 0f, true);
            // fundo escuro dos cortes de lâmina no linho
            _pal[(int)M.PCut] = Lit("M_Pat_Corte", liA, liN, null, new Color(0.075f, 0.05f, 0.04f), 0.05f);

            // corpo do MakeHuman
            // pele: difusa com barba por fazer e cavidade + normal map com musculatura, veias e rugas (mh_skin.py)
            _skin = Lit("M_Pat_Pele", Tex($"{MhTexDir}/pele_albedo.png", TexKind.Color), Tex($"{MhTexDir}/pele_normal.png", TexKind.Normal),
                Tex($"{MhTexDir}/pele_ms.png", TexKind.Linear, 2048), d.pele, 1f, bump: 1f, character: false);
            // shader de pele com espalhamento subsuperficial (Troia/Pele): tira o aspecto de cera/massinha
            var skinShader = Shader.Find("Troia/Pele");
            if (skinShader != null)
            {
                _skin.shader = skinShader;
                // cor do espalhamento em RGB e força no alfa (x 2)
                _skin.SetColor("_SpecColor", new Color(0.9f, 0.4f, 0.3f, 0.375f));
                _skin.SetFloat("_ClearCoatMask", 0.35f);
                _skin.SetFloat("_ClearCoatSmoothness", 0.55f);
                _skin.SetTexture("_GrimeMap", _grime);
                _skin.SetFloat("_OcclusionStrength", 0f);
                _skin.SetFloat("_Parallax", 0f);
            }
            _skin.SetTexture("_DetailNormalMap", porosN);
            _skin.SetFloat("_DetailNormalMapScale", 0.3f);
            _skin.SetTextureScale("_DetailAlbedoMap", new Vector2(70f, 70f));
            BaseShaderGUI.SetMaterialKeywords(_skin, LitGUI.SetMaterialKeywords, null);
            EditorUtility.SetDirty(_skin);
            _eyes = Lit("M_Pat_Olhos", Tex($"{MhTexDir}/olho_castanho.png", TexKind.ColorAlpha), null, null, Color.white, 0.85f, alphaClip: true, cutoff: 0.35f, character: false);
            _brows = Lit("M_Pat_Sobrancelhas", Tex($"{MhTexDir}/sobrancelha.png", TexKind.ColorAlpha), null, null, new Color(1f, 1f, 1f), 0.25f, twoSided: true, transparent: true);
            _lashes = Lit("M_Pat_Cilios", Tex($"{MhTexDir}/cilios.png", TexKind.ColorAlpha), null, null, new Color(1f, 1f, 1f), 0.25f, twoSided: true, transparent: true);
            _hair = Lit("M_Pat_Cabelo", Tex($"{MhTexDir}/cabelo_diffuse.png", TexKind.ColorAlpha), null, null,
                d.cabelo, 0.24f, alphaClip: true, cutoff: 0.3f, twoSided: true, character: false);
            _pal[(int)M.PSkin] = _skin;
            _pal[(int)M.PHair] = _hair;
            Object.DestroyImmediate(tmp);
        }

        // =====================================================================================
        // Personagem
        // =====================================================================================
        static MhCharacter _ch;
        static BodyInfo _body;
        static Texture2D _grime;

        static GameObject Skinned(string name, SkinBuilder sb, Transform parent, bool shadows = true)
        {
            var mesh = sb.ToMesh("Pat_" + name, _ch.bind, _pal, out var mats);
            var bw = mesh.boneWeights;
            var bp = mesh.bindposes;
            mesh = TroiaAssets.SaveMesh(mesh, $"{MeshDir}/Pat_{name}.asset");
            mesh.boneWeights = bw;
            mesh.bindposes = bp;
            EditorUtility.SetDirty(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = _ch.bones;
            smr.rootBone = _ch.rootBone;
            smr.sharedMaterials = mats;
            smr.localBounds = new Bounds(Vector3.zero, new Vector3(2.6f, 2.8f, 2.6f));
            smr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static GameObject Rigid(string name, Transform parent, Action<MeshBuilder> build, bool shadows = true)
        {
            var b = new MeshBuilder();
            build(b);
            var mesh = TroiaAssets.SaveMesh(b.ToMesh("Pat_" + name, _pal, out var mats), $"{MeshDir}/Pat_{name}.asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static Renderer Rend(GameObject go) => go.GetComponent<Renderer>();

        /// <summary>Malhas de um objeto (e dos filhos, se pedido) para o assador de oclusão; marcas de bênção ficam de fora.</summary>
        static void AoItems(List<PatrocloAO.Item> list, GameObject go, bool children, float crevice = 0.7f,
            Func<Vector3, float> dirtBoost = null, Func<Vector3, float> stainBoost = null)
        {
            if (go == null) return;
            foreach (var smr in children ? go.GetComponentsInChildren<SkinnedMeshRenderer>(true) : go.GetComponents<SkinnedMeshRenderer>())
                if (!smr.name.StartsWith("Marca_") && smr.sharedMesh != null)
                    list.Add(new PatrocloAO.Item(smr.sharedMesh, smr.transform.localToWorldMatrix, crevice, dirtBoost, stainBoost));
            foreach (var mf in children ? go.GetComponentsInChildren<MeshFilter>(true) : go.GetComponents<MeshFilter>())
                if (!mf.name.StartsWith("Marca_") && mf.sharedMesh != null)
                    list.Add(new PatrocloAO.Item(mf.sharedMesh, mf.transform.localToWorldMatrix, crevice, dirtBoost, stainBoost));
        }

        static Transform Group(string name, Transform parent)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            return g;
        }

        static GameObject Shield(string name, Transform bone, Vector3 worldPos, Quaternion worldRot, List<GameObject> atena, List<Renderer> emblems, List<Renderer> marks)
        {
            var anchor = new GameObject(name);
            anchor.transform.SetParent(bone, false);
            anchor.transform.SetPositionAndRotation(worldPos, worldRot);
            Rigid("Aspis", anchor.transform, P.Escudo);
            emblems.Add(Rend(Rigid("Emblema_Formiga", anchor.transform, P.Emblema)));
            var mark = Rigid("Marca_Atena", anchor.transform, P.MarcaAtena, false);
            marks.Add(Rend(mark));
            atena.Add(mark);
            return anchor;
        }

        static GameObject BuildCharacter()
        {
            var root = new GameObject("Patroclo");
            var design = root.AddComponent<PatrocloDesign>();
            var refs = design.refs;
            var marks = new List<Renderer>();
            var emblems = new List<Renderer>();
            var atena = new List<GameObject>();

            // ---------------- corpo (MakeHuman): esqueleto + pele, olhos, sobrancelhas, cílios, cabelo
            var data = MakeHumanImporter.Load(JsonPath);
            _ch = MakeHumanImporter.Build(data, root.transform, k =>
                k == "pele" ? _skin : k == "olhos" ? _eyes : k == "sobrancelha" ? _brows : k == "cilios" ? _lashes : _hair, MeshDir, "MH_");
            _body = new BodyInfo(data);
            var fit = new F(_body, new BodyInfo(data, "Corpo"));
            var B = _body;
            int head = B.B("head");

            var arm = Group("Armadura", root.transform);
            var cloth = Group("Roupa", root.transform);

            // ---------------- corselete de linho (linotórax) dos Mirmidões
            var cuir = fit.Cuirass();
            var loops = fit.CorseletLoops(cuir); // [0] barra de baixo, [1] gola, [2..] cavas
            // o corselete tem de ser uma peça inteira: só barra de baixo, gola e duas cavas
            int cuirLoops = F.LoopPoints(cuir).Length;
            if (cuirLoops != 4) Debug.LogWarning($"[Troia] corselete com {cuirLoops} contornos (esperado: 4); pode haver buracos na casca");
            var torsoUv = F.TorsoUv(true);
            var armLoops = new Vector3[Mathf.Max(0, loops.Length - 2)][];
            for (int k = 2; k < loops.Length; k++) armLoops[k - 2] = loops[k];
            var dBottom = F.DistToLoops(loops[0]);
            var dNeck = F.DistToLoops(loops[1]);
            var dArms = F.DistToLoops(armLoops);
            // linho no meio e lã carmesim nas barras: partes complementares da mesma casca (nada sobreposto)
            F.Split(cuir, q => Mathf.Min(dBottom(q) - 0.046f, Mathf.Min(dNeck(q) - 0.024f, dArms(q) - 0.02f)), out var linen, out var trim);
            var sb = new SkinBuilder();
            sb.AddSolid(linen, M.PLinen, M.PLinen, torsoUv, 0.006f);
            sb.AddSolid(trim, M.PCrimsonDark, M.PLinen, torsoUv, 0.006f);
            sb.AddRims(cuir, 0.006f, 0.0055f, M.PCrimsonDark);
            // amarração nas laterais: correia de couro com fivela de ouro
            foreach (string side in F.Sides)
                foreach (float y in new[] { 1.3f, 1.36f })
                {
                    float sx = F.Sgn(side), yy = y;
                    int i = cuir.Closest(Vector3.zero, (q, nn) => Mathf.Abs(q.y - yy) * 3f + Mathf.Abs(q.z - 0.0f) * 2f - q.x * sx);
                    Vector3 pos = cuir.p[i], nrm = cuir.n[i];
                    var w = cuir.w[i];
                    var mb = new MeshBuilder();
                    Vector3 tg = Vector3.Cross(Vector3.up, nrm).normalized;
                    mb.BoxBetween(pos + nrm * 0.0025f - tg * 0.03f, pos + nrm * 0.0025f + tg * 0.03f, 0.016f, 0.004f, M.PLeather);
                    mb.Push(pos + nrm * 0.0055f + tg * 0.012f, Quaternion.LookRotation(nrm, Vector3.up));
                    mb.Box(Vector3.zero, new Vector3(0.016f, 0.021f, 0.003f), M.PGold);
                    mb.Pop();
                    sb.AddBuilder(mb, _ => w);
                }
            // marcas de combate: três cortes de lâmina no linho (alto do peito, costelas direitas e costas)
            fit.Slash(sb, cuir, -164f, 1.478f, 172f, 1.428f, 0.011f);
            fit.Slash(sb, cuir, 130f, 1.358f, 104f, 1.284f, 0.011f);
            fit.Slash(sb, cuir, 22f, 1.47f, -8f, 1.40f, 0.012f);
            Skinned("Corselete", sb, arm);

            // gravações em ouro (acendem com o favor divino):
            // 1: vivo e meandro da barra de baixo; 2: cavas e epômides; 3: gola, disco do peito e elmo
            var g1 = new SkinBuilder();
            var g2 = new SkinBuilder();
            var g3 = new SkinBuilder();
            // vivo de ouro (tubo meio embutido) em toda costura entre o linho e as barras
            void Piping(Shell part, float radius, Func<Vector3, SkinBuilder> target)
            {
                foreach (var lp in part.Loops())
                {
                    int n = lp.Length;
                    if (n < 8) continue;
                    var pts = new Vector3[n];
                    var nrms = new Vector3[n];
                    for (int i = 0; i < n; i++) { pts[i] = part.p[lp[i]]; nrms[i] = part.n[lp[i]]; }
                    SkinBuilder.SmoothLoop(ref pts, ref nrms);
                    var ws = new BoneWeight[n];
                    for (int i = 0; i < n; i++) ws[i] = part.w[lp[i]];
                    SkinBuilder.ResampleLoop(pts, nrms, ws, Mathf.Max(radius * 1.25f, 0.0035f), out var rp, out var rn, out var rw);
                    target(rp[rp.Length / 2]).AddTube(rp, rn, i => rw[i], 0f, radius, M.PGold, 6, true);
                }
            }
            Piping(linen, 0.0023f, q =>
            {
                float eb = Mathf.Abs(dBottom(q) - 0.046f), en = Mathf.Abs(dNeck(q) - 0.024f), ea = Mathf.Abs(dArms(q) - 0.02f);
                return eb <= en && eb <= ea ? g1 : en <= ea ? g3 : g2;
            });
            F.Meander(g1, cuir, loops[0], 0.0235f, 0.0105f, 0.042f, 0.0013f, M.PGold, Vector3.up);

            // escamas de bronze em volta da cintura, fechadas em cima por um cinturão com cravos e fivela
            sb = new SkinBuilder();
            fit.Scales(sb, cuir, loops[0], 0.068f, 8, 0.021f);
            Skinned("Escamas", sb, arm);
            var belt = fit.Belt(cuir, loops[0], 0.2f, 0.238f);
            sb = new SkinBuilder();
            if (belt.VertexCount > 0)
            {
                sb.AddSolid(belt, M.PBronze, M.PBronzeDark, torsoUv, 0.004f);
                sb.AddRims(belt, 0.004f, 0.0036f, M.PGold);
                for (int k = 0; k < 24; k++)
                {
                    float th = -180f + 7.5f + k * 15f, a = th * Mathf.Deg2Rad;
                    float y = F.EdgeHeight(loops[0], th) + 0.219f;
                    if (!belt.Raycast(new Vector3(0f, y, 0f), new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)), out var sp, out var sn, out var sw0)) continue;
                    var one = new MeshBuilder();
                    one.Push(sp, Quaternion.LookRotation(sn, Vector3.up));
                    one.Sphere(new Vector3(0f, 0f, 0.0008f), 0.0052f, 0.0034f, 10, M.PGold);
                    one.Pop();
                    var ww = sw0;
                    sb.AddBuilder(one, _ => ww);
                }
                // fivela na frente: placa com moldura de ouro e botão central
                float yb = F.EdgeHeight(loops[0], 180f) + 0.219f;
                if (belt.Raycast(new Vector3(0f, yb, 0f), Vector3.forward, out var bp, out var bn, out var bw))
                {
                    var bk = new MeshBuilder();
                    bk.Push(bp + bn * 0.003f, Quaternion.LookRotation(bn, Vector3.up));
                    bk.Box(Vector3.zero, new Vector3(0.058f, 0.05f, 0.006f), M.PBronzeDark);
                    foreach (float sy in new[] { -1f, 1f })
                    {
                        bk.Box(new Vector3(0f, sy * 0.023f, 0.003f), new Vector3(0.062f, 0.005f, 0.005f), M.PGold);
                        bk.Box(new Vector3(sy * 0.0285f, 0f, 0.003f), new Vector3(0.005f, 0.05f, 0.005f), M.PGold);
                    }
                    bk.Sphere(new Vector3(0f, 0f, 0.004f), 0.013f, 0.006f, 14, M.PGold);
                    bk.Pop();
                    sb.AddBuilder(bk, _ => bw);
                }
            }
            Skinned("Cinturao", sb, arm);

            // disco de bronze no peito (kardiophylax) com a formiga dos Mirmidões em ouro.
            // Assenta no plano médio de uma área de ~11 cm do peito e fica à frente do ponto mais saliente dela.
            var discHits = new List<Vector3>();
            Vector3 dn = Vector3.zero;
            BoneWeight dw = default;
            for (int k = -1; k < 12; k++)
            {
                Vector3 o = k < 0 ? new Vector3(0f, 1.34f, 0f)
                    : new Vector3(Mathf.Cos(k * Mathf.PI / 6f) * 0.056f, 1.34f + Mathf.Sin(k * Mathf.PI / 6f) * 0.056f, 0f);
                if (!cuir.Raycast(o, Vector3.forward, out var hp, out var hn, out var hw)) continue;
                if (k < 0) dw = hw;
                discHits.Add(hp);
                dn += hn;
            }
            dn.Normalize();
            Vector3 dpos = Vector3.zero;
            foreach (var hp in discHits) dpos += hp / discHits.Count;
            float discLift = 0f;
            foreach (var hp in discHits) discLift = Mathf.Max(discLift, Vector3.Dot(hp - dpos, dn));
            dpos += dn * (discLift + 0.0045f);
            Vector3 dup = (Vector3.up - dn * dn.y).normalized;
            var discRot = Quaternion.LookRotation(dn, dup);
            var disc = new MeshBuilder();
            disc.Push(dpos, discRot);
            disc.Lathe(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new[]
            {
                new Vector2(0.07f, -0.0045f), new Vector2(0.072f, -0.002f), new Vector2(0.071f, 0.001f), new Vector2(0.064f, 0.0036f),
                new Vector2(0.052f, 0.006f), new Vector2(0.038f, 0.0078f), new Vector2(0.022f, 0.009f), new Vector2(0.008f, 0.0096f), new Vector2(0f, 0.0098f),
            }, 56, M.PBronze, false, false);
            disc.Pop();
            sb = new SkinBuilder();
            sb.AddBuilder(disc, _ => dw);
            Skinned("Disco_Peito", sb, arm);
            var discGold = new MeshBuilder();
            discGold.Push(dpos, discRot);
            discGold.Torus(new Vector3(0f, 0f, 0.0012f), Quaternion.Euler(90f, 0f, 0f), 0.0705f, 0.0038f, 56, 8, M.PGold);
            discGold.Torus(new Vector3(0f, 0f, 0.0052f), Quaternion.Euler(90f, 0f, 0f), 0.057f, 0.0016f, 56, 6, M.PGold);
            P.Formiga(discGold, 0.22f, 0.0088f, M.PGold);
            discGold.Pop();
            g3.AddBuilder(discGold, _ => dw);

            // ---------------- quíton de linho, ptéruges e capa
            sb = new SkinBuilder();
            fit.Skirt(sb, fit.CuirassEdges(cuir)[0]);
            Skinned("Quiton", sb, cloth);

            sb = new SkinBuilder();
            fit.Pteruges(sb, fit.CuirassEdges(cuir)[0]);
            Skinned("Pteruges", sb, cloth);
            sb = new SkinBuilder();
            fit.Sash(sb, fit.CuirassEdges(cuir)[0]);
            Skinned("Faixa", sb, cloth);

            // ---------------- adaga de cintura no quadril direito, pendurada da barra do corselete
            // (do lado oposto ao da faixa; fica presa ao osso do quadril e por fora das tiras de couro)
            var dagger = new GameObject("Adaga");
            {
                const float thD = 97f, tilt = 8f;
                float aD = thD * Mathf.Deg2Rad, aTip = (thD - tilt) * Mathf.Deg2Rad;
                var dirD = new Vector3(Mathf.Sin(aD), 0f, -Mathf.Cos(aD));
                float yTopD = F.EdgeHeight(loops[0], thD) + 0.03f, yTipD = yTopD - 0.27f, yHilt = yTopD + 0.12f;
                float RadiusAt(float y) => cuir.Raycast(new Vector3(0f, y, 0f), dirD, out var hit, out _, out _) ? new Vector2(hit.x, hit.z).magnitude : 0.2f;
                float rTip = fit.SkirtRadius(thD - tilt, yTipD) + 0.03f;
                float rTop = RadiusAt(yTopD) + 0.021f;
                // o cabo não pode encostar nas escamas da cintura: se precisar, afasta a boca da bainha
                float rHiltMin = RadiusAt(yHilt) + 0.032f;
                rTop = Mathf.Max(rTop, rTip + (rHiltMin - rTip) * (yTopD - yTipD) / (yHilt - yTipD));
                Vector3 pTop = dirD * rTop + Vector3.up * yTopD;
                Vector3 pTip = new Vector3(Mathf.Sin(aTip), 0f, -Mathf.Cos(aTip)) * rTip + Vector3.up * yTipD;
                Vector3 fwd = (pTip - pTop).normalized;
                Vector3 outN = (dirD - Vector3.Dot(dirD, fwd) * fwd).normalized;
                dagger.transform.SetParent(_ch.byName["root"], false);
                dagger.transform.SetPositionAndRotation(pTop, Quaternion.LookRotation(fwd, Vector3.Cross(fwd, outN)));
                Rigid("Adaga_Bainha", dagger.transform, P.Adaga);
                // correia de couro que prende a boca da bainha na barra do corselete, com um cravo de ouro
                Vector3 anchor = dirD * (RadiusAt(yTopD + 0.05f) + 0.004f) + Vector3.up * (yTopD + 0.05f);
                Vector3 aL = new Vector3(-0.0078f, 0f, 0.014f), bL = dagger.transform.InverseTransformPoint(anchor);
                Rigid("Adaga_Correia", dagger.transform, mb =>
                {
                    Vector3 d = bL - aL;
                    mb.Box((aL + bL) * 0.5f, new Vector3(0.0035f, 0.024f, d.magnitude + 0.012f), M.PLeather, Quaternion.LookRotation(d.normalized, Vector3.up));
                    mb.Sphere(bL + new Vector3(0.002f, 0f, 0f), 0.0062f, 0.0062f, 8, M.PGold);
                });
            }

            sb = new SkinBuilder();
            fit.Cape(sb, cuir, out _);
            refs.capa = Skinned("Capa", sb, cloth);

            // ---------------- epômides (abas de ombro do linotórax) e proteção de bronze no braço do escudo
            sb = new SkinBuilder();
            var ares = new MeshBuilder();
            var aresW = new List<(Vector3, BoneWeight)>();
            foreach (string side in F.Sides)
            {
                float sx = F.Sgn(side);
                var flap = fit.ShoulderFlap(cuir, side);
                if (flap.VertexCount == 0) continue;
                // miolo de linho e barra carmesim complementares, com vivo de ouro na costura
                var dFlap = F.DistToLoops(F.LoopPoints(flap));
                F.Split(flap, q => dFlap(q) - 0.016f, out var flapLinen, out var flapTrim);
                sb.AddSolid(flapLinen, M.PLinen, M.PLinen, torsoUv, 0.005f);
                sb.AddSolid(flapTrim, M.PCrimsonDark, M.PLinen, torsoUv, 0.005f);
                sb.AddRims(flap, 0.005f, 0.0045f, M.PCrimsonDark);
                Piping(flapLinen, 0.002f, _ => g2);
                // ponta da frente amarrada no peito: argola de ouro, cordão de couro e botão no corselete
                int fi = flap.Closest(new Vector3(sx * 0.156f, 1.36f, 0.3f));
                Vector3 tip = flap.p[fi], tn = flap.n[fi];
                var tw = flap.w[fi];
                var tie = new MeshBuilder();
                Vector3 ringC = tip + tn * 0.002f - Vector3.up * 0.013f;
                tie.Torus(ringC, Quaternion.LookRotation(tn, Vector3.up) * Quaternion.Euler(90f, 0f, 0f), 0.0105f, 0.0026f, 18, 6, M.PGold);
                Vector3 hdir = new Vector3(tip.x, 0f, tip.z).normalized;
                if (cuir.Raycast(new Vector3(0f, tip.y - 0.06f, 0f), hdir, out var knobP, out var knobN, out _))
                {
                    tie.CylinderBetween(ringC - Vector3.up * 0.0105f, knobP + knobN * 0.004f, 0.0026f, 0.0026f, 8, M.PLeather);
                    tie.Sphere(knobP + knobN * 0.003f, 0.0068f, 0.0045f, 12, M.PGold);
                }
                sb.AddBuilder(tie, _ => tw);
                // marca de Ares: espigões no alto da epômide
                for (int k = 0; k < 3; k++)
                {
                    int i = flap.Closest(new Vector3(sx * (0.125f + k * 0.03f), 1.6f, 0.0f));
                    Vector3 pos = flap.p[i], nrm = flap.n[i];
                    ares.Lathe(pos - nrm * 0.002f, Quaternion.FromToRotation(Vector3.up, (nrm + Vector3.up * 0.4f).normalized),
                        new[] { new Vector2(0.011f, 0f), new Vector2(0.007f, 0.02f), new Vector2(0f, 0.05f - k * 0.008f) }, 8, M.PMark, false, false);
                    aresW.Add((pos, flap.w[i]));
                }
                if (side == "L")
                {
                    // medalhão de ouro na epômide do lado do escudo
                    int mi = flap.Closest(new Vector3(sx * 0.156f, 1.46f, 0.3f));
                    var medal = new MeshBuilder();
                    F.Medal(medal, flap.p[mi] + flap.n[mi] * 0.003f, flap.n[mi]);
                    var mw = flap.w[mi];
                    g2.AddBuilder(medal, _ => mw);
                    // lâminas de bronze no braço do escudo (assimetria)
                    foreach (var (from, to, off, bulge) in new[] { (0.112f, 0.17f, 0.027f, 0.006f), (0.06f, 0.125f, 0.034f, 0.009f) })
                    {
                        var lame = fit.Pauldron(side, from, to, off, bulge);
                        sb.AddSolid(lame, M.PBronze, M.PBronzeDark, fit.ArmUv(side), 0.0035f);
                        sb.AddRims(lame, 0.0035f, 0.0045f, M.PGold);
                    }
                }
            }
            refs.ombreiras_r = new[] { Rend(Skinned("Epomides", sb, arm)) };
            refs.gravacoes1 = new[] { Rend(Skinned("Gravacoes_1_Barra", g1, arm)) };
            refs.gravacoes2 = new[] { Rend(Skinned("Gravacoes_2_Ombros", g2, arm)) };
            var g3list = new List<Renderer> { Rend(Skinned("Gravacoes_3_Gola", g3, arm)) };
            var aresSb = new SkinBuilder();
            aresSb.AddBuilder(ares, v =>
            {
                float best = float.MaxValue;
                BoneWeight bw2 = default;
                foreach (var (pp, ww) in aresW)
                {
                    float dd = (pp - v).sqrMagnitude;
                    if (dd < best) { best = dd; bw2 = ww; }
                }
                return bw2;
            });
            var aresGo = Skinned("Marca_Ares_Chifres", aresSb, arm, false);
            marks.Add(Rend(aresGo));
            refs.marcasAres = new[] { aresGo };

            sb = new SkinBuilder();
            foreach (string side in F.Sides)
            {
                var s = fit.Bracer(side, out var uv, out var elbow, out var axis, out var dorsal);
                sb.AddSolid(s, M.PLeather, M.PLeather, uv, 0.003f);
                sb.AddRims(s, 0.003f, 0.0026f, M.PLeather);
                Vector3 ax = axis.normalized;
                Vector3 el = elbow, ds = dorsal;
                var plate = s.Clone().Clip((q, w) =>
                {
                    Vector3 r = q - el;
                    r -= Vector3.Dot(r, ax) * ax;
                    return Vector3.Dot(r.normalized, ds) - 0.2f;
                });
                plate.KeepLargest();
                plate.Smooth(4);
                plate.Offset(0.0045f);
                sb.AddSolid(plate, M.PBronze, M.PBronzeDark, uv, 0.0028f);
                sb.AddRims(plate, 0.0028f, 0.0032f, M.PGold);
                foreach (float t in new[] { 0.5f, 0.68f, 0.86f })
                    foreach (float ang in new[] { -45f, 45f })
                    {
                        Vector3 dir = Quaternion.AngleAxis(ang, ax) * dorsal;
                        int i = plate.Closest(elbow + axis * t + dir * 0.12f);
                        var mb = new MeshBuilder();
                        mb.Sphere(plate.p[i] + plate.n[i] * 0.0008f, 0.0045f, 0.0035f, 8, M.PGold);
                        var w = plate.w[i];
                        sb.AddBuilder(mb, _ => w);
                    }
                // amarração em ziguezague fechando a fenda do lado da palma, com ilhoses de ouro
                Vector3 prevLace = Vector3.zero;
                for (int k = 0; k <= 7; k++)
                {
                    float t = 0.445f + k * 0.07f;
                    Vector3 dir = Quaternion.AngleAxis((k % 2 == 0 ? 1f : -1f) * 27f, ax) * -dorsal;
                    int i = s.Closest(elbow + axis * t + dir * 0.12f);
                    Vector3 q = s.p[i] + s.n[i] * 0.0022f;
                    var lace = new MeshBuilder();
                    lace.Sphere(q, 0.0032f, 0.0022f, 8, M.PGold);
                    if (k > 0) lace.CylinderBetween(prevLace, q, 0.0015f, 0.0015f, 6, M.PLeather);
                    var w = s.w[i];
                    sb.AddBuilder(lace, _ => w);
                    prevLace = q;
                }
            }
            Skinned("Bracadeiras", sb, arm);

            sb = new SkinBuilder();
            var hermes = new MeshBuilder();
            foreach (string side in F.Sides)
            {
                var s = fit.Greave(side, out var uv);
                sb.AddSolid(s, M.PBronze, M.PBronzeDark, uv, 0.0035f);
                sb.AddRims(s, 0.0035f, 0.0045f, M.PGold);
                var knee = fit.KneeCap(side);
                if (knee.VertexCount > 0)
                {
                    sb.AddSolid(knee, M.PBronze, M.PBronzeDark, uv, 0.0035f);
                    sb.AddRims(knee, 0.0035f, 0.0045f, M.PGold);
                }
                var legStraps = fit.GreaveStraps(side, out var bkP, out var bkN, out var bkW);
                if (legStraps.VertexCount > 0)
                {
                    sb.AddSolid(legStraps, M.PLeather, M.PLeather, uv, 0.0028f);
                    sb.AddRims(legStraps, 0.0028f, 0.0017f, M.PLeather, 5, 0.01f);
                    for (int k = 0; k < bkP.Length; k++)
                    {
                        var bk = new MeshBuilder();
                        bk.Push(bkP[k] + bkN[k] * 0.003f, Quaternion.LookRotation(bkN[k], Vector3.up));
                        bk.Box(Vector3.zero, new Vector3(0.02f, 0.024f, 0.0035f), M.PGold);
                        bk.Box(new Vector3(0f, 0f, 0.0012f), new Vector3(0.011f, 0.015f, 0.0035f), M.PBronzeDark);
                        bk.Pop();
                        var w = bkW[k];
                        sb.AddBuilder(bk, _ => w);
                    }
                }
                // marca de Hermes: asinhas no tornozelo, por fora
                float sx = F.Sgn(side);
                Vector3 ank = B.Head("foot." + side) + new Vector3(sx * 0.046f, 0.03f, -0.012f);
                for (int f = 0; f < 3; f++)
                    hermes.BoxBetween(ank + Vector3.up * (f * 0.008f), ank + new Vector3(sx * (0.014f + f * 0.004f), 0.03f + f * 0.016f, -0.055f + f * 0.006f), 0.016f - f * 0.003f, 0.004f, M.PMark);
            }
            Skinned("Cnemides", sb, arm);
            var hermesSb = new SkinBuilder();
            var footSet = B.Set("foot.*", "lowerleg02.*");
            hermesSb.AddBuilder(hermes, v => B.NearestWeights(v, i => footSet.Contains(B.dom[i])));
            var hermesGo = Skinned("Marca_Hermes_Asas", hermesSb, arm, false);
            marks.Add(Rend(hermesGo));
            refs.marcasHermes = new[] { hermesGo };

            sb = new SkinBuilder();
            foreach (string side in F.Sides)
            {
                fit.FootFrame(side, out var heel, out var toe, out var fdir, out _);
                var sole = fit.Sole(side);
                var plane = UvProj.Plane(heel, Vector3.Cross(Vector3.up, fdir), fdir);
                sb.AddSolid(sole, M.PLeather, M.PLeather, plane, F.SoleThickness);
                sb.AddRims(sole, F.SoleThickness, F.SoleThickness * 0.55f, M.PLeather, 8);
                var straps = fit.Straps(side);
                if (straps.VertexCount > 0)
                {
                    var uv = UvProj.Cylinder(B.Head("foot." + side), Vector3.up, fdir, 0.04f);
                    sb.AddSolid(straps, M.PLeather, M.PLeather, uv, 0.0025f);
                    sb.AddRims(straps, 0.0025f, 0.0014f, M.PLeather, 5, 0.01f);
                }
            }
            Skinned("Sandalias", sb, arm);

            // ---------------- elmo coríntio, crista, rabo de crina e louros de Apolo
            var helmGroup = Group("Elmo", arm);
            refs.elmo = helmGroup.gameObject;
            var h = fit.MeasureHead();
            var helm = fit.Helmet(h);
            sb = new SkinBuilder();
            sb.AddSolid(helm, M.PBronze, M.PBronzeDark, F.HelmetUv(h), 0.0035f);
            sb.AddRims(helm, 0.0014f, 0.0034f, M.PGold);
            var volutes = new MeshBuilder();
            F.TempleVolutes(volutes, h);
            sb.AddBuilder(volutes, _ => BodyInfo.Rigid(head));
            Skinned("Elmo_Casco", sb, helmGroup);
            var hb = fit.HelmetBand(h, helm);
            if (hb.VertexCount > 0)
            {
                sb = new SkinBuilder();
                sb.AddSolid(hb, M.PGold, M.PGold, F.HelmetUv(h), 0.0013f);
                g3list.Add(Rend(Skinned("Gravacao_Elmo", sb, helmGroup)));
            }
            refs.gravacoes3 = g3list.ToArray();

            var crestMb = new MeshBuilder();
            fit.Crest(crestMb, h, out var flamePos);
            sb = new SkinBuilder();
            sb.AddBuilder(crestMb, _ => BodyInfo.Rigid(head));
            refs.crista = Skinned("Crista", sb, helmGroup);

            Vector3 tailStart = F.Meridian(h, 156f, 0.03f, out _);
            var spine01 = BodyInfo.Rigid(B.B("spine01"));
            var spine02 = BodyInfo.Rigid(B.B("spine02"));
            // escudo nas costas, virado para trás, com a capa presa por baixo
            const float shieldY = F.ShieldY, shieldZ = F.ShieldZ;
            var pathShield = new List<Vector3> { tailStart };
            var pathBack = new List<Vector3> { tailStart };
            for (int k = 1; k <= 12; k++)
            {
                float t = k / 12f;
                float y = Mathf.Lerp(tailStart.y - 0.05f, 1.33f, t);
                float r = Mathf.Abs(y - shieldY);
                float zs = shieldZ - P.EscudoFrenteZ(r) - 0.016f;
                float zOver = Mathf.Lerp(tailStart.z - 0.03f, zs, F.Smooth01(0f, 0.35f, t));
                pathShield.Add(new Vector3(0f, y, Mathf.Min(zOver, tailStart.z - 0.02f * t)));
                float yb = Mathf.Lerp(tailStart.y - 0.05f, 1.28f, t);
                pathBack.Add(new Vector3(0f, yb, Mathf.Lerp(tailStart.z - 0.03f, -0.16f, F.Smooth01(0f, 0.5f, t))));
            }
            Func<Vector3, BoneWeight> TailW(BoneWeight lower, float y0) => v => Shell.LerpW(BodyInfo.Rigid(head), lower, F.Smooth01(y0, y0 - 0.22f, v.y));
            var tailMb = new MeshBuilder();
            F.Tail(tailMb, pathShield, 0.024f);
            sb = new SkinBuilder();
            sb.AddBuilder(tailMb, TailW(spine02, tailStart.y - 0.03f));
            refs.raboEscudo = Skinned("Rabo_de_Crina_Sobre_Escudo", sb, refs.crista.transform);
            tailMb = new MeshBuilder();
            F.Tail(tailMb, pathBack, 0.024f);
            sb = new SkinBuilder();
            sb.AddBuilder(tailMb, TailW(spine01, tailStart.y - 0.03f));
            refs.raboCostas = Skinned("Rabo_de_Crina_Costas", sb, refs.crista.transform);
            refs.crista_r = new[] { Rend(refs.crista), Rend(refs.raboEscudo), Rend(refs.raboCostas) };

            var laurel = new MeshBuilder();
            for (int k = 0; k < 30; k++)
            {
                float lon = -150f + k * (300f / 29f);
                float lat = 38f + (k % 2) * 3.5f;
                Vector3 pa = h.c + F.HelmetShape.Dir(lat, lon) * (F.HelmetRadius(h, lat, lon) + 0.004f);
                Vector3 pb = h.c + F.HelmetShape.Dir(lat + 2.5f, lon + 7f) * (F.HelmetRadius(h, lat + 2.5f, lon + 7f) + 0.006f);
                laurel.BoxBetween(pa, pb, 0.011f, 0.003f, M.PMark);
            }
            sb = new SkinBuilder();
            sb.AddBuilder(laurel, _ => BodyInfo.Rigid(head));
            var apolo = Skinned("Marca_Apolo_Louros", sb, helmGroup, false);
            marks.Add(Rend(apolo));
            refs.marcasApolo = new[] { apolo };

            // chama da crista (nota SSS) reaproveita o fogo do mapa
            TroiaAssets.FireMat = AssetDatabase.LoadAssetAtPath<Material>(TroiaAssets.MatDir + "/M_Fogo.mat");
            TroiaAssets.SmokeMat = AssetDatabase.LoadAssetAtPath<Material>(TroiaAssets.MatDir + "/M_Fumaca.mat");
            TroiaAssets.EmberMat = AssetDatabase.LoadAssetAtPath<Material>(TroiaAssets.MatDir + "/M_Brasa.mat");
            if (TroiaAssets.FireMat != null)
            {
                var flame = TroiaAssets.CreateFireObject("Chama_da_Crista (SSS)", FireSize.Tocha, true, false, true);
                flame.transform.SetParent(_ch.byName["head"], false);
                flame.transform.position = flamePos + Vector3.up * 0.01f;
                flame.transform.localScale = Vector3.one * 0.7f;
                refs.chamaCrista = flame;
            }

            // ---------------- espada grande na mão direita
            var hr = fit.HandFrame("R");
            Vector3 grip = (B.Head("finger2-1.R") + B.Head("finger5-1.R")) * 0.5f + hr.palm * 0.026f - hr.fingers * 0.006f;
            var sword = new GameObject("Espada");
            sword.transform.SetParent(_ch.byName["wrist.R"], false);
            sword.transform.SetPositionAndRotation(grip, Quaternion.LookRotation(hr.thumbSide, hr.fingers));
            Rigid("Punho", sword.transform, P.Punho);
            refs.lamina_r = new[] { Rend(Rigid("Lamina", sword.transform, P.Lamina)) };
            var zeus = Rigid("Marca_Zeus_Raios", sword.transform, P.MarcaZeus, false);
            var poseidon = Rigid("Marca_Poseidon_Agua", sword.transform, P.MarcaPoseidon, false);
            marks.Add(Rend(zeus));
            marks.Add(Rend(poseidon));
            refs.marcasZeus = new[] { zeus };
            refs.marcasPoseidon = new[] { poseidon };

            // ---------------- escudo: nas costas (sobre a capa) ou no antebraço esquerdo
            refs.escudoCostas = Shield("Escudo_Costas", _ch.byName["spine02"], new Vector3(0f, shieldY, shieldZ), Quaternion.Euler(0f, 180f, 0f), atena, emblems, marks);
            var hl = fit.HandFrame("L");
            Vector3 E = B.Head("lowerarm01.L");
            Vector3 fd = (B.Head("wrist.L") - E).normalized;
            Vector3 dors = -(hl.palm - Vector3.Dot(hl.palm, fd) * fd).normalized;
            refs.escudoBraco = Shield("Escudo_Braco", _ch.byName["lowerarm02.L"], E + fd * 0.07f + dors * 0.078f,
                Quaternion.LookRotation(dors, Vector3.Cross(dors, fd)), atena, emblems, marks);
            refs.emblema_r = emblems.ToArray();
            refs.marcasAtena = atena.ToArray();
            refs.marcas_r = marks.ToArray();

            // ---------------- corpo "vestido": sem os polígonos que ficam sempre escondidos sob a roupa
            // (com a armadura ligada o painel mostra esta malha; assim a pele nunca vaza pelo tecido)
            var bodySmr = _ch.renderers["Corpo"];
            var fullMesh = bodySmr.sharedMesh;
            var hiddenV = new bool[_ch.bodyVerts.Length];
            int hiddenCount = 0;
            for (int i = 0; i < hiddenV.Length; i++)
            {
                hiddenV[i] = fit.Hidden(_ch.bodyVerts[i], B.B(_ch.bodyDominant[i]));
                if (hiddenV[i]) hiddenCount++;
            }
            var srcTris = fullMesh.triangles;
            var keepTris = new List<int>(srcTris.Length);
            for (int i = 0; i < srcTris.Length; i += 3)
                if (!(hiddenV[srcTris[i]] && hiddenV[srcTris[i + 1]] && hiddenV[srcTris[i + 2]]))
                {
                    keepTris.Add(srcTris[i]); keepTris.Add(srcTris[i + 1]); keepTris.Add(srcTris[i + 2]);
                }
            var dressedMesh = Object.Instantiate(fullMesh);
            dressedMesh.name = "MH_Corpo_Vestido";
            dressedMesh.triangles = keepTris.ToArray();
            AssetDatabase.CreateAsset(dressedMesh, $"{MeshDir}/MH_Corpo_Vestido.asset");
            var dressedGo = new GameObject("Corpo_Vestido");
            dressedGo.transform.SetParent(root.transform, false);
            var dressedSmr = dressedGo.AddComponent<SkinnedMeshRenderer>();
            dressedSmr.sharedMesh = dressedMesh;
            dressedSmr.bones = bodySmr.bones;
            dressedSmr.rootBone = bodySmr.rootBone;
            dressedSmr.sharedMaterial = bodySmr.sharedMaterial;
            dressedSmr.localBounds = bodySmr.localBounds;
            Debug.Log($"[Troia] corpo vestido: {hiddenCount} de {hiddenV.Length} vértices escondidos, {srcTris.Length / 3 - keepTris.Count / 3} triângulos removidos");

            // ---------------- oclusão de ambiente assada nos vértices (sombras de contato entre as camadas)
            // por grupos: elmo, capa, escudo e espada não escurecem o resto, porque o painel pode desligá-los
            var aoCore = new List<PatrocloAO.Item>();
            foreach (Transform c in arm)
                if (c.name != "Elmo" && !c.name.StartsWith("Marca_")) AoItems(aoCore, c.gameObject, false);
            // a barra da saia e da capa arrasta na poeira; na pele a oclusão vira bem menos sujeira que na armadura
            Func<Vector3, float> skirtHem = q => 0.55f * F.Smooth01(0.83f, 0.74f, q.y);
            Func<Vector3, float> capeHem = q => 0.6f * F.Smooth01(0.7f, 0.32f, q.y);
            const float skinCrevice = 0.22f;
            foreach (Transform c in cloth)
                if (c.name != "Capa") AoItems(aoCore, c.gameObject, false, 0.7f, c.name == "Quiton" ? skirtHem : null);
            AoItems(aoCore, dressedGo, false, skinCrevice);
            AoItems(aoCore, dagger, true);
            PatrocloAO.Bake(aoCore, aoCore);
            var aoHelm = new List<PatrocloAO.Item>();
            AoItems(aoHelm, refs.elmo, true);
            var aoOcc = new List<PatrocloAO.Item>(aoCore);
            aoOcc.AddRange(aoHelm);
            PatrocloAO.Bake(aoHelm, aoOcc);
            var aoCape = new List<PatrocloAO.Item>();
            AoItems(aoCape, refs.capa, false, 0.7f, capeHem);
            aoOcc = new List<PatrocloAO.Item>(aoCore);
            aoOcc.AddRange(aoCape);
            PatrocloAO.Bake(aoCape, aoOcc);
            // sangue na lâmina: cresce para a ponta (a espada tem a lâmina em +Z, a partir da guarda)
            Func<Vector3, float> bladeBlood = q => 0.95f * F.Smooth01(0.45f, 1.05f, sword.transform.InverseTransformPoint(q).z);
            foreach (var rigid in new[] { sword, refs.escudoCostas, refs.escudoBraco })
            {
                var own = new List<PatrocloAO.Item>();
                AoItems(own, rigid, true, 0.7f, null, rigid == sword ? bladeBlood : null);
                PatrocloAO.Bake(own, own, 48, 0.16f);
            }
            // sem armadura: corpo inteiro e uma saia própria, com o cós justo no quadril (a da armadura entra
            // recuada por dentro do corselete) e a oclusão assada só com o que continua visível
            sb = new SkinBuilder();
            fit.Skirt(sb, fit.CuirassEdges(cuir)[0], true);
            var looseGo = Skinned("Quiton_Solto", sb, cloth);
            var aoNude = new List<PatrocloAO.Item>();
            AoItems(aoNude, bodySmr.gameObject, false, skinCrevice);
            AoItems(aoNude, looseGo, false, 0.7f, skirtHem);
            aoOcc = new List<PatrocloAO.Item>(aoNude);
            foreach (Transform c in arm) if (c.name == "Sandalias") AoItems(aoOcc, c.gameObject, false);
            PatrocloAO.Bake(aoNude, aoOcc);
            refs.semArmadura = new[] { bodySmr.gameObject, looseGo };

            // ---------------- referências do painel
            // peças que somem quando o painel desliga a armadura (para ver a musculatura)
            var corpoArm = new List<GameObject>();
            foreach (Transform c in arm)
                if (c.name != "Sandalias" && c.name != "Elmo" && !c.name.StartsWith("Marca_")) corpoArm.Add(c.gameObject);
            foreach (Transform c in cloth)
                if (c.name == "Pteruges" || c.name == "Faixa" || c.name == "Quiton") corpoArm.Add(c.gameObject);
            corpoArm.Add(dressedGo);
            corpoArm.Add(dagger);
            refs.armaduraCorpo = corpoArm.ToArray();
            refs.ossos = _ch.bones;
            refs.raiz = _ch.byName["root"];
            refs.posicaoRaiz = refs.raiz.localPosition;
            refs.cabeloSolto = _ch.renderers["Cabelo"].gameObject;
            var dedos = new List<Transform>();
            var eixos = new List<Vector3>();
            var dobras = new List<float>();
            var direita = new List<bool>();
            foreach (string side in F.Sides)
                for (int f = 1; f <= 5; f++)
                    for (int j = 1; j <= 3; j++)
                    {
                        string n = $"finger{f}-{j}.{side}";
                        if (!_ch.byName.TryGetValue(n, out var t)) continue;
                        dedos.Add(t);
                        eixos.Add(fit.CurlAxis(n, side));
                        dobras.Add(f == 1 ? new[] { 18f, 26f, 32f }[j - 1] : new[] { 74f, 90f, 58f }[j - 1]);
                        direita.Add(side == "R");
                    }
            refs.dedos = dedos.ToArray();
            refs.eixosDedos = eixos.ToArray();
            refs.dobraDedos = dobras.ToArray();
            refs.dedoDaMaoDireita = direita.ToArray();
            refs.bronze = _pal[(int)M.PBronze];
            refs.bronzeEscuro = _pal[(int)M.PBronzeDark];
            refs.ouro = _pal[(int)M.PGold];
            refs.lamina = _pal[(int)M.PBlade];
            refs.carmesim = _pal[(int)M.PCrimson];
            refs.carmesimEscuro = _pal[(int)M.PCrimsonDark];
            refs.linho = _pal[(int)M.PLinen];
            refs.couro = _pal[(int)M.PLeather];
            refs.pele = _skin;
            refs.cabelo = _hair;
            refs.emblema = _pal[(int)M.PEmblem];
            refs.madeira = _pal[(int)M.PWood];
            Debug.Log($"[Troia] elmo: centro {h.c}, mão D palma {hr.palm} dedos {hr.fingers}, mão E palma {hl.palm}");
            design.Apply();
            return root;
        }

        // =====================================================================================
        // Cena-vitrine
        // =====================================================================================
        static void BuildShowroom(GameObject prefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // lua: luz de recorte fria, fraca, vinda de trás-esquerda
            var moonGo = new GameObject("Lua");
            moonGo.transform.rotation = Quaternion.LookRotation(-new Vector3(-0.55f, 0.6f, -0.45f).normalized);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.62f, 0.72f, 1f);
            moon.intensity = 1.1f;
            moon.shadows = LightShadows.Soft;
            moon.shadowNormalBias = 0.2f;
            RenderSettings.sun = moon;
            var sky = AssetDatabase.LoadAssetAtPath<Material>(TroiaAssets.MatDir + "/M_CeuNoturno.mat");
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.39f, 0.47f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.32f, 0.27f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.12f, 0.1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.09f, 0.07f, 0.11f);
            RenderSettings.fogDensity = 0.035f;

            var vol = new GameObject("Volume_PosProcessamento").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = ShowroomProfile();

            // pedestal de pedra escaneada em dois degraus (topo em y = 0) e chão em volta
            var stone = Lit("M_Pat_Pedestal", Tex($"{PhDir}/pedra_albedo.jpg", TexKind.Color), Tex($"{PhDir}/pedra_normal.jpg", TexKind.Normal),
                Tex($"{PhDir}/pedra_ms.png", TexKind.Linear, 2048), new Color(0.92f, 0.86f, 0.8f), 1f, bump: 1f, character: false);
            var ground = Lit("M_Pat_Chao", Tex($"{PhDir}/pedra_albedo.jpg", TexKind.Color), Tex($"{PhDir}/pedra_normal.jpg", TexKind.Normal),
                Tex($"{PhDir}/pedra_ms.png", TexKind.Linear, 2048), new Color(0.42f, 0.38f, 0.36f), 0.8f, bump: 1f, character: false);
            var pedMesh = TroiaAssets.SaveMesh(Pedestal(new[] { (1.62f, 0f, 0.035f, 0.22f), (2.25f, -0.22f, 0.03f, 0.2f) }, 2.4f), $"{MeshDir}/Pat_Pedestal.asset");
            var floor = new GameObject("Pedestal");
            floor.AddComponent<MeshFilter>().sharedMesh = pedMesh;
            floor.AddComponent<MeshRenderer>().sharedMaterial = stone;
            var groundMesh = TroiaAssets.SaveMesh(Pedestal(new[] { (40f, -0.42f, 0f, 0.01f) }, 3.2f), $"{MeshDir}/Pat_Chao.asset");
            var groundGo = new GameObject("Chao");
            groundGo.AddComponent<MeshFilter>().sharedMesh = groundMesh;
            groundGo.AddComponent<MeshRenderer>().sharedMaterial = ground;

            // partículas de fogo sem compressão em blocos (ampliadas no fundo, a compressão vira faixas)
            foreach (var texName in new[] { "T_Particula_Chama", "T_Particula_Fumaca", "T_Particula_Brasa" })
            {
                var pti = AssetImporter.GetAtPath($"{TroiaAssets.TexDir}/{texName}.png") as TextureImporter;
                if (pti != null && pti.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    pti.textureCompression = TextureImporterCompression.Uncompressed;
                    pti.SaveAndReimport();
                }
            }
            var softFire = SoftFireMaterial();
            var brazier = AssetDatabase.LoadAssetAtPath<GameObject>(TroiaAssets.PrefabDir + "/Aderecos/Braseiro.prefab");
            if (brazier != null)
            {
                // no segundo degrau, atrás do herói
                foreach (var p in new[] { new Vector3(-1.5f, -0.22f, -1.35f), new Vector3(1.5f, -0.22f, -1.35f) })
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(brazier);
                    g.transform.position = p;
                    // a luz laranja dos braseiros fica como recorte, sem tingir a pele inteira
                    foreach (var fl in g.GetComponentsInChildren<Light>(true)) fl.intensity *= 0.45f;
                    // chama com textura de 16 bits: sem as faixas do degradê de 8 bits somado em HDR
                    if (softFire != null)
                        foreach (var psr in g.GetComponentsInChildren<ParticleSystemRenderer>(true))
                            if (psr.sharedMaterial != null && psr.sharedMaterial.name == "M_Fogo") psr.sharedMaterial = softFire;
                }
            }

            // luz principal: tocha alta à frente-esquerda, quente e quase neutra (rosto e musculatura)
            var key = new GameObject("Luz_Principal").AddComponent<Light>();
            key.type = LightType.Spot;
            key.color = new Color(1f, 0.93f, 0.85f);
            key.range = 9f;
            key.spotAngle = 50f;
            key.innerSpotAngle = 18f;
            key.intensity = 7.5f;
            key.shadows = LightShadows.Soft;
            key.shadowResolution = LightShadowResolution.VeryHigh;
            key.shadowNormalBias = 0.1f;
            key.transform.position = new Vector3(-1.6f, 2.8f, 2.4f);
            key.transform.LookAt(new Vector3(0f, 1.3f, 0f));

            // preenchimento frio (luar) do lado oposto, para as sombras não ficarem pretas
            var fill = new GameObject("Luz_de_Preenchimento").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.62f, 0.7f, 0.95f);
            fill.range = 7f;
            fill.intensity = 2.4f;
            fill.transform.position = new Vector3(2.1f, 1.9f, 2.0f);

            // recortes quentes dos braseiros atrás: contorno do bronze e dos músculos
            foreach (var (pos, inten) in new[] { (new Vector3(-1.3f, 1.9f, -1.5f), 2.4f), (new Vector3(1.3f, 1.7f, -1.5f), 2f) })
            {
                var rim = new GameObject("Luz_de_Recorte").AddComponent<Light>();
                rim.type = LightType.Point;
                rim.color = new Color(1f, 0.66f, 0.42f);
                rim.range = 5f;
                rim.intensity = inten;
                rim.transform.position = pos;
            }

            // sonda de reflexo: o metal reflete um estúdio quente (HDRI), a pedra e o fogo, não o céu escuro
            var probeGo = new GameObject("Sonda_de_Reflexo");
            probeGo.transform.position = new Vector3(0f, 1.2f, 0.6f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom;
            probe.size = new Vector3(10f, 7f, 10f);
            probe.resolution = 512;
            probe.hdr = true;
            probe.intensity = 1f;
            var studio = StudioSkybox();
            var night = RenderSettings.skybox;
            if (studio != null) RenderSettings.skybox = studio;
            string cube = Root + "/Patroclo_Vitrine_Reflexo.exr";
            if (Lightmapping.BakeReflectionProbe(probe, cube))
            {
                AssetDatabase.ImportAsset(cube, ImportAssetOptions.ForceUpdate);
                probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(cube);
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = probe.customBakedTexture;
            }
            RenderSettings.skybox = night;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            // a sola da sandália fica abaixo do pé: sobe o personagem para ela tocar o chão
            inst.transform.position = new Vector3(0f, F.SoleThickness, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.03f;
            cam.allowMSAA = true;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true; // sem faixas (banding) nos degradês do fogo e do céu
            camGo.transform.position = new Vector3(0f, 1.08f, 4.6f);
            camGo.transform.LookAt(new Vector3(0f, 1.0f, 0f));

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>Discos empilhados (raio, topo, chanfro, altura) com UV plano no topo, em metros / escala.</summary>
        static Mesh Pedestal((float r, float top, float bevel, float h)[] steps, float uvMeters)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            const int seg = 128;
            foreach (var (r, top, bevel, h) in steps)
            {
                // topo: leque com UV plano
                int c = v.Count;
                v.Add(new Vector3(0f, top, 0f)); n.Add(Vector3.up); uv.Add(Vector2.zero);
                float ri = r - bevel;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    var q = new Vector3(Mathf.Cos(a) * ri, top, Mathf.Sin(a) * ri);
                    v.Add(q); n.Add(Vector3.up); uv.Add(new Vector2(q.x, q.z) / uvMeters);
                }
                for (int i = 0; i < seg; i++) { t.Add(c); t.Add(c + 2 + i); t.Add(c + 1 + i); }
                // chanfro e lateral (UV: ângulo x altura)
                var ring = new List<(float rr, float y, Vector3 nn)>();
                if (bevel > 0f)
                {
                    ring.Add((ri, top, new Vector3(0f, 1f, 0f)));
                    ring.Add((r, top - bevel, new Vector3(1f, 1f, 0f).normalized));
                }
                ring.Add((r, top - bevel, new Vector3(1f, 0f, 0f)));
                ring.Add((r, top - h, new Vector3(1f, 0f, 0f)));
                for (int k = 0; k < ring.Count - 1; k++)
                {
                    int b0 = v.Count;
                    for (int j = 0; j < 2; j++)
                    {
                        var (rr, y, nn) = ring[k + j];
                        for (int i = 0; i <= seg; i++)
                        {
                            float a = i * Mathf.PI * 2f / seg;
                            v.Add(new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
                            n.Add(new Vector3(Mathf.Cos(a) * nn.x, nn.y, Mathf.Sin(a) * nn.x).normalized);
                            uv.Add(new Vector2(a * r / uvMeters, y / uvMeters));
                        }
                    }
                    for (int i = 0; i < seg; i++)
                    {
                        int A = b0 + i, B = A + 1, C = A + seg + 1, D = C + 1;
                        t.Add(A); t.Add(B); t.Add(C);
                        t.Add(B); t.Add(D); t.Add(C);
                    }
                }
            }
            var m = new Mesh { name = "Pedestal" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>
        /// Material de chama da vitrine: cópia do M_Fogo com uma textura de 16 bits por canal (EXR). As chamas
        /// ficam grandes e desfocadas atrás do personagem; com 8 bits, a soma aditiva em HDR vira faixas.
        /// </summary>
        static Material SoftFireMaterial()
        {
            var fire = AssetDatabase.LoadAssetAtPath<Material>(TroiaAssets.MatDir + "/M_Fogo.mat");
            if (fire == null) return null;
            string path = TexDir + "/T_Pat_ChamaSuave.exr";
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBAHalf, false, true);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px);
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), tex.EncodeToEXR(Texture2D.EXRFlags.None));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.sRGBTexture = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
            var m = new Material(fire) { name = "M_Pat_FogoVitrine" };
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            return TroiaAssets.SaveAsset(m, $"{MatDir}/M_Pat_FogoVitrine.mat");
        }

        /// <summary>Céu de estúdio (HDRI do Poly Haven) usado só para assar os reflexos da vitrine.</summary>
        static Material StudioSkybox()
        {
            string path = $"{PhDir}/estudio_reflexo.hdr";
            if (!File.Exists(path)) return null;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ti = (TextureImporter)AssetImporter.GetAtPath(path);
            }
            if (ti.textureShape != TextureImporterShape.TextureCube)
            {
                ti.textureShape = TextureImporterShape.TextureCube;
                ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            var cubeTex = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            var m = new Material(Shader.Find("Skybox/Cubemap")) { name = "M_Pat_CeuEstudio" };
            m.SetTexture("_Tex", cubeTex);
            m.SetColor("_Tint", new Color(0.56f, 0.48f, 0.42f));
            m.SetFloat("_Exposure", 1.15f);
            return TroiaAssets.SaveAsset(m, $"{MatDir}/M_Pat_CeuEstudio.mat");
        }

        /// <summary>Pós-processamento próprio da vitrine: tons limpos, bloom contido, sem granulação.</summary>
        static VolumeProfile ShowroomProfile()
        {
            string path = Root + "/PosProcessamento_Vitrine.asset";
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var tm = p.Add<Tonemapping>();
            tm.mode.Override(TonemappingMode.ACES);
            var ca = p.Add<ColorAdjustments>();
            ca.postExposure.Override(0.8f);
            ca.contrast.Override(10f);
            ca.saturation.Override(4f);
            var wb = p.Add<WhiteBalance>();
            wb.temperature.Override(-2f);
            var st = p.Add<SplitToning>();
            st.shadows.Override(new Color(0.48f, 0.5f, 0.58f));
            st.highlights.Override(new Color(0.9f, 0.8f, 0.68f));
            st.balance.Override(-10f);
            var bloom = p.Add<Bloom>();
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(0.4f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(1f, 0.88f, 0.76f));
            bloom.highQualityFiltering.Override(true);
            var vg = p.Add<Vignette>();
            vg.intensity.Override(0.3f);
            vg.smoothness.Override(0.42f);
            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        // =====================================================================================
        // Capturas (conferência visual)
        // =====================================================================================
        public static void CaptureFromCommandLine()
        {
            string dir = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-capturaDir") dir = args[i + 1];
            dir = dir ?? Path.Combine(Directory.GetCurrentDirectory(), "Capturas", "Patroclo");
            Directory.CreateDirectory(dir);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var d = Object.FindFirstObjectByType<PatrocloDesign>();
            var cam = Camera.main;
            // supersampling: renderiza em 3200x4000 com MSAA 4x e reduz para 1600x2000
            const int W = 1600, H = 2000;
            var rt = new RenderTexture(W * 2, H * 2, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            rt.Create();
            var small = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Bilinear };
            small.Create();
            cam.targetTexture = rt;

            void Shot(string name, Vector3 pos, Vector3 target, float fov = 30f)
            {
                d.Apply();
                foreach (var smr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None)) smr.forceMatrixRecalculationPerRender = true;
                foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) ps.Simulate(2f, false, true, true);
                cam.fieldOfView = fov;
                cam.transform.position = pos;
                cam.transform.LookAt(target);
                cam.Render();
                Graphics.Blit(rt, small);
                var prev = RenderTexture.active;
                RenderTexture.active = small;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            var c = new Vector3(0f, 1.0f, 0f);
            Shot("00_aquecimento", new Vector3(0f, 1.08f, 4.6f), c);
            File.Delete(Path.Combine(dir, "00_aquecimento.png"));
            d.pose = PosePatroclo.Neutra; d.escudo = PosicaoEscudo.Costas; d.notaDeEstilo = NotaEstilo.D; d.bencao = Bencao.Nenhuma; d.favorDivino = 0f; d.elmo = true;
            Shot("01_frente", new Vector3(0f, 1.08f, 4.6f), c);
            Shot("02_tres_quartos", new Vector3(2.7f, 1.4f, 3.5f), c);
            Shot("03_costas", new Vector3(-0.5f, 1.3f, -4.5f), c);
            Shot("04_perfil", new Vector3(4.6f, 1.08f, 0f), c);
            Shot("05_rosto_elmo", new Vector3(0.38f, 1.76f, 1.05f), new Vector3(0f, 1.73f, 0.05f), 28f);
            d.elmo = false;
            Shot("06_rosto", new Vector3(0.3f, 1.75f, 0.85f), new Vector3(0f, 1.71f, 0.06f), 28f);
            d.elmo = true;
            Shot("07_torso", new Vector3(0.9f, 1.35f, 1.6f), new Vector3(0f, 1.2f, 0f), 34f);
            d.pose = PosePatroclo.Combate; d.escudo = PosicaoEscudo.Braco; d.notaDeEstilo = NotaEstilo.SSS; d.favorDivino = 1f;
            Shot("08_combate_SSS", new Vector3(-2.3f, 1.35f, 3.7f), new Vector3(0f, 0.95f, 0f));
            d.notaDeEstilo = NotaEstilo.A; d.favorDivino = 0.5f; d.bencao = Bencao.Zeus;
            Shot("09_bencao_zeus", new Vector3(2.6f, 1.35f, 3.5f), new Vector3(0f, 0.95f, 0f));
            d.pose = PosePatroclo.Repouso; d.escudo = PosicaoEscudo.Costas; d.notaDeEstilo = NotaEstilo.D; d.bencao = Bencao.Nenhuma; d.favorDivino = 0f;
            Shot("10_repouso_A", new Vector3(0f, 1.08f, 5.0f), c, 34f);
            d.pose = PosePatroclo.Neutra; d.favorDivino = 0f;
            d.armadura = false; d.elmo = false; d.capa = false;
            Shot("11_corpo_sem_armadura", new Vector3(1.5f, 1.35f, 3.2f), new Vector3(0f, 1.05f, 0f));
            Shot("12_torso_sem_armadura", new Vector3(0.75f, 1.38f, 1.45f), new Vector3(0f, 1.22f, 0f), 34f);
            Shot("13_rosto_frente", new Vector3(0f, 1.74f, 0.78f), new Vector3(0f, 1.705f, 0.06f), 26f);
            Shot("14_braco", new Vector3(-0.85f, 1.15f, 0.85f), new Vector3(-0.3f, 1.07f, 0.12f), 30f);
            // costas da armadura (sem a capa e com o escudo no braço, que normalmente as cobrem)
            d.armadura = true; d.elmo = true; d.capa = false; d.escudo = PosicaoEscudo.Braco;
            Shot("15_costas_sem_capa", new Vector3(-0.6f, 1.3f, -4.4f), c);
            Shot("16_pernas_costas", new Vector3(-0.55f, 0.6f, -1.6f), new Vector3(0f, 0.42f, 0f), 30f);
            // desgaste de batalha: a capa rasgada inteira (com o escudo no braço) e a adaga no quadril direito
            d.capa = true;
            Shot("17_capa_rasgada", new Vector3(-0.7f, 1.15f, -3.3f), new Vector3(0f, 0.9f, 0f));
            d.escudo = PosicaoEscudo.Costas;
            Shot("18_adaga_e_cintura", new Vector3(1.25f, 1.05f, 0.75f), new Vector3(0.12f, 0.98f, 0.02f), 30f);
            d.capa = true; d.escudo = PosicaoEscudo.Costas;
            d.Apply();

            cam.targetTexture = null;
            rt.Release();
            small.Release();
            Debug.Log($"[Troia] Capturas do Pátroclo em {dir}");
        }
    }
}
