using System.Collections.Generic;
using System.Reflection;
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
        static readonly MethodInfo s_ResetThrowSmoothingMethod =
            typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable).GetMethod("ResetThrowSmoothing", BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField] BallImpactEffect m_ImpactEffect;
        [SerializeField] LayerMask m_ValidGroundLayers = ~0;
        [SerializeField] float m_MinGroundUpDot = 0.6f;
        [SerializeField] bool m_RequireGroundContact = true;
        [SerializeField] bool m_AllowOnlyAfterThrow = true;
        [SerializeField] bool m_DestroyOnImpact = true;
        [SerializeField] float m_DestroyDelay = 0f;
        [SerializeField] bool m_IgnoreHolsterCollisionsOnThrow = true;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable m_GrabInteractable;
        Rigidbody m_Rigidbody;
        Collider[] m_Colliders;
        XRBaseInteractable.MovementType m_DefaultMovementType;
        bool m_WasThrown;
        bool m_Consumed;

        void Awake()
        {
            m_GrabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Colliders = GetComponentsInChildren<Collider>(true);
            m_DefaultMovementType = m_GrabInteractable.movementType;

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

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            m_Consumed = false;
            m_WasThrown = false;

            if (args.interactorObject is XRSocketInteractor)
            {
                SetMovementType(XRBaseInteractable.MovementType.Kinematic);
                ClearHeldMomentum();
                return;
            }

            SetMovementType(m_DefaultMovementType);

            if (IsSelectedBySocket(args.interactorObject))
                ClearHeldMomentum();
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (m_GrabInteractable.isSelected || args.interactorObject is XRSocketInteractor)
                return;

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

        bool IsSelectedBySocket(IXRSelectInteractor excludingInteractor)
        {
            List<IXRSelectInteractor> interactorsSelecting = m_GrabInteractable.interactorsSelecting;
            for (int i = 0; i < interactorsSelecting.Count; i++)
            {
                IXRSelectInteractor interactor = interactorsSelecting[i];
                if (ReferenceEquals(interactor, excludingInteractor))
                    continue;

                if (interactor is XRSocketInteractor)
                    return true;
            }

            return false;
        }

        void SetMovementType(XRBaseInteractable.MovementType movementType)
        {
            if (m_GrabInteractable != null && m_GrabInteractable.movementType != movementType)
                m_GrabInteractable.movementType = movementType;
        }

        void ClearHeldMomentum()
        {
            if (m_Rigidbody != null)
            {
                if (m_Rigidbody.isKinematic)
                {
                    m_Rigidbody.Sleep();
                }
                else
                {
#if UNITY_2023_3_OR_NEWER
                    m_Rigidbody.linearVelocity = Vector3.zero;
#else
                    m_Rigidbody.velocity = Vector3.zero;
#endif
                    m_Rigidbody.angularVelocity = Vector3.zero;
                }
            }

            s_ResetThrowSmoothingMethod?.Invoke(m_GrabInteractable, null);
        }
    }
}
