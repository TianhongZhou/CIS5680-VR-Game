Shader "CIS5680VRGame/LocatorScanPulse"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.98, 0.74, 0.24, 0.92)
        _Progress ("Progress", Range(0.0, 1.0)) = 0.0
        _OuterFade ("Outer Fade", Range(0.0, 1.0)) = 1.0
        _ConeHalfAngle ("Cone Half Angle", Range(1.0, 179.0)) = 60.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+200"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "LocatorScanPulse"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Progress;
                float _OuterFade;
                float _ConeHalfAngle;
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

                float halfAngleRad = radians(_ConeHalfAngle);
                float angle = atan2(p.x, p.y);
                float coneMask = 1.0 - smoothstep(halfAngleRad - 0.08, halfAngleRad + 0.08, abs(angle));

                float head = saturate(_Progress);
                float ringCenter = max(0.02, head);
                float ringWidth = lerp(0.18, 0.08, head);
                float ringMask = 1.0 - smoothstep(ringWidth, ringWidth + 0.035, abs(radius - ringCenter));

                float trailMask = saturate((ringCenter - radius) / max(0.05, ringCenter));
                trailMask *= 1.0 - smoothstep(0.0, 0.92, radius / max(0.08, ringCenter));

                float intensity = max(ringMask, trailMask * 0.28) * coneMask;
                if (intensity <= 0.001)
                    discard;

                half4 color = _BaseColor;
                color.rgb *= 0.9 + ringMask * 0.65;
                color.a *= intensity * saturate(0.35 + _OuterFade * 0.65);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
