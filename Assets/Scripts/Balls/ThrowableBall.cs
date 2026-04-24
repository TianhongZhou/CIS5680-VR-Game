using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using CIS5680VRGame.Gameplay;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    public class ThrowableBall : MonoBehaviour
    {
        public static event Action<BallType, Vector3, Vector3, GameObject> ImpactOccurred;

        static readonly MethodInfo s_ResetThrowSmoothingMethod =
            typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable).GetMethod("ResetThrowSmoothing", BindingFlags.Instance | BindingFlags.NonPublic);

        [SerializeField] BallImpactEffect m_ImpactEffect;
        [SerializeField] LayerMask m_ValidGroundLayers = ~0;
        [SerializeField] float m_MinGroundUpDot = 0.6f;
        [SerializeField] bool m_RequireGroundContact = true;
        [SerializeField] bool m_AllowOnlyAfterThrow = true;
        [SerializeField] bool m_DestroyOnImpact = true;
        [SerializeField] float m_DestroyDelay = 0f;
        [SerializeField] bool m_FadePulseBallsAfterImpact = true;
        [SerializeField] float m_PostPulseLingerDuration = 0.2f;
        [SerializeField] float m_PostPulseFadeDuration = 0.55f;
        [SerializeField] float m_MaxReleaseSpeed = 0f;
        [SerializeField] float m_MaxReleaseAngularSpeed = 0f;
        [SerializeField] bool m_IgnoreHolsterCollisionsOnThrow = true;
        [SerializeField] bool m_IgnorePlayerBodyCollisions = true;

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable m_GrabInteractable;
        Rigidbody m_Rigidbody;
        Collider[] m_Colliders;
        XRBaseInteractable.MovementType m_DefaultMovementType;
        bool m_WasThrown;
        bool m_Consumed;

        public LayerMask ValidGroundLayers => m_ValidGroundLayers;
        public float MinGroundUpDot => m_MinGroundUpDot;
        public bool RequireGroundContact => m_RequireGroundContact;
        public float CollisionRadius => ResolveApproximateCollisionRadius();

        void Awake()
        {
            m_GrabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Colliders = GetComponentsInChildren<Collider>(true);
            m_DefaultMovementType = m_GrabInteractable.movementType;

            if (m_ImpactEffect == null)
                m_ImpactEffect = GetComponent<BallImpactEffect>();

            if (SupportsEnemyImpact() && m_Rigidbody.collisionDetectionMode == CollisionDetectionMode.Discrete)
                m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        void OnEnable()
        {
            m_GrabInteractable.selectEntered.AddListener(OnSelectEntered);
            m_GrabInteractable.selectExited.AddListener(OnSelectExited);
            IgnorePlayerBodyCollisions();
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

            IgnorePlayerBodyCollisions();

            if (SupportsEnemyImpact())
                IgnoreEnemyBodyCollisions();

            ClampReleaseVelocity();
            StartCoroutine(ClampReleaseVelocityAfterDetach());
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!CanConsumeImpact())
                return;

            if (collision.contactCount <= 0)
                return;

            ContactPoint contact = collision.GetContact(0);

            bool allowEnemyImpact = SupportsEnemyImpact() && EnemyPatrolController.TryGetPulseAttachSurfaceOwner(collision.collider, out _);
            bool allowSpecialLandingSurface = IsSpecialLandingSurface(collision.collider);
            bool validGroundImpact = ((1 << collision.collider.gameObject.layer) & m_ValidGroundLayers.value) != 0;

            if (!allowEnemyImpact && !allowSpecialLandingSurface && !validGroundImpact)
                return;

            if (!allowEnemyImpact && m_RequireGroundContact && Vector3.Dot(contact.normal, Vector3.up) < m_MinGroundUpDot)
                return;

            ConsumeImpact(new BallImpactContext(contact.point, contact.normal, gameObject, collision));
        }

        void OnTriggerEnter(Collider other)
        {
            TryConsumeTriggerImpact(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryConsumeTriggerImpact(other);
        }

        bool TryConsumeTriggerImpact(Collider other)
        {
            if (!CanConsumeImpact())
                return false;

            bool allowEnemyImpact = SupportsEnemyImpact() && EnemyPatrolController.TryGetPulseAttachSurfaceOwner(other, out _);
            bool allowSpecialLandingSurface = BallLandingSurface.TryGet(other, out BallLandingSurface landingSurface);

            if (!allowEnemyImpact && !allowSpecialLandingSurface)
                return false;

            Collider hitCollider = other;
            Vector3 hitPoint = allowSpecialLandingSurface
                ? landingSurface.ResolveHitPoint(transform.position)
                : ResolveClosestTriggerPoint(hitCollider, transform.position);
            Vector3 hitNormal = allowSpecialLandingSurface
                ? landingSurface.SurfaceNormal
                : ResolveEnemyTriggerHitNormal(hitCollider, hitPoint);
            ConsumeImpact(new BallImpactContext(hitPoint, hitNormal, gameObject, hitCollider));
            return true;
        }

        internal static bool IsSpecialLandingSurface(Collider targetCollider)
        {
            return BallLandingSurface.TryGet(targetCollider, out _);
        }

        internal static Vector3 ResolveClosestTriggerPoint(Collider hitCollider, Vector3 fallbackPosition)
        {
            if (hitCollider == null)
                return fallbackPosition;

            Vector3 hitPoint = hitCollider.ClosestPoint(fallbackPosition);
            if ((hitPoint - fallbackPosition).sqrMagnitude < 0.000001f)
                hitPoint = fallbackPosition;

            return hitPoint;
        }

        Vector3 ResolveEnemyTriggerHitNormal(Collider hitCollider, Vector3 hitPoint)
        {
            if (m_Rigidbody != null)
            {
#if UNITY_2023_3_OR_NEWER
                Vector3 velocity = m_Rigidbody.linearVelocity;
#else
                Vector3 velocity = m_Rigidbody.velocity;
#endif
                if (velocity.sqrMagnitude > 0.0001f)
                    return -velocity.normalized;
            }

            Vector3 fromCenter = hitPoint - hitCollider.bounds.center;
            if (fromCenter.sqrMagnitude > 0.0001f)
                return fromCenter.normalized;

            return Vector3.up;
        }

        bool CanConsumeImpact()
        {
            if (m_Consumed || m_ImpactEffect == null)
                return false;

            if (m_AllowOnlyAfterThrow && !m_WasThrown)
                return false;

            return true;
        }

        void ConsumeImpact(in BallImpactContext context)
        {
            m_Consumed = true;

            ImpactOccurred?.Invoke(m_ImpactEffect.BallType, context.HitPoint, context.HitNormal, gameObject);
            m_ImpactEffect.Apply(context);

            if (TryGetComponent(out SonarExtraBounceProxy bounceProxy) && bounceProxy.IsWaitingForExtraBounce)
                return;

            if (!m_DestroyOnImpact)
                return;

            if (!m_ImpactEffect.ShouldDestroyBallAfterImpact(context))
                return;

            if (ShouldFadeAfterImpact())
            {
                float lingerDuration = Mathf.Max(m_DestroyDelay, m_PostPulseLingerDuration);
                BallFadeOut.Begin(gameObject, lingerDuration, m_PostPulseFadeDuration);
                return;
            }

            Destroy(gameObject, m_DestroyDelay);
        }

        public void LaunchFromAbility(Vector3 velocity, Vector3 angularVelocity)
        {
            m_Consumed = false;
            m_WasThrown = true;

            if (m_GrabInteractable != null)
            {
                SetMovementType(m_DefaultMovementType);
                m_GrabInteractable.enabled = false;
            }

            if (m_IgnoreHolsterCollisionsOnThrow)
                IgnoreHolsterCollisions();

            IgnorePlayerBodyCollisions();

            if (SupportsEnemyImpact())
                IgnoreEnemyBodyCollisions();

            if (m_Rigidbody == null)
                return;

            m_Rigidbody.isKinematic = false;
            m_Rigidbody.useGravity = true;
            m_Rigidbody.detectCollisions = true;
            m_Rigidbody.WakeUp();

#if UNITY_2023_3_OR_NEWER
            m_Rigidbody.linearVelocity = velocity;
#else
            m_Rigidbody.velocity = velocity;
#endif
            m_Rigidbody.angularVelocity = angularVelocity;
            ClampReleaseVelocity();
        }

        public void FreezeAsAnchor()
        {
            if (m_Rigidbody != null)
            {
#if UNITY_2023_3_OR_NEWER
                m_Rigidbody.linearVelocity = Vector3.zero;
#else
                m_Rigidbody.velocity = Vector3.zero;
#endif
                m_Rigidbody.angularVelocity = Vector3.zero;
                m_Rigidbody.useGravity = false;
                m_Rigidbody.detectCollisions = false;
                m_Rigidbody.isKinematic = true;
            }

            if (m_GrabInteractable != null)
                m_GrabInteractable.enabled = false;

            BallHoverInfoDisplay hoverInfoDisplay = GetComponent<BallHoverInfoDisplay>();
            if (hoverInfoDisplay != null)
                hoverInfoDisplay.enabled = false;
        }

        bool ShouldFadeAfterImpact()
        {
            if (!m_FadePulseBallsAfterImpact || m_ImpactEffect == null)
                return false;

            return m_ImpactEffect.BallType == BallType.Sonar || m_ImpactEffect.BallType == BallType.StickyPulse;
        }

        bool SupportsEnemyImpact()
        {
            return m_ImpactEffect != null
                && (m_ImpactEffect.BallType == BallType.Sonar || m_ImpactEffect.BallType == BallType.StickyPulse);
        }

        float ResolveApproximateCollisionRadius()
        {
            if (m_Colliders == null || m_Colliders.Length == 0)
                return 0.12f;

            float largestExtent = 0f;
            for (int i = 0; i < m_Colliders.Length; i++)
            {
                Collider targetCollider = m_Colliders[i];
                if (targetCollider == null)
                    continue;

                Bounds bounds = targetCollider.bounds;
                largestExtent = Mathf.Max(largestExtent, bounds.extents.x, bounds.extents.y, bounds.extents.z);
            }

            return Mathf.Clamp(largestExtent, 0.05f, 0.5f);
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

        void IgnoreEnemyBodyCollisions()
        {
            if (m_Colliders == null || m_Colliders.Length == 0)
                return;

            var ignoredColliders = new HashSet<Collider>();
            EnemyPatrolController[] enemies = FindObjectsOfType<EnemyPatrolController>(true);
            for (int i = 0; i < enemies.Length; i++)
            {
                Collider[] enemyColliders = enemies[i].GetComponentsInChildren<Collider>(true);
                for (int j = 0; j < enemyColliders.Length; j++)
                {
                    Collider enemyCollider = enemyColliders[j];
                    if (enemyCollider == null || EnemyPatrolController.TryGetPulseAttachSurfaceOwner(enemyCollider, out _))
                        continue;

                    if (!ignoredColliders.Add(enemyCollider))
                        continue;

                    for (int k = 0; k < m_Colliders.Length; k++)
                    {
                        Collider selfCollider = m_Colliders[k];
                        if (selfCollider == null || selfCollider == enemyCollider)
                            continue;

                        Physics.IgnoreCollision(selfCollider, enemyCollider, true);
                    }
                }
            }
        }

        void IgnorePlayerBodyCollisions()
        {
            if (!m_IgnorePlayerBodyCollisions || m_Colliders == null || m_Colliders.Length == 0)
                return;

            var ignoredColliders = new HashSet<Collider>();
            CharacterController[] characterControllers = FindObjectsOfType<CharacterController>(true);
            for (int i = 0; i < characterControllers.Length; i++)
            {
                CharacterController characterController = characterControllers[i];
                if (characterController == null)
                    continue;

                IgnoreCollider(characterController, ignoredColliders);
            }
        }

        void IgnoreColliders(Collider[] targetColliders, HashSet<Collider> ignoredColliders)
        {
            if (targetColliders == null)
                return;

            for (int i = 0; i < targetColliders.Length; i++)
            {
                IgnoreCollider(targetColliders[i], ignoredColliders);
            }
        }

        void IgnoreCollider(Collider targetCollider, HashSet<Collider> ignoredColliders)
        {
            if (targetCollider == null || !ignoredColliders.Add(targetCollider))
                return;

            for (int i = 0; i < m_Colliders.Length; i++)
            {
                Collider selfCollider = m_Colliders[i];
                if (selfCollider == null || selfCollider == targetCollider)
                    continue;

                Physics.IgnoreCollision(selfCollider, targetCollider, true);
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

        IEnumerator ClampReleaseVelocityAfterDetach()
        {
            yield return null;
            ClampReleaseVelocity();
            yield return new WaitForFixedUpdate();
            ClampReleaseVelocity();
        }

        void ClampReleaseVelocity()
        {
            if (m_Rigidbody == null)
                return;

            if (m_MaxReleaseSpeed > 0f)
            {
#if UNITY_2023_3_OR_NEWER
                Vector3 velocity = m_Rigidbody.linearVelocity;
#else
                Vector3 velocity = m_Rigidbody.velocity;
#endif
                float speed = velocity.magnitude;
                if (speed > m_MaxReleaseSpeed)
                {
                    velocity = velocity.normalized * m_MaxReleaseSpeed;
#if UNITY_2023_3_OR_NEWER
                    m_Rigidbody.linearVelocity = velocity;
#else
                    m_Rigidbody.velocity = velocity;
#endif
                }
            }

            if (m_MaxReleaseAngularSpeed > 0f && m_Rigidbody.angularVelocity.magnitude > m_MaxReleaseAngularSpeed)
                m_Rigidbody.angularVelocity = m_Rigidbody.angularVelocity.normalized * m_MaxReleaseAngularSpeed;
        }
    }
}
