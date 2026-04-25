Shader "SonarBounce/PulseReveal"
{
    Properties
    {
        _GridDensity ("Grid Density", Float) = 2.0
        _GridLineWidth ("Grid Line Width", Range(0.01, 0.15)) = 0.05
        _UseMeshGridUv ("Use Mesh Grid UV", Float) = 0.0
        _BandWidth ("Pulse Band Width", Float) = 0.6
        _RevealFillStrength ("Reveal Fill Strength", Range(0.0, 1.0)) = 0.4
        _PulseColor ("Pulse Color", Color) = (0.0, 0.8, 1.0, 1.0)
        _BgColor ("Background Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _EmissionStrength ("Emission Strength", Float) = 3.0
        _CircuitGlitchStrength ("Circuit Glitch Strength", Range(0.0, 1.0)) = 0.0
        _CircuitGlitchTime ("Circuit Glitch Time", Float) = 0.0
        _CircuitGlitchSeed ("Circuit Glitch Seed", Float) = 0.0
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
            float4 _PulseBoundsCenters[MAX_PULSES];
            float4 _PulseBoundsExtents[MAX_PULSES];
            float  _PulseIntensities[MAX_PULSES];
            float  _PulseWaveIntensities[MAX_PULSES];
            int    _PulseCount;

            #define MAX_GLOWPOINTS 8
            float4 _GlowPoints[MAX_GLOWPOINTS];
            float  _GlowIntensities[MAX_GLOWPOINTS];
            int    _GlowCount;

            float4 _PlayerAuraPosition;
            float4 _PlayerAuraParams;

            CBUFFER_START(UnityPerMaterial)
                float  _GridDensity;
                float  _GridLineWidth;
                float  _UseMeshGridUv;
                float  _BandWidth;
                float  _RevealFillStrength;
                float4 _PulseColor;
                float4 _BgColor;
                float  _EmissionStrength;
                float  _CircuitGlitchStrength;
                float  _CircuitGlitchTime;
                float  _CircuitGlitchSeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 gridUv      : TEXCOORD0;
                float3 worldPos    : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
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
                OUT.gridUv      = IN.uv;
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float2 ComputeWorldGridUv(float3 worldPos, float3 worldNormal)
            {
                float3 absN = abs(worldNormal);
                float2 gridUv;

                if (absN.y >= absN.x && absN.y >= absN.z)
                    gridUv = worldPos.xz;
                else if (absN.x >= absN.z)
                    gridUv = worldPos.yz;
                else
                    gridUv = worldPos.xy;

                return gridUv;
            }

            float ComputeGrid(float2 gridUv, float density, float lineWidth)
            {
                float2 grid = abs(frac(gridUv * density) - 0.5);
                float lineMask = step(min(grid.x, grid.y), lineWidth);
                return lineMask;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
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

                    float dist;

                    if (_PulseBoundsExtents[i].w > 0.5)
                    {
                        float3 sourceBoundsCenter = _PulseBoundsCenters[i].xyz;
                        float3 sourceBoundsExtents = _PulseBoundsExtents[i].xyz;
                        float3 sourceToPoint = wPos - sourceBoundsCenter;

                        float3 wallBitangent = float3(0.0, 1.0, 0.0);
                        float3 wallTangent = cross(wallBitangent, pulseNormal);
                        if (dot(wallTangent, wallTangent) < 0.0001)
                            wallTangent = float3(1.0, 0.0, 0.0);
                        wallTangent = normalize(wallTangent);

                        float sameFacingMask = smoothstep(0.55, 0.9, dot(wNorm, pulseNormal));

                        float tangentExtent = dot(abs(wallTangent), sourceBoundsExtents);
                        float bitangentExtent = dot(abs(wallBitangent), sourceBoundsExtents);
                        float normalExtent = dot(abs(pulseNormal), sourceBoundsExtents);

                        float tangentCoord = abs(dot(sourceToPoint, wallTangent));
                        float bitangentCoord = abs(dot(sourceToPoint, wallBitangent));
                        float normalCoord = abs(dot(sourceToPoint, pulseNormal));

                        float tangentMask = 1.0 - smoothstep(tangentExtent + _BandWidth, tangentExtent + (_BandWidth * 2.0), tangentCoord);
                        float bitangentMask = 1.0 - smoothstep(bitangentExtent + _BandWidth, bitangentExtent + (_BandWidth * 2.0), bitangentCoord);
                        float planeMask = 1.0 - smoothstep(normalExtent + 0.02, normalExtent + 0.08, normalCoord);

                        float sourceSurfaceMask = sameFacingMask * tangentMask * bitangentMask * planeMask;
                        float genericSurfaceDist = length(delta);

                        // Wall-hit pulses get fast propagation only on the impacted wall.
                        // Everything else still participates, but uses regular 3D travel
                        // distance so floors and adjacent walls can reveal again.
                        dist = lerp(genericSurfaceDist, planarDist, sourceSurfaceMask);
                    }
                    else
                    {
                        float sameFacingSurface = saturate(dot(wNorm, pulseNormal));

                        // Only surfaces that are both similarly oriented and still
                        // close to the source plane get the "free" planar spread.
                        // This prevents elevated horizontal caps (like wall tops)
                        // from lighting as a thin bright strip when the pulse edge
                        // reaches the wall below.
                        float samePlaneSurface = 1.0 - smoothstep(0.03, 0.18, abs(planeOffset));
                        float fastPlanarSpread = sameFacingSurface * samePlaneSurface;
                        float crossSurfacePenalty = 1.0 - fastPlanarSpread;
                        dist = planarDist + abs(planeOffset) * crossSurfacePenalty;
                    }
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

                float playerAura = 0.0;
                if (_PlayerAuraPosition.w > 0.001 && _PlayerAuraParams.x > 0.001)
                {
                    float auraDist = distance(wPos, _PlayerAuraPosition.xyz);
                    float auraT = saturate(1.0 - auraDist / _PlayerAuraPosition.w);
                    auraT = pow(auraT, max(0.01, _PlayerAuraParams.y));
                    playerAura = auraT * _PlayerAuraParams.x;
                }

                float totalReveal = saturate(reveal + glow + playerAura);

                float2 gridUv = _UseMeshGridUv > 0.5 ? IN.gridUv : ComputeWorldGridUv(wPos, wNorm);
                float gridMask = ComputeGrid(gridUv, _GridDensity, _GridLineWidth);
                float glitchStrength = saturate(_CircuitGlitchStrength);
                float revealGate = step(0.001, totalReveal);
                float surge = 0.0;

                if (glitchStrength > 0.001)
                {
                    float segmentRate = lerp(2.5, 6.0, glitchStrength);
                    float2 segmentCell = floor(gridUv * max(0.01, _GridDensity) * segmentRate);
                    float frame = floor(_CircuitGlitchTime * lerp(5.0, 18.0, glitchStrength));
                    float outageNoise = Hash21(segmentCell + float2(_CircuitGlitchSeed, frame));
                    float surgeNoise = Hash21(segmentCell * 1.73 + float2(frame * 0.37, _CircuitGlitchSeed * 3.11));
                    float outage = step(1.0 - glitchStrength * 0.55, outageNoise);
                    surge = step(1.0 - glitchStrength * 0.32, surgeNoise);

                    gridMask *= lerp(1.0, 1.0 - outage * 0.92, glitchStrength * revealGate);
                    surge *= revealGate;
                }

                float fillFactor = 0.08; 
                float brightness = lerp(fillFactor, 1.0, gridMask);
                brightness *= 1.0 + surge * glitchStrength * 1.65;

                float3 litColor = _PulseColor.rgb * brightness * _EmissionStrength * totalReveal;
                float3 finalColor = lerp(_BgColor.rgb, litColor, totalReveal);

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
