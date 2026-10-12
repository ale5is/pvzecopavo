Shader "Unlit/GameUnitShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _AngleX ("AngleX", Float) = 0
        _AngleY ("AngleY", Float) = 0
        _ScaleX ("ScaleX", Float) = 1
        _ScaleY ("ScaleY", Float) = 1
        _Alpha ("Alpha", Range(0,1)) = 1
        _IsVisible ("IsVisible", Range(-1,0)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "GameUnitForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _AngleX;
                float _AngleY;
                float _ScaleX;
                float _ScaleY;
                float _Alpha;
                float _IsVisible;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 p = input.positionOS.xyz;

                p.x *= _ScaleX;
                p.y *= _ScaleY;

                float ax = radians(_AngleX);
                float ay = radians(_AngleY);

                float sinX = sin(ax);
                float cosX = cos(ax);

                p = float3(
                    p.x,
                    p.y * cosX - p.z * sinX,
                    p.y * sinX + p.z * cosX
                );

                float sinY = sin(ay);
                float cosY = cos(ay);

                p = float3(
                    p.x * cosY + p.z * sinY,
                    p.y,
                    -p.x * sinY + p.z * cosY
                );

                output.positionCS = TransformObjectToHClip(p);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(_IsVisible + 0.5);

                half4 color = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv
                );

                color *= input.color;
                color.a *= saturate(_Alpha);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}