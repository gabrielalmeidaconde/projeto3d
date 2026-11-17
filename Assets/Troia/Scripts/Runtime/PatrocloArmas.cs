using System.Collections.Generic;
using UnityEngine;

namespace Troia
{
    /// <summary>Armas feitas por código para o Pátroclo: lança (dory) e dardo, com os materiais do personagem.</summary>
    public static class PatrocloArmas
    {
        public static Material Madeira, Bronze;

        static Material Reserva(ref Material m, Color cor, float metal)
        {
            if (m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = cor };
            m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", metal > .5f ? .6f : .25f);
            return m;
        }
        static Material reservaMadeira, reservaBronze;

        /// <summary>Haste em +Z local. 'pega' é a distância da mão até o conto (ponta de trás).</summary>
        public static GameObject Lanca(string nome, float comprimento, float pega, float raio, float folha)
        {
            var go = new GameObject(nome);
            var haste = new Construtor();
            float z0 = -pega, z1 = comprimento - pega - folha;
            haste.Tubo(z0, z1, raio, raio * .86f, 10);
            // empunhadura de couro trançado: três anéis
            for (int i = -1; i <= 1; i++) haste.Tubo(i * .07f - .012f, i * .07f + .012f, raio * 1.25f, raio * 1.25f, 10);
            var metal = new Construtor();
            // folha: losango achatado com nervura
            metal.Tubo(z1 - .11f, z1, raio * 1.35f, raio * 1.1f, 10);           // alvado
            metal.Folha(z1, z1 + folha, folha * .17f, raio * .95f);
            // conto (sauroter): ponta de bronze na base
            metal.Tubo(z0 - .02f, z0 + .08f, raio * 1.3f, raio * 1.2f, 10);
            metal.Cone(z0 - .02f, z0 - .2f, raio * 1.2f, 8);
            Peca(go.transform, "Haste", haste, Madeira ? Madeira : Reserva(ref reservaMadeira, new Color(.36f, .24f, .14f), 0));
            Peca(go.transform, "Ponta", metal, Bronze ? Bronze : Reserva(ref reservaBronze, new Color(.78f, .58f, .3f), 1));
            return go;
        }
        static void Peca(Transform pai, string nome, Construtor c, Material material)
        {
            var go = new GameObject(nome); go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = c.Malha(nome);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<MalhaPropria>();
        }

