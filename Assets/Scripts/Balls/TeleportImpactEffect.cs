using UnityEngine;
using Unity.XR.CoreUtils;
using CIS5680VRGame.Gameplay;
using CIS5680VRGame.Progression;
using CIS5680VRGame.UI;

namespace CIS5680VRGame.Balls
{
    public class TeleportImpactEffect : BallImpactEffect
    {
        [SerializeField] XROrigin m_XROrigin;
        [SerializeField] Transform m_FallbackRigRoot;
        [SerializeField] float m_SurfaceOffset = 0.05f;
        [SerializeField] PulseManager m_PulseManager;
        [SerializeField, Tooltip("Fallback base radius used if no sonar pulse effect can be found in the scene.")]
        float m_BonusPulseBaseRadiusFallback = 15f;

        [Header("Teleport View Blink")]
        [SerializeField, Range(0f, 1f)] float m_TeleportViewEffectIntensity = 0.68f;
        [SerializeField, Range(0f, 1f)] float m_TeleportViewEffectPeakOpacity = 0.86f;
        [SerializeField, Min(0.01f)] float m_TeleportViewFadeOutDuration = 0.1f;
        [SerializeField, Min(0f)] float m_TeleportViewHoldDuration = 0.025f;
        [SerializeField, Min(0.01f)] float m_TeleportViewFadeInDuration = 0.18f;
        [SerializeField] Color m_TeleportViewTint = new(0.026f, 0.018f, 0.12f, 1f);
        [SerializeField] Color m_TeleportViewCoreColor = new(0.5f, 0.42f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] float m_TeleportHapticsAmplitude = 0.11f;
        [SerializeField, Min(0f)] float m_TeleportHapticsDuration = 0.055f;
        [SerializeField, Range(0f, 1.2f)] float m_TeleportAudioVolumeScale = 0.32f;
        [SerializeField] bool m_PlayWorldActivationBurst;

        int m_BonusPulseRadiusPercent;
        int m_BonusPulseRevealDurationSeconds;
        int m_SingleRunLandingPulseRevealDurationBonusPercent;
        bool m_LastImpactBecameAnchor;

        void Awake()
        {
            if (m_XROrigin == null)
                m_XROrigin = FindObjectOfType<XROrigin>();

            if (m_PulseManager == null)
                m_PulseManager = PulseManager.Instance != null ? PulseManager.Instance : FindObjectOfType<PulseManager>();

            ApplyPersistentLandingPulseRadiusPercent(ResolveProfileLandingPulseRadiusPercent());
            ApplyPersistentLandingPulseRevealDurationSeconds(ResolveProfileLandingPulseRevealDurationSeconds());
        }

        public override void Apply(in BallImpactContext context)
        {
            m_LastImpactBecameAnchor = false;

            if (TeleportBallLauncher.TryRegisterAnchor(this, context))
            {
                m_LastImpactBecameAnchor = true;
                return;
            }

            TeleportNow(context);
        }

        public override bool ShouldDestroyBallAfterImpact(in BallImpactContext context)
        {
            return !m_LastImpactBecameAnchor;
        }

        public void ConfirmAnchoredTeleport(in BallImpactContext context)
        {
            TeleportNow(context);
        }

        void TeleportNow(in BallImpactContext context)
        {
            if (m_XROrigin == null && m_FallbackRigRoot == null)
                return;

            if (m_PlayWorldActivationBurst
                && context.BallObject != null
                && context.BallObject.TryGetComponent(out TeleportBallVisualController visualController))
            {
                visualController.PlayTeleportActivationBurst(context.HitPoint, context.HitNormal);
            }

            BallImpactContext capturedContext = context;
            XROrigin targetOrigin = m_XROrigin;
            Transform fallbackRigRoot = m_FallbackRigRoot;
            Vector3 destination = ResolveTeleportDestination(capturedContext, targetOrigin);
            PulseManager pulseManager = m_PulseManager;
            int bonusPulseRadiusPercent = m_BonusPulseRadiusPercent;
            int bonusPulseRevealDurationSeconds = m_BonusPulseRevealDurationSeconds;
            int singleRunRevealBonusPercent = m_SingleRunLandingPulseRevealDurationBonusPercent;
            float bonusPulseBaseRadiusFallback = m_BonusPulseBaseRadiusFallback;
            float teleportAudioVolumeScale = m_TeleportAudioVolumeScale;

            var settings = new TeleportViewEffectService.BlinkSettings
            {
                Intensity = m_TeleportViewEffectIntensity,
                PeakOpacity = m_TeleportViewEffectPeakOpacity,
                FadeOutDuration = m_TeleportViewFadeOutDuration,
                HoldDuration = m_TeleportViewHoldDuration,
                FadeInDuration = m_TeleportViewFadeInDuration,
                Tint = m_TeleportViewTint,
                CoreColor = m_TeleportViewCoreColor,
                HapticsAmplitude = m_TeleportHapticsAmplitude,
                HapticsDuration = m_TeleportHapticsDuration,
            };

            TeleportViewEffectService.PlayBlink(settings, () =>
            {
                if (targetOrigin != null)
                {
                    targetOrigin.MoveCameraToWorldLocation(destination);
                }
                else if (fallbackRigRoot != null)
                {
                    fallbackRigRoot.position = destination;
                }

                PulseAudioService.PlayTeleportArrival(teleportAudioVolumeScale);
                TrySpawnLandingPulseFromSnapshot(
                    capturedContext,
                    pulseManager,
                    bonusPulseRadiusPercent,
                    bonusPulseRevealDurationSeconds,
                    singleRunRevealBonusPercent,
                    bonusPulseBaseRadiusFallback);
            });
        }

