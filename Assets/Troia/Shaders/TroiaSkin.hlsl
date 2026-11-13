#ifndef TROIA_SKIN_INCLUDED
#define TROIA_SKIN_INCLUDED

// Pele do "Troia/Pele": o PBR normal do URP mais um espalhamento subsuperficial aproximado.
//  - a luz "envolve" a forma além do terminador (wrap) com cor avermelhada: tira o aspecto de cera/massinha
//  - translucidez quando a luz vem por trás (orelhas, dedos, bordas do contorno)
// Parâmetros reaproveitam campos do CBUFFER do Lit que a pele não usa (o SRP Batcher continua valendo):
//  _SpecColor.rgb = cor do espalhamento, _SpecColor.a x 2 = força, _ClearCoatMask = translucidez,
//  _ClearCoatSmoothness = largura do envolvimento da luz. (_Parallax e _OcclusionStrength ficam para o
//  desgaste de batalha, igual ao Troia/Personagem.)
// Incluído por TroiaCharForwardPass.hlsl quando TROIA_SKIN está definido.

half3 TroiaSkinScatter(Light light, half3 N, half3 V, half3 albedo)
{
    half ndl = dot(N, light.direction);
    half w = max(_ClearCoatSmoothness, 0.01h);
    half wrapped = saturate((ndl + w) / (1.0h + w));
    half lambert = saturate(ndl);
    half extra = max(wrapped - lambert, 0.0h);
    // a sombra projetada pesa menos onde a pele já está virando para o escuro (borda mais macia)
    half shadow = lerp(light.shadowAttenuation, 1.0h, 0.35h * (1.0h - lambert));
    half3 sss = albedo * _SpecColor.rgb * extra * (_SpecColor.a * 2.0h);
    // translucidez: luz atravessando por trás, vista no contorno
    half3 H = normalize(light.direction + N * 0.35h);
    half back = pow(saturate(dot(V, -H)), 4.0h) * _ClearCoatMask;
    sss += albedo * _SpecColor.rgb * back * light.shadowAttenuation;
    return sss * light.color * (light.distanceAttenuation * shadow);
}

half3 TroiaSkinScatterAll(InputData inputData, SurfaceData surfaceData)
{
    half3 N = inputData.normalWS;
    half3 V = inputData.viewDirectionWS;
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData, surfaceData);
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
    half3 sss = TroiaSkinScatter(mainLight, N, V, surfaceData.albedo);

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        sss += TroiaSkinScatter(light, N, V, surfaceData.albedo);
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        sss += TroiaSkinScatter(light, N, V, surfaceData.albedo);
    LIGHT_LOOP_END
#endif
    return sss * surfaceData.occlusion;
}

#endif
