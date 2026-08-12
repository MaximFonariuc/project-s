Shader "URP/BlurDistortion"
{
	Properties
	{
		_Color ("Main Color", Color) = (1,1,1,1)
		_BumpAmt ("Distortion", Range(0,128)) = 10
		_MainTex ("Tint Color (RGB)", 2D) = "white" {}
		_BumpMap ("Normalmap", 2D) = "bump" {}
		_Size ("Size", Range(0,20)) = 1
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Opaque" "IgnoreProjector"="True" }
		Pass
		{
			Name "HorizontalBlur"
			Tags{"LightMode"="UniversalForward"}

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _Color;
				float _BumpAmt;
				float _Size;
				float4 _MainTex_ST;
				float4 _BumpMap_ST;
			CBUFFER_END

			TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};
			struct Varyings
			{
				float4 posCS     : SV_POSITION;
				float4 screenPos : TEXCOORD0;
			};

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.posCS = TransformObjectToHClip(IN.positionOS.xyz);
				OUT.screenPos = ComputeScreenPos(OUT.posCS);
				return OUT;
			}

			half4 frag(Varyings IN) : SV_Target
			{
				float2 uv = IN.screenPos.xy / IN.screenPos.w;
				float2 texelSize = _ScreenParams.zw;
				half4 sum = 0;

				#define GRABPIXEL(weight,ofs) SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(texelSize.x*ofs*_Size,0)) * weight

				sum += GRABPIXEL(0.05,-4.0);
				sum += GRABPIXEL(0.09,-3.0);
				sum += GRABPIXEL(0.12,-2.0);
				sum += GRABPIXEL(0.15,-1.0);
				sum += GRABPIXEL(0.18, 0.0);
				sum += GRABPIXEL(0.15, 1.0);
				sum += GRABPIXEL(0.12, 2.0);
				sum += GRABPIXEL(0.09, 3.0);
				sum += GRABPIXEL(0.05, 4.0);

				return sum;
			}
			ENDHLSL
		}

		Pass
		{
			Name "VerticalBlur"
			Tags{"LightMode"="UniversalForward"}

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float _Size;
			CBUFFER_END

			TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);

			struct Attributes { float4 posOS:POSITION; };
			struct Varyings
			{
				float4 posCS:SV_POSITION;
				float4 screenPos:TEXCOORD0;
			};

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
				OUT.screenPos = ComputeScreenPos(OUT.posCS);
				return OUT;
			}

			half4 frag(Varyings IN):SV_Target
			{
				float2 uv = IN.screenPos.xy / IN.screenPos.w;
				float2 texelSize = _ScreenParams.zw;
				half4 sum=0;

				#define GRABPIXEL(weight,ofs) SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv + float2(0,texelSize.y*ofs*_Size)) * weight

				sum += GRABPIXEL(0.05,-4.0);
				sum += GRABPIXEL(0.09,-3.0);
				sum += GRABPIXEL(0.12,-2.0);
				sum += GRABPIXEL(0.15,-1.0);
				sum += GRABPIXEL(0.18, 0.0);
				sum += GRABPIXEL(0.15, 1.0);
				sum += GRABPIXEL(0.12, 2.0);
				sum += GRABPIXEL(0.09, 3.0);
				sum += GRABPIXEL(0.05, 4.0);

				return sum;
			}
			ENDHLSL
		}

		Pass
		{
			Name "DistortionTint"
			Tags{"LightMode"="UniversalForward"}

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _Color;
				float _BumpAmt;
				float4 _MainTex_ST;
				float4 _BumpMap_ST;
			CBUFFER_END

			TEXTURE2D(_CameraOpaqueTexture); SAMPLER(sampler_CameraOpaqueTexture);
			TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
			TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

			struct Attributes
			{
				float4 posOS:POSITION;
				float2 uv:TEXCOORD0;
			};
			struct Varyings
			{
				float4 posCS:SV_POSITION;
				float4 screenPos:TEXCOORD0;
				float2 uvMain:TEXCOORD1;
				float2 uvBump:TEXCOORD2;
			};

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
				OUT.screenPos = ComputeScreenPos(OUT.posCS);
				OUT.uvMain = TRANSFORM_TEX(IN.uv,_MainTex);
				OUT.uvBump = TRANSFORM_TEX(IN.uv,_BumpMap);
				return OUT;
			}

			half4 frag(Varyings IN):SV_Target
			{
				float2 uv = IN.screenPos.xy / IN.screenPos.w;
				half2 bump = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,IN.uvBump)).rg;
				float2 offset = bump * _BumpAmt * _ScreenParams.zw;
				uv += offset;

				half4 col = SAMPLE_TEXTURE2D(_CameraOpaqueTexture,sampler_CameraOpaqueTexture,uv);
				half4 tint = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,IN.uvMain) * _Color;
				return col * tint;
			}
			ENDHLSL
		}
	}
}
