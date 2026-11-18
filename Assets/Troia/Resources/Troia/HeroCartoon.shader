Shader "Troia/Hero Cartoon"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap("Texture", 2D) = "white" {}
        _Metallic("Metal", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = .2
        // rgb = cor do clarão (aviso de ataque, golpe sofrido), a = quanto cobre a cor da peça
        _Flash("Flash", Color) = (1,1,1,0)
        // 1 = usar a cor por vértice (bonecos fundidos); 0 = malha comum, sem cor de vértice
        [HideInInspector] _VertexColor("Vertex Color", Float) = 0
        [HideInInspector] _Parallax("Grime", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Cutoff("Cutoff", Float) = .5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "CartoonForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half4 _Flash;
                half _VertexColor;
            CBUFFER_END
            // cor por vértice: bonecos fundidos (ModeloFundido) guardam a cor da peça em rgb e 1 - metal em a (só vale com _VertexColor = 1)
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fog : TEXCOORD2;
                half4 color : COLOR;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.color = lerp(half4(1, 1, 1, 1), v.color, _VertexColor);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half diffuse = saturate(dot(n, sun.direction) * .5h + .5h);
                half band = .24h + .35h * smoothstep(.3h,.34h,diffuse) + .35h * smoothstep(.7h,.74h,diffuse);
                // An illustrative soft fill keeps the face and coat readable in a night level.
                half fill = .4h + .22h * saturate(dot(n, normalize(half3(-.4h,.7h,.6h))));
                half3 albedo = lerp(_BaseColor.rgb * i.color.rgb, _Flash.rgb, _Flash.a);
                half3 color = albedo * (fill + sun.color * band * sun.shadowAttenuation * .45h);
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(1 - saturate(dot(n, view)), 3);
                color += rim * half3(.07h,.085h,.12h);
                half spec = pow(saturate(dot(n, normalize(sun.direction + view))), 24);
                color += step(.8h,spec) * saturate(_Metallic + 1 - i.color.a) * .18h;
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    Fallback "Universal Render Pipeline/Lit"
}
