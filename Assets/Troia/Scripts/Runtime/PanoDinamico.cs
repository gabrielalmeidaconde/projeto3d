using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    /// <summary>
    /// Física de pano por ossos para peças que antes iam coladas ao corpo (capa, rabo de crina).
    /// Cria uma grade de ossos sobre a malha (pelas UVs), refaz os pesos da malha para eles e simula os nós
    /// (Verlet): o topo acompanha o corpo, o resto pende do ombro, sente arrasto do ar, gira com o corpo e
    /// colide com cápsulas do tronco, das pernas e dos braços. Parado, a peça volta à forma em que foi modelada.
    /// </summary>
    public class PanoDinamico : MonoBehaviour
    {
        [System.Serializable]
        public class Config
        {
            public int colunas = 7, linhas = 9;
            public bool topoEmV1 = true;          // a UV v = 1 fica no topo (capa) ou v = 0 (rabo de crina)
            public string ancora = "spine01";     // osso que carrega o topo
            public float misturaInicio = .45f, misturaFim = 1.3f; // em linhas da grade: até onde valem os pesos originais
            public float linhasComCorpo = 2.2f;   // até esta linha o alvo segue o tronco; depois pende pela gravidade
            public float rigidezTopo = 260, rigidezBarra = 12, curvaRigidez = .2f;
            public float arrastoTopo = 2.5f, arrastoBarra = 1.15f;
            public float amortecimento = 3.2f, gravidade = 9.8f, rigidezVertical = 45;
            [Range(0, 1)] public float inercia = .3f, inerciaGiro = .55f;
            public float vento = .5f, rigidezLateral = .5f;
            public float raioExtra = 0f;
            public bool colisoes = true;
        }
        public static Config ParaCapa() => new Config { arrastoTopo = 3f, arrastoBarra = 1.7f };
        public static Config ParaRabo() => new Config
        {
            colunas = 1, linhas = 6, topoEmV1 = false, ancora = "head", misturaInicio = .25f, misturaFim = 1f, linhasComCorpo = 1.1f,
            rigidezTopo = 320, rigidezBarra = 16, arrastoTopo = 2.5f, arrastoBarra = 1.5f, vento = .35f
        };
        struct Capsula { public Transform a, b; public float raio; public float[] raioNo; }

        Config cfg;
        SkinnedMeshRenderer alvo;
        Mesh malha;
        Transform ancora, recipiente;
        Matrix4x4 bindAncora;
        int C, R;
        Vector3[] repouso, noAncora, desloc, pos, ant, alvoAtual, alvoAnterior;
        float[] compV, compH;
        Quaternion[] quadroRepouso;
        Transform[] ossos;
        readonly List<Capsula> capsulas = new List<Capsula>();
        Vector3 pivoRepouso, pivoAnterior;
        Quaternion giroAnterior = Quaternion.identity;
        bool pronto, primeiro = true;
        float relogio;
        public float chao = float.NegativeInfinity;
        public bool Pronto => pronto;
        public Transform[] Ossos => ossos;
        /// <summary>Depuração: por linha, posição média e alvo médio relativos ao pivô (no referencial do giro do corpo).</summary>
        public Config Ajustes => cfg;
        /// <summary>Depuração: maior velocidade de um nó em relação ao pivô, em m/s (0 = pano assentado).</summary>
        public float Agitacao(float dt)
        {
            float maior = 0; Vector3 corpo = pos[I(C / 2, 0)] - ant[I(C / 2, 0)];
            for (int i = C; i < pos.Length; i++) maior = Mathf.Max(maior, ((pos[i] - ant[i]) - corpo).magnitude);
            int sub = Mathf.Clamp(Mathf.CeilToInt(dt * 90), 1, 6);
            return maior / (dt / sub);
        }
        public string Relatorio()
        {
            var sb = new System.Text.StringBuilder();
            Quaternion inv = Quaternion.Inverse(giroAnterior);
            for (int r = 0; r < R; r++)
            {
                Vector3 p = Vector3.zero, a = Vector3.zero, v = Vector3.zero;
                for (int c = 0; c < C; c++) { p += pos[I(c, r)]; a += alvoAtual[I(c, r)]; v += pos[I(c, r)] - ant[I(c, r)]; }
                p = inv * (p / C - pivoAnterior); a = inv * (a / C - pivoAnterior);
                sb.AppendLine($"  linha {r}: pos {p.ToString("F2")} alvo {a.ToString("F2")} desloc/substep {(inv * (v / C)).ToString("F3")}");
            }
            return sb.ToString();
        }
        public int Colunas => C;

        int I(int c, int r) => r * C + c;

        public static PanoDinamico Criar(SkinnedMeshRenderer alvo, Dictionary<string, Transform> esqueleto, Config cfg)
        {
            if (!alvo || !alvo.sharedMesh || !alvo.sharedMesh.isReadable) return null;
            var p = alvo.gameObject.AddComponent<PanoDinamico>();
            if (!p.Montar(alvo, esqueleto, cfg)) { Destroy(p); return null; }
            return p;
        }

        bool Montar(SkinnedMeshRenderer smr, Dictionary<string, Transform> esqueleto, Config config)
        {
            cfg = config; alvo = smr; C = cfg.colunas; R = cfg.linhas;
            var fonte = smr.sharedMesh;
            var v = fonte.vertices; var uv = fonte.uv; var bw = fonte.boneWeights; var bind = fonte.bindposes; var bones = smr.bones;
            if (uv.Length != v.Length || bw.Length != v.Length || !esqueleto.TryGetValue(cfg.ancora, out ancora)) return false;
            int iAncora = System.Array.IndexOf(bones, ancora);
            if (iAncora < 0) return false;
            bindAncora = bind[iAncora];

            float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
            for (int i = 0; i < uv.Length; i++) { u0 = Mathf.Min(u0, uv[i].x); u1 = Mathf.Max(u1, uv[i].x); v0 = Mathf.Min(v0, uv[i].y); v1 = Mathf.Max(v1, uv[i].y); }
            float du = Mathf.Max(1e-5f, u1 - u0), dv = Mathf.Max(1e-5f, v1 - v0);

            // posição de repouso de cada nó: média dos vértices em volta dele na grade de UV
            int n = C * R;
            repouso = new Vector3[n]; var peso = new float[n];
            var gu = new float[v.Length]; var gv = new float[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                float vn = (uv[i].y - v0) / dv;
                gu[i] = C > 1 ? (uv[i].x - u0) / du * (C - 1) : 0;
                gv[i] = (cfg.topoEmV1 ? 1 - vn : vn) * (R - 1);
                int c0 = Mathf.Clamp(Mathf.FloorToInt(gu[i]), 0, Mathf.Max(0, C - 2)), r0 = Mathf.Clamp(Mathf.FloorToInt(gv[i]), 0, R - 2);
                float fc = C > 1 ? Mathf.Clamp01(gu[i] - c0) : 0, fr = Mathf.Clamp01(gv[i] - r0);
                for (int k = 0; k < 4; k++)
                {
                    int c = Mathf.Min(C - 1, c0 + (k & 1)), r = r0 + (k >> 1);
                    float w = ((k & 1) == 0 ? 1 - fc : fc) * ((k >> 1) == 0 ? 1 - fr : fr);
                    if (C == 1 && (k & 1) == 1) continue;
                    repouso[I(c, r)] += v[i] * w; peso[I(c, r)] += w;
                }
            }
            for (int i = 0; i < n; i++) if (peso[i] > 1e-4f) repouso[i] /= peso[i];
            // nós sem vértice por perto (rasgos): tomam a média dos vizinhos que têm
            for (int volta = 0; volta < 4; volta++)
                for (int r = 0; r < R; r++) for (int c = 0; c < C; c++)
                {
                    if (peso[I(c, r)] > 1e-4f) continue;
                    Vector3 soma = Vector3.zero; int q = 0;
                    void V(int cc, int rr) { if (cc >= 0 && cc < C && rr >= 0 && rr < R && peso[I(cc, rr)] > 1e-4f) { soma += repouso[I(cc, rr)]; q++; } }
                    V(c - 1, r); V(c + 1, r); V(c, r - 1); V(c, r + 1);
                    if (q > 0) { repouso[I(c, r)] = soma / q; peso[I(c, r)] = 1e-3f; }
                }

            pivoRepouso = repouso[I(C / 2, 0)];
            noAncora = new Vector3[n]; desloc = new Vector3[n]; pos = new Vector3[n]; ant = new Vector3[n];
            alvoAtual = new Vector3[n]; alvoAnterior = new Vector3[n]; compV = new float[n]; compH = new float[n]; quadroRepouso = new Quaternion[n];
            for (int i = 0; i < n; i++) { noAncora[i] = bindAncora.MultiplyPoint3x4(repouso[i]); desloc[i] = repouso[i] - pivoRepouso; }
            for (int r = 0; r < R; r++) for (int c = 0; c < C; c++)
            {
                compV[I(c, r)] = r > 0 ? Vector3.Distance(repouso[I(c, r)], repouso[I(c, r - 1)]) : 0;
                compH[I(c, r)] = c > 0 ? Vector3.Distance(repouso[I(c, r)], repouso[I(c - 1, r)]) : 0;
                quadroRepouso[I(c, r)] = Quadro(repouso, c, r, Vector3.right);
            }

            // ossos novos e malha com os pesos refeitos
            recipiente = new GameObject(smr.name + "_ossos").transform; recipiente.SetParent(smr.transform.parent, false);
            ossos = new Transform[n];
            var todos = new Transform[bones.Length + n]; var binds = new Matrix4x4[bind.Length + n];
            System.Array.Copy(bones, todos, bones.Length); System.Array.Copy(bind, binds, bind.Length);
            for (int i = 0; i < n; i++)
            {
                var t = new GameObject($"pano_{i % C}_{i / C}").transform; t.SetParent(recipiente, false);
                ossos[i] = t; todos[bones.Length + i] = t; binds[bind.Length + i] = Matrix4x4.Translate(-repouso[i]);
            }
            var novos = new BoneWeight[v.Length];
            var lista = new List<KeyValuePair<int, float>>(8);
            for (int i = 0; i < v.Length; i++)
            {
                float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(cfg.misturaInicio, cfg.misturaFim, gv[i]));
                lista.Clear();
                if (t < .999f)
                {
                    var o = bw[i];
                    lista.Add(new KeyValuePair<int, float>(o.boneIndex0, o.weight0)); lista.Add(new KeyValuePair<int, float>(o.boneIndex1, o.weight1));
                    lista.Add(new KeyValuePair<int, float>(o.boneIndex2, o.weight2)); lista.Add(new KeyValuePair<int, float>(o.boneIndex3, o.weight3));
                    Manter(lista, t > .001f ? 2 : 4, 1 - t);
                }
                if (t > .001f)
                {
                    int c0 = Mathf.Clamp(Mathf.FloorToInt(gu[i]), 0, Mathf.Max(0, C - 2)), r0 = Mathf.Clamp(Mathf.FloorToInt(gv[i]), 0, R - 2);
                    float fc = C > 1 ? Mathf.Clamp01(gu[i] - c0) : 0, fr = Mathf.Clamp01(gv[i] - r0);
                    var grade = new List<KeyValuePair<int, float>>(4);
                    for (int k = 0; k < 4; k++)
                    {
                        if (C == 1 && (k & 1) == 1) continue;
                        int c = Mathf.Min(C - 1, c0 + (k & 1)), r = r0 + (k >> 1);
                        float w = ((k & 1) == 0 ? 1 - fc : fc) * ((k >> 1) == 0 ? 1 - fr : fr);
                        grade.Add(new KeyValuePair<int, float>(bones.Length + I(c, r), w));
                    }
                    Manter(grade, t < .999f ? 2 : 4, t);
                    lista.AddRange(grade);
                }
                lista.Sort((a, b) => b.Value.CompareTo(a.Value));
                float soma = 0; for (int k = 0; k < lista.Count && k < 4; k++) soma += lista[k].Value;
                var w4 = new BoneWeight();
                if (soma > 1e-6f)
                {
                    if (lista.Count > 0) { w4.boneIndex0 = lista[0].Key; w4.weight0 = lista[0].Value / soma; }
                    if (lista.Count > 1) { w4.boneIndex1 = lista[1].Key; w4.weight1 = lista[1].Value / soma; }
                    if (lista.Count > 2) { w4.boneIndex2 = lista[2].Key; w4.weight2 = lista[2].Value / soma; }
                    if (lista.Count > 3) { w4.boneIndex3 = lista[3].Key; w4.weight3 = lista[3].Value / soma; }
                }
                else w4 = bw[i];
                novos[i] = w4;
            }
            malha = Instantiate(fonte); malha.name = fonte.name + "_dinamica";
            malha.boneWeights = novos; malha.bindposes = binds;
            smr.bones = todos; smr.sharedMesh = malha; smr.updateWhenOffscreen = true;

            foreach (var (a, b, raio) in new[] {
                ("root", "spine03", .135f), ("spine03", "spine01", .15f), ("root", "root", .165f), ("neck01", "head", .1f),
                ("upperleg01.L", "lowerleg01.L", .088f), ("lowerleg01.L", "foot.L", .058f),
                ("upperleg01.R", "lowerleg01.R", .088f), ("lowerleg01.R", "foot.R", .058f),
                ("upperarm01.L", "lowerarm01.L", .058f), ("lowerarm01.L", "wrist.L", .05f),
                ("upperarm01.R", "lowerarm01.R", .058f), ("lowerarm01.R", "wrist.R", .05f) })
                if (esqueleto.TryGetValue(a, out var ta) && esqueleto.TryGetValue(b, out var tb))
                {
                    // no repouso nenhuma cápsula pode empurrar o pano (ele já foi modelado encostado no corpo):
                    // o raio de cada nó é limitado pela distância que ele tem da cápsula na pose de repouso
                    int ia = System.Array.IndexOf(bones, ta), ib = System.Array.IndexOf(bones, tb);
                    var porNo = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        float r0 = raio + cfg.raioExtra;
                        if (ia >= 0 && ib >= 0)
                        {
                            Vector3 pa = bind[ia].inverse.GetColumn(3), pb = bind[ib].inverse.GetColumn(3), ab = pb - pa;
                            float l2 = ab.sqrMagnitude, t = l2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(repouso[i] - pa, ab) / l2) : 0;
                            r0 = Mathf.Min(r0, Vector3.Distance(repouso[i], pa + ab * t) - .004f);
                        }
                        porNo[i] = r0;
                    }
                    capsulas.Add(new Capsula { a = ta, b = tb, raio = raio + cfg.raioExtra, raioNo = porNo });
                }
            pronto = true;
            Reiniciar();
            return true;
        }
        static void Manter(List<KeyValuePair<int, float>> lista, int quantos, float escala)
        {
            lista.Sort((a, b) => b.Value.CompareTo(a.Value));
            if (lista.Count > quantos) lista.RemoveRange(quantos, lista.Count - quantos);
            float soma = 0; foreach (var kv in lista) soma += kv.Value;
            if (soma < 1e-6f) { lista.Clear(); return; }
            for (int i = 0; i < lista.Count; i++) lista[i] = new KeyValuePair<int, float>(lista[i].Key, lista[i].Value / soma * escala);
        }

        Quaternion Quadro(Vector3[] p, int c, int r, Vector3 ladoPadrao)
        {
            Vector3 baixo = p[I(c, Mathf.Min(R - 1, r + 1))] - p[I(c, Mathf.Max(0, r - 1))];
            // com uma coluna só (rabo de crina) o lado vem do giro do corpo
            Vector3 lado = C > 1 ? p[I(Mathf.Min(C - 1, c + 1), r)] - p[I(Mathf.Max(0, c - 1), r)] : ladoPadrao;
            if (baixo.sqrMagnitude < 1e-8f) baixo = Vector3.down;
            Vector3 frente = Vector3.Cross(lado, baixo);
            if (frente.sqrMagnitude < 1e-8f) frente = Vector3.Cross(Vector3.right, baixo);
            if (frente.sqrMagnitude < 1e-8f) frente = Vector3.forward;
            return Quaternion.LookRotation(frente.normalized, -baixo.normalized);
        }

        void Alvos()
        {
            Matrix4x4 a = ancora.localToWorldMatrix;
            Vector3 pivo = a.MultiplyPoint3x4(bindAncora.MultiplyPoint3x4(pivoRepouso));
            Vector3 frente = Vector3.ProjectOnPlane(a.MultiplyVector(Vector3.forward), Vector3.up);
            Quaternion giro = frente.sqrMagnitude > .04f ? Quaternion.LookRotation(frente.normalized, Vector3.up) : giroAnterior;
            giroAnterior = giro;
            float escala = a.lossyScale.x;
            for (int r = 0; r < R; r++)
            {
                float corpo = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, cfg.linhasComCorpo + 1, r));
                for (int c = 0; c < C; c++)
                {
                    int i = I(c, r);
                    Vector3 comCorpo = a.MultiplyPoint3x4(noAncora[i]);
                    Vector3 pendente = pivo + giro * desloc[i] * escala;
                    alvoAtual[i] = Vector3.Lerp(pendente, comCorpo, corpo);
                }
            }
            pivoAnterior = pivo;
        }

        /// <summary>Volta o pano à forma de repouso na pose atual (início, reaparecimento, teletransporte).</summary>
        public void Reiniciar()
        {
            if (!pronto) return;
            Alvos();
            for (int i = 0; i < pos.Length; i++) { pos[i] = ant[i] = alvoAnterior[i] = alvoAtual[i]; }
            primeiro = false;
            Escrever();
        }

        /// <summary>Avança a simulação. Chamar depois que a pose do quadro estiver pronta.</summary>
        public void Passo(float dt)
        {
            if (!pronto || !alvo || !alvo.gameObject.activeInHierarchy) return;
            if (dt <= 0) { Escrever(); return; }
            Vector3 pivoAntes = pivoAnterior; Quaternion giroAntes = giroAnterior;
            System.Array.Copy(alvoAtual, alvoAnterior, alvoAtual.Length);
            Alvos();
            if (primeiro || (pivoAnterior - pivoAntes).sqrMagnitude > 9f) { Reiniciar(); return; }
            dt = Mathf.Min(dt, .1f);
            int sub = Mathf.Clamp(Mathf.CeilToInt(dt * 90), 1, 6);
            float h = dt / sub;
            float escala = ancora.lossyScale.x;
            // O pano é carregado junto com o corpo em parte do movimento: só sente uma fração das arrancadas e dos giros
            // bruscos (um herói de jogo acelera a 10 g; pano de verdade viraria do avesso). O arrasto do ar vale inteiro.
            Vector3 carga = (pivoAnterior - pivoAntes) / sub * (1 - cfg.inercia);
            float giroSub = Mathf.DeltaAngle(giroAntes.eulerAngles.y, giroAnterior.eulerAngles.y) / sub * (1 - cfg.inerciaGiro);
            Quaternion rot = Quaternion.AngleAxis(giroSub, Vector3.up);
            for (int s = 0; s < sub; s++)
            {
                relogio += h;
                float f0 = s / (float)sub, f = (s + 1f) / sub;
                Vector3 pivoSub = Vector3.Lerp(pivoAntes, pivoAnterior, f0);
                for (int r = 0; r < R; r++)
                {
                    float k = r / (float)(R - 1);
                    float rigidez = Mathf.Lerp(cfg.rigidezTopo, cfg.rigidezBarra, Mathf.Pow(Mathf.InverseLerp(1, R - 1, r), cfg.curvaRigidez));
                    float arrasto = Mathf.Lerp(cfg.arrastoTopo, cfg.arrastoBarra, k);
                    for (int c = 0; c < C; c++)
                    {
                        int i = I(c, r);
                        Vector3 alvoAntes = Vector3.Lerp(alvoAnterior[i], alvoAtual[i], f0), alvoP = Vector3.Lerp(alvoAnterior[i], alvoAtual[i], f);
                        if (r == 0) { ant[i] = pos[i]; pos[i] = alvoP; continue; }
                        pos[i] = pivoSub + rot * (pos[i] - pivoSub) + carga;
                        ant[i] = pivoSub + rot * (ant[i] - pivoSub) + carga;
                        Vector3 propria = (pos[i] - ant[i]) / h;                       // velocidade além da que o corpo carrega
                        Vector3 noMundo = propria + carga / h;                         // contra o ar (o giro carregado é desprezado)
                        Vector3 relativa = noMundo - (alvoP - alvoAntes) / h;          // em relação a onde o pano deveria estar
                        // rajadas pequenas: dão vida parado e viram ondulação quando ele corre
                        float fase = relogio * 4.3f + c * 1.9f + r * .8f;
                        Vector3 vento = giroAnterior * new Vector3(Mathf.Sin(fase) * .9f, Mathf.Sin(fase * .63f + 1.7f) * .25f, Mathf.Cos(fase * .81f) * .6f)
                            * (cfg.vento * k * (1 + Mathf.Min(noMundo.magnitude, 10) * .3f));
                        // de lado o pano balança como pêndulo (rigidez baixa na barra); para cima ele não fica: se foi
                        // levantado acima de onde pende, a gravidade o traz de volta depressa
                        Vector3 falta = alvoP - pos[i];
                        float sobe = Mathf.Clamp01(-falta.y / .12f);
                        Vector3 acel = new Vector3(falta.x * rigidez, falta.y * Mathf.Max(rigidez, cfg.rigidezVertical), falta.z * rigidez)
                            - arrasto * (noMundo - vento) - cfg.amortecimento * relativa + Vector3.down * (cfg.gravidade * sobe);
                        Vector3 novo = pos[i] + propria * h + acel * (h * h);
                        ant[i] = pos[i]; pos[i] = novo;
                    }
                }
                for (int it = 0; it < 3; it++)
                {
                    // comprimento ao longo da queda: o nó de baixo obedece ao de cima
                    for (int r = 1; r < R; r++) for (int c = 0; c < C; c++)
                    {
                        int i = I(c, r), j = I(c, r - 1);
                        Vector3 d = pos[i] - pos[j]; float m = d.magnitude, alvoM = compV[i] * escala;
                        if (m > 1e-5f) pos[i] = pos[j] + d * (alvoM / m);
                    }
                    // largura: mantém as pregas sem deixar o pano esticar nem amontoar
                    if (C > 1 && cfg.rigidezLateral > 0)
                        for (int r = 1; r < R; r++) for (int c = 1; c < C; c++)
                        {
                            int i = I(c, r), j = I(c - 1, r);
                            Vector3 d = pos[i] - pos[j]; float m = d.magnitude, alvoM = compH[i] * escala;
                            if (m < 1e-5f) continue;
                            Vector3 corr = d * ((m - alvoM) / m * .5f * cfg.rigidezLateral);
                            pos[i] -= corr; pos[j] += corr;
                        }
                    if (cfg.colisoes) Colidir();
                }
            }
            Escrever();
        }

        void Colidir()
        {
            for (int k = 0; k < capsulas.Count; k++)
            {
                var cap = capsulas[k];
                Vector3 a = cap.a.position, b = cap.b.position, ab = b - a;
                float l2 = ab.sqrMagnitude, escala = cap.a.lossyScale.x;
                for (int i = C; i < pos.Length; i++)
                {
                    float raio = cap.raioNo[i] * escala;
                    if (raio <= 0) continue;
                    float t = l2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(pos[i] - a, ab) / l2) : 0;
                    Vector3 d = pos[i] - (a + ab * t); float m = d.magnitude;
                    if (m >= raio || m < 1e-6f) continue;
                    // só a posição: a velocidade sai sozinha da diferença de posições (o pano acompanha o corpo que o empurra)
                    pos[i] += d * ((raio - m) / m);
                }
            }
            if (!float.IsNegativeInfinity(chao))
                for (int i = C; i < pos.Length; i++) if (pos[i].y < chao + .03f) { pos[i].y = chao + .03f; ant[i] = Vector3.Lerp(ant[i], pos[i], .5f); }
        }

        void Escrever()
        {
            for (int r = 0; r < R; r++) for (int c = 0; c < C; c++)
            {
                int i = I(c, r);
                ossos[i].SetPositionAndRotation(pos[i], Quadro(pos, c, r, giroAnterior * Vector3.right) * Quaternion.Inverse(quadroRepouso[i]));
            }
        }

        void OnDestroy()
        {
            if (malha) Destroy(malha);
            if (recipiente) Destroy(recipiente.gameObject);
        }
    }
}
