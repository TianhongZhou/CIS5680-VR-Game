using System.Collections.Generic;
using CIS5680VRGame.Balls;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class MazeExitGateVisualController : MonoBehaviour
    {
        enum LightRole
        {
            Pillar,
            Plane,
            Chevron,
            Scanner,
            WarmStatus,
            LightDoor,
            GuideLine,
        }

        static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Renderer[] m_TargetRenderers;
        [SerializeField] Transform m_MovingScanLine;
        [SerializeField] Color m_ExitBlue = new(0.18f, 0.42f, 1f, 1f);
        [SerializeField] Color m_ExitCore = new(0.72f, 0.9f, 1f, 1f);
        [SerializeField] Color m_LightDoorCore = new(0.78f, 0.96f, 1f, 1f);
        [SerializeField] Color m_WarmStatus = new(1f, 0.55f, 0.14f, 1f);
        [SerializeField, Min(0f)] float m_IdleBreathSpeed = 0.75f;
        [SerializeField, Min(0f)] float m_ChaseSpeed = 1.25f;
        [SerializeField, Min(0f)] float m_ScanLineAmplitude = 0.42f;
        [SerializeField, Min(0f)] float m_PulseFlashDuration = 0.7f;
        [SerializeField, Min(0f)] float m_PulseFlashStrength = 2.8f;
        [SerializeField, Min(0f)] float m_PulseReachMargin = 1.75f;
        [SerializeField, Min(0f)] float m_CompletedGlowBoost = 1.2f;
        [SerializeField, Min(0f)] float m_ExitActivationFlashDuration = 0.9f;
        [SerializeField, Min(0f)] float m_ExitActivationGlowStrength = 2.6f;

        readonly List<RendererState> m_RendererStates = new();
        MaterialPropertyBlock m_PropertyBlock;
        LevelGoalTrigger m_GoalTrigger;
        Vector3 m_ScanLineBaseLocalPosition;
        float m_PhaseOffset;
        float m_PulseFlashUntil;
        float m_ExitActivationUntil;
        bool m_CompletedOverride;

        void Awake()
        {
            m_GoalTrigger = GetComponent<LevelGoalTrigger>();
            m_PhaseOffset = Random.value * Mathf.PI * 2f;
            CacheRenderers();
            CacheMovingScanLine();
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
            float now = ResolveVisualTime();
            UpdateMovingScanLine(now);
            ApplyVisualState();
        }

        public void RefreshTargets()
        {
            m_TargetRenderers = null;
            m_MovingScanLine = null;
            CacheRenderers();
            CacheMovingScanLine();
            ApplyVisualState();
        }

        void CacheRenderers()
        {
            m_RendererStates.Clear();

            if (m_TargetRenderers == null || m_TargetRenderers.Length == 0)
                m_TargetRenderers = GetComponentsInChildren<Renderer>(true);

            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            for (int i = 0; i < m_TargetRenderers.Length; i++)
            {
                Renderer targetRenderer = m_TargetRenderers[i];
                if (targetRenderer == null || !TryResolveRole(targetRenderer, out LightRole role))
                    continue;

                m_RendererStates.Add(new RendererState
                {
                    Renderer = targetRenderer,
                    Role = role,
                    BaseColor = ResolveMaterialColor(targetRenderer.sharedMaterial),
                    Phase = i * 0.67f,
                });
            }
        }

        void CacheMovingScanLine()
        {
            if (m_MovingScanLine == null)
            {
                Transform[] children = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    Transform child = children[i];
                    if (child != null && child.name.Contains("MovingVerticalScanLine"))
                    {
                        m_MovingScanLine = child;
                        break;
                    }
                }
            }

            if (m_MovingScanLine != null)
                m_ScanLineBaseLocalPosition = m_MovingScanLine.localPosition;
        }

        void OnPulseSpawned(Vector3 origin, float radius, Collider sourceCollider)
        {
            float reach = Mathf.Max(0f, radius) + Mathf.Max(0f, m_PulseReachMargin);
            if ((transform.position - origin).sqrMagnitude > reach * reach)
                return;

            m_PulseFlashUntil = Mathf.Max(m_PulseFlashUntil, ResolveVisualTime() + m_PulseFlashDuration);
            ApplyVisualState();
        }

        public void PlayExitActivation()
        {
            m_ExitActivationUntil = Mathf.Max(m_ExitActivationUntil, ResolveVisualTime() + m_ExitActivationFlashDuration);
            ApplyVisualState();
        }

        public void SetCompletedState(bool completed)
        {
            m_CompletedOverride = completed;
            ApplyVisualState();
        }

        void UpdateMovingScanLine(float now)
        {
            if (m_MovingScanLine == null || m_ScanLineAmplitude <= 0f)
                return;

            Vector3 position = m_ScanLineBaseLocalPosition;
            position.x += Mathf.Sin(now * Mathf.PI * 2f * Mathf.Max(0.01f, m_ChaseSpeed) + m_PhaseOffset) * m_ScanLineAmplitude;
            m_MovingScanLine.localPosition = position;
        }

        void ApplyVisualState()
        {
            if (m_RendererStates.Count == 0)
                return;

            float now = ResolveVisualTime();
            float breathWave = 0.5f + 0.5f * Mathf.Sin(now * Mathf.PI * 2f * m_IdleBreathSpeed + m_PhaseOffset);
            float breath = Mathf.SmoothStep(0f, 1f, breathWave);
            float flash = ResolveFlash01(now);
            float activation = ResolveExitActivation01(now);
            float completed = (m_CompletedOverride || (m_GoalTrigger != null && m_GoalTrigger.HasCompleted))
                && m_CompletedGlowBoost > 0f ? 1f : 0f;

            for (int i = 0; i < m_RendererStates.Count; i++)
            {
                RendererState state = m_RendererStates[i];
                if (state.Renderer == null)
                    continue;

                Color glow = ResolveGlowColor(state.Role, completed);
                float strength = ResolveEmissionStrength(state, breath, flash, completed, activation);
                float blend = ResolveBaseColorBlend(state.Role, breath, flash, completed, activation);
                Color baseColor = Color.Lerp(state.BaseColor, glow, blend);
                Color emissionColor = glow * strength;
                baseColor.a = state.BaseColor.a;
                emissionColor.a = 1f;

                m_PropertyBlock.Clear();
                state.Renderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(BaseColorPropertyId, baseColor);
                m_PropertyBlock.SetColor(ColorPropertyId, baseColor);
                m_PropertyBlock.SetColor(EmissionColorPropertyId, emissionColor);
                state.Renderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        float ResolveEmissionStrength(RendererState state, float breath, float flash, float completed, float activation)
        {
            float chase = ResolveChaseWave(state.Phase, ResolveVisualTime());
            float completeBoost = completed * Mathf.Max(0f, m_CompletedGlowBoost);
            float activationBoost = activation * Mathf.Max(0f, m_ExitActivationGlowStrength);
            return state.Role switch
            {
                LightRole.Pillar => Mathf.Lerp(0.55f, 1.45f, chase) + flash * m_PulseFlashStrength + completeBoost,
                LightRole.Plane => Mathf.Lerp(0.18f, 0.65f, breath) + flash * (m_PulseFlashStrength * 0.8f) + completeBoost * 0.8f + activationBoost * 0.55f,
                LightRole.Chevron => Mathf.Lerp(0.45f, 1.35f, chase) + flash * (m_PulseFlashStrength * 0.7f) + completeBoost * 0.7f,
                LightRole.Scanner => Mathf.Lerp(0.35f, 1.1f, breath) + flash * (m_PulseFlashStrength * 0.65f) + completeBoost * 0.5f,
                LightRole.WarmStatus => Mathf.Lerp(0.1f, 0.38f, breath) + flash * 0.25f,
                LightRole.LightDoor => Mathf.Lerp(0.72f, 1.45f, breath) + flash * (m_PulseFlashStrength * 0.45f) + activationBoost,
                LightRole.GuideLine => Mathf.Lerp(0.12f, 0.35f, breath) + flash * 0.65f,
                _ => 0f,
            };
        }

        float ResolveChaseWave(float phase, float now)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(now * Mathf.PI * 2f * Mathf.Max(0.01f, m_ChaseSpeed) + phase);
            return Mathf.SmoothStep(0f, 1f, wave);
        }

        float ResolveBaseColorBlend(LightRole role, float breath, float flash, float completed, float activation)
        {
            return role switch
            {
                LightRole.Plane => Mathf.Clamp01(0.18f + breath * 0.1f + flash * 0.32f + completed * 0.25f + activation * 0.16f),
                LightRole.WarmStatus => Mathf.Clamp01(0.16f + breath * 0.08f),
                LightRole.LightDoor => Mathf.Clamp01(0.54f + breath * 0.16f + flash * 0.18f + activation * 0.28f),
                _ => Mathf.Clamp01(0.22f + breath * 0.14f + flash * 0.42f + completed * 0.2f + activation * 0.08f),
            };
        }

        Color ResolveGlowColor(LightRole role, float completed)
        {
            if (role == LightRole.WarmStatus)
                return m_WarmStatus;

            if (role == LightRole.LightDoor)
                return m_LightDoorCore;

            Color cold = role == LightRole.Scanner || role == LightRole.Plane
                ? m_ExitCore
                : m_ExitBlue;
            return Color.Lerp(cold, new Color(0.45f, 1f, 0.72f, 1f), completed * 0.45f);
        }

        float ResolveFlash01(float now)
        {
            if (m_PulseFlashDuration <= 0f || now >= m_PulseFlashUntil)
                return 0f;

            float remaining = Mathf.Clamp01((m_PulseFlashUntil - now) / m_PulseFlashDuration);
            return remaining * remaining;
        }

        float ResolveExitActivation01(float now)
        {
            if (m_ExitActivationFlashDuration <= 0f || now >= m_ExitActivationUntil)
                return 0f;

            float remaining = Mathf.Clamp01((m_ExitActivationUntil - now) / m_ExitActivationFlashDuration);
            return Mathf.SmoothStep(0f, 1f, remaining);
        }

        static float ResolveVisualTime()
        {
            return Application.isPlaying ? Time.unscaledTime : Time.time;
        }

        static bool TryResolveRole(Renderer targetRenderer, out LightRole role)
        {
            role = LightRole.Pillar;
            if (targetRenderer == null)
                return false;

            string key = $"{targetRenderer.name} {ResolveMaterialName(targetRenderer)}".ToLowerInvariant();
            if (key.Contains("locator"))
                return false;

            if (key.Contains("pillar") && key.Contains("coldlight"))
            {
                role = LightRole.Pillar;
                return true;
            }

            if (key.Contains("extractionplane") || key.Contains("energyplane"))
            {
                role = LightRole.Plane;
                return true;
            }

            if (key.Contains("portaldistortion") || key.Contains("portalrift") || key.Contains("spatial"))
            {
                role = LightRole.Plane;
                return true;
            }

            if (key.Contains("lightdoor") || key.Contains("portalveil") || key.Contains("exitmembrane"))
            {
                role = LightRole.LightDoor;
                return true;
            }

            if (key.Contains("basechevron"))
            {
                role = LightRole.Chevron;
                return true;
            }

            if (key.Contains("scanner") || key.Contains("lens"))
            {
                role = LightRole.Scanner;
                return true;
            }

            if (key.Contains("warmgold") || key.Contains("statuslight"))
            {
                role = LightRole.WarmStatus;
                return true;
            }

            if (key.Contains("guideline"))
            {
                role = LightRole.GuideLine;
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
