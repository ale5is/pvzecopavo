
Shader "MyBaseShader"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Vector) = (1,1,1,1)
        _GrayScale ("GrayScale", Float) = 1
        _Brightness ("Brightness", Float) = 1
        [Toggle] _OpenGray ("OpenGray", Float) = 1
        [Toggle] _OpenBrightness ("OpenBrightness", Float) = 1
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
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SpriteUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _GrayScale;
                float _Brightness;
                float _OpenGray;
                float _OpenBrightness;
            CBUFFER_END

            struct VertexInput
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            VertexOutput vert(VertexInput input)
            {
                VertexOutput output = (VertexOutput)0;

                output.positionCS = TransformObjectToHClip(input.vertex.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;

                return output;
            }

            half4 frag(VertexOutput input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv
                ) * input.color;

                if (_OpenGray > 0.5)
                {
                    half gray = dot(
                        color.rgb,
                        half3(0.299, 0.587, 0.114)
                    );

                    color.rgb = lerp(
                        color.rgb,
                        gray.xxx,
                        saturate(_GrayScale)
                    );
                }

                if (_OpenBrightness > 0.5)
                {
                    color.rgb *= max(_Brightness, 0.0);
                }

                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
