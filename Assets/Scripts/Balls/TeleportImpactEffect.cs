using UnityEngine;
using Unity.XR.CoreUtils;
using CIS5680VRGame.Gameplay;
using CIS5680VRGame.Progression;

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

        int m_BonusPulseRadiusPercent;
        int m_BonusPulseRevealDurationSeconds;
        int m_SingleRunLandingPulseRevealDurationBonusPercent;

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
            if (m_XROrigin == null && m_FallbackRigRoot == null)
                return;

            var destination = context.HitPoint + context.HitNormal * m_SurfaceOffset;

            if (m_XROrigin != null)
            {
                destination += Vector3.up * m_XROrigin.CameraInOriginSpaceHeight;
                m_XROrigin.MoveCameraToWorldLocation(destination);
                PulseAudioService.PlayTeleportArrival(0.96f);
            }
            else
            {
                m_FallbackRigRoot.position = destination;
                PulseAudioService.PlayTeleportArrival(0.96f);
            }

            TrySpawnLandingPulse(context);
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

        void TrySpawnLandingPulse(in BallImpactContext context)
        {
            if (m_BonusPulseRadiusPercent <= 0 || m_BonusPulseRevealDurationSeconds <= 0)
                return;

            if (m_PulseManager == null)
                m_PulseManager = PulseManager.Instance != null ? PulseManager.Instance : FindObjectOfType<PulseManager>();

            if (m_PulseManager == null)
                return;

            float baseRadius = ResolveCurrentSonarPulseRadius();
            float pulseRadius = Mathf.Max(0.1f, baseRadius * (m_BonusPulseRadiusPercent / 100f));
            float revealDurationSeconds = m_BonusPulseRevealDurationSeconds
                * (1f + Mathf.Max(0, m_SingleRunLandingPulseRevealDurationBonusPercent) / 100f);
            m_PulseManager.SpawnPulse(
                context.HitPoint,
                context.HitNormal,
                pulseRadius,
                context.Collision.collider,
                revealDurationSeconds);
            PulseAudioService.PlayPulse(context.HitPoint);
        }

        float ResolveCurrentSonarPulseRadius()
        {
            SonarPulseImpactEffect sonarPulseEffect = FindObjectOfType<SonarPulseImpactEffect>(true);
            return sonarPulseEffect != null
                ? Mathf.Max(0.1f, sonarPulseEffect.CurrentPulseRadius)
                : Mathf.Max(0.1f, m_BonusPulseBaseRadiusFallback);
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
