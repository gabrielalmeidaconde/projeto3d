using System.Collections.Generic;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>Área caminhável (praça retangular ou rua em polilinha) ou área reservada.</summary>
    public class Area
    {
        public string Name;
        public bool Paved = true;
        public bool Frontage = true;   // gera fileira de casas nas bordas
        public bool Shops;             // permite lojas com toldo (só em áreas largas)
        public bool Citadel;           // fica no platô da cidadela
        public float FireChance = 0.15f;

        public bool IsRect;
        public Vector2 Center, Size;
        public Vector2[] Pts;
        public float Width;

        public static Area Rect(string n, float cx, float cz, float sx, float sz) =>
            new Area { Name = n, IsRect = true, Center = new Vector2(cx, cz), Size = new Vector2(sx, sz) };

        public static Area Street(string n, float w, params Vector2[] pts) =>
            new Area { Name = n, IsRect = false, Pts = pts, Width = w };

        /// <summary>Distância com sinal (negativa por dentro).</summary>
        public float Sdf(Vector2 p)
        {
            if (IsRect)
            {
                var d = new Vector2(Mathf.Abs(p.x - Center.x) - Size.x * 0.5f, Mathf.Abs(p.y - Center.y) - Size.y * 0.5f);
                return Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
            }
            float best = float.MaxValue;
            for (int i = 0; i < Pts.Length - 1; i++) best = Mathf.Min(best, SegDist(p, Pts[i], Pts[i + 1]));
            return best - Width * 0.5f;
        }

        public static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Bordas (a, b, normal para fora) onde ficam as fachadas.</summary>
        public IEnumerable<(Vector2 a, Vector2 b, Vector2 outward)> Edges()
        {
            if (IsRect)
            {
                float x0 = Center.x - Size.x * 0.5f, x1 = Center.x + Size.x * 0.5f;
                float z0 = Center.y - Size.y * 0.5f, z1 = Center.y + Size.y * 0.5f;
                yield return (new Vector2(x0, z0), new Vector2(x1, z0), Vector2.down);
                yield return (new Vector2(x1, z1), new Vector2(x0, z1), Vector2.up);
                yield return (new Vector2(x0, z1), new Vector2(x0, z0), Vector2.left);
                yield return (new Vector2(x1, z0), new Vector2(x1, z1), Vector2.right);
                yield break;
            }
            float h = Width * 0.5f;
            for (int i = 0; i < Pts.Length - 1; i++)
            {
                Vector2 a = Pts[i], b = Pts[i + 1];
                Vector2 dir = (b - a).normalized;
                var perp = new Vector2(-dir.y, dir.x);
                yield return (a + perp * h, b + perp * h, perp);
                yield return (b - perp * h, a - perp * h, -perp);
            }
        }
    }

    /// <summary>
    /// Planta da Fase 1 - "A Noite da Invasão".
    /// Eixo X = leste, Z = norte. Cidade baixa em y=0; Pérgamo (cidadela) num platô em y=8.
    ///
    /// Modo de arenas com ondas: cada praça é uma arena trancada por barreiras de fogo até
    /// o jogador limpar todas as ondas; as ruas entre elas são corredores de transição.
    /// Arenas: 1 Ágora (Cavalo) -> 2 Mercado -> 3 Portas Ceias -> 4 Praça da Fonte
    ///         -> [Rampa] -> 5 Pátio da Cidadela (Templo de Atena) -> 6 Altar de Zeus (chefe)
    /// </summary>
    public static class TroiaLayout
    {
        public const float Rx = 105f, Rz = 135f;                 // elipse da muralha externa
        public static readonly Vector2 CitadelC = new Vector2(0f, 90f);
        public const float CitadelR = 40f, CitadelH = 8f;
        public const float RampZ0 = 20f, RampZ1 = 50f;
        public const float TerrainSize = 640f, TerrainBaseY = -20f, TerrainHeight = 100f;
        public static readonly Vector3 TerrainPos = new Vector3(-320f, TerrainBaseY, -360f);
        public const float SeaLevel = -3.5f;

        public static readonly List<Area> Walk = new List<Area>();
        public static readonly List<Area> Reserved = new List<Area>();

        /// <summary>Caminho principal do jogador (para decidir onde vale gastar luzes).</summary>
        public static readonly Vector2[] MainRoute =
        {
            new Vector2(-55, -45), new Vector2(-55, -62), new Vector2(-50, -76), new Vector2(-36, -88),
            new Vector2(-22, -104), new Vector2(0, -117), new Vector2(0, -30), new Vector2(0, 20),
            new Vector2(0, 50), new Vector2(0, 73), new Vector2(14, 97)
        };

        static TroiaLayout()
        {
            // ---------------- cidade baixa
            Walk.Add(new Area
            {
                Name = "Agora", IsRect = true, Center = new Vector2(-55, -45), Size = new Vector2(44, 36),
                Shops = true, FireChance = 0.03f
            });
            Walk.Add(Area.Street("Beco_Agora_Mercado", 6.5f, new Vector2(-55, -62), new Vector2(-50, -76), new Vector2(-41, -84)));
            Walk[1].Paved = false; Walk[1].FireChance = 0.1f;
            Walk.Add(new Area
            {
                Name = "Mercado", IsRect = true, Center = new Vector2(-36, -88), Size = new Vector2(28, 22),
                FireChance = 0.2f
            });
            var beco2 = Area.Street("Beco_Mercado_Portao", 6.5f, new Vector2(-31, -95), new Vector2(-22, -104), new Vector2(-14, -110));
            beco2.Paved = false; beco2.FireChance = 0.18f;
            Walk.Add(beco2);
            Walk.Add(new Area
            {
                Name = "Patio_Portas_Ceias", IsRect = true, Center = new Vector2(0, -117), Size = new Vector2(40, 28),
                FireChance = 0.2f
            });
            var av = Area.Street("Avenida", 9f, new Vector2(0, -104), new Vector2(0, 21));
            av.Shops = true; av.FireChance = 0.22f;
            Walk.Add(av);
            Walk.Add(new Area
            {
                Name = "Praca_da_Fonte", IsRect = true, Center = new Vector2(0, -30), Size = new Vector2(32, 26),
                Shops = true, FireChance = 0.25f
            });
            var rb = Area.Street("Rua_Barricada", 6f, new Vector2(-34, -38), new Vector2(-15, -32));
            rb.Paved = false; rb.FireChance = 0.2f;
            Walk.Add(rb);
            foreach (var s in new[]
                     {
                         Area.Street("Rua_Lateral_1", 6f, new Vector2(4, -75), new Vector2(28, -79)),
                         Area.Street("Rua_Lateral_2", 6f, new Vector2(-4, -6), new Vector2(-26, -2)),
                         Area.Street("Rua_Lateral_3", 6f, new Vector2(4, 4), new Vector2(30, 8)),
                     })
            {
                s.Paved = false;
                s.FireChance = 0.22f;
                Walk.Add(s);
            }

            // ---------------- rampa e cidadela
            Walk.Add(new Area { Name = "Rampa", IsRect = true, Center = new Vector2(0, 35), Size = new Vector2(10, 32), Frontage = false });
            Walk.Add(new Area { Name = "Portao_Cidadela", IsRect = true, Center = new Vector2(0, 53), Size = new Vector2(11, 8), Frontage = false, Citadel = true });
            Walk.Add(new Area
            {
                Name = "Patio_Cidadela", IsRect = true, Center = new Vector2(1, 73), Size = new Vector2(42, 26),
                Citadel = true, FireChance = 0.12f
            });
            Walk.Add(new Area { Name = "Patio_Palacio", IsRect = true, Center = new Vector2(14, 97), Size = new Vector2(22, 20), Frontage = false, Citadel = true });

            // ---------------- reservas (sem casas)
            Reserved.Add(Area.Rect("Templo_de_Atena", -17, 98, 17, 28));
            Reserved.Add(Area.Rect("Palacio_de_Priamo", 12.5f, 101.5f, 39, 39));
            Reserved.Add(Area.Rect("Torres_Portas_Ceias", 0, -131, 30, 10));
            Reserved.Add(Area.Rect("Lados_da_Rampa", 0, 35, 17, 36));
            Reserved.Add(Area.Rect("Torres_Portao_Cidadela", 0, 52, 26, 10));
        }

        public static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float Ellipse(float x, float z) => Mathf.Sqrt((x / Rx) * (x / Rx) + (z / Rz) * (z / Rz));

        public static float DistToRoute(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < MainRoute.Length - 1; i++) best = Mathf.Min(best, Area.SegDist(p, MainRoute[i], MainRoute[i + 1]));
            return best;
        }

        /// <summary>Estrada de terra que sai das Portas Ceias rumo ao mar.</summary>
        public static float RoadMask(float x, float z)
        {
            if (z > -128f) return 0f;
            float cx = 7f * Mathf.Sin((z + 135f) * 0.018f);
            return 1f - Smooth01((Mathf.Abs(x - cx) - 3f) / 3f);
        }

        public static float RoadCenterX(float z) => 7f * Mathf.Sin((z + 135f) * 0.018f);

        /// <summary>Altura do chão (mundo) em (x,z).</summary>
        public static float Height(float x, float z)
        {
            float e = Ellipse(x, z);
            float inside = CityHeight(x, z);
            if (e <= 1f) return inside;
            float d = (e - 1f) * 120f;
            return Mathf.Lerp(inside, OutsideHeight(x, z, d), Smooth01(d / 5f));
        }

        static float CityHeight(float x, float z)
        {
            float h = 0f;
            float dc = Vector2.Distance(new Vector2(x, z), CitadelC);
            if (dc < CitadelR + 0.6f) h = CitadelH;
            else if (dc < CitadelR + 1.4f) h = CitadelH * (1f - (dc - CitadelR - 0.6f) / 0.8f);

            float ax = Mathf.Abs(x);
            if (ax < 6.3f && z > RampZ0 - 1f && z < 60f)
            {
                float rh = CitadelH * Mathf.Clamp01((z - RampZ0) / (RampZ1 - RampZ0));
                float w = ax <= 5.5f ? 1f : 1f - (ax - 5.5f) / 0.8f;
                h = Mathf.Max(h, rh * w);
            }
            return h;
        }

        static float OutsideHeight(float x, float z, float d)
        {
            float n1 = Mathf.PerlinNoise(x * 0.012f + 31.7f, z * 0.012f + 12.3f);
            float n2 = Mathf.PerlinNoise(x * 0.005f + 5.1f, z * 0.005f + 77.9f);
            float r = Mathf.Sqrt(x * x + z * z);
            float southFade = Smooth01((z + 240f) / 90f);
            float hills = Mathf.Max(0f, r - 200f) * (0.12f + 0.3f * n2) * southFade;
            float h = -0.6f * Smooth01(d / 12f) + (n1 - 0.5f) * 5f * Smooth01((d - 8f) / 40f) + hills;
            h = Mathf.Lerp(h, -0.6f + (n1 - 0.5f) * 0.6f, RoadMask(x, z) * 0.9f);
            float shore = Smooth01((-262f - z) / 55f + (n2 - 0.5f) * 0.6f);
            return Mathf.Lerp(h, -8f, shore);
        }

        /// <summary>Peso de calçamento (0..1) para a pintura do terreno.</summary>
        public static float PavedWeight(float x, float z)
        {
            var p = new Vector2(x, z);
            if (Vector2.Distance(p, CitadelC) < CitadelR + 1f) return 1f;
            float w = 0f;
            foreach (var a in Walk)
            {
                if (!a.Paved) continue;
                float s = a.Sdf(p);
                w = Mathf.Max(w, 1f - Mathf.Clamp01((s + 0.3f) / 0.9f));
            }
            return w;
        }

        /// <summary>Peso de "terra pisada" das ruas não calçadas.</summary>
        public static float DirtPathWeight(float x, float z)
        {
            var p = new Vector2(x, z);
            float w = 0f;
            foreach (var a in Walk)
            {
                if (a.Paved) continue;
                w = Mathf.Max(w, 1f - Mathf.Clamp01((a.Sdf(p) + 0.5f) / 1.5f));
            }
            return w;
        }
    }
}
