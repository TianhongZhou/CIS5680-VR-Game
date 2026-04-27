Shader "CIS5680/Sonar Probe Rim Fresnel"
{
    Properties
    {
        _RimColor ("Rim Color", Color) = (0.0, 0.85, 1.0, 0.32)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.2
        _RimIntensity ("Rim Intensity", Range(0.0, 4.0)) = 1.35
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.viewDirWS = normalize(GetWorldSpaceViewDir(positionInputs.positionWS));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half fresnel = pow(saturate(1.0h - abs(dot(normalize(input.normalWS), normalize(input.viewDirWS)))), _RimPower);
                fresnel = saturate(fresnel * _RimIntensity);

                half4 color = _RimColor;
                color.rgb *= fresnel;
                color.a *= fresnel;
                return color;
            }
            ENDHLSL
        }
    }
}
