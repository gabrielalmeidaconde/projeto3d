using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>Liga o Pátroclo ao jogo e gera pranchas com as poses dos golpes (para conferir a animação sem jogar).</summary>
    public static class PatrocloJogo
    {
        public const string Prefab = "Assets/Troia/Personagens/Patroclo/Patroclo.prefab";
        const string Ref = "Assets/Troia/Resources/Troia/Heroi.asset";

        [MenuItem("Troia/Personagem/Usar Pátroclo como herói do jogo", priority = 45)]
        public static void Vincular()
        {
            var so = AssetDatabase.LoadAssetAtPath<HeroiSO>(Ref);
            if (!so) { so = ScriptableObject.CreateInstance<HeroiSO>(); AssetDatabase.CreateAsset(so, Ref); }
            so.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            EditorUtility.SetDirty(so);
            // arsenal antigo (foice e machado) sai; o novo é gravado pela configuração de gameplay
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Troia/Resources/Troia/Combos", "Assets/Troia/Resources/Troia/Armas" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid), nome = Path.GetFileName(path);
                if (nome.StartsWith("Foice") || nome.StartsWith("Machado")) AssetDatabase.DeleteAsset(path);
            }
            Troia.Editor.GameplaySetup.EnsureAssets();
            AssetDatabase.SaveAssets();
            Debug.Log("Pátroclo ligado ao jogo: " + (so.prefab ? so.prefab.name : "prefab não encontrado"));
        }

        // ------------------------------------------------------------------ pranchas
        const int Celula = 400;

        /// <summary>Unity -executeMethod Troia.EditorTools.PatrocloJogo.Pranchas -- [pasta] [vista: frente|lado|tras]</summary>
        public static void Pranchas()
        {
            var args = System.Environment.GetCommandLineArgs();
            int sep = System.Array.IndexOf(args, "--");
            string pasta = sep >= 0 && sep + 1 < args.Length ? args[sep + 1] : "Capturas/Golpes";
            string vista = sep >= 0 && sep + 2 < args.Length ? args[sep + 2] : "frente";
            string filtro = sep >= 0 && sep + 3 < args.Length ? args[sep + 3] : "";
            Directory.CreateDirectory(pasta);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.42f, .42f, .46f);
            var sol = new GameObject("Sol").AddComponent<Light>(); sol.type = LightType.Directional; sol.intensity = 1.5f;
            sol.transform.rotation = Quaternion.Euler(42, vista == "tras" ? 20 : 205, 0); sol.shadows = LightShadows.Soft;
            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane); chao.transform.localScale = Vector3.one * 3;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.32f, .32f, .33f) }; chao.GetComponent<Renderer>().sharedMaterial = mat;
            var cam = new GameObject("Camera").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.2f, .21f, .23f);
            cam.fieldOfView = 30;
            Vector3 olho = vista == "lado" ? new Vector3(4.6f, 1.4f, .3f) : vista == "tras" ? new Vector3(-2f, 1.9f, -4.2f) : new Vector3(2.1f, 1.6f, 4.1f);
            cam.transform.position = olho; cam.transform.LookAt(new Vector3(0, 1.0f, .25f));

            var heroi = new GameObject("Heroi");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), heroi.transform);
            var design = go.GetComponent<PatrocloDesign>(); design.escudo = PosicaoEscudo.Braco; design.Apply();
            var anim = go.AddComponent<PatrocloAnimador>(); anim.Preparar(design);
            PatrocloArmas.Madeira = design.refs.madeira; PatrocloArmas.Bronze = design.refs.lamina;
            var lanca = PatrocloArmas.Lanca("Lanca", 2.3f, .95f, .017f, .36f).transform;
            lanca.SetParent(anim.Rig.pulsoD, false); lanca.localPosition = anim.Rig.espadaPos; lanca.localRotation = anim.Rig.espadaLocal;

            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
            // várias poses no mesmo quadro do editor: a pele precisa ser recalculada a cada render
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
            var config = GameplayDefaults.Create();
            var rt = new RenderTexture(Celula, Celula, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render(); cam.Render();

            foreach (var arma in config.weapons)
            {
                if (filtro != "" && filtro != "base" && !arma.id.ToLower().Contains(filtro.ToLower())) continue;
                if (filtro == "base") break;
                anim.Rig.espada.gameObject.SetActive(arma.kind != WeaponKind.Spear); lanca.gameObject.SetActive(arma.kind == WeaponKind.Spear);
                var linhas = new List<(string, ComboSO, int, bool)>();
                int passo = 1;
                for (var m = arma.light; m; m = m.next) linhas.Add((m.displayName, m, passo++, true));
                var l2 = arma.light.next;
                if (l2 && l2.delayedNext) { linhas.Add((l2.delayedNext.displayName, l2.delayedNext, 3, true)); if (l2.delayedNext.next) linhas.Add((l2.delayedNext.next.displayName, l2.delayedNext.next, 4, true)); }
                linhas.Add((arma.heavy.displayName, arma.heavy, 1, true));
                if (arma.heavy.heavyNext) linhas.Add((arma.heavy.heavyNext.displayName, arma.heavy.heavyNext, 2, true));
                linhas.Add((arma.launcher.displayName, arma.launcher, 1, true));
                linhas.Add((arma.dash.displayName, arma.dash, 1, true));
                linhas.Add((arma.airLight.displayName, arma.airLight, 1, false));
                linhas.Add((arma.slam.displayName, arma.slam, 1, false));
                const int colunas = 8;
                var folha = new Texture2D(Celula * colunas, Celula * linhas.Count, TextureFormat.RGB24, false);
                for (int r = 0; r < linhas.Count; r++)
                {
                    var (nome, m, prof, chao2) = linhas[r];
                    float[] tempos = { 0, m.hitStart * .5f, Mathf.Lerp(m.hitStart * .5f, m.hitStart, .5f), m.hitStart, Mathf.Lerp(m.hitStart, m.hitEnd, .5f), m.hitEnd, Mathf.Lerp(m.hitEnd, m.duration, .4f), m.duration * .97f };
                    var e = new PatrocloAnimador.Estado { noChao = chao2, estado = PlayerState.Attacking, arma = arma.kind, emCombate = true, profundidade = prof, direcaoLocal = Vector3.forward };
                    // parte da guarda e avança em passos de 1/120 s, como no jogo
                    var semGolpe = e; semGolpe.estado = PlayerState.Locomotion;
                    for (int i = 0; i < 40; i++) anim.Avaliar(semGolpe, 1 / 60f);
                    float t = 0; int col = 0;
                    while (col < colunas)
                    {
                        e.golpe = m; e.tempoGolpe = t; anim.Avaliar(e, 1 / 120f);
                        if (t >= tempos[col] - 1e-4f) { Celular(cam, rt, folha, col, linhas.Count - 1 - r); col++; }
                        t += 1 / 120f;
                    }
                    Debug.Log($"prancha {arma.id}: linha {r} = {nome} ({m.motion}, {m.duration:0.00}s, acerta em {m.hitStart:0.00}-{m.hitEnd:0.00})");
                }
                File.WriteAllBytes($"{pasta}/{arma.id}_{vista}.png", folha.EncodeToPNG());
                Object.DestroyImmediate(folha);
            }

            if (filtro == "" || filtro == "base")
            {
                // locomoção e estados
                anim.Rig.espada.gameObject.SetActive(true); lanca.gameObject.SetActive(false);
                var estados = new List<(string, PatrocloAnimador.Estado, float)>();
                var b = new PatrocloAnimador.Estado { noChao = true, estado = PlayerState.Locomotion, arma = WeaponKind.Sword, direcaoLocal = Vector3.forward };
                estados.Add(("parado", b, 1));
                var c = b; c.emCombate = true; estados.Add(("guarda", c, 1));
                for (int i = 0; i < 4; i++) { var w = b; w.velocidade = 5.5f; estados.Add(("corre" + i, w, .5f + .095f * i)); }
                for (int i = 0; i < 4; i++) { var w = b; w.velocidade = 8.5f; w.emCombate = true; estados.Add(("dispara" + i, w, .5f + .073f * i)); }
                var ar = b; ar.noChao = false; ar.velY = 7; estados.Add(("sobe", ar, .3f));
                ar.velY = -8; estados.Add(("cai", ar, .3f));
                var d = c; d.estado = PlayerState.Dodging; d.tempoEstado = .14f; d.esquivaLocal = Vector3.forward; estados.Add(("esquiva", d, .14f));
                d.esquivaLocal = Vector3.left; estados.Add(("esquiva_lado", d, .14f));
                var inv = c; inv.estado = PlayerState.Rushing; inv.tempoEstado = .1f; estados.Add(("investida", inv, .2f));
                var st = c; st.estado = PlayerState.Staggered; st.tempoEstado = .1f; estados.Add(("atordoado", st, .1f));
                var th = c; th.arremesso = .75f; estados.Add(("dardo", th, .08f));
                var mo = c; mo.estado = PlayerState.Dead; mo.tempoEstado = 1; estados.Add(("morto", mo, .5f));
                const int colunas = 8;
                int nl = Mathf.CeilToInt(estados.Count / (float)colunas);
                var folha = new Texture2D(Celula * colunas, Celula * nl, TextureFormat.RGB24, false);
                for (int i = 0; i < estados.Count; i++)
                {
                    var (nome, e, tempo) = estados[i];
                    var neutro = b; for (int k = 0; k < 30; k++) anim.Avaliar(neutro, 1 / 60f);
                    bool ciclo = nome.StartsWith("corre") || nome.StartsWith("dispara");
                    int passos = Mathf.Max(1, Mathf.RoundToInt(tempo * 120));
                    if (!ciclo) for (int k = 0; k < 60; k++) anim.Avaliar(e, 1 / 60f);
                    else for (int k = 0; k < passos + 40; k++) anim.Avaliar(e, 1 / 120f);
                    Celular(cam, rt, folha, i % colunas, nl - 1 - i / colunas);
                }
                File.WriteAllBytes($"{pasta}/base_{vista}.png", folha.EncodeToPNG());
            }
            cam.targetTexture = null; rt.Release();
        }
        public static void Diagnostico()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            var design = go.GetComponent<PatrocloDesign>(); design.pose = PosePatroclo.Repouso; design.Apply();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("skinWeights qualidade: " + QualitySettings.skinWeights);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.name.StartsWith("Capa") && !smr.name.StartsWith("Rabo") && smr.name != "Crista" && smr.name != "Quiton" && smr.name != "Pteruges") continue;
                var m = smr.sharedMesh; var v = m.vertices; var uv = m.uv; var uv2 = m.uv2; var bw = m.boneWeights;
                sb.AppendLine($"== {smr.name} ativo={smr.gameObject.activeSelf} verts {v.Length} tris {m.triangles.Length / 3} submalhas {m.subMeshCount} mats {string.Join(",", System.Array.ConvertAll(smr.sharedMaterials, x => x ? x.name : "-"))}");
                sb.AppendLine($"   bounds {m.bounds.min.ToString("F2")} {m.bounds.max.ToString("F2")} smrLocal {smr.transform.localPosition} pai {smr.transform.parent.name} rootBone {(smr.rootBone ? smr.rootBone.name : "-")} bones {smr.bones.Length} legivel {m.isReadable}");
                if (uv.Length == v.Length && v.Length > 0)
                {
                    float u0 = 9, u1 = -9, v0 = 9, v1 = -9; double syv = 0, sxu = 0, sy = 0, sv = 0, sx = 0, su = 0, sz = 0;
                    for (int i = 0; i < v.Length; i++) { u0 = Mathf.Min(u0, uv[i].x); u1 = Mathf.Max(u1, uv[i].x); v0 = Mathf.Min(v0, uv[i].y); v1 = Mathf.Max(v1, uv[i].y); }
                    sb.AppendLine($"   uv0 u {u0:0.00}..{u1:0.00} v {v0:0.00}..{v1:0.00}");
                    // amostra: posição média por faixa de v e de u
                    for (int f = 0; f < 5; f++)
                    {
                        Vector3 soma = Vector3.zero; int n = 0; float a = Mathf.Lerp(v0, v1, f / 5f), b = Mathf.Lerp(v0, v1, (f + 1) / 5f);
                        for (int i = 0; i < v.Length; i++) if (uv[i].y >= a && uv[i].y <= b) { soma += v[i]; n++; }
                        sb.AppendLine($"   v {a:0.00}-{b:0.00}: n {n} centro {(n > 0 ? (soma / n).ToString("F2") : "-")}");
                    }
                    for (int f = 0; f < 3; f++)
                    {
                        Vector3 soma = Vector3.zero; int n = 0; float a = Mathf.Lerp(u0, u1, f / 3f), b = Mathf.Lerp(u0, u1, (f + 1) / 3f);
                        for (int i = 0; i < v.Length; i++) if (uv[i].x >= a && uv[i].x <= b) { soma += v[i]; n++; }
                        sb.AppendLine($"   u {a:0.00}-{b:0.00}: n {n} centro {(n > 0 ? (soma / n).ToString("F2") : "-")}");
                    }
                }
                var conta = new Dictionary<string, float>();
                for (int i = 0; i < bw.Length; i++)
                {
                    void S(int idx, float w) { if (w <= 0) return; string nome = smr.bones[idx].name; conta.TryGetValue(nome, out float x); conta[nome] = x + w; }
                    S(bw[i].boneIndex0, bw[i].weight0); S(bw[i].boneIndex1, bw[i].weight1); S(bw[i].boneIndex2, bw[i].weight2); S(bw[i].boneIndex3, bw[i].weight3);
                }
                var lista = new List<KeyValuePair<string, float>>(conta); lista.Sort((x, y) => y.Value.CompareTo(x.Value));
                var partes = new List<string>(); foreach (var kv in lista) if (partes.Count < 12) partes.Add($"{kv.Key} {kv.Value / Mathf.Max(1, bw.Length):0.000}");
                sb.AppendLine("   pesos: " + string.Join(", ", partes));
            }
            File.WriteAllText("Logs/patroclo_diag.txt", sb.ToString());
            Object.DestroyImmediate(go);
        }
        public static PanoDinamico.Config ConfigCapa() => PanoDinamico.ParaCapa();
        public static PanoDinamico.Config ConfigRabo() => PanoDinamico.ParaRabo();

        public static void DiagnosticoPano()
        {
            var est = new PatrocloEstudio("lado", 64);
            var anim = est.modelo.AddComponent<PatrocloAnimador>(); anim.Preparar(est.design);
            var esq = new Dictionary<string, Transform>(); foreach (var b in est.design.refs.ossos) if (b) esq[b.name] = b;
            var capa = PanoDinamico.Criar(est.design.refs.capa.GetComponent<SkinnedMeshRenderer>(), esq, ConfigCapa());
            const float dt = 1 / 60f;
            var e = new PatrocloAnimador.Estado { noChao = true, estado = PlayerState.Locomotion, arma = WeaponKind.Sword, direcaoLocal = Vector3.forward };
            var sb = new System.Text.StringBuilder();
            var c = capa.Ajustes; float vento0 = c.vento, lateral0 = c.rigidezLateral;
            foreach (string variante in new[] { "tudo", "sem vento", "sem colisoes", "sem lateral", "sem nada" })
            {
                c.vento = variante == "sem vento" || variante == "sem nada" ? 0 : vento0;
                c.colisoes = !(variante == "sem colisoes" || variante == "sem nada");
                c.rigidezLateral = variante == "sem lateral" || variante == "sem nada" ? 0 : lateral0;
                foreach (bool correndo in new[] { false, true })
                {
                    est.heroi.transform.position = Vector3.zero; e.velocidade = 0; anim.Avaliar(e, dt); capa.Reiniciar();
                    float pico = 0, v = 0;
                    for (int i = 0; i < 150; i++)
                    {
                        if (correndo) { v = Mathf.Min(5.5f, v + 95 * dt); est.heroi.transform.position += Vector3.forward * v * dt; }
                        anim.Avaliar(e, dt); capa.Passo(dt);
                        if (i >= 110) pico = Mathf.Max(pico, capa.Agitacao(dt));
                    }
                    sb.AppendLine($"== {variante} / {(correndo ? "andando 5,5 m/s (corpo rigido)" : "parado")}: agitacao no fim {pico:0.00} m/s");
                    sb.Append(capa.Relatorio());
                }
            }
            File.WriteAllText("Logs/pano_diag.txt", sb.ToString());
            est.Fechar();
        }

        /// <summary>
        /// Estabilidade do pano com as poses dos clipes do jogo (parado, guarda, andar, correr), sem carregar a fase:
        /// agitação média e máxima de capa e rabo de crina e quantos nós chegaram a ser soltos por ficarem presos na frente.
        /// Um pano assentado fica abaixo de ~0,5 m/s parado. Saída em Logs/pano_clipes.txt.
        /// </summary>
        public static void DiagnosticoPanoClipes()
        {
            var est = new PatrocloEstudio("lado", 64);
            var anim = est.modelo.AddComponent<PatrocloAnimador>(); anim.Preparar(est.design);
            var lib = AssetDatabase.LoadAssetAtPath<PatrocloAnimacoesSO>(PatrocloClipes.Biblioteca);
            var tocador = est.modelo.AddComponent<PatrocloTocador>(); tocador.Preparar(lib.avatar);
            var esq = new Dictionary<string, Transform>(); foreach (var b in est.design.refs.ossos) if (b) esq[b.name] = b;
            var panos = new List<PanoDinamico>
            {
                PanoDinamico.Criar(est.design.refs.capa.GetComponent<SkinnedMeshRenderer>(), esq, ConfigCapa()),
                PanoDinamico.Criar(est.design.refs.raboCostas.GetComponent<SkinnedMeshRenderer>(), esq, ConfigRabo())
            };
            const float dt = 1 / 60f;
            var sb = new System.Text.StringBuilder();
            foreach (bool colisoes in new[] { true, false })
                foreach (string id in new[] { "parado", "guarda", "andar", "correr", "disparar" })
                {
                    var trecho = lib.Movimento(id); if (trecho == null) continue;
                    foreach (var p in panos) p.Ajustes.colisoes = colisoes;
                    tocador.Pedir(id, trecho.clip, 0); tocador.Firmar(); tocador.Avaliar(dt, 0);
                    foreach (var p in panos) p.Reiniciar();
                    var soma = new float[panos.Count]; var pico = new float[panos.Count];
                    var soltos = new int[panos.Count];
                    const int quadros = 300, aquece = 90;
                    for (int q = 0; q < quadros; q++)
                    {
                        tocador.Pedir(id, trecho.clip, q * dt); tocador.Avaliar(dt, 0);
                        for (int k = 0; k < panos.Count; k++)
                        {
                            panos[k].Passo(dt);
                            if (q < aquece) continue;
                            float a = panos[k].Agitacao(dt); soma[k] += a; pico[k] = Mathf.Max(pico[k], a); soltos[k] = Mathf.Max(soltos[k], panos[k].Soltos);
                        }
                    }
                    sb.Append($"{(colisoes ? "com colisão" : "sem colisão"),-12} {id,-9}");
                    for (int k = 0; k < panos.Count; k++)
                        sb.Append($" | {panos[k].name,-20} agitação {soma[k] / (quadros - aquece),5:0.00} máx {pico[k],6:0.00}  nós soltos (máx) {soltos[k],2}");
                    sb.AppendLine();
                }
            File.WriteAllText("Logs/pano_clipes.txt", sb.ToString());
            Debug.Log("pano com clipes:\n" + sb);
            est.Fechar();
        }

        /// <summary>Prancha da física de pano: parado, corrida, sprint, giro e pulo, vistos de lado e de trás.</summary>
        public static void PranchaPano()
        {
            var a = PatrocloEstudio.Args();
            string vista = a.Length > 0 ? a[0] : "lado";
            var est = new PatrocloEstudio(vista, 400);
            var anim = est.modelo.AddComponent<PatrocloAnimador>(); anim.Preparar(est.design);
            var esq = new Dictionary<string, Transform>(); foreach (var b in est.design.refs.ossos) if (b) esq[b.name] = b;
            var capa = PanoDinamico.Criar(est.design.refs.capa.GetComponent<SkinnedMeshRenderer>(), esq, ConfigCapa());
            var rabo = PanoDinamico.Criar(est.design.refs.raboCostas.GetComponent<SkinnedMeshRenderer>(), esq, ConfigRabo());
            Debug.Log($"pano: capa {(capa ? "ok" : "falhou")} rabo {(rabo ? "ok" : "falhou")}");
            bool verNos = a.Length > 1 && a[1] == "nos";
            if (verNos && capa)
            {
                // depuração: mostra a grade de nós (uma cor por linha) no lugar da malha
                est.design.refs.capa.GetComponent<SkinnedMeshRenderer>().enabled = false;
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                for (int i = 0; i < capa.Ossos.Length; i++)
                {
                    var bola = GameObject.CreatePrimitive(PrimitiveType.Sphere); bola.transform.SetParent(capa.Ossos[i], false);
                    bola.transform.localScale = Vector3.one * .05f;
                    bola.GetComponent<Renderer>().sharedMaterial = new Material(unlit) { color = Color.HSVToRGB((i / capa.Colunas) / 10f, 1, 1) };
                }
            }
            foreach (var smr in est.modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.forceMatrixRecalculationPerRender = true;
            Vector3 olho = est.cam.transform.position; Quaternion mira = est.cam.transform.rotation;
            const float dt = 1 / 60f;
            var e = new PatrocloAnimador.Estado { noChao = true, estado = PlayerState.Locomotion, arma = WeaponKind.Sword, direcaoLocal = Vector3.forward };
            void Quadros(int n) { for (int i = 0; i < n; i++) { anim.Avaliar(e, dt); if (capa) capa.Passo(dt); if (rabo) rabo.Passo(dt); } }
            void Reposicionar() { est.heroi.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); e.velocidade = 0; e.noChao = true; anim.Avaliar(e, dt); capa?.Reiniciar(); rabo?.Reiniciar(); Quadros(40); }
            est.Folha(8, 5);
            // 0: parado
            Reposicionar();
            for (int k = 0; k < 8; k++) { Quadros(12); est.Celula(k, 0); }
            // 1 e 2: corrida e sprint (a câmera acompanha)
            for (int linha = 1; linha <= 2; linha++)
            {
                Reposicionar(); float v = linha == 1 ? 5.5f : 8.5f;
                for (int k = 0; k < 8; k++)
                {
                    for (int q = 0; q < 7; q++)
                    {
                        // arranca como no jogo (95 m/s2), não num quadro só
                        float vel = Mathf.Min(v, 95 * (k * 7 + q + 1) * dt); e.velocidade = vel;
                        est.heroi.transform.position += Vector3.forward * vel * dt; Quadros(1);
                    }
                    est.cam.transform.position = olho + est.heroi.transform.position; est.Celula(k, linha);
                }
            }
            est.cam.transform.SetPositionAndRotation(olho, mira);
            // 3: giro de 360 graus em 0,5 s e parada
            Reposicionar();
            for (int k = 0; k < 8; k++)
            {
                for (int q = 0; q < 5; q++) { float t = (k * 5 + q) * dt; if (t < .5f) est.heroi.transform.rotation = Quaternion.Euler(0, 360 * (t + dt) / .5f, 0); Quadros(1); }
                est.Celula(k, 3);
            }
            // 4: pulo (sobe 1,3 m e cai) andando para a frente
            Reposicionar(); e.noChao = false;
            for (int k = 0; k < 8; k++)
            {
                for (int q = 0; q < 6; q++)
                {
                    float t = (k * 6 + q) * dt, vy = 7.2f - 25 * t; e.velY = vy;
                    float y = Mathf.Max(0, 7.2f * t - 12.5f * t * t); if (y <= 0 && t > .1f) e.noChao = true;
                    est.heroi.transform.position = new Vector3(0, y, 0); Quadros(1);
                }
                est.cam.transform.position = olho + Vector3.up * est.heroi.transform.position.y * .8f; est.Celula(k, 4);
            }
            est.Salvar($"Capturas/Golpes/pano_{vista}{(verNos ? "_nos" : "")}.jpg");
            est.Fechar();
        }

        static void Celular(Camera cam, RenderTexture rt, Texture2D folha, int col, int lin)
        {
            cam.Render();
            var antes = RenderTexture.active; RenderTexture.active = rt;
            folha.ReadPixels(new Rect(0, 0, Celula, Celula), col * Celula, lin * Celula); RenderTexture.active = antes;
        }
    }
}
