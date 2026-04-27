Shader "SonarBounce/TrapWarningReveal"
{
    Properties
    {
        _BandWidth ("Pulse Band Width", Float) = 0.55
        _RevealFillStrength ("Reveal Fill Strength", Range(0.0, 1.0)) = 0.5
        _PulseColor ("Warning Light Color", Color) = (1.0, 0.05, 0.02, 1.0)
        _BgColor ("Unrevealed Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _EmissionStrength ("Pulse Emission Strength", Float) = 3.5
        _PulseRevealMultiplier ("Pulse Reveal Multiplier", Float) = 1.25
        _GlowRevealMultiplier ("Glow Reveal Multiplier", Float) = 1.0
        _AuraRevealMultiplier ("Near Aura Reveal Multiplier", Float) = 1.45
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
            Name "TrapWarningReveal"
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
                float  _BandWidth;
                float  _RevealFillStrength;
                float4 _PulseColor;
                float4 _BgColor;
                float  _EmissionStrength;
                float  _PulseRevealMultiplier;
                float  _GlowRevealMultiplier;
                float  _AuraRevealMultiplier;
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

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            float ComputePulseReveal(float3 worldPos, float3 worldNormal)
            {
                float reveal = 0.0;
                float bandWidth = max(0.001, _BandWidth);

                for (int i = 0; i < _PulseCount; i++)
                {
                    float3 pulseNormal = _PulseNormals[i].xyz;
                    if (dot(pulseNormal, pulseNormal) < 0.0001)
                        pulseNormal = float3(0.0, 1.0, 0.0);
                    pulseNormal = normalize(pulseNormal);

                    float3 delta = worldPos - _PulseOrigins[i].xyz;
                    float planeOffset = dot(delta, pulseNormal);
                    float3 planarDelta = delta - pulseNormal * planeOffset;
                    float planarDist = length(planarDelta);

                    float dist;

                    if (_PulseBoundsExtents[i].w > 0.5)
                    {
                        float3 sourceBoundsCenter = _PulseBoundsCenters[i].xyz;
                        float3 sourceBoundsExtents = _PulseBoundsExtents[i].xyz;
                        float3 sourceToPoint = worldPos - sourceBoundsCenter;

                        float3 wallBitangent = float3(0.0, 1.0, 0.0);
                        float3 wallTangent = cross(wallBitangent, pulseNormal);
                        if (dot(wallTangent, wallTangent) < 0.0001)
                            wallTangent = float3(1.0, 0.0, 0.0);
                        wallTangent = normalize(wallTangent);

                        float sameFacingMask = smoothstep(0.55, 0.9, dot(worldNormal, pulseNormal));

                        float tangentExtent = dot(abs(wallTangent), sourceBoundsExtents);
                        float bitangentExtent = dot(abs(wallBitangent), sourceBoundsExtents);
                        float normalExtent = dot(abs(pulseNormal), sourceBoundsExtents);

                        float tangentCoord = abs(dot(sourceToPoint, wallTangent));
                        float bitangentCoord = abs(dot(sourceToPoint, wallBitangent));
                        float normalCoord = abs(dot(sourceToPoint, pulseNormal));

                        float tangentMask = 1.0 - smoothstep(tangentExtent + bandWidth, tangentExtent + (bandWidth * 2.0), tangentCoord);
                        float bitangentMask = 1.0 - smoothstep(bitangentExtent + bandWidth, bitangentExtent + (bandWidth * 2.0), bitangentCoord);
                        float planeMask = 1.0 - smoothstep(normalExtent + 0.02, normalExtent + 0.08, normalCoord);

                        float sourceSurfaceMask = sameFacingMask * tangentMask * bitangentMask * planeMask;
                        float genericSurfaceDist = length(delta);
                        dist = lerp(genericSurfaceDist, planarDist, sourceSurfaceMask);
                    }
                    else
                    {
                        float sameFacingSurface = saturate(dot(worldNormal, pulseNormal));
                        float samePlaneSurface = 1.0 - smoothstep(0.03, 0.18, abs(planeOffset));
                        float fastPlanarSpread = sameFacingSurface * samePlaneSurface;
                        float crossSurfacePenalty = 1.0 - fastPlanarSpread;
                        dist = planarDist + abs(planeOffset) * crossSurfacePenalty;
                    }

                    float radius = _PulseOrigins[i].w;
                    float band = 1.0 - saturate(abs(dist - radius) / bandWidth);
                    band = smoothstep(0.0, 1.0, band);

                    float fill = 1.0 - smoothstep(max(radius - (bandWidth * 1.5), 0.0), radius, dist);
                    float waveReveal = band * _PulseWaveIntensities[i] * _PulseIntensities[i];
                    float fillReveal = fill * _RevealFillStrength * _PulseIntensities[i];

                    reveal = max(reveal, max(waveReveal, fillReveal));
                }

                return reveal;
            }

            float ComputeGlowReveal(float3 worldPos)
            {
                float glow = 0.0;
                for (int i = 0; i < _GlowCount; i++)
                {
                    float dist = distance(worldPos, _GlowPoints[i].xyz);
                    float radius = max(0.001, _GlowPoints[i].w);
                    float strength = saturate(1.0 - dist / radius);
                    glow = max(glow, strength * strength * _GlowIntensities[i]);
                }
                return glow;
            }

            float ComputePlayerAuraReveal(float3 worldPos)
            {
                if (_PlayerAuraPosition.w <= 0.001 || _PlayerAuraParams.x <= 0.001)
                    return 0.0;

                float auraDist = distance(worldPos, _PlayerAuraPosition.xyz);
                float auraT = saturate(1.0 - auraDist / _PlayerAuraPosition.w);
                auraT = pow(auraT, max(0.01, _PlayerAuraParams.y));
                return auraT * _PlayerAuraParams.x;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float3 worldNormal = normalize(IN.worldNormal);
                float pulseReveal = ComputePulseReveal(IN.worldPos, worldNormal) * _PulseRevealMultiplier;
                float glowReveal = ComputeGlowReveal(IN.worldPos) * _GlowRevealMultiplier;
                float auraReveal = ComputePlayerAuraReveal(IN.worldPos) * _AuraRevealMultiplier;
                float totalReveal = saturate(pulseReveal + glowReveal + auraReveal);

                float3 litColor = _PulseColor.rgb * (_EmissionStrength * totalReveal);
                float3 finalColor = lerp(_BgColor.rgb, litColor, totalReveal);
                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
