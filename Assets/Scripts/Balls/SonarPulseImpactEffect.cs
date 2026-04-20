using System;
using CIS5680VRGame.Progression;
using UnityEngine;
using CIS5680VRGame.Gameplay;

namespace CIS5680VRGame.Balls
{
    public class SonarPulseImpactEffect : BallImpactEffect
    {
        public static event Action<Vector3, float, Collider> PulseSpawned;

        [SerializeField] PulseManager m_PulseManager;
        [SerializeField] float m_PulseRadius = 10f;
        [SerializeField, Tooltip("How much of the normal sonar radius the bonus bounce pulse reveals at 100% upgrade progress.")]
        float m_MaxExtraBounceRadiusPercent = 100f;
        float m_BasePulseRadius;
        int m_PersistentPulseRadiusBonusPercent;
        int m_SingleRunPulseRadiusBonusPercent;
        int m_ExtraBouncePulseRadiusPercent;

        public float BasePulseRadius => m_BasePulseRadius > 0f ? m_BasePulseRadius : m_PulseRadius;
        public float CurrentPulseRadius => m_PulseRadius;

        void Awake()
        {
            m_BasePulseRadius = m_PulseRadius;
            if (m_PulseManager == null)
                m_PulseManager = FindObjectOfType<PulseManager>();

            ApplyPersistentPulseRadiusBonusPercent(ResolveProfilePulseRadiusBonusPercent());
            ApplyPersistentExtraBouncePulseRadiusPercent(ResolveProfileExtraBounceRadiusPercent());
        }

        public override void Apply(in BallImpactContext context)
        {
            if (m_PulseManager == null)
                m_PulseManager = FindObjectOfType<PulseManager>();

            if (m_PulseManager == null)
                return;

            m_PulseManager.SpawnPulse(context.HitPoint, context.HitNormal, m_PulseRadius, context.Collision.collider);
            PulseAudioService.PlayPulse(context.HitPoint);
            PulseSpawned?.Invoke(context.HitPoint, m_PulseRadius, context.Collision.collider);

            if (ShouldSpawnExtraBounce(context))
            {
                ThrowableBall throwableBall = context.BallObject != null ? context.BallObject.GetComponent<ThrowableBall>() : null;
                if (throwableBall != null)
                {
                    float extraBounceRadius = m_PulseRadius * Mathf.Clamp01(m_ExtraBouncePulseRadiusPercent / m_MaxExtraBounceRadiusPercent);
                    SonarExtraBounceProxy.SpawnFromImpact(
                        context,
                        m_PulseManager,
                        extraBounceRadius,
                        throwableBall.ValidGroundLayers,
                        throwableBall.MinGroundUpDot,
                        throwableBall.RequireGroundContact);
                }
            }
        }

        public void ApplyPersistentPulseRadiusBonusPercent(int bonusPercent)
        {
            m_PersistentPulseRadiusBonusPercent = Mathf.Max(0, bonusPercent);
            RecomputePulseRadius();
        }

        public void ApplySingleRunPulseRadiusBonusPercent(int bonusPercent)
        {
            m_SingleRunPulseRadiusBonusPercent = Mathf.Max(0, bonusPercent);
            RecomputePulseRadius();
        }

        public void ApplyPersistentExtraBouncePulseRadiusPercent(int bonusPercent)
        {
            m_ExtraBouncePulseRadiusPercent = Mathf.Clamp(bonusPercent, 0, Mathf.RoundToInt(m_MaxExtraBounceRadiusPercent));
        }

        bool ShouldSpawnExtraBounce(in BallImpactContext context)
        {
            if (m_ExtraBouncePulseRadiusPercent <= 0)
                return false;

            Vector3 hitNormal = context.HitNormal.sqrMagnitude > 0.0001f ? context.HitNormal.normalized : Vector3.up;
            return Vector3.Dot(hitNormal, Vector3.up) >= 0.5f;
        }

        static int ResolveProfilePulseRadiusBonusPercent()
        {
            return ProfileService.TryGetCurrentProfile(out ProfileSaveData profile) && profile != null
                ? ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.PulseRadiusBonusPercent)
                : 0;
        }

        static int ResolveProfileExtraBounceRadiusPercent()
        {
            return ProfileService.TryGetCurrentProfile(out ProfileSaveData profile) && profile != null
                ? ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.SonarExtraBouncePulseRadiusPercent)
                : 0;
        }

        void RecomputePulseRadius()
        {
            float persistentMultiplier = 1f + Mathf.Max(0, m_PersistentPulseRadiusBonusPercent) / 100f;
            float singleRunMultiplier = 1f + Mathf.Max(0, m_SingleRunPulseRadiusBonusPercent) / 100f;
            m_PulseRadius = Mathf.Max(0.1f, m_BasePulseRadius * persistentMultiplier * singleRunMultiplier);
        }
    }
}
