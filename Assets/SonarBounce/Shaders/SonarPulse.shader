Shader "SonarBounce/PulseReveal"
{
    Properties
    {
        _GridDensity ("Grid Density", Float) = 2.0
        _GridLineWidth ("Grid Line Width", Range(0.01, 0.15)) = 0.05
        _BandWidth ("Pulse Band Width", Float) = 0.6
        _RevealFillStrength ("Reveal Fill Strength", Range(0.0, 1.0)) = 0.4
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
            float4 _PulseNormals[MAX_PULSES];
            float  _PulseIntensities[MAX_PULSES];
            float  _PulseWaveIntensities[MAX_PULSES];
            int    _PulseCount;

            #define MAX_GLOWPOINTS 8
            float4 _GlowPoints[MAX_GLOWPOINTS];
            float  _GlowIntensities[MAX_GLOWPOINTS];
            int    _GlowCount;

            CBUFFER_START(UnityPerMaterial)
                float  _GridDensity;
                float  _GridLineWidth;
                float  _BandWidth;
                float  _RevealFillStrength;
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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ============================================================
            // Vertex
            // ============================================================
            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

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
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 wPos = IN.worldPos;
                float3 wNorm = normalize(IN.worldNormal);

                float reveal = 0.0;
                for (int i = 0; i < _PulseCount; i++)
                {
                    float3 pulseNormal = _PulseNormals[i].xyz;
                    if (dot(pulseNormal, pulseNormal) < 0.0001)
                        pulseNormal = float3(0.0, 1.0, 0.0);
                    pulseNormal = normalize(pulseNormal);

                    float3 delta = wPos - _PulseOrigins[i].xyz;
                    float planeOffset = dot(delta, pulseNormal);
                    float3 planarDelta = delta - pulseNormal * planeOffset;
                    float planarDist = length(planarDelta);

                    // Floors spread across the hit plane first; vertical surfaces
                    // add extra climb distance so walls light only after the pulse reaches them.
                    float surfaceVerticality = saturate(1.0 - abs(dot(wNorm, pulseNormal)));
                    float dist = planarDist + abs(planeOffset) * surfaceVerticality;
                    float radius = _PulseOrigins[i].w;

                    float band = 1.0 - saturate(abs(dist - radius) / _BandWidth);
                    band = smoothstep(0.0, 1.0, band);

                    float fill = 1.0 - smoothstep(max(radius - (_BandWidth * 1.5), 0.0), radius, dist);
                    float waveReveal = band * _PulseWaveIntensities[i] * _PulseIntensities[i];
                    float fillReveal = fill * _RevealFillStrength * _PulseIntensities[i];
                    float pulseReveal = max(waveReveal, fillReveal);

                    reveal = max(reveal, pulseReveal);
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
