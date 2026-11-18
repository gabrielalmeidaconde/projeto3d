using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>
    /// Níveis de detalhe automáticos (Mesh LOD do Unity 6) nas malhas do Pátroclo. O herói tem ~600 mil triângulos,
    /// feitos para o close da vitrine; em jogo ele ocupa uma fração da tela e é redesenhado em cada passada
    /// (profundidade, cor, cascatas de sombra). Com os níveis gravados na própria malha o Unity escolhe sozinho
    /// a versão que cabe no tamanho em tela: de perto continua a malha cheia.
    /// </summary>
    public static class PatrocloLod
    {
        const int MinimoDeTriangulos = 600;

        [MenuItem("Troia/Personagem/Gerar níveis de detalhe (LOD)", priority = 44)]
        public static void Gerar()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatrocloJogo.Prefab);
            if (!prefab) { Debug.LogWarning("[Troia] prefab do Pátroclo não encontrado"); return; }
            var malhas = new List<Mesh>();
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Mesh m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
                if (m && AssetDatabase.Contains(m) && !malhas.Contains(m)) malhas.Add(m);
            }
            var sb = new StringBuilder();
            long antes = 0, menor = 0;
            foreach (var m in malhas)
            {
                int t0 = Triangulos(m, 0);
                antes += t0;
                if (t0 < MinimoDeTriangulos) { menor += t0; continue; }
                MeshLodUtility.GenerateMeshLods(m, -1);
                EditorUtility.SetDirty(m);
                int ultimo = Triangulos(m, m.lodCount - 1);
                menor += ultimo;
                sb.Append($"\n  {m.name}: {m.lodCount} níveis, {t0} -> {ultimo}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Troia] LOD do Pátroclo: {malhas.Count} malhas, {antes} triângulos no nível 0, {menor} no nível mais baixo{sb}");
        }

        static int Triangulos(Mesh m, int nivel)
        {
            long n = 0;
            for (int i = 0; i < m.subMeshCount; i++)
                n += m.lodCount > 1 ? m.GetLod(i, Mathf.Min(nivel, m.lodCount - 1)).indexCount : m.GetSubMesh(i).indexCount;
            return (int)(n / 3);
        }
    }
}
