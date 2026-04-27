using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CIS5680VRGame.Balls
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class SonarProbeBallVisualController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("References")]
        [SerializeField] Transform m_VisualRoot;
        [SerializeField] TrailRenderer m_Trail;
        [SerializeField] Light m_CoreLight;
        [SerializeField] ParticleSystem m_CoreParticles;

        [Header("Throw trail")]
        [SerializeField, Min(0f)] float m_MinThrowSpeed = 0.75f;
        [SerializeField, Min(0f)] float m_StopTrailSpeed = 0.22f;
        [SerializeField, Min(0f)] float m_ThrowArmDuration = 0.45f;
        [SerializeField, Min(0f)] float m_SlowTrailLinger = 0.18f;
        [SerializeField, Min(0f)] float m_MaxVisualSpeed = 5f;

        [Header("Glow")]
        [SerializeField] Color m_CyanColor = new(0f, 0.86f, 1f, 1f);
        [SerializeField] Color m_CoreColor = new(0.72f, 1f, 1f, 1f);
        [SerializeField, Min(0f)] float m_IdlePulseSpeed = 2.2f;
        [SerializeField, Min(0f)] float m_IdleLightIntensity = 0.18f;
        [SerializeField, Min(0f)] float m_ThrownLightIntensity = 0.92f;
        [SerializeField, Range(0f, 1f)] float m_StandbyBreathMin = 0.42f;
        [SerializeField, Range(0f, 2f)] float m_StandbyBreathMax = 1.18f;
        [SerializeField, Range(0f, 2f)] float m_InFlightSteadyGlow = 1.08f;
        [SerializeField, Min(0f)] float m_ReleaseFlashDuration = 0.22f;
        [SerializeField, Min(0f)] float m_ImpactFlashDuration = 0.18f;
        [SerializeField, Min(0f)] float m_FlashLightBoost = 1.6f;

        [Header("Motion")]
        [SerializeField, Min(0f)] float m_IdleSpinDegreesPerSecond = 0f;
        [SerializeField, Min(0f)] float m_ThrownSpinDegreesPerSecond = 42f;

        readonly List<RendererState> m_RendererStates = new();
        readonly List<Transform> m_SpinTransforms = new();
        MaterialPropertyBlock m_PropertyBlock;
        XRGrabInteractable m_GrabInteractable;
        Rigidbody m_Rigidbody;
        float m_TrailArmExpiresAt;
        float m_TrailSlowExpiresAt;
        float m_ReleaseFlashExpiresAt;
        float m_ImpactFlashExpiresAt;
        bool m_TrailArmed;
        bool m_TrailActive;
        bool m_InFlight;

        struct RendererState
        {
            public Renderer Renderer;
            public Color BaseColor;
            public float GlowWeight;
            public bool IsCore;
            public bool IsHologram;
        }

        void Awake()
        {
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
            m_Rigidbody = GetComponent<Rigidbody>();
            m_PropertyBlock = new MaterialPropertyBlock();

            ResolveReferences();
            CacheAnimatedRenderers();
            CacheSpinTransforms();
            SetTrailActive(false, true);
            ConfigureParticleEmission(0f);
        }

        void OnEnable()
        {
            if (m_GrabInteractable != null)
            {
                m_GrabInteractable.selectEntered.AddListener(OnSelectEntered);
                m_GrabInteractable.selectExited.AddListener(OnSelectExited);
            }

            ThrowableBall.ImpactOccurred += OnBallImpactOccurred;
            SetTrailActive(false, true);
            m_InFlight = false;
        }

        void OnDisable()
        {
            if (m_GrabInteractable != null)
            {
                m_GrabInteractable.selectEntered.RemoveListener(OnSelectEntered);
                m_GrabInteractable.selectExited.RemoveListener(OnSelectExited);
            }

            ThrowableBall.ImpactOccurred -= OnBallImpactOccurred;
            SetTrailActive(false, true);
            m_InFlight = false;
        }

        void Update()
        {
            float speed = ResolveSpeed();
            bool heldOrSocketed = IsHeldOrSocketed();

            if (!heldOrSocketed && !m_InFlight && speed >= m_MinThrowSpeed)
                m_InFlight = true;

            UpdateTrailState(speed, heldOrSocketed);
            UpdateGlow(speed, heldOrSocketed);
            UpdateSpin(speed, heldOrSocketed);
            UpdateParticles(speed, heldOrSocketed);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            m_InFlight = false;
            m_TrailArmed = false;
            SetTrailActive(false, true);
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor)
            {
                m_InFlight = false;
                m_TrailArmed = false;
                SetTrailActive(false, true);
                return;
            }

            if (m_GrabInteractable != null && m_GrabInteractable.isSelected)
                return;

            m_InFlight = false;
            m_TrailArmed = true;
            m_TrailArmExpiresAt = Time.time + m_ThrowArmDuration;
            m_TrailSlowExpiresAt = 0f;
            m_ReleaseFlashExpiresAt = Time.time + m_ReleaseFlashDuration;
            EmitReleaseBurst();
        }

        void OnBallImpactOccurred(BallType ballType, Vector3 hitPoint, Vector3 hitNormal, GameObject ballObject)
        {
            if (ballObject != gameObject)
                return;

            m_ImpactFlashExpiresAt = Time.time + m_ImpactFlashDuration;
            m_InFlight = false;
            m_TrailArmed = false;
            SetTrailActive(false, false);

            if (m_CoreParticles != null)
                m_CoreParticles.Emit(14);
        }

        public void BeginExtraBounceFlightVisuals()
        {
            ResolveReferences();

            m_InFlight = true;
            m_TrailArmed = false;
            m_TrailArmExpiresAt = 0f;
            m_TrailSlowExpiresAt = 0f;
            m_ReleaseFlashExpiresAt = Time.time + Mathf.Max(0.08f, m_ReleaseFlashDuration * 0.65f);

            SetTrailActive(true, true);
            EmitReleaseBurst();
        }

        void ResolveReferences()
        {
            if (m_VisualRoot == null)
                m_VisualRoot = transform.Find("VisualRoot");

            if (m_Trail == null && m_VisualRoot != null)
                m_Trail = m_VisualRoot.GetComponent<TrailRenderer>();

            if (m_CoreLight == null && m_VisualRoot != null)
                m_CoreLight = m_VisualRoot.GetComponentInChildren<Light>(true);

            if (m_CoreParticles == null && m_VisualRoot != null)
                m_CoreParticles = m_VisualRoot.GetComponentInChildren<ParticleSystem>(true);
        }

        void CacheAnimatedRenderers()
        {
            m_RendererStates.Clear();

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
                bool isCore = key.Contains("core_whitecyan") || key.Contains("energyorb");
                bool isHologram = key.Contains("hologram") || key.Contains("pulsearc");
                bool isCyan = isCore || isHologram || key.Contains("cyan") || key.Contains("inlay") || key.Contains("statusdot");

                if (!isCyan)
                    continue;

                float glowWeight = isCore ? 1.35f : isHologram ? 0.42f : 1f;
                m_RendererStates.Add(new RendererState
                {
                    Renderer = targetRenderer,
                    BaseColor = ResolveMaterialColor(material),
                    GlowWeight = glowWeight,
                    IsCore = isCore,
                    IsHologram = isHologram
                });
            }
        }

        void CacheSpinTransforms()
        {
            m_SpinTransforms.Clear();

            if (m_VisualRoot == null)
                return;

            Transform[] transforms = m_VisualRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform target = transforms[i];
                string name = target.name.ToLowerInvariant();
                if (name.Contains("equator_cyanscanring")
                    || name.Contains("equator_cyantick")
                    || name.Contains("equator_darkraisedbelt"))
                {
                    m_SpinTransforms.Add(target);
                }
            }
        }

        void UpdateTrailState(float speed, bool heldOrSocketed)
        {
            if (heldOrSocketed)
            {
                m_TrailArmed = false;
                SetTrailActive(false, true);
                return;
            }

            if (m_TrailArmed && Time.time > m_TrailArmExpiresAt)
                m_TrailArmed = false;

            if (m_TrailArmed && speed >= m_MinThrowSpeed)
            {
                m_TrailArmed = false;
                m_TrailSlowExpiresAt = 0f;
                SetTrailActive(true, true);
            }

            if (!m_TrailActive)
                return;

            if (speed <= m_StopTrailSpeed)
            {
                if (m_TrailSlowExpiresAt <= 0f)
                    m_TrailSlowExpiresAt = Time.time + m_SlowTrailLinger;

                if (Time.time >= m_TrailSlowExpiresAt)
                    SetTrailActive(false, false);
            }
            else
            {
                m_TrailSlowExpiresAt = 0f;
            }
        }

        void UpdateGlow(float speed, bool heldOrSocketed)
        {
            bool inFlight = m_InFlight && !heldOrSocketed;
            float speedFactor = inFlight ? Mathf.InverseLerp(m_MinThrowSpeed, m_MaxVisualSpeed, speed) : 0f;
            float idleWave = 0.5f + 0.5f * Mathf.Sin(Time.time * m_IdlePulseSpeed);
            float breath = Mathf.SmoothStep(0f, 1f, idleWave);
            float standbyGlow = Mathf.Lerp(m_StandbyBreathMin, m_StandbyBreathMax, breath);
            float steadyGlow = m_InFlightSteadyGlow + speedFactor * 0.12f;
            float releaseFlash = ResolveFlash01(m_ReleaseFlashExpiresAt, m_ReleaseFlashDuration);
            float impactFlash = ResolveFlash01(m_ImpactFlashExpiresAt, m_ImpactFlashDuration);
            float flash = Mathf.Max(releaseFlash, impactFlash);
            float intensity = (inFlight ? steadyGlow : standbyGlow) + flash * 1.25f;

            for (int i = 0; i < m_RendererStates.Count; i++)
            {
                RendererState state = m_RendererStates[i];
                if (state.Renderer == null)
                    continue;

                Color targetColor = state.IsCore ? m_CoreColor : m_CyanColor;
                float hologramEnergy = inFlight
                    ? Mathf.Max(0.52f, speedFactor * 0.85f, flash)
                    : Mathf.Max(breath * 0.22f, flash * 0.55f);
                float alpha = state.IsHologram
                    ? Mathf.Lerp(0.06f, 0.58f, Mathf.Clamp01(hologramEnergy))
                    : state.BaseColor.a;

                float colorBlend = inFlight
                    ? Mathf.Clamp01(0.72f + speedFactor * 0.18f + flash * 0.4f)
                    : Mathf.Clamp01(0.38f + breath * 0.18f + flash * 0.4f);
                Color color = Color.Lerp(state.BaseColor, targetColor, colorBlend);
                color *= Mathf.Max(0.05f, intensity * state.GlowWeight);
                color.a = alpha;

                m_PropertyBlock.Clear();
                state.Renderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(BaseColorId, color);
                m_PropertyBlock.SetColor(ColorId, color);
                m_PropertyBlock.SetColor(EmissionColorId, color);
                state.Renderer.SetPropertyBlock(m_PropertyBlock);
            }

            if (m_CoreLight != null)
            {
                float standbyLight = m_IdleLightIntensity * Mathf.Lerp(0.55f, 1.35f, breath);
                float inFlightLight = m_ThrownLightIntensity * (0.92f + speedFactor * 0.08f);
                float lightIntensity = (inFlight ? inFlightLight : standbyLight) + flash * m_FlashLightBoost;
                m_CoreLight.enabled = lightIntensity > 0.01f;
                m_CoreLight.intensity = lightIntensity;
                m_CoreLight.color = Color.Lerp(m_CyanColor, m_CoreColor, 0.45f + flash * 0.35f);
            }
        }

        void UpdateSpin(float speed, bool heldOrSocketed)
        {
            if (m_SpinTransforms.Count == 0)
                return;

            bool inFlight = m_InFlight && !heldOrSocketed;
            float speedFactor = inFlight ? Mathf.InverseLerp(m_MinThrowSpeed, m_MaxVisualSpeed, speed) : 0f;
            float degrees = Mathf.Lerp(m_IdleSpinDegreesPerSecond, m_ThrownSpinDegreesPerSecond, speedFactor) * Time.deltaTime;

            if (degrees <= 0f)
                return;

            Vector3 center = m_VisualRoot != null ? m_VisualRoot.position : transform.position;
            Vector3 axis = m_VisualRoot != null ? m_VisualRoot.forward : transform.forward;
            for (int i = 0; i < m_SpinTransforms.Count; i++)
            {
                Transform target = m_SpinTransforms[i];
                if (target != null)
                    target.RotateAround(center, axis, degrees);
            }
        }

        void UpdateParticles(float speed, bool heldOrSocketed)
        {
            if (m_CoreParticles == null)
                return;

            bool inFlight = m_InFlight && !heldOrSocketed;
            float speedFactor = inFlight ? Mathf.InverseLerp(m_MinThrowSpeed, m_MaxVisualSpeed, speed) : 0f;
            ConfigureParticleEmission(inFlight ? Mathf.Lerp(2.5f, 11f, speedFactor) : 0.12f);
        }

        void ConfigureParticleEmission(float rate)
        {
            if (m_CoreParticles == null)
                return;

            var emission = m_CoreParticles.emission;
            emission.rateOverTime = rate;
        }

        void EmitReleaseBurst()
        {
            if (m_CoreParticles != null)
                m_CoreParticles.Emit(8);
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

        bool IsHeldOrSocketed()
        {
            if (m_GrabInteractable == null)
                return false;

            if (!m_GrabInteractable.isSelected)
                return false;

            return true;
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

        float ResolveFlash01(float expiresAt, float duration)
        {
            if (duration <= 0f || Time.time >= expiresAt)
                return 0f;

            return Mathf.Clamp01((expiresAt - Time.time) / duration);
        }

        static Color ResolveMaterialColor(Material material)
        {
            if (material.HasProperty(BaseColorId))
                return material.GetColor(BaseColorId);

            if (material.HasProperty(ColorId))
                return material.GetColor(ColorId);

            return Color.white;
        }
    }
}
