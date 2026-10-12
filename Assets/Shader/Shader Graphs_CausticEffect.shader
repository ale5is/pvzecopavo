Shader "Shader Graphs/CausticEffect"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
        [ToggleUI] _WorldSpace ("WorldSpace", Float) = 0
        _FogSize ("FogSize", Float) = 8.44
        _FogColor ("FogColor", Vector) = (1,1,1,0)
        _Fog_Speed ("Fog Speed", Vector) = (0,0,0,0)
        _Speed2 ("Speed2", Vector) = (0,0,0,0)
        _Scale ("Scale", Vector) = (0,0,0,0)
        [NoScaleOffset] _SampleTexture2D_d8d34466f5714a48a7a8cb8d46a4e209_Texture_1 ("Texture2D", 2D) = "white" {}

        [HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 200
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "CausticEffect"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_SampleTexture2D_d8d34466f5714a48a7a8cb8d46a4e209_Texture_1);
            SAMPLER(sampler_SampleTexture2D_d8d34466f5714a48a7a8cb8d46a4e209_Texture_1);

            CBUFFER_START(UnityPerMaterial)
                float _WorldSpace;
                float _FogSize;
                float4 _FogColor;
                float4 _Fog_Speed;
                float4 _Speed2;
                float4 _Scale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;

                return output;
            }

            float2 GetCausticUV(float2 uv, float3 positionWS)
            {
                float scaleX = abs(_Scale.x) > 0.0001 ? _Scale.x : 1.0;
                float scaleY = abs(_Scale.y) > 0.0001 ? _Scale.y : 1.0;
                float fogSize = max(abs(_FogSize), 0.001);

                float2 uvLocal = uv * float2(scaleX, scaleY);
                float2 uvWorld = positionWS.xz / fogSize;

                return lerp(uvLocal, uvWorld, saturate(_WorldSpace));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 baseUV = GetCausticUV(input.uv, input.positionWS);
                float time = _Time.y;

                float2 uv1 = baseUV + _Fog_Speed.xy * time;
                float2 uv2 = baseUV + _Speed2.xy * time;

                half4 tex1 = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    uv1
                );

                half4 tex2 = SAMPLE_TEXTURE2D(
                    _SampleTexture2D_d8d34466f5714a48a7a8cb8d46a4e209_Texture_1,
                    sampler_SampleTexture2D_d8d34466f5714a48a7a8cb8d46a4e209_Texture_1,
                    uv2
                );

                half3 caustics = tex1.rgb * tex2.rgb;
                half3 result = caustics * _FogColor.rgb;

                return half4(result, 1.0);
            }

            ENDHLSL
        }
    }

    FallBack Off
}