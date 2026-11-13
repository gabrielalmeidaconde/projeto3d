using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>
    /// Peças rígidas do Pátroclo, cada uma na sua própria origem:
    /// espada (empunhadura na origem, lâmina em +Z, largura em Y) e aspis (centro na origem, frente em +Z).
    /// </summary>
    public static class PatrocloMeshes
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector2 V2(float x, float y) => new Vector2(x, y);
        static readonly float[] Sides = { -1f, 1f };

        static void Loft(MeshBuilder b, int seg, M m, params (Vector3 c, Vector2 r)[] rings)
        {
            var cs = new Vector3[rings.Length];
            var rs = new Vector2[rings.Length];
            for (int i = 0; i < rings.Length; i++) { cs[i] = rings[i].c; rs[i] = rings[i].r; }
            b.Loft(cs, rs, seg, m, true, true);
        }

        static (Vector3, Vector2) R(float x, float y, float z, float rx, float ry) => (V(x, y, z), V2(rx, ry));

        static void Ellipsoid(MeshBuilder b, Vector3 c, float rxy, float rz, M m, int seg = 10)
        {
            b.Push(c, Quaternion.Euler(90f, 0f, 0f));
            b.Sphere(Vector3.zero, rxy, rz, seg, m);
            b.Pop();
        }

        // ---------------- espada (lâmina em folha, estilo Naue II, ~1,4 m)
        /// <summary>Comprimento da lâmina a partir da guarda (m).</summary>
        public const float BladeLength = 1.02f;
        const float BladeBase = 0.152f;

        static readonly float[] WZ = { 0f, 0.08f, 0.25f, 0.5f, 0.68f, 0.83f, 0.93f, 0.985f, 1.02f };
        static readonly float[] WW = { 0.031f, 0.028f, 0.031f, 0.042f, 0.047f, 0.041f, 0.026f, 0.01f, 0f };

        static float Interp(float[] xs, float[] ys, float x)
        {
            if (x <= xs[0]) return ys[0];
            for (int i = 0; i < xs.Length - 1; i++)
                if (x <= xs[i + 1])
                {
                    float t = (x - xs[i]) / (xs[i + 1] - xs[i]);
                    t = t * t * (3f - 2f * t);
                    return Mathf.Lerp(ys[i], ys[i + 1], t);
                }
            return ys[ys.Length - 1];
        }

        /// <summary>Meia-largura da lâmina a uma distância z da guarda.</summary>
        public static float BladeHalfWidth(float z) => Interp(WZ, WW, z);

        /// <summary>Meia-espessura na nervura central.</summary>
        public static float BladeHalfThick(float z) => Mathf.Lerp(0.0068f, 0.0042f, Mathf.Clamp01(z / 0.85f)) * Mathf.Clamp01((BladeLength - z) / 0.12f + 0.08f);

        /// <summary>Empunhadura de duas mãos em couro trançado, pomo em disco e guarda larga com volutas de ouro.</summary>
        public static void Punho(MeshBuilder b)
        {
            b.CylinderBetween(V(0, 0, -0.118f), V(0, 0, 0.108f), 0.0165f, 0.0175f, 14, M.PLeather);
            for (int k = 0; k < 13; k++)
            {
                float z = -0.108f + k * 0.0172f;
                b.Push(V(0, 0, z), Quaternion.Euler(0f, 0f, k * 28f));
                b.Torus(Vector3.zero, Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, 0f, 9f), 0.0172f, 0.0026f, 14, 5, M.PLeather);
                b.Pop();
            }
            foreach (float z in new[] { -0.122f, 0.11f })
            {
                b.CylinderBetween(V(0, 0, z - 0.006f), V(0, 0, z + 0.006f), 0.0205f, 0.0205f, 18, M.PGold);
                b.Torus(V(0, 0, z), Quaternion.Euler(90f, 0f, 0f), 0.0205f, 0.0028f, 18, 5, M.PGold);
            }
            // pomo: disco de bronze com aro e botão de ouro (compacto, para não parecer outra guarda)
            b.CylinderBetween(V(0, 0, -0.126f), V(0, 0, -0.138f), 0.022f, 0.03f, 22, M.PBronzeDark);
            b.CylinderBetween(V(0, 0, -0.138f), V(0, 0, -0.152f), 0.03f, 0.026f, 22, M.PBronzeDark);
            b.Torus(V(0, 0, -0.138f), Quaternion.Euler(90f, 0f, 0f), 0.03f, 0.0032f, 22, 6, M.PGold);
            b.Sphere(V(0, 0, -0.154f), 0.016f, 0.009f, 14, M.PGold);
            // guarda: braços largos curvados para a lâmina, terminando em volutas de ouro
            b.Box(V(0, 0, 0.132f), V(0.03f, 0.082f, 0.046f), M.PBronzeDark);
            foreach (float sx in Sides) b.Sphere(V(sx * 0.016f, 0, 0.132f), 0.017f, 0.009f, 14, M.PGold);
            foreach (float s in Sides)
            {
                var g = new Vector3[7];
                for (int i = 0; i < g.Length; i++)
                {
                    float u = i / (float)(g.Length - 1);
                    float y = s * Mathf.Lerp(0.035f, 0.128f, u);
                    float z = 0.13f + 0.045f * u * u;
                    g[i] = V(0, y, z);
                }
                for (int i = 0; i < g.Length - 1; i++)
                {
                    float r0 = Mathf.Lerp(0.019f, 0.01f, i / 6f), r1 = Mathf.Lerp(0.019f, 0.01f, (i + 1) / 6f);
                    b.CylinderBetween(g[i], g[i + 1], r0, r1, 12, M.PBronze);
                    b.Sphere(g[i + 1], r1, r1, 10, M.PBronze);
                }
                // voluta: espiral de ouro na ponta
                Vector3 end = g[g.Length - 1];
                for (int k = 0; k < 9; k++)
                {
                    float a = k * 0.75f, rad = 0.016f * (1f - k / 11f);
                    Vector3 c = end + V(0, s * (0.008f + Mathf.Sin(a) * rad * 0.4f), 0.012f + Mathf.Cos(a) * rad * 0.5f - 0.004f);
                    b.Sphere(c, 0.0075f * (1f - k / 14f), 0.0075f * (1f - k / 14f), 8, M.PGold);
                }
            }
        }

        /// <summary>Lâmina em folha com seção em losango: chanfro de corte, face e nervura central (arestas vivas).</summary>
        public static void Lamina(MeshBuilder b)
        {
            const int N = 44;
            var zs = new float[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N;
                zs[i] = BladeLength * (1f - (1f - u) * (1f - u) * 0.15f - 0.85f * (1f - u)); // mais estações perto da ponta
            }
            // pontos da seção: fio, chanfro, nervura (frações da largura/espessura)
            Vector3 Pt(float z, float fy, float fx, float side)
            {
                float w = BladeHalfWidth(z), t = BladeHalfThick(z);
                return V(side * fx * t, fy * w, BladeBase + z);
            }
            float[][] sec = { new[] { 1f, 0f }, new[] { 0.8f, 0.34f }, new[] { 0f, 1f }, new[] { -0.8f, 0.34f }, new[] { -1f, 0f } };
            foreach (float side in Sides)
                for (int f = 0; f < 4; f++)
                {
                    var p = new Vector3[N + 1, 2];
                    var n = new Vector3[N + 1, 2];
                    for (int i = 0; i <= N; i++)
                    {
                        p[i, 0] = Pt(zs[i], sec[f][0], sec[f][1], side);
                        p[i, 1] = Pt(zs[i], sec[f + 1][0], sec[f + 1][1], side);
                        int j = Mathf.Min(i + 1, N), k = Mathf.Max(i - 1, 0);
                        Vector3 along = Pt(zs[j], (sec[f][0] + sec[f + 1][0]) * 0.5f, (sec[f][1] + sec[f + 1][1]) * 0.5f, side)
                                        - Pt(zs[k], (sec[f][0] + sec[f + 1][0]) * 0.5f, (sec[f][1] + sec[f + 1][1]) * 0.5f, side);
                        Vector3 nn = Vector3.Cross(along, p[i, 1] - p[i, 0]).normalized;
                        if (nn.x * side < 0f) nn = -nn;
                        n[i, 0] = n[i, 1] = nn;
                    }
                    b.Grid(p, n, M.PBlade, true);
                }
            // filete de ouro na nervura e meandro gravado perto da guarda (nas duas faces)
            foreach (float side in Sides)
            {
                for (int i = 0; i < 26; i++)
                {
                    float z0 = 0.02f + i * 0.024f, z1 = z0 + 0.024f;
                    b.BoxBetween(V(side * (BladeHalfThick(z0) + 0.0004f), 0, BladeBase + z0), V(side * (BladeHalfThick(z1) + 0.0004f), 0, BladeBase + z1), 0.0012f, 0.004f, M.PGold);
                }
                for (int k = 0; k < 7; k++)
                {
                    float z = 0.03f + k * 0.012f;
                    float w = BladeHalfWidth(z) * 0.55f, x = side * BladeHalfThick(z) * 0.62f + side * 0.0004f;
                    // gancho do meandro: barra vertical + dois traços
                    b.BoxBetween(V(x, -w, BladeBase + z), V(x, w, BladeBase + z), 0.001f, 0.0016f, M.PGold);
                    float yy = (k % 2 == 0 ? 1f : -1f) * w;
                    b.BoxBetween(V(x, yy, BladeBase + z), V(x, yy, BladeBase + z + 0.012f), 0.001f, 0.0016f, M.PGold);
                }
            }
        }

        public static void MarcaZeus(MeshBuilder b)
        {
            foreach (float sx in Sides)
                for (int i = 0; i < 12; i++)
                {
                    float z0 = 0.14f + i * 0.065f, z1 = z0 + 0.065f;
                    float y0 = (i % 2 == 0 ? -1f : 1f) * BladeHalfWidth(z0) * 0.42f, y1 = (i % 2 == 0 ? 1f : -1f) * BladeHalfWidth(z1) * 0.42f;
                    float x0 = sx * (BladeHalfThick(z0) * (1f - 0.66f * Mathf.Abs(y0) / BladeHalfWidth(z0)) + 0.0012f);
                    float x1 = sx * (BladeHalfThick(z1) * (1f - 0.66f * Mathf.Abs(y1) / Mathf.Max(BladeHalfWidth(z1), 0.001f)) + 0.0012f);
                    b.BoxBetween(V(x0, y0, BladeBase + z0), V(x1, y1, BladeBase + z1), 0.003f, 0.009f, M.PMark);
                }
        }

        public static void MarcaPoseidon(MeshBuilder b)
        {
            for (int k = 0; k < 70; k++)
            {
                float z = 0.12f + k * 0.012f, a = k * 0.5f;
                float w = BladeHalfWidth(z), t = BladeHalfThick(z);
                b.Sphere(V(Mathf.Cos(a) * (t + 0.002f), Mathf.Sin(a) * w * 0.9f, BladeBase + z), 0.0068f, 0.0068f, 6, M.PMark);
            }
        }

        // ---------------- adaga de cintura
        /// <summary>
        /// Adaga curta na bainha, para pendurar no quadril: origem na boca da bainha, ponta em +Z, largura em Y,
        /// cabo em -Z. O lado +X fica virado para fora do corpo.
        /// </summary>
        public static void Adaga(MeshBuilder b)
        {
            // bainha de couro
            Loft(b, 14, M.PLeather,
                R(0, 0, 0.0f, 0.0085f, 0.024f), R(0, 0, 0.09f, 0.008f, 0.0225f), R(0, 0, 0.19f, 0.0068f, 0.0175f), R(0, 0, 0.25f, 0.0052f, 0.0115f));
            // boca e ponteira de bronze com filetes de ouro
            Loft(b, 14, M.PBronze, R(0, 0, -0.005f, 0.0102f, 0.027f), R(0, 0, 0.024f, 0.0098f, 0.0262f));
            Loft(b, 14, M.PGold, R(0, 0, 0.024f, 0.0104f, 0.0268f), R(0, 0, 0.029f, 0.0102f, 0.0264f));
            Loft(b, 14, M.PBronze, R(0, 0, 0.228f, 0.0072f, 0.015f), R(0, 0, 0.268f, 0.0056f, 0.0105f));
            Loft(b, 14, M.PGold, R(0, 0, 0.223f, 0.0076f, 0.0158f), R(0, 0, 0.228f, 0.0075f, 0.0155f));
            b.Sphere(V(0, 0, 0.276f), 0.0088f, 0.0088f, 10, M.PGold);
            // nervura na face de fora
            b.Box(V(0.0078f, 0, 0.125f), V(0.002f, 0.004f, 0.17f), M.PBronzeDark);
            // argolas de suspensão dos dois lados da boca
            foreach (float s in Sides)
                b.Torus(V(0, s * 0.031f, 0.008f), Quaternion.Euler(0f, 0f, 90f), 0.0075f, 0.0022f, 12, 5, M.PGold);
            // cabo: guarda curta, empunhadura de couro com anéis e pomo de ouro
            b.Box(V(0, 0, -0.012f), V(0.015f, 0.066f, 0.013f), M.PBronzeDark);
            foreach (float s in Sides) b.Sphere(V(0, s * 0.033f, -0.012f), 0.0085f, 0.0085f, 8, M.PGold);
            b.CylinderBetween(V(0, 0, -0.018f), V(0, 0, -0.098f), 0.0108f, 0.0098f, 12, M.PLeather);
            for (int k = 0; k < 4; k++)
                b.Torus(V(0, 0, -0.03f - k * 0.02f), Quaternion.Euler(90f, 0f, 0f), 0.0108f, 0.0016f, 12, 4, M.PGold);
            b.CylinderBetween(V(0, 0, -0.098f), V(0, 0, -0.106f), 0.0125f, 0.0135f, 12, M.PBronzeDark);
            b.Sphere(V(0, 0, -0.116f), 0.0155f, 0.0125f, 12, M.PGold);
        }

        // ---------------- aspis
        /// <summary>Aspis com ~0,9 m de diâmetro, centrado na origem, frente para +Z, alça (antilabe) em +X.</summary>
        public static void Escudo(MeshBuilder b)
        {
            Quaternion q = Quaternion.Euler(90f, 0f, 0f);
            b.Lathe(Vector3.zero, q, new[] { V2(0.455f, -0.022f), V2(0.455f, -0.005f), V2(0.44f, 0f), V2(0.41f, 0.012f), V2(0.34f, 0.032f), V2(0.24f, 0.05f), V2(0.12f, 0.061f), V2(0f, 0.065f) }, 56, M.PBronze, false, false);
            b.Lathe(Vector3.zero, q, new[] { V2(0f, 0.03f), V2(0.2f, 0.02f), V2(0.35f, 0f), V2(0.43f, -0.02f), V2(0.455f, -0.022f) }, 56, M.PWood, false, false);
            b.Torus(V(0, 0, 0.004f), q, 0.437f, 0.011f, 56, 8, M.PGold);
            b.Torus(V(0, 0, 0.033f), q, 0.30f, 0.004f, 56, 5, M.PBronzeDark);
            // porpax (braçadeira central) e antilabe (alça na borda)
            b.Torus(V(0, 0, 0.0f), Quaternion.Euler(0f, 0f, 90f), 0.05f, 0.01f, 16, 6, M.PLeather);
            b.BoxBetween(V(0.31f, -0.05f, -0.018f), V(0.31f, 0.05f, -0.018f), 0.024f, 0.018f, M.PLeather);
            // rebites de ouro em volta, por dentro do aro
            for (int k = 0; k < 28; k++)
            {
                float a = (k + 0.5f) * Mathf.PI * 2f / 28f;
                const float r = 0.398f;
                Ellipsoid(b, V(Mathf.Cos(a) * r, Mathf.Sin(a) * r, EscudoFrenteZ(r) + 0.0008f), 0.0068f, 0.0036f, M.PGold, 8);
            }
            // marcas de batalha: tocos de flechas quebradas cravados na face
            FlechaQuebrada(b, V2(0.29f, 0.16f), V(0.35f, 0.3f, 1f), 0.115f, 0.7f);
            FlechaQuebrada(b, V2(-0.21f, -0.26f), V(-0.45f, 0.1f, 1f), 0.075f, 2.1f);
            FlechaQuebrada(b, V2(-0.3f, 0.19f), V(-0.2f, -0.35f, 1f), 0.05f, 4.4f);
        }

        /// <summary>Toco de flecha quebrada cravado na face do escudo: haste lascada e rebarba do bronze em volta do furo.</summary>
        static void FlechaQuebrada(MeshBuilder b, Vector2 pos, Vector3 dir, float len, float seed)
        {
            float z0 = EscudoFrenteZ(pos.magnitude);
            dir = dir.normalized;
            Vector3 p0 = V(pos.x, pos.y, z0 - 0.012f), p1 = V(pos.x, pos.y, z0) + dir * len;
            b.CylinderBetween(p0, p1, 0.0046f, 0.0043f, 8, M.PWood);
            // ponta lascada
            for (int k = 0; k < 3; k++)
            {
                float a = seed + k * 2.2f;
                Vector3 off = V(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.0022f;
                b.CylinderBetween(p1 + off - dir * 0.004f, p1 + off * 1.6f + dir * (0.008f + 0.007f * k), 0.0024f, 0.0005f, 5, M.PWood);
            }
            // rebarba em volta do furo
            b.Torus(V(pos.x, pos.y, z0 + 0.0006f), Quaternion.Euler(90f, 0f, 0f), 0.0085f, 0.0026f, 12, 5, M.PBronzeDark);
        }

        /// <summary>Formiga dos Mirmidões em relevo na frente do escudo.</summary>
        public static void Emblema(MeshBuilder b) => Formiga(b, 1f, 0.056f, M.PEmblem);

        /// <summary>
        /// Formiga dos Mirmidões (símbolo de Pátroclo) no plano XY, relevo para +Z. s = escala
        /// (1 = tamanho do escudo, ~36 cm); z0 = altura da base do relevo.
        /// </summary>
        public static void Formiga(MeshBuilder b, float s, float z0, M m)
        {
            Vector3 P(float x, float y, float z) => V(x * s, y * s, z0 + (z - 0.056f) * s);
            void E(float x, float y, float z, float rxy, float rz, int seg) => Ellipsoid(b, P(x, y, z), rxy * s, rz * s, m, seg);
            void C(Vector3 a, Vector3 c, float r0, float r1) => b.CylinderBetween(a, c, r0 * s, r1 * s, 6, m);
            E(0f, 0.13f, 0.066f, 0.03f, 0.012f, 12);
            E(0f, 0.072f, 0.068f, 0.03f, 0.013f, 12);
            E(0f, 0.04f, 0.068f, 0.03f, 0.013f, 12);
            E(0f, -0.05f, 0.066f, 0.055f, 0.016f, 14);
            E(0f, -0.1f, 0.064f, 0.048f, 0.015f, 14);
            foreach (float k in Sides)
            {
                C(P(k * 0.025f, 0.07f, 0.07f), P(k * 0.11f, 0.13f, 0.062f), 0.008f, 0.008f);
                C(P(k * 0.11f, 0.13f, 0.062f), P(k * 0.15f, 0.18f, 0.058f), 0.008f, 0.006f);
                C(P(k * 0.028f, 0.055f, 0.07f), P(k * 0.13f, 0.05f, 0.062f), 0.008f, 0.008f);
                C(P(k * 0.13f, 0.05f, 0.062f), P(k * 0.18f, 0.02f, 0.056f), 0.008f, 0.006f);
                C(P(k * 0.025f, 0.04f, 0.07f), P(k * 0.1f, -0.03f, 0.062f), 0.008f, 0.008f);
                C(P(k * 0.1f, -0.03f, 0.062f), P(k * 0.14f, -0.1f, 0.058f), 0.008f, 0.006f);
                C(P(k * 0.012f, 0.155f, 0.07f), P(k * 0.04f, 0.2f, 0.066f), 0.006f, 0.005f);
                C(P(k * 0.04f, 0.2f, 0.066f), P(k * 0.075f, 0.215f, 0.062f), 0.005f, 0.004f);
            }
        }

        /// <summary>Olhos de coruja de Atena sobre o escudo.</summary>
        public static void MarcaAtena(MeshBuilder b)
        {
            Quaternion q = Quaternion.Euler(90f, 0f, 0f);
            foreach (float sx in Sides)
            {
                b.Torus(V(sx * 0.1f, 0.09f, 0.074f), q, 0.06f, 0.01f, 24, 6, M.PMark);
                b.Sphere(V(sx * 0.1f, 0.09f, 0.075f), 0.02f, 0.012f, 10, M.PMark);
            }
            b.BoxBetween(V(0f, 0.05f, 0.078f), V(0f, -0.03f, 0.09f), 0.03f, 0.02f, M.PMark);
        }

        /// <summary>Altura (z local) da face de dentro (madeira) do escudo a uma distância r do centro.</summary>
        public static float EscudoDentroZ(float r)
        {
            var prof = new[] { V2(0f, 0.03f), V2(0.2f, 0.02f), V2(0.35f, 0f), V2(0.43f, -0.02f), V2(0.455f, -0.022f) };
            for (int i = 0; i < prof.Length - 1; i++)
                if (r <= prof[i + 1].x)
                    return Mathf.Lerp(prof[i].y, prof[i + 1].y, (r - prof[i].x) / (prof[i + 1].x - prof[i].x));
            return -0.022f;
        }

        /// <summary>Altura (z local) da face de bronze do escudo a uma distância r do centro.</summary>
        public static float EscudoFrenteZ(float r)
        {
            var prof = new[] { V2(0f, 0.065f), V2(0.12f, 0.061f), V2(0.24f, 0.05f), V2(0.34f, 0.032f), V2(0.41f, 0.012f), V2(0.44f, 0f), V2(0.455f, -0.005f) };
            for (int i = 0; i < prof.Length - 1; i++)
                if (r <= prof[i + 1].x)
                    return Mathf.Lerp(prof[i].y, prof[i + 1].y, (r - prof[i].x) / (prof[i + 1].x - prof[i].x));
            return -0.022f;
        }
    }
}
