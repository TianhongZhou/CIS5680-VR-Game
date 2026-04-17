Shader "CIS5680VRGame/LocatorResponseRing"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1.0, 0.64, 0.12, 0.88)
        _Progress ("Progress", Range(0.0, 1.0)) = 0.0
        _Alpha ("Alpha", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+240"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "LocatorResponseRing"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Progress;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
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

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 p = input.uv * 2.0 - 1.0;
                float radius = length(p);
                if (radius > 1.0)
                    discard;

                float progress = saturate(_Progress);
                float ringCenter = lerp(0.08, 0.92, progress);
                float ringWidth = lerp(0.18, 0.08, progress);
                float ring = 1.0 - smoothstep(ringWidth, ringWidth + 0.04, abs(radius - ringCenter));
                float centerGlow = (1.0 - smoothstep(0.0, 0.35, radius)) * saturate(1.0 - progress * 2.2);
                float intensity = max(ring, centerGlow * 0.7);
                if (intensity <= 0.001)
                    discard;

                half4 color = _BaseColor;
                color.rgb *= 0.95 + ring * 0.5 + centerGlow * 0.25;
                color.a *= intensity * saturate(_Alpha);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
