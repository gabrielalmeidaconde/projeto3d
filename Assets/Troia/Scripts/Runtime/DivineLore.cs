using UnityEngine;

namespace Troia
{
    /// <summary>
    /// Identidade de cada deus na tela: nome do poder, cores, tempo de conjuração e o símbolo desenhado para o HUD.
    /// As regras (dano, custo, duração) continuam em <see cref="DivineFavor"/> e nos BlessingSO.
    /// </summary>
    public static class DivineLore
    {
        static readonly string[] Nomes = { "ZEUS", "ARES", "APOLO", "HERMES", "ATENA", "POSEIDON" };
        static readonly string[] Poderes = { "RAIO DE ZEUS", "FÚRIA DE ARES", "LUZ DE APOLO", "PASSO DE HERMES", "ÉGIDE DE ATENA", "MARÉ DE POSEIDON" };
        static readonly string[] Resumos = {
            "Raios caem em volta do herói.", "Ataca mais rápido e mais forte.", "Cura sob a luz do sol.",
            "Atravessa os inimigos cortando.", "Nenhum golpe passa pelo escudo.", "A maré ergue os inimigos no ar." };
        static readonly Color[] Cores = {
            new Color(.42f, .72f, 1f), new Color(1f, .22f, .1f), new Color(1f, .78f, .28f),
            new Color(.38f, 1f, .8f), new Color(.68f, .78f, 1f), new Color(.14f, .78f, .9f) };
        static readonly Color[] Claros = {
            new Color(.86f, .94f, 1f), new Color(1f, .62f, .16f), new Color(1f, .96f, .78f),
            new Color(.88f, 1f, .96f), new Color(1f, .9f, .62f), new Color(.78f, 1f, 1f) };
        // segundos entre apertar Q e o poder cair (o gesto do herói chega junto) e quanto o gesto prende o herói
        static readonly float[] Atrasos = { .33f, 0f, 0f, 0f, 0f, .28f };
        static readonly float[] Travas = { .72f, .58f, .8f, 0f, .42f, .7f };
        // instante do gesto em que a pose chega ao auge (para os poderes sem atraso)
        static readonly float[] Auges = { .33f, .16f, .3f, .1f, .12f, .28f };

        public static string Nome(God g) => Nomes[(int)g];
        public static string Poder(God g) => Poderes[(int)g];
        public static string Resumo(God g) => Resumos[(int)g];
        public static Color Cor(God g) => Cores[(int)g];
        public static Color Claro(God g) => Claros[(int)g];
        public static float Atraso(God g) => Atrasos[(int)g];
        public static float Trava(God g) => Travas[(int)g];
        public static float Auge(God g) => Auges[(int)g];
        public static string Clipe(God g) => "bencao_" + Nomes[(int)g].ToLowerInvariant();

        // ------------------------------------------------------------------ símbolos
        static readonly Sprite[] simbolos = new Sprite[6];
        static Sprite vinheta;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Limpar() { for (int i = 0; i < simbolos.Length; i++) simbolos[i] = null; vinheta = null; }

        /// <summary>Símbolo branco do deus (raio, espada, sol, asa, escudo, tridente), para tingir no HUD.</summary>
        public static Sprite Simbolo(God g)
        {
            int i = (int)g;
            if (!simbolos[i]) simbolos[i] = Desenhar(g);
            return simbolos[i];
        }

