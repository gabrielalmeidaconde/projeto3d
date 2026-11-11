using System.Collections.Generic;
using UnityEngine;

namespace Troia.EditorTools
{
    public enum HouseType { Terrea, Escada, Sobrado, Alta, Loja, Queimada }

    /// <summary>
    /// Geometria procedural (blockout detalhado) de todas as construções e adereços da Fase 1.
    /// Convenção: frente das casas/adereços em +Z local, pivô no chão.
    /// </summary>
    public static class TroiaMeshes
    {
        static System.Random _r = new System.Random(1);
        static float F(float a, float b) => a + (float)_r.NextDouble() * (b - a);
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector2 V2(float x, float y) => new Vector2(x, y);
        static Quaternion Yaw(float d) => Quaternion.Euler(0f, d, 0f);
        static readonly Quaternion Id = Quaternion.identity;

        // =====================================================================================
        // Peças comuns
        // =====================================================================================
        static void Plinth(MeshBuilder b, float x0, float x1, float z0, float z1) =>
            b.Box(V((x0 + x1) * 0.5f, 0.075f, (z0 + z1) * 0.5f), V(x1 - x0 + 0.14f, 0.75f, z1 - z0 + 0.14f), M.Stone);

        static void Walls(MeshBuilder b, float x0, float x1, float z0, float z1, float y0, float y1, M m) =>
            b.Box(V((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f), V(x1 - x0, y1 - y0, z1 - z0), m);

        static void FlatRoof(MeshBuilder b, float x0, float x1, float z0, float z1, float y, M parapet,
            bool front = true, bool back = true, bool left = true, bool right = true)
        {
            float w = x1 - x0, d = z1 - z0, cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            b.Box(V(cx, y + 0.125f, cz), V(w + 0.3f, 0.25f, d + 0.3f), M.Roof);
            float py = y + 0.5f;
            if (front) b.Box(V(cx, py, z1 + 0.025f), V(w + 0.3f, 0.5f, 0.25f), parapet);
            if (back) b.Box(V(cx, py, z0 - 0.025f), V(w + 0.3f, 0.5f, 0.25f), parapet);
            if (left) b.Box(V(x0 - 0.025f, py, cz), V(0.25f, 0.5f, d - 0.2f), parapet);
            if (right) b.Box(V(x1 + 0.025f, py, cz), V(0.25f, 0.5f, d - 0.2f), parapet);
        }

        static void BeamEnds(MeshBuilder b, float x0, float x1, float zFace, float sign, float y)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt((x1 - x0) / 1.3f));
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Lerp(x0 + 0.5f, x1 - 0.5f, i / (float)(n - 1));
                b.Box(V(x, y, zFace + sign * 0.17f), V(0.16f, 0.16f, 0.34f), M.WoodDark);
            }
        }

        static void Door(MeshBuilder b, float x, float zFace, float sign, float y0 = 0f, float w = 1.2f, float h = 2.3f)
        {
            double dr = _r.NextDouble();
            b.Box(V(x, y0 + h * 0.5f, zFace), V(w, h, 0.1f), dr < 0.4 ? M.Dark : dr < 0.7 ? M.PaintedBlue : M.WoodDark);
            b.Box(V(x, y0 + h + 0.12f, zFace + sign * 0.06f), V(w + 0.5f, 0.24f, 0.2f), M.Wood);
            b.Box(V(x - w * 0.5f - 0.08f, y0 + h * 0.5f, zFace + sign * 0.04f), V(0.16f, h, 0.14f), M.Wood);
            b.Box(V(x + w * 0.5f + 0.08f, y0 + h * 0.5f, zFace + sign * 0.04f), V(0.16f, h, 0.14f), M.Wood);
        }

        static void Window(MeshBuilder b, float x, float y, float zFace, float sign, float w = 0.7f, float h = 0.55f)
        {
            b.Box(V(x, y, zFace), V(w, h, 0.1f), _r.NextDouble() < 0.3 ? M.WindowLit : M.Dark);
            b.Box(V(x, y - h * 0.5f - 0.04f, zFace + sign * 0.06f), V(w + 0.2f, 0.08f, 0.16f), M.Wood);
            b.Box(V(x, y + h * 0.5f + 0.05f, zFace + sign * 0.05f), V(w + 0.2f, 0.1f, 0.12f), M.Wood);
        }

        static void WindowX(MeshBuilder b, float z, float y, float xFace, float sign, float w = 0.7f, float h = 0.55f)
        {
            b.Box(V(xFace, y, z), V(0.1f, h, w), _r.NextDouble() < 0.3 ? M.WindowLit : M.Dark);
            b.Box(V(xFace + sign * 0.06f, y - h * 0.5f - 0.04f, z), V(0.16f, 0.08f, w + 0.2f), M.Wood);
        }

        public static void Amphora(MeshBuilder b, Vector3 p, float s = 1f, M m = M.Terracotta, Quaternion? rot = null)
        {
            Quaternion q = rot ?? Id;
            var prof = new[]
            {
                V2(0.05f, 0f), V2(0.12f, 0.03f), V2(0.24f, 0.28f), V2(0.28f, 0.55f), V2(0.22f, 0.8f),
                V2(0.1f, 0.95f), V2(0.085f, 1.05f), V2(0.12f, 1.1f)
            };
            for (int i = 0; i < prof.Length; i++) prof[i] *= s;
            b.Lathe(p, q, prof, 10, m, true, true);
            b.BoxBetween(p + q * (V(0.2f, 0.82f, 0f) * s), p + q * (V(0.13f, 1.02f, 0f) * s), 0.04f * s, 0.08f * s, m);
            b.BoxBetween(p + q * (V(-0.2f, 0.82f, 0f) * s), p + q * (V(-0.13f, 1.02f, 0f) * s), 0.04f * s, 0.08f * s, m);
        }

        public static void Basket(MeshBuilder b, Vector3 p, float s = 1f)
        {
            var prof = new[] { V2(0.3f, 0f), V2(0.38f, 0.35f), V2(0.4f, 0.4f), V2(0.36f, 0.4f), V2(0.34f, 0.06f), V2(0f, 0.06f) };
            for (int i = 0; i < prof.Length; i++) prof[i] *= s;
            b.Lathe(p, Id, prof, 10, M.Straw, false, true);
        }

        static void RoofClutter(MeshBuilder b, float x0, float x1, float z0, float z1, float y)
        {
            if (_r.NextDouble() < 0.5)
            {
                int n = 1 + _r.Next(3);
                for (int i = 0; i < n; i++) Amphora(b, V(F(x0, x1), y, F(z0, z0 + 1.5f)), F(0.7f, 1f));
            }
            if (_r.NextDouble() < 0.4)
                b.Box(V(F(x0 + 1f, x1 - 1f), y + 0.03f, F(z0 + 1f, z1 - 1f)), V(F(1.5f, 2.5f), 0.06f, F(1.2f, 2f)), M.Straw, Yaw(F(0f, 90f)));
            if (_r.NextDouble() < 0.25)
                b.Box(V(F(x0, x1), y + 0.4f, F(z0, z1)), V(0.8f, 0.8f, 0.8f), M.Wood);
        }

        static void Balcony(MeshBuilder b, float x, float y)
        {
            b.Box(V(x, y, 4.55f), V(2.6f, 0.15f, 1.1f), M.Wood);
            b.Box(V(x, y + 0.95f, 5.05f), V(2.6f, 0.08f, 0.08f), M.Wood);
            b.Box(V(x - 1.26f, y + 0.95f, 4.55f), V(0.08f, 0.08f, 1.0f), M.Wood);
            b.Box(V(x + 1.26f, y + 0.95f, 4.55f), V(0.08f, 0.08f, 1.0f), M.Wood);
            for (int i = 0; i < 6; i++) b.Box(V(x - 1.25f + i * 0.5f, y + 0.5f, 5.05f), V(0.07f, 0.9f, 0.07f), M.Wood);
            b.BoxBetween(V(x - 1.1f, y - 0.9f, 4.0f), V(x - 1.1f, y - 0.05f, 4.9f), 0.12f, 0.12f, M.WoodDark);
            b.BoxBetween(V(x + 1.1f, y - 0.9f, 4.0f), V(x + 1.1f, y - 0.05f, 4.9f), 0.12f, 0.12f, M.WoodDark);
            Door(b, x, 4f, 1f, y + 0.08f, 1.0f, 2.0f);
        }

        // =====================================================================================
        // Casas (pegada 8 x 8 m, frente em +Z)
        // =====================================================================================
        public static MeshBuilder House(HouseType type, int seed, out Vector3 fireLocal)
        {
            _r = new System.Random(seed * 7919 + (int)type * 104729 + 17);
            var b = new MeshBuilder();
            M[] wallOptions = { M.Plaster, M.PlasterOchre, M.Plaster, M.PlasterWhite, M.MudBrick, M.PlasterRed, M.PlasterOchre };
            M wall = wallOptions[_r.Next(wallOptions.Length)];
            M trim = _r.NextDouble() < 0.5 ? M.MudBrick : M.Stone;
            if (wall == M.MudBrick) trim = M.Stone;
            fireLocal = Vector3.zero;

            switch (type)
            {
                case HouseType.Terrea:
                {
                    float H = F(3.5f, 4.1f);
                    Plinth(b, -4, 4, -4, 4);
                    Walls(b, -4, 4, -4, 4, 0, H, wall);
                    FlatRoof(b, -4, 4, -4, 4, H, trim);
                    BeamEnds(b, -4, 4, 4, 1, H - 0.25f);
                    BeamEnds(b, -4, 4, -4, -1, H - 0.25f);
                    float dx = F(-2.2f, 2.2f);
                    Door(b, dx, 4f, 1f);
                    Window(b, dx > 0 ? -2.3f : 2.3f, 2.6f, 4f, 1f);
                    Window(b, -2f, 2.6f, -4f, -1f);
                    Window(b, 2f, 2.6f, -4f, -1f);
                    WindowX(b, F(-2f, 2f), 2.6f, 4f, 1f);
                    WindowX(b, F(-2f, 2f), 2.6f, -4f, -1f);
                    RoofClutter(b, -3.4f, 3.4f, -3.4f, 3.4f, H + 0.25f);
                    b.Box(V(F(-3f, 3f), H - 0.05f, 4.35f), V(0.16f, 0.12f, 0.7f), M.WoodDark);
                    fireLocal = V(0f, H + 0.25f, 0f);
                    break;
                }
                case HouseType.Escada:
                {
                    float H = F(3.3f, 3.7f);
                    Plinth(b, -4, 4, -4, 4);
                    Walls(b, -4, 2.6f, -4, 4, 0, H, wall);
                    FlatRoof(b, -4, 2.6f, -4, 4, H, trim, true, true, true, false);
                    int n = Mathf.CeilToInt(H / 0.3f);
                    float run = 7.2f / n;
                    for (int i = 0; i < n; i++)
                    {
                        float top = (i + 1) * H / n;
                        b.Box(V(3.3f, top * 0.5f, 3.9f - run * (i + 0.5f)), V(1.3f, top, run + 0.01f), trim);
                    }
                    Door(b, -1.5f, 4f, 1f);
                    Window(b, 1.2f, 2.5f, 4f, 1f);
                    Window(b, -2f, 2.5f, -4f, -1f);
                    WindowX(b, 0f, 2.5f, -4f, -1f);
                    BeamEnds(b, -4, 2.6f, 4, 1, H - 0.25f);
                    float y = H + 0.25f;
                    foreach (var (px, pz) in new[] { (-3.5f, -3.5f), (-0.7f, -3.5f), (-3.5f, -0.7f), (-0.7f, -0.7f) })
                        b.Box(V(px, y + 1.1f, pz), V(0.14f, 2.2f, 0.14f), M.Wood);
                    b.Box(V(-2.1f, y + 2.25f, -2.1f), V(3.4f, 0.1f, 3.4f), M.Straw);
                    Amphora(b, V(1.4f, y, 2.6f), 0.9f);
                    fireLocal = V(-1f, y, 1f);
                    break;
                }
                case HouseType.Sobrado:
                {
                    float H1 = F(3.4f, 3.8f), H2 = F(2.8f, 3.2f), zU = -4f + 4.6f;
                    Plinth(b, -4, 4, -4, 4);
                    Walls(b, -4, 4, -4, 4, 0, H1, wall);
                    Walls(b, -4, 4, -4, zU, H1, H1 + H2, wall);
                    b.Box(V(0, H1 + 0.125f, 0), V(8.3f, 0.25f, 8.3f), M.Roof);
                    b.Box(V(0, H1 + 0.5f, 4.025f), V(8.3f, 0.5f, 0.25f), trim);
                    b.Box(V(-4.025f, H1 + 0.5f, (zU + 4f) * 0.5f), V(0.25f, 0.5f, 4f - zU), trim);
                    b.Box(V(4.025f, H1 + 0.5f, (zU + 4f) * 0.5f), V(0.25f, 0.5f, 4f - zU), trim);
                    FlatRoof(b, -4, 4, -4, zU, H1 + H2, trim);
                    float dx = F(-2f, 2f);
                    Door(b, dx, 4f, 1f);
                    Window(b, dx > 0 ? -2.3f : 2.3f, 2.5f, 4f, 1f);
                    Door(b, 1.6f, zU, 1f, H1 + 0.25f, 1.0f, 2.1f);
                    Window(b, -1.8f, H1 + 1.7f, zU, 1f);
                    Window(b, -2f, 2.5f, -4f, -1f);
                    Window(b, 2f, 2.5f, -4f, -1f);
                    Window(b, -2f, H1 + 1.7f, -4f, -1f);
                    Window(b, 2f, H1 + 1.7f, -4f, -1f);
                    WindowX(b, 0f, 2.5f, 4f, 1f);
                    WindowX(b, -2.2f, H1 + 1.7f, -4f, -1f);
                    BeamEnds(b, -4, 4, 4, 1, H1 - 0.25f);
                    BeamEnds(b, -4, 4, -4, -1, H1 + H2 - 0.25f);
                    if (_r.NextDouble() < 0.6)
                    {
                        b.Box(V(-3.5f, H1 + 1.35f, 3.5f), V(0.14f, 2.2f, 0.14f), M.Wood);
                        b.Box(V(3.5f, H1 + 1.35f, 3.5f), V(0.14f, 2.2f, 0.14f), M.Wood);
                        b.Box(V(0f, H1 + 2.45f, 3.5f), V(7.4f, 0.14f, 0.14f), M.Wood);
                        b.Box(V(0f, H1 + 2.55f, (zU + 3.6f) * 0.5f), V(7.2f, 0.05f, 3.6f - zU), _r.NextDouble() < 0.5 ? M.Cloth : M.Straw);
                    }
                    fireLocal = V(0f, H1 + H2 + 0.25f, (-4f + zU) * 0.5f);
                    break;
                }
                case HouseType.Alta:
                {
                    float H = F(6.4f, 7.2f);
                    Plinth(b, -4, 4, -4, 4);
                    Walls(b, -4, 4, -4, 4, 0, H, wall);
                    b.Box(V(0, 3.3f, 0), V(8.12f, 0.22f, 8.12f), M.WoodDark);
                    FlatRoof(b, -4, 4, -4, 4, H, trim);
                    BeamEnds(b, -4, 4, 4, 1, H - 0.25f);
                    BeamEnds(b, -4, 4, -4, -1, H - 0.25f);
                    float dx = F(-2f, 2f);
                    Door(b, dx, 4f, 1f);
                    Window(b, dx > 0 ? -2.3f : 2.3f, 2.4f, 4f, 1f);
                    float xb = dx > 0 ? -1.6f : 1.6f;
                    Window(b, -xb, H - 1.6f, 4f, 1f);
                    Balcony(b, xb, 3.45f);
                    Window(b, -2f, 2.4f, -4f, -1f);
                    Window(b, 2f, 2.4f, -4f, -1f);
                    Window(b, -2f, H - 1.6f, -4f, -1f);
                    Window(b, 2f, H - 1.6f, -4f, -1f);
                    WindowX(b, 0f, 2.4f, 4f, 1f);
                    WindowX(b, 0f, H - 1.6f, 4f, 1f);
                    WindowX(b, 0f, H - 1.6f, -4f, -1f);
                    if (_r.NextDouble() < 0.5)
                    {
                        Walls(b, 0.8f, 3.2f, -3.2f, -0.8f, H + 0.25f, H + 2.1f, wall);
                        FlatRoof(b, 0.8f, 3.2f, -3.2f, -0.8f, H + 2.1f, trim, false, false, false, false);
                        Door(b, 2f, -0.8f, 1f, H + 0.25f, 0.9f, 1.6f);
                    }
                    else RoofClutter(b, -3.4f, 3.4f, -3.4f, 3.4f, H + 0.25f);
                    fireLocal = V(-1.5f, H + 0.25f, 1f);
                    break;
                }
                case HouseType.Loja:
                {
                    float H = F(3.5f, 3.9f);
                    Plinth(b, -4, 4, -4, 4);
                    Walls(b, -4, 4, -4, 4, 0, H, wall);
                    FlatRoof(b, -4, 4, -4, 4, H, trim);
                    BeamEnds(b, -4, 4, -4, -1, H - 0.25f);
                    Door(b, 0f, 4f, 1f, 0f, 2.2f, 2.4f);
                    Window(b, -2f, 2.5f, -4f, -1f);
                    Window(b, 2f, 2.5f, -4f, -1f);
                    WindowX(b, 0f, 2.5f, 4f, 1f);
                    M cloth = _r.NextDouble() < 0.5 ? M.Cloth : M.ClothBlue;
                    b.Box(V(-3.7f, 1.35f, 6.1f), V(0.14f, 2.7f, 0.14f), M.Wood);
                    b.Box(V(3.7f, 1.35f, 6.1f), V(0.14f, 2.7f, 0.14f), M.Wood);
                    b.Box(V(0f, 2.72f, 6.1f), V(7.6f, 0.14f, 0.14f), M.Wood);
                    b.Box(V(0f, 2.95f, 5.08f), V(7.8f, 0.05f, 2.3f), cloth, Quaternion.Euler(12f, 0f, 0f));
                    b.Box(V(-2.4f, 0.5f, 4.7f), V(2.2f, 1.0f, 0.8f), M.Wood);
                    Amphora(b, V(-3f, 1.0f, 4.7f), 0.5f);
                    Basket(b, V(-1.9f, 1.0f, 4.7f), 0.6f);
                    Amphora(b, V(2.8f, 0f, 5.2f), 0.9f);
                    Amphora(b, V(3.2f, 0f, 4.6f), 0.8f);
                    Basket(b, V(2.1f, 0f, 5.6f), 1f);
                    RoofClutter(b, -3.4f, 3.4f, -3.4f, 3.4f, H + 0.25f);
                    fireLocal = V(0f, H + 0.25f, -1f);
                    break;
                }
                case HouseType.Queimada:
                {
                    float hmax = F(2.6f, 3.8f);
                    Plinth(b, -4, 4, -4, 4);
                    b.Box(V(0, 0.5f, 0), V(7.4f, 0.12f, 7.4f), M.Charred);
                    for (int side = 0; side < 4; side++)
                    {
                        for (int s = 0; s < 4; s++)
                        {
                            if (side == 0 && s == 1) continue; // vão da porta
                            float h = F(0.8f, hmax);
                            float t = -3f + s * 2f;
                            Vector3 c, sz;
                            switch (side)
                            {
                                case 0: c = V(t, h * 0.5f, 3.75f); sz = V(2.02f, h, 0.5f); break;
                                case 1: c = V(t, h * 0.5f, -3.75f); sz = V(2.02f, h, 0.5f); break;
                                case 2: c = V(-3.75f, h * 0.5f, t); sz = V(0.5f, h, 2.02f); break;
                                default: c = V(3.75f, h * 0.5f, t); sz = V(0.5f, h, 2.02f); break;
                            }
                            b.Box(c, sz, M.MudBrick);
                            b.Box(c + V(0, h * 0.5f + 0.04f, 0), V(sz.x + 0.02f, 0.08f, sz.z + 0.02f), M.Charred);
                        }
                    }
                    for (int i = 0; i < 3; i++)
                        b.BoxBetween(V(F(-3f, 3f), F(1.5f, hmax), F(-3f, 3f)), V(F(-3f, 3f), 0.6f, F(-3f, 3f)), 0.22f, 0.22f, M.Charred);
                    for (int i = 0; i < 12; i++)
                    {
                        float s = F(0.3f, 0.8f);
                        b.Box(V(F(-2.8f, 2.8f), 0.5f + s * 0.35f, F(-2.8f, 2.8f)), V(s, s * 0.8f, s * F(0.8f, 1.3f)),
                            _r.NextDouble() < 0.5 ? M.MudBrick : M.Charred, Quaternion.Euler(F(-20f, 20f), F(0f, 360f), F(-20f, 20f)));
                    }
                    fireLocal = V(0f, 0.6f, 0f);
                    break;
                }
            }
            return b;
        }

        // =====================================================================================
        // Adereços
        // =====================================================================================
        public static MeshBuilder Crate()
        {
            var b = new MeshBuilder();
            b.Box(V(0, 0.45f, 0), V(0.9f, 0.9f, 0.9f), M.Wood);
            foreach (float x in new[] { -0.42f, 0.42f })
            foreach (float z in new[] { -0.42f, 0.42f })
                b.Box(V(x, 0.45f, z), V(0.1f, 0.92f, 0.1f), M.WoodDark);
            b.Box(V(0, 0.88f, 0), V(0.94f, 0.06f, 0.94f), M.WoodDark);
            return b;
        }

        public static MeshBuilder AmphoraProp()
        {
            var b = new MeshBuilder();
            Amphora(b, Vector3.zero, 1f);
            return b;
        }

        public static MeshBuilder BasketProp()
        {
            var b = new MeshBuilder();
            Basket(b, Vector3.zero, 1.2f);
            b.Sphere(V(0, 0.38f, 0), 0.38f, 0.12f, 8, M.Terracotta);
            return b;
        }

        public static MeshBuilder Table()
        {
            _r = new System.Random(31);
            var b = new MeshBuilder();
            b.Box(V(0, 0.78f, 0), V(3.6f, 0.1f, 1.1f), M.Wood);
            foreach (float x in new[] { -1.6f, 1.6f })
            foreach (float z in new[] { -0.42f, 0.42f })
                b.Box(V(x, 0.37f, z), V(0.12f, 0.74f, 0.12f), M.WoodDark);
            for (int i = 0; i < 4; i++)
                b.Lathe(V(-1.3f + i * 0.85f, 0.83f, F(-0.2f, 0.2f)), Id,
                    new[] { V2(0.05f, 0f), V2(0.18f, 0.08f), V2(0.2f, 0.12f), V2(0.16f, 0.1f), V2(0f, 0.04f) }, 10, M.Bronze, false, false);
            Amphora(b, V(1.2f, 0.83f, 0.15f), 0.55f);
            Amphora(b, V(-0.4f, 1.05f, 0.1f), 0.5f, M.Terracotta, Quaternion.Euler(0f, 30f, 90f));
            b.Sphere(V(0.4f, 0.92f, -0.2f), 0.16f, 0.09f, 8, M.Straw);
            b.Sphere(V(-1.0f, 0.92f, 0.25f), 0.14f, 0.08f, 8, M.Straw);
            return b;
        }

        public static MeshBuilder Bench()
        {
            var b = new MeshBuilder();
            b.Box(V(0, 0.45f, 0), V(3.2f, 0.08f, 0.4f), M.Wood);
            foreach (float x in new[] { -1.4f, 1.4f }) b.Box(V(x, 0.22f, 0), V(0.1f, 0.44f, 0.34f), M.WoodDark);
            return b;
        }

        public static MeshBuilder Cart(bool hay)
        {
            var b = new MeshBuilder();
            b.Box(V(0, 0.85f, 0), V(1.8f, 0.15f, 3.2f), M.Wood);
            b.Box(V(-0.88f, 1.15f, 0), V(0.08f, 0.5f, 3.2f), M.Wood);
            b.Box(V(0.88f, 1.15f, 0), V(0.08f, 0.5f, 3.2f), M.Wood);
            b.Box(V(0, 1.15f, -1.56f), V(1.7f, 0.5f, 0.08f), M.Wood);
            b.CylinderBetween(V(-1.05f, 0.7f, 0), V(-0.92f, 0.7f, 0), 0.7f, 0.7f, 14, M.WoodDark);
            b.CylinderBetween(V(0.92f, 0.7f, 0), V(1.05f, 0.7f, 0), 0.7f, 0.7f, 14, M.WoodDark);
            b.CylinderBetween(V(-0.95f, 0.7f, 0), V(0.95f, 0.7f, 0), 0.07f, 0.07f, 6, M.WoodDark);
            b.BoxBetween(V(-0.4f, 0.8f, 1.5f), V(-0.4f, 0.45f, 3.6f), 0.1f, 0.1f, M.Wood);
            b.BoxBetween(V(0.4f, 0.8f, 1.5f), V(0.4f, 0.45f, 3.6f), 0.1f, 0.1f, M.Wood);
            if (hay) b.Sphere(V(0, 1.1f, -0.1f), 0.95f, 0.55f, 10, M.Straw);
            else
            {
                Amphora(b, V(-0.4f, 0.93f, -0.8f), 0.7f);
                Amphora(b, V(0.35f, 0.93f, -0.5f), 0.65f);
                b.Box(V(0.2f, 1.33f, 0.7f), V(0.8f, 0.8f, 0.8f), M.Wood, Yaw(12f));
            }
            return b;
        }

        public static MeshBuilder Stall(M cloth)
        {
            var b = new MeshBuilder();
            foreach (float x in new[] { -1.4f, 1.4f })
            foreach (float z in new[] { -0.9f, 0.9f })
                b.Box(V(x, 1.2f, z), V(0.12f, 2.4f, 0.12f), M.Wood);
            b.Box(V(0, 2.42f, 0), V(3.3f, 0.05f, 2.4f), cloth, Quaternion.Euler(-10f, 0f, 0f));
            b.Box(V(0, 0.5f, 0.7f), V(2.8f, 1.0f, 0.6f), M.Wood);
            Amphora(b, V(-0.9f, 1.0f, 0.7f), 0.45f);
            Basket(b, V(0.2f, 1.0f, 0.7f), 0.5f);
            Basket(b, V(0.9f, 1.0f, 0.7f), 0.5f);
            b.Box(V(-0.8f, 0.4f, -0.5f), V(0.8f, 0.8f, 0.8f), M.Wood);
            Amphora(b, V(0.8f, 0f, -0.5f), 0.8f);
            return b;
        }

        public static MeshBuilder Brazier()
        {
            var b = new MeshBuilder();
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                b.BoxBetween(V(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f), V(Mathf.Cos(a) * 0.22f, 1.0f, Mathf.Sin(a) * 0.22f), 0.06f, 0.06f, M.Bronze);
            }
            b.Lathe(V(0, 0.95f, 0), Id, new[] { V2(0.1f, 0f), V2(0.38f, 0.08f), V2(0.52f, 0.3f), V2(0.48f, 0.33f), V2(0.32f, 0.17f), V2(0f, 0.14f) }, 14, M.Bronze, false, false);
            b.Sphere(V(0, 1.12f, 0), 0.34f, 0.08f, 8, M.Charred);
            return b;
        }

        public static MeshBuilder TorchPole()
        {
            var b = new MeshBuilder();
            b.CylinderBetween(V(0, 0, 0), V(0, 2.2f, 0), 0.07f, 0.05f, 6, M.WoodDark);
            b.Lathe(V(0, 2.1f, 0), Id, new[] { V2(0.05f, 0f), V2(0.14f, 0.15f), V2(0.15f, 0.35f), V2(0.1f, 0.4f) }, 8, M.Charred, true, false);
            return b;
        }

        public static MeshBuilder Rubble(int seed, float radius, int count)
        {
            _r = new System.Random(seed);
            var b = new MeshBuilder();
            b.Sphere(V(0, 0, 0), radius * 0.85f, radius * 0.42f, 12, M.Roof);
            M[] mats = { M.MudBrick, M.Stone, M.Charred, M.MudBrick, M.Plaster };
            for (int i = 0; i < count; i++)
            {
                float a = F(0f, Mathf.PI * 2f), d = Mathf.Sqrt(F(0f, 1f)) * radius;
                float s = F(0.3f, 0.9f) * (1f - d / radius * 0.5f);
                float y = (1f - d / radius) * radius * 0.4f + s * 0.25f;
                b.Box(V(Mathf.Cos(a) * d, y, Mathf.Sin(a) * d), V(s * F(0.8f, 1.5f), s * F(0.6f, 1f), s * F(0.8f, 1.4f)),
                    mats[_r.Next(mats.Length)], Quaternion.Euler(F(-25f, 25f), F(0f, 360f), F(-25f, 25f)));
            }
            for (int i = 0; i < 3; i++)
                b.BoxBetween(V(F(-radius, radius) * 0.6f, F(0.3f, radius * 0.6f), F(-radius, radius) * 0.6f),
                    V(F(-radius, radius), 0.1f, F(-radius, radius)), 0.2f, 0.2f, M.Charred);
            return b;
        }

        /// <summary>Barricada de ~6 m de largura (eixo X local).</summary>
        public static MeshBuilder Barricade()
        {
            _r = new System.Random(77);
            var b = new MeshBuilder();
            for (int i = 0; i < 9; i++)
                b.Box(V(F(-2.8f, 2.8f), 0.45f + (i % 3 == 0 ? 0.9f : 0f), F(-0.6f, 0.6f)), V(0.9f, 0.9f, 0.9f), M.Wood, Yaw(F(-20f, 20f)));
            for (int i = 0; i < 6; i++)
            {
                float x = -2.7f + i * 1.08f;
                b.BoxBetween(V(x, 0f, -1.4f), V(x + F(-0.3f, 0.3f), 2.2f, 0.2f), 0.25f, 0.08f, M.WoodDark);
            }
            for (int i = 0; i < 7; i++)
            {
                float x = -3f + i;
                b.BoxBetween(V(x, 0.2f, 0.6f), V(x + F(-0.2f, 0.2f), 1.6f, 2.0f), 0.14f, 0.14f, M.Wood);
            }
            b.Box(V(1.5f, 0.9f, -0.9f), V(3.4f, 1.6f, 0.1f), M.Wood, Yaw(8f));
            Amphora(b, V(-2.2f, 0.3f, -1.2f), 0.9f, M.Terracotta, Quaternion.Euler(80f, 30f, 0f));
            return b;
        }

        static void StatueFigure(MeshBuilder b, Vector3 p, float s, M fig, bool warrior)
        {
            b.Lathe(p, Id, new[]
            {
                V2(0.32f * s, 0f), V2(0.36f * s, 0.3f * s), V2(0.3f * s, 1.0f * s), V2(0.27f * s, 1.45f * s),
                V2(0.34f * s, 1.7f * s), V2(0.3f * s, 1.95f * s), V2(0.12f * s, 2.05f * s)
            }, 12, fig, true, true);
            b.Sphere(p + V(0, 2.25f * s, 0), 0.17f * s, 0.2f * s, 10, fig);
            if (warrior)
            {
                b.Sphere(p + V(0, 2.32f * s, 0), 0.19f * s, 0.15f * s, 10, fig);
                b.Box(p + V(0, 2.55f * s, 0), V(0.06f * s, 0.22f * s, 0.5f * s), M.Cloth);
                b.BoxBetween(p + V(0.3f * s, 1.85f * s, 0), p + V(0.5f * s, 2.3f * s, 0.15f * s), 0.12f * s, 0.12f * s, fig);
                b.CylinderBetween(p + V(0.5f * s, 0.1f * s, 0.2f * s), p + V(0.5f * s, 3.4f * s, 0.12f * s), 0.03f * s, 0.03f * s, 6, M.WoodDark);
                b.CylinderBetween(p + V(-0.36f * s, 1.4f * s, 0), p + V(-0.46f * s, 1.4f * s, 0), 0.5f * s, 0.5f * s, 14, fig);
            }
            else
            {
                b.BoxBetween(p + V(0.28f * s, 1.8f * s, 0), p + V(0.3f * s, 1.3f * s, 0.38f * s), 0.1f * s, 0.1f * s, fig);
                b.BoxBetween(p + V(-0.28f * s, 1.8f * s, 0), p + V(-0.3f * s, 1.3f * s, 0.38f * s), 0.1f * s, 0.1f * s, fig);
                b.Lathe(p + V(0, 1.25f * s, 0.42f * s), Id,
                    new[] { V2(0.04f * s, 0f), V2(0.2f * s, 0.08f * s), V2(0.22f * s, 0.12f * s), V2(0f, 0.06f * s) }, 10, M.Gold, false, false);
            }
        }

        public static MeshBuilder Statue(bool bronze)
        {
            var b = new MeshBuilder();
            b.Box(V(0, 0.08f, 0), V(1.6f, 0.16f, 1.6f), M.Marble);
            b.Box(V(0, 0.8f, 0), V(1.3f, 1.3f, 1.3f), M.Marble);
            b.Box(V(0, 1.5f, 0), V(1.5f, 0.15f, 1.5f), M.Marble);
            StatueFigure(b, V(0, 1.57f, 0), 1f, bronze ? M.Bronze : M.Marble, bronze);
            return b;
        }

        public static MeshBuilder WeaponRack()
        {
            var b = new MeshBuilder();
            b.Box(V(-1.1f, 0.9f, 0), V(0.12f, 1.8f, 0.12f), M.Wood);
            b.Box(V(1.1f, 0.9f, 0), V(0.12f, 1.8f, 0.12f), M.Wood);
            b.Box(V(0, 1.6f, 0), V(2.4f, 0.1f, 0.12f), M.Wood);
            b.Box(V(0, 0.3f, 0), V(2.4f, 0.1f, 0.3f), M.Wood);
            for (int i = 0; i < 5; i++)
            {
                float x = -0.9f + i * 0.45f;
                b.CylinderBetween(V(x, 0.3f, 0.08f), V(x + 0.05f, 2.6f, -0.05f), 0.025f, 0.025f, 5, M.WoodDark);
                b.Lathe(V(x + 0.05f, 2.6f, -0.05f), Id, new[] { V2(0.05f, 0f), V2(0f, 0.25f) }, 6, M.Bronze, false, false);
            }
            b.CylinderBetween(V(-0.6f, 0.65f, 0.3f), V(-0.62f, 0.7f, 0.42f), 0.5f, 0.5f, 14, M.Bronze);
            b.CylinderBetween(V(0.6f, 0.65f, 0.3f), V(0.62f, 0.7f, 0.42f), 0.5f, 0.5f, 14, M.Bronze);
            return b;
        }

        public static MeshBuilder SmallAltar()
        {
            var b = new MeshBuilder();
            b.Box(V(0, 0.06f, 0), V(2.6f, 0.12f, 1.6f), M.Marble);
            b.Box(V(0, 0.6f, 0), V(2.2f, 1.0f, 1.2f), M.Marble);
            b.Box(V(0, 1.16f, 0), V(2.5f, 0.12f, 1.4f), M.Marble);
            b.Box(V(-1.1f, 1.37f, 0), V(0.25f, 0.3f, 1.2f), M.Marble);
            b.Box(V(1.1f, 1.37f, 0), V(0.25f, 0.3f, 1.2f), M.Marble);
            b.Sphere(V(0, 1.22f, 0), 0.6f, 0.1f, 10, M.Charred);
            return b;
        }

        // =====================================================================================
        // Muralhas
        // =====================================================================================
        static void WallSegment(MeshBuilder b, Vector2 a, Vector2 c, float lowH, float topH, float thick, M low, M up, Vector2 center)
        {
            Vector2 d = c - a;
            float L = d.magnitude;
            Vector2 dir = d / L;
            Vector2 mid = (a + c) * 0.5f;
            var outward = new Vector2(dir.y, -dir.x);
            if (Vector2.Dot(outward, mid - center) < 0f) outward = -outward;
            Quaternion r = Yaw(Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg);
            var m3 = V(mid.x, 0f, mid.y);
            var o3 = V(outward.x, 0f, outward.y);
            var d3 = V(dir.x, 0f, dir.y);
            const float ext = 1.2f;
            b.Box(m3 + Vector3.up * ((lowH - 2f) * 0.5f), V(thick + 1.5f, lowH + 2f, L + ext), low, r);
            b.Box(m3 + Vector3.up * ((lowH + topH) * 0.5f), V(thick, topH - lowH, L + ext * 0.6f), up, r);
            // ameias no lado externo + mureta no lado interno
            int n = Mathf.Max(1, Mathf.RoundToInt(L / 2.2f));
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n - 0.5f;
                b.Box(m3 + d3 * (t * L) + o3 * (thick * 0.5f - 0.35f) + Vector3.up * (topH + 0.7f), V(0.7f, 1.4f, 1.2f), up, r);
            }
            b.Box(m3 - o3 * (thick * 0.5f - 0.2f) + Vector3.up * (topH + 0.35f), V(0.4f, 0.7f, L + ext * 0.6f), up, r);
            // faixa de vigas de madeira (construção de Troia VI)
            b.Box(m3 + Vector3.up * (lowH + 0.15f), V(thick + 0.12f, 0.3f, L + ext * 0.6f), M.WoodDark, r);
        }

        static void Tower(MeshBuilder b, Vector2 pos, float yaw, float w, float d, float h, float lowH, M low, M up)
        {
            Quaternion r = Yaw(yaw);
            var p = V(pos.x, 0f, pos.y);
            b.Box(p + Vector3.up * ((lowH - 2f) * 0.5f), V(w + 1f, lowH + 2f, d + 1f), low, r);
            b.Box(p + Vector3.up * ((lowH + h) * 0.5f), V(w, h - lowH, d), up, r);
            b.Box(p + Vector3.up * (lowH + 0.15f), V(w + 0.12f, 0.3f, d + 0.12f), M.WoodDark, r);
            int nx = Mathf.Max(2, Mathf.RoundToInt(w / 2.2f)), nz = Mathf.Max(2, Mathf.RoundToInt(d / 2.2f));
            for (int i = 0; i < nx; i++)
            {
                float x = -w * 0.5f + (i + 0.5f) * w / nx;
                b.Box(p + r * V(x, h + 0.7f, d * 0.5f - 0.35f), V(1.1f, 1.4f, 0.7f), up, r);
                b.Box(p + r * V(x, h + 0.7f, -d * 0.5f + 0.35f), V(1.1f, 1.4f, 0.7f), up, r);
            }
            for (int i = 0; i < nz; i++)
            {
                float z = -d * 0.5f + (i + 0.5f) * d / nz;
                b.Box(p + r * V(w * 0.5f - 0.35f, h + 0.7f, z), V(0.7f, 1.4f, 1.1f), up, r);
                b.Box(p + r * V(-w * 0.5f + 0.35f, h + 0.7f, z), V(0.7f, 1.4f, 1.1f), up, r);
            }
            // seteiras
            foreach (float s in new[] { -0.25f, 0.25f })
            {
                b.Box(p + r * V(w * s, h - 3f, d * 0.5f), V(0.3f, 1.3f, 0.1f), M.Dark, r);
                b.Box(p + r * V(w * s, h - 3f, -d * 0.5f), V(0.3f, 1.3f, 0.1f), M.Dark, r);
                b.Box(p + r * V(w * 0.5f, h - 3f, d * s), V(0.1f, 1.3f, 0.3f), M.Dark, r);
                b.Box(p + r * V(-w * 0.5f, h - 3f, d * s), V(0.1f, 1.3f, 0.3f), M.Dark, r);
            }
        }

        public static Vector2 OuterWallPoint(int i, int n)
        {
            float t = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / n;
            return new Vector2(TroiaLayout.Rx * Mathf.Cos(t), TroiaLayout.Rz * Mathf.Sin(t));
        }

        /// <summary>Muralha externa em blocos (o vértice 0 é o portão sul, tratado à parte).</summary>
        public static List<MeshBuilder> OuterWall()
        {
            const int N = 38;
            var P = new Vector2[N];
            for (int i = 0; i < N; i++) P[i] = OuterWallPoint(i, N);
            var chunks = new List<MeshBuilder>();
            MeshBuilder b = null;
            for (int i = 1; i <= 36; i++)
            {
                if ((i - 1) % 6 == 0)
                {
                    b = new MeshBuilder();
                    chunks.Add(b);
                }
                WallSegment(b, P[i], P[i + 1], 6f, 12f, 4.5f, M.Stone, M.MudBrick, Vector2.zero);
                if (i % 2 == 0)
                {
                    Vector2 tan = P[i + 1] - P[i - 1];
                    var nrm = new Vector2(P[i].x / (TroiaLayout.Rx * TroiaLayout.Rx), P[i].y / (TroiaLayout.Rz * TroiaLayout.Rz)).normalized;
                    Tower(b, P[i] + nrm * 2.5f, Mathf.Atan2(tan.x, tan.y) * Mathf.Rad2Deg, 9f, 9f, 16f, 6f, M.Stone, M.MudBrick);
                }
            }
            var g = new MeshBuilder();
            WallSegment(g, new Vector2(13f, -135f), P[1], 6f, 12f, 4.5f, M.Stone, M.MudBrick, Vector2.zero);
            WallSegment(g, P[37], new Vector2(-13f, -135f), 6f, 12f, 4.5f, M.Stone, M.MudBrick, Vector2.zero);
            chunks.Add(g);
            return chunks;
        }

        /// <summary>Portas Ceias: torres, verga e passagem (as folhas do portão são objetos separados).</summary>
        public static MeshBuilder ScaeanGate()
        {
            var b = new MeshBuilder();
            foreach (float sx in new[] { -1f, 1f })
                Tower(b, new Vector2(9f * sx, -135f), 0f, 9f, 12f, 18f, 7f, M.Stone, M.MudBrick);
            b.Box(V(0, 10.75f, -135f), V(9.4f, 4.5f, 6f), M.Stone);
            for (int i = 0; i < 4; i++)
            {
                float x = -3.3f + i * 2.2f;
                b.Box(V(x, 13.7f, -137.6f), V(1.2f, 1.4f, 0.7f), M.MudBrick);
                b.Box(V(x, 13.35f, -132.4f), V(1.2f, 0.7f, 0.4f), M.MudBrick);
            }
            for (int i = 0; i < 5; i++) b.Box(V(0, 8.4f, -137.2f + i * 1.2f), V(9.2f, 0.3f, 0.3f), M.WoodDark);
            b.Box(V(0, 0.05f, -135f), V(9f, 0.12f, 6.5f), M.Stone);
            // estandartes troianos na face interna das torres
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box(V(9f * sx, 10f, -128.97f), V(2.2f, 4f, 0.06f), M.Cloth);
                b.Box(V(9f * sx, 12.1f, -128.9f), V(2.6f, 0.15f, 0.15f), M.WoodDark);
            }
            return b;
        }

        /// <summary>Folha do portão: dobradiça na origem, estende-se para +X (ou -X se mirror).</summary>
        public static MeshBuilder GateLeaf(bool mirror)
        {
            float s = mirror ? -1f : 1f;
            var b = new MeshBuilder();
            b.Box(V(s * 2.25f, 4.25f, 0), V(4.5f, 8.5f, 0.4f), M.Wood);
            foreach (float y in new[] { 1.2f, 4.25f, 7.3f })
                b.Box(V(s * 2.25f, y, 0), V(4.4f, 0.28f, 0.48f), M.Bronze);
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    float x = s * (0.6f + i * 1.1f), y = 0.6f + j * 1.85f + 0.3f;
                    b.Box(V(x, y, 0.22f), V(0.14f, 0.14f, 0.08f), M.Bronze);
                    b.Box(V(x, y, -0.22f), V(0.14f, 0.14f, 0.08f), M.Bronze);
                }
            }
            return b;
        }

        public static MeshBuilder GateBar()
        {
            var b = new MeshBuilder();
            b.Box(V(0, 0, 0), V(9.6f, 0.45f, 0.45f), M.WoodDark);
            foreach (float x in new[] { -3.2f, -1f, 1f, 3.2f })
                b.Box(V(x, 0, -0.1f), V(0.25f, 0.75f, 0.6f), M.Bronze);
            return b;
        }

        public static List<MeshBuilder> CitadelWall()
        {
            const int N = 30;
            Vector2 C = TroiaLayout.CitadelC;
            float R = TroiaLayout.CitadelR;
            var P = new Vector2[N];
            for (int i = 0; i < N; i++)
            {
                float a = (-90f + i * 12f) * Mathf.Deg2Rad;
                P[i] = C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R;
            }
            var chunks = new List<MeshBuilder>();
            MeshBuilder b = null;
            for (int i = 1; i <= 28; i++)
            {
                if ((i - 1) % 7 == 0)
                {
                    b = new MeshBuilder();
                    chunks.Add(b);
                }
                WallSegment(b, P[i], P[i + 1], 8f, 14f, 3f, M.Stone, M.Stone, C);
                if (i % 3 == 0)
                {
                    Vector2 tan = P[(i + 1) % N] - P[i - 1];
                    Vector2 nrm = (P[i] - C).normalized;
                    Tower(b, P[i] + nrm * 1.5f, Mathf.Atan2(tan.x, tan.y) * Mathf.Rad2Deg, 6f, 6f, 18f, 8f, M.Stone, M.Stone);
                }
            }
            return chunks;
        }

        public static MeshBuilder CitadelGate()
        {
            var b = new MeshBuilder();
            foreach (float sx in new[] { -1f, 1f })
                Tower(b, new Vector2(8.5f * sx, 51f), 0f, 6f, 8f, 19f, 8f, M.Stone, M.Stone);
            b.Box(V(0, 16f, 50.5f), V(11.4f, 3f, 3.2f), M.Stone);
            for (int i = 0; i < 5; i++) b.Box(V(-4.4f + i * 2.2f, 18.2f, 49.2f), V(1.1f, 1.4f, 0.6f), M.Stone);
            // relevo dos leões sobre a verga
            b.Box(V(0, 18.3f, 48.75f), V(4.2f, 2.2f, 0.4f), M.Stone);
            b.Cylinder(V(0, 17.3f, 48.45f), 0.2f, 0.28f, 2.0f, 8, M.Marble);
            b.Box(V(-1.15f, 18.1f, 48.5f), V(1.3f, 1.5f, 0.25f), M.Marble, Quaternion.Euler(0, 0, -8f));
            b.Box(V(1.15f, 18.1f, 48.5f), V(1.3f, 1.5f, 0.25f), M.Marble, Quaternion.Euler(0, 0, 8f));
            // folhas do portão abertas para dentro (já arrombado)
            const float floor = TroiaLayout.CitadelH;
            foreach (float sx in new[] { -1f, 1f })
            {
                var hinge = V(5.4f * sx, floor, 51.8f);
                Quaternion q = Yaw(sx < 0 ? -100f : 100f);
                b.Box(hinge + q * V(-sx * 2.6f, 3.2f, 0f), V(5.2f, 6.4f, 0.3f), M.WoodDark, q);
                foreach (float y in new[] { 1f, 3.2f, 5.4f })
                    b.Box(hinge + q * V(-sx * 2.6f, y, 0f), V(5.1f, 0.25f, 0.38f), M.Bronze, q);
            }
            return b;
        }

        public static MeshBuilder RampWalls()
        {
            var b = new MeshBuilder();
            float z0 = TroiaLayout.RampZ0, z1 = TroiaLayout.RampZ1, h = TroiaLayout.CitadelH;
            float len = z1 - z0;
            float ang = Mathf.Atan2(h, len) * Mathf.Rad2Deg;
            float slen = Mathf.Sqrt(len * len + h * h);
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Wedge(V(5.75f * sx, 0f, (z0 + z1) * 0.5f), 1.5f, h, len, M.Stone, Id);
                b.Box(V(5.55f * sx, h * 0.5f + 0.45f, (z0 + z1) * 0.5f), V(0.6f, 1.4f, slen + 0.6f), M.Stone, Quaternion.Euler(-ang, 0f, 0f));
                for (int k = 0; k <= 4; k++)
                {
                    float z = z0 + k * 7.5f;
                    float y = h * (z - z0) / len;
                    b.Box(V(5.55f * sx, y + 0.75f, z), V(0.9f, 1.7f, 0.9f), M.Stone);
                }
            }
            return b;
        }

        // =====================================================================================
        // Marcos
        // =====================================================================================
        // ---------------------------------------------------------------- Cavalo de Troia
        // Tabela do corpo: (z, y do centro, raio lateral, raio vertical). Frente do cavalo em +Z.
        static readonly float[,] HorseBody =
        {
            { -5.65f, 7.75f, 0.3f, 0.35f }, { -5.3f, 7.7f, 1.2f, 1.3f }, { -4.75f, 7.55f, 1.85f, 1.95f },
            { -3.8f, 7.4f, 2.1f, 2.15f }, { -2.4f, 7.25f, 2.2f, 2.2f }, { -0.6f, 7.05f, 2.25f, 2.3f },
            { 1.2f, 7.1f, 2.2f, 2.3f }, { 2.6f, 7.4f, 2.0f, 2.35f }, { 3.6f, 7.85f, 1.7f, 2.2f },
            { 4.3f, 8.4f, 1.25f, 1.75f }, { 4.7f, 8.9f, 0.55f, 0.85f }
        };

        static void BodyAt(float z, out float y, out float rx, out float ry)
        {
            int n = HorseBody.GetLength(0);
            int i = 0;
            while (i < n - 2 && z > HorseBody[i + 1, 0]) i++;
            float t = Mathf.Clamp01((z - HorseBody[i, 0]) / (HorseBody[i + 1, 0] - HorseBody[i, 0]));
            y = Mathf.Lerp(HorseBody[i, 1], HorseBody[i + 1, 1], t);
            rx = Mathf.Lerp(HorseBody[i, 2], HorseBody[i + 1, 2], t);
            ry = Mathf.Lerp(HorseBody[i, 3], HorseBody[i + 1, 3], t);
        }

        static void Loft(MeshBuilder b, int seg, M m, params (Vector3 c, Vector2 r)[] rings)
        {
            var cs = new Vector3[rings.Length];
            var rs = new Vector2[rings.Length];
            for (int i = 0; i < rings.Length; i++) { cs[i] = rings[i].c; rs[i] = rings[i].r; }
            b.Loft(cs, rs, seg, m);
        }

        static (Vector3, Vector2) R(float x, float y, float z, float rx, float ry) => (V(x, y, z), V2(rx, ry));

        /// <summary>Faixa (anel curto) ao redor do corpo do cavalo na posição z.</summary>
        static void BodyBand(MeshBuilder b, float z, float width, float extra, M m, bool studs)
        {
            BodyAt(z - width * 0.5f, out float y0, out float rx0, out float ry0);
            BodyAt(z + width * 0.5f, out float y1, out float rx1, out float ry1);
            b.Loft(new[] { V(0, y0, z - width * 0.5f), V(0, y1, z + width * 0.5f) },
                new[] { V2(rx0 + extra, ry0 + extra), V2(rx1 + extra, ry1 + extra) }, 24, m, false, false);
            if (!studs) return;
            BodyAt(z, out float y, out float rx, out float ry);
            for (int k = 0; k < 16; k++)
            {
                float a = (-35f + k * 250f / 15f) * Mathf.Deg2Rad;
                b.Sphere(V(Mathf.Cos(a) * (rx + extra + 0.03f), y + Mathf.Sin(a) * (ry + extra + 0.03f), z), 0.065f, 0.065f, 6, M.Bronze);
            }
        }

        static void Wheel(MeshBuilder b, Vector3 c)
        {
            b.Push(c, Quaternion.Euler(0f, 0f, 90f));
            b.Torus(Vector3.zero, Id, 0.78f, 0.1f, 22, 6, M.WoodDark);
            b.Torus(Vector3.zero, Id, 0.86f, 0.035f, 22, 4, M.Bronze);
            b.Cylinder(V(0, -0.2f, 0), 0.19f, 0.19f, 0.4f, 10, M.Bronze);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                var d = V(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.BoxBetween(d * 0.16f, d * 0.72f, 0.08f, 0.08f, M.Wood);
            }
            b.Pop();
        }

        public static MeshBuilder Horse()
        {
            _r = new System.Random(1184);
            var b = new MeshBuilder();
            float[] sides = { -1f, 1f };
            const float deck = 1.28f;

            // ---------------- plataforma de pranchas sobre rodas
            foreach (float sx in sides) b.Box(V(sx * 2.1f, 0.95f, 0), V(0.35f, 0.42f, 12.4f), M.WoodDark);
            for (int i = 0; i < 7; i++) b.Box(V(0, 0.95f, -5.7f + i * 1.9f), V(5.2f, 0.32f, 0.3f), M.WoodDark);
            for (int i = 0; i < 11; i++)
            {
                float x = -2.6f + i * 0.52f;
                b.Box(V(x, 1.22f, F(-0.1f, 0.1f)), V(0.48f, 0.12f, 12.5f + F(-0.15f, 0.25f)), M.HorseWood);
            }
            foreach (float sx in sides)
            foreach (float sz in sides)
            {
                b.Box(V(sx * 2.62f, 1.1f, sz * 6.2f), V(0.3f, 0.5f, 0.3f), M.Bronze);
                b.Torus(V(sx * 2.2f, 1.0f, sz * 6.35f), Quaternion.Euler(90f, 0f, 0f), 0.18f, 0.045f, 12, 5, M.Bronze);
            }
            for (int i = 0; i < 3; i++)
            {
                float z = -4.4f + i * 4.4f;
                b.CylinderBetween(V(-3.25f, 0.85f, z), V(3.25f, 0.85f, z), 0.1f, 0.1f, 8, M.WoodDark);
                foreach (float sx in sides) Wheel(b, V(sx * 2.98f, 0.85f, z));
            }

            // ---------------- corpo de tábuas
            int nb = HorseBody.GetLength(0);
            var bc = new Vector3[nb];
            var br = new Vector2[nb];
            for (int i = 0; i < nb; i++) { bc[i] = V(0, HorseBody[i, 1], HorseBody[i, 0]); br[i] = V2(HorseBody[i, 2], HorseBody[i, 3]); }
            b.Loft(bc, br, 24, M.HorseWood);
            // músculos: paletas e ancas
            foreach (float sx in sides)
            {
                b.Sphere(V(sx * 1.45f, 7.05f, 2.75f), 0.95f, 1.35f, 12, M.HorseWood);
                b.Sphere(V(sx * 1.5f, 7.45f, -3.55f), 1.05f, 1.35f, 12, M.HorseWood);
            }
            // cavernas (costelas da estrutura) e cintas de bronze cravejadas
            foreach (float z in new[] { -4.2f, -1.5f, 1.0f, 3.2f }) BodyBand(b, z, 0.2f, 0.05f, M.WoodDark, false);
            foreach (float z in new[] { -2.9f, -0.25f, 2.2f }) BodyBand(b, z, 0.34f, 0.07f, M.Bronze, true);

            // ---------------- manta cerimonial sobre o lombo (festa troiana)
            {
                var mc = new List<Vector3>();
                var mr = new List<Vector2>();
                for (float z = -1.45f; z <= 1.46f; z += 0.725f)
                {
                    BodyAt(z, out float y, out float rx, out float ry);
                    mc.Add(V(0, y, z));
                    mr.Add(V2(rx + 0.12f, ry + 0.12f));
                }
                b.Loft(mc, mr, 18, M.Cloth, false, false, 22f, 158f);
                foreach (float aDeg in new[] { 22f, 158f })
                {
                    float a = aDeg * Mathf.Deg2Rad;
                    for (float z = -1.4f; z <= 1.41f; z += 0.28f)
                    {
                        BodyAt(z, out float y, out float rx, out float ry);
                        Vector3 p = V(Mathf.Cos(a) * (rx + 0.14f), y + Mathf.Sin(a) * (ry + 0.14f), z);
                        b.CylinderBetween(p, p + V(0, -0.28f, 0), 0.025f, 0.025f, 4, M.Gold);
                        b.Sphere(p + V(0, -0.32f, 0), 0.06f, 0.07f, 6, M.Gold);
                    }
                }
                foreach (float sx in sides)
                {
                    BodyAt(0f, out float y, out float rx, out float ry);
                    float a = (sx > 0 ? 55f : 125f) * Mathf.Deg2Rad;
                    Vector3 p = V(Mathf.Cos(a) * (rx + 0.13f), y + Mathf.Sin(a) * (ry + 0.13f), 0f);
                    Vector3 nrm = V(Mathf.Cos(a) / rx, Mathf.Sin(a) / ry, 0f).normalized;
                    b.CylinderBetween(p, p + nrm * 0.06f, 0.42f, 0.42f, 16, M.Gold);
                    b.CylinderBetween(p + nrm * 0.06f, p + nrm * 0.1f, 0.2f, 0.12f, 12, M.Bronze);
                }
            }

            // ---------------- pernas (tábuas), joelhos e cascos ferrados
            foreach (float sx in sides)
            {
                float x = sx * 1.05f;
                Loft(b, 14, M.HorseWood,
                    R(x, 6.4f, 3.1f, 0.75f, 0.85f), R(x, 5.2f, 3.3f, 0.55f, 0.68f), R(x, 4.25f, 3.4f, 0.43f, 0.52f),
                    R(x, 3.9f, 3.42f, 0.44f, 0.5f), R(x, 2.9f, 3.45f, 0.3f, 0.36f), R(x, 2.15f, 3.5f, 0.36f, 0.42f),
                    R(x, 1.8f, 3.62f, 0.33f, 0.38f), R(x, 1.65f, 3.68f, 0.38f, 0.43f));
                b.Sphere(V(x, 4.05f, 3.5f), 0.46f, 0.5f, 10, M.HorseWood);
                Loft(b, 14, M.WoodDark, R(x, 1.7f, 3.68f, 0.38f, 0.43f), R(x, deck + 0.05f, 3.76f, 0.5f, 0.56f));
                b.Torus(V(x, 1.72f, 3.69f), Id, 0.42f, 0.05f, 14, 5, M.Bronze);

                float xh = sx * 1.1f;
                Loft(b, 14, M.HorseWood,
                    R(xh, 6.8f, -3.4f, 0.95f, 1.1f), R(xh, 5.5f, -3.9f, 0.65f, 0.8f), R(xh, 4.3f, -4.35f, 0.42f, 0.55f),
                    R(xh, 3.9f, -4.3f, 0.38f, 0.45f), R(xh, 2.9f, -4.1f, 0.3f, 0.36f), R(xh, 2.15f, -3.95f, 0.36f, 0.42f),
                    R(xh, 1.8f, -3.86f, 0.33f, 0.38f), R(xh, 1.65f, -3.8f, 0.38f, 0.43f));
                b.Sphere(V(xh, 4.3f, -4.42f), 0.42f, 0.42f, 10, M.HorseWood);
                Loft(b, 14, M.WoodDark, R(xh, 1.7f, -3.8f, 0.38f, 0.43f), R(xh, deck + 0.05f, -3.74f, 0.5f, 0.56f));
                b.Torus(V(xh, 1.72f, -3.79f), Id, 0.42f, 0.05f, 14, 5, M.Bronze);
            }

            // ---------------- pescoço, cabeça e mandíbula
            var neck = new[]
            {
                R(0, 8.3f, 2.7f, 1.35f, 1.75f), R(0, 9.6f, 3.6f, 1.18f, 1.52f), R(0, 10.8f, 4.45f, 1.02f, 1.32f),
                R(0, 11.9f, 5.15f, 0.9f, 1.14f), R(0, 12.8f, 5.65f, 0.82f, 0.98f), R(0, 13.3f, 5.95f, 0.7f, 0.75f)
            };
            Loft(b, 20, M.HorseWood, neck);
            Loft(b, 18, M.HorseWood,
                R(0, 13.45f, 5.55f, 0.45f, 0.4f), R(0, 13.4f, 6.0f, 0.78f, 0.8f), R(0, 13.0f, 6.75f, 0.8f, 0.9f),
                R(0, 12.4f, 7.55f, 0.66f, 0.8f), R(0, 11.85f, 8.25f, 0.55f, 0.68f), R(0, 11.45f, 8.85f, 0.52f, 0.62f),
                R(0, 11.25f, 9.25f, 0.42f, 0.5f), R(0, 11.18f, 9.45f, 0.18f, 0.22f));
            Loft(b, 14, M.HorseWood, R(0, 12.55f, 6.6f, 0.62f, 0.55f), R(0, 12.05f, 7.3f, 0.55f, 0.45f), R(0, 11.55f, 7.95f, 0.36f, 0.3f));
            BodyBand(b, 4.0f, 0.3f, 0.0f, M.Bronze, false); // colar no peito

            // olhos, sobrancelhas, narinas e boca
            foreach (float sx in sides)
            {
                b.Sphere(V(sx * 0.74f, 12.85f, 6.95f), 0.21f, 0.21f, 10, M.Bronze);
                b.Sphere(V(sx * 0.9f, 12.85f, 7.02f), 0.1f, 0.11f, 8, M.Dark);
                b.BoxBetween(V(sx * 0.68f, 13.15f, 6.65f), V(sx * 0.8f, 13.08f, 7.25f), 0.12f, 0.12f, M.WoodDark);
                b.Sphere(V(sx * 0.26f, 11.32f, 9.32f), 0.1f, 0.13f, 8, M.Dark);
                b.BoxBetween(V(sx * 0.42f, 10.95f, 8.6f), V(sx * 0.24f, 10.97f, 9.35f), 0.04f, 0.04f, M.Dark);
                // orelhas
                Loft(b, 10, M.HorseWood, R(sx * 0.32f, 13.45f, 5.85f, 0.2f, 0.13f), R(sx * 0.4f, 14.0f, 5.75f, 0.17f, 0.11f), R(sx * 0.47f, 14.5f, 5.68f, 0.03f, 0.02f));
                // freio: argolas, faces e rédeas
                b.Torus(V(sx * 0.55f, 11.25f, 8.95f), Quaternion.Euler(0f, 0f, 90f), 0.14f, 0.035f, 12, 5, M.Bronze);
                b.BoxBetween(V(sx * 0.6f, 11.75f, 8.5f), V(sx * 0.8f, 13.25f, 6.2f), 0.07f, 0.14f, M.Bronze);
                Loft(b, 6, M.Rope, R(sx * 0.62f, 11.22f, 8.98f, 0.05f, 0.05f), R(sx * 0.98f, 10.45f, 8.05f, 0.05f, 0.05f),
                    R(sx * 1.18f, 9.85f, 6.7f, 0.05f, 0.05f), R(sx * 1.12f, 10.3f, 5.3f, 0.05f, 0.05f), R(sx * 0.95f, 10.95f, 4.55f, 0.05f, 0.05f));
            }
            Loft(b, 18, M.Bronze, R(0, 11.7f, 8.46f, 0.6f, 0.72f), R(0, 11.6f, 8.64f, 0.58f, 0.7f));   // focinheira
            Loft(b, 18, M.Bronze, R(0, 13.42f, 6.1f, 0.84f, 0.85f), R(0, 13.36f, 6.3f, 0.84f, 0.86f)); // testeira
            b.Sphere(V(0, 13.6f, 6.25f), 0.2f, 0.2f, 8, M.Gold);

            // ---------------- crina e topete de ripas
            for (int i = 0; i < 14; i++)
            {
                float t = i / 13f * (neck.Length - 1.001f);
                int k = Mathf.FloorToInt(t);
                float f = t - k;
                Vector3 c = Vector3.Lerp(neck[k].Item1, neck[k + 1].Item1, f);
                float ry = Mathf.Lerp(neck[k].Item2.y, neck[k + 1].Item2.y, f);
                Vector3 tg = (neck[k + 1].Item1 - neck[k].Item1).normalized;
                Vector3 up = (Vector3.up - Vector3.Dot(Vector3.up, tg) * tg).normalized;
                Vector3 crest = c + up * (ry - 0.1f);
                Vector3 dir = (up * 0.85f - tg * 0.45f).normalized;
                b.BoxBetween(crest - dir * 0.15f, crest + dir * F(0.75f, 1.05f), 0.14f, 0.5f, M.WoodDark);
            }
            for (int i = 0; i < 3; i++)
                b.BoxBetween(V(-0.15f + i * 0.15f, 13.6f, 5.95f), V(-0.2f + i * 0.2f, 13.15f, 6.65f), 0.1f, 0.3f, M.WoodDark);

            // ---------------- guirlanda de flores no pescoço
            {
                Vector3 c0 = neck[1].Item1;
                Vector3 tg = (neck[2].Item1 - neck[0].Item1).normalized;
                Vector3 up = (Vector3.up - Vector3.Dot(Vector3.up, tg) * tg).normalized;
                Vector3 sd = Vector3.Cross(up, tg).normalized;
                Vector2 rr = neck[1].Item2;
                M[] flowers = { M.Cloth, M.PlasterWhite, M.Gold, M.Cloth };
                for (int k = 0; k < 26; k++)
                {
                    float a = k * Mathf.PI * 2f / 26f;
                    Vector3 p = c0 + sd * (Mathf.Cos(a) * (rr.x + 0.1f)) + up * (Mathf.Sin(a) * (rr.y + 0.1f));
                    b.Sphere(p, 0.2f, 0.17f, 6, M.Foliage);
                    if (k % 2 == 0) b.Sphere(p + (p - c0).normalized * 0.16f, 0.1f, 0.1f, 6, flowers[(k / 2) % flowers.Length]);
                }
            }

            // ---------------- cauda de cordas
            b.Torus(V(0, 7.62f, -5.62f), Quaternion.Euler(70f, 0f, 0f), 0.28f, 0.07f, 12, 5, M.Bronze);
            for (int i = 0; i < 7; i++)
            {
                float dx = (i - 3) * 0.08f, sway = F(-0.25f, 0.25f);
                Loft(b, 6, M.Rope, R(dx, 7.65f, -5.6f, 0.09f, 0.09f), R(dx * 1.3f, 6.9f, -6.2f, 0.085f, 0.085f),
                    R(dx * 2f + sway * 0.5f, 5.1f, -6.55f, 0.07f, 0.07f), R(dx * 2.6f + sway, 3.3f, -6.4f, 0.05f, 0.05f));
            }

            // ---------------- escotilha aberta no flanco esquerdo + escada de corda
            b.Push(V(-2.2f, 6.45f, 0.2f), Quaternion.Euler(0f, 0f, 15f));
            b.Box(V(0, 0, 0), V(0.08f, 1.4f, 1.7f), M.Dark);
            b.Box(V(-0.06f, 0.76f, 0), V(0.14f, 0.14f, 1.95f), M.WoodDark);
            b.Box(V(-0.06f, -0.76f, 0), V(0.14f, 0.14f, 1.95f), M.WoodDark);
            b.Box(V(-0.06f, 0, 0.92f), V(0.14f, 1.5f, 0.14f), M.WoodDark);
            b.Box(V(-0.06f, 0, -0.92f), V(0.14f, 1.5f, 0.14f), M.WoodDark);
            b.Box(V(-0.2f, -1.5f, 0), V(0.1f, 1.45f, 1.7f), M.HorseWood, Quaternion.Euler(0f, 0f, -12f));
            foreach (float zz in new[] { -0.55f, 0.55f }) b.Box(V(-0.12f, -0.8f, zz), V(0.1f, 0.25f, 0.16f), M.Bronze);
            b.Pop();
            foreach (float zz in new[] { -0.3f, 0.7f })
                b.CylinderBetween(V(-2.35f, 5.8f, zz), V(-3.4f, 0.05f, zz), 0.04f, 0.04f, 5, M.Rope);
            for (int i = 1; i < 13; i++)
            {
                Vector3 p = Vector3.Lerp(V(-2.35f, 5.8f, 0), V(-3.4f, 0.05f, 0), i / 13f);
                b.CylinderBetween(p + V(0, 0, -0.3f), p + V(0, 0, 0.7f), 0.035f, 0.035f, 5, M.HorseWood);
            }

            // ---------------- cordas de arrasto, rolos de corda e oferendas no deque
            foreach (float sx in sides)
            {
                Loft(b, 6, M.Rope, R(sx * 2.2f, 1.0f, 6.45f, 0.07f, 0.07f), R(sx * 2.45f, 0.45f, 7.4f, 0.07f, 0.07f),
                    R(sx * 2.8f, 0.08f, 9.0f, 0.07f, 0.07f), R(sx * 3.1f, 0.07f, 12.5f, 0.07f, 0.07f),
                    R(sx * 3.5f, 0.07f, 16f, 0.07f, 0.07f), R(sx * 3.4f, 0.07f, 18.4f, 0.07f, 0.07f));
                for (int k = 0; k < 3; k++)
                    b.Torus(V(sx * 3.8f, 0.08f + k * 0.13f, 19.1f), Quaternion.Euler(0f, k * 20f, 0f), 0.55f - k * 0.05f, 0.07f, 16, 5, M.Rope);
                Amphora(b, V(sx * 2.0f, deck, -5.4f), 0.75f);
                Amphora(b, V(sx * 1.6f, deck, 5.5f), 0.6f, M.Gold);
                for (int k = 0; k < 6; k++)
                    b.Sphere(V(sx * F(0.3f, 2.3f), deck + 0.08f, F(-5.5f, 5.5f)), 0.16f, 0.09f, 6, k % 2 == 0 ? M.Foliage : M.Cloth);
            }
            return b;
        }

        static void Column(MeshBuilder b, Vector3 p, Vector2[] prof)
        {
            b.Lathe(p, Id, prof, 12, M.Marble, false, false);
            b.Box(p + V(0, 6.55f, 0), V(1.5f, 0.3f, 1.5f), M.Marble);
        }

        static void Athena(MeshBuilder b, Vector3 p)
        {
            b.Box(p + V(0, 0.5f, 0), V(2f, 1f, 2f), M.Marble);
            b.Box(p + V(0, 1.05f, 0), V(2.2f, 0.1f, 2.2f), M.Marble);
            Vector3 q = p + V(0, 1.1f, 0);
            b.Lathe(q, Id, new[] { V2(0.62f, 0f), V2(0.66f, 0.4f), V2(0.52f, 1.7f), V2(0.44f, 2.6f), V2(0.5f, 2.9f), V2(0.42f, 3.2f), V2(0.15f, 3.35f) }, 14, M.Gold, true, true);
            b.Sphere(q + V(0, 3.62f, 0), 0.28f, 0.32f, 12, M.Gold);
            b.Sphere(q + V(0, 3.78f, -0.02f), 0.31f, 0.24f, 12, M.Bronze);
            b.Box(q + V(0, 4.15f, -0.05f), V(0.08f, 0.35f, 0.75f), M.Cloth);
            b.BoxBetween(q + V(0.42f, 2.9f, 0), q + V(0.85f, 3.3f, 0.15f), 0.18f, 0.18f, M.Gold);
            b.CylinderBetween(q + V(0.85f, 0f, 0.15f), q + V(0.85f, 5.4f, 0.15f), 0.045f, 0.045f, 6, M.WoodDark);
            b.Lathe(q + V(0.85f, 5.4f, 0.15f), Id, new[] { V2(0.09f, 0f), V2(0f, 0.45f) }, 6, M.Bronze, false, false);
            b.CylinderBetween(q + V(-0.62f, 1.9f, 0.15f), q + V(-0.66f, 1.9f, 0.32f), 0.85f, 0.85f, 18, M.Bronze);
        }

        /// <summary>Templo de Atena (frente em +Z local). Base em y=0.</summary>
        public static MeshBuilder Temple()
        {
            var b = new MeshBuilder();
            for (int k = 0; k < 3; k++) b.Box(V(0, 0.2f + k * 0.4f, 0), V(13f - k * 0.8f, 0.4f, 24f - k * 0.8f), M.Marble);
            const float top = 1.2f;
            var prof = new[] { V2(0.62f, 0f), V2(0.6f, 2f), V2(0.56f, 4.4f), V2(0.5f, 5.95f), V2(0.5f, 6.0f), V2(0.72f, 6.3f), V2(0.72f, 6.4f) };
            for (int i = 0; i < 6; i++)
            {
                float x = -4.9f + i * 1.96f;
                Column(b, V(x, top, 10.4f), prof);
                Column(b, V(x, top, -10.4f), prof);
            }
            for (int j = 1; j < 10; j++)
            {
                float z = -10.4f + j * 2.08f;
                Column(b, V(4.9f, top, z), prof);
                Column(b, V(-4.9f, top, z), prof);
            }
            Column(b, V(-1.6f, top, 8.1f), prof);
            Column(b, V(1.6f, top, 8.1f), prof);
            float colTop = top + 6.7f;
            b.Box(V(0, colTop + 0.45f, 0), V(11.8f, 0.9f, 22.8f), M.Marble);
            b.Box(V(0, colTop + 1.3f, 0), V(11.6f, 0.8f, 22.6f), M.Marble);
            for (int i = 0; i < 12; i++)
            {
                float x = -5.5f + i * 1.0f;
                b.Box(V(x, colTop + 1.3f, 11.32f), V(0.35f, 0.75f, 0.08f), M.ClothBlue);
                b.Box(V(x, colTop + 1.3f, -11.32f), V(0.35f, 0.75f, 0.08f), M.ClothBlue);
            }
            for (int i = 0; i < 22; i++)
            {
                float z = -10.5f + i * 1.0f;
                b.Box(V(5.82f, colTop + 1.3f, z), V(0.08f, 0.75f, 0.35f), M.ClothBlue);
                b.Box(V(-5.82f, colTop + 1.3f, z), V(0.08f, 0.75f, 0.35f), M.ClothBlue);
            }
            b.Box(V(0, colTop + 1.78f, 0), V(11.9f, 0.16f, 22.9f), M.PlasterRed);
            b.Box(V(0, colTop + 2.0f, 0), V(12.4f, 0.3f, 23.4f), M.Marble);
            b.Prism(V(0, colTop + 2.15f, 0), 12.4f, 2.4f, 23.4f, M.RoofTile, M.Marble, Id);
            foreach (float z in new[] { 11.75f, -11.75f })
                b.Sphere(V(0, colTop + 4.75f, z), 0.4f, 0.5f, 8, M.Gold);
            // cela (naos)
            float ch = colTop - top, cy = top + ch * 0.5f;
            b.Box(V(-3.2f, cy, -0.5f), V(0.6f, ch, 15f), M.PlasterWhite);
            b.Box(V(3.2f, cy, -0.5f), V(0.6f, ch, 15f), M.PlasterWhite);
            b.Box(V(0, cy, -7.7f), V(7f, ch, 0.6f), M.PlasterWhite);
            b.Box(V(-2.5f, cy, 6.7f), V(2f, ch, 0.6f), M.PlasterWhite);
            b.Box(V(2.5f, cy, 6.7f), V(2f, ch, 0.6f), M.PlasterWhite);
            const float doorH = 5f;
            b.Box(V(0, top + doorH + (ch - doorH) * 0.5f, 6.7f), V(3f, ch - doorH, 0.6f), M.PlasterWhite);
            b.Box(V(-2.88f, top + 3f, -0.5f), V(0.04f, 0.5f, 14f), M.PlasterRed);
            b.Box(V(2.88f, top + 3f, -0.5f), V(0.04f, 0.5f, 14f), M.PlasterRed);
            Athena(b, V(0, top, -5.5f));
            return b;
        }

        static void MinoanColumn(MeshBuilder b, Vector3 p, float h)
        {
            b.Box(p + V(0, 0.1f, 0), V(0.9f, 0.2f, 0.9f), M.Stone);
            b.Lathe(p + V(0, 0.2f, 0), Id, new[] { V2(0.26f, 0f), V2(0.36f, h - 0.75f) }, 10, M.WoodPainted, false, false);
            b.Lathe(p + V(0, h - 0.55f, 0), Id, new[] { V2(0.36f, 0f), V2(0.5f, 0.18f), V2(0.5f, 0.3f) }, 10, M.WoodDark, true, false);
            b.Box(p + V(0, h - 0.12f, 0), V(1.0f, 0.24f, 1.0f), M.WoodDark);
        }

        static void Wing(MeshBuilder b, float x0, float x1, float z0, float z1, float h, M wall)
        {
            Plinth(b, x0, x1, z0, z1);
            Walls(b, x0, x1, z0, z1, 0, h, wall);
            FlatRoof(b, x0, x1, z0, z1, h, M.MudBrick);
            b.Box(V((x0 + x1) * 0.5f, h - 0.6f, (z0 + z1) * 0.5f), V(x1 - x0 + 0.06f, 0.35f, z1 - z0 + 0.06f), M.PlasterRed);
        }

        /// <summary>Palácio de Príamo. Construído em coordenadas do mundo (XZ) com pivô em (14, plateau, 103).</summary>
        public static MeshBuilder Palace()
        {
            var b = new MeshBuilder();
            b.Push(V(-14f, 0f, -103f));
            // propileu (entrada sul)
            b.Box(V(6f, 3f, 86.5f), V(6f, 6f, 1.2f), M.Plaster);
            b.Box(V(22f, 3f, 86.5f), V(6f, 6f, 1.2f), M.Plaster);
            b.Box(V(6f, 4.4f, 86.5f), V(6.04f, 0.4f, 1.24f), M.PlasterRed);
            b.Box(V(22f, 4.4f, 86.5f), V(6.04f, 0.4f, 1.24f), M.PlasterRed);
            foreach (float x in new[] { 11f, 17f })
            foreach (float z in new[] { 84.8f, 88.2f })
                MinoanColumn(b, V(x, 0, z), 5.4f);
            b.Box(V(14f, 5.65f, 86.5f), V(11.5f, 0.5f, 4.6f), M.Roof);
            b.Box(V(14f, 6.05f, 86.5f), V(11.8f, 0.3f, 4.9f), M.PlasterRed);
            // alas (depósitos e aposentos)
            Wing(b, -5f, 3.5f, 87f, 107f, 7f, M.Plaster);
            Wing(b, 24.5f, 31f, 87f, 107f, 6.5f, M.PlasterOchre);
            // muros que fecham os cantos norte do pátio (a arena do chefe fica totalmente fechada)
            b.Box(V(4.75f, 3f, 107.5f), V(3.5f, 6f, 1f), M.Plaster);
            b.Box(V(23.25f, 3f, 107.5f), V(3.5f, 6f, 1f), M.Plaster);
            for (int i = 0; i < 3; i++)
            {
                b.Box(V(3.55f, 1.2f, 91f + i * 6f), V(0.1f, 2.4f, 1.3f), M.Dark);
                b.Box(V(24.45f, 1.2f, 91f + i * 6f), V(0.1f, 2.4f, 1.3f), M.Dark);
            }
            // pórticos internos
            for (int i = 0; i < 5; i++)
            {
                float z = 89f + i * 4f;
                MinoanColumn(b, V(5.6f, 0, z), 4.7f);
                MinoanColumn(b, V(22.4f, 0, z), 4.7f);
            }
            b.Box(V(4.6f, 4.85f, 97f), V(2.4f, 0.3f, 20f), M.Roof);
            b.Box(V(23.4f, 4.85f, 97f), V(2.4f, 0.3f, 20f), M.Roof);
            // mégaron (salão do trono)
            b.Box(V(14f, 3.75f, 118.5f), V(16f, 7.5f, 1f), M.Plaster);
            b.Box(V(6.5f, 3.75f, 113f), V(1f, 7.5f, 12f), M.Plaster);
            b.Box(V(21.5f, 3.75f, 113f), V(1f, 7.5f, 12f), M.Plaster);
            b.Box(V(9f, 3.75f, 110.5f), V(6f, 7.5f, 1f), M.Plaster);
            b.Box(V(19f, 3.75f, 110.5f), V(6f, 7.5f, 1f), M.Plaster);
            b.Box(V(14f, 6.25f, 110.5f), V(4f, 2.5f, 1f), M.Plaster);
            b.Box(V(14f, 5.2f, 113f), V(16.06f, 0.4f, 12.06f), M.PlasterRed);
            b.Box(V(14f, 0.05f, 114.5f), V(14f, 0.1f, 7f), M.PlasterRed);
            MinoanColumn(b, V(11f, 0, 107.7f), 7f);
            MinoanColumn(b, V(17f, 0, 107.7f), 7f);
            b.Box(V(14f, 7.75f, 113f), V(17f, 0.5f, 13f), M.Roof);
            b.Box(V(14f, 8.15f, 113f), V(17.2f, 0.3f, 13.2f), M.PlasterRed);
            foreach (float dx in new[] { -1.8f, 1.8f })
            foreach (float dz in new[] { -1.8f, 1.8f })
                b.Box(V(14f + dx, 9.0f, 114.5f + dz), V(0.25f, 1.4f, 0.25f), M.WoodPainted);
            b.Box(V(14f, 9.85f, 114.5f), V(4.6f, 0.3f, 4.6f), M.Roof);
            // lareira central, colunas e trono
            b.Lathe(V(14f, 0, 114.5f), Id, new[] { V2(2.0f, 0f), V2(2.0f, 0.45f), V2(1.6f, 0.45f), V2(1.6f, 0.15f), V2(0f, 0.15f) }, 20, M.Stone, false, false);
            foreach (var (x, z) in new[] { (11.5f, 112f), (16.5f, 112f), (11.5f, 117f), (16.5f, 117f) })
                MinoanColumn(b, V(x, 0, z), 7.5f);
            b.Box(V(14f, 0.25f, 117.4f), V(3f, 0.5f, 1.6f), M.Stone);
            b.Box(V(14f, 0.85f, 117.5f), V(1.3f, 0.7f, 1.0f), M.Gold);
            b.Box(V(14f, 1.9f, 117.95f), V(1.3f, 2.2f, 0.2f), M.Gold);
            b.Box(V(14f, 3.2f, 117.97f), V(14.8f, 0.6f, 0.04f), M.ClothBlue);
            b.Box(V(7.03f, 3.2f, 114f), V(0.04f, 0.6f, 7f), M.ClothBlue);
            b.Box(V(20.97f, 3.2f, 114f), V(0.04f, 0.6f, 7f), M.ClothBlue);
            b.Pop();
            return b;
        }

        public static MeshBuilder ZeusAltar()
        {
            var b = new MeshBuilder();
            b.Box(V(0, 0.2f, 0), V(5f, 0.4f, 5f), M.Marble);
            b.Box(V(0, 0.6f, 0), V(4f, 0.4f, 4f), M.Marble);
            b.Box(V(0, 1.35f, 0), V(2.4f, 1.1f, 1.6f), M.Marble);
            b.Box(V(0, 1.98f, 0), V(2.7f, 0.16f, 1.9f), M.Marble);
            foreach (float s in new[] { -1f, 1f }) b.Box(V(s * 1.0f, 2.35f, 0), V(0.3f, 0.6f, 1.2f), M.Marble);
            b.Sphere(V(0, 2.06f, 0), 0.7f, 0.1f, 10, M.Charred);
            Amphora(b, V(1.8f, 0.8f, 1.4f), 0.7f, M.Gold);
            Amphora(b, V(-1.8f, 0.8f, -1.4f), 0.7f, M.Gold);
            return b;
        }

        public static MeshBuilder Laurel()
        {
            _r = new System.Random(5);
            var b = new MeshBuilder();
            b.CylinderBetween(V(0, 0, 0), V(0.3f, 2.2f, 0.1f), 0.32f, 0.24f, 8, M.WoodDark);
            b.CylinderBetween(V(0.3f, 2.2f, 0.1f), V(-0.4f, 3.6f, 0.3f), 0.24f, 0.15f, 7, M.WoodDark);
            b.CylinderBetween(V(0.3f, 2.2f, 0.1f), V(1.2f, 3.3f, -0.3f), 0.2f, 0.12f, 7, M.WoodDark);
            for (int i = 0; i < 9; i++)
                b.Sphere(V(F(-1.6f, 1.8f), F(3.2f, 5.4f), F(-1.5f, 1.5f)), F(0.9f, 1.5f), F(0.8f, 1.2f), 10, M.Foliage);
            b.Lathe(V(0, 0, 0), Id, new[] { V2(1.5f, 0f), V2(1.5f, 0.5f), V2(1.2f, 0.5f), V2(1.2f, 0.1f) }, 16, M.Stone, false, false);
            return b;
        }

        public static MeshBuilder Fountain()
        {
            var b = new MeshBuilder();
            b.Lathe(V(0, 0, 0), Id, new[] { V2(3.6f, 0f), V2(3.6f, 0.75f), V2(3.15f, 0.75f), V2(3.15f, 0.2f), V2(0f, 0.2f) }, 28, M.Stone, false, false);
            b.Cylinder(V(0, 0.1f, 0), 3.16f, 3.16f, 0.45f, 28, M.Water, true);
            b.Lathe(V(0, 0, 0), Id, new[] { V2(0.7f, 0f), V2(0.7f, 1.3f), V2(1.0f, 1.5f), V2(0.45f, 1.7f), V2(0.4f, 2.7f), V2(0.65f, 2.9f), V2(0.65f, 3.05f) }, 14, M.Marble, true, false);
            StatueFigure(b, V(0, 3.05f, 0), 0.8f, M.Bronze, true);
            for (int i = 0; i < 4; i++)
            {
                Vector3 d = Yaw(i * 90f + 45f) * Vector3.forward;
                b.BoxBetween(d * 0.4f + V(0, 2.3f, 0), d * 1.3f + V(0, 2.1f, 0), 0.12f, 0.12f, M.Bronze);
            }
            return b;
        }

        public static MeshBuilder Ship()
        {
            var b = new MeshBuilder();
            var hull = new[] { V2(0.2f, 0f), V2(1.2f, 1.5f), V2(1.75f, 5f), V2(1.75f, 11f), V2(1.3f, 14.5f), V2(0.2f, 16f) };
            var hullIn = new Vector2[hull.Length];
            for (int i = 0; i < hull.Length; i++) hullIn[hull.Length - 1 - i] = new Vector2(Mathf.Max(0.05f, hull[i].x - 0.1f), hull[i].y);
            b.Lathe(V(0, 1.0f, -8f), Quaternion.Euler(90f, 0f, 0f), hull, 10, M.WoodDark, false, false, 180f);
            b.Lathe(V(0, 1.0f, -8f), Quaternion.Euler(90f, 0f, 0f), hullIn, 10, M.WoodDark, false, false, 180f);
            b.Box(V(0, 0.6f, 0), V(2.8f, 0.12f, 13f), M.Wood);
            b.BoxBetween(V(0, 0.6f, 7.6f), V(0, 3.6f, 9.4f), 0.35f, 0.5f, M.WoodDark);
            b.BoxBetween(V(0, 0.6f, -7.6f), V(0, 3.2f, -9.2f), 0.35f, 0.5f, M.WoodDark);
            b.BoxBetween(V(0, 3.2f, -9.2f), V(0, 3.9f, -8.4f), 0.3f, 0.4f, M.WoodDark);
            b.CylinderBetween(V(0, 0.6f, 1f), V(0, 9.5f, 1f), 0.16f, 0.12f, 6, M.Wood);
            b.CylinderBetween(V(-4f, 8.6f, 1f), V(4f, 8.6f, 1f), 0.09f, 0.09f, 6, M.Wood);
            b.Box(V(0, 8.25f, 1.05f), V(7.6f, 0.5f, 0.4f), M.Cloth);
            for (int i = 0; i < 9; i++)
            {
                float z = -5.5f + i * 1.3f;
                foreach (float s in new[] { -1f, 1f })
                {
                    b.CylinderBetween(V(s * 1.6f, 1.0f, z), V(s * 4.6f, -0.8f, z + 0.4f), 0.05f, 0.05f, 4, M.Wood);
                    if (i < 7) b.CylinderBetween(V(s * 1.72f, 1.25f, -4.5f + i * 1.5f), V(s * 1.84f, 1.25f, -4.5f + i * 1.5f), 0.42f, 0.42f, 10, M.Bronze);
                }
            }
            return b;
        }

        /// <summary>Silhuetas de soldados gregos (um único mesh). Devolve a ponta das tochas.</summary>
        public static MeshBuilder Army(List<Vector3> pos, List<float> yaw, int torchEvery, out List<Vector3> torchTips)
        {
            var b = new MeshBuilder();
            torchTips = new List<Vector3>();
            for (int i = 0; i < pos.Count; i++)
            {
                Quaternion q = Yaw(yaw[i]);
                b.Push(pos[i], q);
                b.Lathe(V(0, 0, 0), Id, new[] { V2(0.3f, 0f), V2(0.28f, 0.85f), V2(0.24f, 1.35f), V2(0.17f, 1.5f) }, 7, M.Cloth, true, false);
                b.Lathe(V(0, 0.85f, 0), Id, new[] { V2(0.29f, 0f), V2(0.27f, 0.5f), V2(0.18f, 0.66f) }, 7, M.Bronze, true, false);
                b.Sphere(V(0, 1.62f, 0), 0.14f, 0.16f, 6, M.Bronze);
                b.Box(V(0, 1.85f, -0.02f), V(0.05f, 0.18f, 0.4f), M.Cloth);
                b.CylinderBetween(V(-0.3f, 1.0f, 0.05f), V(-0.36f, 1.0f, 0.12f), 0.42f, 0.42f, 10, M.Bronze);
                if (i % torchEvery == 0)
                {
                    b.CylinderBetween(V(0.28f, 0.9f, 0.15f), V(0.32f, 1.95f, 0.25f), 0.03f, 0.03f, 4, M.WoodDark);
                    torchTips.Add(pos[i] + q * V(0.32f, 2.05f, 0.25f));
                }
                else b.CylinderBetween(V(0.28f, 0.0f, 0.1f), V(0.3f, 2.6f, 0.15f), 0.022f, 0.022f, 4, M.WoodDark);
                b.Pop();
            }
            return b;
        }

        /// <summary>Detritos de batalha espalhados no chão (um único mesh, sem colisor).</summary>
        public static MeshBuilder Debris(IList<Vector3> pos, int seed)
        {
            _r = new System.Random(seed);
            var b = new MeshBuilder();
            foreach (var p in pos)
            {
                float yaw = F(0f, 360f);
                Quaternion q = Quaternion.Euler(F(-12f, 12f), yaw, F(-12f, 12f));
                Vector3 dir = Yaw(yaw) * Vector3.forward;
                int t = _r.Next(12);
                switch (t)
                {
                    case 0:
                    case 1:
                    case 2:
                    {
                        int n = 1 + _r.Next(3);
                        for (int i = 0; i < n; i++)
                        {
                            float s = F(0.12f, 0.32f);
                            b.Box(p + V(F(-0.4f, 0.4f), s * 0.25f, F(-0.4f, 0.4f)), V(s * 1.6f, s * 0.6f, s), _r.NextDouble() < 0.6 ? M.MudBrick : M.Stone,
                                Quaternion.Euler(F(-25f, 25f), F(0f, 360f), F(-25f, 25f)));
                        }
                        break;
                    }
                    case 3:
                        for (int i = 0; i < 4; i++)
                            b.Box(p + V(F(-0.35f, 0.35f), 0.02f, F(-0.35f, 0.35f)), V(F(0.08f, 0.2f), 0.025f, F(0.06f, 0.16f)), M.Terracotta,
                                Quaternion.Euler(F(-20f, 20f), F(0f, 360f), F(-20f, 20f)));
                        break;
                    case 4:
                    {
                        Vector3 a = p + V(0, 0.03f, 0) - dir * 0.4f, c = p + V(0, 0.05f, 0) + dir * 0.4f;
                        b.CylinderBetween(a, c, 0.012f, 0.012f, 4, M.Wood);
                        b.Lathe(c, Quaternion.FromToRotation(Vector3.up, dir), new[] { V2(0.025f, 0f), V2(0f, 0.08f) }, 4, M.Bronze, false, false);
                        b.Box(a + dir * 0.06f, V(0.09f, 0.002f, 0.1f), M.Cloth, Quaternion.LookRotation(dir));
                        break;
                    }
                    case 5:
                    {
                        float L = F(0.7f, 1.4f);
                        Vector3 a = p + V(0, 0.04f, 0) - dir * L * 0.5f, c = p + V(0, 0.04f, 0) + dir * L * 0.5f;
                        b.CylinderBetween(a, c, 0.025f, 0.025f, 5, M.WoodDark);
                        if (_r.NextDouble() < 0.5)
                            b.Lathe(c, Quaternion.FromToRotation(Vector3.up, dir), new[] { V2(0.05f, 0f), V2(0f, 0.25f) }, 5, M.Bronze, false, false);
                        break;
                    }
                    case 6:
                    {
                        Vector3 up = q * Vector3.up;
                        b.CylinderBetween(p + V(0, 0.02f, 0), p + V(0, 0.02f, 0) + up * 0.06f, 0.42f, 0.4f, 14, M.Bronze);
                        b.Sphere(p + V(0, 0.08f, 0) + up * 0.04f, 0.1f, 0.06f, 8, M.Bronze);
                        break;
                    }
                    case 7:
                        b.Sphere(p + V(0, 0.13f, 0), 0.16f, 0.15f, 8, M.Bronze);
                        b.Box(p + V(0, 0.2f, 0) + dir * 0.12f, V(0.04f, 0.22f, 0.36f), M.Cloth, Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, 70f));
                        break;
                    case 8:
                    case 9:
                    {
                        float L = F(0.6f, 1.6f);
                        b.BoxBetween(p + V(0, 0.08f, 0) - dir * L * 0.5f, p + V(0, F(0.08f, 0.3f), 0) + dir * L * 0.5f, F(0.1f, 0.2f), F(0.1f, 0.18f), M.Charred);
                        break;
                    }
                    case 10:
                        b.Sphere(p + V(0, 0.01f, 0), F(0.25f, 0.45f), 0.06f, 8, M.Charred);
                        break;
                    default:
                        b.Box(p + V(0, 0.015f, 0), V(F(0.4f, 0.8f), 0.02f, F(0.3f, 0.6f)), _r.NextDouble() < 0.5 ? M.Cloth : M.ClothBlue, q);
                        break;
                }
            }
            return b;
        }

        public static MeshBuilder Sea(float y)
        {
            var b = new MeshBuilder();
            b.QuadOut(V(-1600f, y, -1800f), V(-1600f, y, -250f), V(1600f, y, -250f), V(1600f, y, -1800f), Vector3.up, M.Water);
            return b;
        }
    }
}
