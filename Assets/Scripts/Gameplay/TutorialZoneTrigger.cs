using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class TutorialZoneTrigger : MonoBehaviour
    {
        [SerializeField] string m_ZoneId;
        [SerializeField] TutorialLevelController m_Controller;
        [SerializeField] bool m_OneShot = true;

        bool m_HasTriggered;
        Collider m_Collider;

        void Reset()
        {
            ResolveController();
            EnsureTriggerCollider();
        }

        void Awake()
        {
            ResolveController();
            EnsureTriggerCollider();
        }

        void EnsureTriggerCollider()
        {
            if (m_Collider == null)
                m_Collider = GetComponent<Collider>();

            if (m_Collider != null)
                m_Collider.isTrigger = true;
        }

        void ResolveController()
        {
            if (m_Controller == null)
                m_Controller = GetComponentInParent<TutorialLevelController>();
        }

        void OnTriggerEnter(Collider other)
        {
            if ((m_OneShot && m_HasTriggered) || !IsPlayerBody(other))
                return;

            m_HasTriggered = true;
            m_Controller?.HandleZoneTriggered(m_ZoneId);
        }

        bool IsPlayerBody(Collider other)
        {
            return other != null && other.GetComponent<CharacterController>() != null;
        }
    }
}
