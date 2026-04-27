using System.Collections.Generic;
using CIS5680VRGame.Balls;
using UnityEngine;

namespace CIS5680VRGame.Generation
{
    [DisallowMultipleComponent]
    public sealed class MazeEntranceGateVisualController : MonoBehaviour
    {
        enum LightRole
        {
            Flow,
            Scanner,
            Calibration,
            Amber,
        }

        static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Renderer[] m_TargetRenderers;
        [SerializeField] Light[] m_GlowLights;
        [SerializeField] Color m_CyanColor = new(0.02f, 0.8f, 1f, 1f);
        [SerializeField] Color m_CoreColor = new(0.68f, 1f, 1f, 1f);
        [SerializeField] Color m_AmberColor = new(1f, 0.55f, 0.12f, 1f);
        [SerializeField, Min(0f)] float m_BreathSpeed = 0.85f;
        [SerializeField, Min(0f)] float m_FlowSpeed = 1.35f;
        [SerializeField, Min(0f)] float m_MinLightIntensity = 0.03f;
        [SerializeField, Min(0f)] float m_MaxLightIntensity = 0.18f;
        [SerializeField, Min(0f)] float m_PulseFlashDuration = 0.55f;
        [SerializeField, Min(0f)] float m_PulseFlashStrength = 2.8f;
        [SerializeField, Min(0f)] float m_PulseReachMargin = 1.5f;

        readonly List<RendererState> m_RendererStates = new();
        MaterialPropertyBlock m_PropertyBlock;
        float m_PhaseOffset;
        float m_PulseFlashUntil;

        void Awake()
        {
            m_PhaseOffset = Random.value * Mathf.PI * 2f;
            CacheRenderers();
            ApplyVisualState();
        }

        void OnEnable()
        {
            SonarPulseImpactEffect.PulseSpawned += OnPulseSpawned;
            StickyPulseImpactEffect.PulseSpawned += OnPulseSpawned;
            ApplyVisualState();
        }

        void OnDisable()
        {
            SonarPulseImpactEffect.PulseSpawned -= OnPulseSpawned;
            StickyPulseImpactEffect.PulseSpawned -= OnPulseSpawned;
        }

        void Update()
        {
            ApplyVisualState();
        }

        public void RefreshTargets()
        {
            m_TargetRenderers = null;
            m_GlowLights = null;
            CacheRenderers();
            ApplyVisualState();
        }

        void CacheRenderers()
        {
            m_RendererStates.Clear();

            if (m_TargetRenderers == null || m_TargetRenderers.Length == 0)
                m_TargetRenderers = GetComponentsInChildren<Renderer>(true);

            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            if (m_GlowLights == null || m_GlowLights.Length == 0)
                m_GlowLights = GetComponentsInChildren<Light>(true);

            for (int i = 0; i < m_TargetRenderers.Length; i++)
            {
                Renderer targetRenderer = m_TargetRenderers[i];
                if (targetRenderer == null || !TryResolveRole(targetRenderer, out LightRole role))
                    continue;

                Material material = targetRenderer.sharedMaterial;
                m_RendererStates.Add(new RendererState
                {
                    Renderer = targetRenderer,
                    Role = role,
                    BaseColor = ResolveMaterialColor(material),
                    Phase = i * 0.83f,
                });
            }
        }

        void OnPulseSpawned(Vector3 origin, float radius, Collider sourceCollider)
        {
            float reach = Mathf.Max(0f, radius) + Mathf.Max(0f, m_PulseReachMargin);
            if ((transform.position - origin).sqrMagnitude > reach * reach)
                return;

            m_PulseFlashUntil = Mathf.Max(m_PulseFlashUntil, Time.time + m_PulseFlashDuration);
            ApplyVisualState();
        }

        void ApplyVisualState()
        {
            if (m_RendererStates.Count == 0)
                return;

            float breathWave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * m_BreathSpeed + m_PhaseOffset);
            float breath = Mathf.SmoothStep(0f, 1f, breathWave);
            float flash = ResolveFlash01();

            for (int i = 0; i < m_RendererStates.Count; i++)
            {
                RendererState state = m_RendererStates[i];
                if (state.Renderer == null)
                    continue;

                Color glowColor = ResolveGlowColor(state.Role);
                float strength = ResolveEmissionStrength(state, breath, flash);
                Color baseColor = Color.Lerp(state.BaseColor, glowColor, ResolveBaseColorBlend(state.Role, breath, flash));
                Color emissionColor = glowColor * strength;
                baseColor.a = state.BaseColor.a;
                emissionColor.a = 1f;

                m_PropertyBlock.Clear();
                state.Renderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(BaseColorPropertyId, baseColor);
                m_PropertyBlock.SetColor(ColorPropertyId, baseColor);
                m_PropertyBlock.SetColor(EmissionColorPropertyId, emissionColor);
                state.Renderer.SetPropertyBlock(m_PropertyBlock);
            }

