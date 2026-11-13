using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Troia.EditorTools
{
    [Serializable] public class TrocaSub { public string material; public long idx = -1; public int nt; }
    [Serializable] public class TrocaInst { public string caminho, osso; public float[] matriz; }

    [Serializable]
    public class TrocaMalha
    {
        public string nome;
        public bool rigida;
        public int nv;
        // posição de cada bloco no arquivo .bin (-1 = não tem)
        public long pos = -1, nor = -1, tan = -1, uv0 = -1, uv1 = -1, cor = -1, bi = -1, bw = -1;
        public TrocaSub[] subs;
        public TrocaInst[] instancias;
    }

    [Serializable] public class TrocaOsso { public string nome; public int pai; public float[] cabeca, cauda; }
    [Serializable] public class TrocaPacote { public int versao = 1; public TrocaOsso[] ossos; public TrocaMalha[] malhas; }

    /// <summary>
    /// Ponte entre o Unity e o Blender. O formato é um manifesto .json mais um .bin com os blocos de números
    /// (float32 / int32 / bytes), tudo no sistema de eixos do Unity; quem converte os eixos é o lado do Blender.
    ///   Unity -> Blender: ExportBase grava as peças procedurais em Arte/Patroclo/troca/base (não versionado).
    ///   Blender -> Unity: Ferramentas/blender grava Fonte/patroclo_blender.json/.bin; aqui cada peça com o
    ///   mesmo nome de malha substitui a procedural (TryBuild) e as peças novas entram por nome.
    /// </summary>
    public static class PatrocloTroca
    {
        public const string BaseDir = "Arte/Patroclo/troca";
        public const string BlenderPath = PatrocloBuilder.Root + "/Fonte/patroclo_blender";

        /// <summary>Ligado = o construtor ignora o pacote do Blender e gera só as peças procedurais.</summary>
        public static bool Desligado;

        static TrocaPacote _pk;
        static byte[] _bin;
        static Dictionary<string, TrocaMalha> _byName;
        static readonly HashSet<string> _used = new HashSet<string>();

        // ------------------------------------------------------------------ Unity -> Blender
        class Writer : IDisposable
        {
            readonly FileStream _fs;
            public Writer(string path) { _fs = new FileStream(path, FileMode.Create); }
            public void Dispose() => _fs.Dispose();

            long Put(Array a, int bytes)
            {
                long at = _fs.Position;
                var buf = new byte[bytes];
                Buffer.BlockCopy(a, 0, buf, 0, bytes);
                _fs.Write(buf, 0, bytes);
                return at;
            }

            public long Floats(float[] a) => Put(a, a.Length * 4);
            public long Ints(int[] a) => Put(a, a.Length * 4);
            public long Bytes(byte[] a) => Put(a, a.Length);

            public long V3(Vector3[] v)
            {
                var a = new float[v.Length * 3];
                for (int i = 0; i < v.Length; i++) { a[i * 3] = v[i].x; a[i * 3 + 1] = v[i].y; a[i * 3 + 2] = v[i].z; }
                return Floats(a);
            }

            public long V2(Vector2[] v)
            {
                var a = new float[v.Length * 2];
                for (int i = 0; i < v.Length; i++) { a[i * 2] = v[i].x; a[i * 2 + 1] = v[i].y; }
                return Floats(a);
            }
        }

        static float[] Flat(Matrix4x4 m)
        {
            var a = new float[16];
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++) a[r * 4 + c] = m[r, c];
            return a;
        }

        static string PathOf(Transform t, Transform root)
        {
            string s = t.name;
            for (var p = t.parent; p != null && p != root; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        /// <summary>Grava todas as malhas do personagem (pose de repouso) para o Blender montar o arquivo de arte.</summary>
        public static void ExportBase(MhCharacter ch, GameObject root)
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), BaseDir);
            Directory.CreateDirectory(dir);
            var boneIndex = new Dictionary<Transform, int>();
            for (int i = 0; i < ch.bones.Length; i++) boneIndex[ch.bones[i]] = i;
            var pk = new TrocaPacote { ossos = new TrocaOsso[ch.data.bones.Length] };
            for (int i = 0; i < pk.ossos.Length; i++)
            {
                var b = ch.data.bones[i];
                pk.ossos[i] = new TrocaOsso { nome = b.name, pai = b.parent, cabeca = b.head, cauda = b.tail };
            }
            var list = new List<TrocaMalha>();
            var seen = new Dictionary<Mesh, TrocaMalha>();
            var insts = new Dictionary<TrocaMalha, List<TrocaInst>>();
            using (var w = new Writer(Path.Combine(dir, "base.bin")))
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var smr = r as SkinnedMeshRenderer;
                    var mf = r.GetComponent<MeshFilter>();
                    Mesh mesh = smr != null ? smr.sharedMesh : mf != null ? mf.sharedMesh : null;
                    if (mesh == null || mesh.vertexCount == 0) continue;
                    // marcas das bênçãos e a chama da crista são prévias do jogo: ficam só no Unity
                    string path = PathOf(r.transform, root.transform);
                    if (path.Contains("Marca_") || path.Contains("Chama")) continue;
                    if (!seen.TryGetValue(mesh, out var tm))
                    {
                        tm = new TrocaMalha { nome = mesh.name, rigida = smr == null, nv = mesh.vertexCount };
                        tm.pos = w.V3(mesh.vertices);
                        tm.nor = w.V3(mesh.normals);
                        var uv = mesh.uv;
                        if (uv.Length == tm.nv) tm.uv0 = w.V2(uv);
                        var col = mesh.colors32;
                        if (col.Length == tm.nv)
                        {
                            var cb = new byte[tm.nv * 4];
                            for (int i = 0; i < tm.nv; i++) { cb[i * 4] = col[i].r; cb[i * 4 + 1] = col[i].g; cb[i * 4 + 2] = col[i].b; cb[i * 4 + 3] = col[i].a; }
                            tm.cor = w.Bytes(cb);
                        }
                        if (smr != null)
                        {
                            var bws = mesh.boneWeights;
                            var bi = new int[tm.nv * 4];
                            var bw = new float[tm.nv * 4];
                            // os índices da malha apontam para smr.bones; converte para a ordem dos ossos do pacote
                            var map = new int[smr.bones.Length];
                            for (int i = 0; i < map.Length; i++) map[i] = boneIndex.TryGetValue(smr.bones[i], out int bx) ? bx : 0;
                            for (int i = 0; i < tm.nv; i++)
                            {
                                var q = bws[i];
                                bi[i * 4] = map[q.boneIndex0]; bi[i * 4 + 1] = map[q.boneIndex1]; bi[i * 4 + 2] = map[q.boneIndex2]; bi[i * 4 + 3] = map[q.boneIndex3];
                                bw[i * 4] = q.weight0; bw[i * 4 + 1] = q.weight1; bw[i * 4 + 2] = q.weight2; bw[i * 4 + 3] = q.weight3;
                            }
                            tm.bi = w.Ints(bi);
                            tm.bw = w.Floats(bw);
                        }
                        var mats = r.sharedMaterials;
                        tm.subs = new TrocaSub[mesh.subMeshCount];
                        for (int s = 0; s < mesh.subMeshCount; s++)
                        {
                            var tri = mesh.GetTriangles(s);
                            tm.subs[s] = new TrocaSub { material = s < mats.Length && mats[s] != null ? mats[s].name : "", idx = w.Ints(tri), nt = tri.Length / 3 };
                        }
                        seen[mesh] = tm;
                        insts[tm] = new List<TrocaInst>();
                        list.Add(tm);
                    }
                    string bone = "";
                    for (var p = r.transform; p != null && p != root.transform; p = p.parent)
                        if (boneIndex.ContainsKey(p)) { bone = p.name; break; }
                    insts[tm].Add(new TrocaInst { caminho = path, osso = bone, matriz = Flat(root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix) });
                }
            }
            foreach (var tm in list) tm.instancias = insts[tm].ToArray();
            pk.malhas = list.ToArray();
            File.WriteAllText(Path.Combine(dir, "base.json"), JsonUtility.ToJson(pk));
            Debug.Log($"[Troia] base para o Blender: {list.Count} malhas em {BaseDir}/base.json");
        }

        // ------------------------------------------------------------------ Blender -> Unity
        /// <summary>Carrega o pacote vindo do Blender, se existir.</summary>
        public static bool Load()
        {
            _pk = null;
            _bin = null;
            _byName = null;
            _used.Clear();
            if (Desligado) return false;
            string json = Path.Combine(Directory.GetCurrentDirectory(), BlenderPath + ".json");
            string bin = Path.Combine(Directory.GetCurrentDirectory(), BlenderPath + ".bin");
            if (!File.Exists(json) || !File.Exists(bin)) return false;
            _pk = JsonUtility.FromJson<TrocaPacote>(File.ReadAllText(json));
            _bin = File.ReadAllBytes(bin);
            _byName = new Dictionary<string, TrocaMalha>();
            foreach (var m in _pk.malhas) _byName[m.nome] = m;
            Debug.Log($"[Troia] pacote do Blender: {_pk.malhas.Length} malhas");
            return true;
        }

        public static bool Has(string meshName) => _byName != null && _byName.ContainsKey(meshName);

        /// <summary>Peças do pacote que nenhuma chamada usou (nome errado ou peça nova sem lugar no construtor).</summary>
        public static List<string> Unused()
        {
            var l = new List<string>();
            if (_byName != null)
                foreach (var k in _byName.Keys) if (!_used.Contains(k)) l.Add(k);
            return l;
        }

        static float[] Floats(long at, int n)
        {
            var a = new float[n];
            Buffer.BlockCopy(_bin, (int)at, a, 0, n * 4);
            return a;
        }

        static int[] Ints(long at, int n)
        {
            var a = new int[n];
            Buffer.BlockCopy(_bin, (int)at, a, 0, n * 4);
            return a;
        }

        /// <summary>
        /// Monta a malha de uma peça do pacote do Blender. boneByName = ossos do personagem (peças com pesos);
        /// para peça rígida passe null. Devolve false se o pacote não tem a peça.
        /// </summary>
        public static bool TryBuild(string meshName, MhCharacter ch, Func<string, Material> materialFor, out Mesh mesh, out Material[] mats)
        {
            mesh = null;
            mats = null;
            if (_byName == null || !_byName.TryGetValue(meshName, out var tm)) return false;
            _used.Add(meshName);
            int n = tm.nv;
            var fp = Floats(tm.pos, n * 3);
            var fn = Floats(tm.nor, n * 3);
            var v = new Vector3[n];
            var nr = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(fp[i * 3], fp[i * 3 + 1], fp[i * 3 + 2]);
                nr[i] = new Vector3(fn[i * 3], fn[i * 3 + 1], fn[i * 3 + 2]);
            }
            mesh = new Mesh { name = meshName };
            if (n > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = v;
            mesh.normals = nr;
            Vector2[] uv0 = null;
            if (tm.uv0 >= 0)
            {
                var f = Floats(tm.uv0, n * 2);
                uv0 = new Vector2[n];
                for (int i = 0; i < n; i++) uv0[i] = new Vector2(f[i * 2], f[i * 2 + 1]);
                mesh.uv = uv0;
            }
            if (tm.uv1 >= 0)
            {
                var f = Floats(tm.uv1, n * 2);
                var uv1 = new Vector2[n];
                for (int i = 0; i < n; i++) uv1[i] = new Vector2(f[i * 2], f[i * 2 + 1]);
                mesh.uv2 = uv1;
            }
            if (tm.cor >= 0)
            {
                var c = new Color32[n];
                int at = (int)tm.cor;
                for (int i = 0; i < n; i++) c[i] = new Color32(_bin[at + i * 4], _bin[at + i * 4 + 1], _bin[at + i * 4 + 2], _bin[at + i * 4 + 3]);
                mesh.colors32 = c;
            }
            mesh.subMeshCount = tm.subs.Length;
            mats = new Material[tm.subs.Length];
            for (int s = 0; s < tm.subs.Length; s++)
            {
                mesh.SetTriangles(Ints(tm.subs[s].idx, tm.subs[s].nt * 3), s, false);
                mats[s] = materialFor(tm.subs[s].material);
                if (mats[s] == null) Debug.LogWarning($"[Troia] {meshName}: material '{tm.subs[s].material}' do Blender não existe no Unity");
            }
            if (tm.tan >= 0)
            {
                var f = Floats(tm.tan, n * 4);
                var t = new Vector4[n];
                for (int i = 0; i < n; i++) t[i] = new Vector4(f[i * 4], f[i * 4 + 1], f[i * 4 + 2], f[i * 4 + 3]);
                mesh.tangents = t;
            }
            else mesh.RecalculateTangents();
            if (!tm.rigida && ch != null && tm.bi >= 0)
            {
                // os índices do pacote seguem a lista de ossos dele: casa pelo nome com os ossos do personagem
                var map = new int[_pk.ossos.Length];
                var byName = new Dictionary<string, int>();
                for (int i = 0; i < ch.bones.Length; i++) byName[ch.bones[i].name] = i;
                for (int i = 0; i < map.Length; i++) map[i] = byName.TryGetValue(_pk.ossos[i].nome, out int bx) ? bx : 0;
                var bi = Ints(tm.bi, n * 4);
                var bw = Floats(tm.bw, n * 4);
                var w = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                    w[i] = new BoneWeight
                    {
                        boneIndex0 = map[bi[i * 4]], weight0 = bw[i * 4],
                        boneIndex1 = map[bi[i * 4 + 1]], weight1 = bw[i * 4 + 1],
                        boneIndex2 = map[bi[i * 4 + 2]], weight2 = bw[i * 4 + 2],
                        boneIndex3 = map[bi[i * 4 + 3]], weight3 = bw[i * 4 + 3],
                    };
                mesh.boneWeights = w;
                mesh.bindposes = ch.bind;
            }
            mesh.RecalculateBounds();
            return true;
        }
    }
}
