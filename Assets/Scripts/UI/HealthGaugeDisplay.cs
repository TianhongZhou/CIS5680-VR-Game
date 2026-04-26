using CIS5680VRGame.Gameplay;
using UnityEngine;

namespace CIS5680VRGame.UI
{
    [RequireComponent(typeof(MeshRenderer))]
    public class HealthGaugeDisplay : MonoBehaviour
    {
        static readonly int s_FillAmountId = Shader.PropertyToID("_FillAmount");
        static readonly int s_FillColorId = Shader.PropertyToID("_FillColor");
        static readonly int s_BackgroundColorId = Shader.PropertyToID("_BackgroundColor");
        static readonly int s_TickValueMaxId = Shader.PropertyToID("_TickValueMax");
        const float k_DefaultTickValueMax = 100f;

        enum GaugeAnimationMode
        {
            Normal = 0,
            Damage = 1,
            Regen = 2,
            Heal = 3,
        }

        [SerializeField] PlayerHealth m_PlayerHealth;
        [SerializeField] MeshRenderer m_Renderer;
        [SerializeField] Color m_NormalFillColor = new(0.95f, 0.18f, 0.22f, 0.96f);
        [SerializeField] Color m_NormalBackgroundColor = new(0.16f, 0.04f, 0.05f, 0.48f);
        [SerializeField] Color m_LowHealthFillColorA = new(1f, 0.18f, 0.24f, 0.98f);
        [SerializeField] Color m_LowHealthFillColorB = new(1f, 0.72f, 0.74f, 1f);
        [SerializeField] Color m_LowHealthBackgroundColor = new(0.28f, 0.02f, 0.04f, 0.6f);
        [SerializeField] float m_LowHealthFlashFrequency = 3.6f;
        [SerializeField] Color m_HealFlashColorA = new(0.42f, 1f, 0.58f, 0.98f);
        [SerializeField] Color m_HealFlashColorB = new(1f, 1f, 1f, 0.98f);
        [SerializeField] Color m_HealBackgroundColor = new(0.08f, 0.22f, 0.12f, 0.58f);
        [SerializeField] float m_HealFlashFrequency = 7.2f;
        [SerializeField] float m_DamageSmoothTime = 0.1f;
        [SerializeField] float m_RegenSmoothTime = 0.22f;
        [SerializeField] float m_HealAnimationDuration = 0.38f;

        MaterialPropertyBlock m_PropertyBlock;
        float m_DisplayedFillAmount;
        float m_TargetFillAmount;
        float m_FillVelocity;
        GaugeAnimationMode m_AnimationMode;

        public void SetHealthSource(PlayerHealth health)
        {
            if (m_PlayerHealth == health)
                return;

            Unsubscribe();
            m_PlayerHealth = health;
            Subscribe();
            SyncDisplayedFillToTarget();
            UpdateVisuals();
        }

        void Awake()
        {
            if (m_Renderer == null)
                m_Renderer = GetComponent<MeshRenderer>();

            m_PropertyBlock = new MaterialPropertyBlock();
            ResolveHealth();
            SyncDisplayedFillToTarget();
        }

