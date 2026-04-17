using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public class PlayerFallbackVisionAura : MonoBehaviour
    {
        static readonly int s_PlayerAuraPositionId = Shader.PropertyToID("_PlayerAuraPosition");
        static readonly int s_PlayerAuraParamsId = Shader.PropertyToID("_PlayerAuraParams");

        [SerializeField] bool m_EnableAura = true;
        [SerializeField] bool m_OnlyWhenPulseUnavailable = false;
        [SerializeField] PlayerEnergy m_PlayerEnergy;
        [SerializeField] Transform m_ViewTransform;
        [SerializeField] Vector3 m_LocalOffset = new(0f, -0.55f, 0.15f);
        [SerializeField] float m_Radius = 2.25f;
        [SerializeField, Range(0f, 1f)] float m_Intensity = 0.14f;
        [SerializeField] float m_FalloffPower = 2.5f;
        [SerializeField] int m_PulseAvailabilityEnergyThreshold = 10;

        void Awake()
        {
            ResolveReferences();
        }

        void OnEnable()
        {
            ResolveReferences();
            ApplyAura();
        }

        void OnValidate()
        {
            ResolveReferences();

            m_Radius = Mathf.Max(0.1f, m_Radius);
            m_FalloffPower = Mathf.Max(0.01f, m_FalloffPower);
            m_PulseAvailabilityEnergyThreshold = Mathf.Max(0, m_PulseAvailabilityEnergyThreshold);

            ApplyAura();
        }

        void LateUpdate()
        {
            ApplyAura();
        }

        void OnDisable()
        {
            ClearAura();
        }

        void OnDestroy()
        {
            ClearAura();
        }

        void ResolveReferences()
        {
            if (m_PlayerEnergy == null)
                m_PlayerEnergy = GetComponent<PlayerEnergy>() ?? FindObjectOfType<PlayerEnergy>();

            if (m_ViewTransform == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                    m_ViewTransform = mainCamera.transform;
            }
        }

        void ApplyAura()
        {
            if (!Application.isPlaying)
            {
                ClearAura();
                return;
            }

            ResolveReferences();

            if (!ShouldEnableAura())
            {
                ClearAura();
                return;
            }

            Transform auraTransform = m_ViewTransform != null ? m_ViewTransform : transform;
            Vector3 worldPosition = auraTransform.TransformPoint(m_LocalOffset);

            Shader.SetGlobalVector(s_PlayerAuraPositionId, new Vector4(worldPosition.x, worldPosition.y, worldPosition.z, m_Radius));
            Shader.SetGlobalVector(s_PlayerAuraParamsId, new Vector4(m_Intensity, m_FalloffPower, 0f, 0f));
        }

        bool ShouldEnableAura()
        {
            if (!m_EnableAura)
                return false;

            if (!m_OnlyWhenPulseUnavailable)
                return true;

            if (m_PlayerEnergy == null)
                return true;

            return m_PlayerEnergy.CurrentEnergy < m_PulseAvailabilityEnergyThreshold;
        }

        void ClearAura()
        {
            Shader.SetGlobalVector(s_PlayerAuraPositionId, Vector4.zero);
            Shader.SetGlobalVector(s_PlayerAuraParamsId, Vector4.zero);
        }
    }
}
