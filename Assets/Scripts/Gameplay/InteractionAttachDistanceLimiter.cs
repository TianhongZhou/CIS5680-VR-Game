using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace CIS5680VRGame.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionAttachController))]
    public class InteractionAttachDistanceLimiter : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] float m_MinDistanceFromController = 0.12f;
        [SerializeField] bool m_UseFarCasterDistanceAsMaxDistance = true;
        [SerializeField, Min(0.01f)] float m_FallbackMaxDistanceFromController = 2.5f;

        InteractionAttachController m_AttachController;
        XRBaseInteractor m_Interactor;
        CurveInteractionCaster m_CurveCaster;
        Transform m_AttachAnchor;

        void Awake()
        {
            CacheReferences();
        }

        void OnEnable()
        {
            CacheReferences();

            if (m_AttachController != null)
                m_AttachController.attachUpdated += ClampAttachDistance;
        }

        void OnDisable()
        {
            if (m_AttachController != null)
                m_AttachController.attachUpdated -= ClampAttachDistance;
        }

        void OnValidate()
        {
            m_MinDistanceFromController = Mathf.Max(0.01f, m_MinDistanceFromController);
            m_FallbackMaxDistanceFromController = Mathf.Max(m_MinDistanceFromController, m_FallbackMaxDistanceFromController);
        }

        void CacheReferences()
        {
            if (m_AttachController == null)
                m_AttachController = GetComponent<InteractionAttachController>();

            if (m_Interactor == null)
                m_Interactor = GetComponent<XRBaseInteractor>();

            if (m_CurveCaster == null)
                m_CurveCaster = GetComponent<CurveInteractionCaster>();
        }

        void ClampAttachDistance()
        {
            if (m_AttachController == null || m_Interactor == null || !m_Interactor.hasSelection)
            {
                return;
            }

            if (m_AttachAnchor == null)
                m_AttachAnchor = ((IInteractionAttachController)m_AttachController).GetOrCreateAnchorTransform();

            if (m_AttachAnchor == null)
                return;

            Vector3 localOffset = m_AttachAnchor.localPosition;
            float currentDistance = localOffset.magnitude;
            float minDistance = m_MinDistanceFromController;
            float maxDistance = Mathf.Max(minDistance, ResolveMaxDistance());

            if (currentDistance <= Mathf.Epsilon)
            {
                m_AttachAnchor.localPosition = Vector3.forward * minDistance;
                return;
            }

            float clampedDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
            if (Mathf.Abs(clampedDistance - currentDistance) <= 0.0001f)
                return;

            m_AttachAnchor.localPosition = localOffset * (clampedDistance / currentDistance);
        }

        float ResolveMaxDistance()
        {
            CacheReferences();

            if (m_UseFarCasterDistanceAsMaxDistance && m_CurveCaster != null && m_CurveCaster.castDistance > 0f)
                return m_CurveCaster.castDistance;

            return m_FallbackMaxDistanceFromController;
        }
    }
}
