#ifndef TROIA_CHAR_FORWARD_PASS_INCLUDED
#define TROIA_CHAR_FORWARD_PASS_INCLUDED

// Cópia do LitForwardPass.hlsl do URP 17.3 para os personagens:
//  - lê a cor do vértice (R = oclusão de ambiente assada pelo PatrocloBuilder) e aplica na oclusão e,
//    em parte, no difuso (sombras de contato finas entre as camadas da roupa e da armadura);
//  - desgaste de batalha (TroiaWeather): sujeira e sangue seco a partir das máscaras G e B do vértice
//    e da textura repetível _GrimeMap;
//  - com TROIA_SKIN definido, soma o espalhamento subsuperficial da pele (TroiaSkin.hlsl).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#if defined(_PARALLAXMAP)
#define REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR
#endif

#if (defined(_NORMALMAP) || (defined(_PARALLAXMAP) && !defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR))) || defined(_DETAIL)
#define REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR
#endif

// keep this file in sync with LitGBufferPass.hlsl

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float4 tangentOS    : TANGENT;
    float2 texcoord     : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    half4 color         : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD1;
#endif

    float3 normalWS                 : TEXCOORD2;
    float2 uvSujeira                : TEXCOORD4;    // UV em metros para a textura de sujeira
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    half4 tangentWS                : TEXCOORD3;    // xyz: tangent, w: sign
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight   : TEXCOORD5; // x: fogFactor, yzw: vertex light
#else
    half  fogFactor                 : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord              : TEXCOORD6;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirTS                : TEXCOORD7;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV : TEXCOORD9; // Dynamic lightmap UVs
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD10;
#endif

    half4 vertexAO                  : COLOR;
    float4 positionCS               : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(_NORMALMAP) || defined(_DETAIL)
    float sgn = input.tangentWS.w;      // should be either +1 or -1
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);

    #if defined(_NORMALMAP)
    inputData.tangentToWorld = tangentToWorld;
    #endif
    inputData.normalWS = TransformTangentToWorld(normalTS, tangentToWorld);
#else
    inputData.normalWS = input.normalWS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
    #if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
    #elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
    #else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
    #endif
}

///////////////////////////////////////////////////////////////////////////////
//                  Vertex and Fragment functions                            //
///////////////////////////////////////////////////////////////////////////////

// Used in Standard (Physically Based) shader
Varyings TroiaCharPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
#if defined(_TROIA_UV2)
    // peças com textura pintada por inteiro: a primeira UV é o mapa da pintura; a sujeira usa a segunda (em metros)
    output.uvSujeira = input.staticLightmapUV;
#else
    output.uvSujeira = output.uv;
#endif

    // already normalized from normal transform to WS.
    output.normalWS = normalInput.normalWS;
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR) || defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    real sign = input.tangentOS.w * GetOddNegativeScale();
    half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
#if defined(REQUIRES_WORLD_SPACE_TANGENT_INTERPOLATOR)
    output.tangentWS = tangentWS;
#endif

#if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
    half3 viewDirTS = GetViewDirectionTangentSpace(tangentWS, output.normalWS, viewDirWS);
    output.viewDirTS = viewDirTS;
#endif

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = vertexInput.positionWS;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    output.positionCS = vertexInput.positionCS;
    output.vertexAO = input.color;

    return output;
}

#if defined(TROIA_SKIN)
#include "Assets/Troia/Shaders/TroiaSkin.hlsl"
#endif

// ---------------------------------------------------------------------------------------------
// Desgaste de batalha: sujeira e sangue seco.
//   cor do vértice  R = oclusão assada
//                   G = 1 - sujeira (lama perto do chão, poeira nas frestas, manchas)
//                   B = 1 - região de sangue (malhas sem cor assada vêm brancas = limpas)
//   _GrimeMap       textura repetível: R manchas largas, G escorridos, B respingos, A poeira fina
//   quantidade      _OcclusionStrength = sujeira, _Parallax = sangue. São campos do CBUFFER do Lit que
//                   estes shaders não usam para outra coisa (o SRP Batcher continua valendo).
#ifndef TROIA_GRIME_TILING
#define TROIA_GRIME_TILING 1.0
#endif
TEXTURE2D(_GrimeMap);
SAMPLER(sampler_GrimeMap);

