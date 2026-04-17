using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class ThrowableBall : MonoBehaviour
    {
        [SerializeField] BallImpactEffect m_ImpactEffect;
        [SerializeField] LayerMask m_ValidGroundLayers = ~0;
        [SerializeField] float m_MinGroundUpDot = 0.6f;
        [SerializeField] bool m_RequireGroundContact = true;
        [SerializeField] bool m_AllowOnlyAfterThrow = true;
        [SerializeField] bool m_DestroyOnImpact = true;
        [SerializeField] float m_DestroyDelay = 0f;
        [SerializeField] bool m_IgnoreHolsterCollisionsOnThrow = true;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable m_GrabInteractable;
        Collider[] m_Colliders;
        bool m_WasThrown;
        bool m_Consumed;

        void Awake()
        {
            m_GrabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            m_Colliders = GetComponentsInChildren<Collider>(true);

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

            if (m_IgnoreHolsterCollisionsOnThrow)
                IgnoreHolsterCollisions();
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
            if (m_RequireGroundContact && Vector3.Dot(contact.normal, Vector3.up) < m_MinGroundUpDot)
                return;

            m_Consumed = true;

            var context = new BallImpactContext(contact.point, contact.normal, gameObject, collision);
            m_ImpactEffect.Apply(context);

            if (m_DestroyOnImpact)
                Destroy(gameObject, m_DestroyDelay);
        }

        void IgnoreHolsterCollisions()
        {
            if (m_Colliders == null || m_Colliders.Length == 0)
                return;

            var ignoredColliders = new HashSet<Collider>();

            var holsterSlots = FindObjectsOfType<BallHolsterSlot>(true);
            for (int i = 0; i < holsterSlots.Length; i++)
            {
                Collider[] holsterColliders = holsterSlots[i].GetComponentsInChildren<Collider>(true);
                IgnoreColliders(holsterColliders, ignoredColliders);
            }

            var sockets = FindObjectsOfType<XRSocketInteractor>(true);
            for (int i = 0; i < sockets.Length; i++)
            {
                foreach (IXRSelectInteractable selectedInteractable in sockets[i].interactablesSelected)
                {
                    if (selectedInteractable is not Component selectedComponent)
                        continue;

                    Collider[] selectedColliders = selectedComponent.GetComponentsInChildren<Collider>(true);
                    IgnoreColliders(selectedColliders, ignoredColliders);
                }
            }
        }

        void IgnoreColliders(Collider[] targetColliders, HashSet<Collider> ignoredColliders)
        {
            if (targetColliders == null)
                return;

            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider targetCollider = targetColliders[i];
                if (targetCollider == null || !ignoredColliders.Add(targetCollider))
                    continue;

                for (int j = 0; j < m_Colliders.Length; j++)
                {
                    Collider selfCollider = m_Colliders[j];
                    if (selfCollider == null || selfCollider == targetCollider)
                        continue;

                    Physics.IgnoreCollision(selfCollider, targetCollider, true);
                }
            }
        }
    }
}
