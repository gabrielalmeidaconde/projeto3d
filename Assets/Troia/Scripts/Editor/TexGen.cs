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
            var cc = new Color[size * size];
            var hh = new float[size * size];
            System.Threading.Tasks.Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    fn((x + 0.5f) / size, (y + 0.5f) / size, out Color c, out float h);
                    c.a = 1f;
                    cc[y * size + x] = c;
                    hh[y * size + x] = h;
                }
            });
            col = cc;
            hgt = hh;
        }

        /// <summary>Gera RGBA em paralelo (para máscaras e texturas com alfa).</summary>
        public static Color[] BakeRgba(int size, System.Func<float, float, Color> fn)
        {
            var o = new Color[size * size];
            System.Threading.Tasks.Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++) o[y * size + x] = fn((x + 0.5f) / size, (y + 0.5f) / size);
            });
            return o;
        }

        public static void SaveJpg(string assetPath, Color[] px, int w, int h, int quality = 92)
        {
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), assetPath), t.EncodeToJPG(quality));
            Object.DestroyImmediate(t);
        }

        public static Color[] NormalMap(float[] h, int size, float strength)
        {
            var o = new Color[size * size];
            System.Threading.Tasks.Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    float l = h[y * size + Mod(x - 1, size)], r = h[y * size + Mod(x + 1, size)];
                    float d = h[Mod(y - 1, size) * size + x], u = h[Mod(y + 1, size) * size + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    o[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            });
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
        /// <summary>Calcário em blocos irregulares: juntas com terra, quinas lascadas, líquen, poros e escorridos.</summary>
        public static void Stone(float u, float v, out Color c, out float h)
        {
            const int rows = 5;
            int row = Mathf.FloorToInt(v * rows);
            float fy = v * rows - row;
            int nb = 3 + (int)(Hash(row, 0, 5) * 1.99f);
            float t = Mathf.Repeat(u + Hash(row, 1, 5), 1f);
            t += 0.05f * Mathf.Sin((t * 2f + Hash(row, 2, 5)) * Mathf.PI * 2f) / Mathf.PI;
            float x = t * nb;
            int col = Mathf.FloorToInt(x);
            float fx = x - col;
            int id = Mod(col, nb) + row * 7;
            float n = Fbm(u, v, 8, 8, 5, 11), n2 = Fbm(u, v, 48, 48, 3, 12), n3 = Fbm(u, v, 160, 160, 2, 18);
            float d = Mathf.Min(Mathf.Min(fx, 1f - fx) / nb, Mathf.Min(fy, 1f - fy) / rows) + (n2 - 0.5f) * 0.005f;
            float bevel = Smooth(0.0015f, 0.016f, d);
            float joint = Smooth(0.0006f, 0.0035f, d);
            float tone = 0.84f + 0.26f * Hash(id, row, 3);
            Color stone = Color.Lerp(C(0.66f, 0.62f, 0.55f), C(0.7f, 0.61f, 0.49f), Hash(id, row, 4)) * tone;
            stone *= (0.84f + 0.22f * n) * (0.92f + 0.1f * n2) * (0.95f + 0.08f * n3);
            float streak = Smooth(0.5f, 0.85f, Fbm(u, v, 16, 2, 4, 13));
            stone = Color.Lerp(stone, stone * 0.68f, streak * 0.6f);
            float chip = (1f - Smooth(0f, 0.25f, Fbm(u, v, 24, 24, 3, 14))) * 0.6f;
            stone = Color.Lerp(stone, stone * 0.78f, chip);
            float lichen = Smooth(0.62f, 0.72f, Fbm(u, v, 12, 12, 5, 15)) * Smooth(0.3f, 0.6f, Fbm(u, v, 3, 3, 3, 16));
            stone = Color.Lerp(stone, C(0.35f, 0.39f, 0.29f) * (0.8f + 0.4f * n2), lichen * 0.65f);
            float edge = (1f - Smooth(0.002f, 0.01f, d)) * Smooth(0.45f, 0.7f, n2);
            stone = Color.Lerp(stone, stone * 1.15f, edge * 0.5f);
            float pores = Smooth(0.74f, 0.84f, Fbm(u, v, 96, 96, 2, 17));
            stone *= 1f - pores * 0.28f;
            Color jointCol = C(0.23f, 0.19f, 0.15f) * (0.8f + 0.4f * n2);
            c = Color.Lerp(jointCol, stone * (0.8f + 0.2f * bevel), joint);
            h = bevel * (0.85f + 0.15f * n) + n2 * 0.06f + n3 * 0.03f - chip * 0.1f - pores * 0.06f + lichen * 0.03f;
        }

        /// <summary>Adobe: tijolos irregulares com palha, bordas esfareladas, argamassa funda, restos de reboco e salitre.</summary>
        public static void MudBrick(float u, float v, out Color c, out float h)
        {
            const int rows = 12;
            int row = Mathf.FloorToInt(v * rows);
            float fy = v * rows - row;
            int nb = 5 + (int)(Hash(row, 0, 25) * 2.99f);
            float t = Mathf.Repeat(u + Hash(row, 1, 25), 1f);
            t += 0.04f * Mathf.Sin((t * 3f + Hash(row, 2, 25)) * Mathf.PI * 2f) / (Mathf.PI * 1.5f);
            float x = t * nb;
            int col = Mathf.FloorToInt(x);
            float fx = x - col;
            int id = Mod(col, nb) + row * 13;
            float n = Fbm(u, v, 8, 8, 5, 21), n2 = Fbm(u, v, 48, 48, 3, 22), n3 = Fbm(u, v, 160, 160, 2, 23);
            float erode = (Fbm(u, v, 32, 32, 3, 24) - 0.5f) * 0.012f;
            float d = Mathf.Min(Mathf.Min(fx, 1f - fx) / nb, Mathf.Min(fy, 1f - fy) / rows) + erode;
            float brickMask = Smooth(0.003f, 0.009f, d);
            float tone = 0.78f + 0.36f * Hash(id, row, 7);
            Color brick = Color.Lerp(C(0.55f, 0.41f, 0.3f), C(0.6f, 0.48f, 0.34f), Hash(id, 3, 8)) * tone;
            brick *= (0.85f + 0.3f * n2) * (0.92f + 0.12f * n3);
            float straw = Smooth(0.8f, 0.9f, Fbm(u, v, 256, 32, 1, 26)) * brickMask;
            brick = Color.Lerp(brick, C(0.72f, 0.62f, 0.38f), straw * 0.6f);
            Color mud = C(0.38f, 0.31f, 0.24f) * (0.9f + 0.2f * n);
            c = Color.Lerp(mud, brick, brickMask);
            float plaster = Smooth(0.64f, 0.74f, n) * 0.55f;
            c = Color.Lerp(c, C(0.62f, 0.55f, 0.45f) * (0.9f + 0.15f * n2), plaster);
            float salt = Smooth(0.68f, 0.8f, Fbm(u, v, 6, 6, 4, 27)) * 0.35f;
            c = Color.Lerp(c, C(0.7f, 0.66f, 0.6f), salt);
            h = Mathf.Lerp(brickMask * (0.8f + 0.2f * n2) + n3 * 0.05f, 0.95f + n2 * 0.05f, plaster) + straw * 0.05f;
        }

        /// <summary>Reboco de cal (tingido pelo material): rachaduras em 2 escalas, escorridos e reboco caído mostrando o adobe.</summary>
        public static void Plaster(float u, float v, out Color c, out float h)
        {
            float n = Fbm(u, v, 4, 4, 6, 31), n2 = Fbm(u, v, 40, 40, 3, 32), n3 = Fbm(u, v, 128, 128, 2, 38);
            Voronoi(u, v, 6, 33, out float f1, out float f2, out _);
            float crack = (1f - Smooth(0f, 0.012f, f2 - f1)) * Smooth(0.64f, 0.76f, Fbm(u, v, 8, 8, 3, 34));
            Voronoi(u, v, 20, 37, out float g1, out float g2, out _);
            float crack2 = (1f - Smooth(0f, 0.018f, g2 - g1)) * Smooth(0.68f, 0.8f, Fbm(u, v, 12, 12, 3, 39));
            float cr = Mathf.Max(crack, crack2 * 0.45f);
            c = C(0.86f, 0.82f, 0.74f) * (0.88f + 0.14f * n + 0.06f * n2) * (0.96f + 0.06f * n3);
            c = Color.Lerp(c, C(0.45f, 0.4f, 0.35f), cr * 0.38f);
            float streak = Smooth(0.52f, 0.82f, Fbm(u, v, 12, 2, 4, 35));
            c = Color.Lerp(c, c * 0.74f, streak * 0.55f);
            float flakeN = Fbm(u, v, 8, 8, 5, 36);
            float flake = Smooth(0.75f, 0.78f, flakeN);
            float bh = 0f;
            if (flake > 0f)
            {
                MudBrick(u, v, out Color bc, out bh);
                c = Color.Lerp(c, bc * 1.05f, flake);
            }
            float rim = Smooth(0.7f, 0.73f, flakeN) * (1f - flake);
            c = Color.Lerp(c, c * 0.8f, rim * 0.6f);
            h = 0.62f + n2 * 0.2f + n3 * 0.05f - cr * 0.35f - flake * (0.45f - bh * 0.15f) + rim * 0.04f;
        }

        /// <summary>Tábuas com veios, nós, rachaduras, pregos e tábuas acinzentadas pelo tempo.</summary>
        public static void Wood(float u, float v, out Color c, out float h)
        {
            const int planks = 8;
            float x = u * planks;
            int p = Mathf.FloorToInt(x);
            float fx = x - p;
            int pw = Mod(p, planks);
            float off = Hash(pw, 0, 41);
            float jv = Mathf.Repeat(v + off, 0.5f) * 2f;
            float seam = Smooth(0f, 0.06f, Mathf.Min(fx, 1f - fx));
            float joint = Smooth(0f, 0.015f, Mathf.Min(jv, 1f - jv));
            float grain = Fbm(u, v, 64, 4, 4, 42);
            float rings = Mathf.Abs(Mathf.Sin((grain * 6f + fx * 3f) * Mathf.PI));
            Color baseC = Color.Lerp(C(0.45f, 0.31f, 0.19f), C(0.4f, 0.35f, 0.3f), Hash(pw, 5, 41) * 0.85f);
            c = baseC * (0.75f + 0.4f * off) * (0.8f + 0.25f * grain + 0.08f * rings);
            // nó
            float ku = (p + 0.3f + 0.4f * Hash(pw, 8, 41)) / planks, kv = Hash(pw, 9, 41);
            float du = u - ku, dv = Mathf.Repeat(v - kv + 0.5f, 1f) - 0.5f;
            float kd = Mathf.Sqrt(du * du + dv * dv * 0.08f);
            float knot = 1f - Smooth(0.004f, 0.011f, kd);
            float knotRing = (1f - Smooth(0.011f, 0.02f, kd)) * (1f - knot);
            c = Color.Lerp(c, c * 0.45f, knot);
            c = Color.Lerp(c, c * 0.8f, knotRing * 0.6f);
            // rachaduras ao longo do veio
            float crack = Smooth(0.9f, 0.96f, Fbm(u, v, 256, 6, 2, 43));
            c = Color.Lerp(c, C(0.08f, 0.06f, 0.05f), crack * 0.7f);
            // pregos nas pontas das tábuas
            float nd = Mathf.Sqrt(((fx - 0.5f) / planks) * ((fx - 0.5f) / planks) * 1f + Mathf.Pow(Mathf.Min(jv, 1f - jv) * 0.5f - 0.012f, 2f));
            float nail = 1f - Smooth(0.002f, 0.0035f, nd);
            c = Color.Lerp(c, C(0.12f, 0.1f, 0.09f), nail);
            float mask = seam * joint;
            c = Color.Lerp(C(0.08f, 0.06f, 0.045f), c, mask);
            h = mask * (0.7f + 0.3f * grain) - crack * 0.2f - knot * 0.08f + nail * 0.25f;
        }

        /// <summary>Terra batida: sulcos, manchas úmidas, palha, cacos de cerâmica e pedregulhos.</summary>
        public static void Earth(float u, float v, out Color c, out float h)
        {
            float n = Fbm(u, v, 6, 6, 6, 51), n2 = Fbm(u, v, 40, 40, 3, 52), n3 = Fbm(u, v, 128, 128, 2, 57);
            Voronoi(u, v, 24, 53, out float f1, out _, out float id);
            float pebble = (1f - Smooth(0.15f, 0.3f, f1)) * Smooth(0.72f, 0.8f, id);
            c = C(0.4f, 0.32f, 0.24f) * (0.82f + 0.3f * n + 0.1f * n2) * (0.95f + 0.08f * n3);
            c = Color.Lerp(c, C(0.44f, 0.39f, 0.33f) * (0.85f + 0.25f * id), pebble * 0.8f);
            float shard = (1f - Smooth(0.1f, 0.18f, f1)) * Smooth(0.95f, 0.97f, id);
            c = Color.Lerp(c, C(0.55f, 0.3f, 0.2f), shard * 0.8f);
            float rut = Smooth(0.6f, 0.8f, Fbm(u, v, 2, 16, 3, 54));
            c = Color.Lerp(c, c * 0.72f, rut * 0.5f);
            float damp = Smooth(0.6f, 0.75f, Fbm(u, v, 4, 4, 4, 55));
            c = Color.Lerp(c, c * 0.62f, damp * 0.5f);
            float strawBits = Smooth(0.86f, 0.91f, Fbm(u, v, 128, 16, 1, 56));
            c = Color.Lerp(c, C(0.6f, 0.52f, 0.33f), strawBits * 0.55f);
            h = n * 0.5f + n2 * 0.2f + n3 * 0.05f + pebble * 0.12f + shard * 0.08f - rut * 0.08f;
        }

        /// <summary>Telhas de terracota com musgo e lascas.</summary>
        public static void RoofTile(float u, float v, out Color c, out float h)
        {
            const int cols = 6, rows = 8;
            int row = Mathf.FloorToInt(v * rows);
            float x = u * cols + (row % 2) * 0.5f;
            int col = Mathf.FloorToInt(x);
            float fx = x - col, fy = v * rows - row;
            float barrel = Mathf.Sin(fx * Mathf.PI);
            float lip = Smooth(0f, 0.18f, fy);
            float n = Fbm(u, v, 16, 16, 4, 61), n2 = Fbm(u, v, 64, 64, 2, 65);
            float tone = 0.8f + 0.3f * Hash(Mod(col, cols), row, 62);
            c = C(0.6f, 0.3f, 0.18f) * tone * (0.55f + 0.45f * barrel) * (0.6f + 0.4f * lip) * (0.85f + 0.25f * n) * (0.95f + 0.08f * n2);
            float moss = Smooth(0.6f, 0.75f, Fbm(u, v, 10, 10, 4, 63)) * (1f - lip * 0.4f);
            c = Color.Lerp(c, C(0.28f, 0.31f, 0.2f) * (0.8f + 0.4f * n2), moss * 0.55f);
            float chip = Smooth(0.8f, 0.85f, Fbm(u, v, 40, 40, 2, 64));
            c *= 1f - chip * 0.3f;
            h = barrel * 0.6f + fy * 0.3f + n * 0.1f - chip * 0.1f + moss * 0.03f;
        }

        /// <summary>Lajes irregulares: rachaduras, centros gastos, juntas com terra/musgo/cascalho e manchas.</summary>
        public static void Cobble(float u, float v, out Color c, out float h)
        {
            Voronoi(u, v, 7, 71, out float f1, out float f2, out float id);
            float e = f2 - f1;
            float stone = Smooth(0.012f, 0.05f, e);
            float n = Fbm(u, v, 16, 16, 4, 72), n2 = Fbm(u, v, 4, 4, 3, 73), n3 = Fbm(u, v, 64, 64, 2, 74);
            Color sc = Color.Lerp(C(0.5f, 0.48f, 0.45f), C(0.63f, 0.56f, 0.46f), id) * (0.82f + 0.26f * n) * (0.92f + 0.12f * n3);
            Voronoi(u, v, 21, 75, out float g1, out float g2, out _);
            float crack = (1f - Smooth(0f, 0.025f, g2 - g1)) * Smooth(0.6f, 0.72f, Fbm(u, v, 10, 10, 3, 76)) * stone;
            sc = Color.Lerp(sc, sc * 0.45f, crack);
            sc *= 0.95f + 0.1f * (1f - Smooth(0f, 0.5f, f1));
            float moss = Smooth(0.5f, 0.7f, Fbm(u, v, 16, 16, 4, 77));
            Color jointC = Color.Lerp(C(0.26f, 0.22f, 0.18f), C(0.21f, 0.25f, 0.15f), moss * 0.7f) * (0.85f + 0.3f * n);
            float grit = Smooth(0.7f, 0.8f, Fbm(u, v, 160, 160, 1, 78));
            jointC = Color.Lerp(jointC, C(0.42f, 0.38f, 0.33f), grit * 0.5f);
            c = Color.Lerp(jointC, sc, stone);
            float stain = Smooth(0.62f, 0.8f, Fbm(u, v, 5, 5, 4, 79));
            c = Color.Lerp(c, c * 0.68f, stain * 0.6f);
            c *= 0.86f + 0.2f * n2;
            h = stone * (0.75f + 0.15f * n + 0.1f * (1f - Mathf.Clamp01(f1 * 1.2f))) + n3 * 0.04f - crack * 0.3f + grit * (1f - stone) * 0.1f;
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


        // ------------------------------------------------------------------ brasas, sujeira global e decals
        /// <summary>Mapa de emissão das brasas nas rachaduras do carvão (mesmas sementes do Charred).</summary>
        public static Color[] CharredEmission(int s) => BakeRgba(s, (u, v) =>
        {
            Voronoi(u, v, 12, 112, out float f1, out float f2, out _);
            float crack = 1f - Smooth(0f, 0.05f, f2 - f1);
            float glow = crack * crack * Smooth(0.42f, 0.7f, Fbm(u, v, 6, 6, 3, 113));
            float hot = Smooth(0.6f, 0.9f, glow);
            return new Color(glow, glow * (0.35f + 0.4f * hot), glow * (0.08f + 0.2f * hot), 1f);
        });

        /// <summary>R: tom em grande escala  G: escorridos verticais  B: respingos  A: poeira (linear).</summary>
        public static Color[] Grime(int s) => BakeRgba(s, (u, v) =>
        {
            float macro = Smooth(0.25f, 0.75f, Fbm(u, v, 4, 4, 5, 201));
            float streak = Smooth(0.5f, 0.85f, Fbm(u, v, 24, 3, 4, 202)) * Smooth(0.35f, 0.7f, Fbm(u, v, 4, 2, 3, 203));
            float splash = Smooth(0.3f, 0.8f, Fbm(u, v, 16, 16, 5, 204));
            float dust = Smooth(0.35f, 0.75f, Fbm(u, v, 32, 32, 3, 205));
            return new Color(macro, streak, splash, dust);
        });

        static Color Rgba(Color c, float a) => new Color(c.r, c.g, c.b, Mathf.Clamp01(a));

        public static Color[] DecalSoot(int s) => BakeRgba(s, (u, v) =>
        {
            float x = (u - 0.5f) * 2f, y = v;
            float w = 0.22f + 0.75f * y;
            float shape = 1f - Smooth(w * 0.45f, w, Mathf.Abs(x) + (Fbm(u, v, 4, 4, 4, 301) - 0.5f) * 0.4f);
            float fade = Smooth(0f, 0.12f, y) * (1f - Smooth(0.6f, 1f, y));
            float n = Fbm(u, v, 8, 8, 5, 302);
            return Rgba(C(0.035f, 0.03f, 0.028f) * (0.7f + 0.6f * n), shape * fade * (0.55f + 0.7f * n));
        });

        public static Color[] DecalScorch(int s) => BakeRgba(s, (u, v) =>
        {
            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
            float n = Fbm(u, v, 6, 6, 5, 311);
            float d = Mathf.Sqrt(dx * dx + dy * dy) + (n - 0.5f) * 0.45f;
            float a = 1f - Smooth(0.35f, 0.95f, d);
            Color col = Color.Lerp(C(0.02f, 0.018f, 0.016f), C(0.22f, 0.21f, 0.2f), Smooth(0.3f, 0.85f, d) * 0.7f);
            return Rgba(col * (0.8f + 0.4f * Fbm(u, v, 32, 32, 2, 312)), a * 0.95f);
        });

        public static Color[] DecalMud(int s) => BakeRgba(s, (u, v) =>
        {
            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
            float n = Fbm(u, v, 5, 5, 5, 321);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Smooth(0.42f, 0.6f, n * (1.25f - d * 0.9f));
            return Rgba(C(0.2f, 0.15f, 0.11f) * (0.75f + 0.5f * Fbm(u, v, 24, 24, 3, 322)), a * 0.85f);
        });

        public static Color[] DecalBlood(int s) => BakeRgba(s, (u, v) =>
        {
            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
            float n = Fbm(u, v, 6, 6, 5, 331);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float pool = 1f - Smooth(0.25f, 0.4f, d + (n - 0.5f) * 0.35f);
            Voronoi(u, v, 18, 332, out float f1, out _, out float id);
            float drops = (1f - Smooth(0.08f, 0.16f, f1)) * Smooth(0.75f, 0.8f, id) * (1f - Smooth(0.5f, 0.95f, d));
            float a = Mathf.Max(pool, drops);
            return Rgba(C(0.2f, 0.015f, 0.012f) * (0.65f + 0.5f * n), a * 0.9f);
        });

        public static Color[] DecalCrack(int s) => BakeRgba(s, (u, v) =>
        {
            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            Voronoi(u, v, 5, 341, out float f1, out float f2, out _);
            Voronoi(u, v, 13, 342, out float g1, out float g2, out _);
            float line = Mathf.Max(1f - Smooth(0f, 0.025f, f2 - f1), (1f - Smooth(0f, 0.03f, g2 - g1)) * 0.7f);
            float mask = (1f - Smooth(0.3f, 0.95f, d)) * Smooth(0.4f, 0.55f, Fbm(u, v, 4, 4, 4, 343) + (1f - d) * 0.3f);
            return Rgba(C(0.06f, 0.05f, 0.045f), line * mask);
        });

        public static Color[] DecalAsh(int s) => BakeRgba(s, (u, v) =>
        {
            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
            float n = Fbm(u, v, 8, 8, 5, 351);
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Smooth(0.4f, 0.6f, n * (1.3f - d));
            float ember = Smooth(0.82f, 0.9f, Fbm(u, v, 48, 48, 2, 352));
            Color col = Color.Lerp(C(0.3f, 0.29f, 0.28f), C(0.08f, 0.07f, 0.065f), Fbm(u, v, 24, 24, 3, 353));
            return Rgba(col, a * 0.9f + ember * 0f);
        });

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
