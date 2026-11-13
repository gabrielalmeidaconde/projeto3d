using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Troia.EditorTools
{
    /// <summary>Slots de material usados pela geração procedural do mapa.</summary>
    public enum M
    {
        Stone, MudBrick, Plaster, PlasterOchre, PlasterRed, PlasterWhite, Terracotta, Straw,
        Wood, WoodDark, WoodPainted, Roof, RoofTile, Cobble, Marble, Bronze, Gold,
        Cloth, ClothBlue, Dark, Charred, Rope, Foliage, Water, WindowLit, PaintedBlue, HorseWood,
        // personagem (Pátroclo)
        PBronze, PBronzeDark, PGold, PBlade, PCrimson, PCrimsonDark, PLinen, PLeather, PSkin, PHair, PEye, PCut, PEmblem, PWood, PMark,
        Count
    }

    /// <summary>
    /// Monta malhas a partir de primitivas (caixas, prismas, cunhas, sólidos de revolução)
    /// com várias submalhas (uma por material). As UVs são projetadas em metros, então as
    /// texturas mantêm a mesma escala em qualquer tamanho de peça.
    /// </summary>
    public class MeshBuilder
    {
        static readonly float[] UvScale = MakeUvScale();
        /// <summary>Metros por unidade de UV do slot de material.</summary>
        public static float UvScaleOf(M m) => UvScale[(int)m];

        static float[] MakeUvScale()
        {
            var s = new float[(int)M.Count];
            for (int i = 0; i < s.Length; i++) s[i] = 2f;
            s[(int)M.Stone] = 4f;
            s[(int)M.MudBrick] = 2.4f;
            s[(int)M.Plaster] = s[(int)M.PlasterOchre] = s[(int)M.PlasterRed] = s[(int)M.PlasterWhite] = 3f;
            s[(int)M.Terracotta] = 1.5f;
            s[(int)M.Wood] = s[(int)M.WoodDark] = s[(int)M.WoodPainted] = 2f;
            s[(int)M.Roof] = 3f;
            s[(int)M.RoofTile] = 2f;
            s[(int)M.Cobble] = 3f;
            s[(int)M.Marble] = 3f;
            s[(int)M.Foliage] = 1.5f;
            s[(int)M.Water] = 25f;
            s[(int)M.PBronze] = s[(int)M.PBronzeDark] = 0.5f;
            s[(int)M.PGold] = 0.3f;
            s[(int)M.PBlade] = 0.4f;
            s[(int)M.PCrimson] = 0.25f;
            s[(int)M.PCrimsonDark] = 0.5f;
            s[(int)M.PLinen] = 0.3f;
            s[(int)M.PLeather] = 0.35f;
            s[(int)M.PSkin] = 0.3f;
            s[(int)M.PHair] = 0.12f;
            s[(int)M.PEye] = s[(int)M.PCut] = s[(int)M.PEmblem] = s[(int)M.PMark] = 0.3f;
            s[(int)M.PWood] = 0.8f;
            return s;
        }

        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<int>[] _t = new List<int>[(int)M.Count];
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();
        Matrix4x4 _m = Matrix4x4.identity;

        public int VertexCount => _v.Count;
        public IReadOnlyList<Vector3> Vertices => _v;
        public IReadOnlyList<Vector3> Normals => _n;
        public IReadOnlyList<Vector2> Uvs => _uv;
        /// <summary>Triângulos do slot de material (null se não usado).</summary>
        public List<int> TrianglesOf(M m) => _t[(int)m];

        // ------------------------------------------------------------------ transformações
        public void Push(Vector3 pos, Quaternion rot)
        {
            _stack.Push(_m);
            _m = _m * Matrix4x4.TRS(pos, rot, Vector3.one);
        }

        public void Push(Vector3 pos) => Push(pos, Quaternion.identity);
        public void Pop() => _m = _stack.Pop();

        List<int> Tris(M m)
        {
            int i = (int)m;
            if (_t[i] == null) _t[i] = new List<int>();
            return _t[i];
        }

        static Vector2 ProjUv(Vector3 p, Vector3 n, M m)
        {
            Vector3 u, v;
            if (Mathf.Abs(n.y) > 0.95f)
            {
                u = Vector3.right;
                v = Vector3.forward;
            }
            else
            {
                u = Vector3.Cross(Vector3.up, n).normalized;
                v = Vector3.Cross(n, u);
            }
            float s = 1f / UvScale[(int)m];
            return new Vector2(Vector3.Dot(p, u) * s, Vector3.Dot(p, v) * s);
        }

        void Add(Vector3 p, Vector3 n, M m)
        {
            _v.Add(p);
            _n.Add(n);
            _uv.Add(ProjUv(p, n, m));
        }

        // ------------------------------------------------------------------ faces
        /// <summary>Quad plano (vértices em sentido horário vistos de fora).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, M m)
        {
            a = _m.MultiplyPoint3x4(a);
            b = _m.MultiplyPoint3x4(b);
            c = _m.MultiplyPoint3x4(c);
            d = _m.MultiplyPoint3x4(d);
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
            n.Normalize();
            int i = _v.Count;
            Add(a, n, m); Add(b, n, m); Add(c, n, m); Add(d, n, m);
            var t = Tris(m);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        /// <summary>Quad que garante a face voltada para 'outward' (espaço local).</summary>
        public void QuadOut(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, M m)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) Quad(a, d, c, b, m);
            else Quad(a, b, c, d, m);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, M m)
        {
            a = _m.MultiplyPoint3x4(a);
            b = _m.MultiplyPoint3x4(b);
            c = _m.MultiplyPoint3x4(c);
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i = _v.Count;
            Add(a, n, m); Add(b, n, m); Add(c, n, m);
            var t = Tris(m);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        public void TriOut(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, M m)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) Tri(a, c, b, m);
            else Tri(a, b, c, m);
        }

        // ------------------------------------------------------------------ sólidos
        public void Box(Vector3 center, Vector3 size, M m) => Box(center, size, m, Quaternion.identity);

        public void Box(Vector3 center, Vector3 size, M m, Quaternion rot)
        {
            Vector3 h = size * 0.5f;
            Face(center, rot, Vector3.up, Vector3.right, h.x, h.z, h.y, m);
            Face(center, rot, Vector3.down, Vector3.right, h.x, h.z, h.y, m);
            Face(center, rot, Vector3.right, Vector3.forward, h.z, h.y, h.x, m);
            Face(center, rot, Vector3.left, Vector3.back, h.z, h.y, h.x, m);
            Face(center, rot, Vector3.forward, Vector3.left, h.x, h.y, h.z, m);
            Face(center, rot, Vector3.back, Vector3.right, h.x, h.y, h.z, m);
        }

        void Face(Vector3 c, Quaternion r, Vector3 n, Vector3 u, float hu, float hv, float dist, M m)
        {
            Vector3 v = Vector3.Cross(u, n);
            Vector3 fc = c + r * (n * dist);
            Vector3 U = r * (u * hu);
            Vector3 V = r * (v * hv);
            Quad(fc - U - V, fc - U + V, fc + U + V, fc + U - V, m);
        }

        /// <summary>Caixa orientada de 'a' até 'b' (largura w, altura h na seção).</summary>
        public void BoxBetween(Vector3 a, Vector3 b, float w, float h, M m)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Vector3 dir = d / len;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            Box((a + b) * 0.5f, new Vector3(w, h, len), m, Quaternion.LookRotation(dir, up));
        }

        /// <summary>Telhado de duas águas: base em y=0, largura w (x), altura h, comprimento l (z).</summary>
        public void Prism(Vector3 baseCenter, float w, float h, float l, M slope, M gable, Quaternion rot)
        {
            float x = w * 0.5f, z = l * 0.5f;
            var l0 = new Vector3(-x, 0, -z); var r0 = new Vector3(x, 0, -z); var t0 = new Vector3(0, h, -z);
            var l1 = new Vector3(-x, 0, z); var r1 = new Vector3(x, 0, z); var t1 = new Vector3(0, h, z);
            Push(baseCenter, rot);
            QuadOut(l0, t0, t1, l1, new Vector3(-h, x, 0), slope);
            QuadOut(r0, r1, t1, t0, new Vector3(h, x, 0), slope);
            TriOut(l0, r0, t0, Vector3.back, gable);
            TriOut(l1, t1, r1, Vector3.forward, gable);
            QuadOut(l0, l1, r1, r0, Vector3.down, gable);
            Pop();
        }

        /// <summary>Cunha que sobe ao longo de +z: espessura t (x), altura h, comprimento l.</summary>
        public void Wedge(Vector3 baseCenter, float t, float h, float l, M m, Quaternion rot)
        {
            float x = t * 0.5f, z = l * 0.5f;
            var a0 = new Vector3(-x, 0, -z); var b0 = new Vector3(-x, 0, z); var c0 = new Vector3(-x, h, z);
            var a1 = new Vector3(x, 0, -z); var b1 = new Vector3(x, 0, z); var c1 = new Vector3(x, h, z);
            Push(baseCenter, rot);
            QuadOut(a0, a1, b1, b0, Vector3.down, m);
            QuadOut(b0, b1, c1, c0, Vector3.forward, m);
            QuadOut(a0, c0, c1, a1, new Vector3(0, l, -h), m);
            TriOut(a0, b0, c0, Vector3.left, m);
            TriOut(a1, c1, b1, Vector3.right, m);
            Pop();
        }

        /// <summary>Sólido de revolução em torno do eixo Y local. Perfil = (raio, altura).</summary>
        public void Lathe(Vector3 origin, Quaternion rot, IList<Vector2> prof, int seg, M m, bool capTop = true, bool capBottom = false, float arcDeg = 360f)
        {
            Push(origin, rot);
            int n = prof.Count;
            var pn = new Vector2[n];
            for (int j = 0; j < n; j++)
            {
                Vector2 t = prof[Mathf.Min(j + 1, n - 1)] - prof[Mathf.Max(j - 1, 0)];
                pn[j] = new Vector2(t.y, -t.x).normalized;
            }
            var vl = new float[n];
            float maxR = 0.05f;
            for (int j = 0; j < n; j++)
            {
                if (j > 0) vl[j] = vl[j - 1] + Vector2.Distance(prof[j], prof[j - 1]);
                maxR = Mathf.Max(maxR, prof[j].x);
            }
            float s = 1f / UvScale[(int)m];
            float arc = arcDeg * Mathf.Deg2Rad;
            float circ = arc * maxR;
            int start = _v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * arc / seg;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int j = 0; j < n; j++)
                {
                    var p = new Vector3(ca * prof[j].x, prof[j].y, sa * prof[j].x);
                    var nn = new Vector3(ca * pn[j].x, pn[j].y, sa * pn[j].x);
                    _v.Add(_m.MultiplyPoint3x4(p));
                    _n.Add(_m.MultiplyVector(nn).normalized);
                    _uv.Add(new Vector2((float)i / seg * circ * s, vl[j] * s));
                }
            }
            var tr = Tris(m);
            for (int i = 0; i < seg; i++)
            {
                for (int j = 0; j < n - 1; j++)
                {
                    int a0 = start + i * n + j, a1 = a0 + 1, b0 = a0 + n, b1 = b0 + 1;
                    tr.Add(a0); tr.Add(a1); tr.Add(b1);
                    tr.Add(a0); tr.Add(b1); tr.Add(b0);
                }
            }
            bool full = arcDeg >= 359.9f;
            if (full && capTop && prof[n - 1].x > 1e-4f) Cap(prof[n - 1], seg, true, m);
            if (full && capBottom && prof[0].x > 1e-4f) Cap(prof[0], seg, false, m);
            Pop();
        }

        void Cap(Vector2 pr, int seg, bool up, M m)
        {
            var c = new Vector3(0, pr.y, 0);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                var p0 = new Vector3(Mathf.Cos(a0) * pr.x, pr.y, Mathf.Sin(a0) * pr.x);
                var p1 = new Vector3(Mathf.Cos(a1) * pr.x, pr.y, Mathf.Sin(a1) * pr.x);
                if (up) Tri(c, p1, p0, m);
                else Tri(c, p0, p1, m);
            }
        }

        public void Cylinder(Vector3 baseCenter, float r0, float r1, float h, int seg, M m, bool caps = true)
            => Lathe(baseCenter, Quaternion.identity, new[] { new Vector2(r0, 0), new Vector2(r1, h) }, seg, m, caps, caps);

        public void CylinderBetween(Vector3 a, Vector3 b, float r0, float r1, int seg, M m, bool caps = true)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Lathe(a, Quaternion.FromToRotation(Vector3.up, d / len),
                new[] { new Vector2(r0, 0), new Vector2(r1, len) }, seg, m, caps, caps);
        }

        /// <summary>Elipsoide com raio horizontal r e vertical ry, centrado em c.</summary>
        public void Sphere(Vector3 c, float r, float ry, int seg, M m)
        {
            int rings = Mathf.Max(4, seg / 2);
            var prof = new Vector2[rings + 1];
            for (int j = 0; j <= rings; j++)
            {
                float t = Mathf.PI * j / rings;
                prof[j] = new Vector2(Mathf.Sin(t) * r, ry - Mathf.Cos(t) * ry);
            }
            Lathe(c - Vector3.up * ry, Quaternion.identity, prof, seg, m, false, false);
        }

        /// <summary>
        /// Superfície "lofted": anéis elípticos ao longo de uma espinha (centros c, raios r = (lateral, "para cima")).
        /// O referencial é transportado em paralelo ao longo da curva (sem torção). UV: (em volta, ao longo).
        /// a0..a1 em graus permite superfícies abertas (ex.: uma manta sobre o lombo).
        /// </summary>
        public void Loft(IList<Vector3> c, IList<Vector2> r, int seg, M m, bool capStart = true, bool capEnd = true, float a0 = 0f, float a1 = 360f)
        {
            int n = c.Count;
            if (n < 2) return;
            float arc = (a1 - a0) * Mathf.Deg2Rad, start = a0 * Mathf.Deg2Rad;
            bool closed = a1 - a0 >= 359.9f;
            var tan = new Vector3[n];
            var side = new Vector3[n];
            var upv = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 t = c[Mathf.Min(i + 1, n - 1)] - c[Mathf.Max(i - 1, 0)];
                tan[i] = t.sqrMagnitude > 1e-10f ? t.normalized : (i > 0 ? tan[i - 1] : Vector3.forward);
                Vector3 u;
                if (i == 0)
                {
                    Vector3 refUp = Mathf.Abs(Vector3.Dot(tan[0], Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
                    u = refUp - Vector3.Dot(refUp, tan[0]) * tan[0];
                }
                else
                {
                    u = Quaternion.FromToRotation(tan[i - 1], tan[i]) * upv[i - 1];
                    u -= Vector3.Dot(u, tan[i]) * tan[i];
                }
                upv[i] = u.normalized;
                side[i] = Vector3.Cross(upv[i], tan[i]).normalized;
            }
            float s = 1f / UvScale[(int)m];
            float perim = 0f;
            for (int i = 0; i < n; i++) perim = Mathf.Max(perim, arc * Mathf.Sqrt((r[i].x * r[i].x + r[i].y * r[i].y) * 0.5f));
            int rv = seg + 1;
            int first = _v.Count;
            float along = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) along += Vector3.Distance(c[i], c[i - 1]);
                float rx = Mathf.Max(r[i].x, 1e-4f), ry = Mathf.Max(r[i].y, 1e-4f);
                for (int j = 0; j <= seg; j++)
                {
                    float a = start + arc * j / seg;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    Vector3 p = c[i] + side[i] * (ca * r[i].x) + upv[i] * (sa * r[i].y);
                    Vector3 nn = (side[i] * (ca / rx) + upv[i] * (sa / ry)).normalized;
                    _v.Add(_m.MultiplyPoint3x4(p));
                    _n.Add(_m.MultiplyVector(nn).normalized);
                    _uv.Add(new Vector2((float)j / seg * perim * s, along * s));
                }
            }
            var tr = Tris(m);
            for (int i = 0; i < n - 1; i++)
            {
                for (int j = 0; j < seg; j++)
                {
                    int A = first + i * rv + j, B = A + rv, C = B + 1, D = A + 1;
                    tr.Add(A); tr.Add(D); tr.Add(C);
                    tr.Add(A); tr.Add(C); tr.Add(B);
                }
            }
            if (!closed) return;
            if (capStart && r[0].x > 1e-3f) LoftCap(c[0], side[0], upv[0], r[0], seg, -tan[0], m);
            if (capEnd && r[n - 1].x > 1e-3f) LoftCap(c[n - 1], side[n - 1], upv[n - 1], r[n - 1], seg, tan[n - 1], m);
        }

        void LoftCap(Vector3 c, Vector3 side, Vector3 up, Vector2 r, int seg, Vector3 outward, M m)
        {
            for (int j = 0; j < seg; j++)
            {
                float a0 = j * Mathf.PI * 2f / seg, a1 = (j + 1) * Mathf.PI * 2f / seg;
                Vector3 p0 = c + side * (Mathf.Cos(a0) * r.x) + up * (Mathf.Sin(a0) * r.y);
                Vector3 p1 = c + side * (Mathf.Cos(a1) * r.x) + up * (Mathf.Sin(a1) * r.y);
                TriOut(c, p0, p1, outward, m);
            }
        }

        /// <summary>Anel (toro) em torno do eixo Y local: raio R, espessura t.</summary>
        public void Torus(Vector3 center, Quaternion rot, float R, float t, int seg, int segT, M m)
        {
            var prof = new Vector2[segT + 1];
            for (int k = 0; k <= segT; k++)
            {
                float th = k * Mathf.PI * 2f / segT;
                prof[k] = new Vector2(R + t * Mathf.Cos(th), t * Mathf.Sin(th));
            }
            Lathe(center, rot, prof, seg, m, false, false);
        }

        /// <summary>
        /// Grade [linha, coluna] com normais dadas (sombreamento suave). As faces são orientadas
        /// para o lado das normais. UV: (ao longo das colunas, ao longo das linhas) em metros.
        /// </summary>
        public void Grid(Vector3[,] p, Vector3[,] nrm, M m, bool swapUv = false)
        {
            int R = p.GetLength(0), C = p.GetLength(1);
            float s = 1f / UvScale[(int)m];
            int first = _v.Count;
            for (int r = 0; r < R; r++)
            {
                float u = 0f, v = 0f;
                for (int c = 0; c < C; c++)
                {
                    if (c > 0) u += Vector3.Distance(p[r, c], p[r, c - 1]);
                    _v.Add(_m.MultiplyPoint3x4(p[r, c]));
                    _n.Add(_m.MultiplyVector(nrm[r, c]).normalized);
                    v = 0f;
                    for (int rr = 1; rr <= r; rr++) v += Vector3.Distance(p[rr, c], p[rr - 1, c]);
                    _uv.Add(swapUv ? new Vector2(v * s, u * s) : new Vector2(u * s, v * s));
                }
            }
            var t = Tris(m);
            for (int r = 0; r < R - 1; r++)
                for (int c = 0; c < C - 1; c++)
                {
                    int A = first + r * C + c, B = A + 1, D = A + C, E = D + 1;
                    Vector3 fn = Vector3.Cross(_v[D] - _v[A], _v[E] - _v[A]);
                    if (Vector3.Dot(fn, _n[A] + _n[E]) >= 0f) { t.Add(A); t.Add(D); t.Add(E); t.Add(A); t.Add(E); t.Add(B); }
                    else { t.Add(A); t.Add(E); t.Add(D); t.Add(A); t.Add(B); t.Add(E); }
                }
        }

        /// <summary>Folha dupla face a partir de uma grade de pontos [linha, coluna] (ex.: capa).</summary>
        public void Sheet(Vector3[,] p, M front, M back)
        {
            int R = p.GetLength(0), C = p.GetLength(1);
            var nrm = new Vector3[R, C];
            for (int r = 0; r < R; r++)
                for (int c = 0; c < C; c++)
                {
                    Vector3 du = p[r, Mathf.Min(c + 1, C - 1)] - p[r, Mathf.Max(c - 1, 0)];
                    Vector3 dv = p[Mathf.Min(r + 1, R - 1), c] - p[Mathf.Max(r - 1, 0), c];
                    nrm[r, c] = Vector3.Cross(dv, du).normalized;
                }
            float w = 0f, hgt = 0f;
            for (int c = 1; c < C; c++) w += Vector3.Distance(p[0, c], p[0, c - 1]);
            for (int r = 1; r < R; r++) hgt += Vector3.Distance(p[r, 0], p[r - 1, 0]);
            for (int side = 0; side < 2; side++)
            {
                M m = side == 0 ? front : back;
                float s = 1f / UvScale[(int)m];
                int first = _v.Count;
                for (int r = 0; r < R; r++)
                    for (int c = 0; c < C; c++)
                    {
                        _v.Add(_m.MultiplyPoint3x4(p[r, c]));
                        _n.Add(_m.MultiplyVector(side == 0 ? nrm[r, c] : -nrm[r, c]).normalized);
                        _uv.Add(new Vector2((float)c / (C - 1) * w * s, (float)r / (R - 1) * hgt * s));
                    }
                var t = Tris(m);
                for (int r = 0; r < R - 1; r++)
                    for (int c = 0; c < C - 1; c++)
                    {
                        int A = first + r * C + c, B = A + 1, D = A + C, E = D + 1;
                        if (side == 0) { t.Add(A); t.Add(E); t.Add(B); t.Add(A); t.Add(D); t.Add(E); }
                        else { t.Add(A); t.Add(B); t.Add(E); t.Add(A); t.Add(E); t.Add(D); }
                    }
            }
        }

        // ------------------------------------------------------------------ saída
        public Mesh ToMesh(string name, Material[] palette, out Material[] mats)
        {
            var mesh = new Mesh { name = name };
            if (_v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_v);
            mesh.SetNormals(_n);
            mesh.SetUVs(0, _uv);
            var used = new List<int>();
            for (int i = 0; i < _t.Length; i++)
                if (_t[i] != null && _t[i].Count > 0) used.Add(i);
            mesh.subMeshCount = used.Count;
            for (int k = 0; k < used.Count; k++) mesh.SetTriangles(_t[used[k]], k, false);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mats = new Material[used.Count];
            for (int k = 0; k < used.Count; k++) mats[k] = palette[used[k]];
            return mesh;
        }
    }
}
