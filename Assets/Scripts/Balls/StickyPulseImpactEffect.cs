using System;
using System.Collections;
using CIS5680VRGame.Progression;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using CIS5680VRGame.Gameplay;

namespace CIS5680VRGame.Balls
{
    public class StickyPulseImpactEffect : BallImpactEffect
    {
        public static event Action<GameObject, Vector3, Vector3, Collider> AttachLockStarted;
        public static event Action<GameObject, Vector3, Vector3, Collider> AttachLockCompleted;
        public static event Action<Vector3, float, Collider> PulseSpawned;
        public static event Action<GameObject, Vector3, float, Collider> PulseSpawnedByBall;

        [SerializeField] PulseManager m_PulseManager;
        [SerializeField] float m_PulseRadius = 15f;
        [SerializeField] float m_PulseInterval = 8f;
        [SerializeField] int m_PulseCount = 3;
        [SerializeField] float m_StickSurfaceOffset = 0.02f;
        [Header("Attachment Lock")]
        [SerializeField, Min(0f)] float m_AttachLockDuration = 0.55f;
        [SerializeField, Min(0f)] float m_AttachSettleOutwardOffset = 0.035f;
        [SerializeField, Range(0f, 25f)] float m_AttachSettleWobbleDegrees = 7f;
        [SerializeField, Min(0f)] float m_AttachSettleWobbleFrequency = 18f;
        [Header("Lifetime")]
        [SerializeField] float m_DestroyDelayAfterLastPulse = 0.75f;
        [SerializeField] float m_FadeDurationAfterLastPulse = 0.55f;
        float m_BasePulseRadius;
        int m_PersistentPulseRadiusBonusPercent;
        int m_SingleRunPulseRadiusBonusPercent;
        int m_PersistentExtraPulseCount;

        Rigidbody m_Rigidbody;
        Collider[] m_Colliders;
        XRGrabInteractable m_GrabInteractable;
        BallHoverInfoDisplay m_HoverInfoDisplay;
        Coroutine m_PulseRoutine;
        bool m_HasStuck;
        Collider m_StuckSurfaceCollider;
        Transform m_StuckAnchorTransform;
        Vector3 m_StuckLocalPosition;
        Quaternion m_StuckLocalRotation = Quaternion.identity;
        bool m_IsAttachLocking;
        float m_AttachLockT = 1f;

        void Awake()
        {
            m_BasePulseRadius = m_PulseRadius;
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Colliders = GetComponents<Collider>();
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
            m_HoverInfoDisplay = GetComponent<BallHoverInfoDisplay>();

            if (m_PulseManager == null)
                m_PulseManager = FindObjectOfType<PulseManager>();

            ApplyPersistentPulseRadiusBonusPercent(ResolveProfilePulseRadiusBonusPercent());
        }

        void OnDisable()
        {
            if (m_PulseRoutine != null)
            {
                StopCoroutine(m_PulseRoutine);
                m_PulseRoutine = null;
            }

            m_IsAttachLocking = false;
            m_AttachLockT = 1f;
        }

        void LateUpdate()
        {
            if (!m_HasStuck)
                return;

            ApplyStuckPose();
        }

        public override void Apply(in BallImpactContext context)
        {
            if (m_HasStuck)
                return;

            if (m_PulseManager == null)
                m_PulseManager = FindObjectOfType<PulseManager>();

            m_HasStuck = true;
            m_StuckSurfaceCollider = context.HitCollider;
            StickToSurface(context);
            m_PulseRoutine = StartCoroutine(LockThenEmitPulses());
        }

        void StickToSurface(in BallImpactContext context)
        {
            Vector3 surfaceNormal = context.HitNormal.sqrMagnitude < 0.0001f
                ? Vector3.up
                : context.HitNormal.normalized;

            transform.up = surfaceNormal;
            transform.position = context.HitPoint + surfaceNormal * m_StickSurfaceOffset;

            m_StuckAnchorTransform = context.HitCollider != null ? context.HitCollider.transform : null;
            if (m_StuckAnchorTransform != null)
            {
                m_StuckLocalPosition = m_StuckAnchorTransform.InverseTransformPoint(transform.position);
                m_StuckLocalRotation = Quaternion.Inverse(m_StuckAnchorTransform.rotation) * transform.rotation;
                transform.SetParent(null, true);
            }
            else
            {
                m_StuckLocalPosition = transform.position;
                m_StuckLocalRotation = transform.rotation;
            }

            if (m_Rigidbody != null)
            {
                m_Rigidbody.velocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
                m_Rigidbody.useGravity = false;
                m_Rigidbody.isKinematic = true;
                m_Rigidbody.detectCollisions = false;
            }

            if (m_GrabInteractable != null)
                m_GrabInteractable.enabled = false;

            if (m_HoverInfoDisplay != null)
                m_HoverInfoDisplay.enabled = false;

            if (m_Colliders == null)
                return;

            for (int i = 0; i < m_Colliders.Length; i++)
            {
                if (m_Colliders[i] != null)
                    m_Colliders[i].enabled = false;
            }
        }

