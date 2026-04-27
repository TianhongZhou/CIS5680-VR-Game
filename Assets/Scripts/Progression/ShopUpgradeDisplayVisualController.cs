using System;
using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Progression
{
    public sealed class ShopUpgradeDisplayVisualController : MonoBehaviour
    {
        const string HealthGroupId = "group_max_health";
        const string EnergyGroupId = "group_max_energy";
        const string StartingEnergyGroupId = "group_starting_energy";
        const string EnergyRegenGroupId = "group_energy_regen";
        const string HealthRecoveryGroupId = "group_health_recovery";
        const string RefillReserveGroupId = "group_refill_reserve";
        const string SonarGroupId = "group_sonar_efficiency";
        const string RevealGroupId = "group_pulse_reveal";
        const string PulseRadiusGroupId = "group_pulse_radius";
        const string StickyGroupId = "group_sticky_efficiency";
        const string StickyOverchargeGroupId = "group_sticky_overcharge";
        const string LocatorGroupId = "group_locator_recharge";
        const string RefillBoostGroupId = "group_refill_boost";
        const string TreasureSenseGroupId = "group_treasure_sense";
        const string ThreatSenseGroupId = "group_threat_sense";
        const string LowSignatureGroupId = "group_low_signature";
        const string LocatorSupportSenseGroupId = "group_locator_support_sense";
        const string LifeInsuranceGroupId = "group_temp_life_insurance";
        const string SurveyBurstGroupId = "group_temp_survey_burst";
        const string GoalRevealGroupId = "group_temp_goal_reveal";
        const string ColdStartGroupId = "group_temp_cold_start";
        const string FragileStealthRigGroupId = "group_temp_fragile_stealth_rig";
        const string GreedyCoreGroupId = "group_temp_greedy_core";
        const string GlassBatteryGroupId = "group_temp_glass_battery";
        const string OverclockedSonarGroupId = "group_temp_overclocked_sonar";
        const string SonarExtraBounceGroupId = "group_sonar_extra_bounce";
        const string TeleportLandingPulseGroupId = "group_teleport_landing_pulse";
        const string EchoMemoryGroupId = "group_echo_memory";

        static readonly int k_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int k_ColorId = Shader.PropertyToID("_Color");
        static readonly int k_EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Renderer[] m_CoreRenderers;
        [SerializeField] Renderer[] m_AccentRenderers;
        [SerializeField] Renderer[] m_TierTickRenderers;
        [SerializeField] Renderer[] m_GlassRenderers;
        [SerializeField] Renderer[] m_ProxyRenderers;
        [SerializeField] Light[] m_AccentLights;
        [SerializeField] Transform m_CoreMotionRoot;
        [SerializeField] float m_DefaultEmissionStrength = 3.4f;
        [SerializeField] float m_RiskyEmissionStrength = 4.8f;
        [SerializeField] float m_DimmedBrightnessMultiplier = 0.34f;

        readonly List<Renderer> m_RendererBuffer = new();

        MaterialPropertyBlock m_Block;
        ShopUpgradeDefinition m_Definition;
        Color m_PrimaryColor = new(0.32f, 0.98f, 1f, 1f);
        Color m_SecondaryColor = new(0.86f, 0.96f, 1f, 1f);
        Vector3 m_CoreBaseLocalPosition;
        Quaternion m_CoreBaseLocalRotation = Quaternion.identity;
        int m_TierLightCount = 3;
        float m_BaseEmissionStrength = 3.4f;
        float m_PulseFrequency = 1.4f;
        float m_PulseAmplitude = 0.22f;
        bool m_IsRisky;
        bool m_IsTemporary;
        bool m_IsMechanic;
        bool m_CanAfford = true;
        bool m_CanStillPurchase = true;
        bool m_ReferencesResolved;

        void Awake()
        {
            ResolveReferences();
            PrepareEmissiveMaterials();
            CacheCoreMotionBase();
        }

        void OnValidate()
        {
            ResolveReferences();
            CacheCoreMotionBase();
        }

        void OnEnable()
        {
            ApplyDynamicVisualState();
        }

        void Update()
        {
            ApplyDynamicVisualState();
            UpdateCoreMotion();
        }

        public void ApplyDefinition(ShopUpgradeDefinition definition, bool canAfford, bool canStillPurchase)
        {
            ResolveReferences();

            m_Definition = definition;
            m_CanAfford = canAfford;
            m_CanStillPurchase = canStillPurchase;

            ConfigureFromDefinition(definition);
            ApplyTierLights();
            ApplyDynamicVisualState();
        }

        void ConfigureFromDefinition(ShopUpgradeDefinition definition)
        {
            if (definition == null)
            {
                m_PrimaryColor = new Color(0.32f, 0.98f, 1f, 1f);
                m_SecondaryColor = Color.white;
                m_TierLightCount = 3;
                m_IsRisky = false;
                m_IsTemporary = false;
                m_IsMechanic = false;
                m_BaseEmissionStrength = m_DefaultEmissionStrength;
                m_PulseFrequency = 1.4f;
                m_PulseAmplitude = 0.18f;
                return;
            }

            m_IsRisky = definition.IsRiskySingleRunTemporary;
            m_IsTemporary = definition.IsSingleRunTemporary;
            m_IsMechanic = definition.IsMechanicChanging;
            m_TierLightCount = ResolveTierLightCount(definition);

            m_PrimaryColor = ResolvePrimaryColor(definition);
            m_SecondaryColor = Color.Lerp(m_PrimaryColor, Color.white, m_IsRisky ? 0.16f : 0.34f);
            m_BaseEmissionStrength = m_IsRisky ? m_RiskyEmissionStrength : m_DefaultEmissionStrength;

            if (definition.IsPlaceholder)
            {
                m_BaseEmissionStrength = 0.55f;
                m_PulseFrequency = 0.65f;
                m_PulseAmplitude = 0.08f;
                return;
            }

            if (m_IsRisky)
            {
                m_PulseFrequency = 5.8f;
                m_PulseAmplitude = 0.54f;
                return;
            }

            if (m_IsTemporary)
            {
                m_PulseFrequency = 3.4f;
                m_PulseAmplitude = 0.36f;
                return;
            }

            if (m_IsMechanic)
            {
                m_PulseFrequency = 1.9f;
                m_PulseAmplitude = 0.3f;
                return;
            }

            m_PulseFrequency = 1.15f;
            m_PulseAmplitude = 0.2f;
        }

        Color ResolvePrimaryColor(ShopUpgradeDefinition definition)
        {
            if (definition.IsPlaceholder)
                return new Color(0.55f, 0.56f, 0.58f, 1f);

            if (definition.IsRiskySingleRunTemporary)
                return new Color(1f, 0.11f, 0.06f, 1f);

            string groupId = definition.PurchaseGroupId ?? string.Empty;
            return groupId switch
            {
                HealthGroupId => new Color(0.38f, 1f, 0.52f, 1f),
                HealthRecoveryGroupId => new Color(0.48f, 1f, 0.74f, 1f),
                LifeInsuranceGroupId => new Color(0.55f, 1f, 0.68f, 1f),

                EnergyGroupId => new Color(1f, 0.52f, 0.08f, 1f),
                StartingEnergyGroupId => new Color(1f, 0.65f, 0.18f, 1f),
                EnergyRegenGroupId => new Color(1f, 0.76f, 0.25f, 1f),
                GlassBatteryGroupId => new Color(1f, 0.34f, 0.08f, 1f),

                SonarGroupId => new Color(0.25f, 0.92f, 1f, 1f),
                RevealGroupId => new Color(0.56f, 0.62f, 1f, 1f),
                PulseRadiusGroupId => new Color(0.32f, 1f, 0.94f, 1f),
                SonarExtraBounceGroupId => new Color(0.18f, 0.86f, 1f, 1f),
                SurveyBurstGroupId => new Color(0.16f, 0.95f, 1f, 1f),
                OverclockedSonarGroupId => new Color(1f, 0.16f, 0.06f, 1f),

                StickyGroupId => new Color(0.78f, 0.44f, 1f, 1f),
                StickyOverchargeGroupId => new Color(0.66f, 0.36f, 1f, 1f),

                LocatorGroupId => new Color(0.26f, 1f, 0.82f, 1f),
                LocatorSupportSenseGroupId => new Color(0.34f, 1f, 0.72f, 1f),
                TreasureSenseGroupId => new Color(1f, 0.78f, 0.18f, 1f),
                ThreatSenseGroupId => new Color(1f, 0.26f, 0.16f, 1f),
                LowSignatureGroupId => new Color(0.52f, 0.42f, 1f, 1f),
                EchoMemoryGroupId => new Color(0.42f, 0.94f, 1f, 1f),

                RefillBoostGroupId => new Color(0.72f, 1f, 0.28f, 1f),
                RefillReserveGroupId => new Color(0.95f, 1f, 0.38f, 1f),
                GreedyCoreGroupId => new Color(1f, 0.12f, 0.04f, 1f),

                TeleportLandingPulseGroupId => new Color(0.36f, 0.42f, 1f, 1f),
                GoalRevealGroupId => new Color(0.42f, 0.66f, 1f, 1f),
                ColdStartGroupId => new Color(0.62f, 0.86f, 1f, 1f),
                FragileStealthRigGroupId => new Color(1f, 0.1f, 0.06f, 1f),

                _ => ResolvePrimaryColorFromEffect(definition.EffectType),
            };
        }

        Color ResolvePrimaryColorFromEffect(ShopUpgradeEffectType effectType)
        {
            return effectType switch
            {
                ShopUpgradeEffectType.MaxHealthBonus => new Color(0.38f, 1f, 0.52f, 1f),
                ShopUpgradeEffectType.HealthRegenCapBonus => new Color(0.48f, 1f, 0.74f, 1f),
                ShopUpgradeEffectType.MaxEnergyBonus => new Color(1f, 0.52f, 0.08f, 1f),
                ShopUpgradeEffectType.StartingEnergyBonus => new Color(1f, 0.65f, 0.18f, 1f),
                ShopUpgradeEffectType.EnergyRegenIntervalReductionPercent => new Color(1f, 0.76f, 0.25f, 1f),
                ShopUpgradeEffectType.SonarCostReduction => new Color(0.25f, 0.92f, 1f, 1f),
                ShopUpgradeEffectType.PulseRevealDurationBonusSeconds => new Color(0.56f, 0.62f, 1f, 1f),
                ShopUpgradeEffectType.PulseRadiusBonusPercent => new Color(0.32f, 1f, 0.94f, 1f),
                ShopUpgradeEffectType.StickyPulseCostReduction => new Color(0.78f, 0.44f, 1f, 1f),
                ShopUpgradeEffectType.LocatorCooldownReductionPercent => new Color(0.26f, 1f, 0.82f, 1f),
                ShopUpgradeEffectType.RefillBoostPercent => new Color(0.72f, 1f, 0.28f, 1f),
                ShopUpgradeEffectType.RefillStationExtraUses => new Color(0.95f, 1f, 0.38f, 1f),
                _ => new Color(0.32f, 0.98f, 1f, 1f),
            };
        }

        int ResolveTierLightCount(ShopUpgradeDefinition definition)
        {
            if (definition == null)
                return 3;

            string name = definition.DisplayName ?? string.Empty;
            if (name.IndexOf("Tier B", StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;

            if (name.IndexOf("Tier A", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;

            if (name.IndexOf("Tier S", StringComparison.OrdinalIgnoreCase) >= 0)
                return 4;

            if (definition.IsPlaceholder)
                return 1;

            return definition.IsRiskySingleRunTemporary ? 4 : 3;
        }

        void ApplyDynamicVisualState()
        {
            ResolveReferences();

            if (m_Block == null)
                m_Block = new MaterialPropertyBlock();

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, m_PulseFrequency) * Mathf.PI * 2f);
            if (m_IsRisky)
                wave = wave > 0.52f ? 1f : 0.12f;

            float brightness = 1f + wave * m_PulseAmplitude;
            if (!m_CanAfford)
                brightness *= m_DimmedBrightnessMultiplier;

            if (!m_CanStillPurchase)
                brightness *= 0.22f;

            float emission = Mathf.Max(0f, m_BaseEmissionStrength * brightness);
            Color coreColor = Color.Lerp(m_PrimaryColor, m_SecondaryColor, m_IsRisky ? wave * 0.18f : wave * 0.32f);
            Color accentColor = Color.Lerp(m_PrimaryColor, m_SecondaryColor, 0.22f);
            ApplyRenderers(m_CoreRenderers, coreColor, emission);
            ApplyRenderers(m_AccentRenderers, accentColor, emission * 0.82f);
            ApplyRenderers(m_TierTickRenderers, accentColor, emission);
            ApplyRenderers(m_GlassRenderers, Color.Lerp(coreColor, Color.white, 0.38f), emission * 0.18f);

            if (m_AccentLights != null)
            {
                for (int i = 0; i < m_AccentLights.Length; i++)
                {
                    Light accentLight = m_AccentLights[i];
                    if (accentLight == null)
                        continue;

                    accentLight.color = coreColor;
                    accentLight.intensity = Mathf.Clamp(emission * 0.22f, 0f, 2.2f);
                }
            }
        }

        void ApplyTierLights()
        {
            if (m_TierTickRenderers == null)
                return;

            int tickCount = Mathf.Clamp(m_TierLightCount, 0, m_TierTickRenderers.Length);
            for (int i = 0; i < m_TierTickRenderers.Length; i++)
            {
                Renderer renderer = m_TierTickRenderers[i];
                if (renderer != null)
                    renderer.gameObject.SetActive(i < tickCount);
            }
        }

        void ApplyRenderers(Renderer[] renderers, Color baseColor, float emissionStrength)
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null || !target.gameObject.activeInHierarchy)
                    continue;

                target.GetPropertyBlock(m_Block);
                if (SupportsProperty(target, k_BaseColorId))
                    m_Block.SetColor(k_BaseColorId, baseColor);

                if (SupportsProperty(target, k_ColorId))
                    m_Block.SetColor(k_ColorId, baseColor);

                if (SupportsProperty(target, k_EmissionColorId))
                    m_Block.SetColor(k_EmissionColorId, baseColor * Mathf.Max(0f, emissionStrength));

                target.SetPropertyBlock(m_Block);
            }
        }

        bool SupportsProperty(Renderer target, int propertyId)
        {
            if (target == null || target.sharedMaterial == null)
                return false;

            return target.sharedMaterial.HasProperty(propertyId);
        }

        void UpdateCoreMotion()
        {
            if (m_CoreMotionRoot == null)
                return;

            m_CoreMotionRoot.localPosition = m_CoreBaseLocalPosition;
            m_CoreMotionRoot.localRotation = m_CoreBaseLocalRotation;
        }

        void ResolveReferences()
        {
            if (m_ReferencesResolved
                && m_CoreRenderers != null && m_CoreRenderers.Length > 0
                && m_AccentRenderers != null && m_AccentRenderers.Length > 0)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            var core = new List<Renderer>();
            var accents = new List<Renderer>();
            var tierTicks = new List<Renderer>();
            var glass = new List<Renderer>();
            var proxies = new List<Renderer>();

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                string lowerName = renderer.name.ToLowerInvariant();
                if (lowerName.Contains("locatorproxy") || lowerName.Contains("proxy"))
                {
                    proxies.Add(renderer);
                    renderer.enabled = false;
                    continue;
                }

                if (lowerName.Contains("transparent") || lowerName.Contains("glass"))
                {
                    glass.Add(renderer);
                    continue;
                }

                if (lowerName.Contains("upgradecore") && lowerName.Contains("emissive"))
                {
                    core.Add(renderer);
                    continue;
                }

                if (lowerName.Contains("statusticks_emissive_top"))
                {
                    tierTicks.Add(renderer);
                    continue;
                }

                if (lowerName.Contains("emissive") || lowerName.Contains("glow") || lowerName.Contains("placeholderbar"))
                    accents.Add(renderer);
            }

            m_CoreRenderers = core.ToArray();
            m_AccentRenderers = accents.ToArray();
            m_TierTickRenderers = tierTicks.ToArray();
            m_GlassRenderers = glass.ToArray();
            m_ProxyRenderers = proxies.ToArray();
            m_AccentLights = GetComponentsInChildren<Light>(true);
            m_CoreMotionRoot = ResolveCoreMotionRoot();
            m_ReferencesResolved = true;
        }

        Transform ResolveCoreMotionRoot()
        {
            Transform[] transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == transform)
                    continue;

                string lowerName = candidate.name.ToLowerInvariant();
                if (lowerName.Contains("upgradecore_emissivecolorslot")
                    && !lowerName.Contains("transparent"))
                {
                    return candidate;
                }
            }

            return null;
        }

        void CacheCoreMotionBase()
        {
            if (m_CoreMotionRoot == null)
                return;

            m_CoreBaseLocalPosition = m_CoreMotionRoot.localPosition;
            m_CoreBaseLocalRotation = m_CoreMotionRoot.localRotation;
        }

        void PrepareEmissiveMaterials()
        {
            m_RendererBuffer.Clear();
            AppendRenderers(m_CoreRenderers);
            AppendRenderers(m_AccentRenderers);
            AppendRenderers(m_TierTickRenderers);
            AppendRenderers(m_GlassRenderers);

            for (int i = 0; i < m_RendererBuffer.Count; i++)
            {
                Renderer renderer = m_RendererBuffer[i];
                if (renderer == null)
                    continue;

                Material[] materials = renderer.sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null || !material.HasProperty(k_EmissionColorId))
                        continue;

                    material.EnableKeyword("_EMISSION");
                }
            }
        }

        void AppendRenderers(Renderer[] renderers)
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && !m_RendererBuffer.Contains(renderers[i]))
                    m_RendererBuffer.Add(renderers[i]);
            }
        }
    }
}
