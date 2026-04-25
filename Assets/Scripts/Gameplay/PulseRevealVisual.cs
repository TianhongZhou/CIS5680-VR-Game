using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [ExecuteAlways]
    public class PulseRevealVisual : MonoBehaviour
    {
        static readonly int s_PulseColorId = Shader.PropertyToID("_PulseColor");
        static readonly int s_BackgroundColorId = Shader.PropertyToID("_BgColor");
        static readonly int s_EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
        static readonly int s_GridDensityId = Shader.PropertyToID("_GridDensity");
        static readonly int s_GridLineWidthId = Shader.PropertyToID("_GridLineWidth");
        static readonly int s_BandWidthId = Shader.PropertyToID("_BandWidth");
        static readonly int s_RevealFillStrengthId = Shader.PropertyToID("_RevealFillStrength");

        [SerializeField] Renderer[] m_TargetRenderers;
        [SerializeField] Color m_PulseColor = new(1f, 0.2f, 0.25f, 1f);
        [SerializeField] Color m_BackgroundColor = Color.black;
        [SerializeField] float m_EmissionStrength = 2.4f;
        [SerializeField] float m_GridDensity = 2f;
        [SerializeField] float m_GridLineWidth = 0.05f;
        [SerializeField] float m_BandWidth = 0.6f;
        [SerializeField] float m_RevealFillStrength = 0.4f;

        MaterialPropertyBlock m_PropertyBlock;

        public void SetVisual(Color backgroundColor, Color pulseColor, float emissionStrength)
        {
            m_BackgroundColor = backgroundColor;
            m_PulseColor = pulseColor;
            m_EmissionStrength = emissionStrength;
            Apply();
        }

        public void RefreshTargets()
        {
            m_TargetRenderers = null;
            ResolveRenderers();
            Apply();
        }

        void Awake()
        {
            ResolveRenderers();
            Apply();
        }

        void OnEnable()
        {
            ResolveRenderers();
            Apply();
        }

        void OnValidate()
        {
            ResolveRenderers();
            Apply();
        }

        void ResolveRenderers()
        {
            if (m_TargetRenderers == null || m_TargetRenderers.Length == 0)
                m_TargetRenderers = GetComponentsInChildren<Renderer>(true);
        }

        void Apply()
        {
            if (m_TargetRenderers == null || m_TargetRenderers.Length == 0)
                return;

            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            for (int i = 0; i < m_TargetRenderers.Length; i++)
            {
                Renderer targetRenderer = m_TargetRenderers[i];
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(s_PulseColorId, m_PulseColor);
                m_PropertyBlock.SetColor(s_BackgroundColorId, m_BackgroundColor);
                m_PropertyBlock.SetFloat(s_EmissionStrengthId, m_EmissionStrength);
                m_PropertyBlock.SetFloat(s_GridDensityId, m_GridDensity);
                m_PropertyBlock.SetFloat(s_GridLineWidthId, m_GridLineWidth);
                m_PropertyBlock.SetFloat(s_BandWidthId, m_BandWidth);
                m_PropertyBlock.SetFloat(s_RevealFillStrengthId, m_RevealFillStrength);
                targetRenderer.SetPropertyBlock(m_PropertyBlock);
            }
        }
    }
}
