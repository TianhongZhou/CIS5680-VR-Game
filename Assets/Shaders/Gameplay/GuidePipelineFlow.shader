Shader "CIS5680VRGame/GuidePipelineFlow"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.02, 0.55, 0.8, 1.0)
        _EmissionColor ("Emission Color", Color) = (0.02, 0.55, 0.8, 1.0)
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
            Name "GuidePipelineFlow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
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
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float cross = abs(input.uv.y * 2.0 - 1.0);
                float softWidth = 1.0 - smoothstep(0.18, 1.0, cross);
                float endFade = smoothstep(0.0, 0.04, input.uv.x) * (1.0 - smoothstep(0.96, 1.0, input.uv.x));
                float alpha = input.color.a * softWidth * endFade;
                if (alpha <= 0.001)
                    discard;

                half3 color = (_BaseColor.rgb * input.color.rgb) + (_EmissionColor.rgb * input.color.a * 0.35);
                color *= 0.58 + softWidth * 0.62;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
