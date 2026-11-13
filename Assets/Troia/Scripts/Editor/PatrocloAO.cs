using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>
    /// Máscaras assadas na cor dos vértices, lidas pelos shaders Troia/Personagem e Troia/Pele:
    ///  R = oclusão de ambiente (sombras de contato finas entre as camadas da roupa e da armadura, que o SSAO
    ///      de raio largo do projeto não resolve); os raios são lançados com o PhysX contra colisores
    ///      temporários das malhas na pose de repouso;
    ///  G = 1 - sujeira (lama e poeira perto do chão, poeira nas frestas, manchas);
    ///  B = 1 - região onde pode haver sangue seco.
    /// G e B são invertidos para que malhas sem cor assada (brancas) fiquem limpas.
    /// </summary>
    public static class PatrocloAO
    {
        public struct Item
        {
            public Mesh mesh;
            public Matrix4x4 matrix;
            /// <summary>Quanto a oclusão vira sujeira (poeira acumulada nas frestas). Pele usa menos que armadura.</summary>
            public float crevice;
            /// <summary>Sujeira extra em função da posição de repouso (ex.: barra da saia), ou null.</summary>
            public System.Func<Vector3, float> dirtBoost;
            /// <summary>Região de sangue extra em função da posição de repouso (ex.: ponta da lâmina), ou null.</summary>
            public System.Func<Vector3, float> stainBoost;
            public Item(Mesh m, Matrix4x4 mx, float crevice = 0.7f, System.Func<Vector3, float> dirtBoost = null, System.Func<Vector3, float> stainBoost = null)
            {
                mesh = m; matrix = mx; this.crevice = crevice; this.dirtBoost = dirtBoost; this.stainBoost = stainBoost;
            }
        }

        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Ruído 3D suave (0..1) a partir de três fatias de Perlin.</summary>
        static float Noise3(Vector3 p)
        {
            float n = (Mathf.PerlinNoise(p.x + 17.3f, p.y + 5.1f) + Mathf.PerlinNoise(p.y + 31.7f, p.z + 9.2f) + Mathf.PerlinNoise(p.z + 23.9f, p.x + 47.6f)) / 3f;
            return Mathf.Clamp01((n - 0.5f) * 2.4f + 0.5f);
        }

        /// <summary>
        /// Sujeira (0..1) e região de sangue (0..1) de um ponto na pose de repouso: lama e poeira subindo do
        /// chão, poeira nas frestas (pouca oclusão), pó pousado em cima e manchas largas ao acaso.
        /// </summary>
        static void Weather(Vector3 p, Vector3 n, float ao, float crevice, out float dirt, out float stain)
        {
            float ground = 0.8f * Mathf.Pow(Smooth(0.95f, 0.15f, p.y), 1.3f) + 0.45f * Smooth(0.3f, 0f, p.y);
            float crev = (1f - ao) * crevice;
            float top = Mathf.Max(n.y, 0f) * 0.16f;
            float blot = Noise3(p * 6f);
            float smudge = Smooth(0.6f, 0.88f, Noise3(p * 3.3f + new Vector3(5f, 3f, 8f))) * 0.5f;
            dirt = Mathf.Clamp01((ground + crev + top) * (0.5f + blot) + smudge);
            stain = Smooth(0.6f, 0.82f, Noise3(p * 2.6f + new Vector3(11f, 7f, 2f)));
        }

        const int TempLayer = 31;

        /// <summary>Assa a oclusão das malhas 'receivers' considerando as 'occluders' (que incluem as próprias, se for o caso).</summary>
        public static void Bake(IList<Item> receivers, IList<Item> occluders, int samples = 48, float maxDist = 0.26f, float floor = 0.12f)
        {
            var temp = new List<GameObject>();
            bool prevBack = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                foreach (var o in occluders)
                {
                    if (o.mesh == null || o.mesh.vertexCount < 3) continue;
                    var go = new GameObject("ao_tmp") { layer = TempLayer, hideFlags = HideFlags.HideAndDontSave };
                    go.transform.SetPositionAndRotation(o.matrix.GetPosition(), o.matrix.rotation);
                    go.transform.localScale = o.matrix.lossyScale;
                    go.AddComponent<MeshCollider>().sharedMesh = o.mesh;
                    temp.Add(go);
                }
                Physics.SyncTransforms();

                // amostras do hemisfério (peso por cosseno), em espaço tangente
                var dirs = new Vector3[samples];
                for (int i = 0; i < samples; i++)
                {
                    float u = (i + 0.5f) / samples, v = Halton(i + 1, 3);
                    float r = Mathf.Sqrt(u), a = v * Mathf.PI * 2f;
                    dirs[i] = new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), Mathf.Sqrt(Mathf.Max(0f, 1f - u)));
                }

                long rays = 0, hits = 0;
                foreach (var rc in receivers)
                {
                    var mesh = rc.mesh;
                    if (mesh == null) continue;
                    var vs = mesh.vertices;
                    var ns = mesh.normals;
                    int n = vs.Length;
                    var ao = new float[n];
                    const int chunk = 4096;
                    for (int start = 0; start < n; start += chunk)
                    {
                        int count = Mathf.Min(chunk, n - start);
                        var cmds = new NativeArray<RaycastCommand>(count * samples, Allocator.TempJob);
                        var res = new NativeArray<RaycastHit>(count * samples, Allocator.TempJob);
                        var qp = new QueryParameters(1 << TempLayer, false, QueryTriggerInteraction.Ignore, true);
                        for (int k = 0; k < count; k++)
                        {
                            Vector3 p = rc.matrix.MultiplyPoint3x4(vs[start + k]);
                            Vector3 nn = rc.matrix.MultiplyVector(ns[start + k]).normalized;
                            Vector3 t1 = Vector3.Cross(nn, Mathf.Abs(nn.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                            Vector3 t2 = Vector3.Cross(nn, t1);
                            // rotação pseudoaleatória por vértice (evita padrões)
                            float rot = Hash(p) * Mathf.PI * 2f;
                            float cr = Mathf.Cos(rot), sr = Mathf.Sin(rot);
                            Vector3 origin = p + nn * 0.0025f;
                            for (int s = 0; s < samples; s++)
                            {
                                Vector3 d = dirs[s];
                                Vector3 dir = t1 * (d.x * cr - d.y * sr) + t2 * (d.x * sr + d.y * cr) + nn * d.z;
                                cmds[k * samples + s] = new RaycastCommand(origin, dir, qp, maxDist);
                            }
                        }
                        RaycastCommand.ScheduleBatch(cmds, res, 256, 1).Complete();
                        for (int k = 0; k < count; k++)
                        {
                            float occ = 0f;
                            for (int s = 0; s < samples; s++)
                            {
                                var h = res[k * samples + s];
                                if (h.distance <= 1e-6f) continue; // sem acerto
                                occ += 1f - h.distance / maxDist;
                                hits++;
                            }
                            ao[start + k] = 1f - occ / samples;
                            rays += samples;
                        }
                        cmds.Dispose();
                        res.Dispose();
                    }
                    SmoothValues(mesh, vs, ns, ao, 2);
                    var cols = new Color32[n];
                    for (int i = 0; i < n; i++)
                    {
                        float a = Mathf.Clamp01(ao[i]);
                        Vector3 p = rc.matrix.MultiplyPoint3x4(vs[i]);
                        Vector3 nn = rc.matrix.MultiplyVector(ns[i]).normalized;
                        Weather(p, nn, a, rc.crevice, out float dirt, out float stain);
                        if (rc.dirtBoost != null) dirt = Mathf.Clamp01(dirt + rc.dirtBoost(p));
                        if (rc.stainBoost != null) stain = Mathf.Max(stain, Mathf.Clamp01(rc.stainBoost(p)));
                        byte r = (byte)Mathf.RoundToInt(255f * Mathf.Lerp(floor, 1f, Mathf.Pow(a, 1.25f)));
                        cols[i] = new Color32(r, (byte)Mathf.RoundToInt(255f * (1f - dirt)), (byte)Mathf.RoundToInt(255f * (1f - stain)), 255);
                    }
                    mesh.colors32 = cols;
                    UnityEditor.EditorUtility.SetDirty(mesh);
                }
                Debug.Log($"[Troia] oclusão assada: {receivers.Count} malhas, {rays / 1000}k raios, {(rays > 0 ? 100 * hits / rays : 0)}% ocluídos");
            }
            finally
            {
                foreach (var go in temp) Object.DestroyImmediate(go);
                Physics.queriesHitBackfaces = prevBack;
            }
        }

        /// <summary>
        /// Suaviza o valor entre vértices vizinhos. Solda os duplicados por posição e direção da normal
        /// (frente e verso de um pano ficam separados), pesa os vizinhos pela semelhança das normais e
        /// ignora vértices enterrados (oclusão ~0): assim o lado escondido de um tubo meio embutido não
        /// escurece o lado visível.
        /// </summary>
        static void SmoothValues(Mesh mesh, Vector3[] vs, Vector3[] ns, float[] ao, int iterations)
        {
            int n = vs.Length;
            var key = new Dictionary<(int, int, int, int), int>();
            var grp = new int[n];
            int groups = 0;
            for (int i = 0; i < n; i++)
            {
                int nk = (Mathf.RoundToInt(ns[i].x * 2f) + 2) * 25 + (Mathf.RoundToInt(ns[i].y * 2f) + 2) * 5 + Mathf.RoundToInt(ns[i].z * 2f) + 2;
                var k = (Mathf.RoundToInt(vs[i].x * 2e4f), Mathf.RoundToInt(vs[i].y * 2e4f), Mathf.RoundToInt(vs[i].z * 2e4f), nk);
                if (!key.TryGetValue(k, out int g)) { g = groups++; key[k] = g; }
                grp[i] = g;
            }
            var val = new float[groups];
            var cnt = new int[groups];
            var gn = new Vector3[groups];
            for (int i = 0; i < n; i++) { val[grp[i]] += ao[i]; cnt[grp[i]]++; gn[grp[i]] += ns[i]; }
            for (int g = 0; g < groups; g++) { val[g] /= Mathf.Max(1, cnt[g]); gn[g] = gn[g].normalized; }
            var raw = (float[])val.Clone();
            var nbr = new List<int>[groups];
            for (int sm = 0; sm < mesh.subMeshCount; sm++)
            {
                var t = mesh.GetTriangles(sm);
                for (int i = 0; i < t.Length; i += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        int a = grp[t[i + e]], b = grp[t[i + (e + 1) % 3]];
                        if (a == b) continue;
                        (nbr[a] ??= new List<int>(6)).Add(b);
                        (nbr[b] ??= new List<int>(6)).Add(a);
                    }
            }
            var tmp = new float[groups];
            for (int it = 0; it < iterations; it++)
            {
                for (int g = 0; g < groups; g++)
                {
                    tmp[g] = val[g];
                    if (nbr[g] == null) continue;
                    float s = 0f, sw = 0f;
                    foreach (int x in nbr[g])
                    {
                        if (raw[x] < 0.08f) continue;
                        float d = Mathf.Max(0f, Vector3.Dot(gn[g], gn[x]));
                        float w = d * d;
                        s += w * val[x];
                        sw += w;
                    }
                    if (sw > 1e-4f) tmp[g] = 0.5f * val[g] + 0.5f * s / sw;
                }
                var swp = val; val = tmp; tmp = swp;
            }
            for (int i = 0; i < n; i++) ao[i] = val[grp[i]];
        }

        static float Halton(int i, int b)
        {
            float f = 1f, r = 0f;
            while (i > 0) { f /= b; r += f * (i % b); i /= b; }
            return r;
        }

        static float Hash(Vector3 p)
        {
            float h = Mathf.Sin(p.x * 127.1f + p.y * 311.7f + p.z * 74.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }
    }
}
