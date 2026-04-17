using System;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public enum EnergyChangeReason
    {
        Initialize = 0,
        Consume = 1,
        Regen = 2,
        RefillStation = 3,
        DirectSet = 4,
    }

    public readonly struct EnergyChangeContext
    {
        public EnergyChangeContext(int previousEnergy, int currentEnergy, int maxEnergy, EnergyChangeReason reason)
        {
            PreviousEnergy = previousEnergy;
            CurrentEnergy = currentEnergy;
            MaxEnergy = maxEnergy;
            Reason = reason;
        }

        public int PreviousEnergy { get; }
        public int CurrentEnergy { get; }
        public int MaxEnergy { get; }
        public EnergyChangeReason Reason { get; }
        public int Delta => CurrentEnergy - PreviousEnergy;
    }

    public class PlayerEnergy : MonoBehaviour
    {
        [SerializeField] int m_MaxEnergy = 100;
        [SerializeField] int m_StartingEnergy = 80;
        [SerializeField] int m_RegenAmount = 1;
        [SerializeField] float m_RegenInterval = 3f;
        [SerializeField] float m_RefillDuration = 0.6f;
        [SerializeField] float m_InsufficientSignalCooldown = 0.2f;

        int m_CurrentEnergy;
        float m_CurrentEnergyValue;
        float m_RegenTimer;
        float m_LastInsufficientSignalTime = -999f;
        bool m_IsRefillInProgress;
        float m_RefillStartEnergyValue;
        float m_RefillTargetEnergyValue;
        float m_RefillElapsed;

        public event Action<int, int> EnergyChanged;
        public event Action<EnergyChangeContext> EnergyChangedDetailed;
        public event Action<int, int> InsufficientEnergySignaled;
        public event Action RefillStarted;

        public int CurrentEnergy => m_CurrentEnergy;
        public int MaxEnergy => Mathf.Max(1, m_MaxEnergy);
        public float NormalizedEnergy => MaxEnergy <= 0 ? 0f : Mathf.Clamp01(m_CurrentEnergyValue / MaxEnergy);
        public bool IsRefillInProgress => m_IsRefillInProgress;

        void Awake()
        {
            m_MaxEnergy = Mathf.Max(1, m_MaxEnergy);
            m_CurrentEnergyValue = Mathf.Clamp(m_StartingEnergy, 0f, m_MaxEnergy);
            m_CurrentEnergy = Mathf.Clamp(Mathf.FloorToInt(m_CurrentEnergyValue + 0.0001f), 0, m_MaxEnergy);
            m_RegenAmount = Mathf.Max(0, m_RegenAmount);
            m_RegenInterval = Mathf.Max(0.01f, m_RegenInterval);
            m_RefillDuration = Mathf.Max(0.05f, m_RefillDuration);
            m_RegenTimer = 0f;
        }

        void Start()
        {
            NotifyEnergyChanged(EnergyChangeReason.Initialize, m_CurrentEnergy);
        }

        void Update()
        {
            UpdateRefill();

            if (m_IsRefillInProgress)
                return;

            if (m_CurrentEnergy >= m_MaxEnergy || m_RegenAmount <= 0)
                return;

            m_RegenTimer += Time.deltaTime;
            while (m_RegenTimer >= m_RegenInterval && m_CurrentEnergy < m_MaxEnergy)
            {
                m_RegenTimer -= m_RegenInterval;
                SetCurrentEnergyValue(m_CurrentEnergyValue + m_RegenAmount, EnergyChangeReason.Regen);
            }
        }

        public bool CanAfford(int amount)
        {
            return amount <= 0 || m_CurrentEnergy >= amount;
        }

        public bool TryConsume(int amount)
        {
            if (amount <= 0)
                return true;

            if (!CanAfford(amount))
            {
                SignalInsufficient(amount);
                return false;
            }

            CancelRefill();
            SetCurrentEnergyValue(m_CurrentEnergyValue - amount, EnergyChangeReason.Consume);
            return true;
        }

        public void SignalInsufficient(int requiredAmount)
        {
            if (Time.unscaledTime - m_LastInsufficientSignalTime < m_InsufficientSignalCooldown)
                return;

            m_LastInsufficientSignalTime = Time.unscaledTime;
            InsufficientEnergySignaled?.Invoke(m_CurrentEnergy, Mathf.Max(0, requiredAmount));
        }

        public bool RefillToMax(EnergyChangeReason reason = EnergyChangeReason.RefillStation)
        {
            if (m_CurrentEnergyValue >= m_MaxEnergy - 0.0001f || m_IsRefillInProgress)
                return false;

            m_IsRefillInProgress = true;
            m_RefillStartEnergyValue = m_CurrentEnergyValue;
            m_RefillTargetEnergyValue = m_MaxEnergy;
            m_RefillElapsed = 0f;
            m_RegenTimer = 0f;
            RefillStarted?.Invoke();
            NotifyEnergyChanged(reason, m_CurrentEnergy);
            return true;
        }

        void UpdateRefill()
        {
            if (!m_IsRefillInProgress)
                return;

            m_RefillElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(m_RefillElapsed / Mathf.Max(0.05f, m_RefillDuration));
            float eased = 1f - Mathf.Pow(1f - t, 2.4f);
            SetCurrentEnergyValue(Mathf.Lerp(m_RefillStartEnergyValue, m_RefillTargetEnergyValue, eased), EnergyChangeReason.RefillStation);

            if (t >= 1f - 0.0001f)
            {
                m_IsRefillInProgress = false;
                SetCurrentEnergyValue(m_RefillTargetEnergyValue, EnergyChangeReason.RefillStation);
            }
        }

        void CancelRefill()
        {
            if (!m_IsRefillInProgress)
                return;

            m_IsRefillInProgress = false;
            m_RefillElapsed = 0f;
            m_RefillStartEnergyValue = m_CurrentEnergyValue;
        }

        void SetCurrentEnergyValue(float newValue, EnergyChangeReason reason = EnergyChangeReason.DirectSet)
        {
            float clampedValue = Mathf.Clamp(newValue, 0f, m_MaxEnergy);
            int previous = m_CurrentEnergy;
            int clampedEnergy = Mathf.Clamp(Mathf.FloorToInt(clampedValue + 0.0001f), 0, m_MaxEnergy);

            m_CurrentEnergyValue = clampedValue;

            if (clampedEnergy == m_CurrentEnergy)
                return;

            m_CurrentEnergy = clampedEnergy;
            if (m_CurrentEnergy >= m_MaxEnergy && !m_IsRefillInProgress)
                m_RegenTimer = 0f;

            NotifyEnergyChanged(reason, previous);
        }

        void NotifyEnergyChanged(EnergyChangeReason reason, int previousEnergy)
        {
            EnergyChanged?.Invoke(m_CurrentEnergy, m_MaxEnergy);
            EnergyChangedDetailed?.Invoke(new EnergyChangeContext(previousEnergy, m_CurrentEnergy, m_MaxEnergy, reason));
        }
    }
}
