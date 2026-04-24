using CIS5680VRGame.Gameplay;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(Collider))]
    public class BallRefillStation : MonoBehaviour
    {
        static class SharedDefaults
        {
            public const int Charges = 2;
            public const float Cooldown = 20f;
            public const float BaseRefillAmount = 50f;
        }

        const int k_UnlimitedChargeCount = 999999;

        static readonly int k_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int k_ColorId = Shader.PropertyToID("_Color");
        static readonly int k_RimColorId = Shader.PropertyToID("_RimColor");

        [SerializeField] BallHolsterSlot[] m_TargetSlots;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] PlayerEnergy m_PlayerEnergy;
        [SerializeField] bool m_OverrideSharedDefaults;
        [SerializeField] bool m_IgnoreProgressionModifiers;
        [SerializeField] bool m_UnlimitedCharges;
        [SerializeField, Min(1)] int m_Charges = SharedDefaults.Charges;
        [SerializeField] float m_Cooldown = SharedDefaults.Cooldown;
        [SerializeField] float m_BaseRefillAmount = SharedDefaults.BaseRefillAmount;
        [SerializeField] Renderer[] m_VisitedStateRenderers;
        [SerializeField] Color m_RechargingColorA = new(0.34f, 0.16f, 0.04f, 1f);
        [SerializeField] Color m_RechargingColorB = new(1f, 0.6f, 0.18f, 1f);
        [SerializeField] Color m_RechargingRimColor = new(1f, 0.88f, 0.5f, 0.34f);
        [SerializeField] float m_RechargingFlashFrequency = 3.2f;
        [SerializeField] Color m_VisitedColor = new(0.18f, 0.84f, 0.96f, 1f);
        [SerializeField] Color m_VisitedRimColor = new(0.9f, 1f, 1f, 0.24f);

        Collider m_Trigger;
        MaterialPropertyBlock m_PropertyBlock;
        float m_CooldownEndsAt = -999f;
        int m_ChargesRemaining;
        int m_PersistentRefillBonusPercent;
        int m_PersistentChargeBonus;
        int m_PersistentCooldownReductionPercent;
        int m_SingleRunChargePenalty;
        int m_SingleRunRefillMultiplierPercent = 100;

        public int MaxCharges
        {
            get
            {
                if (m_UnlimitedCharges)
                    return k_UnlimitedChargeCount;

                int chargeBonus = m_IgnoreProgressionModifiers ? 0 : Mathf.Max(0, m_PersistentChargeBonus);
                int chargePenalty = m_IgnoreProgressionModifiers ? 0 : Mathf.Max(0, m_SingleRunChargePenalty);
                return Mathf.Max(1, m_Charges + chargeBonus - chargePenalty);
            }
        }

        public int ChargesRemaining => m_UnlimitedCharges ? MaxCharges : Mathf.Max(0, m_ChargesRemaining);
        public bool IsDepleted => !m_UnlimitedCharges && ChargesRemaining <= 0;
        public bool IsReady => !IsDepleted && Time.time >= m_CooldownEndsAt;
        public bool IsLocatorAvailable => !IsDepleted;
        public bool HasBeenVisited => !m_UnlimitedCharges && IsDepleted;

        public void SetPersistentRefillBonusPercent(int bonusPercent)
        {
            m_PersistentRefillBonusPercent = Mathf.Max(0, bonusPercent);
        }

        public void SetPersistentChargeBonus(int bonusCharges)
        {
            int previousMaxCharges = MaxCharges;
            m_PersistentChargeBonus = Mathf.Max(0, bonusCharges);
            AdjustRemainingCharges(previousMaxCharges, MaxCharges);
        }

        public void SetSingleRunChargePenalty(int penaltyCharges)
        {
            int previousMaxCharges = MaxCharges;
            m_SingleRunChargePenalty = Mathf.Max(0, penaltyCharges);
            AdjustRemainingCharges(previousMaxCharges, MaxCharges);
        }

        public void SetPersistentCooldownReductionPercent(int reductionPercent)
        {
            m_PersistentCooldownReductionPercent = Mathf.Clamp(reductionPercent, 0, 90);
        }

        public void SetSingleRunRefillMultiplierPercent(int multiplierPercent)
        {
            m_SingleRunRefillMultiplierPercent = Mathf.Clamp(multiplierPercent, 1, 1000);
        }

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

            m_Trigger = GetComponent<Collider>();
            m_Trigger.isTrigger = true;

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_PlayerEnergy == null)
                m_PlayerEnergy = FindObjectOfType<PlayerEnergy>();

            if (m_VisitedStateRenderers == null || m_VisitedStateRenderers.Length == 0)
                m_VisitedStateRenderers = GetComponentsInChildren<Renderer>(true);

            m_ChargesRemaining = MaxCharges;
            m_PropertyBlock = new MaterialPropertyBlock();
            UpdateVisualState();
        }

        void Update()
        {
            UpdateVisualState();
        }

        void OnTriggerEnter(Collider other)
        {
            TryRefill(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryRefill(other);
        }

        bool CanUse(Collider other)
        {
            if (!enabled || !IsReady)
                return false;

            if (other == null)
                return false;

            var rig = other.GetComponentInParent<XROrigin>();
            if (rig == null)
                return false;

            return m_PlayerRig == null || rig == m_PlayerRig;
        }

        void TryRefill(Collider other)
        {
            if (!CanUse(other))
                return;

            bool refilledAny = false;
            float effectiveRefillAmount = GetEffectiveRefillAmount();
            for (int i = 0; i < m_TargetSlots.Length; i++)
            {
                BallHolsterSlot targetSlot = m_TargetSlots[i];
                if (targetSlot == null)
                    continue;

                refilledAny |= targetSlot.RefillAmount(effectiveRefillAmount);
            }

            if (!refilledAny && (m_TargetSlots == null || m_TargetSlots.Length == 0) && m_PlayerEnergy != null)
                refilledAny = m_PlayerEnergy.RestoreAmount(effectiveRefillAmount);

            if (refilledAny)
            {
                m_ChargesRemaining = m_UnlimitedCharges
                    ? MaxCharges
                    : Mathf.Max(0, m_ChargesRemaining - 1);

                if (!IsDepleted)
                    m_CooldownEndsAt = Time.time + GetEffectiveCooldown();
                else
                    m_CooldownEndsAt = -999f;

                PulseAudioService.PlayResourceRestored();
                UpdateVisualState();
            }
        }

        float GetEffectiveRefillAmount()
        {
            if (m_IgnoreProgressionModifiers)
                return Mathf.Max(1f, m_BaseRefillAmount);

            float persistentMultiplier = 1f + Mathf.Max(0, m_PersistentRefillBonusPercent) / 100f;
            float singleRunMultiplier = Mathf.Clamp(m_SingleRunRefillMultiplierPercent, 1, 1000) / 100f;
            float multiplier = persistentMultiplier * singleRunMultiplier;
            return Mathf.Max(1f, m_BaseRefillAmount * multiplier);
        }

        float GetEffectiveCooldown()
        {
            if (m_IgnoreProgressionModifiers)
                return Mathf.Max(0.1f, m_Cooldown);

            float multiplier = 1f - (Mathf.Clamp(m_PersistentCooldownReductionPercent, 0, 90) / 100f);
            return Mathf.Max(0.1f, m_Cooldown * multiplier);
        }

        void UpdateVisualState()
        {
            if (m_VisitedStateRenderers == null || m_VisitedStateRenderers.Length == 0)
                return;

            if (IsDepleted)
            {
                ApplyVisualState(m_VisitedColor, m_VisitedRimColor);
                return;
            }

            if (!IsReady)
            {
                float flashWave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, m_RechargingFlashFrequency) * Mathf.PI * 2f);
                Color flashColor = Color.Lerp(m_RechargingColorA, m_RechargingColorB, flashWave);
                ApplyVisualState(flashColor, m_RechargingRimColor);
                return;
            }

            ClearVisualState();
        }

        void ApplyVisualState(Color baseColor, Color rimColor)
        {
            for (int i = 0; i < m_VisitedStateRenderers.Length; i++)
            {
                Renderer targetRenderer = m_VisitedStateRenderers[i];
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(k_BaseColorId, baseColor);
                m_PropertyBlock.SetColor(k_ColorId, baseColor);
                m_PropertyBlock.SetColor(k_RimColorId, rimColor);
                targetRenderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        void ClearVisualState()
        {
            for (int i = 0; i < m_VisitedStateRenderers.Length; i++)
            {
                Renderer targetRenderer = m_VisitedStateRenderers[i];
                if (targetRenderer == null)
                    continue;

                m_PropertyBlock.Clear();
                targetRenderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        void ApplySharedDefaults()
        {
            if (m_OverrideSharedDefaults)
                return;

            m_IgnoreProgressionModifiers = false;
            m_UnlimitedCharges = false;
            m_Charges = SharedDefaults.Charges;
            m_Cooldown = SharedDefaults.Cooldown;
            m_BaseRefillAmount = SharedDefaults.BaseRefillAmount;
        }

        void AdjustRemainingCharges(int previousMaxCharges, int newMaxCharges)
        {
            if (m_UnlimitedCharges)
            {
                m_ChargesRemaining = MaxCharges;
                return;
            }

            if (m_ChargesRemaining <= 0)
                return;

            if (m_ChargesRemaining >= previousMaxCharges)
            {
                m_ChargesRemaining = newMaxCharges;
                return;
            }

            int chargesSpent = Mathf.Max(0, previousMaxCharges - m_ChargesRemaining);
            m_ChargesRemaining = Mathf.Clamp(newMaxCharges - chargesSpent, 0, newMaxCharges);
        }
    }
}
