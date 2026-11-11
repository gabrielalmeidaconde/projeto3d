using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Col = Troia.EditorTools.TroiaAssets.Col;

namespace Troia.EditorTools
{
    /// <summary>
    /// Gera a cena "Fase1_NoiteDaInvasao": Troia na noite em que os gregos saem do cavalo.
    /// Menu: Troia > Gerar Mapa - Fase 1. Tudo é determinístico (mesma semente => mesmo mapa).
    /// </summary>
    public static class TroiaMapBuilder
    {
        static System.Random R;
        static int _houseLights;
        const int MaxHouseLights = 55;
        static readonly List<Vector2> _houses = new List<Vector2>();
        static Transform _decals;
        static int _decalCount;

        static float Rf(float a, float b) => a + (float)R.NextDouble() * (b - a);
        static Vector3 G(float x, float z, float dy = 0f) => new Vector3(x, TroiaLayout.Height(x, z) + dy, z);
        static Area A(string name) => TroiaLayout.Walk.Find(a => a.Name == name);

        [MenuItem("Troia/Gerar Mapa - Fase 1 (Noite da Invasão)", priority = 1)]
        static void BuildMenu()
        {
            if (!EditorUtility.DisplayDialog("Gerar mapa da Fase 1",
                    "Isto recria a cena Fase1_NoiteDaInvasao e os assets em Assets/Troia/Gerado.\n" +
                    "Edições feitas à mão nesses arquivos serão perdidas. Continuar?", "Gerar", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void BuildFromCommandLine() => Build();

        static void Progress(string msg, float t) => EditorUtility.DisplayProgressBar("Troia - gerando mapa", msg, t);

        public static void Build()
        {
            var sw = Stopwatch.StartNew();
            try
            {
                Progress("Preparando", 0.01f);
                TroiaAssets.EnsureFolders();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                TroiaAssets.CreateAll(Progress);

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                R = new System.Random(1184); // 1184 a.C., data tradicional da queda de Troia
                _houseLights = 0;
                _houses.Clear();
                _props.Clear();

                var env = Group("--- AMBIENTE ---");
                var lower = Group("--- CIDADE BAIXA ---");
                var citadel = Group("--- PERGAMO (CIDADELA) ---");
                var outside = Group("--- EXTERIOR ---");
                var fx = Group("--- EFEITOS ---");
                var gameplay = Group("--- GAMEPLAY ---");
                var fires = Group("Incendios", fx);
                _decals = Group("Decals", fx);
                _decalCount = 0;

                Progress("Iluminação e céu", 0.45f);
                BuildLighting(env);
                BuildShaderGlobals(env);
                Progress("Terreno", 0.5f);
                BuildTerrain(env);
                Progress("Muralhas", 0.6f);
                BuildWalls(lower, citadel);
                Progress("Áreas da fase", 0.65f);
                BuildAgora(lower);
                BuildMarket(lower, fires);
                BuildGateCourt(lower);
                BuildAvenue(lower, fires);
                BuildFountainSquare(lower, fires);
                BuildSideStreets(lower, fires);
                BuildRamp(lower);
                BuildCitadel(citadel, fires);
                ArenaBraziers(Group("Braseiros_das_Arenas", fx));
                Progress("Casas", 0.72f);
                PlaceHouses(Group("Casas_Cidade_Baixa", lower), Group("Casas_Cidadela", citadel), fires);
                Progress("Exterior", 0.8f);
                ScatterGroundDecals();
                WallCracks();
                ScatterDebris(Group("Detritos", lower));
                BuildExterior(outside, fires);
                BuildAtmosphere(fx);
                BuildGameplayMarkers(gameplay);
                Progress("NavMesh", 0.85f);
                BakeNavMesh(gameplay);
                CreateTestPlayer(gameplay, G(-50.5f, -39.5f), 190f);

                Progress("Salvando", 0.97f);
                EditorSceneManager.SaveScene(scene, TroiaAssets.ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(TroiaAssets.ScenePath, true) };
                AssetDatabase.SaveAssets();
                Debug.Log($"[Troia] Mapa da Fase 1 gerado em {sw.Elapsed.TotalSeconds:F1}s: {_houses.Count} casas, " +
                          $"{_houseLights} luzes de incêndio, {_decalCount} decals. Cena: {TroiaAssets.ScenePath}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // =====================================================================================
        // Helpers
        // =====================================================================================
        static Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject MeshObj(string name, Transform parent, MeshBuilder b, Vector3 pos, Quaternion rot, Col col, bool isStatic = true)
        {
            var mesh = TroiaAssets.SaveMesh(b.ToMesh(name, TroiaAssets.Pal, out var mats), $"{TroiaAssets.MeshDir}/{name}.asset");
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            TroiaAssets.AddCollider(go, mesh, col);
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, TroiaAssets.StaticFlags);
            return go;
        }

        static readonly List<Vector2> _props = new List<Vector2>();

        static GameObject Prop(string name, Transform parent, float x, float z, float yaw, float scale = 1f, float dy = 0f)
        {
            _props.Add(new Vector2(x, z));
            if (name == "Braseiro" || name == "Altar_Pequeno") DecalGround("Queimado", x, z, Rf(1.6f, 2.4f));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(TroiaAssets.Props[name], parent);
            go.transform.SetPositionAndRotation(G(x, z, dy), Quaternion.Euler(0f, yaw, 0f));
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = Vector3.one * scale;
            return go;
        }

        static GameObject Fire(string name, Transform parent, Vector3 pos, float scale = 1f)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(TroiaAssets.Fires[name], parent);
            go.transform.position = pos;
            if (_decals != null && !name.StartsWith("Fogo_Tocha") && pos.y - TroiaLayout.Height(pos.x, pos.z) < 2.2f)
            {
                float sz = name.StartsWith("Fogo_Grande") ? 6f : name.StartsWith("Fogo_Medio") ? 4f : 2.2f;
                DecalGround("Queimado", pos.x, pos.z, sz * Rf(0.9f, 1.2f));
                DecalGround("Cinzas", pos.x + Rf(-1f, 1f), pos.z + Rf(-1f, 1f), sz * 0.6f);
            }
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>Espalha caixotes, ânforas e cestos junto às bordas de uma área.</summary>
        static void EdgeClutter(Transform parent, Area a, int count, params Vector2[] avoid)
        {
            string[] items = { "Caixote", "Anfora", "Cesto", "Caixote", "Anfora", "Anfora" };
            var edges = new List<(Vector2 a, Vector2 b, Vector2 outward)>(a.Edges());
            for (int i = 0; i < count; i++)
            {
                var e = edges[R.Next(edges.Count)];
                Vector2 p = Vector2.Lerp(e.a, e.b, Rf(0.08f, 0.92f)) - e.outward * Rf(0.7f, 1.5f);
                if (a.Sdf(p) > -0.5f) continue;
                bool blocked = false;
                foreach (var v in avoid) if ((v - p).sqrMagnitude < 36f) blocked = true;
                if (blocked) continue;
                Prop(items[R.Next(items.Length)], parent, p.x, p.y, Rf(0f, 360f), Rf(0.85f, 1.15f));
                if (R.NextDouble() < 0.4)
                    Prop("Anfora", parent, p.x + Rf(-0.9f, 0.9f), p.y + Rf(-0.9f, 0.9f), Rf(0f, 360f), Rf(0.8f, 1.1f));
            }
        }


        // =====================================================================================
        // Sujeira: parâmetros globais do shader, decals e detritos
        // =====================================================================================
        static void BuildShaderGlobals(Transform parent)
        {
            const int W = 512, H = 640;
            var min = new Vector2(-120f, -150f);
            var size = new Vector2(240f, 300f);
            const float minY = -10f, rangeY = 30f;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float wz = min.y + (y + 0.5f) / H * size.y;
                for (int x = 0; x < W; x++)
                {
                    float wx = min.x + (x + 0.5f) / W * size.x;
                    float h = Mathf.Clamp01((TroiaLayout.Height(wx, wz) - minY) / rangeY);
                    px[y * W + x] = new Color(h, h, h, 1f);
                }
            }
            string path = $"{TroiaAssets.TexDir}/T_Altura_Chao.png";
            TexGen.SavePng(path, px, W, H);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture = false;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.SaveAndReimport();

            var go = new GameObject("Sujeira_Global (shader)");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<TroiaShaderGlobals>();
            g.grimeTexture = TroiaAssets.GrimeTex;
            g.groundHeightTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            g.groundMin = min;
            g.groundSize = size;
            g.groundMinY = minY;
            g.groundRangeY = rangeY;
            g.Apply();
        }

        static void DecalGround(string kind, float x, float z, float size, float fade = 1f)
        {
            if (_decals == null) return;
            var go = new GameObject("Decal_" + kind);
            go.transform.SetParent(_decals, false);
            go.transform.SetPositionAndRotation(G(x, z, 1.2f), Quaternion.Euler(90f, Rf(0f, 360f), 0f));
            var d = go.AddComponent<DecalProjector>();
            d.material = TroiaAssets.Decals[kind];
            d.size = new Vector3(size, size, 3f);
            d.pivot = new Vector3(0f, 0f, 1.5f);
            d.fadeFactor = fade;
            d.drawDistance = 90f;
            _decalCount++;
        }

        static void DecalWall(string kind, Vector3 facePoint, Vector3 outward, float w, float h, float fade = 1f)
        {
            if (_decals == null) return;
            outward.y = 0f;
            outward.Normalize();
            var go = new GameObject("Decal_" + kind);
            go.transform.SetParent(_decals, false);
            go.transform.SetPositionAndRotation(facePoint + outward * 0.6f, Quaternion.LookRotation(-outward, Vector3.up));
            var d = go.AddComponent<DecalProjector>();
            d.material = TroiaAssets.Decals[kind];
            d.size = new Vector3(w, h, 1.6f);
            d.pivot = new Vector3(0f, 0f, 0.8f);
            d.fadeFactor = fade;
            d.drawDistance = 90f;
            _decalCount++;
        }

        static Vector2 RandomPointIn(Area a)
        {
            if (a.IsRect)
                return a.Center + new Vector2(Rf(-0.5f, 0.5f) * a.Size.x, Rf(-0.5f, 0.5f) * a.Size.y);
            int i = R.Next(a.Pts.Length - 1);
            Vector2 p0 = a.Pts[i], p1 = a.Pts[i + 1];
            Vector2 dir = (p1 - p0).normalized;
            return Vector2.Lerp(p0, p1, Rf(0f, 1f)) + new Vector2(-dir.y, dir.x) * Rf(-0.45f, 0.45f) * a.Width;
        }

        static void ScatterGroundDecals()
        {
            string[] arenas = { "Agora", "Mercado", "Patio_Portas_Ceias", "Praca_da_Fonte", "Patio_Cidadela", "Patio_Palacio" };
            foreach (var a in TroiaLayout.Walk)
            {
                float area = a.IsRect ? a.Size.x * a.Size.y : a.Width * PolyLength(a.Pts);
                int mud = Mathf.Clamp(Mathf.RoundToInt(area / 70f), 3, 24);
                for (int i = 0; i < mud; i++)
                {
                    var p = RandomPointIn(a);
                    DecalGround(R.NextDouble() < 0.75 ? "Lama" : "Cinzas", p.x, p.y, Rf(2f, 5.5f), Rf(0.6f, 1f));
                }
                bool isArena = System.Array.IndexOf(arenas, a.Name) >= 0;
                int blood = isArena ? (a.Name == "Agora" ? 2 : 5) : (a.Paved ? 1 : 0);
                for (int i = 0; i < blood; i++)
                {
                    var p = RandomPointIn(a);
                    DecalGround("Sangue", p.x, p.y, Rf(1.4f, 3f), Rf(0.75f, 1f));
                }
            }
        }

        static float PolyLength(Vector2[] pts)
        {
            float l = 0f;
            for (int i = 0; i < pts.Length - 1; i++) l += Vector2.Distance(pts[i], pts[i + 1]);
            return l;
        }

        /// <summary>Rachaduras e fuligem na face interna da muralha externa.</summary>
        static void WallCracks()
        {
            for (int i = 0; i < 70; i++)
            {
                float t = Rf(-80f, 260f) * Mathf.Deg2Rad;
                var p = new Vector2(TroiaLayout.Rx * Mathf.Cos(t), TroiaLayout.Rz * Mathf.Sin(t));
                var inward = -new Vector2(p.x / (TroiaLayout.Rx * TroiaLayout.Rx), p.y / (TroiaLayout.Rz * TroiaLayout.Rz)).normalized;
                Vector3 inw = new Vector3(inward.x, 0f, inward.y);
                bool high = R.NextDouble() < 0.4;
                float y = high ? Rf(7f, 10.5f) : Rf(1.5f, 5f);
                Vector3 face = new Vector3(p.x, 0f, p.y) + inw * (high ? 2.25f : 3.0f) + Vector3.up * y;
                DecalWall(R.NextDouble() < 0.7 ? "Rachadura" : "Fuligem", face, inw, Rf(2.5f, 4.5f), Rf(2.5f, 4f));
            }
        }

        static void ScatterDebris(Transform parent)
        {
            int idx = 0;
            foreach (var a in TroiaLayout.Walk)
            {
                float area = a.IsRect ? a.Size.x * a.Size.y : a.Width * PolyLength(a.Pts);
                int n = Mathf.Clamp(Mathf.RoundToInt(area / 12f), 8, 140);
                var pts = new List<Vector3>();
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = RandomPointIn(a);
                    // concentra perto das bordas (onde o lixo se acumula)
                    if (a.IsRect && R.NextDouble() < 0.6)
                    {
                        Vector2 d = p - a.Center;
                        float k = Rf(0.75f, 0.97f);
                        if (Mathf.Abs(d.x) / a.Size.x > Mathf.Abs(d.y) / a.Size.y) d.x = Mathf.Sign(d.x) * a.Size.x * 0.5f * k;
                        else d.y = Mathf.Sign(d.y) * a.Size.y * 0.5f * k;
                        p = a.Center + d;
                    }
                    pts.Add(G(p.x, p.y));
                }
                var mesh = TroiaMeshes.Debris(pts, 900 + idx);
                var go = MeshObj($"Detritos_{a.Name}", parent, mesh, Vector3.zero, Quaternion.identity, Col.None);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                idx++;
            }
        }

        // =====================================================================================
        // Ambiente
        // =====================================================================================
        static void BuildLighting(Transform parent)
        {
            var moonGo = new GameObject("Lua (Luz Direcional)");
            moonGo.transform.SetParent(parent, false);
            moonGo.transform.rotation = Quaternion.LookRotation(-TroiaAssets.MoonDir);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.75f, 0.81f, 1f);
            moon.intensity = 2.4f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.8f;

            RenderSettings.sun = moon;
            RenderSettings.skybox = TroiaAssets.SkyMat;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.47f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.33f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.14f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.003f;
            RenderSettings.fogColor = new Color(0.16f, 0.14f, 0.2f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.45f;

            var volGo = new GameObject("Volume_PosProcessamento");
            volGo.transform.SetParent(parent, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            vol.sharedProfile = TroiaAssets.Profile;

            string lsPath = $"{TroiaAssets.ConfigDir}/Iluminacao_Fase1.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(lsPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "Iluminacao_Fase1" };
                AssetDatabase.CreateAsset(ls, lsPath);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            ls.autoGenerate = false;
            Lightmapping.lightingSettings = ls;

            ConfigureRenderPipeline();
        }

        /// <summary>Sombras em alta resolução e SSAO mais marcado no perfil de PC do URP.</summary>
        static void ConfigureRenderPipeline()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (urp != null)
            {
                var so = new SerializedObject(urp);
                void SetF(string prop, float v) { var sp = so.FindProperty(prop); if (sp != null) sp.floatValue = v; }
                void SetI(string prop, int v) { var sp = so.FindProperty(prop); if (sp != null) sp.intValue = v; }
                SetF("m_ShadowDistance", 120f);
                SetI("m_ShadowCascadeCount", 4);
                SetI("m_MainLightShadowmapResolution", 4096);
                SetI("m_AdditionalLightsShadowmapResolution", 4096);
                SetI("m_SoftShadowQuality", 3);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(urp);
            }
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (rd != null && !rd.rendererFeatures.Exists(f => f is DecalRendererFeature))
            {
                var df = ScriptableObject.CreateInstance<DecalRendererFeature>();
                df.name = "Decal";
                AssetDatabase.AddObjectToAsset(df, rd);
                rd.rendererFeatures.Add(df);
                typeof(ScriptableRendererData).GetMethod("UpdateMap", BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(rd, null);
                rd.SetDirty();
                EditorUtility.SetDirty(rd);
                AssetDatabase.SaveAssets();
            }
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/PC_Renderer.asset"))
            {
                if (obj == null || obj.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                var so = new SerializedObject(obj);
                void SetF(string prop, float v) { var sp = so.FindProperty(prop); if (sp != null) sp.floatValue = v; }
                SetF("m_Settings.Intensity", 1.1f);
                SetF("m_Settings.Radius", 0.6f);
                SetF("m_Settings.DirectLightingStrength", 0.35f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obj);
            }
        }

        static TerrainLayer Layer(string name, Texture2D d, Texture2D n, float tile, float smooth, Color tint)
        {
            var l = new TerrainLayer
            {
                name = name, diffuseTexture = d, normalMapTexture = n, tileSize = new Vector2(tile, tile),
                smoothness = smooth, metallic = 0f, normalScale = 1f,
                diffuseRemapMin = Vector4.zero, diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f)
            };
            return TroiaAssets.SaveAsset(l, $"{TroiaAssets.TerrainDir}/Camada_{name}.terrainlayer");
        }

        static void BuildTerrain(Transform parent)
        {
            const int res = 1025, ar = 1024;
            float size = TroiaLayout.TerrainSize;
            Vector3 tp = TroiaLayout.TerrainPos;
            // o asset precisa existir ANTES da pintura, senão os splatmaps não são salvos junto
            string path = $"{TroiaAssets.TerrainDir}/Terreno_Fase1.asset";
            AssetDatabase.DeleteAsset(path);
            var td = new TerrainData { heightmapResolution = res };
            td.size = new Vector3(size, TroiaLayout.TerrainHeight, size);
            AssetDatabase.CreateAsset(td, path);
            var h = new float[res, res];
            for (int y = 0; y < res; y++)
            {
                float wz = tp.z + y / (float)(res - 1) * size;
                for (int x = 0; x < res; x++)
                {
                    float wx = tp.x + x / (float)(res - 1) * size;
                    h[y, x] = Mathf.Clamp01((TroiaLayout.Height(wx, wz) - TroiaLayout.TerrainBaseY) / TroiaLayout.TerrainHeight);
                }
            }
            td.SetHeights(0, 0, h);

            td.alphamapResolution = ar;
            td.terrainLayers = new[]
            {
                Layer("Terra", TroiaAssets.TexEarth, TroiaAssets.TexEarthN, 4f, 0f, new Color(0.92f, 0.86f, 0.8f)),
                Layer("Calcamento", TroiaAssets.TexCobble, TroiaAssets.TexCobbleN, 4f, 0.05f, new Color(0.76f, 0.69f, 0.6f)),
                Layer("Grama", TroiaAssets.TexGrass, TroiaAssets.TexGrassN, 5f, 0f, Color.white),
                Layer("Areia", TroiaAssets.TexSand, TroiaAssets.TexSandN, 6f, 0f, new Color(0.9f, 0.86f, 0.8f))
            };
            var am = new float[ar, ar, 4];
            for (int y = 0; y < ar; y++)
            {
                float wz = tp.z + (y + 0.5f) / ar * size;
                for (int x = 0; x < ar; x++)
                {
                    float wx = tp.x + (x + 0.5f) / ar * size;
                    float e = TroiaLayout.Ellipse(wx, wz);
                    float w0, w1 = 0f, w2 = 0f, w3 = 0f;
                    if (e < 1.03f)
                    {
                        // calçamento com terra/lama acumulada de forma irregular
                        float dn = Mathf.PerlinNoise(wx * 0.09f + 17.3f, wz * 0.09f + 5.1f) * 0.65f + Mathf.PerlinNoise(wx * 0.31f + 3.7f, wz * 0.31f + 9.9f) * 0.35f;
                        float dirtIn = TroiaLayout.Smooth01((dn - 0.42f) / 0.14f) * 0.8f;
                        w1 = TroiaLayout.PavedWeight(wx, wz) * (1f - dirtIn);
                        w0 = 1f - w1;
                    }
                    else
                    {
                        float hh = TroiaLayout.Height(wx, wz);
                        float n = Mathf.PerlinNoise(wx * 0.03f, wz * 0.03f);
                        float dist = (e - 1f) * 120f;
                        float dirt = Mathf.Max(TroiaLayout.RoadMask(wx, wz), 1f - TroiaLayout.Smooth01((dist - 6f) / 10f));
                        dirt = Mathf.Max(dirt, TroiaLayout.Smooth01((n - 0.62f) / 0.1f) * 0.8f);
                        w3 = TroiaLayout.Smooth01((-1.5f - hh) / 1.5f);
                        w0 = dirt * (1f - w3);
                        w2 = (1f - dirt) * (1f - w3);
                    }
                    float sum = w0 + w1 + w2 + w3;
                    am[y, x, 0] = w0 / sum;
                    am[y, x, 1] = w1 / sum;
                    am[y, x, 2] = w2 / sum;
                    am[y, x, 3] = w3 / sum;
                }
            }
            td.SetAlphamaps(0, 0, am);
            td.baseMapResolution = 1024;
            foreach (var tex in td.alphamapTextures)
                if (tex != null && !AssetDatabase.Contains(tex)) AssetDatabase.AddObjectToAsset(tex, td);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Terreno";
            go.transform.SetParent(parent, false);
            go.transform.position = tp;
            var t = go.GetComponent<Terrain>();
            t.materialTemplate = TroiaAssets.TerrainMat;
            t.heightmapPixelError = 4f;
            t.basemapDistance = 250f;
            t.drawInstanced = true;
            GameObjectUtility.SetStaticEditorFlags(go, TroiaAssets.StaticFlags);
        }

        // =====================================================================================
        // Muralhas, portões e rampa
        // =====================================================================================
        static void BuildWalls(Transform lower, Transform citadel)
        {
            var walls = Group("Muralha_Externa", lower);
            var chunks = TroiaMeshes.OuterWall();
            for (int i = 0; i < chunks.Count; i++)
                MeshObj($"Muralha_Externa_{i + 1:00}", walls, chunks[i], Vector3.zero, Quaternion.identity, Col.Mesh);

            var cw = Group("Muralha_Cidadela", citadel);
            var cchunks = TroiaMeshes.CitadelWall();
            for (int i = 0; i < cchunks.Count; i++)
                MeshObj($"Muralha_Cidadela_{i + 1:00}", cw, cchunks[i], Vector3.zero, Quaternion.identity, Col.Mesh);
            MeshObj("Portao_da_Cidadela", cw, TroiaMeshes.CitadelGate(), Vector3.zero, Quaternion.identity, Col.Mesh);
        }

        static void BuildRamp(Transform parent)
        {
            var g = Group("Rampa_do_Pergamo", parent);
            MeshObj("Rampa_Muros", g, TroiaMeshes.RampWalls(), Vector3.zero, Quaternion.identity, Col.Mesh);
            foreach (float z in new[] { 24f, 34f, 44f })
            {
                float y = TroiaLayout.CitadelH * (z - TroiaLayout.RampZ0) / (TroiaLayout.RampZ1 - TroiaLayout.RampZ0);
                foreach (float sx in new[] { -1f, 1f })
                {
                    var t = (GameObject)PrefabUtility.InstantiatePrefab(TroiaAssets.Props[z < 40f ? "Tocha_Luz" : "Tocha"], g);
                    t.transform.position = new Vector3(5.55f * sx, y + 1.4f, z);
                    t.transform.localScale = Vector3.one * 0.8f;
                }
            }
            Prop("Escombros_Pequenos", g, -3.4f, 28f, 30f, 0.6f);
            Prop("Anfora", g, 3.8f, 38f, 0f);
        }

        // =====================================================================================
        // Áreas da cidade baixa
        // =====================================================================================
        static void BuildAgora(Transform parent)
        {
            var g = Group("Agora_Cavalo_de_Troia", parent);
            var horse = MeshObj("Cavalo_de_Troia", g, TroiaMeshes.Horse(), G(-55f, -40f), Quaternion.Euler(0f, 165f, 0f), Col.Mesh);
            foreach (var lp in new[] { new Vector3(2.4f, 1.28f, 5.8f), new Vector3(-2.4f, 1.28f, 5.8f), new Vector3(2.4f, 1.28f, -5.8f), new Vector3(-2.4f, 1.28f, -5.8f) })
            {
                var t = (GameObject)PrefabUtility.InstantiatePrefab(TroiaAssets.Props["Tocha_Luz"], g);
                t.transform.position = horse.transform.TransformPoint(lp);
            }

            // restos do banquete troiano (a cidade comemorou o "fim" da guerra)
            Prop("Mesa_Banquete", g, -70f, -50f, 90f);
            Prop("Mesa_Banquete", g, -70f, -36f, 90f);
            Prop("Mesa_Banquete", g, -40f, -54f, 15f);
            Prop("Mesa_Banquete", g, -41f, -32f, -20f);
            Prop("Banco", g, -71.6f, -50f, 90f);
            Prop("Banco", g, -68.4f, -50f, 90f);
            Prop("Banco", g, -71.6f, -36f, 90f);
            Prop("Banco", g, -68.4f, -36f, 85f);
            Prop("Banco", g, -40.5f, -55.6f, 15f);
            Prop("Banco", g, -41.5f, -30.4f, -20f);
            var avoid = new[] { new Vector2(-55f, -40f), new Vector2(-50.5f, -39.5f), new Vector2(-55f, -63f), new Vector2(-33f, -38f) };
            foreach (var (x, z) in new[] { (-74f, -30f), (-36f, -30f), (-74f, -60f), (-36f, -60f) })
                Prop("Braseiro", g, x, z, 0f);
            Prop("Altar_Pequeno", g, -55f, -29.8f, 0f);
            Prop("Estatua_Marmore", g, -35.5f, -45f, -90f);
            Prop("Estatua_Bronze", g, -74.5f, -45f, 90f);
            EdgeClutter(g, A("Agora"), 20, avoid);
        }

        static void BuildMarket(Transform parent, Transform fires)
        {
            var g = Group("Mercado", parent);
            // barracas encostadas nas bordas: o centro fica livre para o combate
            Prop("Barraca_Vermelha", g, -47.6f, -86f, 90f);
            Prop("Barraca_Azul", g, -47.6f, -93f, 90f);
            Prop("Barraca_Vermelha", g, -24.4f, -84f, -90f);
            Prop("Barraca_Azul", g, -38f, -79.4f, 180f);
            var burning = Prop("Barraca_Vermelha", g, -24.4f, -92f, -90f);
            Fire("Fogo_Medio_Luz", fires, burning.transform.position + Vector3.up * 1.2f);
            Prop("Carroca", g, -32f, -97.4f, 80f);
            Prop("Carroca_Feno", g, -42f, -97.4f, -95f);
            Fire("Fogo_Medio", fires, G(-42f, -97.4f, 1.3f));
            Prop("Braseiro", g, -46f, -80f, 0f);
            Prop("Braseiro", g, -26f, -80f, 0f);
            Prop("Braseiro", g, -46f, -96.5f, 0f);
            EdgeClutter(g, A("Mercado"), 12, new Vector2(-49f, -77f), new Vector2(-27f, -99f));
        }

        static void BuildGateCourt(Transform parent)
        {
            var g = Group("Portas_Ceias", parent);
            MeshObj("Portas_Ceias_Torres", g, TroiaMeshes.ScaeanGate(), Vector3.zero, Quaternion.identity, Col.Mesh);
            // folhas e tranca do portão: objetos separados, prontos para animar ("abrir o portão")
            var doors = Group("Portao (interativo)", g);
            MeshObj("Portao_Folha_Esquerda", doors, TroiaMeshes.GateLeaf(false), new Vector3(-4.5f, 0f, -133.2f), Quaternion.identity, Col.Box, false);
            MeshObj("Portao_Folha_Direita", doors, TroiaMeshes.GateLeaf(true), new Vector3(4.5f, 0f, -133.2f), Quaternion.identity, Col.Box, false);
            MeshObj("Tranca_do_Portao", doors, TroiaMeshes.GateBar(), new Vector3(0f, 4.2f, -132.6f), Quaternion.identity, Col.Box, false);

            Prop("Braseiro", g, -7f, -127.5f, 0f);
            Prop("Braseiro", g, 7f, -127.5f, 0f);
            Prop("Suporte_Armas", g, -16f, -128.5f, 0f);
            Prop("Suporte_Armas", g, 15f, -128.5f, 0f);
            Prop("Caixote", g, -17.5f, -110f, 10f);
            Prop("Caixote", g, -17.2f, -111.1f, 40f);
            Prop("Caixote", g, -17.4f, -110.5f, 25f, 1f, 0.9f);
            Prop("Carroca_Feno", g, 13f, -112f, -60f);
            Prop("Tocha_Luz", g, -12f, -129.5f, 0f);
            Prop("Tocha_Luz", g, 12f, -129.5f, 0f);
            EdgeClutter(g, A("Patio_Portas_Ceias"), 10, new Vector2(0f, -128f), new Vector2(-14f, -110f));
        }

        static void BuildAvenue(Transform parent, Transform fires)
        {
            var g = Group("Avenida", parent);
            float[] zs = { -95f, -73f, -51f, -8f, 12f };
            for (int i = 0; i < zs.Length; i++) Prop("Braseiro", g, (i % 2 == 0 ? -3.6f : 3.6f), zs[i], 0f);
            Prop("Carroca", g, 3.2f, -85f, 170f);
            Fire("Fogo_Medio", fires, G(3.2f, -85f, 1.2f));
            Prop("Escombros_Pequenos", g, -3.3f, -64f, 0f, 0.7f);
            Prop("Escombros_Pequenos", g, 3.4f, -1f, 90f, 0.6f);
            Prop("Carroca_Feno", g, -3.1f, 5f, -10f);
            Fire("Fogo_Medio_Luz", fires, G(-3.1f, 5f, 1.4f));
            for (int i = 0; i < 10; i++)
            {
                float z = Rf(-100f, 18f);
                if (z > -45f && z < -15f) continue;
                Prop(R.NextDouble() < 0.5 ? "Anfora" : "Caixote", g, (R.NextDouble() < 0.5 ? -1f : 1f) * Rf(3.4f, 4f), z, Rf(0f, 360f));
            }
        }

        static void BuildFountainSquare(Transform parent, Transform fires)
        {
            var g = Group("Praca_da_Fonte", parent);
            MeshObj("Fonte", g, TroiaMeshes.Fountain(), G(0f, -30f), Quaternion.identity, Col.Mesh);
            Prop("Estatua_Bronze", g, -13f, -20f, 135f);
            Prop("Estatua_Bronze", g, 13f, -20f, -135f);
            Prop("Estatua_Marmore", g, -13f, -40f, 45f);
            Prop("Estatua_Marmore", g, 13f, -40f, -45f);
            Prop("Braseiro", g, -8f, -30f, 0f);
            Prop("Braseiro", g, 8f, -30f, 0f);
            Prop("Banco", g, 0f, -36.5f, 0f);
            Prop("Banco", g, 0f, -23.5f, 0f);
            Prop("Carroca", g, 9.5f, -37f, 60f);
            Fire("Fogo_Medio_Luz", fires, G(9.5f, -37f, 1.2f));
            Prop("Escombros_Pequenos", g, -10f, -26f, 0f, 0.8f);
            EdgeClutter(g, A("Praca_da_Fonte"), 10, new Vector2(0f, -43f), new Vector2(0f, -17f));
        }

        static void BuildSideStreets(Transform parent, Transform fires)
        {
            var g = Group("Ruas_Laterais", parent);
            // barricada que separa a Ágora da Praça da Fonte (atalho bloqueado)
            Vector2 a = new Vector2(-34f, -38f), b = new Vector2(-15f, -32f);
            float yaw = Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg;
            var bar = Prop("Barricada", g, -25f, -35.2f, yaw + 90f, 1.1f);
            Fire("Fogo_Medio_Luz", fires, bar.transform.position + Vector3.up * 0.8f);
            // ruas sem saída: bloqueadas por escombros em chamas
            foreach (var (x, z) in new[] { (27f, -78.8f), (-25f, -2.2f), (29f, 7.8f) })
            {
                Prop("Escombros_Grandes", g, x, z, Rf(0f, 360f), 1.25f);
                Fire("Fogo_Grande_Luz", fires, G(x, z, 1.5f));
            }
        }

        // =====================================================================================
        // Pérgamo: templo de Atena, palácio de Príamo, altar de Zeus
        // =====================================================================================
        static void BuildCitadel(Transform parent, Transform fires)
        {
            float y = TroiaLayout.CitadelH;
            var t = Group("Templo_de_Atena", parent);
            MeshObj("Templo_de_Atena", t, TroiaMeshes.Temple(), new Vector3(-17f, y, 98f), Quaternion.Euler(0f, 180f, 0f), Col.Mesh);
            Prop("Altar_Pequeno", t, -17f, 81.5f, 0f);
            Prop("Braseiro", t, -21f, 84f, 0f);
            Prop("Braseiro", t, -13f, 84f, 0f);

            var p = Group("Palacio_de_Priamo", parent);
            MeshObj("Palacio_de_Priamo", p, TroiaMeshes.Palace(), new Vector3(14f, y, 103f), Quaternion.identity, Col.Mesh);
            var altar = MeshObj("Altar_de_Zeus", p, TroiaMeshes.ZeusAltar(), new Vector3(14f, y, 97f), Quaternion.identity, Col.Mesh);
            Fire("Fogo_Medio_Luz", fires, altar.transform.position + Vector3.up * 2.15f);
            MeshObj("Loureiro_Sagrado", p, TroiaMeshes.Laurel(), new Vector3(19f, y, 101f), Quaternion.Euler(0f, 40f, 0f), Col.Mesh);
            foreach (var (x, z) in new[] { (5.5f, 88.5f), (22.5f, 88.5f), (8f, 105.5f), (20f, 105.5f) })
                Prop("Braseiro", p, x, z, 0f);
            Fire("Fogo_Pequeno_Luz", fires, new Vector3(14f, y + 0.3f, 114.5f)); // lareira do mégaron
            Prop("Suporte_Armas", p, 4.5f, 95f, 90f);

            var plaza = Group("Patio_da_Cidadela", parent);
            Prop("Estatua_Bronze", plaza, -6.5f, 61.5f, 180f);
            Prop("Estatua_Bronze", plaza, 6.5f, 61.5f, 180f);
            Prop("Braseiro", plaza, -18f, 63f, 0f);
            Prop("Braseiro", plaza, 20f, 63f, 0f);
            Prop("Suporte_Armas", plaza, 20.5f, 74f, -90f);
            Prop("Carroca", plaza, 15f, 66f, 120f);
            Fire("Fogo_Medio_Luz", fires, G(15f, 66f, 1.2f));
            Prop("Escombros_Grandes", plaza, -7.8f, 90.5f, 20f, 1f); // fecha o vão entre o templo e a ala oeste
            EdgeClutter(plaza, A("Patio_Cidadela"), 8, new Vector2(0f, 58f), new Vector2(-17f, 84f), new Vector2(14f, 86f));
        }

        // =====================================================================================
        // Casas
        // =====================================================================================
        static void PlaceHouses(Transform lowerParent, Transform citadelParent, Transform fires)
        {
            foreach (var a in TroiaLayout.Walk)
            {
                if (!a.Frontage) continue;
                foreach (var e in a.Edges())
                    PlaceAlong(e.a, e.b, e.outward, a, a.Citadel ? citadelParent : lowerParent, fires);
            }
            Fill(false, lowerParent, fires);
            Fill(true, citadelParent, fires);
        }

        static void PlaceAlong(Vector2 a, Vector2 b, Vector2 outward, Area area, Transform parent, Transform fires)
        {
            float L = Vector2.Distance(a, b);
            Vector2 dir = (b - a) / L;
            float s = -1.5f;
            while (s < L + 1.5f)
            {
                float sx = Rf(0.82f, 1.05f), sz = Rf(0.92f, 1.1f);
                float w = 8f * sx, d = 8f * sz;
                Vector2 c = a + dir * (s + w * 0.5f) + outward * (d * 0.5f + 0.4f);
                float yaw = Mathf.Atan2(-outward.x, -outward.y) * Mathf.Rad2Deg;
                if (Valid(c, yaw, w, d, area.Citadel, 4.6f))
                    SpawnHouse(area, area.Citadel, true, c, yaw, sx, sz, parent, fires);
                s += w;
            }
        }

        static void Fill(bool citadel, Transform parent, Transform fires)
        {
            float x0, x1, z0, z1;
            if (citadel)
            {
                x0 = -40f; x1 = 40f; z0 = 50f; z1 = 130f;
            }
            else
            {
                x0 = -TroiaLayout.Rx; x1 = TroiaLayout.Rx; z0 = -TroiaLayout.Rz; z1 = TroiaLayout.Rz;
            }
            var offsets = new[] { Vector2.zero, new Vector2(4.05f, 4.05f), new Vector2(2f, 6f), new Vector2(6f, 2f) };
            foreach (var off in offsets)
            {
                for (float z = z0 + off.y; z <= z1; z += 8.1f)
                {
                    for (float x = x0 + off.x; x <= x1; x += 8.1f)
                    {
                        var c = new Vector2(x + Rf(-0.7f, 0.7f), z + Rf(-0.7f, 0.7f));
                        float yaw = R.Next(4) * 90f + Rf(-4f, 4f);
                        float sx = Rf(0.92f, 1.05f), sz = Rf(0.92f, 1.05f);
                        if (Valid(c, yaw, 8f * sx, 8f * sz, citadel, 5.4f))
                            SpawnHouse(null, citadel, false, c, yaw, sx, sz, parent, fires);
                    }
                }
            }
        }

        static bool PointOk(Vector2 p, bool citadel)
        {
            foreach (var a in TroiaLayout.Walk) if (a.Sdf(p) < 0.2f) return false;
            foreach (var a in TroiaLayout.Reserved) if (a.Sdf(p) < 0f) return false;
            float dc = Vector2.Distance(p, TroiaLayout.CitadelC);
            if (citadel) return dc <= 36.6f;
            return dc >= 42.6f && TroiaLayout.Ellipse(p.x, p.y) <= 0.962f;
        }

        static bool Valid(Vector2 c, float yaw, float w, float d, bool citadel, float minDist)
        {
            if (!PointOk(c, citadel)) return false;
            float md2 = minDist * minDist;
            foreach (var h in _houses) if ((h - c).sqrMagnitude < md2) return false;
            float rad = yaw * Mathf.Deg2Rad, cs = Mathf.Cos(rad), sn = Mathf.Sin(rad);
            for (int i = 0; i <= 4; i++)
            {
                for (int j = 0; j <= 4; j++)
                {
                    float lx = (i / 4f - 0.5f) * (w - 0.3f), lz = (j / 4f - 0.5f) * (d - 0.3f);
                    if (!PointOk(new Vector2(c.x + lx * cs + lz * sn, c.y - lx * sn + lz * cs), citadel)) return false;
                }
            }
            return true;
        }

        static HouseType PickType(Area area, bool citadel, bool frontage)
        {
            if (!citadel && frontage && area != null && R.NextDouble() < area.FireChance * 0.6f) return HouseType.Queimada;
            if (frontage && area != null && area.Shops && R.NextDouble() < 0.3) return HouseType.Loja;
            double x = R.NextDouble();
            if (citadel) return x < 0.45 ? HouseType.Alta : x < 0.85 ? HouseType.Sobrado : HouseType.Terrea;
            return x < 0.36 ? HouseType.Terrea : x < 0.52 ? HouseType.Escada : x < 0.8 ? HouseType.Sobrado : HouseType.Alta;
        }

        static void SpawnHouse(Area area, bool citadel, bool frontage, Vector2 c, float yaw, float sx, float sz, Transform parent, Transform fires)
        {
            HouseType t = PickType(area, citadel, frontage);
            var options = TroiaAssets.Houses.FindAll(h => h.Type == t);
            var hp = options[R.Next(options.Count)];
            float sy = t == HouseType.Alta ? Rf(0.95f, 1.08f) : Rf(0.9f, 1.18f);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(hp.Prefab, parent);
            go.transform.SetPositionAndRotation(new Vector3(c.x, TroiaLayout.Height(c.x, c.y), c.y), Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = new Vector3(sx, sy, sz);
            _houses.Add(c);
            Vector3 front = go.transform.forward;
            if (frontage && R.NextDouble() < 0.18)
                DecalWall("Rachadura", go.transform.TransformPoint(new Vector3(Rf(-2.5f, 2.5f), Rf(1.2f, 2.6f), 4f)), front, Rf(1.8f, 3f), Rf(1.8f, 3f));

            float chance = frontage && area != null ? area.FireChance : 0.035f;
            if (t != HouseType.Queimada && R.NextDouble() >= chance) return;
            Vector3 fp = go.transform.TransformPoint(hp.FireLocal);
            bool near = TroiaLayout.DistToRoute(c) < 30f && _houseLights < MaxHouseLights;
            string fire;
            if (t == HouseType.Queimada) fire = near ? "Fogo_Medio_Luz" : "Fogo_Medio";
            else if (near) fire = "Fogo_Grande_Luz";
            else fire = frontage ? "Fogo_Grande" : "Fogo_Distante";
            if (fire.EndsWith("_Luz")) _houseLights++;
            Fire(fire, fires, fp);
            if (t == HouseType.Queimada) DecalGround("Cinzas", c.x, c.y, 6.5f);
            else if (frontage)
            {
                float hh = hp.FireLocal.y * sy;
                DecalWall("Fuligem", go.transform.TransformPoint(new Vector3(Rf(-1f, 1f), 0f, 4f)) + Vector3.up * hh * 0.55f, front, 6f * sx, hh * 0.95f);
                DecalWall("Fuligem", go.transform.TransformPoint(new Vector3(Rf(-1f, 1f), 0f, -4f)) + Vector3.up * hh * 0.55f, -front, 6f * sx, hh * 0.95f);
            }
        }

        // =====================================================================================
        // Exterior: exército grego voltando de Tênedos, praia e navios
        // =====================================================================================
        static void BuildExterior(Transform parent, Transform fires)
        {
            var pos = new List<Vector3>();
            var yaws = new List<float>();
            for (float z = -150f; z > -236f; z -= 2.6f)
            {
                for (int col = -2; col <= 2; col++)
                {
                    if (R.NextDouble() < 0.25) continue;
                    float zz = z + Rf(-0.6f, 0.6f);
                    float x = TroiaLayout.RoadCenterX(zz) + col * 1.3f + Rf(-0.3f, 0.3f);
                    pos.Add(new Vector3(x, TroiaLayout.Height(x, zz), zz));
                    yaws.Add(Rf(-12f, 12f));
                }
            }
            var army = TroiaMeshes.Army(pos, yaws, 4, out var tips);
            MeshObj("Exercito_Grego", Group("Exercito_Grego", parent), army, Vector3.zero, Quaternion.identity, Col.None);
            var torches = Group("Tochas_do_Exercito", fires);
            for (int i = 0; i < tips.Count; i++) Fire(i % 4 == 0 ? "Fogo_Tocha_Luz" : "Fogo_Tocha", torches, tips[i]);

            var ships = Group("Frota_Grega", parent);
            for (int i = 0; i < 9; i++)
            {
                float x = -180f + i * 45f + Rf(-10f, 10f);
                float z = -250f;
                while (z > -345f && TroiaLayout.Height(x, z) > TroiaLayout.SeaLevel - 0.4f) z -= 1f;
                var s = (GameObject)PrefabUtility.InstantiatePrefab(TroiaAssets.Props["Navio_Grego"], ships);
                s.transform.SetPositionAndRotation(new Vector3(x, TroiaLayout.SeaLevel - 0.35f, z - Rf(0f, 10f)), Quaternion.Euler(0f, 180f + Rf(-20f, 20f), 0f));
                Fire(i % 3 == 0 ? "Fogo_Tocha_Luz" : "Fogo_Tocha", fires, s.transform.TransformPoint(new Vector3(0f, 3.8f, -8.6f)));
            }
            MeshObj("Mar_Egeu", parent, TroiaMeshes.Sea(TroiaLayout.SeaLevel), Vector3.zero, Quaternion.identity, Col.None);
        }

        static void BuildAtmosphere(Transform parent)
        {
            var go = new GameObject("Cinzas_e_Brasas_no_Ar");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 20f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.2f, 1f), new Color(0.45f, 0.42f, 0.4f, 0.7f));
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.015f;
            var em = ps.emission;
            em.rateOverTime = 300f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            sh.scale = new Vector3(220f, 280f, 36f);
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(0.6f);
            vol.y = new ParticleSystem.MinMaxCurve(-0.1f);
            vol.z = new ParticleSystem.MinMaxCurve(0.3f);
            var no = ps.noise;
            no.enabled = true;
            no.strength = 0.6f;
            no.frequency = 0.15f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = TroiaAssets.EmberMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // =====================================================================================
        // Gameplay: arenas, inimigos, objetivos, checkpoints, NavMesh e jogador de teste
        // =====================================================================================
        static LevelMarker Marker(Transform parent, MarkerKind kind, string label, Vector3 pos, int order, Vector3 size = default, string notes = "", float yaw = 0f, int arena = 0)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var m = go.AddComponent<LevelMarker>();
            m.kind = kind;
            m.label = label;
            m.order = order;
            m.notes = notes;
            m.arena = arena;
            if (size != default) m.size = size;
            return m;
        }

        static readonly (string name, int arena, bool entrance, float x, float z, float yaw, float width)[] Barriers =
        {
            ("Barreira 1 - Saida da Agora", 1, false, -54.6f, -64.5f, 160f, 8f),
            ("Barreira 2 - Entrada do Mercado", 2, true, -50.6f, -74.5f, 160f, 8f),
            ("Barreira 2 - Saida do Mercado", 2, false, -25.8f, -100.2f, 135f, 9f),
            ("Barreira 3 - Entrada do Patio do Portao", 3, true, -20.3f, -105.3f, 127f, 8.5f),
            ("Barreira 3 - Saida para a Avenida", 3, false, 0f, -101.8f, 0f, 11f),
            ("Barreira 3 - Portas Ceias (exterior)", 3, false, 0f, -139f, 180f, 10f),
            ("Barreira 4 - Entrada da Praca da Fonte", 4, true, 0f, -44.5f, 0f, 11f),
            ("Barreira 4 - Saida da Praca da Fonte", 4, false, 0f, -15.5f, 0f, 11f),
            ("Barreira 5 - Portao da Cidadela", 5, true, 0f, 58.5f, 0f, 12f),
            ("Barreira 5 - Propileu do Palacio", 5, false, 14f, 86.5f, 0f, 10.5f),
            ("Barreira 6 - Entrada do Patio do Palacio", 6, true, 14f, 89.5f, 0f, 10.5f),
            ("Barreira 6 - Porta do Megaron", 6, false, 14f, 110.6f, 0f, 5f),
        };

        /// <summary>Braseiros ao longo do perímetro das arenas: luz para ler os inimigos durante as ondas.</summary>
        static void ArenaBraziers(Transform parent)
        {
            var openings = new List<Vector2> { new Vector2(-33f, -36f), new Vector2(0f, -131f), new Vector2(-17f, 86f) };
            foreach (var b in Barriers) openings.Add(new Vector2(b.x, b.z));
            foreach (var n in new[] { "Agora", "Mercado", "Patio_Portas_Ceias", "Praca_da_Fonte", "Patio_Cidadela" })
            {
                var a = A(n);
                foreach (var e in a.Edges())
                {
                    float L = Vector2.Distance(e.a, e.b);
                    int k = Mathf.Max(1, Mathf.RoundToInt(L / 14f));
                    for (int i = 0; i < k; i++)
                    {
                        Vector2 p = Vector2.Lerp(e.a, e.b, (i + 0.5f) / k) - e.outward * 1.6f;
                        bool bad = false;
                        foreach (var o in openings) if ((o - p).sqrMagnitude < 7f * 7f) bad = true;
                        foreach (var o in _props) if ((o - p).sqrMagnitude < 3.5f * 3.5f) bad = true;
                        if (!bad) Prop("Braseiro", parent, p.x, p.y, 0f);
                    }
                }
            }
        }

        static void Barrier(Transform parent, string name, int arena, bool entrance, float x, float z, float yaw, float width)
        {
            var m = Marker(parent, MarkerKind.Barrier, name, G(x, z), arena, new Vector3(width, 4f, 1.5f),
                entrance ? "Fecha atrás do jogador quando a arena começa." : "Abre quando todas as ondas da arena são derrotadas.",
                yaw, arena);
            m.isEntrance = entrance;
            var bc = m.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 2f, 0f);
            bc.size = new Vector3(width, 4f, 1.5f);
            bc.enabled = false; // o sistema de ondas liga/desliga
            var wall = TroiaAssets.CreateFireObject("Muralha_de_Fogo", FireSize.Grande, true, false, true);
            wall.transform.SetParent(m.transform, false);
            var ps = wall.transform.Find("Chamas").GetComponent<ParticleSystem>();
            var sh = ps.shape;
            sh.scale = new Vector3(width, 0.6f, 0.2f);
            var em = ps.emission;
            em.rateOverTime = 30f * width;
            var main = ps.main;
            main.maxParticles = 1200;
            wall.SetActive(false); // visual da barreira (ativado junto com o colisor)
        }

        static void BuildGameplayMarkers(Transform parent)
        {
            Marker(parent, MarkerKind.PlayerSpawn, "Inicio_Jogador (desce do Cavalo)", G(-50.5f, -39.5f), 0, default,
                "O herói desce pela escada de corda do Cavalo de Troia, já dentro da Arena 1.", 190f);

            var arenas = Group("Arenas", parent);
            var spawns = Group("Spawns_Inimigos", parent);
            var barriers = Group("Barreiras", parent);
            var defs = new (string name, Vector2 c, Vector2 s, float h, int waves, string notes)[]
            {
                ("Arena 1 - Agora (O Cavalo)", new Vector2(-55, -45), new Vector2(44, 36), 6f, 3,
                    "Ondas fáceis (guardas bêbados do banquete, sentinelas). Tutorial. O Cavalo no centro serve de obstáculo/cobertura."),
                ("Arena 2 - Mercado", new Vector2(-36, -88), new Vector2(28, 22), 6f, 3,
                    "Milícia troiana desperta. Arena menor e apertada: inimigos em grupo."),
                ("Arena 3 - Portas Ceias", new Vector2(0, -117), new Vector2(40, 28), 8f, 4,
                    "Guarda do portão; última onda com arqueiros nas torres. Ao limpar: a tranca cai e o portão abre para o exército grego (evento)."),
                ("Arena 4 - Praca da Fonte", new Vector2(0, -30), new Vector2(32, 26), 6f, 4,
                    "Contra-ataque troiano. A fonte no centro divide a arena. Inimigos de elite começam a aparecer."),
                ("Arena 5 - Patio da Cidadela", new Vector2(1, 73), new Vector2(42, 26), 6f, 5,
                    "Guarda real diante do Templo de Atena. Maior arena da fase, ondas mistas."),
                ("Arena 6 - Altar de Zeus (Chefe)", new Vector2(14, 97), new Vector2(22, 20), 8f, 1,
                    "Chefe no pátio do Palácio de Príamo, ao redor do altar e do loureiro sagrado (+ reforços)."),
            };
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                var am = Marker(arenas, MarkerKind.CombatArena, d.name, G(d.c.x, d.c.y), i + 1, new Vector3(d.s.x, d.h, d.s.y), d.notes);
                am.waves = d.waves;
                var g = Group($"Spawns_Arena_{i + 1}", spawns);
                float hx = d.s.x * 0.5f, hz = d.s.y * 0.5f;
                var pts = i == 5
                    ? new[] { new Vector2(0f, 7f), new Vector2(-hx + 3f, -hz + 4f), new Vector2(hx - 3f, -hz + 4f), new Vector2(-hx + 3f, hz - 3f), new Vector2(hx - 3f, hz - 3f) }
                    : new[]
                    {
                        new Vector2(-hx + 3f, -hz + 3f), new Vector2(hx - 3f, -hz + 3f), new Vector2(-hx + 3f, hz - 3f), new Vector2(hx - 3f, hz - 3f),
                        new Vector2(-hx + 2f, 0f), new Vector2(hx - 2f, 0f), new Vector2(0f, -hz + 2f), new Vector2(0f, hz - 2f)
                    };
                for (int k = 0; k < pts.Length; k++)
                {
                    Vector2 p = d.c + pts[k];
                    Vector2 toC = d.c - p;
                    string label = i == 5 && k == 0 ? "Chefe" : $"Inimigo_{i + 1}_{k + 1}";
                    Marker(g, MarkerKind.EnemySpawn, label, G(p.x, p.y), k, default, "", Mathf.Atan2(toC.x, toC.y) * Mathf.Rad2Deg, i + 1);
                }
            }

            // barreiras de fogo: trancam a arena enquanto houver ondas
            foreach (var b in Barriers) Barrier(barriers, b.name, b.arena, b.entrance, b.x, b.z, b.yaw, b.width);

            var obj = Group("Objetivos", parent);
            Marker(obj, MarkerKind.Objective, "Evento - Abrir as Portas Ceias", new Vector3(0f, 0f, -131f), 3, default,
                "Ao vencer a Arena 3: remover a Tranca_do_Portao e girar as folhas do portão (~100°) para dentro da cidade.");
            Marker(obj, MarkerKind.Objective, "Fim da Fase - Megaron de Priamo", new Vector3(14f, TroiaLayout.CitadelH, 114f), 7, default,
                "Entrar no salão do trono depois de derrotar o chefe.");

            var cp = Group("Checkpoints", parent);
            var cps = new (string n, float x, float z, float yaw)[]
            {
                ("Checkpoint 1 - Beco", -52.5f, -69f, 160f), ("Checkpoint 2 - Beco do Portao", -22.5f, -103f, 127f),
                ("Checkpoint 3 - Avenida", 0f, -82f, 0f), ("Checkpoint 4 - Pe da Rampa", 0f, 14f, 0f),
                ("Checkpoint 5 - Topo da Rampa", 0f, 52f, 0f), ("Checkpoint 6 - Propileu", 14f, 84.5f, 0f),
            };
            for (int i = 0; i < cps.Length; i++) Marker(cp, MarkerKind.Checkpoint, cps[i].n, G(cps[i].x, cps[i].z), i + 1, default, "", cps[i].yaw);
        }

        static void BakeNavMesh(Transform parent)
        {
            var go = new GameObject("NavMesh_Superficie");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<NavMeshSurface>();
            s.collectObjects = CollectObjects.Volume;
            s.center = new Vector3(0f, 20f, 0f);
            s.size = new Vector3(230f, 70f, 290f);
            s.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            Physics.SyncTransforms();
            s.BuildNavMesh();
            string path = $"{TroiaAssets.ConfigDir}/NavMesh_Fase1.asset";
            AssetDatabase.DeleteAsset(path);
            if (s.navMeshData != null) AssetDatabase.CreateAsset(s.navMeshData, path);
        }

        static void CreateTestPlayer(Transform parent, Vector3 pos, float yaw)
        {
            var p = new GameObject("Jogador_Teste (temporario)") { layer = 2 };
            p.transform.SetParent(parent, false);
            p.transform.SetPositionAndRotation(pos + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            var cc = p.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Corpo";
            body.layer = 2;
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());
            body.transform.SetParent(p.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            body.GetComponent<MeshRenderer>().sharedMaterial = TroiaAssets.Pal[(int)M.Bronze];
            var crest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crest.name = "Crista_do_Elmo";
            crest.layer = 2;
            Object.DestroyImmediate(crest.GetComponent<BoxCollider>());
            crest.transform.SetParent(p.transform, false);
            crest.transform.localPosition = new Vector3(0f, 1.9f, -0.05f);
            crest.transform.localScale = new Vector3(0.08f, 0.3f, 0.6f);
            crest.GetComponent<MeshRenderer>().sharedMaterial = TroiaAssets.Pal[(int)M.Cloth];

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(parent, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.15f;
            cam.farClipPlane = 1500f;
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.position = pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 3f, -6f);
            camGo.transform.LookAt(pos + Vector3.up * 1.7f);

            var w = p.AddComponent<MapPreviewWalker>();
            w.cameraTransform = camGo.transform;
        }
    }
}
