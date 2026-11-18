#ifndef TROIA_DIVINO_INCLUDED
#define TROIA_DIVINO_INCLUDED
// Ruído procedural compartilhado pelos efeitos das bênçãos (sem texturas).
#define DIV_TAU 6.2831853

float DivHash(float2 p)
{
    p = frac(p * float2(123.34, 345.45));
    p += dot(p, p + 34.345);
    return frac(p.x * p.y);
}
float2 DivHash2(float2 p)
{
    float n = DivHash(p);
    return float2(n, DivHash(p + n + 17.17));
}
float DivRuido(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = DivHash(i), b = DivHash(i + float2(1, 0)), c = DivHash(i + float2(0, 1)), d = DivHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}
float DivFbm(float2 p)
{
    float v = 0.0, a = 0.5;
    for (int i = 0; i < 4; i++) { v += a * DivRuido(p); p = p * 2.03 + float2(17.3, 9.1); a *= 0.5; }
    return v / 0.9375;
}
// distância à borda da célula de Voronoi mais próxima (0 na rachadura)
float DivRachadura(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float d1 = 8.0, d2 = 8.0;
    for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
        {
            float2 g = float2(x, y);
            float2 o = DivHash2(i + g);
            float d = length(g + o - f);
            if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
        }
    return d2 - d1;
}
// grade de hexágonos: x = distância à borda (0 na linha, 0,5 no centro), yz = identificador da célula
float3 DivHex(float2 p)
{
    const float2 s = float2(1.0, 1.7320508);
    float4 hc = floor(float4(p, p - float2(0.5, 1.0)) / s.xyxy) + 0.5;
    float4 h = float4(p - hc.xy * s, p - (hc.zw + 0.5) * s);
    bool primeiro = dot(h.xy, h.xy) < dot(h.zw, h.zw);
    float2 q = abs(primeiro ? h.xy : h.zw);
    float2 id = primeiro ? hc.xy : hc.zw + 0.5;
    return float3(0.5 - max(dot(q, s * 0.5), q.x), id);
}
float DivFaixa(float x, float centro, float largura) { return smoothstep(largura, 0.0, abs(x - centro)); }
float DivRet(float2 p, float2 a, float2 b)
{
    float2 e = float2(0.02, 0.02);
    float2 d = smoothstep(a - e, a + e, p) * smoothstep(b + e, b - e, p);
    return d.x * d.y;
}
#endif
