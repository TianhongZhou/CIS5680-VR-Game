using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CIS5680VRGame.Balls
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TeleportBallVisualController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("References")]
        [SerializeField] Transform m_VisualRoot;
        [SerializeField] Transform m_AnchorProjectionRoot;
        [SerializeField] TrailRenderer m_Trail;
        [SerializeField] Light m_CoreLight;
        [SerializeField] ParticleSystem m_CoreParticles;

        [Header("Flight")]
        [SerializeField, Min(0f)] float m_MinTrailSpeed = 0.55f;
        [SerializeField, Min(0f)] float m_StopTrailSpeed = 0.16f;
        [SerializeField, Min(0f)] float m_MaxVisualSpeed = 7.5f;
        [SerializeField, Min(0f)] float m_TrailSlowLinger = 0.18f;

        [Header("Glow")]
        [SerializeField] Color m_PortalColor = new(0.22f, 0.18f, 1f, 1f);
        [SerializeField] Color m_CoreColor = new(0.72f, 0.92f, 1f, 1f);
        [SerializeField, Min(0f)] float m_IdlePulseSpeed = 1.55f;
        [SerializeField, Min(0f)] float m_IdleLightIntensity = 0.2f;
        [SerializeField, Min(0f)] float m_InFlightLightIntensity = 0.78f;
        [SerializeField, Min(0f)] float m_AnchorLightIntensity = 1.05f;
        [SerializeField, Range(0f, 1f)] float m_IdleGlowMin = 0.34f;
        [SerializeField, Range(0f, 2f)] float m_IdleGlowMax = 1.0f;
        [SerializeField, Range(0f, 2f)] float m_InFlightGlow = 0.92f;
        [SerializeField, Range(0f, 2f)] float m_AnchorGlow = 1.24f;

        [Header("Motion")]
        [SerializeField, Min(0f)] float m_IdleRingSpinDegrees = 12f;
        [SerializeField, Min(0f)] float m_FlightRingSpinDegrees = 96f;
        [SerializeField, Min(0f)] float m_AnchorRingSpinDegrees = 54f;

        [Header("Deploy Animation")]
        [SerializeField, Min(0.05f)] float m_FlightShellDiameter = 1.72f;
        [SerializeField, Range(0f, 1f)] float m_FlightShellAlpha = 0.88f;
        [SerializeField, Range(0f, 1f)] float m_FlightClosedMechanismScale = 0.58f;
        [SerializeField, Range(0f, 1f)] float m_FlightClosedHologramScale = 0.03f;
        [SerializeField, Min(0.05f)] float m_DeployPopScale = 1.08f;
        [SerializeField, Min(0f)] float m_ReadyBeaconHeight = 0.28f;
        [SerializeField, Min(0.05f)] float m_ReadyBeaconRadius = 0.24f;

        [Header("Anchor Projection")]
        [SerializeField, Min(0f)] float m_AnchorProjectionRiseOffset = 0.018f;
        [SerializeField, Range(0f, 1f)] float m_AnchorProjectionCenterFollow = 0.9f;
        [SerializeField, Min(0f)] float m_AnchorProjectionMaxCenterCorrection = 0.28f;
        [SerializeField, Min(0f)] float m_AnchorProjectionFadeIn = 0.22f;
        [SerializeField, Range(0f, 1f)] float m_AnchorProjectionMinAlpha = 0.05f;
        [SerializeField, Range(0f, 1f)] float m_AnchorProjectionMaxAlpha = 0.38f;
        [SerializeField, Min(0.05f)] float m_AnchorProjectionInnerRadius = 0.14f;
        [SerializeField, Min(0.05f)] float m_AnchorProjectionMainRadius = 0.34f;
        [SerializeField, Min(0.05f)] float m_AnchorProjectionOuterRadius = 0.5f;
        [SerializeField, Min(0f)] float m_AnchorProjectionLineWidth = 0.009f;
        [SerializeField, Min(0f)] float m_AnchorProjectionPulseScale = 0.018f;

        [Header("Activation Burst")]
        [SerializeField, Min(0f)] float m_ActivationBurstDuration = 0.72f;
        [SerializeField, Min(0f)] float m_ActivationRingRadius = 0.82f;
        [SerializeField, Min(0f)] float m_ActivationLightIntensity = 1.8f;

        [Header("Disappear")]
        [SerializeField, Min(0.05f)] float m_DisappearFadeDuration = 0.42f;
        [SerializeField, Range(0f, 1f)] float m_DisappearEndScale = 0.22f;

        readonly List<RendererState> m_AnimatedRenderers = new();
        readonly List<Transform> m_GyroRings = new();
        readonly List<MorphTargetState> m_MorphTargets = new();
        readonly List<ProjectionLineState> m_AnchorProjectionLines = new();
        readonly List<ProjectionLineState> m_ReadyBeaconLines = new();
        Renderer[] m_DisappearRenderers;
        Material[][] m_DisappearMaterialsByRenderer;
        Color[][] m_DisappearBaseColorsByRenderer;
        Color[][] m_DisappearBaseEmissionsByRenderer;
        MaterialPropertyBlock m_PropertyBlock;
        Material m_AnchorProjectionMaterial;
        Material m_FlightShellMaterial;
        Renderer m_FlightShellRenderer;
        Transform m_FlightShellRoot;
        Transform m_AnchorProjectionPulseRoot;
        Transform m_AnchorProjectionSpinRoot;
        Transform m_ReadyBeaconRoot;
        Transform m_ReadyBeaconPulseRoot;
        Transform m_ReadyBeaconSpinRoot;
        Rigidbody m_Rigidbody;
        XRGrabInteractable m_GrabInteractable;
        Vector3 m_VisualRootBaseLocalPosition;
        Vector3 m_DisappearStartScale;
        Vector3 m_AnchorPoint;
        Vector3 m_AnchorNormal = Vector3.up;
        float m_TrailSlowExpiresAt;
        float m_AnchorStartedAt;
        float m_AnchorReadyAt;
        float m_AnchorExpireAt;
        float m_DisappearStartedAt;
        float m_DisappearDuration;
        bool m_TrailActive;
        bool m_AnchorArming;
        bool m_AnchorReady;
        bool m_Disappearing;

        struct RendererState
        {
            public Renderer Renderer;
            public Color BaseColor;
            public float GlowWeight;
            public bool IsCore;
        }

        struct ProjectionLineState
        {
            public LineRenderer Line;
            public float AlphaScale;
            public float BaseWidth;
        }

        struct MorphTargetState
        {
            public Transform Transform;
            public Vector3 BaseScale;
            public float ClosedScale;
        }

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
            m_PropertyBlock = new MaterialPropertyBlock();

            ResolveReferences();
            ConfigureFlightShell();
            ConfigureAnchorProjectionGraphics();
            ConfigureReadyBeaconGraphics();
            CacheAnimatedRenderers();
            CacheMotionTargets();
            SetTrailActive(false, true);
            ConfigureParticleEmission(0f);
            SetAnchorProjectionVisible(false);
            SetReadyBeaconVisible(false);

            if (m_VisualRoot != null)
                m_VisualRootBaseLocalPosition = m_VisualRoot.localPosition;
        }

        void OnEnable()
        {
            ThrowableBall.ImpactOccurred += OnBallImpactOccurred;
            m_AnchorArming = false;
            m_AnchorReady = false;
            SetTrailActive(false, true);
            SetAnchorProjectionVisible(false);
            SetReadyBeaconVisible(false);
        }

        void OnDisable()
        {
            ThrowableBall.ImpactOccurred -= OnBallImpactOccurred;
            SetTrailActive(false, true);
            SetAnchorProjectionVisible(false);
            SetReadyBeaconVisible(false);

            if (m_VisualRoot != null)
                m_VisualRoot.localPosition = m_VisualRootBaseLocalPosition;
        }

        void OnDestroy()
        {
            if (m_AnchorProjectionMaterial != null)
                Destroy(m_AnchorProjectionMaterial);

            if (m_FlightShellMaterial != null)
                Destroy(m_FlightShellMaterial);
        }

        void Update()
        {
            float speed = ResolveSpeed();
            bool held = m_GrabInteractable != null && m_GrabInteractable.isSelected;

            UpdateTrail(speed, held);
            UpdateGlow(speed, held);
            UpdateRings(speed, held);
            UpdateDeployState();
            UpdateAnchorProjection();
            UpdateParticles(speed, held);
            UpdateDisappearFade();
        }

        public void EnterAnchorArming(Vector3 hitPoint, Vector3 hitNormal, float readyAtTime, float readyUntilTime)
        {
            m_AnchorArming = true;
            m_AnchorReady = false;
            m_AnchorNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
            m_AnchorPoint = hitPoint;
            m_AnchorStartedAt = Time.time;
            m_AnchorReadyAt = Mathf.Max(Time.time, readyAtTime);
            m_AnchorExpireAt = readyUntilTime;

            SetTrailActive(false, false);
            PositionAnchorProjection();
            SetAnchorProjectionVisible(true);
            SetReadyBeaconVisible(false);

            if (m_CoreParticles != null)
                m_CoreParticles.Emit(12);
        }

        public Vector3 ResolveAlignedAnchorPoint(Vector3 hitPoint, Vector3 hitNormal)
        {
            Vector3 normal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
            Vector3 visualCenter = ResolveVisualCenter();
            Vector3 projectedCenter = visualCenter - normal * Vector3.Dot(visualCenter - hitPoint, normal);
            Vector3 correction = projectedCenter - hitPoint;
            float maxCorrection = Mathf.Max(0f, m_AnchorProjectionMaxCenterCorrection);
            if (maxCorrection > 0f && correction.sqrMagnitude > maxCorrection * maxCorrection)
                correction = correction.normalized * maxCorrection;

            return Vector3.Lerp(hitPoint, hitPoint + correction, Mathf.Clamp01(m_AnchorProjectionCenterFollow));
        }

        public void CompleteAnchorReady()
        {
            if (!m_AnchorArming && m_AnchorReady)
                return;

            m_AnchorArming = false;
            m_AnchorReady = true;
            m_AnchorStartedAt = Time.time;

            PositionAnchorProjection();
            PositionReadyBeacon();
            SetAnchorProjectionVisible(true);
            SetReadyBeaconVisible(true);

            if (m_CoreParticles != null)
                m_CoreParticles.Emit(24);
        }

        public void EnterAnchorReady(Vector3 hitPoint, Vector3 hitNormal, float readyUntilTime)
        {
            EnterAnchorArming(hitPoint, hitNormal, Time.time, readyUntilTime);
            CompleteAnchorReady();
        }

        public void ExitAnchorReady()
        {
            m_AnchorArming = false;
            m_AnchorReady = false;
            SetAnchorProjectionVisible(false);
            SetReadyBeaconVisible(false);

            if (m_VisualRoot != null)
                m_VisualRoot.localPosition = m_VisualRootBaseLocalPosition;
        }

        public void PlayTeleportActivationBurst(Vector3 hitPoint, Vector3 hitNormal)
        {
            Vector3 normal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
            GameObject burst = new("TeleportActivationVisualBurst");
            burst.transform.position = hitPoint + normal * m_AnchorProjectionRiseOffset;
            burst.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);

            TeleportActivationBurstVisual burstVisual = burst.AddComponent<TeleportActivationBurstVisual>();
            burstVisual.Configure(
                m_PortalColor,
                m_CoreColor,
                Mathf.Max(0.05f, m_ActivationBurstDuration),
                Mathf.Max(0.1f, m_ActivationRingRadius),
                Mathf.Max(0f, m_ActivationLightIntensity));
        }

        public void BeginDisappear(bool playActivationBurst, Vector3 effectPoint, Vector3 effectNormal)
        {
            if (m_Disappearing)
                return;

            m_Disappearing = true;
            m_DisappearStartedAt = Time.time;
            m_DisappearDuration = Mathf.Max(0.05f, m_DisappearFadeDuration);
            m_DisappearStartScale = transform.localScale;

            if (playActivationBurst)
                PlayTeleportActivationBurst(effectPoint, effectNormal);

            StopPhysicsAndInteraction();
            PrepareDisappearMaterials();
            SetTrailActive(false, false);

            if (m_CoreParticles != null)
                m_CoreParticles.Emit(18);

            Destroy(gameObject, m_DisappearDuration + 0.04f);
        }

        void OnBallImpactOccurred(BallType ballType, Vector3 hitPoint, Vector3 hitNormal, GameObject ballObject)
        {
            if (ballObject != gameObject)
                return;

            SetTrailActive(false, false);
        }

        void ResolveReferences()
        {
            if (m_VisualRoot == null)
                m_VisualRoot = transform.Find("VisualRoot");

            if (m_AnchorProjectionRoot == null)
                m_AnchorProjectionRoot = transform.Find("AnchorProjectionRoot");

            if (m_Trail == null && m_VisualRoot != null)
                m_Trail = m_VisualRoot.GetComponent<TrailRenderer>();

            if (m_CoreLight == null && m_VisualRoot != null)
                m_CoreLight = m_VisualRoot.GetComponentInChildren<Light>(true);

            if (m_CoreParticles == null && m_VisualRoot != null)
                m_CoreParticles = m_VisualRoot.GetComponentInChildren<ParticleSystem>(true);
        }

        void CacheAnimatedRenderers()
        {
            m_AnimatedRenderers.Clear();

            if (m_VisualRoot == null)
                return;

            Renderer[] renderers = m_VisualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null || targetRenderer is TrailRenderer)
                    continue;

                Material material = targetRenderer.sharedMaterial;
                if (material == null)
                    continue;

                string key = $"{targetRenderer.name} {material.name}".ToLowerInvariant();
                bool isCore = key.Contains("singularity") || key.Contains("portalglass") || key.Contains("core_");
                bool isPortalEnergy = isCore
                    || key.Contains("indigo")
                    || key.Contains("violet")
                    || key.Contains("hologram")
                    || key.Contains("lightstrip")
                    || key.Contains("tick")
                    || key.Contains("crosshair");

                if (!isPortalEnergy)
                    continue;

                m_AnimatedRenderers.Add(new RendererState
                {
                    Renderer = targetRenderer,
                    BaseColor = ResolveMaterialColor(material),
                    GlowWeight = isCore ? 1.38f : key.Contains("hologram") ? 0.62f : 1f,
                    IsCore = isCore
                });
            }
        }

        void CacheMotionTargets()
        {
            m_GyroRings.Clear();
            m_MorphTargets.Clear();

            if (m_VisualRoot != null)
            {
                Transform[] transforms = m_VisualRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform target = transforms[i];
                    if (target == null || target == m_VisualRoot || target == m_FlightShellRoot)
                        continue;

                    string name = target.name.ToLowerInvariant();
                    if (name.Contains("gyroring") || name.Contains("cbracket") || name.Contains("hologram_orbitarc"))
                        m_GyroRings.Add(target);

                    if (target.GetComponent<Renderer>() == null)
                        continue;

                    float closedScale = ResolveClosedScale(name);
                    if (closedScale >= 0.99f)
                        continue;

                    m_MorphTargets.Add(new MorphTargetState
                    {
                        Transform = target,
                        BaseScale = target.localScale,
                        ClosedScale = closedScale,
                    });
                }
            }
        }

        float ResolveClosedScale(string lowerName)
        {
            if (lowerName.Contains("hologram_orbitarc"))
                return Mathf.Clamp01(m_FlightClosedHologramScale);

            if (lowerName.Contains("core_portaltick") || lowerName.Contains("crosshair"))
                return 0.22f;

            if (lowerName.Contains("core_"))
                return 0.76f;

            if (lowerName.Contains("emitter_"))
                return 0.42f;

            if (lowerName.Contains("gyroring") || lowerName.Contains("cbracket"))
                return Mathf.Clamp01(m_FlightClosedMechanismScale);

            return 1f;
        }

        void UpdateTrail(float speed, bool held)
        {
            if (held || m_AnchorArming || m_AnchorReady)
            {
                SetTrailActive(false, held);
                return;
            }

            if (!m_TrailActive && speed >= m_MinTrailSpeed)
                SetTrailActive(true, true);

            if (!m_TrailActive)
                return;

            if (speed <= m_StopTrailSpeed)
            {
                if (m_TrailSlowExpiresAt <= 0f)
                    m_TrailSlowExpiresAt = Time.time + m_TrailSlowLinger;

                if (Time.time >= m_TrailSlowExpiresAt)
                    SetTrailActive(false, false);
            }
            else
            {
                m_TrailSlowExpiresAt = 0f;
            }
        }

        void UpdateGlow(float speed, bool held)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * m_IdlePulseSpeed * Mathf.PI * 2f);
            float breath = Mathf.SmoothStep(0f, 1f, wave);
            float speedFactor = Mathf.InverseLerp(m_MinTrailSpeed, m_MaxVisualSpeed, speed);
            float stateGlow = m_AnchorReady
                ? m_AnchorGlow * Mathf.Lerp(0.78f, 1.08f, breath)
                : m_AnchorArming
                    ? Mathf.Lerp(m_InFlightGlow, m_AnchorGlow * 0.92f, ResolveDeployProgress()) * Mathf.Lerp(0.88f, 1.12f, breath)
                : !held && speed >= m_MinTrailSpeed
                    ? m_InFlightGlow + speedFactor * 0.18f
                    : Mathf.Lerp(m_IdleGlowMin, m_IdleGlowMax, breath);
            float disappearAlpha = ResolveDisappearAlpha();

            for (int i = 0; i < m_AnimatedRenderers.Count; i++)
            {
                RendererState state = m_AnimatedRenderers[i];
                if (state.Renderer == null)
                    continue;

                float blend = Mathf.Clamp01((state.IsCore ? 0.55f : 0.34f) + stateGlow * 0.32f);
                Color target = state.IsCore ? m_CoreColor : m_PortalColor;
                Color color = Color.Lerp(state.BaseColor, target, blend);
                color *= Mathf.Max(0.05f, stateGlow * state.GlowWeight);
                color.a *= disappearAlpha;

                m_PropertyBlock.Clear();
                state.Renderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(BaseColorId, color);
                m_PropertyBlock.SetColor(ColorId, color);
                m_PropertyBlock.SetColor(EmissionColorId, color * disappearAlpha);
                state.Renderer.SetPropertyBlock(m_PropertyBlock);
            }

            if (m_CoreLight != null)
            {
                float light = m_AnchorReady
                    ? m_AnchorLightIntensity * Mathf.Lerp(0.72f, 1.16f, breath)
                    : m_AnchorArming
                        ? Mathf.Lerp(m_InFlightLightIntensity, m_AnchorLightIntensity * 0.86f, ResolveDeployProgress())
                        : speed >= m_MinTrailSpeed
                            ? m_InFlightLightIntensity * (0.85f + speedFactor * 0.25f)
                        : m_IdleLightIntensity * Mathf.Lerp(0.7f, 1.25f, breath);
                light *= disappearAlpha;
                m_CoreLight.enabled = light > 0.01f;
                m_CoreLight.intensity = light;
                m_CoreLight.color = Color.Lerp(m_PortalColor, m_CoreColor, 0.5f);
            }
        }

        void UpdateRings(float speed, bool held)
        {
            float spin = m_AnchorReady || m_AnchorArming
                ? m_AnchorRingSpinDegrees
                : !held && speed >= m_MinTrailSpeed
                    ? m_FlightRingSpinDegrees
                    : m_IdleRingSpinDegrees;

            float delta = spin * Time.deltaTime;
            for (int i = 0; i < m_GyroRings.Count; i++)
            {
                Transform target = m_GyroRings[i];
                if (target == null)
                    continue;

                Vector3 axis = i % 3 == 0 ? Vector3.up : i % 3 == 1 ? Vector3.right : Vector3.forward;
                target.Rotate(axis, delta * (i % 2 == 0 ? 1f : -0.68f), Space.Self);
            }
        }

        void UpdateDeployState()
        {
            float deploy = ResolveDeployProgress();
            float smoothDeploy = Mathf.SmoothStep(0f, 1f, deploy);
            float pop = Mathf.Sin(smoothDeploy * Mathf.PI) * Mathf.Max(0f, m_DeployPopScale - 1f);
            float openScale = m_AnchorArming ? 1f + pop : 1f;

            for (int i = 0; i < m_MorphTargets.Count; i++)
            {
                MorphTargetState state = m_MorphTargets[i];
                if (state.Transform == null)
                    continue;

                float scaleFactor = Mathf.Lerp(state.ClosedScale, openScale, smoothDeploy);
                state.Transform.localScale = state.BaseScale * Mathf.Max(0.001f, scaleFactor);
            }

            SetFlightShellAlpha(Mathf.Lerp(m_FlightShellAlpha, 0f, smoothDeploy));
        }

        void UpdateAnchorProjection()
        {
            if ((!m_AnchorArming && !m_AnchorReady) || m_AnchorProjectionRoot == null)
                return;

            PositionAnchorProjection();

            float age = Time.time - m_AnchorStartedAt;
            float fade = m_AnchorProjectionFadeIn <= 0f ? 1f : Mathf.Clamp01(age / m_AnchorProjectionFadeIn);
            float deploy = ResolveDeployProgress();
            float lifetimePulse = m_AnchorExpireAt > Time.time && m_AnchorExpireAt - Time.time < 1.2f
                ? 0.5f + 0.5f * Mathf.Sin(Time.time * 18f)
                : 0.5f + 0.5f * Mathf.Sin(Time.time * 5.5f);
            float alpha = Mathf.Lerp(m_AnchorProjectionMinAlpha, m_AnchorProjectionMaxAlpha, Mathf.SmoothStep(0f, 1f, lifetimePulse)) * fade;
            alpha *= m_AnchorReady ? 1f : Mathf.Lerp(0.18f, 0.72f, deploy);
            alpha *= ResolveDisappearAlpha();
            Color color = Color.Lerp(m_PortalColor, m_CoreColor, 0.18f + lifetimePulse * 0.12f);
            color.a = alpha;

            ApplyProjectionLineState(m_AnchorProjectionLines, color, lifetimePulse);

            if (m_AnchorProjectionPulseRoot != null)
                m_AnchorProjectionPulseRoot.localScale = Vector3.one * (1f + m_AnchorProjectionPulseScale * Mathf.SmoothStep(0f, 1f, lifetimePulse));

            if (m_AnchorProjectionSpinRoot != null)
                m_AnchorProjectionSpinRoot.Rotate(Vector3.up, m_AnchorRingSpinDegrees * 0.42f * Time.deltaTime, Space.Self);

            UpdateReadyBeacon(lifetimePulse);

            if (m_VisualRoot != null)
                m_VisualRoot.localPosition = m_VisualRootBaseLocalPosition;
        }

        void UpdateReadyBeacon(float lifetimePulse)
        {
            if (!m_AnchorReady || m_ReadyBeaconRoot == null)
                return;

            PositionReadyBeacon();

            float readyAge = Time.time - m_AnchorStartedAt;
            float fade = m_AnchorProjectionFadeIn <= 0f ? 1f : Mathf.Clamp01(readyAge / m_AnchorProjectionFadeIn);
            float alpha = Mathf.Lerp(m_AnchorProjectionMinAlpha * 1.1f, m_AnchorProjectionMaxAlpha * 1.55f, Mathf.SmoothStep(0f, 1f, lifetimePulse));
            alpha *= fade * ResolveDisappearAlpha();
            Color color = Color.Lerp(m_PortalColor, m_CoreColor, 0.42f + lifetimePulse * 0.24f);
            color.a = alpha;

            ApplyProjectionLineState(m_ReadyBeaconLines, color, lifetimePulse);

            if (m_ReadyBeaconPulseRoot != null)
                m_ReadyBeaconPulseRoot.localScale = Vector3.one * (1f + m_AnchorProjectionPulseScale * 1.8f * Mathf.SmoothStep(0f, 1f, lifetimePulse));

            if (m_ReadyBeaconSpinRoot != null)
                m_ReadyBeaconSpinRoot.Rotate(Vector3.up, m_AnchorRingSpinDegrees * 0.68f * Time.deltaTime, Space.Self);
        }

        void ApplyProjectionLineState(List<ProjectionLineState> lineStates, Color color, float pulse)
        {
            for (int i = 0; i < lineStates.Count; i++)
            {
                ProjectionLineState state = lineStates[i];
                if (state.Line == null)
                    continue;

                Color lineColor = color;
                lineColor.a *= state.AlphaScale;
                state.Line.startColor = lineColor;
                state.Line.endColor = lineColor;
                state.Line.widthMultiplier = state.BaseWidth * Mathf.Lerp(0.86f, 1.12f, pulse);
            }
        }

        void PositionAnchorProjection()
        {
            if (m_AnchorProjectionRoot == null)
                return;

            m_AnchorProjectionRoot.position = m_AnchorPoint + m_AnchorNormal * m_AnchorProjectionRiseOffset;
            m_AnchorProjectionRoot.rotation = Quaternion.FromToRotation(Vector3.up, m_AnchorNormal);
            m_AnchorProjectionRoot.localScale = ResolveParentScaleCompensation(m_AnchorProjectionRoot);
        }

        Vector3 ResolveVisualCenter()
        {
            if (m_VisualRoot == null)
                return transform.position;

            Renderer[] renderers = m_VisualRoot.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null
                    || !targetRenderer.enabled
                    || targetRenderer is TrailRenderer
                    || targetRenderer is ParticleSystemRenderer)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = targetRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(targetRenderer.bounds);
                }
            }

            return hasBounds ? bounds.center : transform.position;
        }

        void PositionReadyBeacon()
        {
            if (m_ReadyBeaconRoot == null)
                return;

            m_ReadyBeaconRoot.localPosition = Vector3.up * Mathf.Max(0.02f, m_ReadyBeaconHeight);
            m_ReadyBeaconRoot.localRotation = Quaternion.identity;
            m_ReadyBeaconRoot.localScale = Vector3.one;
        }

        void UpdateParticles(float speed, bool held)
        {
            if (m_CoreParticles == null)
                return;

            float speedFactor = Mathf.InverseLerp(m_MinTrailSpeed, m_MaxVisualSpeed, speed);
            float rate = m_AnchorReady
                ? 8.5f
                : m_AnchorArming
                    ? Mathf.Lerp(3.5f, 11.5f, ResolveDeployProgress())
                    : !held && speed >= m_MinTrailSpeed
                        ? Mathf.Lerp(2.5f, 9.5f, speedFactor)
                        : 0.18f;
            rate *= ResolveDisappearAlpha();
            ConfigureParticleEmission(rate);
        }

        void UpdateDisappearFade()
        {
            if (!m_Disappearing)
                return;

            float t = Mathf.Clamp01((Time.time - m_DisappearStartedAt) / Mathf.Max(0.05f, m_DisappearDuration));
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            ApplyDisappearMaterials(alpha);
            transform.localScale = Vector3.Lerp(
                m_DisappearStartScale,
                m_DisappearStartScale * Mathf.Max(0f, m_DisappearEndScale),
                Mathf.SmoothStep(0f, 1f, t));
        }

        void ConfigureParticleEmission(float rate)
        {
            if (m_CoreParticles == null)
                return;

            var emission = m_CoreParticles.emission;
            emission.rateOverTime = rate;
        }

        void SetTrailActive(bool active, bool clear)
        {
            m_TrailActive = active;
            if (m_Trail == null)
                return;

            m_Trail.emitting = active;
            if (clear)
                m_Trail.Clear();
        }

        void SetAnchorProjectionVisible(bool visible)
        {
            if (m_AnchorProjectionRoot != null)
                m_AnchorProjectionRoot.gameObject.SetActive(visible);

            for (int i = 0; i < m_AnchorProjectionLines.Count; i++)
            {
                if (m_AnchorProjectionLines[i].Line != null)
                    m_AnchorProjectionLines[i].Line.enabled = visible;
            }
        }

        void SetReadyBeaconVisible(bool visible)
        {
            if (m_ReadyBeaconRoot != null)
                m_ReadyBeaconRoot.gameObject.SetActive(visible);

            for (int i = 0; i < m_ReadyBeaconLines.Count; i++)
            {
                if (m_ReadyBeaconLines[i].Line != null)
                    m_ReadyBeaconLines[i].Line.enabled = visible;
            }
        }

        void SetFlightShellAlpha(float alpha)
        {
            if (m_FlightShellRenderer == null)
                return;

            float clampedAlpha = Mathf.Clamp01(alpha) * ResolveDisappearAlpha();
            m_FlightShellRenderer.enabled = clampedAlpha > 0.01f;
            if (m_FlightShellMaterial == null)
                return;

            Color shellColor = Color.Lerp(new Color(0.018f, 0.02f, 0.025f, 1f), m_PortalColor, 0.35f);
            shellColor.a = clampedAlpha;
            SetMaterialColor(m_FlightShellMaterial, shellColor);

            if (m_FlightShellMaterial.HasProperty(EmissionColorId))
                m_FlightShellMaterial.SetColor(EmissionColorId, m_PortalColor * (0.35f + clampedAlpha * 0.75f));
        }

        float ResolveDeployProgress()
        {
            if (m_AnchorReady)
                return 1f;

            if (!m_AnchorArming)
                return 0f;

            float duration = Mathf.Max(0.05f, m_AnchorReadyAt - m_AnchorStartedAt);
            return Mathf.Clamp01((Time.time - m_AnchorStartedAt) / duration);
        }

        void ConfigureFlightShell()
        {
            if (m_VisualRoot == null || m_FlightShellRenderer != null)
                return;

            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = "RuntimeClosedFlightShell";
            shell.layer = gameObject.layer;
            shell.transform.SetParent(m_VisualRoot, false);
            shell.transform.SetAsFirstSibling();
            shell.transform.localPosition = Vector3.zero;
            shell.transform.localRotation = Quaternion.identity;
            shell.transform.localScale = Vector3.one * Mathf.Max(0.05f, m_FlightShellDiameter);

            Collider shellCollider = shell.GetComponent<Collider>();
            if (shellCollider != null)
            {
                if (Application.isPlaying)
                    Destroy(shellCollider);
                else
                    DestroyImmediate(shellCollider);
            }

            m_FlightShellRoot = shell.transform;
            m_FlightShellRenderer = shell.GetComponent<Renderer>();
            if (m_FlightShellRenderer != null)
            {
                m_FlightShellMaterial = new Material(ResolveUnlitShader())
                {
                    name = "Runtime_TeleportClosedShell",
                    hideFlags = HideFlags.HideAndDontSave,
                    color = m_PortalColor,
                };
                ConfigureTransparentMaterial(m_FlightShellMaterial);
                if (m_FlightShellMaterial.HasProperty(EmissionColorId))
                    m_FlightShellMaterial.SetColor(EmissionColorId, m_PortalColor * 0.45f);
                m_FlightShellRenderer.sharedMaterial = m_FlightShellMaterial;
                m_FlightShellRenderer.shadowCastingMode = ShadowCastingMode.Off;
                m_FlightShellRenderer.receiveShadows = false;
            }

            SetFlightShellAlpha(m_FlightShellAlpha);
        }

        void ConfigureReadyBeaconGraphics()
        {
            if (m_AnchorProjectionRoot == null || m_ReadyBeaconRoot != null)
                return;

            m_ReadyBeaconRoot = CreateProjectionRoot("ReadyTeleportBeaconRoot", m_AnchorProjectionRoot);
            m_ReadyBeaconPulseRoot = CreateProjectionRoot("ReadyTeleportBeacon_PulseRoot", m_ReadyBeaconRoot);
            m_ReadyBeaconSpinRoot = CreateProjectionRoot("ReadyTeleportBeacon_SpinRoot", m_ReadyBeaconRoot);
            m_ReadyBeaconRoot.localPosition = Vector3.up * Mathf.Max(0.02f, m_ReadyBeaconHeight);

            CreateProjectionRing("ReadyBeacon_OuterPortalRing", m_ReadyBeaconPulseRoot, m_ReadyBeaconRadius, 96, 0.9f, 0.95f, m_ReadyBeaconLines);
            CreateProjectionRing("ReadyBeacon_InnerPortalRing", m_ReadyBeaconPulseRoot, m_ReadyBeaconRadius * 0.55f, 72, 0.55f, 0.72f, m_ReadyBeaconLines);

            for (int i = 0; i < 6; i++)
            {
                float startAngle = i * 60f + 8f;
                CreateProjectionArc(
                    $"ReadyBeacon_SpinSegment_{i:00}",
                    m_ReadyBeaconSpinRoot,
                    m_ReadyBeaconRadius * 1.12f,
                    startAngle,
                    startAngle + 22f,
                    8,
                    0.72f,
                    0.82f,
                    m_ReadyBeaconLines);
            }

            for (int i = 0; i < 4; i++)
            {
                float angle = Mathf.PI * 0.5f * i;
                Vector3 offset = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                LineRenderer line = CreateProjectionLine(
                    $"ReadyBeacon_VerticalTrace_{i:00}",
                    m_ReadyBeaconRoot,
                    0.42f,
                    0.48f,
                    m_ReadyBeaconLines);
                line.positionCount = 2;
                line.SetPosition(0, offset * (m_ReadyBeaconRadius * 0.38f) - Vector3.up * (m_ReadyBeaconHeight * 0.82f));
                line.SetPosition(1, offset * (m_ReadyBeaconRadius * 0.38f) + Vector3.up * (m_ReadyBeaconHeight * 0.48f));
            }

            SetReadyBeaconVisible(false);
        }

        void ConfigureAnchorProjectionGraphics()
        {
            if (m_AnchorProjectionRoot == null)
                return;

            Renderer[] importedRenderers = m_AnchorProjectionRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < importedRenderers.Length; i++)
            {
                Renderer renderer = importedRenderers[i];
                if (renderer != null && renderer is not LineRenderer)
                    renderer.enabled = false;
            }

            if (m_AnchorProjectionLines.Count > 0)
                return;

            m_AnchorProjectionRoot.localScale = ResolveParentScaleCompensation(m_AnchorProjectionRoot);
            m_AnchorProjectionPulseRoot = CreateProjectionRoot("MinimalAnchorProjection_PulseRoot", m_AnchorProjectionRoot);
            m_AnchorProjectionSpinRoot = CreateProjectionRoot("MinimalAnchorProjection_SpinRoot", m_AnchorProjectionRoot);

            CreateProjectionRing("Projection_MainRing", m_AnchorProjectionPulseRoot, m_AnchorProjectionMainRadius, 96, 1f, 1f, m_AnchorProjectionLines);
            CreateProjectionRing("Projection_InnerLockRing", m_AnchorProjectionPulseRoot, m_AnchorProjectionInnerRadius, 72, 0.62f, 0.72f, m_AnchorProjectionLines);
            CreateProjectionRing("Projection_OuterGuideRing", m_AnchorProjectionRoot, m_AnchorProjectionOuterRadius, 128, 0.42f, 0.55f, m_AnchorProjectionLines);

            for (int i = 0; i < 8; i++)
            {
                float startAngle = i * 45f + 5f;
                CreateProjectionArc(
                    $"Projection_Segment_{i:00}",
                    m_AnchorProjectionSpinRoot,
                    m_AnchorProjectionOuterRadius * 0.84f,
                    startAngle,
                    startAngle + 16f,
                    8,
                    0.9f,
                    0.72f,
                    m_AnchorProjectionLines);
            }

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                CreateProjectionRadialLine(
                    $"Projection_AxisMark_{i:00}",
                    m_AnchorProjectionRoot,
                    angle,
                    m_AnchorProjectionInnerRadius * 1.35f,
                    m_AnchorProjectionMainRadius * 0.82f,
                    0.56f,
                    0.6f,
                    m_AnchorProjectionLines);
            }
        }

        static Transform CreateProjectionRoot(string name, Transform parent)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            return root;
        }

        void CreateProjectionRing(string name, Transform parent, float radius, int pointCount, float widthScale, float alphaScale, List<ProjectionLineState> lineStates)
        {
            LineRenderer line = CreateProjectionLine(name, parent, widthScale, alphaScale, lineStates);
            line.loop = true;
            line.positionCount = Mathf.Max(12, pointCount);

            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = Mathf.PI * 2f * i / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        void CreateProjectionArc(string name, Transform parent, float radius, float startDegrees, float endDegrees, int segments, float widthScale, float alphaScale, List<ProjectionLineState> lineStates)
        {
            LineRenderer line = CreateProjectionLine(name, parent, widthScale, alphaScale, lineStates);
            line.loop = false;
            line.positionCount = Mathf.Max(2, segments + 1);

            for (int i = 0; i < line.positionCount; i++)
            {
                float t = line.positionCount <= 1 ? 0f : i / (float)(line.positionCount - 1);
                float angle = Mathf.Deg2Rad * Mathf.Lerp(startDegrees, endDegrees, t);
                line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        void CreateProjectionRadialLine(string name, Transform parent, float degrees, float innerRadius, float outerRadius, float widthScale, float alphaScale, List<ProjectionLineState> lineStates)
        {
            LineRenderer line = CreateProjectionLine(name, parent, widthScale, alphaScale, lineStates);
            float angle = Mathf.Deg2Rad * degrees;
            Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            line.loop = false;
            line.positionCount = 2;
            line.SetPosition(0, direction * innerRadius);
            line.SetPosition(1, direction * outerRadius);
        }

        LineRenderer CreateProjectionLine(string name, Transform parent, float widthScale, float alphaScale, List<ProjectionLineState> lineStates)
        {
            GameObject lineObject = new(name);
            lineObject.transform.SetParent(parent, false);
            lineObject.transform.localPosition = Vector3.zero;
            lineObject.transform.localRotation = Quaternion.identity;
            lineObject.transform.localScale = Vector3.one;

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.widthMultiplier = m_AnchorProjectionLineWidth * widthScale;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.allowOcclusionWhenDynamic = false;
            line.material = ResolveAnchorProjectionMaterial();

            Color transparent = m_PortalColor;
            transparent.a = 0f;
            line.startColor = transparent;
            line.endColor = transparent;
            line.enabled = false;

            lineStates.Add(new ProjectionLineState
            {
                Line = line,
                AlphaScale = Mathf.Max(0f, alphaScale),
                BaseWidth = Mathf.Max(0.001f, m_AnchorProjectionLineWidth * widthScale),
            });
            return line;
        }

        Material ResolveAnchorProjectionMaterial()
        {
            if (m_AnchorProjectionMaterial != null)
                return m_AnchorProjectionMaterial;

            m_AnchorProjectionMaterial = new Material(ResolveUnlitShader())
            {
                name = "Runtime_TeleportAnchorProjection",
                hideFlags = HideFlags.HideAndDontSave,
                color = m_PortalColor,
            };
            ConfigureTransparentMaterial(m_AnchorProjectionMaterial);
            if (m_AnchorProjectionMaterial.HasProperty(EmissionColorId))
                m_AnchorProjectionMaterial.SetColor(EmissionColorId, m_PortalColor * 1.4f);
            return m_AnchorProjectionMaterial;
        }

        void StopPhysicsAndInteraction()
        {
            if (m_Rigidbody != null)
            {
                if (!m_Rigidbody.isKinematic)
                {
#if UNITY_2023_3_OR_NEWER
                    m_Rigidbody.linearVelocity = Vector3.zero;
#else
                    m_Rigidbody.velocity = Vector3.zero;
#endif
                    m_Rigidbody.angularVelocity = Vector3.zero;
                }

                m_Rigidbody.useGravity = false;
                m_Rigidbody.detectCollisions = false;
                m_Rigidbody.isKinematic = true;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }

            if (m_GrabInteractable != null)
                m_GrabInteractable.enabled = false;

            BallHoverInfoDisplay hoverInfoDisplay = GetComponent<BallHoverInfoDisplay>();
            if (hoverInfoDisplay != null)
                hoverInfoDisplay.enabled = false;
        }

        void PrepareDisappearMaterials()
        {
            m_DisappearRenderers = GetComponentsInChildren<Renderer>(true);
            m_DisappearMaterialsByRenderer = new Material[m_DisappearRenderers.Length][];
            m_DisappearBaseColorsByRenderer = new Color[m_DisappearRenderers.Length][];
            m_DisappearBaseEmissionsByRenderer = new Color[m_DisappearRenderers.Length][];

            for (int i = 0; i < m_DisappearRenderers.Length; i++)
            {
                Renderer targetRenderer = m_DisappearRenderers[i];
                if (targetRenderer == null)
                    continue;

                Material[] materials = targetRenderer.materials;
                m_DisappearMaterialsByRenderer[i] = materials;
                m_DisappearBaseColorsByRenderer[i] = new Color[materials.Length];
                m_DisappearBaseEmissionsByRenderer[i] = new Color[materials.Length];

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                        continue;

                    m_DisappearBaseColorsByRenderer[i][j] = ResolveMaterialColor(material);
                    m_DisappearBaseEmissionsByRenderer[i][j] = material.HasProperty(EmissionColorId)
                        ? material.GetColor(EmissionColorId)
                        : Color.clear;
                    ConfigureTransparentMaterial(material);
                }
            }
        }

        void ApplyDisappearMaterials(float alpha)
        {
            if (m_DisappearMaterialsByRenderer == null)
                return;

            for (int i = 0; i < m_DisappearMaterialsByRenderer.Length; i++)
            {
                Material[] materials = m_DisappearMaterialsByRenderer[i];
                if (materials == null)
                    continue;

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                        continue;

                    Color baseColor = m_DisappearBaseColorsByRenderer[i][j];
                    baseColor.a *= alpha;
                    SetMaterialColor(material, baseColor);

                    if (material.HasProperty(EmissionColorId))
                        material.SetColor(EmissionColorId, m_DisappearBaseEmissionsByRenderer[i][j] * alpha);
                }
            }
        }

        float ResolveSpeed()
        {
            if (m_Rigidbody == null)
                return 0f;

#if UNITY_2023_3_OR_NEWER
            return m_Rigidbody.linearVelocity.magnitude;
#else
            return m_Rigidbody.velocity.magnitude;
#endif
        }

        float ResolveDisappearAlpha()
        {
            if (!m_Disappearing)
                return 1f;

            float t = Mathf.Clamp01((Time.time - m_DisappearStartedAt) / Mathf.Max(0.05f, m_DisappearDuration));
            return 1f - Mathf.SmoothStep(0f, 1f, t);
        }

        static Color ResolveMaterialColor(Material material)
        {
            if (material.HasProperty(BaseColorId))
                return material.GetColor(BaseColorId);

            if (material.HasProperty(ColorId))
                return material.GetColor(ColorId);

            return Color.white;
        }

        static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, color);

            if (material.HasProperty(ColorId))
                material.SetColor(ColorId, color);
        }

        static Shader ResolveUnlitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");
            return shader;
        }

        static Vector3 ResolveParentScaleCompensation(Transform target)
        {
            if (target == null || target.parent == null)
                return Vector3.one;

            Vector3 scale = target.parent.lossyScale;
            return new Vector3(SafeInverse(scale.x), SafeInverse(scale.y), SafeInverse(scale.z));
        }

        static float SafeInverse(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
        }

        static void ConfigureTransparentMaterial(Material material)
        {
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);

            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 0f);

            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);

            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        sealed class TeleportActivationBurstVisual : MonoBehaviour
        {
            Color m_PortalColor;
            Color m_CoreColor;
            float m_Duration = 0.7f;
            float m_Radius = 0.8f;
            float m_StartedAt;
            LineRenderer m_Ring;
            Light m_Light;
            ParticleSystem m_Particles;

            public void Configure(Color portalColor, Color coreColor, float duration, float radius, float lightIntensity)
            {
                m_PortalColor = portalColor;
                m_CoreColor = coreColor;
                m_Duration = Mathf.Max(0.05f, duration);
                m_Radius = Mathf.Max(0.1f, radius);
                m_StartedAt = Time.time;

                Material ringMaterial = new(ResolveUnlitShader())
                {
                    color = portalColor
                };
                ringMaterial.SetColor(EmissionColorId, portalColor * 1.8f);

                m_Ring = gameObject.AddComponent<LineRenderer>();
                m_Ring.useWorldSpace = false;
                m_Ring.loop = true;
                m_Ring.positionCount = 96;
                m_Ring.widthMultiplier = 0.018f;
                m_Ring.numCornerVertices = 4;
                m_Ring.numCapVertices = 4;
                m_Ring.material = ringMaterial;
                for (int i = 0; i < m_Ring.positionCount; i++)
                {
                    float angle = Mathf.PI * 2f * i / m_Ring.positionCount;
                    m_Ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
                }

                m_Light = gameObject.AddComponent<Light>();
                m_Light.type = LightType.Point;
                m_Light.color = Color.Lerp(portalColor, coreColor, 0.55f);
                m_Light.range = radius * 2.2f;
                m_Light.intensity = lightIntensity;

                m_Particles = gameObject.AddComponent<ParticleSystem>();
                m_Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = m_Particles.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = duration;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 1.35f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
                main.startColor = new ParticleSystem.MinMaxGradient(portalColor, coreColor);
                main.maxParticles = 96;

                var emission = m_Particles.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });

                var shape = m_Particles.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = radius * 0.42f;

                var renderer = m_Particles.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.material = ringMaterial;

                m_Particles.Play();
                Destroy(gameObject, duration + 0.12f);
            }

            void Update()
            {
                float t = Mathf.Clamp01((Time.time - m_StartedAt) / m_Duration);
                float collapse = 1f - Mathf.SmoothStep(0f, 1f, t);
                float flash = Mathf.Sin(t * Mathf.PI);
                Color color = Color.Lerp(m_PortalColor, m_CoreColor, flash);
                color.a = collapse;

                if (m_Ring != null)
                {
                    m_Ring.startColor = color;
                    m_Ring.endColor = color;
                    m_Ring.widthMultiplier = Mathf.Lerp(0.026f, 0.002f, t);
                    transform.localScale = Vector3.one * Mathf.Lerp(1.18f, 0.18f, Mathf.SmoothStep(0f, 1f, t));
                }

                if (m_Light != null)
                    m_Light.intensity *= Mathf.Lerp(0.72f, 0.1f, t);
            }

            static Shader ResolveUnlitShader()
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                return shader;
            }
        }
    }
}
