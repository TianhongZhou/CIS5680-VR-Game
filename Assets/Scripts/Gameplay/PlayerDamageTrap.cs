using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class PlayerDamageTrap : MonoBehaviour
    {
        [SerializeField] PlayerHealth m_PlayerHealth;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] float m_DamagePerSecond = 10f;
        [SerializeField] float m_TriggerWorldHeight = 1.35f;
        [SerializeField] float m_TriggerWorldCenterY = 0.68f;

        Collider m_Trigger;

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
    }
}