        IEnumerator LockThenEmitPulses()
        {
            m_IsAttachLocking = true;
            m_AttachLockT = 0f;
            AttachLockStarted?.Invoke(gameObject, ResolvePulseOriginFromCurrentAnchor(), ResolveSurfaceNormalFromCurrentAnchor(), m_StuckSurfaceCollider);

            float duration = Mathf.Max(0f, m_AttachLockDuration);
            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    m_AttachLockT = Mathf.Clamp01(elapsed / duration);
                    ApplyStuckPose();
                    yield return null;
                }
            }

            m_AttachLockT = 1f;
            m_IsAttachLocking = false;
            ApplyStuckPose();
            AttachLockCompleted?.Invoke(gameObject, ResolvePulseOriginFromCurrentAnchor(), ResolveSurfaceNormalFromCurrentAnchor(), m_StuckSurfaceCollider);

            yield return EmitPulses();
        }

        IEnumerator EmitPulses()
        {
            int pulseCount = Mathf.Max(1, m_PulseCount);
            pulseCount += Mathf.Max(0, m_PersistentExtraPulseCount);
            float pulseInterval = Mathf.Max(0.01f, m_PulseInterval);

            for (int i = 0; i < pulseCount; i++)
            {
                SpawnPulseFromCurrentAnchor();

                if (i < pulseCount - 1)
                    yield return new WaitForSeconds(pulseInterval);
            }

            m_PulseRoutine = null;

            if (m_DestroyDelayAfterLastPulse > 0f)
                yield return new WaitForSeconds(m_DestroyDelayAfterLastPulse);

            BallFadeOut.Begin(gameObject, 0f, m_FadeDurationAfterLastPulse);
        }

        void SpawnPulseFromCurrentAnchor()
        {
            if (m_PulseManager == null)
                return;

            Vector3 surfaceNormal = ResolveSurfaceNormalFromCurrentAnchor();
            Vector3 pulseOrigin = ResolvePulseOriginFromCurrentAnchor();
            m_PulseManager.SpawnPulse(pulseOrigin, surfaceNormal, m_PulseRadius, m_StuckSurfaceCollider);
            PulseAudioService.PlayPulse(pulseOrigin);
            PulseSpawned?.Invoke(pulseOrigin, m_PulseRadius, m_StuckSurfaceCollider);
            PulseSpawnedByBall?.Invoke(gameObject, pulseOrigin, m_PulseRadius, m_StuckSurfaceCollider);
        }

        void ApplyStuckPose()
        {
            Vector3 basePosition;
            Quaternion baseRotation;
            if (m_StuckAnchorTransform != null)
            {
                basePosition = m_StuckAnchorTransform.TransformPoint(m_StuckLocalPosition);
                baseRotation = m_StuckAnchorTransform.rotation * m_StuckLocalRotation;
            }
            else
            {
                basePosition = m_StuckLocalPosition;
                baseRotation = m_StuckLocalRotation;
            }

            if (!m_IsAttachLocking)
            {
                transform.SetPositionAndRotation(basePosition, baseRotation);
                return;
            }

            float settle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(m_AttachLockT));
            float outwardOffset = Mathf.Lerp(m_AttachSettleOutwardOffset, 0f, settle);
            float wobble = Mathf.Sin(m_AttachLockT * Mathf.Max(0f, m_AttachSettleWobbleFrequency)) * (1f - settle) * m_AttachSettleWobbleDegrees;
            Vector3 normal = baseRotation * Vector3.up;
            Vector3 tangent = baseRotation * Vector3.right;
            Quaternion wobbleRotation = Quaternion.AngleAxis(wobble, tangent);
            transform.SetPositionAndRotation(basePosition + normal * outwardOffset, wobbleRotation * baseRotation);
        }

        Vector3 ResolveSurfaceNormalFromCurrentAnchor()
        {
            Quaternion rotation = m_StuckAnchorTransform != null
                ? m_StuckAnchorTransform.rotation * m_StuckLocalRotation
                : m_StuckLocalRotation;
            Vector3 surfaceNormal = rotation * Vector3.up;
            return surfaceNormal.sqrMagnitude < 0.0001f ? Vector3.up : surfaceNormal.normalized;
        }

        Vector3 ResolvePulseOriginFromCurrentAnchor()
        {
            Vector3 basePosition = m_StuckAnchorTransform != null
                ? m_StuckAnchorTransform.TransformPoint(m_StuckLocalPosition)
                : m_StuckLocalPosition;
            return basePosition - ResolveSurfaceNormalFromCurrentAnchor() * m_StickSurfaceOffset;
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

        public void ApplyPersistentExtraPulseCount(int extraPulseCount)
        {
            m_PersistentExtraPulseCount = Mathf.Max(0, extraPulseCount);
        }

        static int ResolveProfilePulseRadiusBonusPercent()
        {
            return ProfileService.TryGetCurrentProfile(out ProfileSaveData profile) && profile != null
                ? ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.PulseRadiusBonusPercent)
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
