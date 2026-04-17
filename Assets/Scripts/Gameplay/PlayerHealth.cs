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
        [SerializeField] int m_MaxHealth = 100;
        [SerializeField] int m_StartingHealth = 100;
        [SerializeField] int m_LowHealthThreshold = 30;
        [SerializeField] int m_RegenAmount = 1;
        [SerializeField] float m_RegenInterval = 1f;
        [SerializeField] float m_RegenDelayAfterDamage = 10f;

        int m_CurrentHealth;
        float m_CurrentHealthValue;
        float m_RegenTimer;
        float m_TimeSinceLastDamage = float.PositiveInfinity;
        bool m_IsDead;

        public event Action<int, int> HealthChanged;
        public event Action<HealthChangeContext> HealthChangedDetailed;
        public event Action Died;

        public int CurrentHealth => m_CurrentHealth;
        public int MaxHealth => Mathf.Max(1, m_MaxHealth);
        public int LowHealthThreshold => Mathf.Clamp(m_LowHealthThreshold, 1, MaxHealth);
        public float NormalizedHealth => Mathf.Clamp01(m_CurrentHealthValue / MaxHealth);
        public bool IsDead => m_IsDead;

        void Awake()
        {
            m_MaxHealth = Mathf.Max(1, m_MaxHealth);
            m_LowHealthThreshold = Mathf.Clamp(m_LowHealthThreshold, 1, m_MaxHealth);
            m_RegenAmount = Mathf.Max(0, m_RegenAmount);
            m_RegenInterval = Mathf.Max(0.01f, m_RegenInterval);
            m_RegenDelayAfterDamage = Mathf.Max(0f, m_RegenDelayAfterDamage);

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

            if (m_CurrentHealthValue >= LowHealthThreshold || m_CurrentHealthValue <= 0f || m_RegenAmount <= 0)
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
            while (m_RegenTimer >= m_RegenInterval && m_CurrentHealthValue < LowHealthThreshold)
            {
                m_RegenTimer -= m_RegenInterval;
                SetCurrentHealthValue(Mathf.Min(m_CurrentHealthValue + m_RegenAmount, LowHealthThreshold), HealthChangeReason.Regen);
            }
        }

        public bool ApplyDamage(float amount, HealthChangeReason reason = HealthChangeReason.Damage)
        {
            if (m_IsDead || amount <= 0f)
                return false;

            m_TimeSinceLastDamage = 0f;
            m_RegenTimer = 0f;
            return SetCurrentHealthValue(m_CurrentHealthValue - amount, reason);
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
    }
}