            ApplyLights(breath, flash);
        }

        void ApplyLights(float breath, float flash)
        {
            if (m_GlowLights == null || m_GlowLights.Length == 0)
                return;

            float intensity = Mathf.Lerp(m_MinLightIntensity, m_MaxLightIntensity, breath)
                + flash * (m_PulseFlashStrength * 0.45f);
            for (int i = 0; i < m_GlowLights.Length; i++)
            {
                Light glowLight = m_GlowLights[i];
                if (glowLight == null)
                    continue;

                glowLight.enabled = intensity > 0.01f;
                glowLight.intensity = intensity;
                glowLight.color = Color.Lerp(m_CyanColor, m_CoreColor, 0.45f + flash * 0.3f);
            }
        }

        float ResolveEmissionStrength(RendererState state, float breath, float flash)
        {
            return state.Role switch
            {
                LightRole.Flow => Mathf.Lerp(0.65f, 1.55f, ResolveFlowWave(state.Phase)) + flash * m_PulseFlashStrength,
                LightRole.Scanner => Mathf.Lerp(0.45f, 1.35f, breath) + flash * (m_PulseFlashStrength * 0.9f),
                LightRole.Calibration => 0.22f + breath * 0.18f + flash * (m_PulseFlashStrength * 0.7f),
                LightRole.Amber => Mathf.Lerp(0.18f, 0.55f, breath) + flash * 0.35f,
                _ => 0f,
            };
        }

        float ResolveFlowWave(float phase)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * m_FlowSpeed + phase);
            return Mathf.SmoothStep(0f, 1f, wave);
        }

        float ResolveBaseColorBlend(LightRole role, float breath, float flash)
        {
            return role switch
            {
                LightRole.Flow => Mathf.Clamp01(0.22f + breath * 0.18f + flash * 0.5f),
                LightRole.Scanner => Mathf.Clamp01(0.42f + breath * 0.22f + flash * 0.35f),
                LightRole.Calibration => Mathf.Clamp01(0.08f + flash * 0.45f),
                LightRole.Amber => Mathf.Clamp01(0.24f + breath * 0.12f),
                _ => 0f,
            };
        }

        Color ResolveGlowColor(LightRole role)
        {
            return role switch
            {
                LightRole.Scanner => m_CoreColor,
                LightRole.Amber => m_AmberColor,
                _ => m_CyanColor,
            };
        }

        float ResolveFlash01()
        {
            if (m_PulseFlashDuration <= 0f || Time.time >= m_PulseFlashUntil)
                return 0f;

            float remaining = Mathf.Clamp01((m_PulseFlashUntil - Time.time) / m_PulseFlashDuration);
            return remaining * remaining;
        }

        static bool TryResolveRole(Renderer targetRenderer, out LightRole role)
        {
            role = LightRole.Flow;
            if (targetRenderer == null)
                return false;

            string key = $"{targetRenderer.name} {ResolveMaterialName(targetRenderer)}".ToLowerInvariant();
            if (key.Contains("flowarrow"))
                return false;

            if (key.Contains("flowstrip") || key.Contains("outlinelamp") || key.Contains("contourlamp"))
            {
                role = LightRole.Flow;
                return true;
            }

            if (key.Contains("scannercore") || key.Contains("scanner"))
            {
                role = LightRole.Scanner;
                return true;
            }

            if (key.Contains("calibration"))
            {
                role = LightRole.Calibration;
                return true;
            }

            if (key.Contains("serviceamber") || key.Contains("amber"))
            {
                role = LightRole.Amber;
                return true;
            }

            return false;
        }

        static string ResolveMaterialName(Renderer targetRenderer)
        {
            Material material = targetRenderer.sharedMaterial;
            return material != null ? material.name : string.Empty;
        }

        static Color ResolveMaterialColor(Material material)
        {
            if (material == null)
                return Color.white;

            if (material.HasProperty(BaseColorPropertyId))
                return material.GetColor(BaseColorPropertyId);

            if (material.HasProperty(ColorPropertyId))
                return material.GetColor(ColorPropertyId);

            return Color.white;
        }

        struct RendererState
        {
            public Renderer Renderer;
            public LightRole Role;
            public Color BaseColor;
            public float Phase;
        }
    }
}
