using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class EnemyContactDamage : MonoBehaviour
    {
        static float s_GlobalDamageBonusPercent;

        [SerializeField] PlayerHealth m_PlayerHealth;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] float m_DamagePerHit = 50f;
        [SerializeField] float m_RepeatHitCooldown = 2.5f;

        Collider m_Trigger;
        readonly Dictionary<int, float> m_TargetCooldowns = new();

        public static void SetGlobalDamageBonusPercent(float bonusPercent)
        {
            s_GlobalDamageBonusPercent = Mathf.Max(0f, bonusPercent);
        }

        void Reset()
        {
            ConfigureTrigger();
        }

        void Awake()
        {
            ConfigureTrigger();
            ResolvePlayerReferences();
        }

        void OnValidate()
        {
            ConfigureTrigger();
        }

        void OnTriggerEnter(Collider other)
        {
            TryApplyContactDamage(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryApplyContactDamage(other);
        }

        void ConfigureTrigger()
        {
            if (m_Trigger == null)
                m_Trigger = GetComponent<Collider>();

            if (m_Trigger != null)
                m_Trigger.isTrigger = true;
        }

        void ResolvePlayerReferences()
        {
            if (m_PlayerHealth == null)
                m_PlayerHealth = FindObjectOfType<PlayerHealth>();

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();
        }

        void TryApplyContactDamage(Collider other)
        {
            if (!TryResolveTarget(other, out PlayerHealth health))
                return;

            int targetId = health.GetInstanceID();
            float currentTime = Time.time;
            if (m_TargetCooldowns.TryGetValue(targetId, out float nextAllowedDamageTime) && currentTime < nextAllowedDamageTime)
                return;

            float effectiveDamage = Mathf.Max(0f, m_DamagePerHit) * (1f + s_GlobalDamageBonusPercent * 0.01f);
            if (!health.ApplyDamage(effectiveDamage))
                return;

            m_TargetCooldowns[targetId] = currentTime + Mathf.Max(0f, m_RepeatHitCooldown);
        }

        bool TryResolveTarget(Collider other, out PlayerHealth health)
        {
            health = null;
            if (other == null)
                return false;

            CharacterController characterController = other.GetComponent<CharacterController>();
            if (characterController == null)
                characterController = other.GetComponentInParent<CharacterController>();

            if (characterController == null)
                return false;

            XROrigin rig = characterController.GetComponent<XROrigin>();
            if (rig == null)
                rig = characterController.GetComponentInParent<XROrigin>();

            if (m_PlayerRig != null && rig != null && rig != m_PlayerRig)
                return false;

            health = characterController.GetComponent<PlayerHealth>();
            if (health == null)
                health = characterController.GetComponentInParent<PlayerHealth>();

            if (health == null)
            {
                ResolvePlayerReferences();
                health = m_PlayerHealth;
            }

            if (health == null || health.IsDead)
                return false;

            if (m_PlayerHealth == null)
                m_PlayerHealth = health;

            if (m_PlayerRig == null && rig != null)
                m_PlayerRig = rig;

            return true;
        }
    }
}
