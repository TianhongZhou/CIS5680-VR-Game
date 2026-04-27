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
    public sealed class StickyPulseBallVisualController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("References")]
        [SerializeField] Transform m_VisualRoot;
        [SerializeField] TrailRenderer m_Trail;
        [SerializeField] Light m_CoreLight;
        [SerializeField] ParticleSystem m_CoreParticles;
        [SerializeField] Transform m_ContactRing;
        [SerializeField] AudioSource m_LockAudioSource;
        [SerializeField] AudioClip m_LockClip;

        [Header("Throw trail")]
        [SerializeField, Min(0f)] float m_MinThrowSpeed = 0.68f;
        [SerializeField, Min(0f)] float m_StopTrailSpeed = 0.18f;
        [SerializeField, Min(0f)] float m_ThrowArmDuration = 0.45f;
        [SerializeField, Min(0f)] float m_SlowTrailLinger = 0.12f;
        [SerializeField, Min(0f)] float m_MaxVisualSpeed = 4.5f;

        [Header("Glow")]
        [SerializeField] Color m_TealColor = new(0f, 0.92f, 0.78f, 1f);
        [SerializeField] Color m_CoreColor = new(0.52f, 1f, 0.9f, 1f);
        [SerializeField, Min(0f)] float m_IdlePulseSpeed = 1.15f;
        [SerializeField, Min(0f)] float m_IdleLightIntensity = 0.18f;
        [SerializeField, Min(0f)] float m_InFlightLightIntensity = 0.72f;
        [SerializeField, Range(0f, 1f)] float m_StandbyBreathMin = 0.34f;
        [SerializeField, Range(0f, 2f)] float m_StandbyBreathMax = 0.96f;
        [SerializeField, Range(0f, 2f)] float m_InFlightSteadyGlow = 0.9f;
        [SerializeField, Min(0f)] float m_ReleaseFlashDuration = 0.16f;
        [SerializeField, Min(0f)] float m_LockFlashDuration = 0.28f;
        [SerializeField, Min(0f)] float m_PulseFlashDuration = 0.22f;
        [SerializeField, Min(0f)] float m_FlashLightBoost = 0.95f;

        [Header("Attachment lock")]
        [SerializeField, Min(0f)] float m_LockVisualDuration = 0.62f;
        [SerializeField, Min(0f)] float m_ContactRingStartScale = 0.75f;
        [SerializeField, Min(0f)] float m_ContactRingEndScale = 1.28f;
        [SerializeField, Range(0f, 1f)] float m_ContactRingMinAlpha = 0.05f;
        [SerializeField, Range(0f, 1f)] float m_ContactRingMaxAlpha = 0.62f;
        [SerializeField, Min(0f)] float m_ClawSettleDistance = 0.0028f;
        [SerializeField, Range(0f, 1f)] float m_ClawSettleScale = 0.09f;

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] float m_LockVolume = 0.28f;
        [SerializeField, Min(0.1f)] float m_LockMinDistance = 0.35f;
        [SerializeField, Min(0.1f)] float m_LockMaxDistance = 4.4f;

        readonly List<RendererState> m_RendererStates = new();
        readonly List<TransformState> m_ClawStates = new();
        MaterialPropertyBlock m_PropertyBlock;
        XRGrabInteractable m_GrabInteractable;
        Rigidbody m_Rigidbody;
        Renderer[] m_ContactRingRenderers;
        float m_TrailArmExpiresAt;
        float m_TrailSlowExpiresAt;
        float m_ReleaseFlashExpiresAt;
        float m_LockStartedAt = float.NegativeInfinity;
        float m_LockFlashExpiresAt;
        float m_PulseFlashExpiresAt;
        bool m_TrailArmed;
        bool m_TrailActive;
        bool m_InFlight;
        bool m_Attached;
        bool m_AttachLocking;

        struct RendererState
        {
            public Renderer Renderer;
            public Color BaseColor;
            public float GlowWeight;
            public bool IsCore;
            public bool IsContactRing;
        }

        struct TransformState
        {
            public Transform Transform;
            public Vector3 BaseLocalPosition;
            public Vector3 RadialLocalDirection;
            public Vector3 BaseLocalScale;
        }

        void Awake()
        {
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
            m_Rigidbody = GetComponent<Rigidbody>();
            m_PropertyBlock = new MaterialPropertyBlock();

            ResolveReferences();
            ConfigureLockAudioSource();
            CacheAnimatedRenderers();
            CacheClawTransforms();
            CacheContactRingRenderers();
            SetTrailActive(false, true);
            ConfigureParticleEmission(0f);
            SetContactRingVisible(false);
        }

        void OnEnable()
        {
            if (m_GrabInteractable != null)
            {
                m_GrabInteractable.selectEntered.AddListener(OnSelectEntered);
                m_GrabInteractable.selectExited.AddListener(OnSelectExited);
            }

            ThrowableBall.ImpactOccurred += OnBallImpactOccurred;
            StickyPulseImpactEffect.AttachLockStarted += OnAttachLockStarted;
            StickyPulseImpactEffect.AttachLockCompleted += OnAttachLockCompleted;
            StickyPulseImpactEffect.PulseSpawnedByBall += OnStickyPulseSpawned;

            m_InFlight = false;
            m_Attached = false;
            m_AttachLocking = false;
            SetTrailActive(false, true);
            SetContactRingVisible(false);
        }

        void OnDisable()
        {
            if (m_GrabInteractable != null)
            {
                m_GrabInteractable.selectEntered.RemoveListener(OnSelectEntered);
                m_GrabInteractable.selectExited.RemoveListener(OnSelectExited);
            }

            ThrowableBall.ImpactOccurred -= OnBallImpactOccurred;
            StickyPulseImpactEffect.AttachLockStarted -= OnAttachLockStarted;
            StickyPulseImpactEffect.AttachLockCompleted -= OnAttachLockCompleted;
            StickyPulseImpactEffect.PulseSpawnedByBall -= OnStickyPulseSpawned;
            SetTrailActive(false, true);
        }

        void Update()
        {
            float speed = ResolveSpeed();
            bool heldOrSocketed = IsHeldOrSocketed();

            if (!heldOrSocketed && !m_Attached && !m_InFlight && speed >= m_MinThrowSpeed)
                m_InFlight = true;

            UpdateTrailState(speed, heldOrSocketed);
            UpdateGlow(speed, heldOrSocketed);
            UpdateParticles(speed, heldOrSocketed);
            UpdateAttachmentLockVisuals();
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            m_InFlight = false;
            m_Attached = false;
            m_AttachLocking = false;
            m_TrailArmed = false;
            SetTrailActive(false, true);
            SetContactRingVisible(false);
            ResetClawTransforms();
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject is XRSocketInteractor)
            {
                m_InFlight = false;
                m_Attached = false;
                m_AttachLocking = false;
                m_TrailArmed = false;
                SetTrailActive(false, true);
                SetContactRingVisible(false);
                ResetClawTransforms();
                return;
            }

            if (m_GrabInteractable != null && m_GrabInteractable.isSelected)
                return;

            m_TrailArmed = true;
            m_TrailArmExpiresAt = Time.time + m_ThrowArmDuration;
            m_TrailSlowExpiresAt = 0f;
            m_ReleaseFlashExpiresAt = Time.time + m_ReleaseFlashDuration;
        }

        void OnBallImpactOccurred(BallType ballType, Vector3 hitPoint, Vector3 hitNormal, GameObject ballObject)
        {
            if (ballObject != gameObject)
                return;

            m_InFlight = false;
            m_TrailArmed = false;
            SetTrailActive(false, false);
        }

        void OnAttachLockStarted(GameObject ballObject, Vector3 origin, Vector3 normal, Collider sourceCollider)
        {
            if (ballObject != gameObject)
                return;

            m_Attached = true;
            m_AttachLocking = true;
            m_InFlight = false;
            m_TrailArmed = false;
            m_LockStartedAt = Time.time;
            m_LockFlashExpiresAt = Time.time + m_LockFlashDuration;
            SetTrailActive(false, false);
            SetContactRingVisible(true);
            PlayLockAudio();

            if (m_CoreParticles != null)
                m_CoreParticles.Emit(8);
        }

        void OnAttachLockCompleted(GameObject ballObject, Vector3 origin, Vector3 normal, Collider sourceCollider)
        {
            if (ballObject != gameObject)
                return;

            m_AttachLocking = false;
            m_LockFlashExpiresAt = Time.time + m_LockFlashDuration * 0.65f;
            SetContactRingVisible(true);
            ApplyClawProgress(1f);

            if (m_CoreParticles != null)
                m_CoreParticles.Emit(10);
        }

        void OnStickyPulseSpawned(GameObject ballObject, Vector3 pulseOrigin, float pulseRadius, Collider sourceCollider)
        {
            if (ballObject != gameObject)
                return;

            if (!m_Attached)
                return;

            m_PulseFlashExpiresAt = Time.time + m_PulseFlashDuration;

            if (m_CoreParticles != null)
                m_CoreParticles.Emit(12);
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

            if (m_ContactRing == null && m_VisualRoot != null)
            {
                Transform[] transforms = m_VisualRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name.ToLowerInvariant().Contains("contactlockring"))
                    {
                        m_ContactRing = transforms[i];
                        break;
                    }
                }
            }

            if (m_LockAudioSource == null)
                m_LockAudioSource = GetComponent<AudioSource>();
        }

        void ConfigureLockAudioSource()
        {
            if (m_LockAudioSource == null)
                m_LockAudioSource = gameObject.AddComponent<AudioSource>();

            m_LockAudioSource.playOnAwake = false;
            m_LockAudioSource.loop = false;
            m_LockAudioSource.spatialBlend = 1f;
            m_LockAudioSource.rolloffMode = AudioRolloffMode.Linear;
            m_LockAudioSource.minDistance = Mathf.Max(0.1f, m_LockMinDistance);
            m_LockAudioSource.maxDistance = Mathf.Max(m_LockMinDistance + 0.1f, m_LockMaxDistance);
            m_LockAudioSource.dopplerLevel = 0f;
            m_LockAudioSource.spread = 0f;
            m_LockAudioSource.volume = Mathf.Clamp01(m_LockVolume);
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
                bool isCore = key.Contains("pulsecore") || key.Contains("energypoint") || key.Contains("tealglass");
                bool isContactRing = key.Contains("contactlockring") || key.Contains("pressuresseal") || key.Contains("pressureseal");
                bool isTeal = isCore
                    || isContactRing
                    || key.Contains("teal")
                    || key.Contains("diagnostic")
                    || key.Contains("statuswindow")
                    || key.Contains("inlay");

                if (!isTeal)
                    continue;

                m_RendererStates.Add(new RendererState
                {
                    Renderer = targetRenderer,
                    BaseColor = ResolveMaterialColor(material),
                    GlowWeight = isCore ? 1.35f : isContactRing ? 0.82f : 1f,
                    IsCore = isCore,
                    IsContactRing = isContactRing
                });
            }
        }

        void CacheClawTransforms()
        {
            m_ClawStates.Clear();

            if (m_VisualRoot == null)
                return;

            Transform[] transforms = m_VisualRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform target = transforms[i];
                string name = target.name.ToLowerInvariant();
                if (!name.Contains("anchorclaw"))
                    continue;

                Vector3 direction = target.localPosition;
                direction.z = 0f;
                if (direction.sqrMagnitude < 0.0001f)
                    direction = Vector3.right;
                direction.Normalize();

                m_ClawStates.Add(new TransformState
                {
                    Transform = target,
                    BaseLocalPosition = target.localPosition,
                    BaseLocalScale = target.localScale,
                    RadialLocalDirection = direction
                });
            }
        }

        void CacheContactRingRenderers()
        {
            m_ContactRingRenderers = m_ContactRing != null
                ? m_ContactRing.GetComponentsInChildren<Renderer>(true)
                : null;
        }

        void UpdateTrailState(float speed, bool heldOrSocketed)
        {
            if (heldOrSocketed || m_Attached)
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
            bool inFlight = m_InFlight && !heldOrSocketed && !m_Attached;
            float speedFactor = inFlight ? Mathf.InverseLerp(m_MinThrowSpeed, m_MaxVisualSpeed, speed) : 0f;
            float idleWave = 0.5f + 0.5f * Mathf.Sin(Time.time * m_IdlePulseSpeed);
            float breath = Mathf.SmoothStep(0f, 1f, idleWave);
            float standbyGlow = Mathf.Lerp(m_StandbyBreathMin, m_StandbyBreathMax, breath);
            float steadyGlow = m_InFlightSteadyGlow + speedFactor * 0.1f;
            float releaseFlash = ResolveFlash01(m_ReleaseFlashExpiresAt, m_ReleaseFlashDuration);
            float lockFlash = ResolveFlash01(m_LockFlashExpiresAt, m_LockFlashDuration);
            float pulseFlash = ResolveFlash01(m_PulseFlashExpiresAt, m_PulseFlashDuration);
            float flash = Mathf.Max(releaseFlash, lockFlash, pulseFlash);
            float lockEnergy = ResolveLockProgress();
            float attachedPulse = m_Attached ? Mathf.Lerp(0.7f, 1.08f, breath) : 0f;
            float intensity = (m_Attached ? attachedPulse : inFlight ? steadyGlow : standbyGlow) + flash * 1.1f + lockEnergy * 0.25f;

            for (int i = 0; i < m_RendererStates.Count; i++)
            {
                RendererState state = m_RendererStates[i];
                if (state.Renderer == null)
                    continue;

                float colorBlend = m_Attached
                    ? Mathf.Clamp01(0.62f + flash * 0.32f)
                    : inFlight
                        ? Mathf.Clamp01(0.62f + speedFactor * 0.18f + flash * 0.3f)
                        : Mathf.Clamp01(0.32f + breath * 0.18f + flash * 0.3f);
                Color targetColor = state.IsCore ? m_CoreColor : m_TealColor;
                Color color = Color.Lerp(state.BaseColor, targetColor, colorBlend);
                color *= Mathf.Max(0.04f, intensity * state.GlowWeight);

                if (state.IsContactRing)
                    color.a = Mathf.Lerp(m_ContactRingMinAlpha, m_ContactRingMaxAlpha, Mathf.Clamp01(lockEnergy + flash));

                m_PropertyBlock.Clear();
                state.Renderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(BaseColorId, color);
                m_PropertyBlock.SetColor(ColorId, color);
                m_PropertyBlock.SetColor(EmissionColorId, color);
                state.Renderer.SetPropertyBlock(m_PropertyBlock);
            }

            if (m_CoreLight != null)
            {
                float standbyLight = m_IdleLightIntensity * Mathf.Lerp(0.55f, 1.22f, breath);
                float flightLight = m_InFlightLightIntensity * (0.88f + speedFactor * 0.12f);
                float attachedLight = Mathf.Lerp(0.34f, 0.62f, breath);
                float lightIntensity = (m_Attached ? attachedLight : inFlight ? flightLight : standbyLight) + flash * m_FlashLightBoost;
                m_CoreLight.enabled = lightIntensity > 0.01f;
                m_CoreLight.intensity = lightIntensity;
                m_CoreLight.color = Color.Lerp(m_TealColor, m_CoreColor, 0.36f + flash * 0.34f);
            }
        }

        void UpdateParticles(float speed, bool heldOrSocketed)
        {
            if (m_CoreParticles == null)
                return;

            bool inFlight = m_InFlight && !heldOrSocketed && !m_Attached;
            float speedFactor = inFlight ? Mathf.InverseLerp(m_MinThrowSpeed, m_MaxVisualSpeed, speed) : 0f;
            float rate = m_Attached ? 1.15f : inFlight ? Mathf.Lerp(1.8f, 6.8f, speedFactor) : 0.08f;
            ConfigureParticleEmission(rate);
        }

        void UpdateAttachmentLockVisuals()
        {
            if (!m_Attached)
            {
                ApplyClawProgress(0f);
                SetContactRingVisible(false);
                return;
            }

            float progress = m_AttachLocking ? ResolveLockProgress() : 1f;
            ApplyClawProgress(progress);

            if (m_ContactRing == null)
                return;

            SetContactRingVisible(true);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5.5f);
            float scale = Mathf.Lerp(m_ContactRingStartScale, m_ContactRingEndScale, progress);
            scale *= m_AttachLocking ? 1f : Mathf.Lerp(0.98f, 1.04f, pulse);
            m_ContactRing.localScale = Vector3.one * scale;
        }

        void ApplyClawProgress(float progress)
        {
            float settled = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            for (int i = 0; i < m_ClawStates.Count; i++)
            {
                TransformState state = m_ClawStates[i];
                if (state.Transform == null)
                    continue;

                state.Transform.localPosition = state.BaseLocalPosition
                    + state.RadialLocalDirection * (m_ClawSettleDistance * settled);
                state.Transform.localScale = state.BaseLocalScale * (1f + m_ClawSettleScale * settled);
            }
        }

        void ResetClawTransforms()
        {
            for (int i = 0; i < m_ClawStates.Count; i++)
            {
                TransformState state = m_ClawStates[i];
                if (state.Transform == null)
                    continue;

                state.Transform.localPosition = state.BaseLocalPosition;
                state.Transform.localScale = state.BaseLocalScale;
            }
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

        void SetContactRingVisible(bool visible)
        {
            if (m_ContactRingRenderers == null)
                return;

            for (int i = 0; i < m_ContactRingRenderers.Length; i++)
            {
                if (m_ContactRingRenderers[i] != null)
                    m_ContactRingRenderers[i].enabled = visible;
            }
        }

        void PlayLockAudio()
        {
            ConfigureLockAudioSource();

            if (m_LockAudioSource == null || m_LockClip == null)
                return;

            m_LockAudioSource.PlayOneShot(m_LockClip, Mathf.Clamp01(m_LockVolume));
        }

        float ResolveLockProgress()
        {
            if (!m_AttachLocking || m_LockVisualDuration <= 0f)
                return m_Attached ? 1f : 0f;

            return Mathf.Clamp01((Time.time - m_LockStartedAt) / m_LockVisualDuration);
        }

        bool IsHeldOrSocketed()
        {
            if (m_GrabInteractable == null)
                return false;

            return m_GrabInteractable.isSelected;
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