void TroiaWeather(float2 uv, half4 vc, inout SurfaceData s)
{
    half dirtAmt = _OcclusionStrength;
    half bloodAmt = _Parallax;
    float2 guv = uv * TROIA_GRIME_TILING;
    half4 g = SAMPLE_TEXTURE2D(_GrimeMap, sampler_GrimeMap, guv);
    half4 gl = SAMPLE_TEXTURE2D(_GrimeMap, sampler_GrimeMap, guv * 0.31 + float2(0.37, 0.11));

    // sujeira: a máscara assada diz onde (lama, frestas, manchas); as manchas, os escorridos e o grão da
    // textura quebram a borda
    half baked = saturate(1.0h - vc.g);
    half m = baked * dirtAmt;
    half x = m * (0.3h + 1.5h * gl.r) + m * g.g * 0.6h + (g.a - 0.5h) * 0.22h * step(0.02h, m);
    half cov = smoothstep(0.12h, 0.95h, saturate(x));
    // encardido geral: manchas e escorridos leves em toda a peça
    // (no metal polido a mancha pega bem menos que no pano e na pele)
    half soil = dirtAmt * 0.7h * smoothstep(0.25h, 0.95h, gl.r * 0.8h + g.g * 0.55h) * lerp(1.0h, 0.4h, s.metallic);
    cov = max(cov, soil);
    // poeira seca (mais clara) onde há pouca sujeira, lama escura onde há muita (pés, barras do pano)
    half mud = smoothstep(0.3h, 0.7h, baked);
    half3 dirtCol = lerp(half3(0.25h, 0.2h, 0.15h), half3(0.085h, 0.062h, 0.046h), mud) * (0.75h + 0.5h * g.a);
    s.albedo = lerp(s.albedo, dirtCol, cov * 0.9h);
    s.smoothness = lerp(s.smoothness, 0.1h, cov);
    s.metallic = lerp(s.metallic, 0.0h, cov * 0.92h);
    // película de poeira: tira um pouco do brilho mesmo onde não há mancha
    s.smoothness *= 1.0h - 0.3h * dirtAmt * gl.a;

    // sangue seco: só nas regiões assadas (B). Respingos e algumas manchas; onde a região é forte
    // (ponta da lâmina), vira uma camada quase contínua.
    half region = saturate((bloodAmt - vc.b) * 3.0h);
    half spat = smoothstep(0.3h, 0.55h, g.b);
    half pool = smoothstep(0.5h, 0.75h, gl.b * 0.6h + gl.r * 0.55h);
    half smear = saturate(region * 1.5h - 0.6h) * (0.55h + 0.45h * gl.r);
    // com pouco sangue aparecem menos respingos, mas cada um continua opaco (meio transparente, o sangue
    // sobre a pele parece hematoma)
    half thr = 1.0h - region * 0.75h;
    half drops = smoothstep(thr, thr + 0.12h, saturate(spat + pool * 0.6h)) * step(0.01h, region);
    half bl = saturate(drops + smear);
    half3 bloodCol = half3(0.15h, 0.012h, 0.008h) * (0.6h + 0.6h * g.a);
    s.albedo = lerp(s.albedo, bloodCol, bl * 0.94h);
    s.smoothness = lerp(s.smoothness, 0.3h, bl);
    s.metallic = lerp(s.metallic, 0.0h, bl);
}

void TroiaCharPassFragment(
    Varyings input
#if defined(_TROIA_PANO)
    , FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC
#endif
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

    TroiaWeather(input.uvSujeira, input.vertexAO, surfaceData);

    // oclusão assada por vértice: escurece a luz ambiente/reflexos e, em parte, a luz direta
    half bakedAO = saturate(input.vertexAO.r);
    surfaceData.occlusion *= bakedAO;
    surfaceData.albedo *= lerp(1.0h, bakedAO, 0.4h);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData;
    InitializeInputData(input, surfaceData.normalTS, inputData);
#if defined(_TROIA_PANO)
    // pano de folha única visto pelo avesso: a superfície olha para o outro lado
    inputData.normalWS *= IS_FRONT_VFACE(cullFace, 1.0h, -1.0h);
#endif
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

#if defined(_DBUFFER)
    ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
#endif

    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
#if defined(TROIA_SKIN)
    color.rgb += TroiaSkinScatterAll(inputData, surfaceData);
#endif
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
