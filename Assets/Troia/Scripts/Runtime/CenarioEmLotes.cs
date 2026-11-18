using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Troia
{
    /// <summary>
    /// Funde o cenário parado em lotes por região do mapa, ao carregar a fase. Cada casa tem ~9 materiais e era
    /// desenhada peça por peça em cada passada (profundidade, cor, cada cascata de sombra): até 20 mil draw calls.
    /// Um lote é uma malha por célula do mapa, com uma submalha por material, mais uma malha simples (posição e
    /// normal, vértices soldados) que só projeta sombra. Os objetos originais continuam na cena para colisão,
    /// NavMesh e edição; só o desenho deles é desligado. Nada é gravado em disco: editar o mapa não muda.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class CenarioEmLotes : MonoBehaviour
    {
        [Tooltip("Peças paradas do cenário (preenchido pelo gerador do mapa).")]
        public MeshRenderer[] fontes;
        [Tooltip("Lado da célula, em metros. Peças maiores que isso ficam como estão.")]
        public float celula = 40f;

        /// <summary>Uma malha de origem preparada uma vez: vértices compactados por submalha e a versão soldada para sombra.</summary>
        class Molde
        {
            public Vector3[][] v, n; public Vector4[][] t; public Vector2[][] uv; public int[][] tri;
            public Vector3[] sv, sn; public int[] stri;
            public Bounds caixa;
        }

        class Balde
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<Vector4> t = new List<Vector4>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> tri = new List<int>();
        }

        class Lote
        {
            public readonly Dictionary<Material, Balde> baldes = new Dictionary<Material, Balde>();
            public readonly List<Material> ordem = new List<Material>();
            public readonly List<Vector3> sv = new List<Vector3>(), sn = new List<Vector3>();
            public readonly List<int> stri = new List<int>();
            public readonly List<MeshRenderer> pecas = new List<MeshRenderer>();
        }

        void Awake()
        {
            if (fontes == null || fontes.Length == 0) return;
            var relogio = Stopwatch.StartNew();
            var moldes = new Dictionary<Mesh, Molde>();
            var lotes = new Dictionary<Vector2Int, Lote>();
            int pecas = 0;
            foreach (var mr in fontes)
            {
                if (!mr || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                var mf = mr.GetComponent<MeshFilter>();
                var malha = mf ? mf.sharedMesh : null;
                if (!malha || !malha.isReadable) continue;
                if (!moldes.TryGetValue(malha, out var molde)) moldes[malha] = molde = Preparar(malha);
                var matriz = mr.transform.localToWorldMatrix;
                var caixa = Mundo(molde.caixa, matriz);
                // muralhas, mar, exército: fundidos, o lote nunca sairia da tela
                if (Mathf.Max(caixa.size.x, caixa.size.z) > celula) continue;
                var chave = new Vector2Int(Mathf.FloorToInt(caixa.center.x / celula), Mathf.FloorToInt(caixa.center.z / celula));
                if (!lotes.TryGetValue(chave, out var lote)) lotes[chave] = lote = new Lote();
                var normal = matriz.inverse.transpose;
                var mats = mr.sharedMaterials;
                for (int s = 0; s < molde.tri.Length && s < mats.Length; s++)
                {
                    if (!mats[s]) continue;
                    if (!lote.baldes.TryGetValue(mats[s], out var b)) { lote.baldes[mats[s]] = b = new Balde(); lote.ordem.Add(mats[s]); }
                    int b0 = b.v.Count;
                    var sv = molde.v[s]; var sn = molde.n[s]; var st = molde.t[s];
                    for (int i = 0; i < sv.Length; i++)
                    {
                        b.v.Add(matriz.MultiplyPoint3x4(sv[i]));
                        b.n.Add(normal.MultiplyVector(sn[i]).normalized);
                        Vector3 t3 = matriz.MultiplyVector(st[i]).normalized;
                        b.t.Add(new Vector4(t3.x, t3.y, t3.z, st[i].w));
                    }
                    b.uv.AddRange(molde.uv[s]);
                    foreach (int i in molde.tri[s]) b.tri.Add(b0 + i);
                }
                if (mr.shadowCastingMode != ShadowCastingMode.Off)
                {
                    int s0 = lote.sv.Count;
                    for (int i = 0; i < molde.sv.Length; i++)
                    {
                        lote.sv.Add(matriz.MultiplyPoint3x4(molde.sv[i]));
                        lote.sn.Add(normal.MultiplyVector(molde.sn[i]).normalized);
                    }
                    foreach (int i in molde.stri) lote.stri.Add(s0 + i);
                }
                lote.pecas.Add(mr);
                pecas++;
            }

            int layer = gameObject.layer;
            foreach (var par in lotes)
            {
                var lote = par.Value;
                if (lote.ordem.Count == 0) continue;
                string nome = $"Lote_{par.Key.x}_{par.Key.y}";
                var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<Vector4>(); var uv = new List<Vector2>();
                var bases = new int[lote.ordem.Count];
                for (int i = 0; i < lote.ordem.Count; i++)
                {
                    var b = lote.baldes[lote.ordem[i]];
                    bases[i] = v.Count;
                    v.AddRange(b.v); n.AddRange(b.n); t.AddRange(b.t); uv.AddRange(b.uv);
                }
                var malha = new Mesh { name = nome, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                malha.SetVertices(v); malha.SetNormals(n); malha.SetTangents(t); malha.SetUVs(0, uv);
                malha.subMeshCount = lote.ordem.Count;
                for (int i = 0; i < lote.ordem.Count; i++) malha.SetTriangles(lote.baldes[lote.ordem[i]].tri, i, false, bases[i]);
                malha.RecalculateBounds();
                malha.UploadMeshData(true);
                var go = new GameObject(nome) { layer = layer };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = lote.ordem.ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
                go.AddComponent<OwnedMesh>().mesh = malha;

                if (lote.stri.Count > 0)
                {
                    var so = new Mesh { name = nome + "_Sombra", indexFormat = lote.sv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    so.SetVertices(lote.sv); so.SetNormals(lote.sn); so.SetTriangles(lote.stri, 0, false);
                    so.RecalculateBounds();
                    so.UploadMeshData(true);
                    var sg = new GameObject(nome + "_Sombra") { layer = layer };
                    sg.transform.SetParent(go.transform, false);
                    sg.AddComponent<MeshFilter>().sharedMesh = so;
                    var sr = sg.AddComponent<MeshRenderer>();
                    sr.sharedMaterial = lote.ordem[0];
                    sr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    sr.receiveShadows = false;
                    sg.AddComponent<OwnedMesh>().mesh = so;
                }
                foreach (var mr in lote.pecas) mr.enabled = false;
            }
            UnityEngine.Debug.Log($"[Troia] Cenário fundido: {pecas} peças em {lotes.Count} lotes, {relogio.ElapsedMilliseconds} ms.");
        }

        static Molde Preparar(Mesh m)
        {
            var v = m.vertices; var n = m.normals; var t = m.tangents; var uv = m.uv;
            bool temN = n.Length == v.Length, temT = t.Length == v.Length, temUv = uv.Length == v.Length;
            int subs = m.subMeshCount;
            var molde = new Molde
            {
                v = new Vector3[subs][], n = new Vector3[subs][], t = new Vector4[subs][], uv = new Vector2[subs][], tri = new int[subs][],
                caixa = m.bounds
            };
            var mapa = new int[v.Length];
            // sombra: vértices de mesma posição soldados (uma caixa cai de 24 para 8) e normal média, que só entra no viés
            var solda = new Dictionary<Vector3, int>();
            var sv = new List<Vector3>(); var sn = new List<Vector3>(); var stri = new List<int>();
            for (int s = 0; s < subs; s++)
            {
                // só os vértices que a submalha usa
                for (int i = 0; i < mapa.Length; i++) mapa[i] = -1;
                var tri = m.GetTriangles(s);
                var cv = new List<Vector3>(); var cn = new List<Vector3>(); var ct = new List<Vector4>(); var cuv = new List<Vector2>();
                var ctri = new int[tri.Length];
                for (int k = 0; k < tri.Length; k++)
                {
                    int i = tri[k];
                    if (mapa[i] < 0)
                    {
                        mapa[i] = cv.Count;
                        cv.Add(v[i]); cn.Add(temN ? n[i] : Vector3.up); ct.Add(temT ? t[i] : new Vector4(1, 0, 0, 1)); cuv.Add(temUv ? uv[i] : Vector2.zero);
                    }
                    ctri[k] = mapa[i];
                    var p = new Vector3(Mathf.Round(v[i].x * 1000f), Mathf.Round(v[i].y * 1000f), Mathf.Round(v[i].z * 1000f));
                    if (!solda.TryGetValue(p, out int j)) { solda[p] = j = sv.Count; sv.Add(v[i]); sn.Add(Vector3.zero); }
                    sn[j] += temN ? n[i] : Vector3.up;
                    stri.Add(j);
                }
                molde.v[s] = cv.ToArray(); molde.n[s] = cn.ToArray(); molde.t[s] = ct.ToArray(); molde.uv[s] = cuv.ToArray(); molde.tri[s] = ctri;
            }
            for (int i = 0; i < sn.Count; i++) sn[i] = sn[i].sqrMagnitude > 1e-8f ? sn[i].normalized : Vector3.up;
            molde.sv = sv.ToArray(); molde.sn = sn.ToArray(); molde.stri = stri.ToArray();
            return molde;
        }

        static Bounds Mundo(Bounds b, Matrix4x4 m)
        {
            var r = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                r.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2 & 1) * 2 - 1))));
            return r;
        }
    }
}
