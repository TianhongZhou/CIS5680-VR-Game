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
        static class SharedDefaults
        {
            public const int MaxEnergy = 100;
            public const int StartingEnergy = 50;
            public const int RegenAmount = 1;
            public const float RegenInterval = 3f;
            public const float RefillDuration = 0.6f;
            public const float InsufficientSignalCooldown = 0.2f;
        }

        [SerializeField] bool m_OverrideSharedDefaults;
        [SerializeField] int m_MaxEnergy = SharedDefaults.MaxEnergy;
        [SerializeField] int m_StartingEnergy = SharedDefaults.StartingEnergy;
        [SerializeField] int m_RegenAmount = SharedDefaults.RegenAmount;
        [SerializeField] float m_RegenInterval = SharedDefaults.RegenInterval;
        [SerializeField] float m_RefillDuration = SharedDefaults.RefillDuration;
        [SerializeField] float m_InsufficientSignalCooldown = SharedDefaults.InsufficientSignalCooldown;

        int m_CurrentEnergy;
        float m_CurrentEnergyValue;
        float m_RegenTimer;
        float m_LastInsufficientSignalTime = -999f;
        bool m_IsRefillInProgress;
        float m_RefillStartEnergyValue;
        float m_RefillTargetEnergyValue;
        float m_RefillElapsed;
        int m_BaseMaxEnergy;
        int m_BaseStartingEnergy;
        float m_BaseRegenInterval;

        public event Action<int, int> EnergyChanged;
        public event Action<EnergyChangeContext> EnergyChangedDetailed;
        public event Action<int, int> InsufficientEnergySignaled;
        public event Action RefillStarted;

        public int CurrentEnergy => m_CurrentEnergy;
        public int MaxEnergy => Mathf.Max(1, m_MaxEnergy);
        public float NormalizedEnergy => MaxEnergy <= 0 ? 0f : Mathf.Clamp01(m_CurrentEnergyValue / MaxEnergy);
        public bool IsRefillInProgress => m_IsRefillInProgress;

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

            m_MaxEnergy = Mathf.Max(1, m_MaxEnergy);
            m_BaseMaxEnergy = m_MaxEnergy;
            m_BaseStartingEnergy = Mathf.Clamp(m_StartingEnergy, 0, m_MaxEnergy);
            m_BaseRegenInterval = Mathf.Max(0.01f, m_RegenInterval);
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

        public bool RestoreAmount(float amount, EnergyChangeReason reason = EnergyChangeReason.RefillStation)
        {
            if (amount <= 0f || m_IsRefillInProgress || m_CurrentEnergyValue >= m_MaxEnergy - 0.0001f)
                return false;

            m_IsRefillInProgress = true;
            m_RefillStartEnergyValue = m_CurrentEnergyValue;
            m_RefillTargetEnergyValue = Mathf.Clamp(m_CurrentEnergyValue + amount, 0f, m_MaxEnergy);
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

        public void ApplyPersistentMaxEnergyBonus(int bonusAmount, bool restoreToFull = true)
        {
            int clampedBonus = Mathf.Max(0, bonusAmount);
            int targetMaxEnergy = Mathf.Max(1, m_BaseMaxEnergy + clampedBonus);
            int targetStartingEnergy = Mathf.Clamp(m_StartingEnergy, 0, targetMaxEnergy);
            bool changed = targetMaxEnergy != m_MaxEnergy || targetStartingEnergy != m_StartingEnergy;

            CancelRefill();
            m_MaxEnergy = targetMaxEnergy;
            m_StartingEnergy = targetStartingEnergy;

            if (!changed && !restoreToFull)
                return;

            int previousEnergy = m_CurrentEnergy;
            float nextEnergyValue = restoreToFull
                ? m_MaxEnergy
                : Mathf.Clamp(m_CurrentEnergyValue, 0f, m_MaxEnergy);

            m_CurrentEnergyValue = nextEnergyValue;
            m_CurrentEnergy = Mathf.Clamp(Mathf.FloorToInt(m_CurrentEnergyValue + 0.0001f), 0, m_MaxEnergy);
            m_RegenTimer = 0f;
            NotifyEnergyChanged(EnergyChangeReason.DirectSet, previousEnergy);
        }

        public void ApplyPersistentStartingEnergyBonus(int bonusAmount, bool applyToCurrentEnergy = true)
        {
            int clampedBonus = Mathf.Max(0, bonusAmount);
            int targetStartingEnergy = Mathf.Clamp(m_BaseStartingEnergy + clampedBonus, 0, m_MaxEnergy);
            bool changed = targetStartingEnergy != m_StartingEnergy;

            CancelRefill();
            m_StartingEnergy = targetStartingEnergy;

            if (!changed && !applyToCurrentEnergy)
                return;

            int previousEnergy = m_CurrentEnergy;
            float nextEnergyValue = applyToCurrentEnergy
                ? m_StartingEnergy
                : Mathf.Clamp(m_CurrentEnergyValue, 0f, m_MaxEnergy);

            m_CurrentEnergyValue = nextEnergyValue;
            m_CurrentEnergy = Mathf.Clamp(Mathf.FloorToInt(m_CurrentEnergyValue + 0.0001f), 0, m_MaxEnergy);
            m_RegenTimer = 0f;
            NotifyEnergyChanged(EnergyChangeReason.DirectSet, previousEnergy);
        }

        public void ApplyPersistentEnergyRegenIntervalReductionPercent(int reductionPercent)
        {
            float multiplier = 1f - Mathf.Clamp(reductionPercent, 0, 80) / 100f;
            m_RegenInterval = Mathf.Max(0.25f, m_BaseRegenInterval * multiplier);
        }

        void NotifyEnergyChanged(EnergyChangeReason reason, int previousEnergy)
        {
            EnergyChanged?.Invoke(m_CurrentEnergy, m_MaxEnergy);
            EnergyChangedDetailed?.Invoke(new EnergyChangeContext(previousEnergy, m_CurrentEnergy, m_MaxEnergy, reason));
        }

        void ApplySharedDefaults()
        {
            if (m_OverrideSharedDefaults)
                return;

            m_MaxEnergy = SharedDefaults.MaxEnergy;
            m_StartingEnergy = SharedDefaults.StartingEnergy;
            m_RegenAmount = SharedDefaults.RegenAmount;
            m_RegenInterval = SharedDefaults.RegenInterval;
            m_RefillDuration = SharedDefaults.RefillDuration;
            m_InsufficientSignalCooldown = SharedDefaults.InsufficientSignalCooldown;
        }
    }
}
