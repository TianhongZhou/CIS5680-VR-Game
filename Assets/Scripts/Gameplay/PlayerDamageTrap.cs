using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class PlayerDamageTrap : MonoBehaviour
    {
        static readonly HashSet<PlayerDamageTrap> s_ActiveTraps = new();

        [SerializeField] PlayerHealth m_PlayerHealth;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] float m_DamagePerSecond = 10f;
        [SerializeField] float m_TriggerWorldHeight = 1.35f;
        [SerializeField] float m_TriggerWorldCenterY = 0.68f;
        [SerializeField, Range(0f, 1f)] float m_AmbienceVolume = 0.65f;
        [SerializeField] float m_AmbienceActivationDistance = 3f;
        [SerializeField] float m_AmbienceSmoothTime = 0.28f;

        Collider m_Trigger;

        public static bool HasAudibleTrapNearby(Transform listenerTransform)
        {
            return TryGetAmbientTarget(listenerTransform, out _, out _);
        }

        public static bool TryGetAmbientTarget(Transform listenerTransform, out float targetVolume, out float smoothTime)
        {
            targetVolume = 0f;
            smoothTime = 0.25f;
            if (listenerTransform == null)
                return false;

            PlayerDamageTrap bestTrap = null;
            float bestDistance = float.PositiveInfinity;
            foreach (PlayerDamageTrap trap in s_ActiveTraps)
            {
                if (trap == null || !trap.isActiveAndEnabled)
                    continue;

                float candidateVolume = trap.ResolveTargetAmbienceVolume(listenerTransform, out float distance);
                if (candidateVolume <= 0.001f)
                    continue;

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestTrap = trap;
                targetVolume = candidateVolume;
            }

            if (bestTrap == null)
                return false;

            smoothTime = Mathf.Max(0.01f, bestTrap.m_AmbienceSmoothTime);
            return true;
        }

        void Awake()
        {
            m_Trigger = GetComponent<Collider>();
            m_Trigger.isTrigger = true;
            ConfigureTriggerVolume();

            if (m_PlayerHealth == null)
                m_PlayerHealth = FindObjectOfType<PlayerHealth>();

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();
        }

        void OnEnable()
        {
            s_ActiveTraps.Add(this);
        }

        void OnDisable()
        {
            s_ActiveTraps.Remove(this);
        }

        void OnTriggerStay(Collider other)
        {
            if (!CanAffect(other) || m_PlayerHealth == null)
                return;

            m_PlayerHealth.ApplyDamage(Mathf.Max(0f, m_DamagePerSecond) * Time.deltaTime);
        }

        bool CanAffect(Collider other)
        {
            if (other == null || other.GetComponent<CharacterController>() == null)
                return false;

            XROrigin rig = other.GetComponent<XROrigin>();
            if (rig == null)
                rig = other.GetComponentInParent<XROrigin>();

            return rig != null && (m_PlayerRig == null || rig == m_PlayerRig);
        }

        void ConfigureTriggerVolume()
        {
            if (m_Trigger is not BoxCollider boxCollider)
                return;

            Vector3 lossyScale = transform.lossyScale;
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(lossyScale.y));
            float desiredLocalHeight = Mathf.Max(0.01f, m_TriggerWorldHeight) / scaleY;
            float desiredLocalCenterY = m_TriggerWorldCenterY / scaleY;

            Vector3 size = boxCollider.size;
            size.y = desiredLocalHeight;
            boxCollider.size = size;

            Vector3 center = boxCollider.center;
            center.y = desiredLocalCenterY;
            boxCollider.center = center;
        }

        float ResolveTargetAmbienceVolume(Transform listenerTransform, out float distance)
        {
            distance = float.PositiveInfinity;
            if (listenerTransform == null)
                return 0f;

            float activationDistance = Mathf.Max(0.1f, m_AmbienceActivationDistance);
            distance = Vector3.Distance(listenerTransform.position, ResolveAudioAnchorPosition(listenerTransform));
            if (distance >= activationDistance)
                return 0f;

            float t = 1f - Mathf.Clamp01(distance / activationDistance);
            t = Mathf.SmoothStep(0f, 1f, t);
            return Mathf.Lerp(0f, Mathf.Clamp01(m_AmbienceVolume), t);
        }

        Vector3 ResolveAudioAnchorPosition(Transform listenerTransform)
        {
            if (listenerTransform == null || m_Trigger == null)
                return transform.position;

            return m_Trigger.ClosestPoint(listenerTransform.position);
        }
    }
}
