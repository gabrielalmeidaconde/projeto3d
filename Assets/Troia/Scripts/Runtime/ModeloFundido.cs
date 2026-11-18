using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    /// <summary>
    /// Funde as peças soltas de um boneco procedural em uma malha por articulação. Um troiano era ~50 objetos,
    /// cada um com material próprio e esferas de 768 triângulos: ~50 draw calls por passada. Fundido são 6,
    /// com um material só; a cor de cada peça vai no vértice (alfa = 1 - metal) e o shader "Hero Cartoon" lê.
    /// </summary>
    public static class ModeloFundido
    {
        static Mesh esfera, capsula;

        /// <summary>Devolve o material único do boneco (para tingir), ou null se não fundiu.</summary>
        public static Material Fundir(Transform raiz, params Transform[] articulacoes)
        {
            var shader = Resources.Load<Shader>("Troia/HeroCartoon");
            if (!shader || !raiz) return null;
            var grupos = new List<Transform> { raiz };
            foreach (var a in articulacoes) if (a) grupos.Add(a);
            var pecas = new List<MeshFilter>[grupos.Count];
            for (int i = 0; i < pecas.Length; i++) pecas[i] = new List<MeshFilter>();
            foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || !mf.GetComponent<MeshRenderer>()) continue;
                // a peça pertence à articulação mais próxima acima dela
                int grupo = 0;
                for (var t = mf.transform; t && t != raiz; t = t.parent) { int k = grupos.IndexOf(t); if (k > 0) { grupo = k; break; } }
                pecas[grupo].Add(mf);
            }
            var material = new Material(shader) { name = "Boneco_fundido", color = Color.white };
            material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .28f); material.SetFloat("_VertexColor", 1);
            raiz.gameObject.AddComponent<OwnedMaterial>().material = material;

            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var tri = new List<int>();
            for (int g = 0; g < grupos.Count; g++)
            {
                if (pecas[g].Count == 0) continue;
                v.Clear(); n.Clear(); c.Clear(); tri.Clear();
                var paraGrupo = grupos[g].worldToLocalMatrix;
                foreach (var mf in pecas[g])
                {
                    var fonte = Leve(mf.sharedMesh);
                    var mat = mf.GetComponent<MeshRenderer>().sharedMaterial;
                    // cor de material é sRGB e a de vértice chega crua ao shader: converter aqui, senão o boneco sai lavado
                    Color cor = (mat ? mat.color : Color.white).linear;
                    float metal = mat && mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0;
                    var corPeca = new Color(cor.r, cor.g, cor.b, 1 - Mathf.Clamp01(metal));
                    var m = paraGrupo * mf.transform.localToWorldMatrix; var mn = m.inverse.transpose;
                    var fv = fonte.vertices; var fn = fonte.normals; int b = v.Count;
                    for (int i = 0; i < fv.Length; i++)
                    {
                        v.Add(m.MultiplyPoint3x4(fv[i]));
                        n.Add(i < fn.Length ? mn.MultiplyVector(fn[i]).normalized : Vector3.up);
                        c.Add(corPeca);
                    }
                    foreach (int i in fonte.triangles) tri.Add(b + i);
                }
                var malha = new Mesh { name = grupos[g].name + "_fundido" };
                malha.SetVertices(v); malha.SetNormals(n); malha.SetColors(c); malha.SetTriangles(tri, 0); malha.RecalculateBounds();
                foreach (var mf in pecas[g])
                {
                    // folhas somem; peças com filhos (pontos de encaixe) só perdem o desenho
                    if (mf.transform.childCount == 0 && mf.transform != grupos[g]) { mf.gameObject.SetActive(false); Object.Destroy(mf.gameObject); continue; }
                    foreach (var dono in mf.GetComponents<OwnedMaterial>()) Object.Destroy(dono);
                    foreach (var dono in mf.GetComponents<OwnedMesh>()) Object.Destroy(dono);
                    Object.Destroy(mf.GetComponent<MeshRenderer>()); Object.Destroy(mf);
                }
                var go = new GameObject("Malha_fundida") { layer = 2 };
                go.transform.SetParent(grupos[g], false);
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                go.AddComponent<OwnedMesh>().mesh = malha;
            }
            return material;
        }

        // As primitivas do Unity são densas demais para peças de poucos centímetros na tela.
        static Mesh Leve(Mesh m)
        {
            if (m.name == "Sphere") return esfera ? esfera : esfera = Redonda(0);
            if (m.name == "Capsule") return capsula ? capsula : capsula = Redonda(.5f);
            return m;
        }

        /// <summary>Esfera de raio 0,5 (ou cápsula de altura 2 quando meio > 0) com 12 gomos.</summary>
        static Mesh Redonda(float meio)
        {
            const int gomos = 12, aneis = 4; // anéis por hemisfério
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int h = 0; h < 2; h++)
                for (int a = 0; a <= aneis; a++)
                {
                    float lat = (h == 0 ? a : aneis + a) * Mathf.PI / (2 * aneis);
                    float y = Mathf.Cos(lat), r = Mathf.Sin(lat);
                    for (int g = 0; g < gomos; g++)
                    {
                        float lon = g * Mathf.PI * 2 / gomos;
                        var dir = new Vector3(Mathf.Cos(lon) * r, y, Mathf.Sin(lon) * r);
                        v.Add(dir * .5f + Vector3.up * (h == 0 ? meio : -meio)); n.Add(dir);
                    }
                }
            int linhas = 2 * (aneis + 1);
            for (int a = 0; a < linhas - 1; a++)
                for (int g = 0; g < gomos; g++)
                {
                    int i0 = a * gomos + g, i1 = a * gomos + (g + 1) % gomos, i2 = i0 + gomos, i3 = i1 + gomos;
                    t.Add(i0); t.Add(i1); t.Add(i2); t.Add(i1); t.Add(i3); t.Add(i2);
                }
            var malha = new Mesh { name = meio > 0 ? "Capsula_leve" : "Esfera_leve", hideFlags = HideFlags.HideAndDontSave };
            malha.SetVertices(v); malha.SetNormals(n); malha.SetTriangles(t, 0); malha.RecalculateBounds();
            return malha;
        }
    }
}
