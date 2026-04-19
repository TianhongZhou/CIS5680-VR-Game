using System;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public enum HealthChangeReason
    {
        Initialize = 0,
        Damage = 1,
        Regen = 2,
        RefillStation = 3,
        DirectSet = 4,
    }

    public readonly struct HealthChangeContext
    {
        public HealthChangeContext(int previousHealth, int currentHealth, int maxHealth, HealthChangeReason reason)
        {
            PreviousHealth = previousHealth;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            Reason = reason;
        }

        public int PreviousHealth { get; }
        public int CurrentHealth { get; }
        public int MaxHealth { get; }
        public HealthChangeReason Reason { get; }
        public int Delta => CurrentHealth - PreviousHealth;
    }

    public class PlayerHealth : MonoBehaviour
    {
        static class SharedDefaults
        {
            public const int MaxHealth = 100;
            public const int StartingHealth = 100;
            public const int LowHealthThreshold = 30;
            public const int RegenCap = 30;
            public const int RegenAmount = 1;
            public const float RegenInterval = 1f;
            public const float RegenDelayAfterDamage = 10f;
        }

        [SerializeField] bool m_OverrideSharedDefaults;
        [SerializeField] int m_MaxHealth = SharedDefaults.MaxHealth;
        [SerializeField] int m_StartingHealth = SharedDefaults.StartingHealth;
        [SerializeField] int m_LowHealthThreshold = SharedDefaults.LowHealthThreshold;
        [SerializeField] int m_RegenCap = SharedDefaults.RegenCap;
        [SerializeField] int m_RegenAmount = SharedDefaults.RegenAmount;
        [SerializeField] float m_RegenInterval = SharedDefaults.RegenInterval;
        [SerializeField] float m_RegenDelayAfterDamage = SharedDefaults.RegenDelayAfterDamage;

        int m_CurrentHealth;
        float m_CurrentHealthValue;
        float m_RegenTimer;
        float m_TimeSinceLastDamage = float.PositiveInfinity;
        bool m_IsDead;
        int m_BaseMaxHealth;
        int m_BaseStartingHealth;
        int m_BaseLowHealthThreshold;
        int m_BaseRegenCap;
        float m_BaseRegenInterval;
        float m_BaseRegenDelayAfterDamage;
        int m_SingleRunLifeInsuranceCharges;

        public event Action<int, int> HealthChanged;
        public event Action<HealthChangeContext> HealthChangedDetailed;
        public event Action<float, HealthChangeReason> DamageApplied;
        public event Action Died;

        public int CurrentHealth => m_CurrentHealth;
        public int MaxHealth => Mathf.Max(1, m_MaxHealth);
        public int LowHealthThreshold => Mathf.Clamp(m_LowHealthThreshold, 1, MaxHealth);
        public int RegenCap => Mathf.Clamp(m_RegenCap, 0, MaxHealth);
        public float NormalizedHealth => Mathf.Clamp01(m_CurrentHealthValue / MaxHealth);
        public bool IsDead => m_IsDead;

        void Reset()
        {
            ApplySharedDefaults();
        }

        void OnValidate()
        {
            ApplySharedDefaults();
        }

        void Awake()
        {
            ApplySharedDefaults();

            m_MaxHealth = Mathf.Max(1, m_MaxHealth);
            m_LowHealthThreshold = Mathf.Clamp(m_LowHealthThreshold, 1, m_MaxHealth);
            m_RegenCap = Mathf.Clamp(m_RegenCap, 0, m_MaxHealth);
            m_RegenAmount = Mathf.Max(0, m_RegenAmount);
            m_RegenInterval = Mathf.Max(0.01f, m_RegenInterval);
            m_RegenDelayAfterDamage = Mathf.Max(0f, m_RegenDelayAfterDamage);
            m_BaseMaxHealth = m_MaxHealth;
            m_BaseStartingHealth = Mathf.Clamp(m_StartingHealth, 0, m_MaxHealth);
            m_BaseLowHealthThreshold = Mathf.Clamp(m_LowHealthThreshold, 1, m_MaxHealth);
            m_BaseRegenCap = Mathf.Clamp(m_RegenCap, 0, m_MaxHealth);
            m_BaseRegenInterval = Mathf.Max(0.01f, m_RegenInterval);
            m_BaseRegenDelayAfterDamage = Mathf.Max(0f, m_RegenDelayAfterDamage);

            m_CurrentHealthValue = Mathf.Clamp(m_StartingHealth, 0f, m_MaxHealth);
            m_CurrentHealth = Mathf.Clamp(Mathf.FloorToInt(m_CurrentHealthValue + 0.0001f), 0, m_MaxHealth);
            m_IsDead = m_CurrentHealth <= 0;
        }

        void Start()
        {
            NotifyHealthChanged(HealthChangeReason.Initialize, m_CurrentHealth);

            if (m_IsDead)
                Died?.Invoke();
        }

        void Update()
        {
            if (m_IsDead)
                return;

            m_TimeSinceLastDamage += Time.deltaTime;

            if (m_CurrentHealthValue >= RegenCap || m_CurrentHealthValue <= 0f || m_RegenAmount <= 0)
            {
                m_RegenTimer = 0f;
                return;
            }

            if (m_TimeSinceLastDamage < m_RegenDelayAfterDamage)
            {
                m_RegenTimer = 0f;
                return;
            }

            m_RegenTimer += Time.deltaTime;
            while (m_RegenTimer >= m_RegenInterval && m_CurrentHealthValue < RegenCap)
            {
                m_RegenTimer -= m_RegenInterval;
                SetCurrentHealthValue(Mathf.Min(m_CurrentHealthValue + m_RegenAmount, RegenCap), HealthChangeReason.Regen);
            }
        }

        public bool ApplyDamage(float amount, HealthChangeReason reason = HealthChangeReason.Damage)
        {
            if (m_IsDead || amount <= 0f)
                return false;

            float appliedDamage = Mathf.Min(amount, m_CurrentHealthValue);
            if (appliedDamage <= 0.0001f)
                return false;

            m_TimeSinceLastDamage = 0f;
            m_RegenTimer = 0f;
            DamageApplied?.Invoke(appliedDamage, reason);

            float nextHealthValue = m_CurrentHealthValue - appliedDamage;
            if (nextHealthValue <= 0.0001f && m_SingleRunLifeInsuranceCharges > 0)
            {
                m_SingleRunLifeInsuranceCharges = Mathf.Max(0, m_SingleRunLifeInsuranceCharges - 1);
                float insuredHealthValue = Mathf.Max(1f, nextHealthValue);
                if (insuredHealthValue <= m_CurrentHealthValue + 0.0001f)
                    SetCurrentHealthValue(insuredHealthValue, reason);
                return true;
            }

            SetCurrentHealthValue(m_CurrentHealthValue - appliedDamage, reason);
            return true;
        }

        public bool RestoreToMax(HealthChangeReason reason = HealthChangeReason.RefillStation)
        {
            if (m_IsDead || m_CurrentHealthValue >= m_MaxHealth - 0.0001f)
                return false;

            return SetCurrentHealthValue(m_MaxHealth, reason);
        }

        public bool RestoreAmount(float amount, HealthChangeReason reason = HealthChangeReason.DirectSet)
        {
            if (m_IsDead || amount <= 0f || m_CurrentHealthValue >= m_MaxHealth - 0.0001f)
                return false;

            return SetCurrentHealthValue(m_CurrentHealthValue + amount, reason);
        }

        public void ApplyPersistentMaxHealthBonus(int bonusAmount, bool restoreToFull = true)
        {
            int clampedBonus = Mathf.Max(0, bonusAmount);
            int targetMaxHealth = Mathf.Max(1, m_BaseMaxHealth + clampedBonus);
            int targetStartingHealth = Mathf.Clamp(m_BaseStartingHealth + clampedBonus, 0, targetMaxHealth);
            int targetLowHealthThreshold = Mathf.Clamp(m_BaseLowHealthThreshold, 1, targetMaxHealth);
            int targetRegenCap = Mathf.Clamp(m_RegenCap, 0, targetMaxHealth);

            bool changed = targetMaxHealth != m_MaxHealth
                || targetStartingHealth != m_StartingHealth
                || targetLowHealthThreshold != m_LowHealthThreshold
                || targetRegenCap != m_RegenCap;

            m_MaxHealth = targetMaxHealth;
            m_StartingHealth = targetStartingHealth;
            m_LowHealthThreshold = targetLowHealthThreshold;
            m_RegenCap = targetRegenCap;

            if (!changed && !restoreToFull)
                return;

            int previousHealth = m_CurrentHealth;
            float nextHealthValue = restoreToFull
                ? m_MaxHealth
                : Mathf.Clamp(m_CurrentHealthValue, 0f, m_MaxHealth);

            m_CurrentHealthValue = nextHealthValue;
            m_CurrentHealth = Mathf.Clamp(Mathf.FloorToInt(m_CurrentHealthValue + 0.0001f), 0, m_MaxHealth);
            m_IsDead = m_CurrentHealth <= 0;
            NotifyHealthChanged(HealthChangeReason.DirectSet, previousHealth);
        }

        public void ApplySingleRunLifeInsuranceCharges(int charges)
        {
            m_SingleRunLifeInsuranceCharges = Mathf.Max(0, charges);
        }

        public void ApplySingleRunHealthTradeoffReductionPercent(int reductionPercent)
        {
            int clampedReductionPercent = Mathf.Clamp(reductionPercent, 0, 95);
            if (clampedReductionPercent <= 0)
                return;

            float multiplier = 1f - clampedReductionPercent / 100f;
            int targetMaxHealth = Mathf.Max(1, Mathf.RoundToInt(m_MaxHealth * multiplier));
            int targetStartingHealth = Mathf.Clamp(m_StartingHealth, 0, targetMaxHealth);
            int targetLowHealthThreshold = Mathf.Clamp(m_LowHealthThreshold, 1, targetMaxHealth);
            int targetRegenCap = Mathf.Clamp(Mathf.RoundToInt(m_RegenCap * multiplier), 0, targetMaxHealth);

            bool changed = targetMaxHealth != m_MaxHealth
                || targetStartingHealth != m_StartingHealth
                || targetLowHealthThreshold != m_LowHealthThreshold
                || targetRegenCap != m_RegenCap;

            m_MaxHealth = targetMaxHealth;
            m_StartingHealth = targetStartingHealth;
            m_LowHealthThreshold = targetLowHealthThreshold;
            m_RegenCap = targetRegenCap;

            if (!changed)
                return;

            int previousHealth = m_CurrentHealth;
            m_CurrentHealthValue = Mathf.Clamp(m_CurrentHealthValue, 0f, m_MaxHealth);
            m_CurrentHealth = Mathf.Clamp(Mathf.FloorToInt(m_CurrentHealthValue + 0.0001f), 0, m_MaxHealth);
            m_IsDead = m_CurrentHealth <= 0;
            NotifyHealthChanged(HealthChangeReason.DirectSet, previousHealth);
        }

        public void ApplyPersistentHealthRegenCapBonus(int bonusAmount)
        {
            int clampedBonus = Mathf.Max(0, bonusAmount);
            m_RegenCap = Mathf.Clamp(m_BaseRegenCap + clampedBonus, 0, m_MaxHealth);
        }

        public void ApplyPersistentHealthRegenIntervalReductionPercent(int reductionPercent)
        {
            float multiplier = 1f - Mathf.Clamp(reductionPercent, 0, 80) / 100f;
            m_RegenInterval = Mathf.Max(0.25f, m_BaseRegenInterval * multiplier);
        }

        public void ApplyPersistentHealthRegenDelayReductionSeconds(int reductionSeconds)
        {
            m_RegenDelayAfterDamage = Mathf.Max(0f, m_BaseRegenDelayAfterDamage - Mathf.Max(0, reductionSeconds));
        }

        bool SetCurrentHealthValue(float newValue, HealthChangeReason reason)
        {
            float clampedValue = Mathf.Clamp(newValue, 0f, m_MaxHealth);
            int previousHealth = m_CurrentHealth;
            int nextHealth = Mathf.Clamp(Mathf.FloorToInt(clampedValue + 0.0001f), 0, m_MaxHealth);

            if (Mathf.Abs(clampedValue - m_CurrentHealthValue) <= 0.0001f && nextHealth == m_CurrentHealth)
                return false;

            m_CurrentHealthValue = clampedValue;

            if (nextHealth == m_CurrentHealth)
                return false;

            m_CurrentHealth = nextHealth;
            NotifyHealthChanged(reason, previousHealth);

            if (!m_IsDead && m_CurrentHealth <= 0)
            {
                m_IsDead = true;
                Died?.Invoke();
            }

            return true;
        }

        void NotifyHealthChanged(HealthChangeReason reason, int previousHealth)
        {
            HealthChanged?.Invoke(m_CurrentHealth, m_MaxHealth);
            HealthChangedDetailed?.Invoke(new HealthChangeContext(previousHealth, m_CurrentHealth, m_MaxHealth, reason));
        }

        void ApplySharedDefaults()
        {
            if (m_OverrideSharedDefaults)
                return;

            m_MaxHealth = SharedDefaults.MaxHealth;
            m_StartingHealth = SharedDefaults.StartingHealth;
            m_LowHealthThreshold = SharedDefaults.LowHealthThreshold;
            m_RegenCap = SharedDefaults.RegenCap;
            m_RegenAmount = SharedDefaults.RegenAmount;
            m_RegenInterval = SharedDefaults.RegenInterval;
            m_RegenDelayAfterDamage = SharedDefaults.RegenDelayAfterDamage;
        }
    }
}
