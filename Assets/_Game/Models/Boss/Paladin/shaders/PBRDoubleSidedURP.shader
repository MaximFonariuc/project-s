Shader "URP/PBRDoubleSided"
{
    Properties
    {
        _color ("Color", 2D) = "white" {}
        _Metallic ("Metallic", Range(0,1)) = 0
        _roughness ("Roughness", 2D) = "white" {}
        _normal ("Normal", 2D) = "bump" {}
        _AO ("AO", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "Queue"="Geometry"
        }
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode"="UniversalForward"
            }
            Blend One Zero
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_color);
            SAMPLER(sampler_color);
            TEXTURE2D(_roughness);
            SAMPLER(sampler_roughness);
            TEXTURE2D(_normal);
            SAMPLER(sampler_normal);
            TEXTURE2D(_AO);
            SAMPLER(sampler_AO);

            CBUFFER_START(UnityPerMaterial)
                float4 _color_ST;
                float4 _roughness_ST;
                float4 _normal_ST;
                float4 _AO_ST;
                float _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.texcoord, _color);

                VertexNormalInputs normInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.normalWS = normInputs.normalWS;
                OUT.tangentWS = normInputs.tangentWS;
                OUT.bitangentWS = normInputs.bitangentWS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                float3 albedo = SAMPLE_TEXTURE2D(_color, sampler_color, uv).rgb;
                float roughness = SAMPLE_TEXTURE2D(_roughness, sampler_roughness, uv).r;
                float smoothness = 1.0 - roughness;

                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_normal, sampler_normal, uv));
                float3x3 TBN = float3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                float3 normalWS = normalize(mul(normalTS, TBN));

                float ao = SAMPLE_TEXTURE2D(_AO, sampler_AO, uv).r;

                SurfaceData surfaceData;
                surfaceData.albedo = albedo;
                surfaceData.specular = 0;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = ao;
                surfaceData.emission = 0;
                surfaceData.alpha = 1;
                surfaceData.clearCoatMask = 0;
                surfaceData.clearCoatSmoothness = 0;

                InputData inputData;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);

                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
		        inputData.fogCoord = ComputeFogFactor(IN.positionHCS.z);
                #else
                inputData.fogCoord = 0;
                #endif

                inputData.vertexLighting = 0;
                inputData.bakedGI = SampleSH(normalWS);

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}