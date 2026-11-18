using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>
    /// Importa bibliotecas de animação humanoide (FBX) e prepara os clipes para tocar no Pátroclo por retargeting.
    /// </summary>
    public static class PatrocloClipes
    {
        public const string Pasta = "Assets/Troia/Personagens/Animacoes";

        static readonly HashSet<string> Espelhados = new HashSet<string> { "Melee_Hook", "Punch_Cross", "Punch_Jab", "Sword_Regular_A", "Sword_Regular_B", "Sword_Regular_C", "Sword_Heavy_Combo" };
        public const string Biblioteca = "Assets/Troia/Resources/Troia/Animacoes.asset";

        // golpe (id do ComboSO) -> clipe, início, impacto, fim (segundos dentro do clipe)
        static readonly (string id, string clipe, float inicio, float impacto, float fim)[] Golpes =
        {
            // ESPADA: corte, revés e um salto com golpe de cima; os pesados são os giros largos de corpo inteiro
            ("Corte_1", "Sword_Regular_A", 0f, .25f, .43f), ("Corte_2", "Sword_Regular_B", 0f, .27f, .53f), ("Corte_3", "Sword_Regular_C", .22f, .65f, 1.25f),
            ("Xifos_Ruptura", "Sword_Heavy_Combo", 1.5f, 1.85f, 2.3f), ("Xifos_Ascendente", "Sword_Heavy_Combo", .85f, 1.18f, 1.55f),
            ("Pesado", "Sword_Heavy_Combo", .08f, .46f, .9f), ("Xifos_Execucao", "Sword_Heavy_Combo", 2.25f, 2.6f, 3.3f),
            ("Lancador", "Sword_Heavy_Combo", .85f, 1.18f, 1.55f),
            ("Aereo_1", "Sword_Regular_A", 0f, .25f, .43f), ("Aereo_2", "Sword_Regular_B", 0f, .27f, .53f), ("Aereo_3", "Sword_Heavy_Combo", 1.5f, 1.85f, 2.3f),
            ("Queda", "Melee_1H_Attack_Jump_Chop", .35f, .56f, 1.2f),
            ("Xifos_Investida", "Sword_Dash", .29f, .33f, 1f), ("Xifos_Cacada", "Sword_Dash", .29f, .33f, 1f),
            // (Sword_Dash começa com um giro de costas inteiro: os trechos entram já no bote, em 0,29 s)
            // LANÇA: o corpo vem dos golpes largos (avanço fundo, giros); nas estocadas o braço da lança é guiado por IK
            ("Lanca_Estocada1", "Melee_1H_Attack_Stab", .12f, .38f, .95f), ("Lanca_Estocada2", "Sword_Regular_B", 0f, .27f, .53f),
            ("Lanca_Estocada3", "Sword_Dash", .29f, .33f, 1f),
            ("Lanca_Varredura", "Sword_Heavy_Combo", 1.5f, 1.85f, 2.3f), ("Lanca_Repique", "Sword_Dash", .29f, .33f, 1f),
            ("Lanca_Giro", "Sword_Heavy_Combo", 1.5f, 1.85f, 2.3f), ("Lanca_Ceifa", "Sword_Regular_C", .22f, .65f, 1.25f),
            ("Lanca_Icaro", "Sword_Heavy_Combo", .85f, 1.18f, 1.55f), ("Lanca_Alavanca", "Sword_Heavy_Combo", .85f, 1.18f, 1.55f),
            ("Lanca_Aereo1", "Melee_1H_Attack_Stab", .12f, .38f, .95f), ("Lanca_Aereo2", "Sword_Regular_B", 0f, .27f, .53f), ("Lanca_Aereo3", "Sword_Heavy_Combo", 1.5f, 1.85f, 2.3f),
            ("Lanca_Meteoro", "Melee_1H_Attack_Jump_Chop", .35f, .56f, 1.2f),
            ("Lanca_Falange", "Sword_Dash", .29f, .33f, 1f), ("Lanca_Perseguicao", "Sword_Dash", .29f, .33f, 1f),
            // ESCUDO: os golpes da espada espelhados (quem varre é o braço do escudo, com a borda), o chute de hoplita e a investida
            ("Escudo_Corte1", "Sword_Regular_A_Espelho", 0f, .25f, .43f), ("Escudo_Corte2", "Sword_Regular_B_Espelho", 0f, .27f, .53f),
            ("Escudo_Rajada", "Sword_Heavy_Combo_Espelho", 1.5f, 1.85f, 2.3f), ("Escudo_CruzFinal", "Sword_Regular_B_Espelho", 0f, .27f, .53f),
            ("Escudo_Tesoura", "Melee_Unarmed_Attack_Kick", .1f, .32f, .85f), ("Escudo_Furacao", "Sword_Regular_C_Espelho", .22f, .65f, 1.25f),
            ("Escudo_Rasante", "Sword_Heavy_Combo_Espelho", 1.5f, 1.85f, 2.3f), ("Escudo_Espiral", "Sword_Heavy_Combo_Espelho", .85f, 1.18f, 1.55f),
            ("Escudo_Aereo1", "Sword_Regular_A_Espelho", 0f, .25f, .43f), ("Escudo_Aereo2", "Sword_Regular_B_Espelho", 0f, .27f, .53f),
            ("Escudo_Aereo3", "Sword_Regular_A_Espelho", 0f, .25f, .43f), ("Escudo_Aereo4", "Sword_Heavy_Combo_Espelho", 1.5f, 1.85f, 2.3f),
            ("Escudo_Chuva", "Melee_1H_Attack_Jump_Chop", .35f, .56f, 1.2f),
            ("Escudo_Relampago", "Shield_Dash", .1f, .6f, 1.05f), ("Escudo_Borboleta", "Shield_Dash", .1f, .6f, 1.05f),
        };
        // estocadas de lança com o braço guiado: 1 = baixa (do quadril), 2 = alta (por cima do escudo)
        static readonly Dictionary<string, int> Estocadas = new Dictionary<string, int>
        {
            { "Lanca_Estocada1", 1 }, { "Lanca_Estocada3", 1 }, { "Lanca_Repique", 1 }, { "Lanca_Falange", 1 }, { "Lanca_Perseguicao", 2 }, { "Lanca_Aereo1", 2 },
        };
        static readonly (string id, string clipe, float inicio, float fim)[] Movimentos =
        {
            ("parado", "Idle_Loop", 0, 0), ("guarda", "Sword_Idle", 0, 0), ("andar", "Walk_Loop", 0, 0), ("correr", "Jog_Fwd_Loop", 0, 0), ("disparar", "Sprint_Loop", 0, 0),
            ("pulo", "Jump_Start", .45f, 1.1f), ("no_ar", "Jump_Loop", 0, 0), ("pouso", "Jump_Land", 0, .5f), ("pulo_duplo", "NinjaJump_Start", .15f, .9f),
            ("esquiva", "Roll", .12f, .95f), ("dano", "Hit_Chest", 0, .33f), ("morte", "Death01", 0, 2.4f), ("arremesso", "OverhandThrow", .12f, .75f),
            ("investida", "Sprint_Loop", 0, 0),
        };
        // gestos das bênçãos: início, auge da pose (quando o poder cai) e fim dentro do clipe
        static readonly (string id, string clipe, float inicio, float auge, float fim)[] Gestos =
        {
            ("bencao_zeus", "Melee_1H_Attack_Chop", .15f, .58f, 1.0f),        // ergue a espada ao céu e desce o golpe
            ("bencao_ares", "Melee_Blocking", 0f, .3f, .9f),                  // braços abertos, peito para a frente (o grito vem por cima)
            ("bencao_apolo", "Idle_Rail_Call", 1.3f, 2.0f, 2.45f),            // lâmina erguida na vertical, escudo ao peito
            ("bencao_hermes", "Sword_Dash", .3f, .34f, .8f),                  // bote baixo com a lâmina à frente
            ("bencao_atena", "Shield_OneShot", 0f, .12f, .6f),                // escudo apresentado de frente
            ("bencao_poseidon", "Melee_2H_Attack_Slice", .1f, .4f, .95f),     // varrida larga rente ao chão
        };

        [MenuItem("Troia/Personagem/Montar biblioteca de animação", priority = 48)]
        public static void MontarBiblioteca()
        {
            var lib = AssetDatabase.LoadAssetAtPath<PatrocloAnimacoesSO>(Biblioteca);
            if (!lib) { lib = ScriptableObject.CreateInstance<PatrocloAnimacoesSO>(); AssetDatabase.CreateAsset(lib, Biblioteca); }
            lib.avatar = PatrocloAvatar.Carregar();
            lib.golpes.Clear(); lib.movimentos.Clear();
            var faltam = new List<string>();
            // boneco de medição: em que direção cada bote de estocada realmente vai
            var boneco = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PatrocloJogo.Prefab));
            var bd = boneco.GetComponent<PatrocloDesign>(); bd.pose = PosePatroclo.Repouso; bd.Apply(); bd.pose = PosePatroclo.Livre;
            var ba = boneco.AddComponent<Animator>(); ba.avatar = lib.avatar;
            Transform peD = null, peE = null;
            foreach (var t in bd.refs.ossos) { if (t && t.name == "foot.R") peD = t; if (t && t.name == "foot.L") peE = t; }
            var relato = new List<string>();
            foreach (var (id, clipe, inicio, impacto, fim) in Golpes)
            {
                var c = Achar(clipe); if (!c) { faltam.Add(clipe); continue; }
                Estocadas.TryGetValue(id, out int estocada);
                float giro = 0;
                // (as estocadas do KayKit já saem para a frente, com os pés de lado: nelas a linha dos pés não vale)
                if (estocada > 0 && peD && peE && !AssetDatabase.GetAssetPath(c).Contains("KayKit"))
                {
                    // linha do pé de trás ao pé da frente no impacto = direção do bote (medida no referencial do próprio clipe)
                    boneco.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    c.SampleAnimation(boneco, Mathf.Min(impacto + .08f, c.length * .99f));
                    Vector3 frenteClipe = AssetDatabase.GetAssetPath(c).Contains("KayKit") ? Vector3.forward : Vector3.back;
                    Vector3 linha = Vector3.ProjectOnPlane(peD.position - peE.position, Vector3.up);
                    if (Vector3.Dot(linha, frenteClipe) < 0) linha = -linha;
                    if (linha.magnitude > .25f) giro = Mathf.Clamp(Vector3.SignedAngle(frenteClipe, linha, Vector3.up), -60, 60);
                    relato.Add($"{id}:{giro:0}");
                }
                lib.golpes.Add(new PatrocloAnimacoesSO.Trecho { id = id, clip = c, inicio = inicio, impacto = impacto, fim = Mathf.Min(fim, c.length), estocada = estocada, giro = giro });
            }
            foreach (var (id, clipe, inicio, fim) in Movimentos)
            {
                var c = Achar(clipe); if (!c) { faltam.Add(clipe); continue; }
                lib.movimentos.Add(new PatrocloAnimacoesSO.Trecho { id = id, clip = c, inicio = inicio, fim = fim > 0 ? Mathf.Min(fim, c.length) : c.length });
            }
            foreach (var (id, clipe, inicio, auge, fim) in Gestos)
            {
                var c = Achar(clipe); if (!c) { faltam.Add(clipe); continue; }
                lib.movimentos.Add(new PatrocloAnimacoesSO.Trecho { id = id, clip = c, inicio = inicio, impacto = Mathf.Min(auge, c.length), fim = Mathf.Min(fim, c.length) });
            }
            Object.DestroyImmediate(boneco);
            Debug.Log("direção dos botes (graus fora da frente): " + string.Join(" ", relato));
            EditorUtility.SetDirty(lib); AssetDatabase.SaveAssets();
            Debug.Log($"biblioteca de animação: {lib.golpes.Count} golpes, {lib.movimentos.Count} movimentos, avatar {(lib.avatar ? "ok" : "AUSENTE")}; clipes ausentes: {(faltam.Count == 0 ? "nenhum" : string.Join(", ", faltam.Distinct()))}");
        }

        public static IEnumerable<string> Fontes() =>
            AssetDatabase.FindAssets("t:Model", new[] { Pasta }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.ToLower().EndsWith(".fbx"));

        /// <summary>Configura os FBX como Humanoide, com os clipes no lugar (sem deslocar a raiz) e os laços marcados.</summary>
        [MenuItem("Troia/Personagem/Importar bibliotecas de animação", priority = 47)]
        public static void Importar()
        {
            foreach (string caminho in Fontes().ToArray())
            {
                var imp = (ModelImporter)AssetImporter.GetAtPath(caminho);
                bool mudou = false;
                if (imp.animationType != ModelImporterAnimationType.Human)
                {
                    imp.animationType = ModelImporterAnimationType.Human; imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.importBlendShapes = false; imp.importCameras = false; imp.importLights = false;
                    imp.importVisibility = false; imp.animationCompression = ModelImporterAnimationCompression.Optimal;
                    imp.SaveAndReimport(); mudou = true;
                    imp = (ModelImporter)AssetImporter.GetAtPath(caminho);
                }
                var clipes = imp.defaultClipAnimations;
                foreach (var c in clipes)
                {
                    int barra = c.name.LastIndexOf('|'); if (barra >= 0) c.name = c.name.Substring(barra + 1);
                    c.loopTime = c.name.EndsWith("_Loop") || c.name.Contains("Running") || c.name.Contains("Walking") || c.name.EndsWith("Idle");
                    // o jogo desloca o herói por código: giro e altura ficam na pose, o avanço não
                    // (os FBX do Quaternius vêm olhando para -Z e os do KayKit para +Z; o herói do jogo olha para +Z)
                    c.lockRootRotation = true; c.keepOriginalOrientation = true; c.rotationOffset = caminho.Contains("KayKit") ? 0 : 180;
                    c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                    c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = true;
                }
                // versões espelhadas: o soco e o gancho da mão direita viram golpes do braço do escudo
                var lista = clipes.ToList();
                foreach (var c in clipes)
                    if (Espelhados.Contains(c.name))
                        lista.Add(new ModelImporterClipAnimation
                        {
                            name = c.name + "_Espelho", takeName = c.takeName, firstFrame = c.firstFrame, lastFrame = c.lastFrame, mirror = true,
                            loopTime = false, lockRootRotation = true, keepOriginalOrientation = true, rotationOffset = c.rotationOffset,
                            lockRootHeightY = true, keepOriginalPositionY = true, lockRootPositionXZ = false, keepOriginalPositionXZ = true
                        });
                imp.clipAnimations = lista.ToArray();
                imp.SaveAndReimport();
                var avatar = AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<Avatar>().FirstOrDefault();
                var nomes = Clipes(caminho).Select(c => $"{c.name} ({c.length:0.00}s{(c.isHumanMotion ? "" : " NAO-HUMANO")})");
                Debug.Log($"biblioteca {Path.GetFileName(caminho)}: avatar {(avatar ? (avatar.isValid && avatar.isHuman ? "humano ok" : "INVALIDO") : "ausente")}; reimportou={mudou}; clipes: {string.Join(", ", nomes)}");
            }
        }

        /// <summary>Velocidade da ponta da espada, do escudo e dos pés ao longo do clipe: mostra onde estão os golpes.</summary>
        static void Analisar(AnimationClip clip, PatrocloEstudio est)
        {
            Transform espada = null, escudo = est.design.refs.escudoBraco ? est.design.refs.escudoBraco.transform : null, peD = null, raiz = est.design.refs.raiz;
            foreach (var t in est.modelo.GetComponentsInChildren<Transform>(true)) { if (t.name == "Espada") espada = t; if (t.name == "foot.R") peD = t; }
            const float dt = 1 / 60f; int n = Mathf.CeilToInt(clip.length / dt);
            var linhas = new List<string>();
            foreach (var (nome, alvo, ponto) in new[] { ("espada", espada, new Vector3(0, 0, 1.2f)), ("escudo", escudo, Vector3.zero), ("pe_dir", peD, Vector3.zero) })
            {
                if (!alvo) continue;
                var vel = new float[n + 1]; Vector3 antes = Vector3.zero; float maior = 0;
                for (int i = 0; i <= n; i++)
                {
                    est.modelo.transform.localPosition = Vector3.zero; est.modelo.transform.localRotation = Quaternion.identity;
                    clip.SampleAnimation(est.modelo, Mathf.Min(i * dt, clip.length * .999f));
                    Vector3 p = alvo.TransformPoint(ponto); if (i > 0) { vel[i] = (p - antes).magnitude / dt; maior = Mathf.Max(maior, vel[i]); }
                    antes = p;
                }
                var picos = new List<string>();
                for (int i = 2; i < n; i++) if (vel[i] > maior * .45f && vel[i] >= vel[i - 1] && vel[i] >= vel[i + 1]) picos.Add($"{i * dt:0.00}s({vel[i]:0}m/s)");
                linhas.Add($"{nome}: {string.Join(" ", picos)}");
            }
            // deslocamento do quadril (avanço real do clipe) e giro
            est.modelo.transform.localPosition = Vector3.zero; clip.SampleAnimation(est.modelo, 0); Vector3 q0 = raiz.position;
            float longe = 0, alto = 0; for (int i = 0; i <= n; i++) { clip.SampleAnimation(est.modelo, Mathf.Min(i * dt, clip.length * .999f)); Vector3 d = raiz.position - q0; longe = Mathf.Max(longe, new Vector2(d.x, d.z).magnitude); alto = Mathf.Max(alto, d.y); }
            Debug.Log($"analise {clip.name} ({clip.length:0.00}s): {string.Join(" | ", linhas)} | quadril anda até {longe:0.00} m, sobe {alto:0.00} m");
        }

        public static IEnumerable<AnimationClip> Clipes(string caminho) =>
            AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__"));

        public static AnimationClip Achar(string nome)
        {
            foreach (string caminho in Fontes())
                foreach (var c in Clipes(caminho)) if (c.name == nome) return c;
            return null;
        }

        /// <summary>
        /// Prancha de retargeting: cada clipe pedido é amostrado em 8 instantes no Pátroclo.
        /// Unity -executeMethod ...PatrocloClipes.Prancha -- vista arquivo clipe1 clipe2 ...
        /// </summary>
        public static void Prancha()
        {
            var a = PatrocloEstudio.Args();
            string vista = a.Length > 0 ? a[0] : "frente", saida = a.Length > 1 ? a[1] : "clipes";
            // "clipe" ou "clipe:linhas" (clipes longos em várias linhas de 8 quadros)
            var nomes = new List<string>(); var partes = new List<(int de, int total)>();
            foreach (string item in a.Skip(2))
            {
                string[] q = item.Split(':'); int n = q.Length > 1 ? int.Parse(q[1]) : 1;
                for (int i = 0; i < n; i++) { nomes.Add(q[0]); partes.Add((i, n)); }
            }
            var avatar = PatrocloAvatar.Carregar();
            var est = new PatrocloEstudio(vista, 400);
            est.design.pose = PosePatroclo.Combate; est.design.Apply(); est.design.pose = PosePatroclo.Repouso; est.design.Apply(); est.design.pose = PosePatroclo.Livre;
            // mãos fechadas (os dedos não entram no retargeting)
            var refs = est.design.refs;
            if (refs.dedos != null) for (int i = 0; i < refs.dedos.Length; i++) if (refs.dedos[i]) refs.dedos[i].localRotation = Quaternion.AngleAxis(refs.dobraDedos[i], refs.eixosDedos[i]);
            var anim = est.modelo.AddComponent<Animator>(); anim.avatar = avatar; anim.applyRootMotion = false;
            const int colunas = 8;
            est.Folha(colunas, Mathf.Max(1, nomes.Count));
            for (int r = 0; r < nomes.Count; r++)
            {
                var clip = Achar(nomes[r]);
                if (!clip) { Debug.LogError("clipe não encontrado: " + nomes[r]); continue; }
                if (partes[r].de == 0) Analisar(clip, est);
                for (int k = 0; k < colunas; k++)
                {
                    float t = clip.length * (partes[r].de * colunas + k) / (partes[r].total * colunas - 1f) * .999f;
                    est.modelo.transform.localPosition = Vector3.zero; est.modelo.transform.localRotation = Quaternion.identity;
                    clip.SampleAnimation(est.modelo, t);
                    est.Celula(k, r);
                }
                Debug.Log($"prancha linha {r}: {clip.name} {clip.length:0.00}s");
            }
            est.Salvar($"Capturas/Golpes/{saida}_{vista}.jpg");
            est.Fechar();
        }
    }
}
