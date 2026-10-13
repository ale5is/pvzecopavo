
Shader "LitBaseShader"
{
    Properties
    {
        _MainTex ("Diffuse", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}

        _GrayScale ("Grayscale Amount", Range(0,1)) = 1
        _Brightness ("Brightness", Float) = 1

        [Toggle] _OpenGray ("Enable Grayscale", Float) = 0
        [Toggle] _OpenDisplay ("Display", Float) = 1

        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)

        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        _AngleX ("Angle X", Float) = 0
        _AngleY ("Angle Y", Float) = 0
        _ScaleX ("Scale X", Float) = 1
        _ScaleY ("Scale Y", Float) = 1

        _Alpha ("Alpha", Range(0,1)) = 1
        _IsVisible ("Is Visible", Range(-1,0)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "BaseSprite"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            TEXTURE2D(_AlphaTex);
            SAMPLER(sampler_AlphaTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MaskTex_ST;
                float4 _NormalMap_ST;
                float4 _Color;
                float4 _RendererColor;
                float4 _Flip;

                float _GrayScale;
                float _Brightness;
                float _OpenGray;
                float _OpenDisplay;

                float _AngleX;
                float _AngleY;
                float _ScaleX;
                float _ScaleY;

                float _Alpha;
                float _IsVisible;
                float _EnableExternalAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 alphaUV : TEXCOORD1;
                float2 maskUV : TEXCOORD2;
                float2 normalUV : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 position = input.positionOS.xyz;

                position.x *= _ScaleX;
                position.y *= _ScaleY;

                float angleX = radians(_AngleX);
                float angleY = radians(_AngleY);

                float sinX = sin(angleX);
                float cosX = cos(angleX);

                position = float3(
                    position.x,
                    position.y * cosX - position.z * sinX,
                    position.y * sinX + position.z * cosX
                );

                float sinY = sin(angleY);
                float cosY = cos(angleY);

                position = float3(
                    position.x * cosY + position.z * sinY,
                    position.y,
                    -position.x * sinY + position.z * cosY
                );

                position.xy *= _Flip.xy;

                output.positionCS = TransformObjectToHClip(position);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.maskUV = TRANSFORM_TEX(input.uv, _MaskTex);
                output.normalUV = TRANSFORM_TEX(input.uv, _NormalMap);
                output.alphaUV = input.uv;
                output.color = input.color * _Color * _RendererColor;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(_OpenDisplay - 0.5);
                clip(_IsVisible + 0.5);

                half4 color = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv
                ) * input.color;

                half4 mask = SAMPLE_TEXTURE2D(
                    _MaskTex,
                    sampler_MaskTex,
                    input.maskUV
                );

                half3 normalTS = UnpackNormal(
                    SAMPLE_TEXTURE2D(
                        _NormalMap,
                        sampler_NormalMap,
                        input.normalUV
                    )
                );

                color.rgb *= lerp(1.0h, saturate(mask.rgb), 0.0h);
                color.rgb *= lerp(1.0h, saturate(normalTS.z), 0.0h);

                if (_OpenGray > 0.5)
                {
                    half grayscale = dot(
                        color.rgb,
                        half3(0.299, 0.587, 0.114)
                    );

                    color.rgb = lerp(
                        color.rgb,
                        grayscale.xxx,
                        saturate(_GrayScale)
                    );
                }

                color.rgb *= max(_Brightness, 0.0);

                if (_EnableExternalAlpha > 0.5)
                {
                    color.a *= SAMPLE_TEXTURE2D(
                        _AlphaTex,
                        sampler_AlphaTex,
                        input.alphaUV
                    ).r;
                }

                color.a *= saturate(_Alpha);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
