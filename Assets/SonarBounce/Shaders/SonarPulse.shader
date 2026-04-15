Shader "SonarBounce/PulseReveal"
{
    Properties
    {
        _GridDensity ("Grid Density", Float) = 2.0
        _GridLineWidth ("Grid Line Width", Range(0.01, 0.15)) = 0.05
        _BandWidth ("Pulse Band Width", Float) = 0.6
        _PulseColor ("Pulse Color", Color) = (0.0, 0.8, 1.0, 1.0)
        _BgColor ("Background Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _EmissionStrength ("Emission Strength", Float) = 3.0
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
            Name "PulseReveal"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_PULSES 12

            float4 _PulseOrigins[MAX_PULSES];
            float  _PulseIntensities[MAX_PULSES];
            int    _PulseCount;

            #define MAX_GLOWPOINTS 8
            float4 _GlowPoints[MAX_GLOWPOINTS];
            float  _GlowIntensities[MAX_GLOWPOINTS];
            int    _GlowCount;

            CBUFFER_START(UnityPerMaterial)
                float  _GridDensity;
                float  _GridLineWidth;
                float  _BandWidth;
                float4 _PulseColor;
                float4 _BgColor;
                float  _EmissionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ============================================================
            // Vertex
            // ============================================================
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionCS  = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float ComputeGrid(float3 worldPos, float3 worldNormal, float density, float lineWidth)
            {
                float3 absN = abs(worldNormal);
                float2 gridUV;

                if (absN.y >= absN.x && absN.y >= absN.z)
                    gridUV = worldPos.xz;  
                else if (absN.x >= absN.z)
                    gridUV = worldPos.yz;  
                else
                    gridUV = worldPos.xy;  

                float2 grid = abs(frac(gridUV * density) - 0.5);
                float lineMask = step(min(grid.x, grid.y), lineWidth);
                return lineMask;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 wPos = IN.worldPos;
                float3 wNorm = normalize(IN.worldNormal);

                float reveal = 0.0;
                for (int i = 0; i < _PulseCount; i++)
                {
                    float dist = distance(wPos, _PulseOrigins[i].xyz);
                    float radius = _PulseOrigins[i].w;

                    float band = 1.0 - saturate(abs(dist - radius) / _BandWidth);
                    band = smoothstep(0.0, 1.0, band);

                    reveal = max(reveal, band * _PulseIntensities[i]);
                }

                float glow = 0.0;
                for (int j = 0; j < _GlowCount; j++)
                {
                    float dist = distance(wPos, _GlowPoints[j].xyz);
                    float r = _GlowPoints[j].w; 
                    float g = saturate(1.0 - dist / r);
                    g = g * g; 
                    glow = max(glow, g * _GlowIntensities[j]);
                }

                float totalReveal = saturate(reveal + glow);

                float gridMask = ComputeGrid(wPos, wNorm, _GridDensity, _GridLineWidth);

                float fillFactor = 0.08; 
                float brightness = lerp(fillFactor, 1.0, gridMask);

                float3 litColor = _PulseColor.rgb * brightness * _EmissionStrength * totalReveal;
                float3 finalColor = lerp(_BgColor.rgb, litColor, totalReveal);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