        void OnEnable()
        {
            ResolveHealth();
            Subscribe();
            SyncDisplayedFillToTarget();
            UpdateVisuals();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void Update()
        {
            bool needsVisualRefresh = StepDisplayedFill();

            if (needsVisualRefresh || IsHealVisualActive() || IsLowHealthWarningActive())
                UpdateVisuals();
        }

        void ResolveHealth()
        {
            if (m_PlayerHealth == null)
                m_PlayerHealth = FindObjectOfType<PlayerHealth>();
        }

        void Subscribe()
        {
            if (m_PlayerHealth != null)
                m_PlayerHealth.HealthChangedDetailed += OnHealthChanged;
        }

        void Unsubscribe()
        {
            if (m_PlayerHealth != null)
                m_PlayerHealth.HealthChangedDetailed -= OnHealthChanged;
        }

        void OnHealthChanged(HealthChangeContext context)
        {
            m_TargetFillAmount = context.MaxHealth <= 0 ? 0f : Mathf.Clamp01((float)context.CurrentHealth / context.MaxHealth);

            switch (context.Reason)
            {
                case HealthChangeReason.Damage:
                    m_AnimationMode = GaugeAnimationMode.Damage;
                    break;
                case HealthChangeReason.Regen:
                    m_AnimationMode = GaugeAnimationMode.Regen;
                    break;
                case HealthChangeReason.RefillStation:
                    m_AnimationMode = GaugeAnimationMode.Heal;
                    break;
                default:
                    m_AnimationMode = m_TargetFillAmount >= m_DisplayedFillAmount
                        ? GaugeAnimationMode.Regen
                        : GaugeAnimationMode.Damage;
                    break;
            }

            UpdateVisuals();
        }

        bool StepDisplayedFill()
        {
            float previousFill = m_DisplayedFillAmount;
            bool visualModeChanged = false;

            if (Mathf.Abs(m_DisplayedFillAmount - m_TargetFillAmount) <= 0.0005f)
            {
                m_DisplayedFillAmount = m_TargetFillAmount;
                m_FillVelocity = 0f;
                if (m_AnimationMode == GaugeAnimationMode.Heal)
                {
                    m_AnimationMode = GaugeAnimationMode.Normal;
                    visualModeChanged = true;
                }

                return visualModeChanged;
            }

            if (m_AnimationMode == GaugeAnimationMode.Heal)
            {
                float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, m_HealAnimationDuration);
                m_DisplayedFillAmount = Mathf.MoveTowards(m_DisplayedFillAmount, m_TargetFillAmount, step);
            }
            else
            {
                float smoothTime = m_TargetFillAmount < m_DisplayedFillAmount
                    ? Mathf.Max(0.03f, m_DamageSmoothTime)
                    : Mathf.Max(0.04f, m_RegenSmoothTime);

                m_DisplayedFillAmount = Mathf.SmoothDamp(
                    m_DisplayedFillAmount,
                    m_TargetFillAmount,
                    ref m_FillVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }

            if (Mathf.Abs(m_DisplayedFillAmount - m_TargetFillAmount) <= 0.001f)
            {
                m_DisplayedFillAmount = m_TargetFillAmount;
                m_FillVelocity = 0f;
                if (m_AnimationMode == GaugeAnimationMode.Heal)
                {
                    m_AnimationMode = GaugeAnimationMode.Normal;
                    visualModeChanged = true;
                }
            }

            return visualModeChanged || !Mathf.Approximately(previousFill, m_DisplayedFillAmount);
        }

        void UpdateVisuals()
        {
            if (m_Renderer == null)
                return;

            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            ResolveHealth();

            bool showHeal = IsHealVisualActive();
            bool showLowHealth = !showHeal && IsLowHealthWarningActive();
            Color fillColor = m_NormalFillColor;
            Color backgroundColor = m_NormalBackgroundColor;

            if (showHeal)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Max(0.01f, m_HealFlashFrequency) * Mathf.PI * 2f);
                fillColor = Color.Lerp(m_HealFlashColorA, m_HealFlashColorB, wave);
                backgroundColor = m_HealBackgroundColor;
            }
            else if (showLowHealth)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Max(0.01f, m_LowHealthFlashFrequency) * Mathf.PI * 2f);
                fillColor = Color.Lerp(m_LowHealthFillColorA, m_LowHealthFillColorB, wave);
                backgroundColor = Color.Lerp(m_NormalBackgroundColor, m_LowHealthBackgroundColor, wave);
            }

            m_Renderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetFloat(s_FillAmountId, m_DisplayedFillAmount);
            m_PropertyBlock.SetColor(s_FillColorId, fillColor);
            m_PropertyBlock.SetColor(s_BackgroundColorId, backgroundColor);
            m_PropertyBlock.SetFloat(s_TickValueMaxId, m_PlayerHealth != null ? m_PlayerHealth.MaxHealth : k_DefaultTickValueMax);
            m_Renderer.SetPropertyBlock(m_PropertyBlock);
        }

        bool IsHealVisualActive()
        {
            return m_AnimationMode == GaugeAnimationMode.Heal;
        }

        bool IsLowHealthWarningActive()
        {
            return m_PlayerHealth != null
                && !m_PlayerHealth.IsDead
                && m_PlayerHealth.CurrentHealth > 0
                && m_PlayerHealth.CurrentHealth < m_PlayerHealth.LowHealthThreshold;
        }

        void SyncDisplayedFillToTarget()
        {
            m_TargetFillAmount = m_PlayerHealth != null ? m_PlayerHealth.NormalizedHealth : 0f;
            m_DisplayedFillAmount = m_TargetFillAmount;
            m_FillVelocity = 0f;
            m_AnimationMode = GaugeAnimationMode.Normal;
        }
    }
}
