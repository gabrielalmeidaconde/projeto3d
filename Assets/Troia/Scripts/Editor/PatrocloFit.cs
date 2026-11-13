using System;
using System.Collections.Generic;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>
    /// Peças do Pátroclo feitas sob medida para o corpo do MakeHuman (pose de repouso, metros,
    /// frente +Z, direita do personagem em +X). Cada método devolve geometria pronta para o SkinBuilder.
    /// </summary>
    public class PatrocloFit
    {
        readonly BodyInfo b;
        /// <summary>
        /// Malha visível do corpo (subdividida). Usada para peças finas e rentes à pele (correias, tiras):
        /// recortadas da malha base, faixas mais estreitas que os polígonos dela saem pela metade.
        /// </summary>
        readonly BodyInfo hi;
        public PatrocloFit(BodyInfo body, BodyInfo visible = null) { b = body; hi = visible ?? body; }

        public static readonly string[] Sides = { "R", "L" };
        public static float Sgn(string side) => side == "R" ? 1f : -1f;

        public static float Smooth01(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        static float SMax(float a, float c, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - c), 0f) / k;
            return Mathf.Max(a, c) + h * h * k * 0.25f;
        }

        static float SMin(float a, float c, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - c), 0f) / k;
            return Mathf.Min(a, c) - h * h * k * 0.25f;
        }

        Func<int, bool> Dom(HashSet<int> set, Func<Vector3, bool> extra = null) =>
            v => set.Contains(b.dom[v]) && (extra == null || extra(b.p[v]));

        static Func<Vector3, float> DistTo(IList<Vector3> pts) => q =>
        {
            float m = float.MaxValue;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = (pts[i] - q).sqrMagnitude;
                if (d < m) m = d;
            }
            return Mathf.Sqrt(m);
        };

        /// <summary>Pontos de todos os contornos (bordas abertas) de uma casca.</summary>
        public static Vector3[][] LoopPoints(Shell s)
        {
            var outp = new List<Vector3[]>();
            foreach (var l in s.Loops()) if (l.Length > 6) outp.Add(Pts(s, l));
            return outp.ToArray();
        }

        static Vector3[] Pts(Shell s, int[] loop)
        {
            var r = new Vector3[loop.Length];
            for (int i = 0; i < loop.Length; i++) r[i] = s.p[loop[i]];
            return r;
        }

        static Vector3 Mean(Vector3[] pts)
        {
            Vector3 m = Vector3.zero;
            foreach (var q in pts) m += q;
            return m / Mathf.Max(1, pts.Length);
        }

        // =====================================================================================
        // Ruído para o desgaste (comprimentos desiguais, barras esfarrapadas, rasgos)
        // =====================================================================================
        static float Hash(float x)
        {
            float s = Mathf.Sin(x * 127.1f + 311.7f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        /// <summary>Ruído suave 1D em [-1, 1].</summary>
        static float Noise(float x, float seed) => Mathf.PerlinNoise(x + 100f, seed * 13.7f + 0.5f) * 2f - 1f;

        /// <summary>Soma de três oitavas de ruído 1D, aproximadamente em [-1, 1].</summary>
        static float Fbm(float x, float seed) => Noise(x, seed) * 0.6f + Noise(x * 2.7f, seed + 3f) * 0.3f + Noise(x * 7.1f, seed + 7f) * 0.1f;

        /// <summary>Ruído suave em [-1, 1] ao redor do corpo (periódico no ângulo, sem emenda).</summary>
        static float NoiseRing(float thDeg, float freq, float seed)
        {
            float a = thDeg * Mathf.Deg2Rad;
            return Mathf.PerlinNoise(Mathf.Cos(a) * freq + 50f + seed * 7.3f, Mathf.Sin(a) * freq + 50f + seed * 3.1f) * 2f - 1f;
        }

        // rasgos em V que sobem da barra: (posição, meia-largura na barra, altura, inclinação)
        static readonly (float u, float w, float h, float lean)[] CapeRips =
        {
            (0.10f, 0.028f, 0.20f, 0.02f), (0.27f, 0.02f, 0.36f, -0.015f), (0.47f, 0.032f, 0.13f, 0.01f),
            (0.66f, 0.02f, 0.27f, 0.02f), (0.84f, 0.026f, 0.44f, -0.03f), (0.94f, 0.016f, 0.10f, 0f),
        };
        // buracos: (centro u, centro t, raio em u, raio em t)
        static readonly (float u, float t, float ru, float rt)[] CapeHoles =
        {
            (0.38f, 0.70f, 0.016f, 0.024f), (0.60f, 0.83f, 0.022f, 0.015f), (0.75f, 0.60f, 0.010f, 0.018f), (0.18f, 0.86f, 0.012f, 0.012f),
        };

        /// <summary>
        /// Capa de batalha: barra esfarrapada, rasgos em V, buracos e um canto arrancado. Positivo = pano.
        /// u vai de 0 (lado esquerdo do personagem) a 1; t de 0 (ombros) a 1 (barra); 1 unidade ≈ 1,25 m.
        /// </summary>
        static float CapeTear(float u, float t)
        {
            float f = 0.972f - 0.03f * (Fbm(u * 7f, 1f) * 0.5f + 0.5f) - 0.012f * Mathf.Abs(Noise(u * 55f, 2f)) - t;
            foreach (var (u0, w0, h, lean) in CapeRips)
            {
                float s = (1f - t) / h;
                if (s >= 1f) continue;
                float uc = u0 + lean * s + 0.006f * Noise(s * 6f, u0 * 31f);
                float w = w0 * Mathf.Pow(Mathf.Max(0f, 1f - s), 0.85f) * (1f + 0.25f * Noise(t * 40f, u0 * 17f));
                f = Mathf.Min(f, Mathf.Abs(u - uc) - w);
            }
            foreach (var (u0, t0, ru, rt) in CapeHoles)
            {
                float du = (u - u0) / ru, dt = (t - t0) / rt;
                float ang = Mathf.Atan2(dt, du);
                float edge = 1f + 0.4f * Noise(ang * 1.6f, u0 * 9f) + 0.22f * Noise(ang * 4.3f, u0 * 5f);
                f = Mathf.Min(f, (Mathf.Sqrt(du * du + dt * dt) - edge) * Mathf.Min(ru, rt));
            }
            // canto de baixo do lado direito arrancado na diagonal
            f = Mathf.Min(f, ((1f - u) / 0.075f + (1f - t) / 0.2f - 1f - 0.12f * Noise((u + t) * 30f, 5f)) * 0.06f);
            return f;
        }

        // rasgos da barra da saia: (ângulo em graus, meia-largura na barra em graus, altura em t, inclinação em graus).
        // A altura fica abaixo de 0,22 (6,5 cm): acima disso o corpo "vestido" não tem polígonos.
        static readonly (float th, float w, float h, float lean)[] SkirtRips =
        {
            (152f, 5.5f, 0.21f, 2f), (-160f, 4f, 0.14f, -1.5f), (96f, 5f, 0.2f, -2f), (-72f, 5f, 0.17f, 2f), (18f, 4.5f, 0.15f, 1f), (-118f, 3.5f, 0.12f, 0f),
        };

        /// <summary>Barra esfarrapada e rasgos da saia: positivo = pano. th em graus (0 = costas), t de 0 (cós) a 1 (barra).</summary>
        static float SkirtTear(float th, float t)
        {
            float f = 0.985f - 0.035f * (NoiseRing(th, 2.2f, 1f) * 0.5f + 0.5f) - 0.014f * Mathf.Abs(NoiseRing(th, 9f, 2f)) - t;
            foreach (var (th0, w0, h, lean) in SkirtRips)
            {
                float s = (1f - t) / h;
                if (s >= 1f) continue;
                float center = th0 + lean * s + 0.8f * Noise(s * 5f, th0);
                float w = w0 * Mathf.Pow(Mathf.Max(0f, 1f - s), 0.8f) * (1f + 0.25f * Noise(t * 30f, th0 * 0.37f));
                // graus -> mesma escala de t (a barra tem ~1,1 m de volta e a saia ~0,3 m de altura)
                f = Mathf.Min(f, (Mathf.Abs(Mathf.DeltaAngle(center, th)) - w) * 0.0102f);
            }
            return f;
        }

        // =====================================================================================
        // Camadas sem interseção: tecido que segue a pele e corpo escondido sob a roupa
        // =====================================================================================
        Func<int, bool> _hipL, _hipR;
        HashSet<int> _torsoSet, _hipSet;

        /// <summary>
        /// Pesos para tecido solto sobre o quadril e as pernas (saia, tiras, faixa): os da pele mais
        /// próxima, misturando esquerda e direita perto do meio. O pano acompanha a coxa quando a perna
        /// se move, então o corpo não atravessa a roupa nas poses.
        /// </summary>
        public BoneWeight ClothWeights(Vector3 v)
        {
            if (_hipL == null)
            {
                _hipL = Dom(b.Set("root", "spine05", "spine04", "pelvis.L", "upperleg01.L", "upperleg02.L", "lowerleg01.L", "lowerleg02.L"));
                _hipR = Dom(b.Set("root", "spine05", "spine04", "pelvis.R", "upperleg01.R", "upperleg02.R", "lowerleg01.R", "lowerleg02.R"));
            }
            float s = Smooth01(-0.045f, 0.045f, v.x); // +X = lado direito do personagem
            if (s <= 0.001f) return b.NearestWeights(v, _hipL);
            if (s >= 0.999f) return b.NearestWeights(v, _hipR);
            return Shell.LerpW(b.NearestWeights(v, _hipL), b.NearestWeights(v, _hipR), s);
        }

        void LegFrame(string side, out Vector3 K, out Vector3 a, out Vector3 ax, out Vector3 fwd)
        {
            K = b.Head("lowerleg01." + side);
            a = b.Head("foot." + side) - K;
            ax = a.normalized;
            fwd = (Vector3.forward - Vector3.Dot(Vector3.forward, ax) * ax).normalized;
        }

        /// <summary>
        /// O ponto do corpo fica sempre escondido sob a roupa (corselete, saia, cnêmides)? A malha "vestida"
        /// do corpo é gerada sem esses polígonos: nada de pele vazando pela roupa.
        /// </summary>
        public bool Hidden(Vector3 q, int domBone)
        {
            if (_torsoSet == null)
            {
                _torsoSet = b.Set("root", "spine0*", "breast*", "pelvis*", "clavicle*", "shoulder01*", "neck01", "upperarm01*", "upperleg01*", "upperleg02*");
                _hipSet = b.Set("root", "spine05", "spine04", "pelvis*", "upperleg01*", "upperleg02*");
            }
            float bottom = CuirassBottom(q) + 0.025f;
            if (_torsoSet.Contains(domBone) && q.y > bottom && q.y < 1.6f && (NeckMetric(q) > 1.32f || q.y < 1.4f)
                && ArmholeKeep(q, "L") > 0.022f && ArmholeKeep(q, "R") > 0.022f) return true;
            if (_hipSet.Contains(domBone) && q.y > 0.815f && q.y <= bottom) return true;
            foreach (var side in Sides)
            {
                if (domBone != b.B("upperleg02." + side) && domBone != b.B("lowerleg01." + side) && domBone != b.B("lowerleg02." + side)) continue;
                LegFrame(side, out var K, out var a, out var ax, out var fwd);
                Vector3 r = q - K;
                float t = Vector3.Dot(r, a) / a.sqrMagnitude;
                r -= Vector3.Dot(r, ax) * ax;
                float front = Vector3.Dot(r.normalized, fwd);
                if (front > -0.3f && t > -0.05f + 0.075f * (1f - front) && t < 0.82f) return true;
            }
            return false;
        }

        /// <summary>Divide uma casca em duas partes complementares (f ≥ 0 e f ≤ 0) que se encontram na mesma linha.</summary>
        public static void Split(Shell c, Func<Vector3, float> f, out Shell positive, out Shell negative)
        {
            positive = c.Clone().Clip((q, w) => f(q));
            negative = c.Clone().Clip((q, w) => -f(q));
        }

        /// <summary>Distância (m) ao contorno mais próximo de uma lista.</summary>
        public static Func<Vector3, float> DistToLoops(params Vector3[][] loops)
        {
            var all = new List<Vector3>();
            foreach (var l in loops) if (l != null) all.AddRange(l);
            return DistTo(all);
        }

        // =====================================================================================
        // Mãos
        // =====================================================================================
        public struct Hand
        {
            public Vector3 fingers, thumbSide, palm, knuckles, wrist;
        }

        public Hand HandFrame(string s)
        {
            Vector3 i1 = b.Head("finger2-1." + s), p1 = b.Head("finger5-1." + s);
            Vector3 bases = (i1 + b.Head("finger3-1." + s) + b.Head("finger4-1." + s) + p1) * 0.25f;
            Vector3 tips = (b.Tail("finger2-3." + s) + b.Tail("finger3-3." + s) + b.Tail("finger4-3." + s) + b.Tail("finger5-3." + s)) * 0.25f;
            Vector3 v = (tips - bases).normalized;
            Vector3 ts = i1 - p1;
            ts = (ts - Vector3.Dot(ts, v) * v).normalized;
            Vector3 palm = (s == "R" ? Vector3.Cross(v, ts) : Vector3.Cross(ts, v)).normalized;
            return new Hand { fingers = v, thumbSide = ts, palm = palm, knuckles = bases, wrist = b.Head("wrist." + s) };
        }

        /// <summary>Eixo de dobra de cada osso de dedo (no referencial de repouso = mundo).</summary>
        public Vector3 CurlAxis(string bone, string s)
        {
            var h = HandFrame(s);
            Vector3 dir = (b.Tail(bone) - b.Head(bone)).normalized;
            if (bone.StartsWith("finger1"))
            {
                // polegar dobra atravessando a palma
                Vector3 target = (h.palm * 0.7f - h.thumbSide * 0.7f).normalized;
                return Vector3.Cross(dir, target).normalized;
            }
            return Vector3.Cross(dir, h.palm).normalized;
        }

        // =====================================================================================
        // Couraça anatômica (duas chapas), faixas de ouro e medalhão
        // =====================================================================================
        public static float CuirassBottom(Vector3 q)
        {
            float ang = Mathf.Atan2(q.x, q.z - 0.02f);
            float c = Mathf.Cos(ang);
            return 1.035f - 0.042f * Mathf.Pow(Mathf.Max(c, 0f), 1.5f) - 0.01f * Mathf.Max(-c, 0f);
        }

        public static float NeckMetric(Vector3 q)
        {
            float x = q.x / 0.082f, z = (q.z - 0.035f) / 0.074f;
            return Mathf.Sqrt(x * x + z * z);
        }

        public float ArmholeKeep(Vector3 q, string side)
        {
            Vector3 J = b.Head("upperarm01." + side);
            Vector3 d = (b.Head("lowerarm01." + side) - J).normalized;
            Vector3 r = q - J;
            float al = Vector3.Dot(r, d);
            float rad = (r - al * d).magnitude;
            return SMax(-0.03f - al, rad - 0.1f, 0.012f);
        }

        /// <summary>
        /// Corselete de linho endurecido (linotórax): peça rígida, sem anatomia, um pouco mais solta embaixo.
        /// É a base do traje; escamas, epômides, disco do peito e barras vão por cima.
        /// </summary>
        public Shell Cuirass()
        {
            var torso = b.Set("root", "spine0*", "breast*", "pelvis*", "clavicle*", "shoulder01*", "neck01", "upperarm01*", "upperleg01*");
            var s = Shell.FromBody(b, Dom(torso, q => q.y > 0.93f && q.y < 1.66f));
            s.Clip((q, w) => q.y - CuirassBottom(q));
            // a gola é um cilindro elíptico vertical; só vale perto do pescoço. Sem o limite de altura ele
            // atravessa a cintura (mais fina que o pescoço em profundidade) e fura o corselete na lombar e no abdômen
            s.Clip((q, w) => SMax(NeckMetric(q) - 1.06f, (1.42f - q.y) / 0.08f, 0.15f));
            foreach (var side in Sides)
            {
                string sd = side;
                s.Clip((q, w) => ArmholeKeep(q, sd));
            }
            s.KeepLargest();
            s.Smooth(26);
            // ponte sobre o sulco da coluna: o corselete é rígido e passa de um lado ao outro das costas numa
            // curva convexa suave. Sem isso, ao afastar a casca do corpo os dois lados do sulco se dobram um
            // sobre o outro e a barra de baixo ganha um "nó" no meio das costas.
            const float bridgeW = 0.065f;
            var reference = s.Clone();
            // superfície de referência ao lado do sulco. Na última fileira o raio passa rente à borda das costas,
            // não acerta nada ali e segue até a FRENTE do corselete: só vale acerto nas costas (z negativo);
            // senão sobe alguns milímetros e tenta de novo.
            bool Side(float x, float y, out Vector3 hit)
            {
                for (int k = 0; k < 5; k++)
                    if (reference.Raycast(new Vector3(x, y + k * 0.007f, -1f), Vector3.forward, out hit, out _, out _) && hit.z < -0.02f) return true;
                hit = default;
                return false;
            }
            s.Displace((q, n) =>
            {
                if (q.z > -0.02f || Mathf.Abs(q.x) >= bridgeW) return q;
                if (!Side(-bridgeW, q.y, out var left) || !Side(bridgeW, q.y, out var right)) return q;
                float u = q.x / bridgeW;
                float zb = Mathf.Lerp(left.z, right.z, (u + 1f) * 0.5f) - 0.004f * (1f - u * u);
                return q.z > zb ? new Vector3(q.x, q.y, zb) : q;
            });
            s.Smooth(4);
            // afasta do corpo: 17 mm, abrindo mais 14 mm na barra. Perto da barra o afastamento é horizontal:
            // no baixo-ventre as normais apontam para baixo e empurrariam as últimas fileiras para além da barra.
            s.Displace((q, n) =>
            {
                float d = 0.017f + 0.014f * Mathf.Pow(Smooth01(1.07f, 0.99f, q.y), 1.5f);
                var nH = new Vector3(n.x, 0f, n.z);
                Vector3 dir = nH.sqrMagnitude > 1e-6f ? Vector3.Slerp(n, nH.normalized, Smooth01(1.16f, 1.06f, q.y)) : n;
                return q + dir.normalized * d;
            });
            // barra reta: nos últimos 5,5 cm a casca nunca fica mais para dentro que a seção logo acima (o corpo
            // curva para dentro no baixo-ventre) e abre mais 6 mm até a borda. Vira uma faixa limpa para a
            // barra carmesim, o meandro e as tiras que saem de baixo dela.
            const float hemH = 0.055f;
            var beforeHem = s.Clone();
            s.Displace((q, n) =>
            {
                float yc = CuirassBottom(q) + hemH;
                if (q.y >= yc) return q;
                var dirH = new Vector3(q.x, 0f, q.z);
                float r = dirH.magnitude;
                if (r < 1e-4f) return q;
                dirH /= r;
                if (!beforeHem.Raycast(new Vector3(0f, yc, 0f), dirH, out var above, out _, out _)) return q;
                float t = Mathf.Clamp01((yc - q.y) / hemH);
                float rr = Mathf.Max(r, new Vector2(above.x, above.z).magnitude + 0.006f * t);
                return new Vector3(dirH.x * rr, q.y, dirH.z * rr);
            });
            s.Smooth(3);
            s.Subdivide();
            s.Smooth(2);
            // a barra de baixo fica na altura desenhada: sobre o sulco da coluna o deslocamento pela normal
            // puxava um ponto para baixo e deixava um "dente" no meio das costas
            s.Displace((q, n) =>
            {
                float yb = CuirassBottom(q) - 0.003f;
                return q.y < yb ? new Vector3(q.x, yb, q.z) : q;
            });
            return s;
        }

        /// <summary>
        /// Painel de escamas de bronze em volta da cintura. Cada fileira é um anel achado por raios lançados
        /// do eixo do tronco contra o corselete (assenta exatamente na superfície) e as escamas são
        /// distribuídas por comprimento de arco, defasadas meia escama a cada fileira; a de cima cobre o
        /// topo da de baixo.
        /// </summary>
        public void Scales(SkinBuilder sb, Shell corselet, Vector3[] bottomEdge, float above, int rows, float rowStep)
        {
            const float pitch = 0.0232f, halfW = 0.0126f;
            const int NS = 144;
            for (int k = 0; k < rows; k++)
            {
                var ring = new List<Vector3>();
                var rn = new List<Vector3>();
                var rw = new List<BoneWeight>();
                for (int i = 0; i < NS; i++)
                {
                    float th = -180f + 360f * i / NS, a = th * Mathf.Deg2Rad;
                    float y = EdgeAt(bottomEdge, th, out _) + above + k * rowStep;
                    if (!corselet.Raycast(new Vector3(0f, y, 0f), new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)), out var hp, out var hn, out var hw)) continue;
                    ring.Add(hp);
                    rn.Add(hn);
                    rw.Add(hw);
                }
                int n = ring.Count;
                if (n < 8) continue;
                var cum = new float[n + 1];
                for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + Vector3.Distance(ring[i], ring[(i + 1) % n]);
                float L = cum[n];
                int count = Mathf.Max(8, Mathf.RoundToInt(L / pitch));
                int seg = 0;
                for (int j = 0; j < count; j++)
                {
                    float sArc = (j + 0.5f * (k % 2)) * L / count;
                    if (sArc >= L) sArc -= L;
                    if (sArc < cum[seg]) seg = 0;
                    while (seg < n - 1 && cum[seg + 1] < sArc) seg++;
                    float f = (sArc - cum[seg]) / Mathf.Max(1e-6f, cum[seg + 1] - cum[seg]);
                    int i1 = (seg + 1) % n;
                    Vector3 pos = Vector3.Lerp(ring[seg], ring[i1], f);
                    Vector3 nrm = Vector3.Lerp(rn[seg], rn[i1], f).normalized;
                    var w = f < 0.5f ? rw[seg] : rw[i1];
                    Vector3 up = (Vector3.up - nrm * nrm.y).normalized;
                    Vector3 tg = Vector3.Cross(up, nrm).normalized;
                    var mb = new MeshBuilder();
                    const int U = 5, Vn = 7;
                    var o = new Vector3[Vn, U];
                    var on = new Vector3[Vn, U];
                    var ii = new Vector3[Vn, U];
                    var inn = new Vector3[Vn, U];
                    for (int a = 0; a < Vn; a++)
                    {
                        float v = a / (float)(Vn - 1);
                        float hw2 = v < 0.5f ? halfW : halfW * Mathf.Sqrt(Mathf.Max(0.02f, 1f - Mathf.Pow((v - 0.5f) / 0.5f, 2f)));
                        for (int c = 0; c < U; c++)
                        {
                            float x = -1f + 2f * c / (U - 1);
                            // base 2 mm acima do linho, ponta um pouco levantada, nervura central e bordas caídas
                            float lift = 0.002f + 0.003f * v + 0.0017f * (1f - x * x) + 0.0006f * (1f - Mathf.Abs(x));
                            Vector3 q = pos + up * (0.012f - v * 0.031f) + tg * (x * hw2) + nrm * lift;
                            Vector3 nn = (nrm - tg * (x * 0.38f) + up * 0.1f).normalized;
                            o[a, c] = q;
                            on[a, c] = nn;
                            ii[a, c] = q - nrm * 0.0012f;
                            inn[a, c] = -nn;
                        }
                    }
                    mb.Grid(o, on, M.PBronze);
                    mb.Grid(ii, inn, M.PBronzeDark);
                    mb.Push(pos + up * 0.0065f + nrm * 0.0042f, Quaternion.LookRotation(nrm, up));
                    mb.Sphere(Vector3.zero, 0.0021f, 0.0014f, 6, M.PGold);
                    mb.Pop();
                    var ww = w;
                    sb.AddBuilder(mb, _ => ww);
                }
            }
        }

        /// <summary>Contornos do corselete: [0] barra de baixo, [1] gola, [2] e [3] cavas dos braços.</summary>
        public Vector3[][] CorseletLoops(Shell c)
        {
            var loops = new List<Vector3[]>();
            foreach (var l in c.Loops()) if (l.Length > 8) loops.Add(Pts(c, l));
            Vector3[] bottom = null, neck = null;
            float yMin = float.MaxValue, best = float.MaxValue;
            foreach (var pts in loops)
            {
                var m = Mean(pts);
                if (m.y < yMin) { yMin = m.y; bottom = pts; }
                if (m.y > 1.3f && Mathf.Abs(m.x) < best) { best = Mathf.Abs(m.x); neck = pts; }
            }
            var outp = new List<Vector3[]> { bottom, neck };
            foreach (var pts in loops)
            {
                if (pts == bottom || pts == neck) continue;
                var m = Mean(pts);
                if (Mathf.Abs(m.x) > 0.12f && m.y > 1.25f) outp.Add(pts);
            }
            return outp.ToArray();
        }

        /// <summary>
        /// Meandro grego (gancho sobre linha de base) em tubos de ouro, correndo ao longo de um contorno a uma
        /// distância 'dist' dele, sobre a superfície 'c'. O contorno é suavizado e reamostrado (tangentes e
        /// normais estáveis) e cada ponto do desenho é projetado na casca por um raio lançado do eixo do tronco.
        /// </summary>
        public static void Meander(SkinBuilder sb, Shell c, Vector3[] loop, float dist, float halfH, float period, float radius, M m, Vector3 into)
        {
            int n0 = loop.Length;
            var lp = (Vector3[])loop.Clone();
            var ln = new Vector3[n0];
            var lw = new BoneWeight[n0];
            for (int i = 0; i < n0; i++)
            {
                int k = c.Closest(lp[i]);
                ln[i] = c.n[k];
                lw[i] = c.w[k];
            }
            SkinBuilder.SmoothLoop(ref lp, ref ln, 2, 8);
            SkinBuilder.ResampleLoop(lp, ln, lw, 0.004f, out var rp, out var rn, out var rw);
            int n = rp.Length;
            var cum = new float[n + 1];
            for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + Vector3.Distance(rp[i], rp[(i + 1) % n]);
            float L = cum[n];
            int cells = Mathf.Max(4, Mathf.RoundToInt(L / period));
            float per = L / cells;
            Vector3 At(float sArc, float h, out Vector3 nrm, out BoneWeight w)
            {
                sArc = Mathf.Repeat(sArc, L);
                int i = Mathf.Clamp(Mathf.FloorToInt(sArc / L * n), 0, n - 1);
                while (i < n - 1 && cum[i + 1] < sArc) i++;
                while (i > 0 && cum[i] > sArc) i--;
                int j = (i + 1) % n;
                float t = (sArc - cum[i]) / Mathf.Max(1e-6f, cum[i + 1] - cum[i]);
                Vector3 a = Vector3.Lerp(rp[i], rp[j], t);
                Vector3 nn = Vector3.Lerp(rn[i], rn[j], t).normalized;
                Vector3 tg = (rp[(i + 2) % n] - rp[(i + n - 1) % n]).normalized;
                Vector3 side = Vector3.Cross(nn, tg).normalized;
                if (Vector3.Dot(side, into) < 0f) side = -side;
                Vector3 q = a + side * (dist + h);
                w = t < 0.5f ? rw[i] : rw[j];
                nrm = nn;
                // assenta na casca: raio horizontal a partir do eixo do tronco
                Vector3 dir = new Vector3(q.x, 0f, q.z).normalized;
                if (c.Raycast(new Vector3(0f, q.y, 0f), dir, out var hit, out var hn, out _)) { q = hit; nrm = hn; }
                return q + nrm * (radius * 0.55f);
            }
            void Tube(Vector2[] shape, float cell, bool closed)
            {
                var pts = new Vector3[shape.Length];
                var nrms = new Vector3[shape.Length];
                var ws = new BoneWeight[shape.Length];
                for (int i = 0; i < shape.Length; i++) pts[i] = At((cell + shape[i].x) * per, shape[i].y * halfH, out nrms[i], out ws[i]);
                sb.AddTube(pts, nrms, i => ws[i], 0f, radius, m, 5, closed);
            }
            // linha de base contínua
            var baseLine = new Vector2[cells * 4];
            for (int i = 0; i < baseLine.Length; i++) baseLine[i] = new Vector2(i / 4f, -1f);
            Tube(baseLine, 0f, true);
            // ganchos
            var hook = new[] { new Vector2(0.1f, -1f), new Vector2(0.1f, 1f), new Vector2(0.85f, 1f), new Vector2(0.85f, -0.4f), new Vector2(0.4f, -0.4f), new Vector2(0.4f, 0.4f) };
            for (int k = 0; k < cells; k++) Tube(hook, k, false);
        }

        /// <summary>
        /// Corte de lâmina no linho do corselete: fenda escura em forma de lente com as duas beiras levantadas e
        /// desfiadas. É uma peça fina assentada na superfície por raios (não um furo de verdade), então pode
        /// ser mais estreita que os polígonos da casca. (th, y) em graus/metros nas duas pontas.
        /// </summary>
        public void Slash(SkinBuilder sb, Shell corselet, float th0, float y0, float th1, float y1, float width)
        {
            const int N = 22;
            var pts = new Vector3[N];
            var nrm = new Vector3[N];
            var ws = new BoneWeight[N];
            for (int i = 0; i < N; i++)
            {
                float s = i / (float)(N - 1);
                float a = Mathf.LerpAngle(th0, th1, s) * Mathf.Deg2Rad;
                float y = Mathf.Lerp(y0, y1, s) + 0.002f * Noise(s * 3f, th0);
                if (!corselet.Raycast(new Vector3(0f, y, 0f), new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)), out pts[i], out nrm[i], out ws[i])) return;
            }
            var mid = new Vector3[N, 3];
            var midN = new Vector3[N, 3];
            var lipA = new Vector3[N];
            var lipB = new Vector3[N];
            for (int i = 0; i < N; i++)
            {
                float s = i / (float)(N - 1);
                Vector3 tg = (pts[Mathf.Min(i + 1, N - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(nrm[i], tg).normalized;
                // Sin(π) em float é um negativo minúsculo: sem o Max, o Pow devolve NaN e a malha inteira é descartada
                float hw = width * 0.5f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * s)), 0.6f) * (1f + 0.25f * Noise(s * 9f, y0 * 7f));
                for (int c = 0; c < 3; c++)
                {
                    mid[i, c] = pts[i] + side * ((c - 1) * hw) + nrm[i] * 0.0005f;
                    midN[i, c] = nrm[i];
                }
                lipA[i] = pts[i] - side * (hw + 0.0004f) + nrm[i] * 0.0006f;
                lipB[i] = pts[i] + side * (hw + 0.0004f) + nrm[i] * 0.0006f;
            }
            var mb = new MeshBuilder();
            mb.Grid(mid, midN, M.PCut);
            sb.AddBuilder(mb, v =>
            {
                int best = 0;
                float bd = float.MaxValue;
                for (int i = 0; i < N; i++)
                {
                    float d = (pts[i] - v).sqrMagnitude;
                    if (d < bd) { bd = d; best = i; }
                }
                return ws[best];
            });
            sb.AddTube(lipA, nrm, i => ws[i], 0f, 0.0016f, M.PLinen, 5, false);
            sb.AddTube(lipB, nrm, i => ws[i], 0f, 0.0016f, M.PLinen, 5, false);
        }

        static float FlapLow(float z) => z > 0.03f ? 1.375f : z > -0.03f ? Mathf.Lerp(1.3f, 1.375f, (z + 0.03f) / 0.06f) : 1.3f;

        /// <summary>
        /// Epômide (aba de ombro do linotórax): recortada do próprio corselete e afastada 9 mm dele, então
        /// as duas superfícies são paralelas e nunca se cruzam. Sai das costas, passa por cima do ombro e
        /// desce no peito.
        /// </summary>
        public Shell ShoulderFlap(Shell corselet, string side)
        {
            float sx = Sgn(side);
            var s = corselet.Clone();
            s.Clip((q, w) => SMin(sx * q.x - 0.096f, 0.216f - sx * q.x, 0.012f));
            s.Clip((q, w) => q.y - FlapLow(q.z));
            s.KeepLargest();
            s.Offset(0.009f);
            return s;
        }

        /// <summary>Cinturão (zoster): anel do corselete entre duas distâncias da barra, afastado por cima das escamas.</summary>
        public Shell Belt(Shell corselet, Vector3[] bottomEdge, float d0, float d1)
        {
            var dist = DistTo(bottomEdge);
            var s = corselet.Clone().Clip((q, w) => Mathf.Min(dist(q) - d0, d1 - dist(q)));
            s.KeepLargest();
            s.Offset(0.0085f);
            return s;
        }

        public static UvProj TorsoUv(bool front) =>
            UvProj.Cylinder(new Vector3(0f, 0f, 0.02f), Vector3.up, front ? Vector3.forward : Vector3.back, 0.16f);

        /// <summary>Contornos da couraça: [0] = barra de baixo, [1] = gola.</summary>
        public Vector3[][] CuirassEdges(Shell cuirass)
        {
            Vector3[] bottom = null, neck = null;
            float yMin = float.MaxValue, best = float.MaxValue;
            foreach (var l in cuirass.Loops())
            {
                var pts = Pts(cuirass, l);
                var m = Mean(pts);
                if (m.y < yMin) { yMin = m.y; bottom = pts; }
                if (m.y > 1.3f && Mathf.Abs(m.x) < best) { best = Mathf.Abs(m.x); neck = pts; }
            }
            return new[] { bottom, neck };
        }

        public static void Medal(MeshBuilder mb, Vector3 pos, Vector3 normal)
        {
            mb.Push(pos, Quaternion.LookRotation(normal, Vector3.up));
            mb.CylinderBetween(new Vector3(0f, 0f, -0.004f), new Vector3(0f, 0f, 0.006f), 0.031f, 0.029f, 24, M.PGold);
            mb.Torus(new Vector3(0f, 0f, 0.006f), Quaternion.Euler(90f, 0f, 0f), 0.024f, 0.0035f, 24, 6, M.PGold);
            mb.Sphere(new Vector3(0f, 0f, 0.0035f), 0.014f, 0.0065f, 12, M.PGold);
            for (int k = 0; k < 10; k++)
            {
                float a = k * Mathf.PI * 2f / 10f;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                mb.BoxBetween(d * 0.012f + Vector3.forward * 0.006f, d * 0.021f + Vector3.forward * 0.0075f, 0.003f, 0.002f, M.PGold);
            }
            mb.Pop();
        }

        // =====================================================================================
        // Roupa: quíton de linho, ptéruges, capa
        // =====================================================================================
        /// <summary>
        /// Saia do quíton de linho: cai solta do quadril até o meio da coxa (sem entreperna), com pregas
        /// finas, dobras largas e barra. Cada coluna desce pelo contorno máximo do quadril e das coxas,
        /// então o pano passa reto por cima das nádegas e das duas coxas.
        /// </summary>
        Vector3[,] _skirt;
        float[] _skirtTop;
        const float SkirtBot = 0.735f;

        /// <summary>Altura e raio da borda de baixo da couraça no ângulo th (graus, 0 = costas).</summary>
        /// <summary>Altura da borda de baixo do corselete no ângulo th (graus, 0 = costas).</summary>
        public static float EdgeHeight(Vector3[] edge, float th) => EdgeAt(edge, th, out _);

        static float EdgeAt(Vector3[] edge, float th, out float rEdge)
        {
            float best = float.MaxValue;
            Vector3 bp = edge[0];
            foreach (var e in edge)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(e.x, -e.z) * Mathf.Rad2Deg, th));
                if (d < best) { best = d; bp = e; }
            }
            rEdge = new Vector2(bp.x, bp.z).magnitude;
            return bp.y;
        }

        Shell _hipShell;

        /// <summary>Raio da pele visível do quadril (m) num ângulo (graus, 0 = costas) e altura; -1 se não achar.</summary>
        float HipRadius(float thDeg, float y)
        {
            if (_hipShell == null)
            {
                var set = b.Set("root", "spine0*", "pelvis*", "upperleg01*", "upperleg02*");
                _hipShell = Shell.FromBody(hi, v => set.Contains(hi.dom[v]) && hi.p[v].y > 0.6f && hi.p[v].y < 1.2f);
            }
            float a = thDeg * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a));
            return _hipShell.Raycast(new Vector3(0f, y, 0f), dir, out var hit, out _, out _, true) ? new Vector2(hit.x, hit.z).magnitude : -1f;
        }

        /// <param name="loose">Versão usada sem a armadura: o cós assenta justo na pele do quadril (a versão normal
        /// entra recuada por dentro do corselete, e ali o corpo "vestido" não tem polígonos).</param>
        public void Skirt(SkinBuilder sb, Vector3[] cuirassEdge, bool loose = false)
        {
            const float zc = 0.0f, y0 = 0.6f, dy = 0.02f, dA = 3f;
            const int ny = 30, na = 121;
            var hull = TorsoHull(y0, dy, ny, dA, na, zc, out _);
            const int rows = 22, cols = 145, hemRows = 3;
            const float yBot = SkirtBot;
            var p = new Vector3[rows, cols];
            var tops = new float[cols];
            for (int c = 0; c < cols; c++)
            {
                float th = -180f + 360f * c / (cols - 1);
                // começa um pouco acima da borda do corselete, por dentro dele
                float yEdge = EdgeAt(cuirassEdge, th, out float rEdge);
                float yTop = yEdge + 0.012f;
                tops[c] = yTop;
                int topIdx = Mathf.Clamp(Mathf.RoundToInt((yTop - y0) / dy), 0, ny - 1);
                int ai = Mathf.RoundToInt(th / dA) + na / 2;
                float run = 0f;
                for (int r = 0; r < rows; r++)
                {
                    float t = r / (float)(rows - 1);
                    float y = Mathf.Lerp(yTop, yBot, t);
                    int yi = Mathf.Clamp(Mathf.RoundToInt((y - y0) / dy), 0, ny - 1);
                    for (int k = yi; k <= topIdx; k++)
                        for (int da = -2; da <= 2; da++)
                            run = Mathf.Max(run, hull[k, (ai + da + na) % na]);
                    float a = th * Mathf.Deg2Rad;
                    float pleat = (0.0012f + 0.005f * t) * Mathf.Sin(a * 44f);
                    float fold = 0.009f * Mathf.Pow(t, 1.4f) * Mathf.Sin(a * 7f + 0.7f) + 0.005f * t * Mathf.Sin(a * 13f + 2.1f);
                    float rr = run + 0.007f + 0.036f * Mathf.Pow(t, 1.3f) + pleat + fold;
                    if (loose)
                    {
                        // cós justo: nas primeiras fileiras o pano assenta na pele e só depois abre no caimento
                        float br = t < 0.3f ? HipRadius(th, y) : -1f;
                        if (br > 0f)
                        {
                            float snug = br + 0.0065f + pleat;
                            rr = Mathf.Max(snug, Mathf.Lerp(snug, rr, Smooth01(0f, 0.3f, t)));
                        }
                    }
                    // dentro do corselete o pano fica colado: sobra espaço para as duas camadas de ptéruges
                    else if (y > yEdge - 0.006f) rr = Mathf.Min(rr, rEdge - 0.0205f);
                    // barra levemente mais curta na frente e atrás, como tecido preso entre as pernas
                    float yy = y + 0.012f * t * Mathf.Cos(2f * a) * 0.5f;
                    p[r, c] = new Vector3(Mathf.Sin(a) * rr, yy, zc - Mathf.Cos(a) * rr);
                }
            }
            if (!loose)
            {
                // a saia da armadura é a referência para as tiras, a faixa e o cinto assentarem por cima
                _skirt = p;
                _skirtTop = tops;
            }
            var nrm = new Vector3[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int cl = c == 0 ? cols - 2 : c - 1, cr = c == cols - 1 ? 1 : c + 1;
                    Vector3 du = p[r, cr] - p[r, cl];
                    Vector3 dv = p[Mathf.Min(r + 1, rows - 1), c] - p[Mathf.Max(r - 1, 0), c];
                    Vector3 n = Vector3.Cross(dv, du).normalized;
                    if (Vector3.Dot(n, new Vector3(p[r, c].x, 0f, p[r, c].z - zc)) < 0f) n = -n;
                    nrm[r, c] = n;
                }
            // O pano vira uma casca para poder rasgar com recortes limpos: barra esfarrapada e rasgos.
            var cloth = Shell.FromGrid(p, default, new Vector3(0f, 0.9f, zc));
            for (int i = 0; i < cloth.p.Count; i++) cloth.w[i] = ClothWeights(cloth.p[i]);
            // (ângulo, t) de um ponto do pano; t vai de 0 no cós a 1 na barra
            void AngT(Vector3 q, out float th, out float t)
            {
                th = Mathf.Atan2(q.x, -(q.z - zc)) * Mathf.Rad2Deg;
                float fc = Mathf.Repeat(th + 180f, 360f) / 360f * (cols - 1);
                int c0 = Mathf.Clamp(Mathf.FloorToInt(fc), 0, cols - 2);
                float top = Mathf.Lerp(tops[c0], tops[c0 + 1], fc - c0);
                t = (top - q.y) / (top - yBot - 0.006f * Mathf.Cos(2f * th * Mathf.Deg2Rad));
            }
            cloth.Clip((q, w) => { AngT(q, out float th, out float t); return SkirtTear(th, t); });
            cloth.KeepLargest();
            // linho em cima, filete de ouro e barra de lã carmesim embaixo: partes complementares da mesma casca
            float tBand = 1f - hemRows / (float)(rows - 1);
            Split(cloth, q => { AngT(q, out _, out float t); return tBand - 0.017f - t; }, out var linen, out var lower);
            Split(lower, q => { AngT(q, out _, out float t); return tBand - t; }, out var stripe, out var band);
            var uv = UvProj.Cylinder(new Vector3(0f, 0f, zc), Vector3.up, Vector3.forward, 0.17f);
            sb.AddSolid(linen, M.PLinen, M.PLinen, uv, 0.0022f);
            if (stripe.VertexCount > 0) sb.AddSolid(stripe, M.PGold, M.PGold, uv, 0.0022f);
            if (band.VertexCount > 0) sb.AddSolid(band, M.PCrimsonDark, M.PCrimsonDark, uv, 0.0022f);
            // beira crua: fios claros desfiados em todo o contorno, inclusive nos rasgos
            sb.AddRims(cloth, 0.0022f, 0.0019f, M.PLinen, 5, 0.03f);
            if (loose)
            {
                // sem a armadura o cós aparece: bainha enrolada mais grossa
                var waist = new Vector3[cols - 1];
                var waistN = new Vector3[cols - 1];
                for (int c = 0; c < cols - 1; c++) { waist[c] = p[0, c]; waistN[c] = nrm[0, c]; }
                sb.AddTube(waist, waistN, i => ClothWeights(waist[i]), 0f, 0.004f, M.PLinen, 6, true);
            }
        }

        /// <summary>Raio externo da saia (m) num ângulo (graus, 0 = costas) e altura; -1 sem saia.</summary>
        public float SkirtRadius(float thDeg, float y)
        {
            if (_skirt == null) return -1f;
            int rows = _skirt.GetLength(0), cols = _skirt.GetLength(1);
            float fc = Mathf.Repeat(thDeg + 180f, 360f) / 360f * (cols - 1);
            int c0 = Mathf.Clamp(Mathf.FloorToInt(fc), 0, cols - 2);
            float top = Mathf.Lerp(_skirtTop[c0], _skirtTop[c0 + 1], fc - c0);
            float fr = Mathf.Clamp01(Mathf.InverseLerp(top, SkirtBot, y)) * (rows - 1);
            int r0 = Mathf.Clamp(Mathf.FloorToInt(fr), 0, rows - 2);
            float R(int r, int c) => new Vector2(_skirt[r, c].x, _skirt[r, c].z).magnitude;
            float a = Mathf.Lerp(R(r0, c0), R(r0, c0 + 1), fc - c0);
            float bb = Mathf.Lerp(R(r0 + 1, c0), R(r0 + 1, c0 + 1), fc - c0);
            return Mathf.Lerp(a, bb, fr - r0);
        }

        /// <summary>
        /// Ptéruges: duas camadas de tiras de couro que saem de dentro da barra do corselete e deitam
        /// sobre a saia (seguem o contorno dela, por cima das pregas). A camada de fora é curta, a de
        /// dentro é comprida e defasada meia tira. Cada tira tem ponta arredondada, borda enrolada e
        /// cravo de bronze, e usa os pesos da pele mais próxima.
        /// </summary>
        public void Pteruges(SkinBuilder sb, Vector3[] bottomEdge)
        {
            // (quantas, defasagem, comprimento, folga sobre a saia, recuo máximo no topo em relação à barra)
            StripLayer(sb, bottomEdge, 22, 0.5f, 0.205f, 0.003f, 0.0145f);
            StripLayer(sb, bottomEdge, 22, 0f, 0.128f, 0.0075f, 0.0098f);
        }

        void StripLayer(SkinBuilder sb, Vector3[] bottomEdge, int count, float phase, float len, float lift, float topMargin)
        {
            const int rows = 15, cols = 5;
            const float thick = 0.003f;
            for (int k = 0; k < count; k++)
            {
                float th = -180f + 360f * (k + phase) / count;
                float half0 = 360f / count * 0.455f;
                // a barra do corselete é inclinada na frente: a altura e o raio da borda são medidos em cada
                // coluna, e a tira acompanha a inclinação (como se estivesse costurada ao longo da barra)
                var yEdge = new float[cols];
                var rEdge = new float[cols];
                for (int c = 0; c < cols; c++)
                    yEdge[c] = EdgeAt(bottomEdge, th + (c / (float)(cols - 1) * 2f - 1f) * half0, out rEdge[c]);
                // desgaste: nenhuma tira tem exatamente o mesmo comprimento, e algumas foram cortadas em combate
                float h1 = Hash(k * 7.13f + phase * 3.7f + len * 11f);
                bool cut = Hash(k * 3.31f + phase * 11.9f + len * 5f) < 0.16f;
                float lenK = len * (cut ? 0.5f + 0.25f * h1 : 0.9f + 0.18f * h1);
                float slant = cut ? (Hash(k * 5.7f + phase + 2f) - 0.5f) * 0.7f : 0f;
                var outer = new Vector3[rows, cols];
                var nOut = new Vector3[rows, cols];
                for (int r = 0; r < rows; r++)
                {
                    float t = r / (float)(rows - 1);
                    // ponta arredondada (a tira cortada termina reta, na diagonal)
                    float half = half0 * (cut || t < 0.84f ? 1f : Mathf.Sqrt(Mathf.Max(0.04f, 1f - Mathf.Pow((t - 0.84f) / 0.16f, 2f))));
                    // couro firme: fica reto por cima das pregas (raio = o maior da saia na largura da tira)
                    float free = 0f;
                    for (int c = 0; c < cols; c++)
                        free = Mathf.Max(free, SkirtRadius(th + (c / (float)(cols - 1) * 2f - 1f) * half0, yEdge[c] + 0.01f - lenK * t));
                    free += lift + thick;
                    for (int c = 0; c < cols; c++)
                    {
                        float x = c / (float)(cols - 1) * 2f - 1f;
                        float y = yEdge[c] + 0.01f - lenK * (1f + slant * x) * t;
                        // a tira assenta sobre a saia em toda a altura. No topo a saia já fica recuada para dentro
                        // do corselete, então a tira entra junto e passa livre do rebordo da barra.
                        float rr = Mathf.Min(free, rEdge[c] - topMargin + 0.03f * Smooth01(0.004f, -0.02f, y - yEdge[c]));
                        float a = (th + x * half) * Mathf.Deg2Rad;
                        // leve abaulado na largura
                        float bulge = 0.0012f * (1f - x * x);
                        outer[r, c] = new Vector3(Mathf.Sin(a) * (rr + bulge), y, -Mathf.Cos(a) * (rr + bulge));
                        nOut[r, c] = new Vector3(Mathf.Sin(a), 0.12f, -Mathf.Cos(a)).normalized;
                    }
                }
                var inner = new Vector3[rows, cols];
                var nIn = new Vector3[rows, cols];
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++) { inner[r, c] = outer[r, c] - nOut[r, c] * thick; nIn[r, c] = -nOut[r, c]; }
                var mb = new MeshBuilder();
                mb.Grid(outer, nOut, M.PLeather, true);
                mb.Grid(inner, nIn, M.PLeather, true);
                // cravo de bronze perto da ponta e rebite menor em cima
                int rs = Mathf.RoundToInt((rows - 1) * 0.8f), rt = Mathf.RoundToInt((rows - 1) * 0.36f);
                if (!cut) mb.Sphere(outer[rs, cols / 2] + nOut[rs, cols / 2] * 0.0008f, 0.0052f, 0.0036f, 10, M.PGold);
                mb.Sphere(outer[rt, cols / 2] + nOut[rt, cols / 2] * 0.0006f, 0.003f, 0.002f, 8, M.PGold);
                sb.AddBuilder(mb, ClothWeights);
                // borda enrolada em volta da tira (lados e ponta)
                var rim = new Vector3[rows * 2];
                var rimN = new Vector3[rows * 2];
                for (int r = 0; r < rows; r++)
                {
                    rim[r] = outer[r, 0];
                    rim[rows * 2 - 1 - r] = outer[r, cols - 1];
                    rimN[r] = nOut[r, 0];
                    rimN[rows * 2 - 1 - r] = nOut[r, cols - 1];
                }
                sb.AddTube(rim, rimN, i => ClothWeights(rim[i]), thick, thick * 0.62f, M.PLeather, 5, false);
            }
        }

        /// <summary>Faixa de lã carmesim amarrada no quadril esquerdo: nó e duas pontas compridas com franja de ouro.</summary>
        public void Sash(SkinBuilder sb, Vector3[] bottomEdge)
        {
            float th0 = -128f;
            float yK = EdgeAt(bottomEdge, th0, out _) - 0.03f;
            var knot = new MeshBuilder();
            float ak = th0 * Mathf.Deg2Rad;
            Vector3 nk = new Vector3(Mathf.Sin(ak), 0f, -Mathf.Cos(ak));
            float rKnot = SkirtRadius(th0, yK) + 0.03f;
            Vector3 kp = new Vector3(nk.x * rKnot, yK, nk.z * rKnot);
            knot.Push(kp, Quaternion.LookRotation(nk, Vector3.up));
            knot.Sphere(Vector3.zero, 0.027f, 0.015f, 16, M.PCrimsonDark);
            knot.Torus(new Vector3(0f, 0f, 0.004f), Quaternion.Euler(90f, 0f, 0f), 0.02f, 0.0045f, 18, 6, M.PGold);
            knot.Pop();
            var kw = ClothWeights(kp);
            sb.AddBuilder(knot, _ => kw);
            int tailIndex = 0;
            foreach (var (dth, len, w) in new[] { (-7f, 0.5f, 0.055f), (8f, 0.4f, 0.048f) })
            {
                bool torn = tailIndex++ == 0; // a ponta comprida perdeu a franja: termina rasgada na diagonal
                const int rows = 22, cols = 5;
                var o = new Vector3[rows, cols];
                var on = new Vector3[rows, cols];
                for (int r = 0; r < rows; r++)
                {
                    float t = r / (float)(rows - 1);
                    float y = yK - len * t;
                    float half = (w * 0.5f / rKnot) * Mathf.Rad2Deg * (0.55f + 0.7f * Smooth01(0f, 0.25f, t));
                    float sway = 6f * t * t * Mathf.Sign(dth);
                    for (int c = 0; c < cols; c++)
                    {
                        float x = c / (float)(cols - 1) * 2f - 1f;
                        float thc = th0 + dth * (0.4f + 0.6f * t) + sway + x * half;
                        float a = thc * Mathf.Deg2Rad;
                        float sr = SkirtRadius(thc, Mathf.Max(y, SkirtBot));
                        // por cima das ptéruges; abaixo da barra da saia a ponta cai um pouco para fora
                        float rr = sr + 0.02f + 0.004f * Mathf.Sin(t * 7f + x * 1.5f) + 0.03f * Mathf.Max(0f, (SkirtBot - y) / 0.2f);
                        o[r, c] = new Vector3(Mathf.Sin(a) * rr, y + 0.004f * Mathf.Sin(t * 9f + c), -Mathf.Cos(a) * rr);
                        on[r, c] = new Vector3(Mathf.Sin(a), 0.1f, -Mathf.Cos(a)).normalized;
                    }
                }
                if (torn)
                {
                    var tail = Shell.FromGrid(o, default, new Vector3(0f, yK - len * 0.5f, 0f));
                    tail.Subdivide();
                    tail.Subdivide();
                    for (int i = 0; i < tail.p.Count; i++) tail.w[i] = ClothWeights(tail.p[i]);
                    float lenT = len, dthT = dth;
                    tail.Clip((q, bw) =>
                    {
                        float t = (yK - q.y) / lenT;
                        float center = th0 + dthT * (0.4f + 0.6f * t) + 6f * t * t * Mathf.Sign(dthT);
                        float x = Mathf.DeltaAngle(center, Mathf.Atan2(q.x, -q.z) * Mathf.Rad2Deg) / 8f; // ~[-1, 1] na largura
                        return 0.84f + 0.07f * x + 0.035f * Noise(x * 3f, 3f) + 0.02f * Noise(x * 11f, 4f) - t;
                    });
                    tail.KeepLargest();
                    var proj = UvProj.Cylinder(Vector3.zero, Vector3.up, nk, rKnot);
                    sb.AddSolid(tail, M.PCrimsonDark, M.PCrimsonDark, proj, 0.003f);
                    sb.AddRims(tail, 0.003f, 0.0016f, M.PCrimsonDark, 5, 0.02f);
                    continue;
                }
                var inn = new Vector3[rows, cols];
                var inN = new Vector3[rows, cols];
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++) { inn[r, c] = o[r, c] - on[r, c] * 0.003f; inN[r, c] = -on[r, c]; }
                var mb = new MeshBuilder();
                mb.Grid(o, on, M.PCrimsonDark, true);
                mb.Grid(inn, inN, M.PCrimsonDark, true);
                // barra e franja de ouro na ponta
                for (int k = 0; k < 7; k++)
                {
                    Vector3 a0 = Vector3.Lerp(o[rows - 1, 0], o[rows - 1, cols - 1], k / 6f);
                    mb.CylinderBetween(a0, a0 + Vector3.down * 0.034f, 0.0026f, 0.0016f, 6, M.PGold);
                }
                sb.AddBuilder(mb, ClothWeights);
                var bar = new Vector3[cols];
                var barN = new Vector3[cols];
                for (int c = 0; c < cols; c++) { bar[c] = o[rows - 2, c]; barN[c] = on[rows - 2, c]; }
                sb.AddTube(bar, barN, i => ClothWeights(bar[i]), 0f, 0.0028f, M.PGold, 6, false);
            }
        }

        /// <summary>Raio do contorno do tronco/pernas (sem os braços) em fatias horizontais.</summary>
        float[,] TorsoHull(float y0, float dy, int ny, float dAng, int na, float zc, out HashSet<int> set)
        {
            set = b.Set("root", "spine0*", "breast*", "pelvis*", "clavicle*", "neck01", "upperleg01*", "upperleg02*");
            var h = new float[ny, na];
            for (int i = 0; i < b.p.Length; i++)
            {
                if (!set.Contains(b.dom[i])) continue;
                Vector3 q = b.p[i];
                int yi = Mathf.RoundToInt((q.y - y0) / dy);
                if (yi < 0 || yi >= ny) continue;
                float ang = Mathf.Atan2(q.x, -(q.z - zc)) * Mathf.Rad2Deg; // 0 = costas
                int ai = Mathf.RoundToInt(ang / dAng) + na / 2;
                float r = new Vector2(q.x, q.z - zc).magnitude;
                for (int da = -1; da <= 1; da++)
                {
                    int aj = ai + da;
                    if (aj < 0 || aj >= na) continue;
                    if (r > h[yi, aj]) h[yi, aj] = r;
                }
            }
            return h;
        }

        /// <summary>Centro do escudo quando vai nas costas (m): a capa passa por baixo dele.</summary>
        public const float ShieldY = 1.25f, ShieldZ = -0.19f;

        /// <summary>
        /// Capa de lã presa nos ombros por cima das epômides, caindo reto a partir do ponto mais largo das
        /// costas. Fica apertada sob o escudo das costas e é empurrada pelas pernas (pesos da pele próxima).
        /// </summary>
        public void Cape(SkinBuilder sb, Shell corselet, out Vector3[] brooches)
        {
            const float zc = 0.01f, y0 = 0.25f, dy = 0.02f, dA = 3f;
            int ny = 72, na = 121;
            var hull = TorsoHull(y0, dy, ny, dA, na, zc, out _);
            // grade fina: os rasgos são recortados nela (células de ~1,2 x 2,1 cm na barra)
            const int rows = 60, cols = 113;
            const float half = 82f, yTop = 1.548f, yBot = 0.3f;
            int topIdx = Mathf.RoundToInt((yTop + 0.01f - y0) / dy);
            var p = new Vector3[rows, cols];
            for (int c = 0; c < cols; c++)
            {
                float u = c / (float)(cols - 1);
                float th = Mathf.Lerp(-half, half, u);
                int ai = Mathf.Clamp(Mathf.RoundToInt(th / dA) + na / 2, 0, na - 1);
                float run = 0f, runCorselet = 0f;
                for (int r = 0; r < rows; r++)
                {
                    float t = r / (float)(rows - 1);
                    float y = Mathf.Lerp(yTop - 0.03f * (th / half) * (th / half), yBot, t);
                    int yi = Mathf.Clamp(Mathf.RoundToInt((y - y0) / dy), 0, ny - 1);
                    // máximo do contorno desta altura para cima (o pano cai reto)
                    for (int k = yi; k <= Mathf.Min(topIdx, ny - 1); k++)
                        for (int da = -1; da <= 1; da++) run = Mathf.Max(run, hull[k, Mathf.Clamp(ai + da, 0, na - 1)]);
                    float a = th * Mathf.Deg2Rad;
                    // O corselete é rígido e passa em ponte sobre o sulco da coluna: ali ele fica bem mais para
                    // fora que a pele. A capa tem de passar por fora dele (e das epômides), não só do corpo.
                    if (corselet != null && y > 0.98f &&
                        corselet.Raycast(new Vector3(0f, y, zc), new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)), out var hitC, out _, out _, true))
                        runCorselet = Mathf.Max(runCorselet, new Vector2(hitC.x, hitC.z - zc).magnitude);
                    float clear = Mathf.Lerp(0.05f, 0.07f, t) + 0.07f * t * t;
                    float fold = 0.022f * Mathf.Pow(t, 1.1f) * Mathf.Sin(a * 9f + 0.6f) + 0.009f * t * Mathf.Sin(a * 17f + 1.3f)
                               + 0.004f * t * Mathf.Sin(a * 31f + 0.4f);
                    float rr = Mathf.Max(run + clear, runCorselet + 0.03f + 0.02f * t) + fold;
                    var q = new Vector3(Mathf.Sin(a) * rr, y, zc - Mathf.Cos(a) * rr);
                    // sob o escudo das costas o pano fica preso entre o corpo e a madeira
                    float rs = new Vector2(q.x, q.y - ShieldY).magnitude;
                    float limit = ShieldZ - PatrocloMeshes.EscudoDentroZ(Mathf.Min(rs, 0.455f)) + 0.014f;
                    float inside = 1f - Smooth01(0.455f, 0.54f, rs);
                    q.z = Mathf.Lerp(q.z, Mathf.Max(q.z, limit), inside);
                    p[r, c] = q;
                }
            }
            // o contorno do corpo vem em fatias de 2 cm: suaviza na vertical para o pano não cair em degraus
            for (int it = 0; it < 5; it++)
                for (int c = 0; c < cols; c++)
                {
                    Vector3 prev = p[0, c];
                    for (int r = 1; r < rows - 1; r++)
                    {
                        Vector3 cur = p[r, c];
                        Vector3 sm = (prev + cur * 2f + p[r + 1, c]) * 0.25f;
                        p[r, c] = new Vector3(sm.x, cur.y, sm.z);
                        prev = cur;
                    }
                }
            var mb = new MeshBuilder();
            brooches = new[] { p[0, 0], p[0, cols - 1] };
            foreach (var br in brooches)
            {
                Vector3 nn = new Vector3(br.x, 0.6f, br.z - zc + 0.12f).normalized;
                mb.Push(br + nn * 0.004f, Quaternion.LookRotation(nn, Vector3.up));
                mb.CylinderBetween(new Vector3(0f, 0f, -0.004f), new Vector3(0f, 0f, 0.004f), 0.023f, 0.021f, 22, M.PGold);
                mb.Torus(new Vector3(0f, 0f, 0.004f), Quaternion.Euler(90f, 0f, 0f), 0.018f, 0.0032f, 22, 6, M.PGold);
                mb.Sphere(new Vector3(0f, 0f, 0.003f), 0.009f, 0.0045f, 10, M.PGold);
                mb.Pop();
            }
            var capeDom = Dom(b.Set("spine0*", "clavicle*", "neck01", "root"));
            var legDom = Dom(b.Set("upperleg01*", "upperleg02*", "lowerleg01*", "lowerleg02*"));
            // pesos contínuos (sem saltos entre vértices vizinhos): em cima a pele das costas, descendo passa
            // para o quadril e, perto das pernas, para as coxas (misturadas esquerda/direita)
            var hip = BodyInfo.Rigid(b.B("root"));
            int thL = b.B("upperleg01.L"), thR = b.B("upperleg01.R");
            BoneWeight W(Vector3 v)
            {
                var wgt = Shell.LerpW(b.NearestWeights(v, capeDom), hip, Smooth01(1.2f, 0.95f, v.y));
                if (v.y > 0.98f) return wgt;
                int li = b.Nearest(v, legDom);
                if (li < 0) return wgt;
                float f = Smooth01(0.24f, 0.09f, (b.p[li] - v).magnitude) * Smooth01(0.98f, 0.8f, v.y);
                float sx = Smooth01(-0.06f, 0.06f, v.x);
                var thigh = new BoneWeight { boneIndex0 = thL, weight0 = 1f - sx, boneIndex1 = thR, weight1 = sx };
                return Shell.LerpW(wgt, thigh, 0.8f * f);
            }
            sb.AddBuilder(mb, W);

            // O pano vira uma casca para poder rasgar com recortes limpos (CapeTear).
            var cloth = Shell.FromGrid(p, default, new Vector3(0f, 0.9f, zc));
            for (int i = 0; i < cloth.p.Count; i++) cloth.w[i] = W(cloth.p[i]);
            // (u, t) de um ponto do pano: u de 0 (lado esquerdo) a 1, t de 0 (ombros) a 1 (barra)
            void UT(Vector3 q, out float u, out float t)
            {
                float th = Mathf.Clamp(Mathf.Atan2(q.x, -(q.z - zc)) * Mathf.Rad2Deg, -half, half);
                u = (th + half) / (2f * half);
                float yT = yTop - 0.03f * (th / half) * (th / half);
                t = (yT - q.y) / (yT - yBot);
            }
            cloth.Clip((q, w) => { UT(q, out float u, out float t); return CapeTear(u, t); });
            cloth.KeepLargest();
            // duas barras de ouro tecidas no próprio pano, perto da barra (rasgam junto com ele)
            Split(cloth, q =>
            {
                UT(q, out _, out float t);
                return Mathf.Min(Mathf.Abs(t - 0.914f) - 0.006f, Mathf.Abs(t - 0.892f) - 0.0025f);
            }, out var wool, out var bands);
            var uv = UvProj.Cylinder(new Vector3(0f, 0f, zc), Vector3.up, Vector3.back, 0.3f);
            sb.AddSolid(wool, M.PCrimsonDark, M.PCrimsonDark, uv, 0.0018f);
            if (bands.VertexCount > 0) sb.AddSolid(bands, M.PGold, M.PGold, uv, 0.0018f);
            // beira crua (fio desfiado) em todo o contorno, inclusive nos rasgos e nos buracos
            sb.AddRims(cloth, 0.0018f, 0.0013f, M.PCrimsonDark, 5, 0.03f);
        }

        // =====================================================================================
        // Ombreiras, braçadeiras, cnêmides, sandálias
        // =====================================================================================
        public void ArmFrame(string side, out Vector3 J, out Vector3 d, out Vector3 o)
        {
            J = b.Head("upperarm01." + side);
            d = (b.Head("lowerarm01." + side) - J).normalized;
            o = (Vector3.up - Vector3.Dot(Vector3.up, d) * d).normalized;
        }

        /// <summary>Lâmina de ombreira entre 'from' e 'to' ao longo do braço.</summary>
        public Shell Pauldron(string side, float from, float to, float offset, float bulge)
        {
            ArmFrame(side, out var J, out var d, out var o);
            var set = b.Set("shoulder01." + side, "upperarm01." + side, "upperarm02." + side, "clavicle." + side);
            var s = Shell.FromBody(b, Dom(set));
            s.Clip((q, w) => Vector3.Dot(q - J, d) - from);
            s.Clip((q, w) => to - Vector3.Dot(q - J, d));
            s.Clip((q, w) =>
            {
                Vector3 r = q - J;
                r -= Vector3.Dot(r, d) * d;
                return Vector3.Dot(r.normalized, o) + 0.25f;
            });
            s.KeepLargest();
            s.Smooth(22);
            s.Offset(q => offset + bulge * Smooth01(to, from, Vector3.Dot(q - J, d)));
            s.Smooth(4);
            s.Subdivide();
            s.Smooth(2);
            return s;
        }

        public UvProj ArmUv(string side)
        {
            ArmFrame(side, out var J, out var d, out var o);
            return UvProj.Cylinder(J, d, o, 0.06f);
        }

        public Shell Bracer(string side, out UvProj uv, out Vector3 elbow, out Vector3 axis, out Vector3 dorsal)
        {
            elbow = b.Head("lowerarm01." + side);
            Vector3 wr = b.Head("wrist." + side);
            Vector3 a = wr - elbow;
            float L2 = a.sqrMagnitude;
            var hf = HandFrame(side);
            Vector3 ax = a.normalized;
            Vector3 palm = (hf.palm - Vector3.Dot(hf.palm, ax) * ax).normalized;
            dorsal = -palm;
            axis = a;
            var set = b.Set("lowerarm01." + side, "lowerarm02." + side, "wrist." + side);
            var s = Shell.FromBody(b, Dom(set));
            Vector3 e = elbow;
            s.Clip((q, w) => Vector3.Dot(q - e, a) / L2 - 0.4f);
            s.Clip((q, w) => 0.97f - Vector3.Dot(q - e, a) / L2);
            Vector3 pm = palm;
            s.Clip((q, w) =>
            {
                Vector3 r = q - e;
                r -= Vector3.Dot(r, ax) * ax;
                return 0.93f - Vector3.Dot(r.normalized, pm);
            });
            s.KeepLargest();
            s.Smooth(6);
            s.Offset(0.0055f);
            s.Smooth(2);
            s.Subdivide();
            s.Smooth(1);
            uv = UvProj.Cylinder(elbow, ax, dorsal, 0.04f);
            return s;
        }

        public Shell Greave(string side, out UvProj uv)
        {
            Vector3 K = b.Head("lowerleg01." + side), A = b.Head("foot." + side);
            Vector3 a = A - K;
            float L2 = a.sqrMagnitude;
            Vector3 ax = a.normalized;
            Vector3 fwd = (Vector3.forward - Vector3.Dot(Vector3.forward, ax) * ax).normalized;
            var set = b.Set("upperleg02." + side, "lowerleg01." + side, "lowerleg02." + side, "foot." + side);
            var s = Shell.FromBody(b, Dom(set));
            float Front(Vector3 q)
            {
                Vector3 r = q - K;
                r -= Vector3.Dot(r, ax) * ax;
                return Vector3.Dot(r.normalized, fwd);
            }
            float T(Vector3 q) => Vector3.Dot(q - K, a) / L2;
            // borda de cima em arco sobre o joelho; embaixo cobre os tornozelos dos lados
            s.Clip((q, w) => T(q) - (-0.085f + 0.075f * (1f - Front(q))));
            s.Clip((q, w) => 0.86f + 0.05f * (1f - Mathf.Clamp01(Front(q))) - T(q));
            s.Clip((q, w) => Front(q) + 0.5f);
            s.KeepLargest();
            s.Smooth(18);
            s.Offset(0.014f);
            s.Smooth(3);
            s.Subdivide();
            s.Smooth(2);
            // nervura central descendo pela frente da canela
            Vector3 lat = Vector3.Cross(ax, fwd);
            s.Offset(q =>
            {
                Vector3 r = q - K;
                float side = Vector3.Dot(r, lat);
                return 0.0032f * Mathf.Exp(-(side * side) / (0.0075f * 0.0075f)) * Smooth01(0.1f, 0.6f, Front(q)) * Smooth01(0.0f, 0.12f, T(q)) * Smooth01(0.84f, 0.7f, T(q));
            });
            uv = UvProj.Cylinder(K, ax, fwd, 0.05f);
            return s;
        }

        /// <summary>Correias de couro que fecham a cnêmide atrás da panturrilha (passam por baixo das bordas).</summary>
        public Shell GreaveStraps(string side, out Vector3[] buckles, out Vector3[] buckleN, out BoneWeight[] buckleW)
        {
            LegFrame(side, out var K, out var a, out var ax, out var fwd);
            float L2 = a.sqrMagnitude;
            var set = b.Set("lowerleg01." + side, "lowerleg02." + side);
            float Front(Vector3 q)
            {
                Vector3 r = q - K;
                r -= Vector3.Dot(r, ax) * ax;
                return Vector3.Dot(r.normalized, fwd);
            }
            float T(Vector3 q) => Vector3.Dot(q - K, a) / L2;
            // região de trás da panturrilha na malha visível, refinada duas vezes antes do recorte fino
            var s = Shell.FromBody(hi, v => set.Contains(hi.dom[v]));
            s.Clip((q, w) => Mathf.Min(-0.25f - Front(q), Mathf.Min(T(q) - 0.1f, 0.78f - T(q))));
            s.Subdivide();
            s.Subdivide();
            float[] centers = { 0.2f, 0.42f, 0.66f };
            s.Clip((q, w) =>
            {
                float band = -1f;
                foreach (float c in centers) band = Mathf.Max(band, 0.024f - Mathf.Abs(T(q) - c));
                return Mathf.Min(band, -0.36f - Front(q));
            });
            s.Smooth(2);
            s.Offset(0.004f);
            var bl = new List<Vector3>();
            var bn = new List<Vector3>();
            var bw = new List<BoneWeight>();
            foreach (float c in centers)
            {
                int i = s.Closest(Vector3.zero, (q, nn) => Mathf.Abs(T(q) - c) * 4f + (1f + Front(q)));
                if (i < 0) continue;
                bl.Add(s.p[i]);
                bn.Add(s.n[i]);
                bw.Add(s.w[i]);
            }
            buckles = bl.ToArray();
            buckleN = bn.ToArray();
            buckleW = bw.ToArray();
            return s;
        }

        /// <summary>Joelheira em cúpula por cima da cnêmide.</summary>
        public Shell KneeCap(string side)
        {
            Vector3 K = b.Head("lowerleg01." + side), A = b.Head("foot." + side);
            Vector3 a = A - K;
            float L2 = a.sqrMagnitude;
            Vector3 ax = a.normalized;
            Vector3 fwd = (Vector3.forward - Vector3.Dot(Vector3.forward, ax) * ax).normalized;
            var set = b.Set("upperleg02." + side, "lowerleg01." + side);
            var s = Shell.FromBody(b, Dom(set));
            float Front(Vector3 q)
            {
                Vector3 r = q - K;
                r -= Vector3.Dot(r, ax) * ax;
                return Vector3.Dot(r.normalized, fwd);
            }
            float T(Vector3 q) => Vector3.Dot(q - K, a) / L2;
            s.Clip((q, w) => T(q) + 0.17f);
            s.Clip((q, w) => 0.035f - T(q));
            s.Clip((q, w) => Front(q) - 0.2f);
            s.KeepLargest();
            s.Smooth(20);
            s.Offset(q => 0.024f + 0.008f * Smooth01(0.2f, 0.9f, Front(q)));
            s.Smooth(3);
            s.Subdivide();
            s.Smooth(2);
            return s;
        }

        public void FootFrame(string side, out Vector3 heel, out Vector3 toe, out Vector3 fdir, out HashSet<int> set)
        {
            set = b.Set("foot." + side, "toe*." + side);
            heel = new Vector3(0f, 0f, 9f);
            toe = new Vector3(0f, 0f, -9f);
            for (int i = 0; i < b.p.Length; i++)
            {
                if (!set.Contains(b.dom[i])) continue;
                if (b.p[i].z < heel.z) heel = b.p[i];
                if (b.p[i].z > toe.z) toe = b.p[i];
            }
            fdir = toe - heel;
            fdir.y = 0f;
            fdir.Normalize();
        }

        public const float SoleThickness = 0.011f;

        public Shell Sole(string side)
        {
            FootFrame(side, out var heel, out var toe, out var fdir, out var set);
            var s = Shell.FromBody(b, v => set.Contains(b.dom[v]) && b.p[v].y < 0.03f && b.n[v].y < -0.25f);
            s.KeepLargest();
            s.Smooth(4);
            Vector3 c = (heel + toe) * 0.5f;
            s.Displace((q, nn) =>
            {
                Vector3 h = new Vector3(q.x - c.x, 0f, q.z - c.z);
                return new Vector3(q.x, -SoleThickness, q.z) + h.normalized * 0.005f;
            });
            return s;
        }

        public Shell Straps(string side)
        {
            FootFrame(side, out var heel, out var toe, out var fdir, out var set);
            var set2 = new HashSet<int>(set) { b.B("lowerleg02." + side) };
            float len = Vector3.Distance(new Vector3(heel.x, 0, heel.z), new Vector3(toe.x, 0, toe.z));
            Vector3 sideDir = Vector3.Cross(Vector3.up, fdir);
            Vector3 H = new Vector3(heel.x, 0f, heel.z);
            float Band(Vector3 q, Vector3 c, Vector3 nrm, float w) => w * 0.5f - Mathf.Abs(Vector3.Dot(q - c, nrm.normalized));
            Vector3 P(float t) => H + fdir * (len * t);
            // tiras estreitas: recortadas da malha visível do pé, refinada, para ficarem rentes à pele
            var s = Shell.FromBody(hi, v => set2.Contains(hi.dom[v]) && hi.p[v].y < 0.16f);
            s.Subdivide();
            Vector3 footC = (H + P(1f)) * 0.5f;
            s.Clip((q, w) =>
            {
                float toeStrap = Band(q, P(0.72f), fdir, 0.013f);
                float x1 = Band(q, P(0.5f), fdir + sideDir * 0.9f, 0.011f);
                float x2 = Band(q, P(0.5f), fdir - sideDir * 0.9f, 0.011f);
                float cross = Mathf.Min(Mathf.Max(x1, x2), Mathf.Min(Vector3.Dot(q - P(0.32f), fdir), Vector3.Dot(P(0.66f) - q, fdir)));
                float ankle = Band(q, new Vector3(0f, 0.098f, 0f), Vector3.up, 0.016f);
                float ankle2 = Band(q, new Vector3(0f, 0.135f, 0f), Vector3.up, 0.011f);
                float heelStrap = Mathf.Min(Band(q, footC, sideDir, 0.016f), Vector3.Dot(P(0.25f) - q, fdir));
                float u = Mathf.Max(Mathf.Max(toeStrap, cross), Mathf.Max(Mathf.Max(ankle, ankle2), heelStrap));
                return Mathf.Min(u, q.y - 0.008f);
            });
            s.Smooth(2);
            s.Offset(0.0028f);
            return s;
        }

        // =====================================================================================
        // Elmo ático (ajustado ao crânio), crista e rabo de crina
        // =====================================================================================
        public class HelmetShape
        {
            public Vector3 c;
            /// <summary>Marcos do rosto em graus (latitude/longitude a partir de c): olho, ponta do nariz, boca, queixo.</summary>
            public float eyeLat, eyeLon, noseLat, mouthLat, chinLat;
            public float lat0, dLat, dLon;
            public int rows, cols;
            public float[,] r;

            public static Vector3 Dir(float lat, float lon)
            {
                float la = lat * Mathf.Deg2Rad, lo = lon * Mathf.Deg2Rad;
                return new Vector3(Mathf.Cos(la) * Mathf.Sin(lo), Mathf.Sin(la), Mathf.Cos(la) * Mathf.Cos(lo));
            }

            public float Radius(float lat, float lon)
            {
                float fr = Mathf.Clamp((lat - lat0) / dLat, 0f, rows - 1.001f);
                float fc = Mathf.Clamp((Mathf.Repeat(lon + 180f, 360f)) / dLon, 0f, cols - 1.001f);
                int r0 = (int)fr, c0 = (int)fc;
                float tr = fr - r0, tc = fc - c0;
                return Mathf.Lerp(Mathf.Lerp(r[r0, c0], r[r0, c0 + 1], tc), Mathf.Lerp(r[r0 + 1, c0], r[r0 + 1, c0 + 1], tc), tr);
            }

            public Vector3 Point(float lat, float lon, float extra = 0f) => c + Dir(lat, lon) * (Radius(lat, lon) + extra);

            public void LatLon(Vector3 q, out float lat, out float lon)
            {
                Vector3 d = q - c;
                lat = Mathf.Asin(Mathf.Clamp(d.y / Mathf.Max(d.magnitude, 1e-6f), -1f, 1f)) * Mathf.Rad2Deg;
                lon = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            }
        }

        public const float HelmetOffset = 0.022f;

        public HelmetShape MeasureHead()
        {
            var headSet = b.Subtree("head");
            headSet.Add(b.B("neck03"));
            var pts = new List<Vector3>();
            float zmin = 9f, zmax = -9f;
            for (int i = 0; i < b.p.Length; i++)
            {
                if (!headSet.Contains(b.dom[i])) continue;
                Vector3 q = b.p[i];
                if (q.y < 1.6f) continue;
                // orelhas ficam de fora (o elmo tem recorte para elas)
                if (Mathf.Abs(q.x) > 0.068f && q.y > 1.67f && q.y < 1.785f && q.z > -0.035f && q.z < 0.085f) continue;
                pts.Add(q);
                if (q.y > 1.76f && q.y < 1.8f) { zmin = Mathf.Min(zmin, q.z); zmax = Mathf.Max(zmax, q.z); }
            }
            var h = new HelmetShape { c = new Vector3(0f, 1.735f, (zmin + zmax) * 0.5f), lat0 = -62f, dLat = 2f, dLon = 4f };
            h.rows = Mathf.RoundToInt((88f - h.lat0) / h.dLat) + 1;
            h.cols = Mathf.RoundToInt(360f / h.dLon) + 1;
            int R = h.rows, C = h.cols;
            var dirs = new Vector3[R, C];
            var raw = new float[R, C];
            float cosCone = Mathf.Cos(7f * Mathf.Deg2Rad);
            for (int r = 0; r < R; r++)
                for (int c = 0; c < C; c++)
                {
                    var d = HelmetShape.Dir(h.lat0 + r * h.dLat, -180f + c * h.dLon);
                    dirs[r, c] = d;
                    float best = float.NaN;
                    foreach (var q in pts)
                    {
                        Vector3 v = q - h.c;
                        float len = v.magnitude;
                        float proj = Vector3.Dot(v, d);
                        if (proj < cosCone * len) continue;
                        if (float.IsNaN(best) || proj > best) best = proj;
                    }
                    raw[r, c] = best;
                }
            // preenche direções sem vértices (para baixo, dentro do pescoço)
            for (int it = 0; it < 60; it++)
            {
                bool any = false;
                for (int r = 0; r < R; r++)
                    for (int c = 0; c < C; c++)
                    {
                        if (!float.IsNaN(raw[r, c])) continue;
                        float s = 0f;
                        int k = 0;
                        for (int dr = -1; dr <= 1; dr++)
                            for (int dc = -1; dc <= 1; dc++)
                            {
                                int rr = r + dr, cc = (c + dc + C - 1) % (C - 1);
                                if (rr < 0 || rr >= R || float.IsNaN(raw[rr, cc])) continue;
                                s += raw[rr, cc];
                                k++;
                            }
                        if (k > 0) raw[r, c] = s / k;
                        else any = true;
                    }
                if (!any) break;
            }
            // suaviza na esfera (gaussiana de ~10°)
            float sig2 = Mathf.Pow(10f * Mathf.Deg2Rad, 2f);
            float cut = Mathf.Cos(3.5f * 10f * Mathf.Deg2Rad);
            h.r = new float[R, C];
            for (int r = 0; r < R; r++)
                for (int c = 0; c < C; c++)
                {
                    float sw = 0f, s = 0f;
                    for (int r2 = 0; r2 < R; r2++)
                    {
                        float wl = Mathf.Cos((h.lat0 + r2 * h.dLat) * Mathf.Deg2Rad);
                        for (int c2 = 0; c2 < C - 1; c2++)
                        {
                            float dd = Vector3.Dot(dirs[r, c], dirs[r2, c2]);
                            if (dd < cut) continue;
                            float wgt = Mathf.Exp((dd - 1f) / sig2) * wl;
                            sw += wgt;
                            s += wgt * raw[r2, c2];
                        }
                    }
                    h.r[r, c] = s / sw;
                }
            // nunca menor que o crânio cru (com folga) nas regiões que o elmo cobre
            for (int r = 0; r < R; r++)
                for (int c = 0; c < C; c++)
                    h.r[r, c] = Mathf.Max(h.r[r, c], raw[r, c] - 0.008f);
            // marcos do rosto para recortar as aberturas do elmo coríntio
            h.LatLon(b.Tail("eye.R"), out h.eyeLat, out h.eyeLon);
            h.eyeLon = Mathf.Abs(h.eyeLon);
            Vector3 nose = h.c;
            foreach (var q in pts) if (Mathf.Abs(q.x) < 0.006f && q.y < h.c.y && q.z > nose.z) nose = q;
            h.LatLon(nose, out h.noseLat, out _);
            h.LatLon((b.Head("oris04.L") + b.Head("oris06.L")) * 0.5f, out h.mouthLat, out _);
            h.LatLon(b.Tail("special04"), out h.chinLat, out _);
            Debug.Log($"[Troia] elmo: olho lat {h.eyeLat:F1} lon {h.eyeLon:F1}, nariz {h.noseLat:F1}, boca {h.mouthLat:F1}, queixo {h.chinLat:F1}");
            return h;
        }

        /// <summary>Raio final do elmo coríntio: crânio suavizado + folga, bojo das orelhas, abas que abrem embaixo e nuca em leque.</summary>
        public static float HelmetRadius(HelmetShape h, float lat, float lon)
        {
            float alon = Mathf.Abs(lon);
            // folga das orelhas: alargamento largo e suave dos lados (sem "caixas" nas orelhas)
            float ears = 0.011f * Smooth01(30f, 82f, alon) * Smooth01(155f, 100f, alon) * Smooth01(-58f, -14f, lat) * Smooth01(46f, 2f, lat);
            float cheekFlare = 0.012f * Smooth01(-26f, -52f, lat) * Smooth01(115f, 60f, alon);
            float neck = 0.034f * Smooth01(-8f, -46f, lat) * Smooth01(100f, 150f, alon);
            return h.Radius(lat, lon) + HelmetOffset + ears + cheekFlare + neck;
        }

        /// <summary>
        /// Abertura do rosto (positivo = aberto), em graus a partir do centro do crânio (1° ≈ 2 mm):
        /// olhos amendoados, canais largos ao lado do nasal, passagem por baixo da ponta dele (o nasal fica
        /// solto embaixo, como nos elmos coríntios) e a fenda da boca até o queixo. Uniões arredondadas.
        /// </summary>
        public static float HelmetOpening(HelmetShape h, float lat, float alon)
        {
            float nasal = h.eyeLon - 9.5f, outer = h.eyeLon + 16f;
            float u = Mathf.Clamp01((alon - nasal) / (outer - nasal));
            float center = h.eyeLat - 1f + 0.14f * (alon - nasal);
            // Sin(π) em float é um negativo minúsculo: sem o Max, o Pow devolve NaN
            float hh = 5.4f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.65f);
            float eye = Mathf.Min(Mathf.Min(alon - nasal, outer - alon), hh - Mathf.Abs(lat - center));
            float under = h.noseLat - 7f;
            float channel = Mathf.Min(Mathf.Min(alon - nasal, nasal + 7f - alon), Mathf.Min(h.eyeLat - lat, lat - under));
            float conn = Mathf.Min(nasal + 7f - alon, Mathf.Min(h.noseLat - 1.5f - lat, lat - under));
            float slitW = 6f + 0.12f * Mathf.Max(0f, h.noseLat - 4f - lat);
            float slit = Mathf.Min(slitW - alon, h.noseLat - 1.5f - lat);
            return SMax(SMax(SMax(eye, channel, 3f), conn, 3f), slit, 3f);
        }

        public static float HelmetKeep(HelmetShape h, Vector3 q)
        {
            h.LatLon(q, out float lat, out float lon);
            float alon = Mathf.Abs(lon);
            float front = h.chinLat - 6f;
            float lowEdge = alon < 48f ? front : alon < 108f ? Mathf.Lerp(front, -38f, Smooth01(48f, 95f, alon)) : Mathf.Lerp(-38f, -47f, Smooth01(108f, 145f, alon));
            return SMin(lat - lowEdge, -HelmetOpening(h, lat, alon), 3f);
        }

        public Shell Helmet(HelmetShape h)
        {
            // grade mais fina que a medição (o raio é interpolado): recortes dos olhos e do nasal ficam limpos
            const float dLat = 1f, dLon = 2f;
            // vai até o polo (90°): a última fileira colapsa num ponto e fecha o topo da cúpula
            int R = Mathf.RoundToInt((90f - h.lat0) / dLat) + 1, C = Mathf.RoundToInt(360f / dLon) + 1;
            var pts = new Vector3[R, C];
            for (int r = 0; r < R; r++)
                for (int c = 0; c < C; c++)
                {
                    float lat = h.lat0 + r * dLat, lon = -180f + c * dLon;
                    pts[r, c] = h.c + HelmetShape.Dir(lat, lon) * HelmetRadius(h, lat, lon);
                }
            var s = Shell.FromGrid(pts, BodyInfo.Rigid(b.B("head")), h.c);
            s.Clip((q, w) => HelmetKeep(h, q));
            s.KeepLargest();
            // tira as facetas do mapa de raio medido (grade de 2° x 4°) sem mudar a forma geral
            s.Smooth(12);
            return s;
        }

        public static UvProj HelmetUv(HelmetShape h) => UvProj.Sphere(h.c, Vector3.up, Vector3.forward, 0.11f);

        /// <summary>Sobrancelhas gravadas em ouro sobre os olhos, continuando como a quina em volta do elmo.</summary>
        public Shell HelmetBand(HelmetShape h, Shell helmet)
        {
            var s = helmet.Clone().Clip((q, w) =>
            {
                h.LatLon(q, out float lat, out float lon);
                float alon = Mathf.Abs(lon);
                float nasal = h.eyeLon - 9.5f, outer = h.eyeLon + 16f;
                float u = Mathf.Clamp01((alon - nasal) / (outer - nasal));
                float top = alon < nasal ? h.eyeLat + 1.2f
                    : h.eyeLat - 1f + 0.14f * (alon - nasal) + 5.4f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.65f);
                float brow = Mathf.Min(Mathf.Min(lat - (top + 1.6f), top + 4.4f - lat), outer + 2f - alon);
                float ring = Mathf.Min(Mathf.Min(lat - (h.eyeLat + 3.5f), h.eyeLat + 6.3f - lat), alon - (outer - 1f));
                return Mathf.Max(brow, ring);
            });
            return s.VertexCount == 0 ? s : s.Offset(0.0014f);
        }

        /// <summary>Volutas de ouro nas têmporas (o mesmo motivo da guarda da espada): espiral lisa com haste.</summary>
        public static void TempleVolutes(MeshBuilder mb, HelmetShape h)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                float lat0 = h.eyeLat + 9f, lon0 = side * (h.eyeLon + 40f);
                Vector3 n = HelmetShape.Dir(lat0, lon0);
                Vector3 p0 = h.c + n * (HelmetRadius(h, lat0, lon0) + 0.004f);
                Vector3 t1 = Vector3.Cross(n, Vector3.up).normalized;
                Vector3 t2 = Vector3.Cross(t1, n).normalized;
                var pts = new List<Vector3>();
                var rad = new List<float>();
                // haste: sobe da bochecha até a espiral
                for (int k = 6; k >= 1; k--)
                {
                    float la = lat0 - 2.5f - k * 3.2f, lo = lon0 - side * k * 1.2f;
                    Vector3 nn = HelmetShape.Dir(la, lo);
                    pts.Add(h.c + nn * (HelmetRadius(h, la, lo) + 0.0035f));
                    rad.Add(0.0028f);
                }
                // espiral para dentro
                for (int k = 0; k <= 40; k++)
                {
                    float a = -1.2f + k * 0.26f, r = 0.019f * (1f - k / 46f);
                    pts.Add(p0 + (t1 * Mathf.Cos(a) * side + t2 * Mathf.Sin(a)) * r + n * 0.0006f * k / 40f);
                    rad.Add(Mathf.Lerp(0.0034f, 0.0018f, k / 40f));
                }
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    mb.CylinderBetween(pts[i], pts[i + 1], rad[i], rad[i + 1], 8, M.PGold);
                    mb.Sphere(pts[i + 1], rad[i + 1], rad[i + 1], 6, M.PGold);
                }
                mb.Sphere(pts[pts.Count - 1], 0.004f, 0.004f, 8, M.PGold);
            }
        }

        /// <summary>Ponto da linha central (meridiano) do elmo: m de 0 (frente, horizontal) a 180 (nuca).</summary>
        public static Vector3 Meridian(HelmetShape h, float m, float extra, out Vector3 normal)
        {
            float lat = m <= 90f ? m : 180f - m;
            float lon = m <= 90f ? 0f : 180f;
            normal = HelmetShape.Dir(lat, lon);
            return h.c + normal * (HelmetRadius(h, lat, lon) + extra);
        }

        /// <summary>Loft com o lado sempre em X (para peças no plano de simetria: crista, suporte, rabo).</summary>
        public static void LoftX(MeshBuilder mb, IList<Vector3> c, IList<Vector3> up, IList<Vector2> r, int seg, M m, bool strands = false)
        {
            int n = c.Count;
            var pos = new Vector3[n, seg + 1];
            var nrm = new Vector3[n, seg + 1];
            for (int k = 0; k < n; k++)
                for (int j = 0; j <= seg; j++)
                {
                    float a = j * Mathf.PI * 2f / seg;
                    pos[k, j] = c[k] + Vector3.right * (Mathf.Cos(a) * r[k].x) + up[k] * (Mathf.Sin(a) * r[k].y);
                    nrm[k, j] = (Vector3.right * (Mathf.Cos(a) / Mathf.Max(r[k].x, 1e-4f)) + up[k] * (Mathf.Sin(a) / Mathf.Max(r[k].y, 1e-4f))).normalized;
                }
            mb.Grid(pos, nrm, m, strands);
        }

        public void Crest(MeshBuilder mb, HelmetShape h, out Vector3 flamePos)
        {
            var cs = new List<Vector3>();
            var ups = new List<Vector3>();
            var rs = new List<Vector2>();
            var hc = new List<Vector3>();
            var hu = new List<Vector3>();
            var hr = new List<Vector2>();
            flamePos = Vector3.zero;
            float best = -1f;
            for (float m = 32f; m <= 164.01f; m += 4f)
            {
                float t = (m - 32f) / 132f;
                float hgt = 0.024f + 0.115f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f));
                float wid = 0.011f + 0.03f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.05f));
                Vector3 p = Meridian(h, m, 0.016f, out var nn);
                cs.Add(p + nn * (hgt * 0.5f));
                ups.Add(nn);
                rs.Add(new Vector2(wid, hgt * 0.5f));
                Vector3 ph = Meridian(h, m, 0f, out var nh);
                hc.Add(ph + nh * 0.009f);
                hu.Add(nh);
                hr.Add(new Vector2(0.0055f, 0.0095f));
                Vector3 top = p + nn * hgt;
                if (top.y > best) { best = top.y; flamePos = top; }
            }
            LoftX(mb, cs, ups, rs, 18, M.PCrimson, true);
            LoftX(mb, hc, hu, hr, 8, M.PBronzeDark);
        }

        /// <summary>Rabo de crina pendurado da ponta de trás da crista, seguindo uma curva dada.</summary>
        public static void Tail(MeshBuilder mb, IList<Vector3> path, float r0)
        {
            var ups = new List<Vector3>();
            var rs = new List<Vector2>();
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 tg = path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)];
                Vector3 up = Vector3.Cross(tg, Vector3.right).normalized;
                if (up.z > 0f) up = -up;
                ups.Add(up);
                float t = i / (float)(path.Count - 1);
                rs.Add(new Vector2(r0 * (1f - 0.75f * t) + 0.002f, r0 * 0.7f * (1f - 0.8f * t) + 0.002f));
            }
            LoftX(mb, path, ups, rs, 12, M.PCrimson, true);
        }
    }
}
