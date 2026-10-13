Shader "LitPlantShader"
{
    Properties
    {
        _MainTex ("Diffuse", 2D) = "white" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _EyeTex ("Eye Texture", 2D) = "black" {}
        [Toggle] _UseEyeTex ("Use Eye Texture", Float) = 0
        _GrayScale ("GrayScale Amount", Range(0,1)) = 1
        _Brightness ("Brightness", Float) = 1
        [Toggle] _OpenGray ("Enable Grayscale", Float) = 1
        [Toggle] _OpenSolid ("Enable Solid Tint", Float) = 0
        _SolideColor ("Solid Tint Color", Color) = (1,1,1,1)
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
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
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Plant2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex CombinedShapeLightVertex
            #pragma fragment CombinedShapeLightFragment
            #pragma target 3.0

            #pragma multi_compile_instancing
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __
            #pragma multi_compile _ DEBUG_DISPLAY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            TEXTURE2D(_EyeTex);
            SAMPLER(sampler_EyeTex);
            TEXTURE2D(_AlphaTex);
            SAMPLER(sampler_AlphaTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _RendererColor;
                float4 _Flip;
                float4 _SolideColor;
                float _GrayScale;
                float _Brightness;
                float _OpenGray;
                float _OpenSolid;
                float _UseEyeTex;
                float _AngleX;
                float _AngleY;
                float _ScaleX;
                float _ScaleY;
                float _Alpha;
                float _IsVisible;
                float _EnableExternalAlpha;
            CBUFFER_END

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half2 lightingUV : TEXCOORD1;
                float2 alphaUV : TEXCOORD2;

                #if defined(DEBUG_DISPLAY)
                float3 positionWS : TEXCOORD3;
                #endif

                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 ApplyPlantEffects(half4 c, float2 uv, float2 alphaUV)
            {
                half mask = SAMPLE_TEXTURE2D(
                    _MaskTex, sampler_MaskTex, uv
                ).r;

                if (_OpenSolid > 0.5)
                {
                    c.rgb = lerp(
                        c.rgb,
                        _SolideColor.rgb,
                        saturate(mask * _SolideColor.a)
                    );
                }

                if (_OpenGray > 0.5)
                {
                    half gray = dot(c.rgb, half3(0.299, 0.587, 0.114));
                    c.rgb = lerp(c.rgb, gray.xxx, saturate(_GrayScale));
                }

                c.rgb *= max(_Brightness, 0.0);

                if (_UseEyeTex > 0.5)
                {
                    half4 eye = SAMPLE_TEXTURE2D(
                        _EyeTex, sampler_EyeTex, uv
                    );

                    c.rgb = lerp(c.rgb, eye.rgb, eye.a);
                }

                if (_EnableExternalAlpha > 0.5)
                {
                    c.a *= SAMPLE_TEXTURE2D(
                        _AlphaTex, sampler_AlphaTex, alphaUV
                    ).r;
                }

                c.a *= saturate(_Alpha);

                return c;
            }

            Varyings CombinedShapeLightVertex(Attributes v)
            {
                Varyings o = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                #ifdef UNITY_INSTANCING_ENABLED
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteFlip);
                #endif

                float3 p = v.positionOS;
                p.x *= _ScaleX;
                p.y *= _ScaleY;

                // Giro plano 2D con el sentido invertido.
                float angle = -_AngleY;
                float s = sin(angle);
                float c = cos(angle);

                p.xy = float2(
                    p.x * c - p.y * s,
                    p.x * s + p.y * c
                );

                p.xy *= _Flip.xy;

                o.positionCS = TransformObjectToHClip(p);

                #if defined(DEBUG_DISPLAY)
                o.positionWS = TransformObjectToWorld(p);
                #endif

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.alphaUV = v.uv;

                o.lightingUV = half2(
                    ComputeScreenPos(
                        o.positionCS / max(o.positionCS.w, 0.0001)
                    ).xy
                );

                o.color = v.color * _Color * _RendererColor;

                #ifdef UNITY_INSTANCING_ENABLED
                o.color *= unity_SpriteColor;
                #endif

                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            half4 CombinedShapeLightFragment(Varyings i) : SV_Target
            {
                clip(_IsVisible + 0.5);

                half4 main = i.color * SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, i.uv
                );

                main = ApplyPlantEffects(main, i.uv, i.alphaUV);

                half4 mask = SAMPLE_TEXTURE2D(
                    _MaskTex, sampler_MaskTex, i.uv
                );

                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(
                    main.rgb, main.a, mask, surfaceData
                );

                InitializeInputData(i.uv, i.lightingUV, inputData);

                #if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_DATA_2D(inputData, i.positionWS);
                #endif

                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        Pass
        {
            Name "NormalsRendering"
            Tags { "LightMode" = "NormalsRendering" }

            HLSLPROGRAM
            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NormalMap_ST;
                float4 _Color;
                float4 _RendererColor;
                float4 _Flip;
                float4 _SolideColor;
                float _GrayScale;
                float _Brightness;
                float _OpenGray;
                float _OpenSolid;
                float _UseEyeTex;
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
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 tangent : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 tangentWS : TEXCOORD2;
                half3 bitangentWS : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings NormalsRenderingVertex(Attributes v)
            {
                Varyings o = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                #ifdef UNITY_INSTANCING_ENABLED
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteFlip);
                #endif

                float3 p = v.positionOS;
                p.x *= _ScaleX;
                p.y *= _ScaleY;

                float angle = -_AngleY;
                float s = sin(angle);
                float c = cos(angle);

                p.xy = float2(
                    p.x * c - p.y * s,
                    p.x * s + p.y * c
                );

                p.xy *= _Flip.xy;

                o.positionCS = TransformObjectToHClip(p);
                o.uv = TRANSFORM_TEX(v.uv, _NormalMap);
                o.color = v.color * _Color * _RendererColor;

                #ifdef UNITY_INSTANCING_ENABLED
                o.color *= unity_SpriteColor;
                #endif

                o.normalWS = -GetViewForwardDir();
                o.tangentWS = TransformObjectToWorldDir(v.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * v.tangent.w;

                return o;
            }

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"

            half4 NormalsRenderingFragment(Varyings i) : SV_Target
            {
                clip(_IsVisible + 0.5);

                half4 mainTex = i.color * SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, i.uv
                );

                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(
                    _NormalMap, sampler_NormalMap, i.uv
                ));

                return NormalsRenderingShared(
                    mainTex,
                    normalTS,
                    i.tangentWS,
                    i.bitangentWS,
                    i.normalWS
                );
            }
            ENDHLSL
        }

        Pass
        {
            Name "PlantUnlitFallback"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);
            TEXTURE2D(_EyeTex);
            SAMPLER(sampler_EyeTex);
            TEXTURE2D(_AlphaTex);
            SAMPLER(sampler_AlphaTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _RendererColor;
                float4 _Flip;
                float4 _SolideColor;
                float _GrayScale;
                float _Brightness;
                float _OpenGray;
                float _OpenSolid;
                float _UseEyeTex;
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
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 alphaUV : TEXCOORD1;

                #if defined(DEBUG_DISPLAY)
                float3 positionWS : TEXCOORD2;
                #endif

                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings UnlitVertex(Attributes v)
            {
                Varyings o = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                #ifdef UNITY_INSTANCING_ENABLED
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteFlip);
                #endif

                float3 p = v.positionOS;
                p.x *= _ScaleX;
                p.y *= _ScaleY;

                float angle = -_AngleY;
                float s = sin(angle);
                float c = cos(angle);

                p.xy = float2(
                    p.x * c - p.y * s,
                    p.x * s + p.y * c
                );

                p.xy *= _Flip.xy;

                o.positionCS = TransformObjectToHClip(p);

                #if defined(DEBUG_DISPLAY)
                o.positionWS = TransformObjectToWorld(p);
                #endif

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.alphaUV = v.uv;
                o.color = v.color * _Color * _RendererColor;

                #ifdef UNITY_INSTANCING_ENABLED
                o.color *= unity_SpriteColor;
                #endif

                return o;
            }

            half4 UnlitFragment(Varyings i) : SV_Target
            {
                clip(_IsVisible + 0.5);

                half4 c = i.color * SAMPLE_TEXTURE2D(
                    _MainTex, sampler_MainTex, i.uv
                );

                half mask = SAMPLE_TEXTURE2D(
                    _MaskTex, sampler_MaskTex, i.uv
                ).r;

                if (_OpenSolid > 0.5)
                {
                    c.rgb = lerp(
                        c.rgb,
                        _SolideColor.rgb,
                        saturate(mask * _SolideColor.a)
                    );
                }

                if (_OpenGray > 0.5)
                {
                    half gray = dot(c.rgb, half3(0.299, 0.587, 0.114));
                    c.rgb = lerp(c.rgb, gray.xxx, saturate(_GrayScale));
                }

                c.rgb *= max(_Brightness, 0.0);

                if (_UseEyeTex > 0.5)
                {
                    half4 eye = SAMPLE_TEXTURE2D(
                        _EyeTex, sampler_EyeTex, i.uv
                    );

                    c.rgb = lerp(c.rgb, eye.rgb, eye.a);
                }

                if (_EnableExternalAlpha > 0.5)
                {
                    c.a *= SAMPLE_TEXTURE2D(
                        _AlphaTex, sampler_AlphaTex, i.alphaUV
                    ).r;
                }

                c.a *= saturate(_Alpha);

                #if defined(DEBUG_DISPLAY)
                SurfaceData2D surfaceData;
                InputData2D inputData;
                half4 debugColor = 0;

                InitializeSurfaceData(c.rgb, c.a, surfaceData);
                InitializeInputData(i.uv, inputData);
                SETUP_DEBUG_DATA_2D(inputData, i.positionWS);

                if (CanDebugOverrideOutputColor(
                    surfaceData, inputData, debugColor))
                {
                    return debugColor;
                }
                #endif

                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}