        /// <summary>Moldura escura no centro e cheia nas bordas: tinge a tela enquanto um poder está ativo.</summary>
        public static Sprite Vinheta()
        {
            if (vinheta) return vinheta;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Vinheta_divina", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + .5f) / n * 2 - 1, v = (y + .5f) / n * 2 - 1;
                    float r = Mathf.Sqrt(u * u * .8f + v * v);
                    float a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1.25f, r));
                    px[y * n + x] = new Color(1, 1, 1, a * a);
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            vinheta = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100);
            return vinheta;
        }

        static Sprite Desenhar(God g)
        {
            const int n = 96, amostras = 3;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Simbolo_" + Nome(g), wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int dentro = 0;
                    for (int sy = 0; sy < amostras; sy++)
                        for (int sx = 0; sx < amostras; sx++)
                        {
                            var p = new Vector2((x + (sx + .5f) / amostras) / n * 2 - 1, (y + (sy + .5f) / amostras) / n * 2 - 1);
                            if (Forma(g, p)) dentro++;
                        }
                    px[y * n + x] = new Color(1, 1, 1, dentro / (float)(amostras * amostras));
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100);
        }

        static readonly Vector2[] RaioPts = { new Vector2(-.1f, .95f), new Vector2(.5f, .95f), new Vector2(.14f, .2f), new Vector2(.52f, .2f), new Vector2(-.34f, -.96f), new Vector2(-.06f, -.1f), new Vector2(-.48f, -.1f) };
        static readonly Vector2[] LaminaPts = { new Vector2(-.14f, -.22f), new Vector2(-.14f, .58f), new Vector2(0, .97f), new Vector2(.14f, .58f), new Vector2(.14f, -.22f) };

        static bool Forma(God g, Vector2 p)
        {
            switch (g)
            {
                case God.Zeus: return Poligono(RaioPts, p);
                case God.Ares:
                    return Poligono(LaminaPts, p) || Ret(p, -.52f, -.38f, .52f, -.22f) || Ret(p, -.08f, -.8f, .08f, -.38f) || p.x * p.x + (p.y + .84f) * (p.y + .84f) < .14f * .14f;
                case God.Apollo:
                {
                    float r = p.magnitude;
                    if (r < .36f) return true;
                    float a = Mathf.Atan2(p.y, p.x), s = Mathf.Abs(Mathf.Repeat(a / (Mathf.PI * 2) * 8, 1) - .5f) * 2;   // 1 no eixo do raio
                    return r > .48f && r < .48f + .46f * Mathf.Clamp01((s - .45f) / .55f);
                }
                case God.Hermes:
                    return Capsula(p, new Vector2(-.62f, -.5f), new Vector2(.72f, .78f), .15f) || Capsula(p, new Vector2(-.62f, -.5f), new Vector2(.86f, .3f), .14f)
                        || Capsula(p, new Vector2(-.62f, -.5f), new Vector2(.74f, -.2f), .13f) || Capsula(p, new Vector2(-.62f, -.5f), new Vector2(.36f, -.56f), .12f)
                        || (p - new Vector2(-.62f, -.5f)).magnitude < .24f;
                case God.Athena:
                {
                    float r = p.magnitude;
                    return (r > .74f && r < .94f) || (r > .42f && r < .54f) || r < .18f;
                }
                default:
                    return Ret(p, -.07f, -.96f, .07f, .24f) || Ret(p, -.58f, .14f, .58f, .3f) || Dente(p, 0, 1f) || Dente(p, -.5f, .9f) || Dente(p, .5f, .9f);
            }
        }
        static bool Dente(Vector2 p, float x, float topo)
        {
            if (Mathf.Abs(p.x - x) > .085f || p.y < .28f) return false;
            float ponta = topo - .26f;
            return p.y < ponta || (p.y < topo && Mathf.Abs(p.x - x) < .085f * (topo - p.y) / .26f);
        }
        static bool Ret(Vector2 p, float x0, float y0, float x1, float y1) => p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1;
        static bool Capsula(Vector2 p, Vector2 a, Vector2 b, float raio)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            // a pena afina para a ponta
            return (p - (a + ab * t)).magnitude < raio * Mathf.Lerp(1f, .45f, t);
        }
        static bool Poligono(Vector2[] v, Vector2 p)
        {
            bool dentro = false;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
                if ((v[i].y > p.y) != (v[j].y > p.y) && p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x) dentro = !dentro;
            return dentro;
        }
    }
}
