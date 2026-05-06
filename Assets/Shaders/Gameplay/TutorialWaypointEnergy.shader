Shader "CIS5680VRGame/TutorialWaypointEnergy"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.02, 0.55, 0.9, 1.0)
        _EmissionColor ("Emission Color", Color) = (0.4, 0.95, 1.0, 1.0)
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.65
        _FresnelPower ("Fresnel Power", Float) = 2.7
        _FlowStrength ("Flow Strength", Range(0.0, 1.0)) = 0.35
        _FlowScale ("Flow Scale", Float) = 2.8
        _FlowSpeed ("Flow Speed", Float) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "TutorialWaypointEnergy"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float _Alpha;
                float _FresnelPower;
                float _FlowStrength;
                float _FlowScale;
                float _FlowSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float4 color : COLOR;
                float2 uv : TEXCOORD2;
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
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 normal = normalize(input.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - input.worldPos);
                float fresnel = pow(1.0 - saturate(abs(dot(normal, viewDir))), max(0.01, _FresnelPower));

                float flowTime = _Time.y * _FlowSpeed;
                float verticalFlow = sin((input.worldPos.y * _FlowScale + flowTime) * 6.2831853) * 0.5 + 0.5;
                float orbitalFlow = sin((input.uv.x * 6.0 + flowTime * 1.7) * 6.2831853) * 0.5 + 0.5;
                float flow = lerp(verticalFlow, orbitalFlow, 0.45) * _FlowStrength;

                float softBand = 1.0 - smoothstep(0.72, 1.0, abs(input.uv.y * 2.0 - 1.0));
                float vertexFade = saturate(input.color.a);
                float alpha = _Alpha * vertexFade * saturate(0.08 + fresnel * 1.3 + flow * 0.7) * softBand;
                if (alpha <= 0.001)
                    discard;

                float glow = saturate(fresnel * 1.6 + flow + 0.25);
                float3 color = _BaseColor.rgb * (0.28 + glow * 0.35) + _EmissionColor.rgb * glow;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
