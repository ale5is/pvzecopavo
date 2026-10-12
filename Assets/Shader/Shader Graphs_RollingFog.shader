Shader "Shader Graphs/RollingFog"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
        [ToggleUI] _WorldSpace ("WorldSpace", Float) = 0
        _FogSize ("FogSize", Float) = 8.44
        _FogIntensity ("FogIntensity", Float) = 0
        _FogColor ("FogColor", Vector) = (1,1,1,0)
        _Fog_Speed ("Fog Speed", Vector) = (0,0,0,0)
        _Scale ("Scale", Vector) = (0,0,0,0)

        [HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        LOD 200
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RollingFog"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _WorldSpace;
                float _FogSize;
                float _FogIntensity;
                float4 _FogColor;
                float4 _Fog_Speed;
                float4 _Scale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                output.color = input.color;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float fogSize = max(abs(_FogSize), 0.001);

                float scaleX = abs(_Scale.x) > 0.0001
                    ? _Scale.x
                    : 1.0;

                float scaleY = abs(_Scale.y) > 0.0001
                    ? _Scale.y
                    : 1.0;

                float2 uvLocal = input.uv * float2(scaleX, scaleY);
                float2 uvWorld = input.positionWS.xz / fogSize;

                float2 uv = lerp(
                    uvLocal,
                    uvWorld,
                    saturate(_WorldSpace)
                );

                uv += _Fog_Speed.xy * time;

                half4 fogTexture = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    uv
                );

                half mask = saturate(fogTexture.a * fogTexture.r);
                half intensity = saturate(_FogIntensity);

                half alpha = mask * intensity * _FogColor.a;
                half3 color = fogTexture.rgb * _FogColor.rgb;

                color *= input.color.rgb;
                alpha *= input.color.a;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}