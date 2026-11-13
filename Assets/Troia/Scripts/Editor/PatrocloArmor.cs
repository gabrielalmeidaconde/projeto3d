using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Troia.EditorTools
{
    /// <summary>Corpo do MakeHuman na pose de repouso: vértices, pesos e busca do vértice mais próximo.</summary>
    public class BodyInfo
    {
        public readonly Vector3[] p, n;
        public readonly BoneWeight[] w;
        public readonly int[] t;
        /// <summary>Osso dominante de cada vértice.</summary>
        public readonly int[] dom;
        public readonly string[] bones;
        public readonly int[] parent;
        public readonly Vector3[] head, tail;
        readonly Dictionary<string, int> _index = new Dictionary<string, int>();

        const float Cell = 0.04f;
        readonly Dictionary<long, List<int>> _grid = new Dictionary<long, List<int>>();

        /// <param name="meshName">Malha do JSON a usar. Padrão: "CorpoAjuste" (malha base, leve), a forma usada
        /// para tudo que veste o corpo. "Corpo" é a malha subdividida que aparece no jogo.</param>
        public BodyInfo(MhData d, string meshName = null)
        {
            int nb = d.bones.Length;
            bones = new string[nb];
            parent = new int[nb];
            head = new Vector3[nb];
            tail = new Vector3[nb];
            for (int i = 0; i < nb; i++)
            {
                var b = d.bones[i];
                bones[i] = b.name;
                parent[i] = b.parent;
                head[i] = new Vector3(b.head[0], b.head[1], b.head[2]);
                tail[i] = new Vector3(b.tail[0], b.tail[1], b.tail[2]);
                _index[b.name] = i;
            }
            MhMesh m = null;
            // a malha base (sem subdivisão) é mais leve e já é a forma usada para tudo que veste o corpo
            foreach (var mm in d.meshes) if (mm.name == (meshName ?? "CorpoAjuste")) m = mm;
            if (m == null) foreach (var mm in d.meshes) if (mm.name == "Corpo") m = mm;
            int nv = m.vertices.Length / 3;
            p = new Vector3[nv];
            n = new Vector3[nv];
            w = new BoneWeight[nv];
            dom = new int[nv];
            for (int i = 0; i < nv; i++)
            {
                p[i] = new Vector3(m.vertices[i * 3], m.vertices[i * 3 + 1], m.vertices[i * 3 + 2]);
                n[i] = new Vector3(m.normals[i * 3], m.normals[i * 3 + 1], m.normals[i * 3 + 2]);
                w[i] = new BoneWeight
                {
                    boneIndex0 = m.boneIndex[i * 4], weight0 = m.boneWeight[i * 4],
                    boneIndex1 = m.boneIndex[i * 4 + 1], weight1 = m.boneWeight[i * 4 + 1],
                    boneIndex2 = m.boneIndex[i * 4 + 2], weight2 = m.boneWeight[i * 4 + 2],
                    boneIndex3 = m.boneIndex[i * 4 + 3], weight3 = m.boneWeight[i * 4 + 3],
                };
                dom[i] = m.boneIndex[i * 4];
                long k = Key(p[i]);
                if (!_grid.TryGetValue(k, out var l)) _grid[k] = l = new List<int>();
                l.Add(i);
            }
            t = m.triangles;
        }

        public int B(string name) => _index[name];
        public bool Has(string name) => _index.ContainsKey(name);
        public Vector3 Head(string b) => head[_index[b]];
        public Vector3 Tail(string b) => tail[_index[b]];

        /// <summary>Conjunto de ossos por nome; "x*" casa com o prefixo x.</summary>
        public HashSet<int> Set(params string[] patterns)
        {
            var s = new HashSet<int>();
            for (int i = 0; i < bones.Length; i++)
                foreach (var pat in patterns)
                    if (pat.EndsWith("*") ? bones[i].StartsWith(pat.Substring(0, pat.Length - 1)) : bones[i] == pat)
                        s.Add(i);
            return s;
        }

        /// <summary>O osso e todos os seus descendentes.</summary>
        public HashSet<int> Subtree(string root)
        {
            var s = new HashSet<int> { B(root) };
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < bones.Length; i++)
                    if (!s.Contains(i) && parent[i] >= 0 && s.Contains(parent[i])) { s.Add(i); grew = true; }
            }
            return s;
        }

        public static float Weight(BoneWeight bw, HashSet<int> set)
        {
            float s = 0f;
            if (set.Contains(bw.boneIndex0)) s += bw.weight0;
            if (set.Contains(bw.boneIndex1)) s += bw.weight1;
            if (set.Contains(bw.boneIndex2)) s += bw.weight2;
            if (set.Contains(bw.boneIndex3)) s += bw.weight3;
            return s;
        }

        public static BoneWeight Rigid(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1f };

        static long Key(Vector3 q) => Key(Mathf.FloorToInt(q.x / Cell), Mathf.FloorToInt(q.y / Cell), Mathf.FloorToInt(q.z / Cell));
        static long Key(int x, int y, int z) => ((long)(x + 4096) << 40) | ((long)(y + 4096) << 20) | (long)(z + 4096);

        /// <summary>Vértice do corpo mais próximo de q que passa no filtro (-1 se nenhum até ~0,5 m).</summary>
        public int Nearest(Vector3 q, Func<int, bool> ok)
        {
            int cx = Mathf.FloorToInt(q.x / Cell), cy = Mathf.FloorToInt(q.y / Cell), cz = Mathf.FloorToInt(q.z / Cell);
            int best = -1;
            float bd = float.MaxValue;
            for (int r = 0; r <= 12; r++)
            {
                for (int x = -r; x <= r; x++)
                    for (int y = -r; y <= r; y++)
                        for (int z = -r; z <= r; z++)
                        {
                            if (Mathf.Max(Mathf.Abs(x), Mathf.Max(Mathf.Abs(y), Mathf.Abs(z))) != r) continue;
                            if (!_grid.TryGetValue(Key(cx + x, cy + y, cz + z), out var l)) continue;
                            foreach (int i in l)
                            {
                                float d = (p[i] - q).sqrMagnitude;
                                if (d < bd && (ok == null || ok(i))) { bd = d; best = i; }
                            }
                        }
                // tudo que está fora do cubo de raio r fica a pelo menos r*Cell
                if (best >= 0 && Mathf.Sqrt(bd) <= r * Cell) break;
            }
            return best;
        }

        /// <summary>Pesos do vértice do corpo mais próximo (filtrado), para peças que acompanham a pele.</summary>
        public BoneWeight NearestWeights(Vector3 q, Func<int, bool> ok)
        {
            int i = Nearest(q, ok);
            return i >= 0 ? w[i] : Rigid(0);
        }
    }

    /// <summary>
    /// Casca de superfície (normalmente recortada do corpo) para fazer armadura sob medida:
    /// recorte por funções implícitas, suavização de Taubin, deslocamento pela normal e bordas.
    /// Os vértices guardam os pesos do esqueleto, então a peça deforma junto com o corpo.
    /// </summary>
    public class Shell
    {
        public readonly List<Vector3> p = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<BoneWeight> w = new List<BoneWeight>();
        public List<int> t = new List<int>();

        // topologia (vértices soldados por posição)
        int[] _grp;
        int _groups;
        int[] _rep;
        Vector3[] _gn;
        bool[] _gBoundary;
        List<int>[] _gNbr, _gBNbr;
        Dictionary<long, int> _gEdgeCount;
        bool _dirty = true;

        public static Shell FromBody(BodyInfo b, Func<int, bool> vertexOk)
        {
            var s = new Shell();
            var map = new Dictionary<int, int>();
            int Get(int v)
            {
                if (map.TryGetValue(v, out int id)) return id;
                id = s.p.Count;
                s.p.Add(b.p[v]);
                s.n.Add(b.n[v]);
                s.w.Add(b.w[v]);
                map[v] = id;
                return id;
            }
            for (int i = 0; i < b.t.Length; i += 3)
            {
                int a = b.t[i], c = b.t[i + 1], d = b.t[i + 2];
                if (!vertexOk(a) || !vertexOk(c) || !vertexOk(d)) continue;
                s.t.Add(Get(a));
                s.t.Add(Get(c));
                s.t.Add(Get(d));
            }
            return s;
        }

        /// <summary>Casca a partir de uma grade [linha, coluna]; 'outward' orienta as faces.</summary>
        public static Shell FromGrid(Vector3[,] pts, BoneWeight bw, Vector3 outwardFrom)
        {
            var s = new Shell();
            int R = pts.GetLength(0), C = pts.GetLength(1);
            for (int r = 0; r < R; r++)
                for (int c = 0; c < C; c++)
                {
                    s.p.Add(pts[r, c]);
                    s.n.Add((pts[r, c] - outwardFrom).normalized);
                    s.w.Add(bw);
                }
            for (int r = 0; r < R - 1; r++)
                for (int c = 0; c < C - 1; c++)
                {
                    int A = r * C + c, B = A + 1, D = A + C, E = D + 1;
                    s.AddTriOut(A, B, E, outwardFrom);
                    s.AddTriOut(A, E, D, outwardFrom);
                }
            return s;
        }

        void AddTriOut(int a, int b, int c, Vector3 from)
        {
            Vector3 nn = Vector3.Cross(p[b] - p[a], p[c] - p[a]);
            if (nn.sqrMagnitude < 1e-20f) return;
            Vector3 ctr = (p[a] + p[b] + p[c]) / 3f;
            if (Vector3.Dot(nn, ctr - from) < 0f) { t.Add(a); t.Add(c); t.Add(b); }
            else { t.Add(a); t.Add(b); t.Add(c); }
        }

        public Shell Clone()
        {
            var s = new Shell();
            s.p.AddRange(p);
            s.n.AddRange(n);
            s.w.AddRange(w);
            s.t = new List<int>(t);
            return s;
        }

        // ------------------------------------------------------------------ recorte
        public static BoneWeight LerpW(BoneWeight a, BoneWeight b, float s)
        {
            var d = new Dictionary<int, float>();
            void Add(int i, float v)
            {
                if (v <= 0f) return;
                d[i] = d.TryGetValue(i, out float o) ? o + v : v;
            }
            Add(a.boneIndex0, a.weight0 * (1 - s)); Add(a.boneIndex1, a.weight1 * (1 - s));
            Add(a.boneIndex2, a.weight2 * (1 - s)); Add(a.boneIndex3, a.weight3 * (1 - s));
            Add(b.boneIndex0, b.weight0 * s); Add(b.boneIndex1, b.weight1 * s);
            Add(b.boneIndex2, b.weight2 * s); Add(b.boneIndex3, b.weight3 * s);
            return Top4(d);
        }

        public static BoneWeight Top4(Dictionary<int, float> d)
        {
            var l = new List<KeyValuePair<int, float>>(d);
            l.Sort((x, y) => y.Value.CompareTo(x.Value));
            float sum = 0f;
            for (int i = 0; i < Mathf.Min(4, l.Count); i++) sum += l[i].Value;
            if (sum <= 0f) return BodyInfo.Rigid(0);
            var r = new BoneWeight();
            if (l.Count > 0) { r.boneIndex0 = l[0].Key; r.weight0 = l[0].Value / sum; }
            if (l.Count > 1) { r.boneIndex1 = l[1].Key; r.weight1 = l[1].Value / sum; }
            if (l.Count > 2) { r.boneIndex2 = l[2].Key; r.weight2 = l[2].Value / sum; }
            if (l.Count > 3) { r.boneIndex3 = l[3].Key; r.weight3 = l[3].Value / sum; }
            return r;
        }

        /// <summary>Mantém só a parte onde f(posição, pesos) ≥ 0, cortando os triângulos na curva f = 0.</summary>
        public Shell Clip(Func<Vector3, BoneWeight, float> f)
        {
            int nv = p.Count;
            var fv = new float[nv];
            for (int i = 0; i < nv; i++)
            {
                float v = f(p[i], w[i]);
                fv[i] = Mathf.Abs(v) < 1e-7f ? 1e-7f : v;
            }
            var edge = new Dictionary<long, int>();
            int Cut(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (edge.TryGetValue(key, out int id)) return id;
                // ponto de corte: interpolação linear refinada por bisseção na função de verdade. Sem isso,
                // onde a função tem quinas o corte anda em degraus pela malha e a borda fica ondulada.
                float s = fv[a] / (fv[a] - fv[b]);
                float lo = 0f, hi = 1f;
                bool posA = fv[a] >= 0f;
                for (int it = 0; it < 7; it++)
                {
                    float fm = f(Vector3.Lerp(p[a], p[b], s), s < 0.5f ? w[a] : w[b]);
                    if (Mathf.Abs(fm) < 1e-6f) break;
                    if ((fm >= 0f) == posA) lo = s; else hi = s;
                    s = 0.5f * (lo + hi);
                }
                id = p.Count;
                p.Add(Vector3.Lerp(p[a], p[b], s));
                n.Add(Vector3.Lerp(n[a], n[b], s).normalized);
                w.Add(LerpW(w[a], w[b], s));
                edge[key] = id;
                return id;
            }
            var nt = new List<int>(t.Count);
            for (int i = 0; i < t.Count; i += 3)
            {
                int a = t[i], b = t[i + 1], c = t[i + 2];
                int k = (fv[a] >= 0 ? 1 : 0) + (fv[b] >= 0 ? 1 : 0) + (fv[c] >= 0 ? 1 : 0);
                if (k == 3) { nt.Add(a); nt.Add(b); nt.Add(c); continue; }
                if (k == 0) continue;
                bool Odd(int v) => k == 1 ? fv[v] >= 0 : fv[v] < 0;
                int x = a, y = b, z = c;
                if (Odd(b)) { x = b; y = c; z = a; }
                else if (Odd(c)) { x = c; y = a; z = b; }
                int exy = Cut(x, y), exz = Cut(x, z);
                if (k == 1) { nt.Add(x); nt.Add(exy); nt.Add(exz); }
                else { nt.Add(exy); nt.Add(y); nt.Add(z); nt.Add(exy); nt.Add(z); nt.Add(exz); }
            }
            t = nt;
            Compact();
            return this;
        }

        void Compact()
        {
            var map = new int[p.Count];
            for (int i = 0; i < map.Length; i++) map[i] = -1;
            var np = new List<Vector3>();
            var nn = new List<Vector3>();
            var nw = new List<BoneWeight>();
            for (int i = 0; i < t.Count; i++)
            {
                int v = t[i];
                if (map[v] < 0)
                {
                    map[v] = np.Count;
                    np.Add(p[v]);
                    nn.Add(n[v]);
                    nw.Add(w[v]);
                }
                t[i] = map[v];
            }
            p.Clear(); p.AddRange(np);
            n.Clear(); n.AddRange(nn);
            w.Clear(); w.AddRange(nw);
            _dirty = true;
        }

        // ------------------------------------------------------------------ topologia
        void Topology()
        {
            if (!_dirty) return;
            _dirty = false;
            var key = new Dictionary<(int, int, int), int>();
            _grp = new int[p.Count];
            var rep = new List<int>();
            for (int i = 0; i < p.Count; i++)
            {
                var k = (Mathf.RoundToInt(p[i].x * 1e5f), Mathf.RoundToInt(p[i].y * 1e5f), Mathf.RoundToInt(p[i].z * 1e5f));
                if (!key.TryGetValue(k, out int g)) { g = rep.Count; key[k] = g; rep.Add(i); }
                _grp[i] = g;
            }
            _groups = rep.Count;
            _rep = rep.ToArray();
            // remove triângulos degenerados depois da solda
            var nt = new List<int>(t.Count);
            for (int i = 0; i < t.Count; i += 3)
            {
                int ga = _grp[t[i]], gb = _grp[t[i + 1]], gc = _grp[t[i + 2]];
                if (ga == gb || gb == gc || ga == gc) continue;
                nt.Add(t[i]); nt.Add(t[i + 1]); nt.Add(t[i + 2]);
            }
            t = nt;
            _gEdgeCount = new Dictionary<long, int>();
            for (int i = 0; i < t.Count; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    long ek = EKey(_grp[t[i + e]], _grp[t[i + (e + 1) % 3]]);
                    _gEdgeCount[ek] = _gEdgeCount.TryGetValue(ek, out int c) ? c + 1 : 1;
                }
            _gNbr = new List<int>[_groups];
            _gBNbr = new List<int>[_groups];
            _gBoundary = new bool[_groups];
            for (int g = 0; g < _groups; g++) { _gNbr[g] = new List<int>(6); _gBNbr[g] = new List<int>(2); }
            foreach (var kv in _gEdgeCount)
            {
                int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
                _gNbr[a].Add(b);
                _gNbr[b].Add(a);
                if (kv.Value == 1)
                {
                    _gBoundary[a] = _gBoundary[b] = true;
                    _gBNbr[a].Add(b);
                    _gBNbr[b].Add(a);
                }
            }
            Normals();
        }

        static long EKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        void Normals()
        {
            _gn = new Vector3[_groups];
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = p[t[i]], b = p[t[i + 1]], c = p[t[i + 2]];
                Vector3 nn = Vector3.Cross(b - a, c - a);
                _gn[_grp[t[i]]] += nn;
                _gn[_grp[t[i + 1]]] += nn;
                _gn[_grp[t[i + 2]]] += nn;
            }
            for (int g = 0; g < _groups; g++) _gn[g] = _gn[g].normalized;
            for (int i = 0; i < p.Count; i++) n[i] = _gn[_grp[i]];
        }

        public int VertexCount => p.Count;

        /// <summary>Mantém só o maior pedaço conectado.</summary>
        public Shell KeepLargest()
        {
            Topology();
            var comp = new int[_groups];
            for (int g = 0; g < _groups; g++) comp[g] = -1;
            int nc = 0;
            var sizes = new List<int>();
            var stack = new Stack<int>();
            for (int g = 0; g < _groups; g++)
            {
                if (comp[g] >= 0) continue;
                int size = 0;
                comp[g] = nc;
                stack.Push(g);
                while (stack.Count > 0)
                {
                    int x = stack.Pop();
                    size++;
                    foreach (int y in _gNbr[x])
                        if (comp[y] < 0) { comp[y] = nc; stack.Push(y); }
                }
                sizes.Add(size);
                nc++;
            }
            int best = 0;
            for (int c = 1; c < nc; c++) if (sizes[c] > sizes[best]) best = c;
            var nt = new List<int>();
            for (int i = 0; i < t.Count; i += 3)
                if (comp[_grp[t[i]]] == best) { nt.Add(t[i]); nt.Add(t[i + 1]); nt.Add(t[i + 2]); }
            t = nt;
            Compact();
            return this;
        }

        /// <summary>Suavização de Taubin (não encolhe). Na borda, suaviza só ao longo da borda.</summary>
        public Shell Smooth(int iterations, float lambda = 0.5f, float mu = -0.53f, Func<Vector3, float> amount = null)
        {
            Topology();
            var cur = new Vector3[_groups];
            for (int g = 0; g < _groups; g++) cur[g] = p[_rep[g]];
            var amt = new float[_groups];
            for (int g = 0; g < _groups; g++) amt[g] = amount == null ? 1f : Mathf.Clamp01(amount(cur[g]));
            var tmp = new Vector3[_groups];
            for (int it = 0; it < iterations; it++)
                for (int pass = 0; pass < 2; pass++)
                {
                    float f = pass == 0 ? lambda : mu;
                    for (int g = 0; g < _groups; g++)
                    {
                        var nb = _gBoundary[g] ? _gBNbr[g] : _gNbr[g];
                        if (nb.Count == 0) { tmp[g] = cur[g]; continue; }
                        Vector3 avg = Vector3.zero;
                        foreach (int x in nb) avg += cur[x];
                        avg /= nb.Count;
                        tmp[g] = cur[g] + f * amt[g] * (avg - cur[g]);
                    }
                    var sw = cur; cur = tmp; tmp = sw;
                }
            for (int i = 0; i < p.Count; i++) p[i] = cur[_grp[i]];
            Normals();
            return this;
        }

        /// <summary>Desloca cada ponto pela normal (d em metros, pode variar com a posição).</summary>
        public Shell Offset(Func<Vector3, float> d)
        {
            Topology();
            var np = new Vector3[_groups];
            for (int g = 0; g < _groups; g++)
            {
                Vector3 q = p[_rep[g]];
                np[g] = q + _gn[g] * d(q);
            }
            for (int i = 0; i < p.Count; i++) p[i] = np[_grp[i]];
            Normals();
            return this;
        }

        public Shell Offset(float d) => Offset(_ => d);

        /// <summary>Move cada ponto por uma função livre (posição, normal) → nova posição.</summary>
        public Shell Displace(Func<Vector3, Vector3, Vector3> fn)
        {
            Topology();
            var np = new Vector3[_groups];
            for (int g = 0; g < _groups; g++) np[g] = fn(p[_rep[g]], _gn[g]);
            for (int i = 0; i < p.Count; i++) p[i] = np[_grp[i]];
            Normals();
            return this;
        }

        /// <summary>
        /// Contornos (bordas) como listas ordenadas de vértices representativos. Cada aresta de borda entra
        /// em exatamente um contorno fechado, mesmo quando dois trechos da borda se tocam num vértice
        /// (nesse caso o contorno é dividido ali, em vez de se perder).
        /// </summary>
        public List<int[]> Loops()
        {
            Topology();
            var outEdges = new Dictionary<int, List<int>>();
            for (int i = 0; i < t.Count; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int ga = _grp[t[i + e]], gb = _grp[t[i + (e + 1) % 3]];
                    if (_gEdgeCount[EKey(ga, gb)] != 1) continue;
                    if (!outEdges.TryGetValue(ga, out var l)) outEdges[ga] = l = new List<int>(1);
                    l.Add(gb);
                }
            var loops = new List<int[]>();
            var starts = new List<int>(outEdges.Keys);
            starts.Sort();
            foreach (int start in starts)
            {
                while (outEdges[start].Count > 0)
                {
                    var loop = new List<int>();
                    int cur = start;
                    while (true)
                    {
                        loop.Add(_rep[cur]);
                        if (!outEdges.TryGetValue(cur, out var lst) || lst.Count == 0) break;
                        int nxt = lst[lst.Count - 1];
                        lst.RemoveAt(lst.Count - 1);
                        cur = nxt;
                        if (cur == start) break;
                    }
                    if (loop.Count >= 3) loops.Add(loop.ToArray());
                }
            }
            return loops;
        }

        /// <summary>Vértice da casca mais próximo de q (posição e normal), para prender enfeites.</summary>
        public int Closest(Vector3 q, Func<Vector3, Vector3, float> score = null)
        {
            Topology();
            int best = -1;
            float bs = float.MaxValue;
            for (int i = 0; i < p.Count; i++)
            {
                float s = score != null ? score(p[i], n[i]) : (p[i] - q).sqrMagnitude;
                if (s < bs) { bs = s; best = i; }
            }
            return best;
        }

        public Bounds GetBounds()
        {
            var b = new Bounds(p.Count > 0 ? p[0] : Vector3.zero, Vector3.zero);
            foreach (var q in p) b.Encapsulate(q);
            return b;
        }

        /// <summary>
        /// Subdivide cada triângulo em quatro (ponto médio das arestas, pesos interpolados). Deixa a
        /// silhueta lisa e os recortes limpos; normalmente seguido de uma suavização leve.
        /// </summary>
        public Shell Subdivide()
        {
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int id)) return id;
                id = p.Count;
                p.Add((p[a] + p[b]) * 0.5f);
                n.Add((n[a] + n[b]).normalized);
                w.Add(LerpW(w[a], w[b], 0.5f));
                mid[key] = id;
                return id;
            }
            var nt = new List<int>(t.Count * 4);
            for (int i = 0; i < t.Count; i += 3)
            {
                int a = t[i], b = t[i + 1], c = t[i + 2];
                int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                nt.Add(a); nt.Add(ab); nt.Add(ca);
                nt.Add(ab); nt.Add(b); nt.Add(bc);
                nt.Add(ca); nt.Add(bc); nt.Add(c);
                nt.Add(ab); nt.Add(bc); nt.Add(ca);
            }
            t = nt;
            _dirty = true;
            return this;
        }

        /// <summary>
        /// Interseção de um raio com a casca (o ponto mais próximo da origem, ou o mais distante).
        /// Devolve posição, normal interpolada e os pesos do vértice dominante do triângulo.
        /// </summary>
        public bool Raycast(Vector3 origin, Vector3 dir, out Vector3 pos, out Vector3 nrm, out BoneWeight bw, bool farthest = false)
        {
            Topology();
            pos = nrm = Vector3.zero;
            bw = default;
            float best = farthest ? -1f : float.MaxValue;
            bool hit = false;
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 a = p[t[i]], e1 = p[t[i + 1]] - a, e2 = p[t[i + 2]] - a;
                Vector3 pv = Vector3.Cross(dir, e2);
                float det = Vector3.Dot(e1, pv);
                if (Mathf.Abs(det) < 1e-12f) continue;
                float inv = 1f / det;
                Vector3 tv = origin - a;
                float u = Vector3.Dot(tv, pv) * inv;
                if (u < -1e-4f || u > 1.0001f) continue;
                Vector3 qv = Vector3.Cross(tv, e1);
                float v = Vector3.Dot(dir, qv) * inv;
                if (v < -1e-4f || u + v > 1.0001f) continue;
                float d = Vector3.Dot(e2, qv) * inv;
                if (d <= 1e-5f) continue;
                if (farthest ? d <= best : d >= best) continue;
                best = d;
                hit = true;
                pos = origin + dir * d;
                float wa = 1f - u - v;
                nrm = (n[t[i]] * wa + n[t[i + 1]] * u + n[t[i + 2]] * v).normalized;
                bw = wa >= u && wa >= v ? w[t[i]] : u >= v ? w[t[i + 1]] : w[t[i + 2]];
            }
            return hit;
        }
    }

    /// <summary>Projeção de UV em metros: cilíndrica (com período, para dar a volta) ou plana.</summary>
    public struct UvProj
    {
        public Vector3 origin, axis, refDir;
        public float radius, period;
        public bool planar, spherical;

        public static UvProj Cylinder(Vector3 origin, Vector3 axis, Vector3 refDir, float radius)
        {
            axis.Normalize();
            refDir = (refDir - Vector3.Dot(refDir, axis) * axis).normalized;
            return new UvProj { origin = origin, axis = axis, refDir = refDir, radius = radius, period = 2f * Mathf.PI * radius };
        }

        /// <summary>Esférica (u = longitude, v = latitude, em metros no raio dado).</summary>
        public static UvProj Sphere(Vector3 center, Vector3 axis, Vector3 refDir, float radius)
        {
            var u = Cylinder(center, axis, refDir, radius);
            u.spherical = true;
            return u;
        }

        /// <summary>Plano com eixos u = refDir e v = axis.</summary>
        public static UvProj Plane(Vector3 origin, Vector3 uDir, Vector3 vDir) =>
            new UvProj { origin = origin, axis = vDir.normalized, refDir = uDir.normalized, planar = true };

        public Vector2 Eval(Vector3 q)
        {
            Vector3 d = q - origin;
            float v = Vector3.Dot(d, axis);
            if (planar) return new Vector2(Vector3.Dot(d, refDir), v);
            Vector3 r = d - v * axis;
            Vector3 side = Vector3.Cross(axis, refDir);
            float ang = Mathf.Atan2(Vector3.Dot(r, side), Vector3.Dot(r, refDir));
            if (spherical) v = Mathf.Asin(Mathf.Clamp(v / Mathf.Max(d.magnitude, 1e-6f), -1f, 1f)) * radius;
            return new Vector2(ang * radius, v);
        }
    }

    /// <summary>Junta cascas, bordas enroladas e peças do MeshBuilder numa malha com skinning.</summary>
    public class SkinBuilder
    {
        readonly List<Vector3> _v = new List<Vector3>();
        readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<BoneWeight> _w = new List<BoneWeight>();
        readonly List<int>[] _t = new List<int>[(int)M.Count];

        public bool Empty => _v.Count == 0;

        List<int> T(M m)
        {
            int i = (int)m;
            if (_t[i] == null) _t[i] = new List<int>();
            return _t[i];
        }

        int Add(Vector3 p, Vector3 n, Vector2 uv, BoneWeight w)
        {
            _v.Add(p);
            _n.Add(n);
            _uv.Add(uv);
            _w.Add(w);
            return _v.Count - 1;
        }

        /// <summary>Superfície da casca. inset desloca para dentro (face interna com flip).</summary>
        public void AddSurface(Shell s, M m, UvProj proj, float inset = 0f, bool flip = false)
        {
            float k = 1f / MeshBuilder.UvScaleOf(m);
            int first = _v.Count;
            var uvs = new Vector2[s.p.Count];
            for (int i = 0; i < s.p.Count; i++)
            {
                uvs[i] = proj.Eval(s.p[i]) * k;
                Add(s.p[i] - s.n[i] * inset, flip ? -s.n[i] : s.n[i], uvs[i], s.w[i]);
            }
            var tr = T(m);
            var dup = new Dictionary<int, int>();
            float per = proj.period * k;
            for (int i = 0; i < s.t.Count; i += 3)
            {
                int a = s.t[i], b = s.t[i + 1], c = s.t[i + 2];
                int ia = first + a, ib = first + b, ic = first + c;
                if (!proj.planar && per > 0f)
                {
                    float umax = Mathf.Max(uvs[a].x, Mathf.Max(uvs[b].x, uvs[c].x));
                    float umin = Mathf.Min(uvs[a].x, Mathf.Min(uvs[b].x, uvs[c].x));
                    if (umax - umin > per * 0.5f)
                    {
                        int Fix(int v, int idx)
                        {
                            if (uvs[v].x > umax - per * 0.5f) return idx;
                            if (dup.TryGetValue(v, out int d)) return d;
                            d = Add(_v[idx], _n[idx], uvs[v] + new Vector2(per, 0f), _w[idx]);
                            dup[v] = d;
                            return d;
                        }
                        ia = Fix(a, ia);
                        ib = Fix(b, ib);
                        ic = Fix(c, ic);
                    }
                }
                if (flip) { tr.Add(ia); tr.Add(ic); tr.Add(ib); }
                else { tr.Add(ia); tr.Add(ib); tr.Add(ic); }
            }
        }

        /// <summary>Chapa com espessura: face externa + interna (a borda fica por conta de AddRims).</summary>
        public void AddSolid(Shell s, M outer, M inner, UvProj proj, float thick)
        {
            AddSurface(s, outer, proj);
            AddSurface(s, inner, proj, thick, true);
        }

        /// <summary>Borda enrolada (tubo) em todos os contornos da casca.</summary>
        public void AddRims(Shell s, float thick, float radius, M m, int seg = 8, float minLength = 0.04f, float outward = 0f)
        {
            foreach (var loop in s.Loops())
            {
                int n = loop.Length;
                float len = 0f;
                for (int i = 0; i < n; i++) len += Vector3.Distance(s.p[loop[i]], s.p[loop[(i + 1) % n]]);
                if (len < minLength) continue;
                // pontos suavizados ao longo do contorno
                var pts = new Vector3[n];
                var nrm = new Vector3[n];
                var ws = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                {
                    pts[i] = s.p[loop[i]];
                    nrm[i] = s.n[loop[i]];
                    ws[i] = s.w[loop[i]];
                }
                SmoothLoop(ref pts, ref nrm);
                ResampleLoop(pts, nrm, ws, Mathf.Max(radius * 1.25f, 0.0035f), out var rp, out var rn, out var rw);
                AddTube(rp, rn, i => rw[i], thick, radius, m, seg, true, outward);
            }
        }

        /// <summary>
        /// Suaviza um contorno fechado: pouco as posições (mantém o desenho) e bastante as normais. Na borda
        /// de uma casca as normais alternam de inclinação de um vértice para o outro; como o tubo da borda é
        /// posicionado ao longo delas, sem isso ele sai "em contas".
        /// </summary>
        public static void SmoothLoop(ref Vector3[] pts, ref Vector3[] nrm, int posIterations = 3, int normalIterations = 10)
        {
            int n = pts.Length;
            for (int it = 0; it < posIterations; it++)
            {
                var q = new Vector3[n];
                for (int i = 0; i < n; i++) q[i] = (pts[(i + n - 1) % n] + pts[i] * 2f + pts[(i + 1) % n]) * 0.25f;
                pts = q;
            }
            for (int it = 0; it < normalIterations; it++)
            {
                var q = new Vector3[n];
                for (int i = 0; i < n; i++) q[i] = (nrm[(i + n - 1) % n] + nrm[i] * 2f + nrm[(i + 1) % n]) * 0.25f;
                nrm = q;
            }
            for (int i = 0; i < n; i++) nrm[i] = nrm[i].normalized;
        }

        /// <summary>
        /// Reamostra uma curva fechada com espaçamento uniforme por comprimento de arco. Sem isso, pontos
        /// mais próximos que o raio do tubo fazem os anéis se cruzarem e a borda fica serrilhada.
        /// </summary>
        public static void ResampleLoop(Vector3[] pts, Vector3[] nrm, BoneWeight[] w, float step,
            out Vector3[] op, out Vector3[] on, out BoneWeight[] ow)
        {
            int n = pts.Length;
            var cum = new float[n + 1];
            for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + Vector3.Distance(pts[i], pts[(i + 1) % n]);
            float L = cum[n];
            int m = Mathf.Max(6, Mathf.RoundToInt(L / Mathf.Max(step, 1e-4f)));
            op = new Vector3[m];
            on = new Vector3[m];
            ow = new BoneWeight[m];
            int seg = 0;
            for (int k = 0; k < m; k++)
            {
                float sArc = k * L / m;
                while (seg < n - 1 && cum[seg + 1] < sArc) seg++;
                float f = (sArc - cum[seg]) / Mathf.Max(1e-7f, cum[seg + 1] - cum[seg]);
                int j = (seg + 1) % n;
                op[k] = Vector3.Lerp(pts[seg], pts[j], f);
                on[k] = Vector3.Lerp(nrm[seg], nrm[j], f).normalized;
                ow[k] = f < 0.5f ? w[seg] : w[j];
            }
        }

        /// <summary>Tubo ao longo de uma curva; o centro fica 'thick/2' para dentro da normal da casca.</summary>
        public void AddTube(Vector3[] pts, Vector3[] nrm, Func<int, BoneWeight> weight, float thick, float radius, M m, int seg, bool closed, float outward = 0f)
        {
            int n = pts.Length;
            float k = 1f / MeshBuilder.UvScaleOf(m);
            int first = _v.Count;
            float along = 0f;
            int rings = closed ? n + 1 : n;
            for (int r = 0; r < rings; r++)
            {
                int i = r % n;
                if (r > 0) along += Vector3.Distance(pts[i], pts[(r - 1) % n]);
                Vector3 tg = closed ? pts[(i + 1) % n] - pts[(i + n - 1) % n] : pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                tg.Normalize();
                Vector3 e1 = (nrm[i] - Vector3.Dot(nrm[i], tg) * tg).normalized;
                Vector3 e2 = Vector3.Cross(tg, e1);
                Vector3 c = pts[i] - nrm[i] * (thick * 0.5f) + e2 * outward;
                for (int j = 0; j <= seg; j++)
                {
                    float a = j * Mathf.PI * 2f / seg;
                    Vector3 dir = e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a);
                    Add(c + dir * radius, dir, new Vector2(along * k, j * 2f * Mathf.PI * radius * k / seg), weight(i));
                }
            }
            var tr = T(m);
            int rv = seg + 1;
            for (int r = 0; r < rings - 1; r++)
                for (int j = 0; j < seg; j++)
                {
                    int A = first + r * rv + j, B = A + rv, C = B + 1, D = A + 1;
                    // orientação: a normal do primeiro vértice aponta para fora do tubo
                    Vector3 fn = Vector3.Cross(_v[B] - _v[A], _v[C] - _v[A]);
                    if (Vector3.Dot(fn, _n[A] + _n[C]) >= 0f) { tr.Add(A); tr.Add(B); tr.Add(C); tr.Add(A); tr.Add(C); tr.Add(D); }
                    else { tr.Add(A); tr.Add(C); tr.Add(B); tr.Add(A); tr.Add(D); tr.Add(C); }
                }
        }

        /// <summary>Copia uma peça do MeshBuilder; os pesos vêm de uma função da posição.</summary>
        public void AddBuilder(MeshBuilder b, Func<Vector3, BoneWeight> weight)
        {
            int first = _v.Count;
            var vs = b.Vertices;
            var ns = b.Normals;
            var us = b.Uvs;
            var cache = new Dictionary<(int, int, int), BoneWeight>();
            for (int i = 0; i < vs.Count; i++)
            {
                var key = (Mathf.RoundToInt(vs[i].x * 1e4f), Mathf.RoundToInt(vs[i].y * 1e4f), Mathf.RoundToInt(vs[i].z * 1e4f));
                if (!cache.TryGetValue(key, out var bw)) cache[key] = bw = weight(vs[i]);
                Add(vs[i], ns[i], us[i], bw);
            }
            for (int m = 0; m < (int)M.Count; m++)
            {
                var src = b.TrianglesOf((M)m);
                if (src == null || src.Count == 0) continue;
                var tr = T((M)m);
                foreach (int i in src) tr.Add(first + i);
            }
        }

        public Mesh ToMesh(string name, Matrix4x4[] bind, Material[] palette, out Material[] mats)
        {
            // um único vértice inválido (NaN/infinito) faz o Unity salvar a malha inteira vazia, sem avisar
            int bad = 0;
            for (int i = 0; i < _v.Count; i++)
            {
                Vector3 q = _v[i];
                if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z))
                {
                    bad++;
                    _v[i] = Vector3.zero;
                }
            }
            if (bad > 0) Debug.LogError($"[Troia] malha {name}: {bad} vértices inválidos (NaN) foram zerados; há um erro numérico na peça");
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
            mesh.boneWeights = _w.ToArray();
            mesh.bindposes = bind;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            mats = new Material[used.Count];
            for (int k = 0; k < used.Count; k++) mats[k] = palette[used[k]];
            return mesh;
        }
    }
}
