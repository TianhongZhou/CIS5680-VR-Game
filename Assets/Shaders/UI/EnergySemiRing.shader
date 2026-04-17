Shader "CIS5680VRGame/EnergySemiRing"
{
    Properties
    {
        _FillAmount ("Fill Amount", Range(0.0, 1.0)) = 0.8
        _FillColor ("Fill Color", Color) = (1.0, 0.55, 0.0, 0.95)
        _BackgroundColor ("Background Color", Color) = (0.12, 0.12, 0.12, 0.45)
        _OuterRadius ("Outer Radius", Range(0.1, 1.0)) = 0.9
        _InnerRadius ("Inner Radius", Range(0.0, 0.95)) = 0.58
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.2)) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "EnergySemiRing"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _FillAmount;
                float4 _FillColor;
                float4 _BackgroundColor;
                float _OuterRadius;
                float _InnerRadius;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
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
                output.uv = input.uv;
                return output;
            }

            float RingMask(float radius, float innerRadius, float outerRadius, float softness)
            {
                float outer = 1.0 - smoothstep(outerRadius - softness, outerRadius + softness, radius);
                float inner = smoothstep(innerRadius - softness, innerRadius + softness, radius);
                return saturate(outer * inner);
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 p = input.uv * 2.0 - 1.0;
                float radius = length(p);
                float upperHalfMask = step(0.0, p.y);

                float ringMask = RingMask(radius, _InnerRadius, _OuterRadius, _EdgeSoftness) * upperHalfMask;
                if (ringMask <= 0.0001)
                    return float4(0, 0, 0, 0);

                float angle = atan2(p.y, p.x);
                // Reverse the arc fill direction so the upper side represents spent energy
                // while the lower side represents remaining energy.
                float progress = saturate(angle / PI);
                float fillMask = step(progress, saturate(_FillAmount));

                float4 color = lerp(_BackgroundColor, _FillColor, fillMask);
                color.a *= ringMask;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
