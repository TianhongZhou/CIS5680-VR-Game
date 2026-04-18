using System.Collections;
using UnityEngine;

namespace CIS5680VRGame.UI
{
    public class MainMenuStickyPulseBeacon : MonoBehaviour
    {
        [SerializeField] PulseManager m_PulseManager;
        [SerializeField] Collider m_PulseSourceCollider;
        [SerializeField] float m_PulseRadius = 14f;
        [SerializeField] float m_PulseInterval = 5f;
        [SerializeField] float m_RevealHoldDuration = 3f;
        [SerializeField] float m_SurfaceOffset = 0.04f;
        [SerializeField] bool m_FireOnEnable = true;

        Coroutine m_PulseRoutine;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();

            if (!Application.isPlaying)
                return;

            m_PulseRoutine = StartCoroutine(PulseLoop());
        }

        void OnDisable()
        {
            if (m_PulseRoutine == null)
                return;

            StopCoroutine(m_PulseRoutine);
            m_PulseRoutine = null;
        }

        void OnValidate()
        {
            ResolveReferences();
            m_PulseRadius = Mathf.Max(0.5f, m_PulseRadius);
            m_PulseInterval = Mathf.Max(0.25f, m_PulseInterval);
            m_RevealHoldDuration = Mathf.Max(0f, m_RevealHoldDuration);
            m_SurfaceOffset = Mathf.Max(0f, m_SurfaceOffset);
        }

        [ContextMenu("Emit Pulse")]
        public void EmitPulse()
        {
            ResolveReferences();

            if (m_PulseManager == null)
                return;

            Vector3 surfaceNormal = transform.up.sqrMagnitude < 0.0001f
                ? Vector3.up
                : transform.up.normalized;

            Vector3 pulseOrigin = transform.position - surfaceNormal * m_SurfaceOffset;
            float previousRevealHoldDuration = m_PulseManager.revealHoldDuration;
            m_PulseManager.revealHoldDuration = m_RevealHoldDuration;
            m_PulseManager.SpawnPulse(pulseOrigin, surfaceNormal, m_PulseRadius, m_PulseSourceCollider);
            m_PulseManager.revealHoldDuration = previousRevealHoldDuration;
        }

        IEnumerator PulseLoop()
        {
            if (m_FireOnEnable)
                EmitPulse();

            WaitForSeconds wait = new(Mathf.Max(0.25f, m_PulseInterval));

            while (enabled)
            {
                yield return wait;
                EmitPulse();
            }
        }

        void ResolveReferences()
        {
            if (m_PulseManager == null)
                m_PulseManager = PulseManager.Instance != null ? PulseManager.Instance : FindObjectOfType<PulseManager>();
        }
    }
}
