using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Troia.Tests
{
    /// <summary>
    /// Não é um teste de regra: joga todos os golpes e movimentos do Pátroclo no treino e mede, quadro a quadro,
    /// quanto a arma entra no elmo e na crista e quanto da capa fica dentro ou na frente do corpo.
    /// Relatório em Logs/colisoes.txt, quadros em Logs/colisoes_quadros.csv, piores momentos em Capturas/Colisoes.
    /// Roda só quando pedido pelo nome (-testFilter PatrocloColisoes).
    /// </summary>
    public class PatrocloColisoes
    {
        static IEnumerator Esperar(float segundos)
        {
            float fim = Time.time + segundos;
            while (Time.time < fim) yield return null;
        }

        [UnityTest, Explicit, Timeout(1500000)] public IEnumerator Medir()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Troia/Cenas/Fase1_NoiteDaInvasao.unity");
            yield return null;
            var gm = GameManager.Instance; var player = gm.player; player.readInput = false;
            yield return new WaitForSeconds(.6f);
            Assert.That(player.GetComponent<PlayerPresentation>().UsaPatroclo, Is.True, "O herói do jogo deve ser o Pátroclo");
            bool fotos = !System.Environment.GetCommandLineArgs().Contains("-troia-sem-fotos");
            string so = null;
            foreach (string a in System.Environment.GetCommandLineArgs()) if (a.StartsWith("-troia-so=")) so = a.Substring(10);
            var med = player.gameObject.AddComponent<MedidorDeColisoes>(); med.Preparar(player, fotos);
            Time.captureFramerate = 60;
            var comecar = typeof(PlayerCombat).GetMethod("BeginAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(comecar, Is.Not.Null);

            gm.BeginTraining(); med.rotulo = null; yield return Esperar(.5f);
            player.Targeting.Clear();
            float rumo = player.transform.eulerAngles.y;
            if (so == null)
            {
                med.rotulo = "guarda"; yield return Esperar(1.2f);
                // em combate (inimigo por perto): anda e corre em guarda
                for (int w = 0; w < 2 && w < player.config.weapons.Length; w++)
                {
                    player.SelectWeapon(w); med.rotulo = null; yield return Esperar(.6f);
                    string arma = player.Weapon.id + " em combate";
                    player.SetMoveInput(new Vector2(0, .3f), rumo + 90, false); med.rotulo = arma + "/andar"; yield return Esperar(1.4f);
                    player.SetMoveInput(new Vector2(0, 1), rumo - 90, false); med.rotulo = arma + "/correr"; yield return Esperar(1.4f);
                    player.SetMoveInput(new Vector2(0, 1), rumo + 90, true); med.rotulo = arma + "/disparar"; yield return Esperar(1.2f);
                    player.SetMoveInput(Vector2.zero, rumo, false); med.rotulo = arma + "/freada"; yield return Esperar(.8f);
                }
                // sem inimigo por perto ele sai da guarda: a espada vai para o ombro
                foreach (var enemy in EnemyCombat.Combatants.ToArray())
                {
                    if (!enemy) continue;
                    var agente = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agente) agente.enabled = false;
                    enemy.transform.position += new Vector3(0, -200, 0);
                }
                med.rotulo = null; yield return Esperar(4.2f);
                for (int w = 0; w < player.config.weapons.Length; w++)
                {
                    player.SelectWeapon(w); med.rotulo = null; yield return Esperar(.6f);
                    string arma = player.Weapon.id;
                    med.rotulo = arma + "/parado"; yield return Esperar(1.5f);
                    player.SetMoveInput(new Vector2(0, .3f), rumo, false); med.rotulo = arma + "/andar"; yield return Esperar(1.6f);
                    player.SetMoveInput(new Vector2(0, 1), rumo + 180, false); med.rotulo = arma + "/correr"; yield return Esperar(1.8f);
                    player.SetMoveInput(new Vector2(0, 1), rumo, true); med.rotulo = arma + "/disparar"; yield return Esperar(1.6f);
                    player.SetMoveInput(new Vector2(0, 1), rumo + 180, true); med.rotulo = arma + "/disparar_volta"; yield return Esperar(1.3f);
                    player.SetMoveInput(Vector2.zero, rumo, false); med.rotulo = arma + "/freada"; yield return Esperar(1f);
                    player.RequestJump(); med.rotulo = arma + "/pulo"; yield return Esperar(.3f);
                    player.RequestJump(); yield return Esperar(1.3f);
                    med.rotulo = arma + "/esquiva";
                    foreach (var d in new[] { Vector3.forward, Vector3.left, Vector3.back, Vector3.right })
                    {
                        float limite = Time.time + 2;
                        while (!player.RequestDodge(player.transform.TransformDirection(d)) && Time.time < limite) yield return null;
                        yield return Esperar(.75f);
                    }
                    med.rotulo = arma + "/dardo"; player.RequestShoot(); yield return Esperar(.7f);
                }
            }
            for (int w = 0; w < player.config.weapons.Length; w++)
            {
                player.SelectWeapon(w); med.rotulo = null; yield return Esperar(.6f);
                var arma = player.Weapon;
                var golpes = new List<ComboSO>();
                void Cadeia(ComboSO m)
                {
                    for (int guarda = 0; m && guarda < 8 && !golpes.Contains(m); guarda++)
                    {
                        golpes.Add(m);
                        if (m.delayedNext) Cadeia(m.delayedNext);
                        if (m.heavyNext) Cadeia(m.heavyNext);
                        m = m.next;
                    }
                }
                Cadeia(arma.light); Cadeia(arma.heavy); Cadeia(arma.launcher); Cadeia(arma.dash); Cadeia(arma.airDash); Cadeia(arma.airLight); Cadeia(arma.slam);
                foreach (var golpe in golpes)
                {
                    if (so != null && !golpe.id.Contains(so)) continue;
                    float limite = Time.time + 3;
                    while ((player.State != PlayerState.Locomotion || player.MoveCooldown(golpe) > 0 || !player.IsGrounded) && Time.time < limite) yield return null;
                    med.rotulo = null;
                    if (golpe.aerial || golpe.slam) { player.RequestJump(); yield return Esperar(.28f); }
                    comecar.Invoke(player, new object[] { golpe, false });
                    limite = Time.time + golpe.duration + 1.5f;
                    yield return null;
                    while (player.CurrentAttack == golpe && Time.time < limite) yield return null;
                    // a volta à guarda também conta: é quando a arma passa de novo perto da cabeça
                    med.rotulo = arma.id + "/" + golpe.id + " (volta)"; yield return Esperar(.45f);
                    med.rotulo = null; yield return Esperar(.25f);
                }
            }
            Time.captureFramerate = 0;
            med.Relatorio();
            gm.EndRun(false);
        }
    }

    /// <summary>Mede depois que a pose e o pano do quadro estão prontos.</summary>
    [DefaultExecutionOrder(10000)]
    public class MedidorDeColisoes : MonoBehaviour
    {
        class Soma
        {
            public int quadros, comElmo, comCrista, comCorpo;
            public float elmo = float.NegativeInfinity, crista = float.NegativeInfinity, corpo = float.NegativeInfinity, tElmo, tCrista, zElmo, mao = float.PositiveInfinity;
            public float dentro, frente, tDentro, tFrente, fotoArma = .015f, fotoCapa = .02f;
        }
        public string rotulo;
        PlayerCombat player;
        bool fotos;
        Transform cabeca, espada, lanca, modelo;
        Vector2 zEspada, zLanca;
        // elmo: cápsula vertical no espaço do osso da cabeça; crista: cápsula achatada no plano do meio da cabeça
        Vector3 elmoA, elmoB, cristaA, cristaB; float elmoRaio, cristaRaio, cristaAchata = 1;
        struct Capsula { public Transform a, b; public float raio; public bool tronco; }
        readonly List<Capsula> corpo = new List<Capsula>();
        SkinnedMeshRenderer capa; Mesh assada; readonly List<Vector3> verts = new List<Vector3>();
        bool[] ignorarDentro, ignorarFrente; int amostras;
        readonly Dictionary<string, Soma> somas = new Dictionary<string, Soma>();
        readonly List<string> ordem = new List<string>();
        readonly System.Text.StringBuilder csv = new System.Text.StringBuilder("quadro;rotulo;t;elmo_cm;crista_cm;corpo_cm;z_arma;mao_cabeca_cm;capa_dentro_pct;capa_frente_pct\n");
        readonly System.Text.StringBuilder cabecalho = new System.Text.StringBuilder();
        int quadro;

        public void Preparar(PlayerCombat p, bool comFotos)
        {
            player = p; fotos = comFotos;
            modelo = p.transform.Find("Patroclo");
            var design = modelo.GetComponent<PatrocloDesign>();
            var osso = new Dictionary<string, Transform>(); foreach (var b in design.refs.ossos) if (b) osso[b.name] = b;
            cabeca = osso["head"];
            espada = osso["wrist.R"].Find("Espada"); lanca = osso["wrist.R"].Find("Lanca");
            zEspada = Alcance(espada); zLanca = Alcance(lanca);
            // só o casco e só o leque da crista (sem os rabos de crina, que são pano solto)
            var casco = design.refs.elmo.transform.Find("Elmo_Casco");
            var be = Volume(casco ? casco.gameObject : design.refs.elmo, cabeca, false); var bc = Volume(design.refs.crista, cabeca, false);
            elmoRaio = (be.extents.x + be.extents.z) * .5f;
            elmoA = new Vector3(be.center.x, be.min.y + Mathf.Min(elmoRaio, be.extents.y), be.center.z);
            elmoB = new Vector3(be.center.x, be.max.y - Mathf.Min(elmoRaio, be.extents.y), be.center.z);
            // crista: leque fino no plano do meio; vira cápsula redonda se X for esticado por 'cristaAchata'
            cristaRaio = Mathf.Min(bc.extents.y, bc.extents.z);
            cristaAchata = cristaRaio / Mathf.Max(.01f, bc.extents.x);
            bool aoComprido = bc.extents.z >= bc.extents.y;
            Vector3 meio = bc.center, eixo = aoComprido ? Vector3.forward * (bc.extents.z - cristaRaio) : Vector3.up * (bc.extents.y - cristaRaio);
            cristaA = meio - eixo; cristaB = meio + eixo;
            cabecalho.AppendLine($"elmo (osso da cabeça): min {be.min.ToString("F3")} max {be.max.ToString("F3")} -> cápsula {elmoA.ToString("F3")}..{elmoB.ToString("F3")} raio {elmoRaio:0.000}");
            cabecalho.AppendLine($"crista (osso da cabeça): min {bc.min.ToString("F3")} max {bc.max.ToString("F3")} -> cápsula {cristaA.ToString("F3")}..{cristaB.ToString("F3")} raio {cristaRaio:0.000} achatada {cristaAchata:0.0}x");
            cabecalho.AppendLine($"espada z {zEspada.x:0.00}..{zEspada.y:0.00}; lança z {zLanca.x:0.00}..{zLanca.y:0.00}; escala do modelo {modelo.lossyScale.x:0.000}");
            foreach (var (a, b, raio, tronco) in new[] {
                ("root", "spine03", .135f, true), ("spine03", "spine01", .15f, true), ("neck01", "head", .1f, false),
                ("upperleg01.L", "lowerleg01.L", .088f, false), ("lowerleg01.L", "foot.L", .058f, false),
                ("upperleg01.R", "lowerleg01.R", .088f, false), ("lowerleg01.R", "foot.R", .058f, false),
                ("upperarm01.L", "lowerarm01.L", .058f, false), ("lowerarm01.L", "wrist.L", .05f, false),
                ("upperarm01.R", "lowerarm01.R", .058f, false), ("lowerarm01.R", "wrist.R", .05f, false) })
                if (osso.TryGetValue(a, out var ta) && osso.TryGetValue(b, out var tb)) corpo.Add(new Capsula { a = ta, b = tb, raio = raio, tronco = tronco });
            capa = design.refs.capa ? design.refs.capa.GetComponent<SkinnedMeshRenderer>() : null;
            assada = new Mesh();
        }
        static Vector2 Alcance(Transform arma)
        {
            float z0 = float.MaxValue, z1 = float.MinValue;
            if (arma)
                foreach (var mf in arma.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh || !mf.gameObject.activeSelf && mf.transform != arma) continue;
                    var b = mf.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 canto = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        float z = arma.InverseTransformPoint(mf.transform.TransformPoint(canto)).z; z0 = Mathf.Min(z0, z); z1 = Mathf.Max(z1, z);
                    }
                }
            return z0 > z1 ? new Vector2(0, 1) : new Vector2(z0, z1);
        }
        /// <summary>Caixa de uma peça no espaço de um osso, na pose em que a malha foi presa a ele.</summary>
        static Bounds Volume(GameObject peca, Transform osso, bool comFilhos = true)
        {
            bool tem = false; var caixa = new Bounds();
            void Soma(Vector3 p) { if (!tem) { caixa = new Bounds(p, Vector3.zero); tem = true; } else caixa.Encapsulate(p); }
            if (peca)
            {
                foreach (var smr in comFilhos ? peca.GetComponentsInChildren<SkinnedMeshRenderer>(true) : peca.GetComponents<SkinnedMeshRenderer>())
                {
                    var malha = smr.sharedMesh; if (!malha || !malha.isReadable) continue;
                    int i = System.Array.IndexOf(smr.bones, osso); if (i < 0) continue;
                    var bind = malha.bindposes[i]; var bw = malha.boneWeights; var v = malha.vertices;
                    for (int k = 0; k < v.Length; k++)
                    {
                        float peso = (bw[k].boneIndex0 == i ? bw[k].weight0 : 0) + (bw[k].boneIndex1 == i ? bw[k].weight1 : 0) + (bw[k].boneIndex2 == i ? bw[k].weight2 : 0) + (bw[k].boneIndex3 == i ? bw[k].weight3 : 0);
                        if (peso > .5f) Soma(bind.MultiplyPoint3x4(v[k]));
                    }
                }
                foreach (var mf in peca.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh && mf.sharedMesh.isReadable && mf.transform.IsChildOf(osso))
                        foreach (var v in mf.sharedMesh.vertices) Soma(osso.InverseTransformPoint(mf.transform.TransformPoint(v)));
            }
            return caixa;
        }

        void LateUpdate()
        {
            if (!player) return;
            var golpe = player.CurrentAttack;
            string nome = golpe ? player.Weapon.id + "/" + golpe.id : rotulo;
            quadro++;
            if (nome == null) return;
            if (!somas.TryGetValue(nome, out var s)) { s = new Soma(); somas[nome] = s; ordem.Add(nome); }
            s.quadros++;
            float t = golpe ? player.AttackTime : s.quadros / 60f;

            // ---------------- arma contra o elmo, a crista e o tronco
            Transform arma = lanca && lanca.gameObject.activeInHierarchy ? lanca : espada && espada.gameObject.activeInHierarchy ? espada : null;
            float pElmo = float.NegativeInfinity, pCrista = float.NegativeInfinity, pCorpo = float.NegativeInfinity, zPerto = 0, dMao = 0;
            if (arma)
            {
                Vector2 z = arma == lanca ? zLanca : zEspada;
                float grossura = arma == lanca ? .017f : .02f;
                Vector3 p0 = arma.TransformPoint(0, 0, z.x), p1 = arma.TransformPoint(0, 0, z.y);
                Vector3 a = cabeca.InverseTransformPoint(p0), b = cabeca.InverseTransformPoint(p1);
                float dist = Segmentos(a, b, elmoA, elmoB, out float u);
                pElmo = elmoRaio + grossura - dist; zPerto = Mathf.Lerp(z.x, z.y, u);
                Vector3 esc = new Vector3(cristaAchata, 1, 1);
                float distC = Segmentos(Vector3.Scale(a, esc), Vector3.Scale(b, esc), Vector3.Scale(cristaA, esc), Vector3.Scale(cristaB, esc), out _);
                pCrista = cristaRaio + grossura - distC;
                dMao = Segmentos(cabeca.InverseTransformPoint(arma.position), cabeca.InverseTransformPoint(arma.position), elmoA, elmoB, out _) - elmoRaio;
                // tronco: só a parte da arma além da mão (lâmina ou haste da frente e de trás, fora os 25 cm do punho)
                foreach (var c in corpo)
                {
                    if (!c.tronco) continue;
                    foreach (var (za, zb) in new[] { (Mathf.Max(z.x, .25f), z.y), (z.x, Mathf.Min(z.y, -.25f)) })
                    {
                        if (zb <= za) continue;
                        float d = Segmentos(arma.TransformPoint(0, 0, za), arma.TransformPoint(0, 0, zb), c.a.position, c.b.position, out _);
                        pCorpo = Mathf.Max(pCorpo, c.raio * modelo.lossyScale.x - d);
                    }
                }
                if (pElmo > 0) s.comElmo++; if (pCrista > 0) s.comCrista++; if (pCorpo > 0) s.comCorpo++;
                if (pElmo > s.elmo) { s.elmo = pElmo; s.tElmo = t; s.zElmo = zPerto; }
                if (pCrista > s.crista) { s.crista = pCrista; s.tCrista = t; }
                s.corpo = Mathf.Max(s.corpo, pCorpo); s.mao = Mathf.Min(s.mao, dMao);
                float pior = Mathf.Max(pElmo, pCrista);
                if (fotos && pior > s.fotoArma + .03f)
                {
                    s.fotoArma = pior;
                    Foto($"arma_{Limpo(nome)}", cabeca.position + player.transform.TransformDirection(new Vector3(1.0f, .35f, 1.7f)), cabeca.position);
                    Foto($"arma_{Limpo(nome)}_lado", cabeca.position + player.transform.TransformDirection(new Vector3(-2.0f, .3f, -.2f)), cabeca.position);
                }
            }

            // ---------------- capa: quanto dela está dentro do corpo ou na frente do tronco
            float dentro = 0, frente = 0;
            if (capa && capa.gameObject.activeInHierarchy)
            {
                capa.BakeMesh(assada, true); assada.GetVertices(verts);
                var m = capa.transform.localToWorldMatrix; float escala = modelo.lossyScale.x;
                const int passo = 5;
                int n = (verts.Count + passo - 1) / passo;
                bool primeira = ignorarDentro == null;
                if (primeira) { ignorarDentro = new bool[n]; ignorarFrente = new bool[n]; amostras = n; }
                int nd = 0, nf = 0;
                for (int k = 0; k < n && k < amostras; k++)
                {
                    Vector3 p = m.MultiplyPoint3x4(verts[k * passo]);
                    bool d = false, f = false;
                    for (int c = 0; c < corpo.Count; c++)
                    {
                        var cap = corpo[c];
                        Vector3 a = cap.a.position, ab = cap.b.position - a;
                        float l2 = ab.sqrMagnitude, u = l2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2) : 0;
                        Vector3 fora = p - (a + ab * u); float dist = fora.magnitude, raio = cap.raio * escala;
                        if (dist < raio * .8f) d = true;
                        // na frente do tronco: passou do plano das costas para o lado do peito, colado ao corpo
                        if (cap.tronco && u > .02f && u < .98f && dist < raio + .1f * escala && Vector3.Dot(fora, cap.a.forward) > .04f * escala) f = true;
                    }
                    if (primeira) { ignorarDentro[k] = d; ignorarFrente[k] = f; }
                    if (d && !ignorarDentro[k]) nd++;
                    if (f && !ignorarFrente[k]) nf++;
                }
                if (primeira) cabecalho.AppendLine($"capa: {verts.Count} vértices, {n} amostras; já no repouso {ignorarDentro.Count(x => x)} dentro e {ignorarFrente.Count(x => x)} na frente (descontadas)");
                dentro = 100f * nd / n; frente = 100f * nf / n;
                if (dentro > s.dentro) { s.dentro = dentro; s.tDentro = t; }
                if (frente > s.frente) { s.frente = frente; s.tFrente = t; }
                float piorCapa = Mathf.Max(dentro, frente) / 100f;
                if (fotos && piorCapa > s.fotoCapa + .05f)
                {
                    s.fotoCapa = piorCapa;
                    Vector3 alvo = player.transform.position + Vector3.up * 1.05f;
                    Foto($"capa_{Limpo(nome)}_tras", alvo + player.transform.TransformDirection(new Vector3(1.5f, .7f, -2.6f)), alvo);
                    Foto($"capa_{Limpo(nome)}_frente", alvo + player.transform.TransformDirection(new Vector3(-1.5f, .5f, 2.6f)), alvo);
                }
            }
            csv.AppendLine($"{quadro};{nome};{t:0.000};{pElmo * 100:0.0};{pCrista * 100:0.0};{pCorpo * 100:0.0};{zPerto:0.00};{dMao * 100:0.0};{dentro:0.0};{frente:0.0}");
        }

        public void Relatorio()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(cabecalho);
            sb.AppendLine("valores em cm: quanto a arma entra (positivo = atravessa); entre parênteses, quadros atravessando / quadros medidos");
            sb.AppendLine($"{"movimento",-42} {"elmo",18} {"crista",16} {"tronco",14} {"mão-elmo",9} {"capa dentro",14} {"capa na frente",15}");
            foreach (string nome in ordem)
            {
                var s = somas[nome];
                string e = float.IsNegativeInfinity(s.elmo) ? "-" : $"{s.elmo * 100,5:0.0} ({s.comElmo}/{s.quadros}) t{s.tElmo:0.00} z{s.zElmo:0.00}";
                string c = float.IsNegativeInfinity(s.crista) ? "-" : $"{s.crista * 100,5:0.0} ({s.comCrista}) t{s.tCrista:0.00}";
                string k = float.IsNegativeInfinity(s.corpo) ? "-" : $"{s.corpo * 100,5:0.0} ({s.comCorpo})";
                sb.AppendLine($"{nome,-42} {e,18} {c,16} {k,14} {(float.IsPositiveInfinity(s.mao) ? 0 : s.mao * 100),9:0.0} {s.dentro,7:0.0}% t{s.tDentro:0.00} {s.frente,7:0.0}% t{s.tFrente:0.00}");
            }
            File.WriteAllText("Logs/colisoes.txt", sb.ToString());
            File.WriteAllText("Logs/colisoes_quadros.csv", csv.ToString());
            Debug.Log("PATROCLO_COLISOES ok\n" + sb);
        }

        static string Limpo(string s) => s.Replace('/', '_').Replace(' ', '_').Replace("(", "").Replace(")", "");

        /// <summary>Menor distância entre dois segmentos; 'u' é a posição do ponto mais próximo ao longo do primeiro (0..1).</summary>
        static float Segmentos(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float u)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = d1.sqrMagnitude, e = d2.sqrMagnitude, f = Vector3.Dot(d2, r), t;
            if (a <= 1e-9f && e <= 1e-9f) { u = 0; return r.magnitude; }
            if (a <= 1e-9f) { u = 0; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-9f) { t = 0; u = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), den = a * e - b * b;
                    u = den > 1e-9f ? Mathf.Clamp01((b * f - c * e) / den) : 0;
                    t = (b * u + f) / e;
                    if (t < 0) { t = 0; u = Mathf.Clamp01(-c / a); }
                    else if (t > 1) { t = 1; u = Mathf.Clamp01((b - c) / a); }
                }
            }
            return Vector3.Distance(p1 + d1 * u, p2 + d2 * t);
        }

        void Foto(string nome, Vector3 olho, Vector3 alvo)
        {
            var camera = Camera.main; if (!camera) return;
            Directory.CreateDirectory("Capturas/Colisoes");
            var view = camera.transform; Vector3 p0 = view.position; Quaternion r0 = view.rotation; float fov = camera.fieldOfView;
            view.position = olho; view.LookAt(alvo); camera.fieldOfView = 38;
            var rt = new RenderTexture(960, 720, 24) { antiAliasing = 2 };
            var antes = camera.targetTexture; var ativo = RenderTexture.active;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(960, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.GetFullPath("Capturas/Colisoes/" + nome + ".jpg"), tex.EncodeToJPG(88));
            camera.targetTexture = antes; RenderTexture.active = ativo; Destroy(tex); rt.Release(); Destroy(rt);
            view.SetPositionAndRotation(p0, r0); camera.fieldOfView = fov;
        }
    }
}