        class Construtor
        {
            readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector3> n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> t = new List<int>();
            public void Tubo(float za, float zb, float ra, float rb, int lados)
            {
                int b = v.Count;
                for (int i = 0; i <= lados; i++)
                {
                    float a = i * 2 * Mathf.PI / lados; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    v.Add(d * ra + Vector3.forward * za); v.Add(d * rb + Vector3.forward * zb); n.Add(d); n.Add(d);
                    uv.Add(new Vector2(i / (float)lados * .3f, za)); uv.Add(new Vector2(i / (float)lados * .3f, zb));
                }
                for (int i = 0; i < lados; i++) { int k = b + i * 2; Quad(k, k + 1, k + 3, k + 2, zb > za); }
            }
            public void Cone(float za, float zb, float r, int lados)
            {
                for (int i = 0; i < lados; i++)
                {
                    float a0 = i * 2 * Mathf.PI / lados, a1 = (i + 1) * 2 * Mathf.PI / lados;
                    Tri(new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * r + Vector3.forward * za, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * r + Vector3.forward * za, Vector3.forward * zb);
                }
            }
            public void Folha(float za, float zb, float meiaLargura, float meiaEspessura)
            {
                // perfil da folha ao longo de Z; em cada estação um losango (gumes em ±X, nervura em ±Y)
                float[] pz = { 0, .12f, .34f, .62f, .86f, 1 }; float[] pl = { .32f, .78f, 1, .8f, .42f, 0 };
                for (int i = 0; i < pz.Length - 1; i++)
                {
                    float z0 = Mathf.Lerp(za, zb, pz[i]), z1 = Mathf.Lerp(za, zb, pz[i + 1]);
                    float w0 = meiaLargura * pl[i], w1 = meiaLargura * pl[i + 1];
                    float e0 = meiaEspessura * Mathf.Lerp(1, .35f, pz[i]), e1 = meiaEspessura * Mathf.Lerp(1, .35f, pz[i + 1]);
                    for (int q = 0; q < 4; q++)
                    {
                        float sx = q == 0 || q == 3 ? 1 : -1, sy = q < 2 ? 1 : -1;
                        Vector3 a = new Vector3(sx * w0, 0, z0), b = new Vector3(0, sy * e0, z0), c = new Vector3(0, sy * e1, z1), d = new Vector3(sx * w1, 0, z1);
                        if (sx * sy > 0) { Tri(a, b, c); Tri(a, c, d); } else { Tri(a, c, b); Tri(a, d, c); }
                    }
                }
            }
            void Quad(int a, int b, int c, int d, bool frente)
            {
                if (frente) { t.Add(a); t.Add(b); t.Add(c); t.Add(a); t.Add(c); t.Add(d); }
                else { t.Add(a); t.Add(c); t.Add(b); t.Add(a); t.Add(d); t.Add(c); }
            }
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int k = v.Count; v.Add(a); v.Add(b); v.Add(c); n.Add(normal); n.Add(normal); n.Add(normal);
                uv.Add(new Vector2(a.x, a.z)); uv.Add(new Vector2(b.x, b.z)); uv.Add(new Vector2(c.x, c.z));
                t.Add(k); t.Add(k + 1); t.Add(k + 2);
            }
            public Mesh Malha(string nome)
            {
                var m = new Mesh { name = nome };
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetUVs(1, uv);
                var cores = new Color32[v.Count]; for (int i = 0; i < cores.Length; i++) cores[i] = new Color32(255, 255, 255, 255);
                m.colors32 = cores; m.SetTriangles(t, 0); m.RecalculateTangents(); m.RecalculateBounds();
                return m;
            }
        }
    }

    public class MalhaPropria : MonoBehaviour
    {
        void OnDestroy() { var f = GetComponent<MeshFilter>(); if (f && f.sharedMesh) Destroy(f.sharedMesh); }
    }

    /// <summary>Faixa de luz que segue a arma durante o golpe (da base à ponta), no lugar de um TrailRenderer fino.</summary>
    public class RastroDeArma : MonoBehaviour
    {
        public Transform arma;
        public float inicio = .3f, fim = 1.3f, duracao = .17f;
        public Vector3 eixo = Vector3.forward;
        public Color cor = Color.white;
        public bool emitindo;
        struct Amostra { public Vector3 a, b; public float t; }
        readonly List<Amostra> amostras = new List<Amostra>();
        readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color> cores = new List<Color>(); readonly List<int> tri = new List<int>();
        Mesh malha; Material material; float relogio;
        static readonly int CorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            malha = new Mesh { name = "Rastro" }; malha.MarkDynamic();
            var filho = new GameObject("Rastro_malha") { layer = 2 };
            filho.transform.SetParent(null, false);
            filho.AddComponent<MeshFilter>().sharedMesh = malha;
            var r = filho.AddComponent<MeshRenderer>();
            material = new Material(Shader.Find("Troia/Rastro") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            r.sharedMaterial = material; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            visual = filho;
        }
        GameObject visual;

        /// <summary>Chamado depois que a pose do quadro foi resolvida.</summary>
        public void Passo(float dt)
        {
            relogio += dt;
            if (emitindo && arma)
            {
                var s = new Amostra { a = arma.TransformPoint(eixo * inicio), b = arma.TransformPoint(eixo * fim), t = relogio };
                if (amostras.Count == 0 || dt > 0) amostras.Add(s);
            }
            while (amostras.Count > 0 && relogio - amostras[0].t > duracao) amostras.RemoveAt(0);
            Reconstruir();
        }
        /// <summary>Uma faixa quase invisível por alguns quadros: força a compilação do shader antes do primeiro golpe.</summary>
        public void Aquecer(Vector3 ponto)
        {
            var guardada = cor; cor = new Color(.004f, .004f, .004f, 1);
            amostras.Add(new Amostra { a = ponto, b = ponto + Vector3.up * .05f, t = relogio });
            amostras.Add(new Amostra { a = ponto + Vector3.right * .05f, b = ponto + new Vector3(.05f, .05f, 0), t = relogio + .001f });
            Reconstruir(); cor = guardada;
        }
        public void Limpar() { amostras.Clear(); if (malha) malha.Clear(); }

        void Reconstruir()
        {
            malha.Clear();
            if (amostras.Count < 2) return;
            v.Clear(); uv.Clear(); cores.Clear(); tri.Clear();
            const int sub = 4;
            int n = amostras.Count;
            for (int i = 0; i < n - 1; i++)
            {
                var p0 = amostras[Mathf.Max(i - 1, 0)]; var p1 = amostras[i]; var p2 = amostras[i + 1]; var p3 = amostras[Mathf.Min(i + 2, n - 1)];
                for (int s = 0; s < sub + (i == n - 2 ? 1 : 0); s++)
                {
                    float f = s / (float)sub;
                    Vector3 a = Catmull(p0.a, p1.a, p2.a, p3.a, f), b = Catmull(p0.b, p1.b, p2.b, p3.b, f);
                    float idade = Mathf.Clamp01((relogio - Mathf.Lerp(p1.t, p2.t, f)) / duracao);
                    v.Add(a); v.Add(b); uv.Add(new Vector2(idade, 0)); uv.Add(new Vector2(idade, 1)); cores.Add(cor); cores.Add(cor);
                }
            }
            for (int i = 0; i + 3 < v.Count; i += 2) { tri.Add(i); tri.Add(i + 1); tri.Add(i + 3); tri.Add(i); tri.Add(i + 3); tri.Add(i + 2); }
            malha.SetVertices(v); malha.SetUVs(0, uv); malha.SetColors(cores); malha.SetTriangles(tri, 0); malha.RecalculateBounds();
            material.SetColor(CorId, cor);
        }
        static Vector3 Catmull(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return .5f * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
        }
        void OnDestroy() { if (visual) Destroy(visual); if (malha) Destroy(malha); if (material) Destroy(material); }
    }
}