        public void ApplyPersistentLandingPulseRadiusPercent(int bonusPercent)
        {
            m_BonusPulseRadiusPercent = Mathf.Max(0, bonusPercent);
        }

        public void ApplyPersistentLandingPulseRevealDurationSeconds(int durationSeconds)
        {
            m_BonusPulseRevealDurationSeconds = Mathf.Max(0, durationSeconds);
        }

        public void ApplySingleRunLandingPulseRevealDurationBonusPercent(int bonusPercent)
        {
            m_SingleRunLandingPulseRevealDurationBonusPercent = Mathf.Max(0, bonusPercent);
        }

        Vector3 ResolveTeleportDestination(in BallImpactContext context, XROrigin targetOrigin)
        {
            Vector3 destination = context.HitPoint + context.HitNormal * m_SurfaceOffset;
            if (targetOrigin != null)
                destination += Vector3.up * targetOrigin.CameraInOriginSpaceHeight;

            return destination;
        }

        static void TrySpawnLandingPulseFromSnapshot(
            in BallImpactContext context,
            PulseManager pulseManager,
            int bonusPulseRadiusPercent,
            int bonusPulseRevealDurationSeconds,
            int singleRunRevealDurationBonusPercent,
            float bonusPulseBaseRadiusFallback)
        {
            if (bonusPulseRadiusPercent <= 0 || bonusPulseRevealDurationSeconds <= 0)
                return;

            if (pulseManager == null)
                pulseManager = PulseManager.Instance != null ? PulseManager.Instance : FindObjectOfType<PulseManager>();

            if (pulseManager == null)
                return;

            float baseRadius = ResolveCurrentSonarPulseRadius(bonusPulseBaseRadiusFallback);
            float pulseRadius = Mathf.Max(0.1f, baseRadius * (bonusPulseRadiusPercent / 100f));
            float revealDurationSeconds = bonusPulseRevealDurationSeconds
                * (1f + Mathf.Max(0, singleRunRevealDurationBonusPercent) / 100f);
            pulseManager.SpawnPulse(
                context.HitPoint,
                context.HitNormal,
                pulseRadius,
                context.HitCollider,
                revealDurationSeconds);
            PulseAudioService.PlayPulse(context.HitPoint);
        }

        static float ResolveCurrentSonarPulseRadius(float bonusPulseBaseRadiusFallback)
        {
            SonarPulseImpactEffect sonarPulseEffect = FindObjectOfType<SonarPulseImpactEffect>(true);
            return sonarPulseEffect != null
                ? Mathf.Max(0.1f, sonarPulseEffect.CurrentPulseRadius)
                : Mathf.Max(0.1f, bonusPulseBaseRadiusFallback);
        }

        static int ResolveProfileLandingPulseRadiusPercent()
        {
            return ProfileService.TryGetCurrentProfile(out ProfileSaveData profile) && profile != null
                ? ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.TeleportLandingPulseRadiusPercent)
                : 0;
        }

        static int ResolveProfileLandingPulseRevealDurationSeconds()
        {
            return ProfileService.TryGetCurrentProfile(out ProfileSaveData profile) && profile != null
                ? ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.TeleportLandingPulseRevealDurationSeconds)
                : 0;
        }
    }
}
