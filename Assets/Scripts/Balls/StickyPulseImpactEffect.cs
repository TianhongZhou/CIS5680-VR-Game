using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using CIS5680VRGame.Gameplay;

namespace CIS5680VRGame.Balls
{
    public class StickyPulseImpactEffect : BallImpactEffect
    {
        [SerializeField] PulseManager m_PulseManager;
        [SerializeField] float m_PulseRadius = 15f;
        [SerializeField] float m_PulseInterval = 8f;
        [SerializeField] int m_PulseCount = 3;
        [SerializeField] float m_StickSurfaceOffset = 0.02f;
        [SerializeField] float m_DestroyDelayAfterLastPulse = 0.75f;

        Rigidbody m_Rigidbody;
        Collider[] m_Colliders;
        XRGrabInteractable m_GrabInteractable;
        BallHoverInfoDisplay m_HoverInfoDisplay;
        Coroutine m_PulseRoutine;
        bool m_HasStuck;
        Collider m_StuckSurfaceCollider;

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Colliders = GetComponents<Collider>();
            m_GrabInteractable = GetComponent<XRGrabInteractable>();
            m_HoverInfoDisplay = GetComponent<BallHoverInfoDisplay>();

            if (m_PulseManager == null)
                m_PulseManager = FindObjectOfType<PulseManager>();
        }

        void OnDisable()
        {
            if (m_PulseRoutine != null)
            {
                StopCoroutine(m_PulseRoutine);
                m_PulseRoutine = null;
            }
        }

        public override void Apply(in BallImpactContext context)
        {
            if (m_HasStuck)
                return;

            if (m_PulseManager == null)
                m_PulseManager = FindObjectOfType<PulseManager>();

            m_HasStuck = true;
            m_StuckSurfaceCollider = context.Collision.collider;
            StickToSurface(context);
            m_PulseRoutine = StartCoroutine(EmitPulses());
        }

        void StickToSurface(in BallImpactContext context)
        {
            Vector3 surfaceNormal = context.HitNormal.sqrMagnitude < 0.0001f
                ? Vector3.up
                : context.HitNormal.normalized;

            transform.up = surfaceNormal;
            transform.position = context.HitPoint + surfaceNormal * m_StickSurfaceOffset;

            Transform parent = context.Collision.collider != null ? context.Collision.collider.transform : context.Collision.transform;
            if (parent != null)
                transform.SetParent(parent, true);

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

        IEnumerator EmitPulses()
        {
            int pulseCount = Mathf.Max(1, m_PulseCount);
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

            Destroy(gameObject);
        }

        void SpawnPulseFromCurrentAnchor()
        {
            if (m_PulseManager == null)
                return;

            Vector3 surfaceNormal = transform.up.sqrMagnitude < 0.0001f
                ? Vector3.up
                : transform.up.normalized;

            Vector3 pulseOrigin = transform.position - surfaceNormal * m_StickSurfaceOffset;
            m_PulseManager.SpawnPulse(pulseOrigin, surfaceNormal, m_PulseRadius, m_StuckSurfaceCollider);
            PulseAudioService.PlayPulse(pulseOrigin);
        }
    }
}
