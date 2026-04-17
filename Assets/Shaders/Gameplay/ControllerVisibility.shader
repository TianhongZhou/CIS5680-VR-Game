Shader "CIS5680VRGame/ControllerVisibility"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.04, 0.05, 0.06, 1.0)
        _RimColor ("Rim Color", Color) = (0.3, 0.85, 1.0, 0.5)
        _EmissionColor ("Top Accent", Color) = (0.06, 0.18, 0.24, 0.2)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.5
        _Exposure ("Exposure", Range(0.0, 4.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ControllerVisibility"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float4 _EmissionColor;
                float _RimPower;
                float _Exposure;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.worldNormal = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 worldNormal = normalize(input.worldNormal);
                float3 viewDir = SafeNormalize(_WorldSpaceCameraPos.xyz - input.worldPos);

                float fresnel = pow(1.0 - saturate(dot(worldNormal, viewDir)), max(0.01, _RimPower));
                float topMask = saturate(worldNormal.y * 0.5 + 0.5);
                topMask *= topMask;

                float3 finalColor = _BaseColor.rgb;
                finalColor += _RimColor.rgb * (fresnel * _RimColor.a);
                finalColor += _EmissionColor.rgb * (topMask * _EmissionColor.a);
                finalColor *= _Exposure;

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
