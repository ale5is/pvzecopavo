Shader "BaseUnitLitShader"
{
	Properties
	{
		_MainTex ("Diffuse", 2D) = "white" {}
		_MaskTex ("Mask", 2D) = "white" {}
		_NormalMap ("Normal Map", 2D) = "bump" {}
		_GrayScale ("GrayScale", Float) = 1
		_Brightness ("Brightness", Float) = 1
		[Toggle] _OpenGray ("OpenGray", Float) = 1
		[HideInInspector] _Color ("Tint", Vector) = (1,1,1,1)
		[HideInInspector] _RendererColor ("RendererColor", Vector) = (1,1,1,1)
		[HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
		[HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
		[HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
		_AngleX ("AngleX", Float) = 0
		_AngleY ("AngleY", Float) = 0
		_ScaleX ("ScaleX", Float) = 1
		_ScaleY ("ScaleY", Float) = 1
		_Alpha ("Alpha", Float) = 1
		_IsVisible ("IsVisible", Range(-1, 0)) = 0
	}

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"RenderType"="Transparent"
			"IgnoreProjector"="True"
			"CanUseSpriteAtlas"="True"
			"PreviewType"="Plane"
		}

		Cull Off
		Lighting Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct VertexInput
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct VertexOutput
			{
				float4 vertex : SV_POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			sampler2D _MainTex;
			sampler2D _MaskTex;
			sampler2D _NormalMap;
			sampler2D _AlphaTex;

			float4 _MainTex_ST;
			float4 _Color;
			float4 _RendererColor;
			float4 _Flip;

			float _GrayScale;
			float _Brightness;
			float _OpenGray;

			float _AngleX;
			float _AngleY;
			float _ScaleX;
			float _ScaleY;
			float _Alpha;
			float _IsVisible;

			float _EnableExternalAlpha;

			VertexOutput vert(VertexInput input)
			{
				VertexOutput output;

				float4 vertex = input.vertex;

				vertex.x *= _ScaleX;
				vertex.y *= _ScaleY;

				output.vertex = UnityObjectToClipPos(vertex);
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				output.color = input.color * _Color * _RendererColor;

				return output;
			}

			float4 frag(VertexOutput input) : SV_Target
			{
				float4 color = tex2D(_MainTex, input.uv);

				color *= input.color;

				if (_OpenGray > 0.5)
				{
					float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));
					color.rgb = lerp(color.rgb, gray.xxx, saturate(_GrayScale));
				}

				color.rgb *= _Brightness;

				color.a *= _Alpha;

				if (_EnableExternalAlpha > 0.5)
				{
					color.a *= tex2D(_AlphaTex, input.uv).r;
				}

				return color;
			}

			ENDHLSL
		}
	}

	Fallback "Sprites/Default"
}