using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public class EnemyDangerBeacon : MonoBehaviour
    {
        static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int s_ColorId = Shader.PropertyToID("_Color");
        static readonly int s_EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Renderer[] m_TargetRenderers;
        [SerializeField] Light[] m_TargetLights;
        [SerializeField] Color m_BeaconColor = new(1f, 0.08f, 0.03f, 1f);
        [SerializeField] float m_BaseEmission = 5f;
        [SerializeField] float m_EmissionPulseAmplitude = 2.25f;
        [SerializeField] float m_BaseLightIntensity = 1.25f;
        [SerializeField] float m_LightPulseAmplitude = 0.75f;
        [SerializeField] float m_PulseSpeed = 4.2f;

        MaterialPropertyBlock m_PropertyBlock;

        void Awake()
        {
            ResolveTargets();
            Apply(0.5f);
        }

        void OnEnable()
        {
            ResolveTargets();
            Apply(0.5f);
        }

        void OnValidate()
        {
            ResolveTargets();
            Apply(0.5f);
        }

        void Update()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, m_PulseSpeed));
            Apply(pulse);
        }

        void ResolveTargets()
        {
            if (m_TargetRenderers == null || m_TargetRenderers.Length == 0)
                m_TargetRenderers = GetComponentsInChildren<Renderer>(true);

            if (m_TargetLights == null || m_TargetLights.Length == 0)
                m_TargetLights = GetComponentsInChildren<Light>(true);
        }

        void Apply(float pulse)
        {
            float emissionStrength = Mathf.Max(0f, m_BaseEmission + m_EmissionPulseAmplitude * pulse);
            Color emissionColor = m_BeaconColor * emissionStrength;
            Color baseColor = Color.Lerp(m_BeaconColor * 0.28f, m_BeaconColor * 0.45f, pulse);

            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            if (m_TargetRenderers != null)
            {
                for (int i = 0; i < m_TargetRenderers.Length; i++)
                {
                    Renderer targetRenderer = m_TargetRenderers[i];
                    if (targetRenderer == null)
                        continue;

                    targetRenderer.GetPropertyBlock(m_PropertyBlock);
                    m_PropertyBlock.SetColor(s_BaseColorId, baseColor);
                    m_PropertyBlock.SetColor(s_ColorId, baseColor);
                    m_PropertyBlock.SetColor(s_EmissionColorId, emissionColor);
                    targetRenderer.SetPropertyBlock(m_PropertyBlock);
                }
            }

            if (m_TargetLights == null)
                return;

            float lightIntensity = Mathf.Max(0f, m_BaseLightIntensity + m_LightPulseAmplitude * pulse);
            for (int i = 0; i < m_TargetLights.Length; i++)
            {
                Light targetLight = m_TargetLights[i];
                if (targetLight == null)
                    continue;

                targetLight.color = m_BeaconColor;
                targetLight.intensity = lightIntensity;
            }
        }
    }
}
