using System.IO;
using UnityEngine;

namespace Troia.EditorTools
{
    /// <summary>
    /// Texturas procedurais "tileáveis" (ruído de valor periódico + Voronoi periódico).
    /// Cada função recebe (u,v) em [0,1) e devolve cor (sRGB) e altura (para normal map).
    /// </summary>
    public static class TexGen
    {
        public delegate void Px(float u, float v, out Color c, out float h);

        public static int Mod(int a, int n) => ((a % n) + n) % n;

        public static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        public static float VNoise(float x, float y, int px, int py, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = Mod(x0, px), xb = Mod(x0 + 1, px), ya = Mod(y0, py), yb = Mod(y0 + 1, py);
            float a = Hash(xa, ya, seed), b = Hash(xb, ya, seed), c = Hash(xa, yb, seed), d = Hash(xb, yb, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>fBm periódico: fx/fy = frequência base em cada eixo (inteiros => tileável).</summary>
        public static float Fbm(float u, float v, int fx, int fy, int oct, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < oct; o++)
            {
                sum += amp * VNoise(u * fx, v * fy, fx, fy, seed + o * 31);
                norm += amp;
                amp *= 0.5f;
                fx *= 2;
                fy *= 2;
            }
            return sum / norm;
        }

        public static void Voronoi(float u, float v, int cells, int seed, out float f1, out float f2, out float id)
        {
            float x = u * cells, y = v * cells;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            f1 = f2 = 10f;
            id = 0f;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy;
                    int wx = Mod(gx, cells), wy = Mod(gy, cells);
                    float px = gx + 0.1f + 0.8f * Hash(wx, wy, seed);
                    float py = gy + 0.1f + 0.8f * Hash(wx, wy, seed + 17);
                    float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                    if (d < f1)
                    {
                        f2 = f1;
                        f1 = d;
                        id = Hash(wx, wy, seed + 5);
                    }
                    else if (d < f2) f2 = d;
                }
            }
        }

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        // ------------------------------------------------------------------ geração / IO
        public static void Bake(int size, Px fn, out Color[] col, out float[] hgt)
        {
            col = new Color[size * size];
            hgt = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    fn((x + 0.5f) / size, (y + 0.5f) / size, out Color c, out float h);
                    c.a = 1f;
                    col[y * size + x] = c;
                    hgt[y * size + x] = h;
                }
            }
        }

        public static Color[] NormalMap(float[] h, int size, float strength)
        {
            var o = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float l = h[y * size + Mod(x - 1, size)], r = h[y * size + Mod(x + 1, size)];
                    float d = h[Mod(y - 1, size) * size + x], u = h[Mod(y + 1, size) * size + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    o[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            return o;
        }

        public static void SavePng(string assetPath, Color[] px, int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            File.WriteAllBytes(full, t.EncodeToPNG());
            Object.DestroyImmediate(t);
        }

        // ------------------------------------------------------------------ materiais
        /// <summary>Blocos de calcário encaixados (muralhas de Troia VI).</summary>
        public static void Stone(float u, float v, out Color c, out float h)
        {
            const int cols = 4, rows = 6;
            int row = Mathf.FloorToInt(v * rows);
            float x = u * cols + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(x);
            float fx = x - col, fy = v * rows - row;
            float d = Mathf.Min(Mathf.Min(fx, 1f - fx) / cols, Mathf.Min(fy, 1f - fy) / rows);
            float n = Fbm(u, v, 8, 8, 5, 11);
            float n2 = Fbm(u, v, 32, 32, 3, 12);
            float mortar = Smooth(0.003f, 0.01f, d + (n - 0.5f) * 0.006f);
            float tone = 0.8f + 0.32f * Hash(Mod(col, cols), row, 3);
            Color stone = C(0.6f, 0.56f, 0.49f) * tone * (0.8f + 0.35f * n) * (0.9f + 0.14f * n2);
            c = Color.Lerp(C(0.2f, 0.18f, 0.16f), stone, mortar);
            h = mortar * (0.7f + 0.3f * n) + n2 * 0.08f;
        }

        /// <summary>Tijolos de adobe com restos de reboco.</summary>
        public static void MudBrick(float u, float v, out Color c, out float h)
        {
            const int cols = 6, rows = 12;
            int row = Mathf.FloorToInt(v * rows);
            float x = u * cols + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(x);
            float fx = x - col, fy = v * rows - row;
            float d = Mathf.Min(Mathf.Min(fx, 1f - fx) / cols, Mathf.Min(fy, 1f - fy) / rows);
            float n = Fbm(u, v, 8, 8, 5, 21), n2 = Fbm(u, v, 48, 48, 2, 22);
            float mortar = Smooth(0.004f, 0.011f, d + (n2 - 0.5f) * 0.006f);
            float tone = 0.8f + 0.35f * Hash(Mod(col, cols), row, 7);
            Color brick = C(0.56f, 0.4f, 0.28f) * tone * (0.85f + 0.3f * n2);
            Color mud = C(0.45f, 0.38f, 0.3f) * (0.9f + 0.2f * n);
            c = Color.Lerp(mud, brick, mortar);
            float plaster = Smooth(0.64f, 0.74f, n) * 0.55f;
            c = Color.Lerp(c, C(0.62f, 0.55f, 0.45f) * (0.9f + 0.15f * n2), plaster);
            h = Mathf.Lerp(mortar * 0.8f + n2 * 0.15f, 0.95f + n2 * 0.05f, plaster);
        }

        /// <summary>Reboco de cal (claro; tingido pelo material).</summary>
        public static void Plaster(float u, float v, out Color c, out float h)
        {
            float n = Fbm(u, v, 4, 4, 6, 31), n2 = Fbm(u, v, 32, 32, 3, 32);
            Voronoi(u, v, 6, 33, out float f1, out float f2, out _);
            float crack = 1f - Smooth(0f, 0.035f, f2 - f1);
            crack *= Smooth(0.5f, 0.7f, Fbm(u, v, 8, 8, 3, 34));
            c = C(0.84f, 0.8f, 0.72f) * (0.86f + 0.18f * n + 0.06f * n2);
            c = Color.Lerp(c, C(0.42f, 0.38f, 0.33f), crack * 0.6f);
            float dirt = Smooth(0.55f, 0.82f, Fbm(u, v, 3, 3, 4, 35));
            c = Color.Lerp(c, c * 0.62f, dirt);
            h = 0.5f + n2 * 0.3f - crack * 0.4f + n * 0.2f;
        }

        /// <summary>Tábuas de madeira com veios.</summary>
        public static void Wood(float u, float v, out Color c, out float h)
        {
            const int planks = 8;
            float x = u * planks;
            int p = Mathf.FloorToInt(x);
            float fx = x - p;
            float off = Hash(Mod(p, planks), 0, 41);
            float jv = Mathf.Repeat(v + off, 0.5f) * 2f;
            float seam = Smooth(0f, 0.06f, Mathf.Min(fx, 1f - fx));
            float joint = Smooth(0f, 0.015f, Mathf.Min(jv, 1f - jv));
            float grain = Fbm(u, v, 64, 4, 4, 42);
            float rings = Mathf.Abs(Mathf.Sin((grain * 6f + fx * 3f) * Mathf.PI));
            c = C(0.45f, 0.31f, 0.19f) * (0.75f + 0.4f * off) * (0.8f + 0.25f * grain + 0.08f * rings);
            float mask = seam * joint;
            c = Color.Lerp(C(0.1f, 0.07f, 0.05f), c, mask);
            h = mask * (0.7f + 0.3f * grain);
        }

        /// <summary>Terra batida com pedregulhos (telhados planos e chão da cidade).</summary>
        public static void Earth(float u, float v, out Color c, out float h)
        {
            float n = Fbm(u, v, 6, 6, 6, 51), n2 = Fbm(u, v, 40, 40, 3, 52);
            Voronoi(u, v, 24, 53, out float f1, out _, out float id);
            float pebble = (1f - Smooth(0.18f, 0.32f, f1)) * Smooth(0.6f, 0.7f, id);
            c = C(0.36f, 0.29f, 0.22f) * (0.8f + 0.35f * n + 0.1f * n2);
            c = Color.Lerp(c, C(0.5f, 0.46f, 0.4f) * (0.8f + 0.4f * id), pebble);
            h = n * 0.5f + n2 * 0.2f + pebble * 0.4f;
        }

        /// <summary>Telhas de terracota (templo e palácio).</summary>
        public static void RoofTile(float u, float v, out Color c, out float h)
        {
            const int cols = 6, rows = 8;
            int row = Mathf.FloorToInt(v * rows);
            float x = u * cols + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(x);
            float fx = x - col, fy = v * rows - row;
            float barrel = Mathf.Sin(fx * Mathf.PI);
            float lip = Smooth(0f, 0.18f, fy);
            float n = Fbm(u, v, 16, 16, 4, 61);
            float tone = 0.8f + 0.3f * Hash(Mod(col, cols), row, 62);
            c = C(0.6f, 0.3f, 0.18f) * tone * (0.55f + 0.45f * barrel) * (0.6f + 0.4f * lip) * (0.85f + 0.25f * n);
            h = barrel * 0.6f + fy * 0.3f + n * 0.1f;
        }

        /// <summary>Calçamento de pedras irregulares.</summary>
        public static void Cobble(float u, float v, out Color c, out float h)
        {
            Voronoi(u, v, 10, 71, out float f1, out float f2, out float id);
            float stone = Smooth(0.04f, 0.16f, f2 - f1);
            float n = Fbm(u, v, 16, 16, 4, 72), n2 = Fbm(u, v, 4, 4, 3, 73);
            Color sc = C(0.5f, 0.47f, 0.42f) * (0.7f + 0.45f * id) * (0.85f + 0.25f * n);
            Color dirt = C(0.22f, 0.19f, 0.15f) * (0.8f + 0.3f * n);
            c = Color.Lerp(dirt, sc, stone);
            c = Color.Lerp(c, c * 0.7f, Smooth(0.5f, 0.8f, n2));
            h = stone * (0.6f + 0.3f * (1f - Mathf.Clamp01(f1 * 1.6f)) + 0.1f * n);
        }

        public static void Marble(float u, float v, out Color c, out float h)
        {
            float n = Fbm(u, v, 4, 4, 6, 81);
            float vein = 1f - Mathf.Abs(Mathf.Sin((3f * u + 2f * v + n * 4f) * Mathf.PI));
            vein = Mathf.Pow(vein, 10f);
            float n2 = Fbm(u, v, 24, 24, 3, 82);
            c = C(0.86f, 0.84f, 0.8f) * (0.93f + 0.08f * n2);
            c = Color.Lerp(c, C(0.55f, 0.53f, 0.52f), vein * 0.6f);
            h = 0.5f + 0.1f * n2;
        }

        public static void Grass(float u, float v, out Color c, out float h)
        {
            float n = Fbm(u, v, 6, 6, 5, 91), blades = Fbm(u, v, 128, 32, 2, 92), n3 = Fbm(u, v, 3, 3, 3, 93);
            c = Color.Lerp(C(0.24f, 0.25f, 0.13f), C(0.36f, 0.33f, 0.18f), n) * (0.75f + 0.4f * blades);
            c = Color.Lerp(c, C(0.33f, 0.27f, 0.2f), Smooth(0.6f, 0.75f, n3) * 0.7f);
            h = blades * 0.6f + n * 0.4f;
        }

        public static void Sand(float u, float v, out Color c, out float h)
        {
            float n = Fbm(u, v, 8, 8, 5, 101), r = Fbm(u, v, 2, 24, 3, 102);
            c = C(0.62f, 0.56f, 0.44f) * (0.85f + 0.2f * n) * (0.92f + 0.1f * r);
            h = r * 0.5f + n * 0.3f;
        }

        public static void Charred(float u, float v, out Color c, out float h)
        {
            float n = Fbm(u, v, 8, 8, 5, 111);
            Voronoi(u, v, 12, 112, out float f1, out float f2, out _);
            float crack = 1f - Smooth(0f, 0.05f, f2 - f1);
            c = C(0.07f, 0.06f, 0.055f) * (0.7f + 0.8f * n);
            c = Color.Lerp(c, C(0.02f, 0.015f, 0.01f), crack);
            h = n * 0.6f - crack * 0.4f + 0.4f;
        }

        public static void Cloth(float u, float v, out Color c, out float h)
        {
            float w = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 96f) * Mathf.Sin(v * Mathf.PI * 2f * 96f);
            float n = Fbm(u, v, 4, 4, 4, 121);
            c = C(0.9f, 0.88f, 0.84f) * (0.85f + 0.1f * w) * (0.8f + 0.25f * n);
            h = w * 0.5f;
        }

        // ------------------------------------------------------------------ partículas (alfa)
        public static Color[] Flame(int s)
        {
            var o = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                    float dx = (u - 0.5f) / 0.42f, dy = (v - 0.42f) / 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = (1f - Smooth(0.15f, 1f, d)) * (0.7f + 0.3f * Fbm(u, v, 4, 4, 4, 131));
                    o[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a * 1.2f));
                }
            }
            return o;
        }

        public static Color[] Smoke(int s)
        {
            var o = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                    float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    float a = (1f - Smooth(0.25f, 1f, d)) * Smooth(0.25f, 0.75f, Fbm(u, v, 4, 4, 5, 141));
                    o[y * s + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a * 1.3f));
                }
            }
            return o;
        }

        public static Color[] Spark(int s)
        {
            var o = new Color[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                    float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                    float a = Mathf.Exp(-d * d * 9f);
                    o[y * s + x] = new Color(1f, 1f, 1f, a);
                }
            }
            return o;
        }
    }
}
