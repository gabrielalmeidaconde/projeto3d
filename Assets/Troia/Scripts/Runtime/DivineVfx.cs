using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Troia
{
    /// <summary>Guarda materiais e malhas criados para um efeito e os destrói junto com ele.</summary>
    public class DonoDeEfeito : MonoBehaviour
    {
        readonly List<Object> donos = new List<Object>();
        public T Dono<T>(T o) where T : Object { donos.Add(o); return o; }
        protected virtual void OnDestroy() { foreach (var o in donos) if (o) Destroy(o); }
    }

    /// <summary>Efeito de duração fixa: 'passo' recebe a idade normalizada (0..1) e o dt a cada quadro.</summary>
    public class EfeitoDivino : DonoDeEfeito
    {
        public float duracao = 1;
        public System.Action<float, float> passo;
        float idade;
        public float Segundos => idade;
        void Update()
        {
            float dt = Time.deltaTime;
            idade += dt;
            passo?.Invoke(duracao > 0 ? Mathf.Clamp01(idade / duracao) : 1, dt);
            if (idade >= duracao) Destroy(gameObject);
        }
    }

    /// <summary>Efeito que dura enquanto o poder estiver ativo: entra, fica e sai suave quando mandam encerrar.</summary>
    public class AuraDivina : DonoDeEfeito
    {
        public float entra = .25f, sai = .4f;
        public System.Action<float, float> passo;      // (força 0..1, dt)
        public System.Action aoEncerrar;
        public bool Saindo { get; private set; }
        float forca; bool morrendo;
        public void Encerrar()
        {
            if (Saindo) return;
            Saindo = true; aoEncerrar?.Invoke();
            foreach (var ps in GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        void Update()
        {
            float dt = Time.deltaTime;
            forca = Mathf.MoveTowards(forca, Saindo ? 0 : 1, dt / Mathf.Max(.01f, Saindo ? sai : entra));
            passo?.Invoke(forca, dt);
            if (Saindo && forca <= 0 && !morrendo) { morrendo = true; Destroy(gameObject, 1.3f); }   // as partículas terminam de cair
        }
    }

    /// <summary>
    /// Raio: uma linha quebrada por subdivisão (com ramos) desenhada como faixa virada para a câmera.
    /// É refeita a cada 'intervalo' segundos, o que dá o tremor de relâmpago.
    /// </summary>
    public class RaioDivino : MonoBehaviour
    {
        public System.Func<Vector3> de, ate;
        public float largura = .6f, desvio = .1f, intervalo = .05f, forca = 1;
        public int ramos = 3, niveis = 6;
        Mesh malha;
        readonly List<List<Vector3>> linhas = new List<List<Vector3>>();
        readonly List<float> grossuras = new List<float>();
        readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color> cores = new List<Color>(); readonly List<int> tri = new List<int>();
        float relogio;

        public static RaioDivino Criar(DonoDeEfeito dono, System.Func<Vector3> de, System.Func<Vector3> ate, Color cor, float largura, int ramos)
        {
            // a malha é montada em coordenadas do mundo: o objeto fica solto na origem e morre com o dono
            var go = new GameObject("Raio") { layer = 2 };
            dono.Dono(go);
            var r = go.AddComponent<RaioDivino>(); r.de = de; r.ate = ate; r.largura = largura; r.ramos = ramos;
            r.malha = dono.Dono(new Mesh { name = "Raio" }); r.malha.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = r.malha;
            var mr = go.AddComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            var m = DivineVfx.Mat(dono, "Raio"); m.SetColor(DivineVfx.CorId, cor); mr.sharedMaterial = m;
            return r;
        }
        static void Fractal(List<Vector3> pts, Vector3 a, Vector3 b, float amplitude, int niveis)
        {
            pts.Clear(); pts.Add(a); pts.Add(b);
            Vector3 eixo = (b - a).normalized;
            for (int n = 0; n < niveis; n++)
            {
                for (int i = pts.Count - 2; i >= 0; i--)
                {
                    Vector3 fora = Vector3.Cross(eixo, Random.onUnitSphere);
                    pts.Insert(i + 1, (pts[i] + pts[i + 1]) * .5f + fora * (amplitude * Random.Range(.35f, 1f)));
                }
                amplitude *= .5f;
            }
        }
        void Gerar()
        {
            Vector3 a = de(), b = ate();
            float dist = (b - a).magnitude;
            int usadas = 0;
            if (dist > .02f)
            {
                var principal = Linha(usadas++); Fractal(principal, a, b, dist * desvio, niveis);
                Vector3 eixo = (b - a) / dist;
                for (int r = 0; r < ramos; r++)
                {
                    int i = Random.Range(principal.Count / 6, principal.Count * 4 / 5);
                    Vector3 lado = Vector3.Cross(eixo, Random.onUnitSphere).normalized;
                    float comprimento = dist * Random.Range(.1f, .26f);
                    Vector3 fim = principal[i] + (eixo * Random.Range(.5f, 1f) + lado * Random.Range(.45f, 1f)).normalized * comprimento;
                    Fractal(Linha(usadas++), principal[i], fim, comprimento * desvio * 1.7f, Mathf.Max(3, niveis - 2));
                }
            }
            while (linhas.Count > usadas) linhas.RemoveAt(linhas.Count - 1);
            grossuras.Clear(); for (int i = 0; i < linhas.Count; i++) grossuras.Add(i == 0 ? 1 : .5f);
        }
        List<Vector3> Linha(int i) { while (linhas.Count <= i) linhas.Add(new List<Vector3>()); return linhas[i]; }
        void LateUpdate()
        {
            if (de == null || ate == null || !malha) return;
            relogio -= Time.unscaledDeltaTime;
            if (relogio <= 0) { Gerar(); relogio = intervalo * Random.Range(.7f, 1.3f); }
            malha.Clear();
            var cam = Camera.main;
            if (!cam || forca <= .01f) return;
            Vector3 olho = cam.transform.position;
            v.Clear(); uv.Clear(); cores.Clear(); tri.Clear();
            for (int l = 0; l < linhas.Count; l++)
            {
                var pts = linhas[l]; int n = pts.Count; if (n < 2) continue;
                int inicio = v.Count; float g = grossuras[l];
                for (int i = 0; i < n; i++)
                {
                    float u = i / (float)(n - 1);
                    Vector3 tang = pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)];
                    Vector3 lado = Vector3.Cross(tang, olho - pts[i]).normalized * (largura * g * (l == 0 ? 1 : Mathf.Lerp(1, .2f, u)) * .5f);
                    v.Add(pts[i] - lado); v.Add(pts[i] + lado); uv.Add(new Vector2(u, 0)); uv.Add(new Vector2(u, 1));
                    var c = new Color(1, 1, 1, forca * (l == 0 ? 1 : Mathf.Lerp(.75f, .3f, u)));
                    cores.Add(c); cores.Add(c);
                }
                for (int i = 0; i < n - 1; i++)
                {
                    int k = inicio + i * 2;
                    tri.Add(k); tri.Add(k + 1); tri.Add(k + 3); tri.Add(k); tri.Add(k + 3); tri.Add(k + 2);
                }
            }
            malha.SetVertices(v); malha.SetUVs(0, uv); malha.SetColors(cores); malha.SetTriangles(tri, 0); malha.RecalculateBounds();
        }
    }

    /// <summary>Parede de água em anel: a crista sobe, corre para fora e desaba. A malha é refeita a cada quadro.</summary>
    public class OndaDivina : MonoBehaviour
    {
        const int Gomos = 80, Fiadas = 7;
        public Vector3 frente = Vector3.forward;
        public float forca = 1;
        Mesh malha;
        readonly Vector3[] v = new Vector3[(Gomos + 1) * (Fiadas + 1)];
        readonly Color[] cores = new Color[(Gomos + 1) * (Fiadas + 1)];
        readonly float[] crista = new float[Gomos + 1];

        public static OndaDivina Criar(DonoDeEfeito dono, Vector3 frente, Material material)
        {
            var go = new GameObject("Onda") { layer = 2 };
            go.transform.SetParent(dono.transform, false);
            var o = go.AddComponent<OndaDivina>(); o.frente = Vector3.ProjectOnPlane(frente, Vector3.up).normalized;
            o.malha = dono.Dono(new Mesh { name = "Onda" }); o.malha.MarkDynamic();
            // crista irregular: soma de senos com fase sorteada, igual no começo e no fim da volta
            float f1 = Random.value * 6.28f, f2 = Random.value * 6.28f, f3 = Random.value * 6.28f;
            for (int j = 0; j <= Gomos; j++)
            {
                float a = j / (float)Gomos * Mathf.PI * 2;
                o.crista[j] = 1 + .15f * Mathf.Sin(a * 3 + f1) + .08f * Mathf.Sin(a * 5 + f2) + .03f * Mathf.Sin(a * 9 + f3);
            }
            var uv = new Vector2[o.v.Length]; var tri = new int[Gomos * Fiadas * 6]; int t = 0;
            for (int j = 0; j <= Gomos; j++)
                for (int i = 0; i <= Fiadas; i++)
                    uv[j * (Fiadas + 1) + i] = new Vector2(j / (float)Gomos, i / (float)Fiadas);
            for (int j = 0; j < Gomos; j++)
                for (int i = 0; i < Fiadas; i++)
                {
                    int k = j * (Fiadas + 1) + i, p = k + Fiadas + 1;
                    tri[t++] = k; tri[t++] = k + 1; tri[t++] = p + 1; tri[t++] = k; tri[t++] = p + 1; tri[t++] = p;
                }
            o.Formar(.5f, .01f);
            o.malha.vertices = o.v; o.malha.uv = uv; o.malha.colors = o.cores; o.malha.triangles = tri;
            go.AddComponent<MeshFilter>().sharedMesh = o.malha;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = material; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            return o;
        }
        /// <summary>Altura da crista (em metros) na direção dada, para o respingo sair do lugar certo.</summary>
        public float Altura(Vector3 dir, float altura) => altura * Mathf.Lerp(.5f, 1f, Mathf.Clamp01(Vector3.Dot(dir, frente) * .5f + .5f));
        public void Formar(float raio, float altura)
        {
            for (int j = 0; j <= Gomos; j++)
            {
                float a = j / (float)Gomos * Mathf.PI * 2;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                float h = Altura(dir, altura) * crista[j];
                for (int i = 0; i <= Fiadas; i++)
                {
                    float s = i / (float)Fiadas;
                    // perfil da onda: nasce atrás, sobe e a crista se dobra para a frente
                    float sobe = h * (1 - (1 - s) * (1 - s));
                    float avanca = h * (.62f * s * s - .2f);
                    int k = j * (Fiadas + 1) + i;
                    v[k] = dir * (raio + avanca) + Vector3.up * sobe;
                    cores[k] = new Color(1, 1, 1, forca);
                }
            }
            if (!malha) return;
            malha.vertices = v; malha.colors = cores; malha.RecalculateBounds();
        }
    }

    /// <summary>Efeitos visuais das bênçãos. Tudo é montado por código: malhas simples, partículas e os shaders Troia/Divino.</summary>
    public static class DivineVfx
    {
        public static readonly int CorId = Shader.PropertyToID("_Color"), Cor2Id = Shader.PropertyToID("_Cor2"), ModoId = Shader.PropertyToID("_Modo"),
            TId = Shader.PropertyToID("_T"), ForcaId = Shader.PropertyToID("_Forca"), RaiosId = Shader.PropertyToID("_Raios"), GiroId = Shader.PropertyToID("_Giro"),
            SementeId = Shader.PropertyToID("_Semente"), RolagemId = Shader.PropertyToID("_Rolagem"), SuaveId = Shader.PropertyToID("_Suave"),
            SrcId = Shader.PropertyToID("_Src"), DstId = Shader.PropertyToID("_Dst"), MalhaId = Shader.PropertyToID("_Malha"), ImpactoId = Shader.PropertyToID("_Impacto");

        static Mesh quadro, esfera;
        static readonly Dictionary<int, Mesh> tubos = new Dictionary<int, Mesh>();
        static Texture2D brilho, fumaca, pena, risco, estilhaco;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Limpar() { quadro = esfera = null; tubos.Clear(); brilho = fumaca = pena = risco = estilhaco = null; }

        public class Peca { public Transform tr; public Material m; }

        // ------------------------------------------------------------------ recursos
        public static Material Mat(DonoDeEfeito dono, string shader)
        {
            var s = Shader.Find("Troia/Divino/" + shader) ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            return dono.Dono(new Material(s));
        }
        static Mesh Quadro()
        {
            if (quadro) return quadro;
            quadro = new Mesh { name = "Quadro_divino" };
            quadro.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
            quadro.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            quadro.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            quadro.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            quadro.bounds = new Bounds(Vector3.zero, new Vector3(1, .2f, 1));
            return quadro;
        }
        /// <summary>Tubo aberto de raio 1 na base e 'topo' em cima, altura 1.</summary>
        static Mesh Tubo(float topo)
        {
            int chave = Mathf.RoundToInt(topo * 100);
            if (tubos.TryGetValue(chave, out var m) && m) return m;
            const int n = 40, fiadas = 6;
            var v = new Vector3[(n + 1) * (fiadas + 1)]; var nrm = new Vector3[v.Length]; var uv = new Vector2[v.Length]; var tri = new int[n * fiadas * 6]; int t = 0;
            for (int j = 0; j <= n; j++)
            {
                float a = j / (float)n * Mathf.PI * 2; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int i = 0; i <= fiadas; i++)
                {
                    float y = i / (float)fiadas; int k = j * (fiadas + 1) + i;
                    v[k] = dir * Mathf.Lerp(1, topo, y * y) + Vector3.up * y; nrm[k] = dir; uv[k] = new Vector2(j / (float)n, y);
                }
            }
            for (int j = 0; j < n; j++)
                for (int i = 0; i < fiadas; i++)
                {
                    int k = j * (fiadas + 1) + i, p = k + fiadas + 1;
                    tri[t++] = k; tri[t++] = k + 1; tri[t++] = p + 1; tri[t++] = k; tri[t++] = p + 1; tri[t++] = p;
                }
            m = new Mesh { name = "Tubo_divino" }; m.vertices = v; m.normals = nrm; m.uv = uv; m.triangles = tri; m.RecalculateBounds();
            tubos[chave] = m; return m;
        }
        static Mesh Esfera()
        {
            if (esfera) return esfera;
            const int n = 48, fiadas = 24;
            var v = new Vector3[(n + 1) * (fiadas + 1)]; var tri = new int[n * fiadas * 6]; int t = 0;
            for (int j = 0; j <= n; j++)
                for (int i = 0; i <= fiadas; i++)
                {
                    float lon = j / (float)n * Mathf.PI * 2, lat = (i / (float)fiadas - .5f) * Mathf.PI;
                    v[j * (fiadas + 1) + i] = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                }
            for (int j = 0; j < n; j++)
                for (int i = 0; i < fiadas; i++)
                {
                    int k = j * (fiadas + 1) + i, p = k + fiadas + 1;
                    tri[t++] = k; tri[t++] = k + 1; tri[t++] = p + 1; tri[t++] = k; tri[t++] = p + 1; tri[t++] = p;
                }
            esfera = new Mesh { name = "Esfera_divina" }; esfera.vertices = v; esfera.normals = v; esfera.triangles = tri; esfera.RecalculateBounds();
            return esfera;
        }
        static Texture2D Textura(ref Texture2D guardada, string nome, int n, System.Func<float, float, float> alfa)
        {
            if (guardada) return guardada;
            guardada = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = nome, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(alfa((x + .5f) / n * 2 - 1, (y + .5f) / n * 2 - 1)));
            guardada.SetPixels32(px); guardada.Apply(true, true);
            return guardada;
        }
        static float Ruido(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float H(int a, int b) { float s = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }
            return Mathf.Lerp(Mathf.Lerp(H(xi, yi), H(xi + 1, yi), fx), Mathf.Lerp(H(xi, yi + 1), H(xi + 1, yi + 1), fx), fy);
        }
        static Texture2D Brilho() => Textura(ref brilho, "Brilho", 64, (u, v) => Mathf.Pow(Mathf.Clamp01(1 - Mathf.Sqrt(u * u + v * v)), 2.2f));
        static Texture2D Fumaca() => Textura(ref fumaca, "Fumaca", 128, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float n = Ruido(u * 2.3f + 5, v * 2.3f + 9) * .55f + Ruido(u * 5.1f + 1, v * 5.1f + 3) * .3f + Ruido(u * 11f, v * 11f) * .15f;
            return Mathf.Pow(Mathf.Clamp01(1 - r), 1.3f) * (.25f + 1.1f * n);
        });
        static Texture2D Pena() => Textura(ref pena, "Pena", 64, (u, v) =>
        {
            // pena deitada: haste no meio, barbas em farpa, ponta afinada
            float largura = .3f * Mathf.Pow(Mathf.Clamp01(1 - u * u), .7f) * Mathf.Lerp(1, .55f, (u + 1) * .5f);
            float corpo = Mathf.SmoothStep(largura, largura * .7f, Mathf.Abs(v));
            float farpas = .7f + .3f * Mathf.Sin((u * 1.2f + Mathf.Abs(v)) * 46);
            float haste = Mathf.SmoothStep(.035f, 0, Mathf.Abs(v)) * Mathf.SmoothStep(1, .9f, Mathf.Abs(u));
            return Mathf.Max(corpo * farpas * .85f, haste);
        });
        static Texture2D Risco() => Textura(ref risco, "Risco", 64, (u, v) =>
            Mathf.Pow(Mathf.Clamp01(1 - Mathf.Abs(v) * 5f), 2) * Mathf.Pow(Mathf.Clamp01(1 - Mathf.Abs(u)), 1.4f));
        static Texture2D Estilhaco() => Textura(ref estilhaco, "Estilhaco", 32, (u, v) =>
        {
            // triângulo fino: caco de escudo
            float t = (v + 1) * .5f;
            return v > -.9f && v < .9f && Mathf.Abs(u + (t - .5f) * .3f) < .42f * (1 - t) ? 1 : 0;
        });

        public static Vector3 Solo(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 40, ~(1 << 2), QueryTriggerInteraction.Ignore) ? hit.point : p;
        static float Pulso(float t, float sobe, float desce) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, sobe, t)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(desce, 1, t)));

        static EfeitoDivino Novo(string nome, Vector3 posicao, float duracao, Transform pai = null)
        {
            var go = new GameObject(nome) { layer = 2 };
            if (pai) { go.transform.SetParent(pai, false); go.transform.localPosition = Vector3.zero; }
            else go.transform.position = posicao;
            var e = go.AddComponent<EfeitoDivino>(); e.duracao = duracao; return e;
        }
        static AuraDivina NovaAura(string nome, Transform pai)
        {
            var go = new GameObject(nome) { layer = 2 };
            go.transform.SetParent(pai, false);
            return go.AddComponent<AuraDivina>();
        }
        static Peca Malha(DonoDeEfeito dono, string nome, Mesh malha, string shader, Vector3 local, Vector3 escala)
        {
            var go = new GameObject(nome) { layer = 2 };
            go.transform.SetParent(dono.transform, false); go.transform.localPosition = local; go.transform.localScale = escala;
            go.AddComponent<MeshFilter>().sharedMesh = malha;
            var mr = go.AddComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            var m = Mat(dono, shader); mr.sharedMaterial = m;
            m.SetFloat(SementeId, Random.value * 40);
            return new Peca { tr = go.transform, m = m };
        }
        /// <summary>Desenho no chão (ver os modos em Chao.shader).</summary>
        static Peca Chao(DonoDeEfeito dono, int modo, float raio, Color cor, Color cor2, Vector3 local = default)
        {
            var p = Malha(dono, "Chao", Quadro(), "Chao", local + Vector3.up * .07f, new Vector3(raio * 2, 1, raio * 2));
            p.m.SetFloat(ModoId, modo); p.m.SetColor(CorId, cor); p.m.SetColor(Cor2Id, cor2); p.m.SetFloat(TId, 0);
            return p;
        }
        /// <summary>Tubo vertical (ver os modos em Coluna.shader).</summary>
        static Peca Coluna(DonoDeEfeito dono, int modo, Vector3 local, float raio, float altura, Color cor, Color cor2, float topo = 1)
        {
            var p = Malha(dono, "Coluna", Tubo(topo), "Coluna", local, new Vector3(raio, altura, raio));
            p.m.SetFloat(ModoId, modo); p.m.SetColor(CorId, cor); p.m.SetColor(Cor2Id, cor2); p.m.SetFloat(TId, 0);
            return p;
        }
        static Light Luz(DonoDeEfeito dono, Vector3 local, Color cor, float alcance)
        {
            var go = new GameObject("Luz") { layer = 2 };
            go.transform.SetParent(dono.transform, false); go.transform.localPosition = local;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = cor; l.range = alcance; l.intensity = 0;
            l.shadows = LightShadows.None; l.renderMode = LightRenderMode.ForcePixel;
            return l;
        }

        struct Jato
        {
            public Texture2D tex; public bool mistura;       // mistura = alfa comum; senão soma (luz)
            public Color cor;                                  // HDR, vai no material
            public Color fim;                                  // tom no fim da vida (o alfa sempre cai a zero)
            public float vida0, vida1, vel0, vel1, tam0, tam1, gravidade, crescer, estica;
            public int maximo;
        }
        static ParticleSystem Particulas(DonoDeEfeito dono, string nome, Jato j, Vector3 local = default)
        {
            var go = new GameObject(nome) { layer = 2 };
            go.transform.SetParent(dono.transform, false); go.transform.localPosition = local;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.duration = 5;
            main.startLifetime = new ParticleSystem.MinMaxCurve(j.vida0, j.vida1); main.startSpeed = new ParticleSystem.MinMaxCurve(j.vel0, j.vel1);
            main.startSize = new ParticleSystem.MinMaxCurve(j.tam0, j.tam1); main.gravityModifier = j.gravidade; main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = j.maximo > 0 ? j.maximo : 300;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 0;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .1f;
            var cor = ps.colorOverLifetime; cor.enabled = true;
            var g = new Gradient();
            Color fim = j.fim.maxColorComponent > 0 ? j.fim : Color.white;
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(fim, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(.8f, .5f), new GradientAlphaKey(0, 1) });
            cor.color = new ParticleSystem.MinMaxGradient(g);
            var tam = ps.sizeOverLifetime; tam.enabled = true;
            float cresce = j.crescer > 0 ? j.crescer : .35f;
            tam.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, cresce > 1 ? .5f : 1), new Keyframe(1, cresce)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            if (j.estica > 0) { r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 1; r.velocityScale = j.estica; }
            var m = Mat(dono, "Particula"); m.mainTexture = j.tex ? j.tex : Brilho(); m.SetColor(CorId, j.cor);
            m.SetFloat(SrcId, (float)BlendMode.SrcAlpha); m.SetFloat(DstId, (float)(j.mistura ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
            r.sharedMaterial = m;
            ps.Play();
            return ps;
        }
        static void Cone(ParticleSystem ps, float angulo, float raio)
        {
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = angulo; sh.radius = raio; sh.rotation = new Vector3(-90, 0, 0);
        }
        static void Bola(ParticleSystem ps, float raio, bool casca = false)
        {
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = raio; sh.radiusThickness = casca ? 0 : 1;
        }
        static void Meia(ParticleSystem ps, float raio)
        {
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Hemisphere; sh.radius = raio; sh.rotation = new Vector3(-90, 0, 0);
        }
        static void Taxa(ParticleSystem ps, float porSegundo) { var em = ps.emission; em.rateOverTime = porSegundo; }

        /// <summary>Compila shaders e gera texturas antes do primeiro poder, para o jogo não engasgar no meio da luta.</summary>
        public static void Aquecer(Vector3 ponto)
        {
            var e = Novo("Aquecimento_divino", ponto, .25f);
            Color quase = new Color(.003f, .003f, .003f, 1);
            for (int modo = 0; modo <= 4; modo++) Chao(e, modo, .05f, quase, quase).m.SetFloat(TId, .3f);
            for (int modo = 0; modo <= 2; modo++) Coluna(e, modo, Vector3.zero, .03f, .05f, quase, quase).m.SetFloat(TId, .3f);
            var egide = Malha(e, "Egide", Esfera(), "Egide", Vector3.zero, Vector3.one * .03f); egide.m.SetColor(CorId, quase); egide.m.SetColor(Cor2Id, quase);
            var fantasma = Malha(e, "Fantasma", Esfera(), "Fantasma", Vector3.zero, Vector3.one * .03f); fantasma.m.SetColor(CorId, quase);
            var onda = Mat(e, "Onda"); onda.SetFloat(ForcaId, .01f); OndaDivina.Criar(e, Vector3.forward, onda).Formar(.03f, .02f);
            RaioDivino.Criar(e, () => ponto, () => ponto + Vector3.up * .05f, quase, .02f, 0);
            Particulas(e, "P", new Jato { cor = quase, vida0 = .1f, vida1 = .1f, tam0 = .01f, tam1 = .01f }).Emit(1);
            Fumaca(); Pena(); Risco(); Estilhaco();
        }

        // ================================================================== ZEUS
        // A fase tem bloom, ACES e exposição alta: somas acima de ~1 viram branco. 'Ganho' segura todos os efeitos
        // na faixa em que a cor de cada deus ainda aparece; só os miolos (raio, faíscas) passam disso.
        const float Ganho = .3f, Clarao = .3f;
        static Color Hdr(Color c, float f) => new Color(c.r * f * Ganho, c.g * f * Ganho, c.b * f * Ganho, 1);
        static Color Cru(float r, float g, float b) => new Color(r * Ganho, g * Ganho, b * Ganho, 1);

        /// <summary>Espada erguida: arcos estalam na lâmina, faíscas correm para ela e o chão se eletriza.</summary>
        public static void ZeusCarga(Transform heroi, System.Func<Vector3> ponta, float duracao)
        {
            var e = Novo("Zeus_carga", Solo(heroi.position), duracao + .1f);
            Color azul = DivineLore.Cor(God.Zeus), claro = DivineLore.Claro(God.Zeus);
            var chao = Chao(e, 4, 2.6f, Hdr(azul, 2.4f), Hdr(claro, 3.2f));
            var luz = Luz(e, Vector3.up * 2.4f, claro, 9);
            var arcos = new RaioDivino[3];
            for (int i = 0; i < arcos.Length; i++)
            {
                arcos[i] = RaioDivino.Criar(e, ponta, () => ponta() + Random.onUnitSphere * Random.Range(.45f, 1.2f), Hdr(azul, 2.6f), .15f, 1);
                arcos[i].niveis = 4; arcos[i].intervalo = .035f; arcos[i].desvio = .24f;
            }
            var faiscas = Particulas(e, "Faiscas", new Jato { cor = Hdr(claro, 3.5f), fim = azul, vida0 = .22f, vida1 = .34f, vel0 = -7.5f, vel1 = -5f, tam0 = .03f, tam1 = .06f, estica = .07f });
            Bola(faiscas, 2.3f, true); Taxa(faiscas, 110);
            e.passo = (t, dt) =>
            {
                Vector3 p = ponta();
                chao.m.SetFloat(TId, 1 - t * .85f);
                luz.transform.position = p; luz.intensity = Clarao * (t * 11);
                faiscas.transform.position = p;
                foreach (var a in arcos) a.forca = .45f + t * .9f;
            };
        }

        /// <summary>O golpe desce: o raio principal cai na frente do herói e a onda de choque varre a área.</summary>
        public static void ZeusGolpe(Vector3 centro, float raio, Vector3 foco)
        {
            centro = Solo(centro); foco = Solo(foco);
            var e = Novo("Zeus_golpe", centro, 2.4f);
            Color azul = DivineLore.Cor(God.Zeus), claro = DivineLore.Claro(God.Zeus);
            Vector3 ceu = foco + new Vector3(Random.Range(-5f, 5f), 32, Random.Range(-5f, 5f));
            var tronco = RaioDivino.Criar(e, () => ceu, () => foco, Hdr(azul, 3.2f), 1.25f, 6);
            tronco.desvio = .075f; tronco.niveis = 7;
            var gemeo = RaioDivino.Criar(e, () => ceu + Vector3.right * 2, () => foco, Hdr(azul, 2.2f), .7f, 2);
            gemeo.desvio = .11f; gemeo.niveis = 6;
            Vector3 local = foco - centro;
            var coluna = Coluna(e, 0, local, .42f, 32, Hdr(azul, 1.3f), Hdr(claro, 2.2f)); coluna.m.SetFloat(RolagemId, 6); coluna.m.SetFloat(TId, .3f);
            var onda = Chao(e, 0, raio, Hdr(azul, 2.4f), Hdr(claro, 3.4f));
            var onda2 = Chao(e, 0, raio * .55f, Hdr(azul, 1.6f), Hdr(claro, 2.6f));
            var marca = Chao(e, 4, 4.2f, Hdr(azul, 2.4f), Hdr(claro, 3.4f), local);
            var luz = Luz(e, local + Vector3.up * 3, claro, 28);
            var faiscas = Particulas(e, "Faiscas", new Jato { cor = Hdr(claro, 4f), fim = azul, vida0 = .3f, vida1 = .8f, vel0 = 6, vel1 = 19, tam0 = .035f, tam1 = .085f, gravidade = 1.7f, estica = .06f }, local + Vector3.up * .15f);
            Meia(faiscas, .5f); faiscas.Emit(70);
            var poeira = Particulas(e, "Poeira", new Jato { tex = Fumaca(), cor = Hdr(azul, .35f), vida0 = .7f, vida1 = 1.3f, vel0 = 1.5f, vel1 = 5, tam0 = 1.2f, tam1 = 2.4f, crescer = 1.7f, maximo = 40 }, local + Vector3.up * .3f);
            Meia(poeira, 1f); poeira.Emit(14);
            e.passo = (t, dt) =>
            {
                float s = e.Segundos;
                // dois clarões: o golpe e o eco
                float clarao = Mathf.Max(Mathf.Exp(-s * 8.5f), s > .17f ? .75f * Mathf.Exp(-(s - .17f) * 10) : 0);
                tronco.forca = clarao * 1.35f; gemeo.forca = Mathf.Clamp01(clarao * 1.6f - .35f);
                coluna.m.SetFloat(ForcaId, clarao * 1.1f);
                luz.intensity = Clarao * (clarao * 60);
                onda.m.SetFloat(TId, Mathf.Clamp01(s / .62f)); onda2.m.SetFloat(TId, Mathf.Clamp01((s - .08f) / .5f));
                marca.m.SetFloat(TId, Mathf.Clamp01(s / 2.4f));
            };
        }

        /// <summary>Raio que cai num inimigo; arcos ficam estalando no corpo dele por um instante.</summary>
        public static void ZeusAlvo(Transform alvo, float escala)
        {
            if (!alvo) return;
            Vector3 pe = Solo(alvo.position);
            var e = Novo("Zeus_alvo", pe, 1.1f);
            Color azul = DivineLore.Cor(God.Zeus), claro = DivineLore.Claro(God.Zeus);
            Vector3 ceu = pe + new Vector3(Random.Range(-4f, 4f), 28, Random.Range(-4f, 4f));
            System.Func<Vector3> corpo = () => (alvo ? alvo.position : pe) + Vector3.up * 1.1f;
            var raio = RaioDivino.Criar(e, () => ceu, corpo, Hdr(azul, 3f), 1.05f * escala, 3);
            raio.desvio = .085f;
            var arcos = new RaioDivino[2];
            for (int i = 0; i < arcos.Length; i++)
            {
                arcos[i] = RaioDivino.Criar(e, () => corpo() + Random.insideUnitSphere * .5f, () => corpo() + new Vector3(Random.Range(-.8f, .8f), Random.Range(-1f, .9f), Random.Range(-.8f, .8f)), Hdr(azul, 2.4f), .14f, 1);
                arcos[i].niveis = 4; arcos[i].intervalo = .045f; arcos[i].desvio = .26f;
            }
            var anel = Chao(e, 0, 2.3f * escala, Hdr(azul, 2.4f), Hdr(claro, 4f));
            var marca = Chao(e, 4, 1.5f * escala, Hdr(azul, 2f), Hdr(claro, 3f));
            var luz = Luz(e, Vector3.up * 1.6f, claro, 10 * escala);
            var faiscas = Particulas(e, "Faiscas", new Jato { cor = Hdr(claro, 3.6f), fim = azul, vida0 = .22f, vida1 = .55f, vel0 = 4, vel1 = 13, tam0 = .03f, tam1 = .07f, gravidade = 1.6f, estica = .06f }, Vector3.up * 1.1f);
            Bola(faiscas, .3f); faiscas.Emit(Mathf.RoundToInt(26 * escala));
            e.passo = (t, dt) =>
            {
                float s = e.Segundos;
                float clarao = Mathf.Max(Mathf.Exp(-s * 9), s > .15f ? .6f * Mathf.Exp(-(s - .15f) * 12) : 0);
                raio.forca = clarao * 1.25f;
                float resto = (1 - t) * (1 - t);
                foreach (var a in arcos) a.forca = resto * 1.1f;
                luz.intensity = Clarao * (clarao * 22 + resto * 2.5f);
                anel.m.SetFloat(TId, Mathf.Clamp01(s / .45f)); marca.m.SetFloat(TId, t);
            };
        }

        // ================================================================== ARES
        /// <summary>Grito de guerra: labareda sobe do chão, anel de fogo e brasas.</summary>
        public static void AresErupcao(Transform heroi)
        {
            var e = Novo("Ares_erupcao", Solo(heroi.position), 1.5f);
            Color rubro = DivineLore.Cor(God.Ares), brasa = DivineLore.Claro(God.Ares);
            var fogo = Chao(e, 2, 4.2f, Hdr(rubro, 2.2f), Hdr(brasa, 2.1f));
            var onda = Chao(e, 0, 6f, Hdr(rubro, 2.6f), Hdr(brasa, 3.4f));
            var chama = Coluna(e, 1, Vector3.zero, 1.2f, 3.4f, Hdr(rubro, 2f), Hdr(brasa, 2.2f), 1.35f); chama.m.SetFloat(SuaveId, 1.1f);
            var chama2 = Coluna(e, 1, Vector3.zero, .7f, 4.4f, Hdr(rubro, 1.5f), Cru(1.9f, 1.1f, .35f), 1.2f); chama2.m.SetFloat(RolagemId, 1.5f);
            var luz = Luz(e, Vector3.up * 1.4f, brasa, 14);
            var brasas = Particulas(e, "Brasas", new Jato { cor = Hdr(brasa, 4.5f), fim = rubro, vida0 = .7f, vida1 = 1.7f, vel0 = 2.5f, vel1 = 9.5f, tam0 = .045f, tam1 = .12f, gravidade = -.22f, maximo = 200 }, Vector3.up * .2f);
            Cone(brasas, 38, 1f); brasas.Emit(90);
            var fumo = Particulas(e, "Fumo", new Jato { tex = Fumaca(), cor = Hdr(rubro, .4f), vida0 = .6f, vida1 = 1.1f, vel0 = 1.5f, vel1 = 4, tam0 = 1.3f, tam1 = 2.4f, gravidade = -.3f, crescer = 1.8f, maximo = 30 }, Vector3.up * .4f);
            Cone(fumo, 30, 1.2f); fumo.Emit(12);
            e.passo = (t, dt) =>
            {
                float s = e.Segundos;
                fogo.m.SetFloat(TId, Mathf.Clamp01(s / 1.2f)); onda.m.SetFloat(TId, Mathf.Clamp01(s / .5f));
                chama.m.SetFloat(TId, Mathf.Clamp01(s / .55f)); chama2.m.SetFloat(TId, Mathf.Clamp01(s / .42f));
                float sobe = Mathf.SmoothStep(.4f, 1.15f, Mathf.Clamp01(s / .28f));
                chama.tr.localScale = new Vector3(1.2f, 3.4f * sobe, 1.2f); chama2.tr.localScale = new Vector3(.7f, 4.4f * sobe, .7f);
                luz.intensity = Clarao * (30 * Mathf.Exp(-s * 3.6f));
            };
        }

        /// <summary>Enquanto a fúria dura: chão em brasa sob os pés, fagulhas subindo e luz vermelha.</summary>
        public static AuraDivina AresAura(Transform heroi)
        {
            var aura = NovaAura("Ares_furia", heroi);
            Color rubro = DivineLore.Cor(God.Ares), brasa = DivineLore.Claro(God.Ares);
            var fogo = Chao(aura, 2, 1.45f, Hdr(rubro, 2f), Hdr(brasa, 2f)); fogo.m.SetFloat(TId, .3f); fogo.m.SetFloat(ForcaId, 0);
            var luz = Luz(aura, new Vector3(0, 1.3f, .2f), new Color(1, .35f, .15f), 4.5f);
            var brasas = Particulas(aura, "Brasas", new Jato { cor = Hdr(brasa, 4f), fim = rubro, vida0 = .6f, vida1 = 1.15f, vel0 = 1.1f, vel1 = 2.7f, tam0 = .03f, tam1 = .075f, gravidade = -.12f, maximo = 120 }, Vector3.up * .1f);
            Cone(brasas, 7, .55f); Taxa(brasas, 34);
            var labaredas = Particulas(aura, "Labaredas", new Jato { tex = Fumaca(), cor = Hdr(rubro, .7f), fim = new Color(.4f, .05f, 0), vida0 = .35f, vida1 = .6f, vel0 = 1.2f, vel1 = 2.2f, tam0 = .35f, tam1 = .6f, gravidade = -.2f, crescer = .5f, maximo = 60 }, Vector3.up * .25f);
            Cone(labaredas, 10, .4f); Taxa(labaredas, 16);
            aura.passo = (f, dt) =>
            {
                fogo.m.SetFloat(ForcaId, f * .6f);
                luz.intensity = Clarao * (f * (2.4f + .7f * Mathf.Sin(Time.time * 17) + .4f * Mathf.Sin(Time.time * 31)));
            };
            return aura;
        }

        /// <summary>Golpe dado em fúria: brasas saltam do alvo.</summary>
        public static void AresGolpe(Vector3 ponto, Vector3 direcao)
        {
            var e = Novo("Ares_golpe", ponto, .9f);
            Color rubro = DivineLore.Cor(God.Ares), brasa = DivineLore.Claro(God.Ares);
            var brasas = Particulas(e, "Brasas", new Jato { cor = Hdr(brasa, 4.5f), fim = rubro, vida0 = .3f, vida1 = .75f, vel0 = 2.5f, vel1 = 8, tam0 = .035f, tam1 = .09f, gravidade = .6f, estica = .03f });
            Bola(brasas, .25f); brasas.Emit(16);
            if (direcao.sqrMagnitude > .01f) e.transform.rotation = Quaternion.LookRotation(direcao);
            var luz = Luz(e, Vector3.zero, brasa, 4);
            e.passo = (t, dt) => luz.intensity = Clarao * (6 * Mathf.Exp(-e.Segundos * 12));
        }

        // ================================================================== APOLO
        /// <summary>Pilar de luz do sol sobre o herói, selo solar no chão, anéis de cura subindo pelo corpo.</summary>
        public static void ApoloLuz(Transform heroi)
        {
            var e = Novo("Apolo_luz", heroi.position, 1.9f, heroi);
            Color ouro = DivineLore.Cor(God.Apollo), claro = DivineLore.Claro(God.Apollo);
            var pilar = Coluna(e, 0, Vector3.zero, 1.35f, 20, Hdr(ouro, .3f), Hdr(Color.Lerp(ouro, claro, .5f), .45f)); pilar.m.SetFloat(RolagemId, 1.1f);
            var miolo = Coluna(e, 0, Vector3.zero, .42f, 20, Hdr(ouro, .1f), Cru(.24f, .2f, .1f)); miolo.m.SetFloat(RolagemId, 2.2f); miolo.m.SetFloat(SuaveId, 2.4f);
            var selo = Chao(e, 1, 2.7f, Hdr(ouro, 2.2f), Hdr(claro, 3.4f)); selo.m.SetFloat(RaiosId, 12); selo.m.SetFloat(GiroId, .45f);
            var aneis = new Peca[2];
            for (int i = 0; i < aneis.Length; i++)
            {
                aneis[i] = Coluna(e, 0, Vector3.zero, .62f, .3f, Hdr(ouro, .7f), Hdr(claro, 1f)); aneis[i].m.SetFloat(SuaveId, .5f); aneis[i].m.SetFloat(TId, .3f);
            }
            var luz = Luz(e, Vector3.up * 1.7f, new Color(1, .86f, .55f), 9);
            var motas = Particulas(e, "Motas", new Jato { cor = Hdr(claro, 3.6f), fim = ouro, vida0 = .8f, vida1 = 1.3f, vel0 = 1.3f, vel1 = 3.3f, tam0 = .035f, tam1 = .09f, gravidade = -.05f, maximo = 160 }, Vector3.up * .05f);
            Cone(motas, 2, 1.15f); Taxa(motas, 62);
            var giro = motas.velocityOverLifetime; giro.enabled = true; giro.space = ParticleSystemSimulationSpace.Local;
            giro.x = 0; giro.y = 0; giro.z = 0; giro.orbitalX = 0; giro.orbitalY = 0; giro.orbitalZ = 2.2f; giro.radial = -.35f;
            var raios = Particulas(e, "Raios", new Jato { cor = Hdr(claro, 2.6f), fim = ouro, vida0 = .35f, vida1 = .6f, vel0 = -16, vel1 = -10, tam0 = .03f, tam1 = .055f, estica = .1f, maximo = 80 }, Vector3.up * 7f);
            Cone(raios, 1, 1.0f); Taxa(raios, 40);
            e.passo = (t, dt) =>
            {
                float s = e.Segundos;
                pilar.m.SetFloat(TId, t); miolo.m.SetFloat(TId, Mathf.Clamp01(t * 1.2f)); selo.m.SetFloat(TId, t);
                float vivo = Pulso(t, .1f, .55f);
                luz.intensity = Clarao * (4 * vivo);
                for (int i = 0; i < aneis.Length; i++)
                {
                    float k = Mathf.Repeat(s / .62f + i * .5f, 1);
                    aneis[i].tr.localPosition = Vector3.up * Mathf.Lerp(.05f, 2.15f, k);
                    float r = .62f * (1 - .25f * k);
                    aneis[i].tr.localScale = new Vector3(r, .3f, r);
                    aneis[i].m.SetFloat(ForcaId, Mathf.Sin(k * Mathf.PI) * vivo * (s < 1.15f ? 1 : 0));
                }
                if (s > 1.05f) { Taxa(motas, 0); Taxa(raios, 0); }
            };
        }

        /// <summary>Cura passiva de Apolo (ao subir de nota): um sopro de luz dourada.</summary>
        public static void ApoloCentelha(Transform heroi)
        {
            var e = Novo("Apolo_centelha", heroi.position, 1.1f, heroi);
            Color ouro = DivineLore.Cor(God.Apollo), claro = DivineLore.Claro(God.Apollo);
            var selo = Chao(e, 0, 1.8f, Hdr(ouro, 2f), Hdr(claro, 3f));
            var motas = Particulas(e, "Motas", new Jato { cor = Hdr(claro, 3.4f), fim = ouro, vida0 = .6f, vida1 = 1f, vel0 = 1.4f, vel1 = 3f, tam0 = .035f, tam1 = .08f, gravidade = -.05f }, Vector3.up * .1f);
            Cone(motas, 3, .7f); motas.Emit(26);
            var luz = Luz(e, Vector3.up * 1.4f, new Color(1, .86f, .55f), 6);
            e.passo = (t, dt) => { selo.m.SetFloat(TId, Mathf.Clamp01(e.Segundos / .5f)); luz.intensity = Clarao * (6 * Mathf.Exp(-e.Segundos * 5)); };
        }

        // ================================================================== HERMES
        /// <summary>Arranque: rajadas de vento esticadas para trás, penas soltas no caminho e um anel no ponto de partida.</summary>
        public static void HermesVento(Transform heroi, Vector3 direcao, float duracao)
        {
            Color verde = DivineLore.Cor(God.Hermes), claro = DivineLore.Claro(God.Hermes);
            var partida = Novo("Hermes_partida", Solo(heroi.position), .6f);
            var anel = Chao(partida, 0, 2.6f, Hdr(verde, 2.2f), Hdr(claro, 3.4f));
            partida.passo = (t, dt) => anel.m.SetFloat(TId, Mathf.Clamp01(partida.Segundos / .42f));

            var e = Novo("Hermes_vento", heroi.position, duracao + 1.9f, heroi);
            var riscos = Particulas(e, "Riscos", new Jato { cor = Hdr(claro, 2.8f), fim = verde, vida0 = .14f, vida1 = .28f, vel0 = 0, vel1 = 0, tam0 = .025f, tam1 = .06f, estica = .1f, maximo = 200 }, Vector3.up * .95f);
            var sh = riscos.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.1f, 1.7f, .8f);
            var vel = riscos.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            Vector3 tras = -direcao.normalized * 9;
            vel.x = tras.x; vel.y = tras.y; vel.z = tras.z;
            Taxa(riscos, 190);
            var penas = Particulas(e, "Penas", new Jato { tex = Pena(), cor = Hdr(claro, 1.5f), fim = verde, vida0 = 1.1f, vida1 = 1.8f, vel0 = .4f, vel1 = 1.6f, tam0 = .16f, tam1 = .3f, gravidade = .16f, crescer = 1f, maximo = 40 }, Vector3.up * 1.0f);
            Bola(penas, .45f);
            var main = penas.main; main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var gira = penas.rotationOverLifetime; gira.enabled = true; gira.z = new ParticleSystem.MinMaxCurve(-3.5f, 3.5f);
            Taxa(penas, 30);
            var luz = Luz(e, Vector3.up * 1.1f, verde, 6);
            e.passo = (t, dt) =>
            {
                float s = e.Segundos;
                if (s > duracao) { Taxa(riscos, 0); Taxa(penas, 0); }
                luz.intensity = Clarao * (7 * Mathf.Clamp01(1 - s / (duracao + .25f)));
            };
        }

        /// <summary>Rastro fantasma: uma cópia do herói, congelada na pose do instante, que brilha e some.</summary>
        public static void Fantasma(SkinnedMeshRenderer[] peles, MeshFilter[] rigidos, Color cor, float duracao)
        {
            var e = Novo("Hermes_fantasma", Vector3.zero, duracao);
            var m = Mat(e, "Fantasma"); m.SetColor(CorId, cor);
            void Peca(Mesh malha, Transform origem, Vector3 escala)
            {
                var go = new GameObject("Fantasma") { layer = 2 };
                go.transform.SetParent(e.transform, false);
                go.transform.SetPositionAndRotation(origem.position, origem.rotation); go.transform.localScale = escala;
                go.AddComponent<MeshFilter>().sharedMesh = malha;
                var mr = go.AddComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
                var lista = new Material[Mathf.Max(1, malha.subMeshCount)];
                for (int i = 0; i < lista.Length; i++) lista[i] = m;
                mr.sharedMaterials = lista;
            }
            if (peles != null)
                foreach (var smr in peles)
                {
                    if (!smr || !smr.enabled || !smr.gameObject.activeInHierarchy || !smr.sharedMesh) continue;
                    var malha = e.Dono(new Mesh { name = "Fantasma" });
                    smr.BakeMesh(malha, true);
                    Peca(malha, smr.transform, Vector3.one);
                }
            if (rigidos != null)
                foreach (var mf in rigidos)
                    if (mf && mf.sharedMesh && mf.gameObject.activeInHierarchy) Peca(mf.sharedMesh, mf.transform, mf.transform.lossyScale);
            e.passo = (t, dt) => m.SetFloat(ForcaId, (1 - t) * (1 - t));
        }

        /// <summary>Cortes de vento num inimigo atravessado pelo arranque.</summary>
        public static void HermesCorte(Vector3 ponto)
        {
            var e = Novo("Hermes_corte", ponto, .7f);
            Color verde = DivineLore.Cor(God.Hermes), claro = DivineLore.Claro(God.Hermes);
            var cortes = Particulas(e, "Cortes", new Jato { tex = Risco(), cor = Hdr(claro, 3.4f), fim = verde, vida0 = .16f, vida1 = .26f, tam0 = 1, tam1 = 1, crescer = 1.5f, maximo = 8 });
            var main = cortes.main; main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(2.4f, 3.4f); main.startSizeY = new ParticleSystem.MinMaxCurve(.35f, .55f); main.startSizeZ = 1;
            main.startRotation = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f); main.startSpeed = 0;
            Bola(cortes, .2f); cortes.Emit(3);
            var faiscas = Particulas(e, "Faiscas", new Jato { cor = Hdr(claro, 3.2f), fim = verde, vida0 = .2f, vida1 = .45f, vel0 = 3, vel1 = 10, tam0 = .03f, tam1 = .06f, gravidade = .8f, estica = .06f });
            Bola(faiscas, .25f); faiscas.Emit(14);
            var luz = Luz(e, Vector3.zero, verde, 5);
            e.passo = (t, dt) => luz.intensity = Clarao * (8 * Mathf.Exp(-e.Segundos * 10));
        }

        /// <summary>Vento nos pés depois de uma esquiva (bônus de dano de Hermes).</summary>
        public static void HermesPassos(Transform heroi, float duracao)
        {
            var e = Novo("Hermes_passos", heroi.position, duracao + .6f, heroi);
            Color verde = DivineLore.Cor(God.Hermes), claro = DivineLore.Claro(God.Hermes);
            var fios = Particulas(e, "Fios", new Jato { cor = Hdr(claro, 2.4f), fim = verde, vida0 = .3f, vida1 = .55f, vel0 = .4f, vel1 = 1.4f, tam0 = .03f, tam1 = .07f, gravidade = -.1f, maximo = 80 }, Vector3.up * .12f);
            Cone(fios, 25, .35f); Taxa(fios, 42);
            e.passo = (t, dt) => { if (e.Segundos > duracao) Taxa(fios, 0); };
        }

        // ================================================================== ATENA
        /// <summary>A égide: cúpula de hexágonos em volta do herói. 'impacto' acende a ondulação no ponto atingido.</summary>
        public static AuraDivina AtenaEgide(Transform heroi, out System.Action<Vector3> impacto)
        {
            var aura = NovaAura("Atena_egide", heroi);
            aura.entra = .12f; aura.sai = .38f;
            Color prata = DivineLore.Cor(God.Athena), ouro = DivineLore.Claro(God.Athena);
            const float raio = 1.7f;
            var cupula = Malha(aura, "Cupula", Esfera(), "Egide", Vector3.up * 1.0f, Vector3.one * raio);
            cupula.m.SetColor(CorId, Hdr(new Color(.45f, .62f, 1f), 1.25f)); cupula.m.SetColor(Cor2Id, Hdr(new Color(1f, .8f, .4f), 1.7f)); cupula.m.SetFloat(MalhaId, 20); cupula.m.SetFloat(TId, 0); cupula.m.SetFloat(ForcaId, 0);
            var selo = Chao(aura, 1, 2.1f, Hdr(prata, 1.7f), Hdr(ouro, 2.6f)); selo.m.SetFloat(RaiosId, 8); selo.m.SetFloat(GiroId, .22f); selo.m.SetFloat(TId, .4f); selo.m.SetFloat(ForcaId, 0);
            var luz = Luz(aura, new Vector3(0, 1.4f, .3f), prata, 6);
            float nasce = 0, idade = 2; Vector3 direcao = Vector3.forward; float clarao = 0;
            aura.passo = (f, dt) =>
            {
                nasce = Mathf.Min(1, nasce + dt / .32f); idade += dt / .55f; clarao = Mathf.Max(0, clarao - dt * 4);
                cupula.m.SetFloat(TId, nasce);
                cupula.m.SetFloat(ForcaId, f * (aura.Saindo ? 1.6f : 1f));
                cupula.m.SetVector(ImpactoId, new Vector4(direcao.x, direcao.y, direcao.z, idade));
                // ao acabar, a cúpula se abre e some
                cupula.tr.localScale = Vector3.one * raio * (aura.Saindo ? Mathf.Lerp(1.3f, 1f, f) : 1);
                selo.m.SetFloat(ForcaId, f * .85f);
                luz.intensity = Clarao * (f * 2.2f + clarao * 9);
            };
            impacto = mundo =>
            {
                if (!cupula.tr) return;
                direcao = cupula.tr.InverseTransformDirection(mundo.sqrMagnitude > .001f ? mundo.normalized : Vector3.forward);
                idade = 0; clarao = 1;
            };
            return aura;
        }

        /// <summary>O escudo se ergue: anel prateado no chão e cacos de luz.</summary>
        public static void AtenaSurgir(Transform heroi)
        {
            var e = Novo("Atena_surgir", Solo(heroi.position), 1.2f);
            Color prata = DivineLore.Cor(God.Athena), ouro = DivineLore.Claro(God.Athena);
            var anel = Chao(e, 0, 3.8f, Hdr(prata, 2.4f), Hdr(ouro, 3.6f));
            var cacos = Particulas(e, "Cacos", new Jato { tex = Estilhaco(), cor = Hdr(ouro, 2.6f), fim = prata, vida0 = .5f, vida1 = 1f, vel0 = 1.5f, vel1 = 4.5f, tam0 = .06f, tam1 = .14f, gravidade = -.1f, maximo = 60 }, Vector3.up * .2f);
            var sh = cacos.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.6f; sh.radiusThickness = .1f; sh.rotation = new Vector3(-90, 0, 0);
            var sobe = cacos.velocityOverLifetime; sobe.enabled = true; sobe.space = ParticleSystemSimulationSpace.World; sobe.x = 0; sobe.z = 0; sobe.y = 2.4f;
            var main = cacos.main; main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); main.startSpeed = new ParticleSystem.MinMaxCurve(-.6f, .4f);
            cacos.Emit(34);
            var luz = Luz(e, Vector3.up * 1.4f, prata, 10);
            e.passo = (t, dt) => { anel.m.SetFloat(TId, Mathf.Clamp01(e.Segundos / .5f)); luz.intensity = Clarao * (14 * Mathf.Exp(-e.Segundos * 5)); };
        }

        /// <summary>A égide se desfaz em cacos.</summary>
        public static void AtenaQuebrar(Vector3 centro)
        {
            var e = Novo("Atena_quebrar", centro, 1.1f);
            Color prata = DivineLore.Cor(God.Athena), ouro = DivineLore.Claro(God.Athena);
            var cacos = Particulas(e, "Cacos", new Jato { tex = Estilhaco(), cor = Hdr(ouro, 2.4f), fim = prata, vida0 = .4f, vida1 = .9f, vel0 = 1.5f, vel1 = 5, tam0 = .07f, tam1 = .16f, gravidade = .9f, maximo = 80 }, Vector3.up * 1f);
            Bola(cacos, 1.6f, true);
            var main = cacos.main; main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var gira = cacos.rotationOverLifetime; gira.enabled = true; gira.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
            cacos.Emit(46);
        }

        /// <summary>Golpe aparado (égide ou esquiva no tempo certo): selo de luz de frente para o atacante e faíscas.</summary>
        public static void AtenaAparo(Vector3 ponto, Vector3 paraOAtacante)
        {
            var e = Novo("Atena_aparo", ponto, .5f);
            Color prata = DivineLore.Cor(God.Athena), ouro = DivineLore.Claro(God.Athena);
            if (paraOAtacante.sqrMagnitude > .001f) e.transform.rotation = Quaternion.FromToRotation(Vector3.up, paraOAtacante.normalized);
            var selo = Chao(e, 1, 1.05f, Hdr(prata, 2.6f), Hdr(ouro, 4f)); selo.m.SetFloat(RaiosId, 8); selo.m.SetFloat(GiroId, 2f);
            var faiscas = Particulas(e, "Faiscas", new Jato { cor = Hdr(ouro, 3.6f), fim = prata, vida0 = .2f, vida1 = .5f, vel0 = 3, vel1 = 11, tam0 = .03f, tam1 = .07f, gravidade = 1.2f, estica = .06f });
            Cone(faiscas, 50, .2f); faiscas.Emit(22);
            var luz = Luz(e, Vector3.up * .2f, ouro, 7);
            e.passo = (t, dt) => { selo.m.SetFloat(TId, .12f + t * .88f); selo.tr.localScale = Vector3.one * 2.1f * Mathf.Lerp(.7f, 1.15f, t); luz.intensity = Clarao * (12 * Mathf.Exp(-e.Segundos * 9)); };
        }

        // ================================================================== POSEIDON
        /// <summary>Preparo: a água brota em volta dos pés e gotas sobem.</summary>
        public static void PoseidonCarga(Transform heroi, float duracao)
        {
            var e = Novo("Poseidon_carga", Solo(heroi.position), duracao + .25f);
            Color mar = DivineLore.Cor(God.Poseidon), claro = DivineLore.Claro(God.Poseidon);
            var agua = Chao(e, 3, 2.6f, Hdr(mar, 1.6f), Hdr(claro, 2.6f));
            var gotas = Particulas(e, "Gotas", new Jato { cor = Hdr(claro, 2.8f), fim = mar, vida0 = .3f, vida1 = .55f, vel0 = 2, vel1 = 5.5f, tam0 = .04f, tam1 = .08f, gravidade = .6f, estica = .02f }, Vector3.up * .05f);
            Cone(gotas, 12, 1.7f); Taxa(gotas, 120);
            var luz = Luz(e, Vector3.up * 1f, mar, 7);
            e.passo = (t, dt) => { agua.m.SetFloat(TId, t * .42f); luz.intensity = Clarao * (t * 6); if (e.Segundos > duracao) Taxa(gotas, 0); };
        }

        /// <summary>A maré: parede de água em anel que corre para fora, jato no centro, lâmina d'água e respingos.</summary>
        public static void PoseidonMare(Vector3 centro, float raio, Vector3 frente)
        {
            centro = Solo(centro);
            var e = Novo("Poseidon_mare", centro, 2.6f);
            Color mar = DivineLore.Cor(God.Poseidon), claro = DivineLore.Claro(God.Poseidon);
                        var agua = Chao(e, 3, raio * 1.15f, Hdr(mar, 1.5f), Hdr(claro, 2f));
            var materialOnda = Mat(e, "Onda"); materialOnda.SetColor(CorId, new Color(.01f, .15f, .26f)); materialOnda.SetColor(Cor2Id, new Color(.06f, .46f, .6f)); materialOnda.SetFloat(SementeId, Random.value * 30);
            var onda = OndaDivina.Criar(e, frente, materialOnda);
            var jato = Coluna(e, 2, Vector3.zero, .8f, 5.6f, Hdr(mar, 1.3f), Cru(.8f, 1.25f, 1.35f), 1.55f);
            var jato2 = Coluna(e, 2, Vector3.zero, .55f, 7f, Hdr(mar, 1.3f), Cru(1f, 1.4f, 1.5f), 1.3f); jato2.m.SetFloat(RolagemId, 1.6f);
            var luz = Luz(e, Vector3.up * 2.2f, mar, 18);
            var respingo = Particulas(e, "Respingo", new Jato { cor = Hdr(claro, 3.2f), fim = mar, vida0 = .45f, vida1 = 1f, tam0 = .05f, tam1 = .12f, gravidade = 1.7f, estica = .02f, maximo = 500 });
            var nevoa = Particulas(e, "Nevoa", new Jato { tex = Fumaca(), cor = Hdr(claro, .4f), fim = mar, vida0 = .9f, vida1 = 1.7f, tam0 = 1.4f, tam1 = 2.8f, crescer = 1.7f, maximo = 90 });
            var chafariz = Particulas(e, "Chafariz", new Jato { cor = Hdr(claro, 3.2f), fim = mar, vida0 = .7f, vida1 = 1.3f, vel0 = 6, vel1 = 13, tam0 = .06f, tam1 = .13f, gravidade = 2.1f, estica = .02f, maximo = 120 }, Vector3.up * .3f);
            Cone(chafariz, 24, .6f); chafariz.Emit(70);
            var gota = new ParticleSystem.EmitParams(); var puff = new ParticleSystem.EmitParams();
            e.passo = (t, dt) =>
            {
                float s = e.Segundos;
                float k = Mathf.Clamp01(s / .78f);
                float R = Mathf.Lerp(.8f, raio * 1.03f, 1 - Mathf.Pow(1 - k, 2.1f));
                float H = 2.7f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Clamp01(s / .92f))), .75f);
                onda.forca = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.6f, .92f, s));
                onda.Formar(R, Mathf.Max(.01f, H));
                if (s < .85f && dt > 0)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        float a = Random.value * Mathf.PI * 2; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        float h = onda.Altura(dir, H);
                        gota.position = centro + dir * (R + h * .3f) + Vector3.up * h * Random.Range(.6f, 1f);
                        gota.velocity = dir * Random.Range(2f, 6.5f) + Vector3.up * Random.Range(1.5f, 6f);
                        respingo.Emit(gota, 1);
                    }
                    if (Random.value < .6f)
                    {
                        float a = Random.value * Mathf.PI * 2; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        puff.position = centro + dir * R + Vector3.up * Random.Range(.3f, 1.2f);
                        puff.velocity = dir * Random.Range(.5f, 2f) + Vector3.up * Random.Range(.2f, .9f);
                        nevoa.Emit(puff, 1);
                    }
                }
                agua.m.SetFloat(TId, Mathf.Clamp01(s / 2.6f));
                jato.m.SetFloat(TId, Mathf.Clamp01(s / 1.0f)); jato2.m.SetFloat(TId, Mathf.Clamp01(s / .8f));
                float sobe = Mathf.SmoothStep(.25f, 1, Mathf.Clamp01(s / .2f));
                jato.tr.localScale = new Vector3(.8f, 5.6f * sobe, .8f); jato2.tr.localScale = new Vector3(.55f, 7f * sobe, .55f);
                luz.intensity = Clarao * (26 * Mathf.Exp(-s * 3.2f));
            };
        }

        /// <summary>Jato d'água que ergue um inimigo.</summary>
        public static void PoseidonGeiser(Vector3 ponto, float escala)
        {
            var e = Novo("Poseidon_geiser", Solo(ponto), 1.5f);
            Color mar = DivineLore.Cor(God.Poseidon), claro = DivineLore.Claro(God.Poseidon);
            var jato = Coluna(e, 2, Vector3.zero, .6f * escala, 4f * escala, Hdr(mar, 1.3f), Cru(.9f, 1.3f, 1.4f), 1.7f);
            var poca = Chao(e, 3, 2.1f * escala, Hdr(mar, 1.5f), Hdr(claro, 2.6f));
            var gotas = Particulas(e, "Gotas", new Jato { cor = Hdr(claro, 3.2f), fim = mar, vida0 = .5f, vida1 = 1.1f, vel0 = 4.5f, vel1 = 10.5f, tam0 = .05f, tam1 = .12f, gravidade = 2.2f, estica = .02f }, Vector3.up * .2f);
            Cone(gotas, 20, .35f * escala); gotas.Emit(Mathf.RoundToInt(36 * escala));
            var nevoa = Particulas(e, "Nevoa", new Jato { tex = Fumaca(), cor = Hdr(claro, .5f), fim = mar, vida0 = .7f, vida1 = 1.2f, vel0 = .6f, vel1 = 2, tam0 = 1f, tam1 = 1.9f, crescer = 1.6f, maximo = 20 }, Vector3.up * .4f);
            Meia(nevoa, .6f); nevoa.Emit(7);
            var luz = Luz(e, Vector3.up * 1.3f, mar, 8);
            e.passo = (t, dt) =>
            {
                float s = e.Segundos;
                jato.m.SetFloat(TId, Mathf.Clamp01(s / .85f)); poca.m.SetFloat(TId, Mathf.Clamp01(s / 1.5f));
                float sobe = Mathf.SmoothStep(.2f, 1, Mathf.Clamp01(s / .16f));
                jato.tr.localScale = new Vector3(.6f * escala, 4f * escala * sobe, .6f * escala);
                luz.intensity = Clarao * (12 * Mathf.Exp(-s * 5));
            };
        }

        // ================================================================== geral
        /// <summary>Faíscas na cor do deus num inimigo atingido por dano divino.</summary>
        public static void Toque(God deus, Vector3 ponto)
        {
            var e = Novo("Toque_divino", ponto, .7f);
            Color cor = DivineLore.Cor(deus), claro = DivineLore.Claro(deus);
            var faiscas = Particulas(e, "Faiscas", new Jato { cor = Hdr(claro, 3.4f), fim = cor, vida0 = .2f, vida1 = .5f, vel0 = 3, vel1 = 10, tam0 = .03f, tam1 = .07f, gravidade = 1.2f, estica = .06f });
            Bola(faiscas, .25f); faiscas.Emit(14);
            var clarao = Particulas(e, "Clarao", new Jato { cor = Hdr(cor, 2.2f), vida0 = .14f, vida1 = .18f, tam0 = 1.6f, tam1 = 2f, crescer = 1.6f, maximo = 2 });
            clarao.Emit(1);
        }
    }
}
