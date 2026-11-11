#ifndef TROIA_GRIME_INCLUDED
#define TROIA_GRIME_INCLUDED

// Sujeira procedural em espaço de mundo para o "Troia/Lit Sujo".
// Parâmetros globais (definidos pelo componente TroiaShaderGlobals na cena), para não
// mexer no CBUFFER do Lit e manter compatibilidade com o SRP Batcher.
//   _TroiaGrimeTex   R: variação de tom em grande escala  G: escorridos verticais
//                    B: respingos/manchas                A: poeira fina
//   _TroiaGroundTex  R: altura do chão (normalizada) para saber a "base" das paredes
// Por material: _Parallax = quantidade de sujeira (0..1). Sem mapa de parallax o Lit não usa esse valor.

TEXTURE2D(_TroiaGrimeTex);  SAMPLER(sampler_TroiaGrimeTex);
TEXTURE2D(_TroiaGroundTex); SAMPLER(sampler_TroiaGroundTex);
float4 _TroiaGroundRect;    // xy = canto mínimo (x,z) no mundo, zw = 1 / tamanho
float4 _TroiaGroundRange;   // x = altura mínima, y = faixa de altura
float4 _TroiaGrimeParams;   // x = altura da sujeira da base (m), y = força global, z = variação de tom, w = escorridos
float4 _TroiaGrimeColor;    // cor da lama/fuligem

float TroiaGroundY(float2 xz)
{
    float2 uv = saturate((xz - _TroiaGroundRect.xy) * _TroiaGroundRect.zw);
    return _TroiaGroundRange.x + SAMPLE_TEXTURE2D_LOD(_TroiaGroundTex, sampler_TroiaGroundTex, uv, 0).r * _TroiaGroundRange.y;
}

void TroiaApplyGrime(float3 positionWS, float3 normalWS, inout SurfaceData s)
{
    float amount = saturate(_Parallax) * _TroiaGrimeParams.y;
    if (amount <= 0.001)
        return;

    float3 n = normalize(normalWS);
    float vertical = 1.0 - abs(n.y);
    float2 wallUV = abs(n.x) > abs(n.z) ? positionWS.zy : positionWS.xy;

    // variação de tom em grande escala (quebra a repetição das texturas)
    float4 macro = SAMPLE_TEXTURE2D(_TroiaGrimeTex, sampler_TroiaGrimeTex, positionWS.xz * 0.031 + positionWS.y * 0.017);
    s.albedo *= lerp(1.0, 0.7 + 0.55 * macro.r, saturate(_TroiaGrimeParams.z * amount));

    // escorridos verticais nas paredes
    float streak = SAMPLE_TEXTURE2D(_TroiaGrimeTex, sampler_TroiaGrimeTex, wallUV * float2(0.23, 0.05)).g;

    // lama e respingos na base das paredes
    float splash = SAMPLE_TEXTURE2D(_TroiaGrimeTex, sampler_TroiaGrimeTex, wallUV * 0.45).b;
    float h = positionWS.y - TroiaGroundY(positionWS.xz);
    float baseH = max(_TroiaGrimeParams.x * (0.55 + 0.9 * splash), 0.05);
    float baseMask = saturate(1.0 - h / baseH);
    baseMask *= baseMask;

    // poeira acumulada em superfícies voltadas para cima
    float dust = saturate(n.y) * macro.a * 0.45;

    float dirt = saturate(baseMask * vertical * 1.15 + streak * _TroiaGrimeParams.w * vertical);
    dirt = max(dirt, dust) * amount;

    float3 grime = _TroiaGrimeColor.rgb * (0.65 + 0.7 * splash);
    s.albedo = lerp(s.albedo, s.albedo * grime * 2.2, saturate(dirt * 0.85));
    s.smoothness *= 1.0 - saturate(dirt) * 0.6;
    s.occlusion *= 1.0 - baseMask * vertical * 0.4 * amount;
}

// Igual ao LitPassFragment do URP, com a sujeira aplicada logo após ler o material.
void TroiaLitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_PARALLAXMAP)
#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS = input.viewDirTS;
#else
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
#endif
    ApplyPerPixelDisplacement(viewDirTS, input.uv);
#endif

    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
    TroiaApplyGrime(input.positionWS, input.normalWS.xyz, surfaceData);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
