using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Troia.EditorTools
{
    [Serializable] public class MhBone { public string name; public int parent; public float[] head; public float[] tail; }

    [Serializable]
    public class MhMesh
    {
        public string name, material;
        public float[] vertices, normals, uvs;
        public int[] triangles;
        public int[] boneIndex;
        public float[] boneWeight;
    }

    [Serializable] public class MhData { public float height; public MhBone[] bones; public MhMesh[] meshes; }

    /// <summary>Personagem montado a partir do JSON do MakeHuman (ossos + malhas com skinning).</summary>
    public class MhCharacter
    {
        public Transform root;
        public Transform[] bones;
        public readonly Dictionary<string, Transform> byName = new Dictionary<string, Transform>();
        public readonly Dictionary<string, SkinnedMeshRenderer> renderers = new Dictionary<string, SkinnedMeshRenderer>();
        public MhData data;
        /// <summary>Vértices do corpo (pose de repouso, mundo) e osso dominante de cada um.</summary>
        public Vector3[] bodyVerts;
        public string[] bodyDominant;
        public Matrix4x4[] bind;
        public Transform rootBone;

        public Vector3 Head(string bone) => byName[bone].position;
        public Vector3 Tail(string bone)
        {
            foreach (var b in data.bones) if (b.name == bone) return new Vector3(b.tail[0], b.tail[1], b.tail[2]);
            return Head(bone);
        }
    }

    public static class MakeHumanImporter
    {
        public static MhData Load(string assetPath) =>
            JsonUtility.FromJson<MhData>(File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), assetPath)));

        public static MhCharacter Build(MhData data, Transform root, Func<string, Material> materialFor, string meshDir, string prefix)
        {
            var ch = new MhCharacter { root = root, data = data, bones = new Transform[data.bones.Length] };
            for (int i = 0; i < data.bones.Length; i++)
            {
                var b = data.bones[i];
                var go = new GameObject(b.name);
                go.transform.SetParent(b.parent >= 0 ? ch.bones[b.parent] : root, false);
                go.transform.position = new Vector3(b.head[0], b.head[1], b.head[2]);
                ch.bones[i] = go.transform;
                ch.byName[b.name] = go.transform;
            }
            var bind = new Matrix4x4[ch.bones.Length];
            for (int i = 0; i < bind.Length; i++) bind[i] = ch.bones[i].worldToLocalMatrix * root.localToWorldMatrix;
            Transform rootBone = ch.byName.TryGetValue("root", out var rb) ? rb : ch.bones[0];
            ch.bind = bind;
            ch.rootBone = rootBone;

            foreach (var md in data.meshes)
            {
                // "ajuste": malha base sem subdivisão, só para ajustar a armadura (BodyInfo); não é desenhada
                if (md.material == "ajuste") continue;
                int n = md.vertices.Length / 3;
                var v = new Vector3[n];
                var nr = new Vector3[n];
                var uv = new Vector2[n];
                var bw = new BoneWeight[n];
                for (int i = 0; i < n; i++)
                {
                    v[i] = new Vector3(md.vertices[i * 3], md.vertices[i * 3 + 1], md.vertices[i * 3 + 2]);
                    nr[i] = new Vector3(md.normals[i * 3], md.normals[i * 3 + 1], md.normals[i * 3 + 2]);
                    uv[i] = new Vector2(md.uvs[i * 2], md.uvs[i * 2 + 1]);
                    bw[i] = new BoneWeight
                    {
                        boneIndex0 = md.boneIndex[i * 4], weight0 = md.boneWeight[i * 4],
                        boneIndex1 = md.boneIndex[i * 4 + 1], weight1 = md.boneWeight[i * 4 + 1],
                        boneIndex2 = md.boneIndex[i * 4 + 2], weight2 = md.boneWeight[i * 4 + 2],
                        boneIndex3 = md.boneIndex[i * 4 + 3], weight3 = md.boneWeight[i * 4 + 3],
                    };
                }
                var mesh = new Mesh { name = prefix + md.name };
                if (n > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = v;
                mesh.normals = nr;
                mesh.uv = uv;
                mesh.triangles = md.triangles;
                mesh.boneWeights = bw;
                mesh.bindposes = bind;
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                mesh = TroiaAssets.SaveMesh(mesh, $"{meshDir}/{prefix}{md.name}.asset");
                // SaveMesh copia apenas os dados básicos; reaplica pesos e bindposes
                mesh.boneWeights = bw;
                mesh.bindposes = bind;
                EditorUtility.SetDirty(mesh);

                var go = new GameObject(md.name);
                go.transform.SetParent(root, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = ch.bones;
                smr.rootBone = rootBone;
                smr.sharedMaterial = materialFor(md.material);
                smr.localBounds = new Bounds(Vector3.zero, new Vector3(2.6f, 2.8f, 2.6f));
                ch.renderers[md.name] = smr;

                if (md.name == "Corpo")
                {
                    ch.bodyVerts = v;
                    ch.bodyDominant = new string[n];
                    for (int i = 0; i < n; i++) ch.bodyDominant[i] = data.bones[md.boneIndex[i * 4]].name;
                }
            }
            return ch;
        }
    }
}
