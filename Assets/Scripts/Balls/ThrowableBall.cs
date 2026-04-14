using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class ThrowableBall : MonoBehaviour
    {
        [SerializeField] BallImpactEffect m_ImpactEffect;
        [SerializeField] LayerMask m_ValidGroundLayers = ~0;
        [SerializeField] float m_MinGroundUpDot = 0.6f;
        [SerializeField] bool m_AllowOnlyAfterThrow = true;
        [SerializeField] bool m_DestroyOnImpact = true;
        [SerializeField] float m_DestroyDelay = 0f;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable m_GrabInteractable;
        bool m_WasThrown;
        bool m_Consumed;

        void Awake()
        {
            m_GrabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

            if (m_ImpactEffect == null)
                m_ImpactEffect = GetComponent<BallImpactEffect>();
        }

        void OnEnable()
        {
            m_GrabInteractable.selectEntered.AddListener(OnSelectEntered);
            m_GrabInteractable.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            m_GrabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            m_GrabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        void OnSelectEntered(SelectEnterEventArgs _)
        {
            m_Consumed = false;
            m_WasThrown = false;
        }

        void OnSelectExited(SelectExitEventArgs _)
        {
            m_WasThrown = true;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (m_Consumed || m_ImpactEffect == null)
                return;

            if (m_AllowOnlyAfterThrow && !m_WasThrown)
                return;

            if (((1 << collision.gameObject.layer) & m_ValidGroundLayers.value) == 0)
                return;

            if (collision.contactCount <= 0)
                return;

            ContactPoint contact = collision.GetContact(0);
            if (Vector3.Dot(contact.normal, Vector3.up) < m_MinGroundUpDot)
                return;

            m_Consumed = true;

            var context = new BallImpactContext(contact.point, contact.normal, gameObject, collision);
            m_ImpactEffect.Apply(context);

            if (m_DestroyOnImpact)
                Destroy(gameObject, m_DestroyDelay);
        }
    }
}
