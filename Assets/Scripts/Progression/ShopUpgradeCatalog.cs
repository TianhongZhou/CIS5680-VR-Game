using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CIS5680VRGame.Progression
{
    public enum ShopUpgradeEffectType
    {
        None = 0,
        MaxHealthBonus = 1,
        MaxEnergyBonus = 2,
        StartingEnergyBonus = 3,
        SonarCostReduction = 4,
        PulseRevealDurationBonusSeconds = 5,
        PulseRadiusBonusPercent = 6,
        StickyPulseCostReduction = 7,
        LocatorCooldownReductionPercent = 8,
        RefillBoostPercent = 9,
        NextRunLifeInsuranceCharge = 10,
        TreasureSenseRangeMeters = 11,
        LocatorSupportSenseRangeMeters = 12,
        EnergyRegenIntervalReductionPercent = 13,
        HealthRegenCapBonus = 14,
        HealthRegenIntervalReductionPercent = 15,
        HealthRegenDelayReductionSeconds = 16,
        NextRunSurveyBurstCharge = 17,
        NextRunGoalRevealCharge = 18,
        SonarExtraBouncePulseRadiusPercent = 19,
        TeleportLandingPulseRadiusPercent = 20,
        TeleportLandingPulseRevealDurationSeconds = 21,
        EchoMemoryTrailLengthMeters = 22,
        EchoMemoryTrailDurationSeconds = 23,
        RefillStationExtraUses = 24,
        RefillStationCooldownReductionPercent = 25,
        StickyPulseExtraPulseCount = 26,
        NextRunEscapeGoldBonusPercent = 27,
        NextRunRefillStationUsePenalty = 28,
        NextRunRefillAmountPenaltyPercent = 29,
        NextRunEnergyCapacityAndStartingEnergyBonus = 30,
        NextRunEnergyRegenIntervalReductionPercent = 31,
        NextRunHealthTradeoffReductionPercent = 32,
        NextRunPulseRadiusBonusPercent = 33,
        NextRunRevealHoldDurationBonusPercent = 34,
        NextRunSonarAndStickyEnergyCostPenalty = 35,
    }

    public sealed class ShopUpgradeDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public int Cost { get; set; }
        public ShopUpgradeEffectType EffectType { get; set; }
        public int EffectValue { get; set; }
        public ShopUpgradeEffectType SecondaryEffectType { get; set; }
        public int SecondaryEffectValue { get; set; }
        public ShopUpgradeEffectType TertiaryEffectType { get; set; }
        public int TertiaryEffectValue { get; set; }
        public string PurchaseGroupId { get; set; }
        public int MaxPurchaseCount { get; set; } = 1;
        public int MaxGroupPurchaseCount { get; set; } = 1;
        public bool IsPlaceholder { get; set; }
        public bool PersistsPurchase { get; set; } = true;
        public bool IsSingleRunTemporary { get; set; }
        public bool IsRiskySingleRunTemporary { get; set; }
        public bool IsMechanicChanging { get; set; }
    }

    public static class ShopUpgradeCatalog
    {
        public const int DefaultOfferCount = 3;

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
        const string LocatorSupportSenseGroupId = "group_locator_support_sense";
        const string LifeInsuranceGroupId = "group_temp_life_insurance";
        const string SurveyBurstGroupId = "group_temp_survey_burst";
        const string GoalRevealGroupId = "group_temp_goal_reveal";
        const string GreedyCoreGroupId = "group_temp_greedy_core";
        const string GlassBatteryGroupId = "group_temp_glass_battery";
        const string OverclockedSonarGroupId = "group_temp_overclocked_sonar";
        const string SonarExtraBounceGroupId = "group_sonar_extra_bounce";
        const string TeleportLandingPulseGroupId = "group_teleport_landing_pulse";
        const string EchoMemoryGroupId = "group_echo_memory";

        static readonly ShopUpgradeDefinition s_PlaceholderOffer = new()
        {
            Id = "upgrade_placeholder_trinket",
            DisplayName = "Useless Trinket",
            Description = "This item does absolutely nothing, but it will cost you 1G.",
            Cost = 1,
            EffectType = ShopUpgradeEffectType.None,
            EffectValue = 0,
            PurchaseGroupId = "group_placeholder",
            MaxPurchaseCount = int.MaxValue,
            MaxGroupPurchaseCount = int.MaxValue,
            IsPlaceholder = true,
            PersistsPurchase = false,
        };

        static readonly ShopUpgradeDefinition[] s_RealShopOffers =
        {
            new()
            {
                Id = "upgrade_reinforced_suit_alpha",
                DisplayName = "Reinforced Suit Tier B",
                Description = "+10 Max Health",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.MaxHealthBonus,
                EffectValue = 10,
                PurchaseGroupId = HealthGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_reinforced_suit_beta",
                DisplayName = "Reinforced Suit Tier A",
                Description = "+15 Max Health",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.MaxHealthBonus,
                EffectValue = 15,
                PurchaseGroupId = HealthGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_reinforced_suit_gamma",
                DisplayName = "Reinforced Suit Tier S",
                Description = "+20 Max Health",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.MaxHealthBonus,
                EffectValue = 20,
                PurchaseGroupId = HealthGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_extended_battery_alpha",
                DisplayName = "Extended Battery Tier B",
                Description = "+8 Max Energy",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.MaxEnergyBonus,
                EffectValue = 8,
                PurchaseGroupId = EnergyGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_extended_battery_beta",
                DisplayName = "Extended Battery Tier A",
                Description = "+12 Max Energy",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.MaxEnergyBonus,
                EffectValue = 12,
                PurchaseGroupId = EnergyGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_extended_battery_gamma",
                DisplayName = "Extended Battery Tier S",
                Description = "+15 Max Energy",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.MaxEnergyBonus,
                EffectValue = 15,
                PurchaseGroupId = EnergyGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_preparedness_alpha",
                DisplayName = "Preparedness Tier B",
                Description = "Start each run with +8 energy.",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.StartingEnergyBonus,
                EffectValue = 8,
                PurchaseGroupId = StartingEnergyGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_preparedness_beta",
                DisplayName = "Preparedness Tier A",
                Description = "Start each run with +10 energy.",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.StartingEnergyBonus,
                EffectValue = 10,
                PurchaseGroupId = StartingEnergyGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_preparedness_gamma",
                DisplayName = "Preparedness Tier S",
                Description = "Start each run with +12 energy.",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.StartingEnergyBonus,
                EffectValue = 12,
                PurchaseGroupId = StartingEnergyGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_rapid_recharge_alpha",
                DisplayName = "Rapid Recharge Tier B",
                Description = "Energy regenerates 10% faster.",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.EnergyRegenIntervalReductionPercent,
                EffectValue = 10,
                PurchaseGroupId = EnergyRegenGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_rapid_recharge_beta",
                DisplayName = "Rapid Recharge Tier A",
                Description = "Energy regenerates 15% faster.",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.EnergyRegenIntervalReductionPercent,
                EffectValue = 15,
                PurchaseGroupId = EnergyRegenGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_rapid_recharge_gamma",
                DisplayName = "Rapid Recharge Tier S",
                Description = "Energy regenerates 20% faster.",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.EnergyRegenIntervalReductionPercent,
                EffectValue = 20,
                PurchaseGroupId = EnergyRegenGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_second_wind_alpha",
                DisplayName = "Second Wind Tier B",
                Description = "Low-health recovery cap +8, regen speed +10%, regen delay -1s.",
                Cost = 2,
                EffectType = ShopUpgradeEffectType.HealthRegenCapBonus,
                EffectValue = 8,
                SecondaryEffectType = ShopUpgradeEffectType.HealthRegenIntervalReductionPercent,
                SecondaryEffectValue = 10,
                TertiaryEffectType = ShopUpgradeEffectType.HealthRegenDelayReductionSeconds,
                TertiaryEffectValue = 1,
                PurchaseGroupId = HealthRecoveryGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_second_wind_beta",
                DisplayName = "Second Wind Tier A",
                Description = "Low-health recovery cap +10, regen speed +15%, regen delay -2s.",
                Cost = 4,
                EffectType = ShopUpgradeEffectType.HealthRegenCapBonus,
                EffectValue = 10,
                SecondaryEffectType = ShopUpgradeEffectType.HealthRegenIntervalReductionPercent,
                SecondaryEffectValue = 15,
                TertiaryEffectType = ShopUpgradeEffectType.HealthRegenDelayReductionSeconds,
                TertiaryEffectValue = 2,
                PurchaseGroupId = HealthRecoveryGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_second_wind_gamma",
                DisplayName = "Second Wind Tier S",
                Description = "Low-health recovery cap +12, regen speed +20%, regen delay -3s.",
                Cost = 6,
                EffectType = ShopUpgradeEffectType.HealthRegenCapBonus,
                EffectValue = 12,
                SecondaryEffectType = ShopUpgradeEffectType.HealthRegenIntervalReductionPercent,
                SecondaryEffectValue = 20,
                TertiaryEffectType = ShopUpgradeEffectType.HealthRegenDelayReductionSeconds,
                TertiaryEffectValue = 3,
                PurchaseGroupId = HealthRecoveryGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_field_supplies_alpha",
                DisplayName = "Field Supplies Tier B",
                Description = "Refill stations gain +1 use. Cooldown -10%.",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.RefillStationExtraUses,
                EffectValue = 1,
                SecondaryEffectType = ShopUpgradeEffectType.RefillStationCooldownReductionPercent,
                SecondaryEffectValue = 10,
                PurchaseGroupId = RefillReserveGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_field_supplies_beta",
                DisplayName = "Field Supplies Tier A",
                Description = "Refill stations gain +1 use. Cooldown -15%.",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.RefillStationExtraUses,
                EffectValue = 1,
                SecondaryEffectType = ShopUpgradeEffectType.RefillStationCooldownReductionPercent,
                SecondaryEffectValue = 15,
                PurchaseGroupId = RefillReserveGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_field_supplies_gamma",
                DisplayName = "Field Supplies Tier S",
                Description = "Refill stations gain +1 use. Cooldown -25%.",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.RefillStationExtraUses,
                EffectValue = 1,
                SecondaryEffectType = ShopUpgradeEffectType.RefillStationCooldownReductionPercent,
                SecondaryEffectValue = 25,
                PurchaseGroupId = RefillReserveGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_sonar_efficiency_alpha",
                DisplayName = "Efficient Sonar Tier B",
                Description = "Sonar cost -1 energy",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.SonarCostReduction,
                EffectValue = 1,
                PurchaseGroupId = SonarGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_sonar_efficiency_beta",
                DisplayName = "Efficient Sonar Tier A",
                Description = "Sonar cost -2 energy",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.SonarCostReduction,
                EffectValue = 2,
                PurchaseGroupId = SonarGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_sonar_efficiency_gamma",
                DisplayName = "Efficient Sonar Tier S",
                Description = "Sonar cost -3 energy",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.SonarCostReduction,
                EffectValue = 3,
                PurchaseGroupId = SonarGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_extended_reveal_alpha",
                DisplayName = "Extended Reveal Tier B",
                Description = "+1s reveal duration",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.PulseRevealDurationBonusSeconds,
                EffectValue = 1,
                PurchaseGroupId = RevealGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_extended_reveal_beta",
                DisplayName = "Extended Reveal Tier A",
                Description = "+2s reveal duration",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.PulseRevealDurationBonusSeconds,
                EffectValue = 2,
                PurchaseGroupId = RevealGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_extended_reveal_gamma",
                DisplayName = "Extended Reveal Tier S",
                Description = "+3s reveal duration",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.PulseRevealDurationBonusSeconds,
                EffectValue = 3,
                PurchaseGroupId = RevealGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_wide_pulse_alpha",
                DisplayName = "Wide Pulse Tier B",
                Description = "+10% pulse radius",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.PulseRadiusBonusPercent,
                EffectValue = 10,
                PurchaseGroupId = PulseRadiusGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_wide_pulse_beta",
                DisplayName = "Wide Pulse Tier A",
                Description = "+15% pulse radius",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.PulseRadiusBonusPercent,
                EffectValue = 15,
                PurchaseGroupId = PulseRadiusGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_wide_pulse_gamma",
                DisplayName = "Wide Pulse Tier S",
                Description = "+20% pulse radius",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.PulseRadiusBonusPercent,
                EffectValue = 20,
                PurchaseGroupId = PulseRadiusGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_sticky_efficiency_alpha",
                DisplayName = "Efficient Sticky Pulse Tier B",
                Description = "Sticky Pulse cost -3 energy",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.StickyPulseCostReduction,
                EffectValue = 3,
                PurchaseGroupId = StickyGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_sticky_efficiency_beta",
                DisplayName = "Efficient Sticky Pulse Tier A",
                Description = "Sticky Pulse cost -4 energy",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.StickyPulseCostReduction,
                EffectValue = 4,
                PurchaseGroupId = StickyGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_sticky_efficiency_gamma",
                DisplayName = "Efficient Sticky Pulse Tier S",
                Description = "Sticky Pulse cost -5 energy",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.StickyPulseCostReduction,
                EffectValue = 5,
                PurchaseGroupId = StickyGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_sticky_overcharge",
                DisplayName = "Sticky Overcharge",
                Description = "Sticky Pulse emits one additional pulse before it expires.",
                Cost = 4,
                EffectType = ShopUpgradeEffectType.StickyPulseExtraPulseCount,
                EffectValue = 1,
                PurchaseGroupId = StickyOverchargeGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 1,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_locator_recharge_alpha",
                DisplayName = "Locator Recharge Tier B",
                Description = "Locator cooldown -10%",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.LocatorCooldownReductionPercent,
                EffectValue = 10,
                PurchaseGroupId = LocatorGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_locator_recharge_beta",
                DisplayName = "Locator Recharge Tier A",
                Description = "Locator cooldown -15%",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.LocatorCooldownReductionPercent,
                EffectValue = 15,
                PurchaseGroupId = LocatorGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_locator_recharge_gamma",
                DisplayName = "Locator Recharge Tier S",
                Description = "Locator cooldown -20%",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.LocatorCooldownReductionPercent,
                EffectValue = 20,
                PurchaseGroupId = LocatorGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_refill_boost_alpha",
                DisplayName = "Refill Boost Tier B",
                Description = "+15% refill station recovery",
                Cost = 1,
                EffectType = ShopUpgradeEffectType.RefillBoostPercent,
                EffectValue = 15,
                PurchaseGroupId = RefillBoostGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_refill_boost_beta",
                DisplayName = "Refill Boost Tier A",
                Description = "+25% refill station recovery",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.RefillBoostPercent,
                EffectValue = 25,
                PurchaseGroupId = RefillBoostGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_refill_boost_gamma",
                DisplayName = "Refill Boost Tier S",
                Description = "+30% refill station recovery",
                Cost = 5,
                EffectType = ShopUpgradeEffectType.RefillBoostPercent,
                EffectValue = 30,
                PurchaseGroupId = RefillBoostGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
            },
            new()
            {
                Id = "upgrade_treasure_sense_alpha",
                DisplayName = "Treasure Sense Tier B",
                Description = "Locator can now detect coins, and coin detection range increases by 4m.",
                Cost = 2,
                EffectType = ShopUpgradeEffectType.TreasureSenseRangeMeters,
                EffectValue = 4,
                PurchaseGroupId = TreasureSenseGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_locator_support_sense_alpha",
                DisplayName = "Recovery Sense Tier B",
                Description = "Locator can now detect active energy and health refill stations, and locator range increases by 4m.",
                Cost = 2,
                EffectType = ShopUpgradeEffectType.LocatorSupportSenseRangeMeters,
                EffectValue = 4,
                PurchaseGroupId = LocatorSupportSenseGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_locator_support_sense_beta",
                DisplayName = "Recovery Sense Tier A",
                Description = "Locator can now detect active energy and health refill stations, and locator range increases by 6m.",
                Cost = 4,
                EffectType = ShopUpgradeEffectType.LocatorSupportSenseRangeMeters,
                EffectValue = 6,
                PurchaseGroupId = LocatorSupportSenseGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_locator_support_sense_gamma",
                DisplayName = "Recovery Sense Tier S",
                Description = "Locator can now detect active energy and health refill stations, and locator range increases by 10m.",
                Cost = 6,
                EffectType = ShopUpgradeEffectType.LocatorSupportSenseRangeMeters,
                EffectValue = 10,
                PurchaseGroupId = LocatorSupportSenseGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_sonar_extra_bounce_alpha",
                DisplayName = "Echo Bounce Tier B",
                Description = "Unlocks one extra sonar bounce. The bonus bounce pulse reveals an additional 25% of normal range. Stacks with other Echo Bounce upgrades.",
                Cost = 2,
                EffectType = ShopUpgradeEffectType.SonarExtraBouncePulseRadiusPercent,
                EffectValue = 25,
                PurchaseGroupId = SonarExtraBounceGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_sonar_extra_bounce_beta",
                DisplayName = "Echo Bounce Tier A",
                Description = "Unlocks one extra sonar bounce. The bonus bounce pulse reveals an additional 35% of normal range. Stacks with other Echo Bounce upgrades.",
                Cost = 4,
                EffectType = ShopUpgradeEffectType.SonarExtraBouncePulseRadiusPercent,
                EffectValue = 35,
                PurchaseGroupId = SonarExtraBounceGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_sonar_extra_bounce_gamma",
                DisplayName = "Echo Bounce Tier S",
                Description = "Unlocks one extra sonar bounce. The bonus bounce pulse reveals an additional 40% of normal range. Stacks with other Echo Bounce upgrades.",
                Cost = 6,
                EffectType = ShopUpgradeEffectType.SonarExtraBouncePulseRadiusPercent,
                EffectValue = 40,
                PurchaseGroupId = SonarExtraBounceGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_teleport_landing_pulse_alpha",
                DisplayName = "Blink Echo Tier B",
                Description = "Teleport landing reveals a small area for 1s. Pulse size +20%. Stacks with other Blink Echo upgrades.",
                Cost = 2,
                EffectType = ShopUpgradeEffectType.TeleportLandingPulseRadiusPercent,
                EffectValue = 20,
                SecondaryEffectType = ShopUpgradeEffectType.TeleportLandingPulseRevealDurationSeconds,
                SecondaryEffectValue = 1,
                PurchaseGroupId = TeleportLandingPulseGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_teleport_landing_pulse_beta",
                DisplayName = "Blink Echo Tier A",
                Description = "Teleport landing reveals a small area for 1s. Pulse size +25%. Stacks with other Blink Echo upgrades.",
                Cost = 4,
                EffectType = ShopUpgradeEffectType.TeleportLandingPulseRadiusPercent,
                EffectValue = 25,
                SecondaryEffectType = ShopUpgradeEffectType.TeleportLandingPulseRevealDurationSeconds,
                SecondaryEffectValue = 1,
                PurchaseGroupId = TeleportLandingPulseGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_teleport_landing_pulse_gamma",
                DisplayName = "Blink Echo Tier S",
                Description = "Teleport landing reveals a small area for 1s. Pulse size +30%. Stacks with other Blink Echo upgrades.",
                Cost = 6,
                EffectType = ShopUpgradeEffectType.TeleportLandingPulseRadiusPercent,
                EffectValue = 30,
                SecondaryEffectType = ShopUpgradeEffectType.TeleportLandingPulseRevealDurationSeconds,
                SecondaryEffectValue = 1,
                PurchaseGroupId = TeleportLandingPulseGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_echo_memory_alpha",
                DisplayName = "Echo Memory Tier B",
                Description = "Leaves a fading trail behind you. Trail length +5m, memory duration +2s. Stacks with other Echo Memory upgrades.",
                Cost = 2,
                EffectType = ShopUpgradeEffectType.EchoMemoryTrailLengthMeters,
                EffectValue = 5,
                SecondaryEffectType = ShopUpgradeEffectType.EchoMemoryTrailDurationSeconds,
                SecondaryEffectValue = 2,
                PurchaseGroupId = EchoMemoryGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_echo_memory_beta",
                DisplayName = "Echo Memory Tier A",
                Description = "Leaves a fading trail behind you. Trail length +7m, memory duration +3s. Stacks with other Echo Memory upgrades.",
                Cost = 4,
                EffectType = ShopUpgradeEffectType.EchoMemoryTrailLengthMeters,
                EffectValue = 7,
                SecondaryEffectType = ShopUpgradeEffectType.EchoMemoryTrailDurationSeconds,
                SecondaryEffectValue = 3,
                PurchaseGroupId = EchoMemoryGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_echo_memory_gamma",
                DisplayName = "Echo Memory Tier S",
                Description = "Leaves a fading trail behind you. Trail length +8m, memory duration +5s. Stacks with other Echo Memory upgrades.",
                Cost = 6,
                EffectType = ShopUpgradeEffectType.EchoMemoryTrailLengthMeters,
                EffectValue = 8,
                SecondaryEffectType = ShopUpgradeEffectType.EchoMemoryTrailDurationSeconds,
                SecondaryEffectValue = 5,
                PurchaseGroupId = EchoMemoryGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_treasure_sense_beta",
                DisplayName = "Treasure Sense Tier A",
                Description = "Locator can now detect coins, and coin detection range increases by 6m.",
                Cost = 4,
                EffectType = ShopUpgradeEffectType.TreasureSenseRangeMeters,
                EffectValue = 6,
                PurchaseGroupId = TreasureSenseGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_treasure_sense_gamma",
                DisplayName = "Treasure Sense Tier S",
                Description = "Locator can now detect coins, and coin detection range increases by 10m.",
                Cost = 6,
                EffectType = ShopUpgradeEffectType.TreasureSenseRangeMeters,
                EffectValue = 10,
                PurchaseGroupId = TreasureSenseGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 3,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_life_insurance",
                DisplayName = "Life Insurance",
                Description = "The first lethal hit in your next run leaves you at 1 health.",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.NextRunLifeInsuranceCharge,
                EffectValue = 1,
                PurchaseGroupId = LifeInsuranceGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 1,
                PersistsPurchase = false,
                IsSingleRunTemporary = true,
            },
            new()
            {
                Id = "upgrade_survey_burst",
                DisplayName = "Survey Burst",
                Description = "Your next run begins with a massive pulse that reveals the maze for 20 seconds.",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.NextRunSurveyBurstCharge,
                EffectValue = 1,
                PurchaseGroupId = SurveyBurstGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 1,
                PersistsPurchase = false,
                IsSingleRunTemporary = true,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_goal_reveal_burst",
                DisplayName = "Exit Lock",
                Description = "Your next run begins with the goal highlighted for 10 seconds.",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.NextRunGoalRevealCharge,
                EffectValue = 1,
                PurchaseGroupId = GoalRevealGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 1,
                PersistsPurchase = false,
                IsSingleRunTemporary = true,
                IsMechanicChanging = true,
            },
            new()
            {
                Id = "upgrade_greedy_core",
                DisplayName = "Greedy Core",
                Description = "Next run only: a successful escape triples deposited gold. Drawback: refill stations lose 1 use and restore 20% less.",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.NextRunEscapeGoldBonusPercent,
                EffectValue = 200,
                SecondaryEffectType = ShopUpgradeEffectType.NextRunRefillStationUsePenalty,
                SecondaryEffectValue = 1,
                TertiaryEffectType = ShopUpgradeEffectType.NextRunRefillAmountPenaltyPercent,
                TertiaryEffectValue = 20,
                PurchaseGroupId = GreedyCoreGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 1,
                PersistsPurchase = false,
                IsSingleRunTemporary = true,
                IsRiskySingleRunTemporary = true,
            },
            new()
            {
                Id = "upgrade_glass_battery",
                DisplayName = "Glass Battery",
                Description = "Next run only: +50 max energy, +50 starting energy, and energy regenerates three times faster. Drawback: max health and low-health recovery cap -30%.",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.NextRunEnergyCapacityAndStartingEnergyBonus,
                EffectValue = 50,
                SecondaryEffectType = ShopUpgradeEffectType.NextRunEnergyRegenIntervalReductionPercent,
                SecondaryEffectValue = 67,
                TertiaryEffectType = ShopUpgradeEffectType.NextRunHealthTradeoffReductionPercent,
                TertiaryEffectValue = 30,
                PurchaseGroupId = GlassBatteryGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 1,
                PersistsPurchase = false,
                IsSingleRunTemporary = true,
                IsRiskySingleRunTemporary = true,
            },
            new()
            {
                Id = "upgrade_overclocked_sonar",
                DisplayName = "Overclocked Sonar",
                Description = "Next run only: pulse radius and reveal duration +30%. Drawback: Sonar and Sticky Pulse cost +5 energy.",
                Cost = 3,
                EffectType = ShopUpgradeEffectType.NextRunPulseRadiusBonusPercent,
                EffectValue = 30,
                SecondaryEffectType = ShopUpgradeEffectType.NextRunRevealHoldDurationBonusPercent,
                SecondaryEffectValue = 30,
                TertiaryEffectType = ShopUpgradeEffectType.NextRunSonarAndStickyEnergyCostPenalty,
                TertiaryEffectValue = 5,
                PurchaseGroupId = OverclockedSonarGroupId,
                MaxPurchaseCount = 1,
                MaxGroupPurchaseCount = 1,
                PersistsPurchase = false,
                IsSingleRunTemporary = true,
                IsRiskySingleRunTemporary = true,
            },
        };

        static readonly ShopUpgradeDefinition[] s_AllShopOffers = s_RealShopOffers
            .Concat(new[] { s_PlaceholderOffer })
            .ToArray();

        public static IReadOnlyList<ShopUpgradeDefinition> AllShopOffers => s_AllShopOffers;
        public static IReadOnlyList<ShopUpgradeDefinition> RealShopOffers => s_RealShopOffers;
        public static ShopUpgradeDefinition PlaceholderOffer => s_PlaceholderOffer;

        public static bool TryGetDefinition(string upgradeId, out ShopUpgradeDefinition definition)
        {
            definition = string.IsNullOrWhiteSpace(upgradeId)
                ? null
                : s_AllShopOffers.FirstOrDefault(candidate => string.Equals(candidate.Id, upgradeId, StringComparison.Ordinal));
            return definition != null;
        }

        public static int GetPurchaseCount(ProfileSaveData profile, string upgradeId)
        {
            if (profile?.purchasedUpgradeIds == null || string.IsNullOrWhiteSpace(upgradeId))
                return 0;

            int total = 0;
            for (int i = 0; i < profile.purchasedUpgradeIds.Count; i++)
            {
                if (string.Equals(profile.purchasedUpgradeIds[i], upgradeId, StringComparison.Ordinal))
                    total++;
            }

            return total;
        }

        public static int GetSingleRunHeldCount(ProfileSaveData profile, string upgradeId)
        {
            if (profile?.queuedSingleRunUpgradeIds == null || string.IsNullOrWhiteSpace(upgradeId))
                return 0;

            return CountMatchingUpgradeIds(profile.queuedSingleRunUpgradeIds, upgradeId);
        }

        public static int GetGroupPurchaseCount(ProfileSaveData profile, string purchaseGroupId)
        {
            if (profile?.purchasedUpgradeIds == null || string.IsNullOrWhiteSpace(purchaseGroupId))
                return 0;

            int total = 0;
            for (int i = 0; i < profile.purchasedUpgradeIds.Count; i++)
            {
                string purchasedId = profile.purchasedUpgradeIds[i];
                if (!TryGetDefinition(purchasedId, out ShopUpgradeDefinition definition))
                    continue;

                if (string.Equals(definition.PurchaseGroupId, purchaseGroupId, StringComparison.Ordinal))
                    total++;
            }

            return total;
        }

        public static int GetSingleRunGroupHeldCount(ProfileSaveData profile, string purchaseGroupId)
        {
            if (profile?.queuedSingleRunUpgradeIds == null || string.IsNullOrWhiteSpace(purchaseGroupId))
                return 0;

            return CountMatchingGroupIds(profile.queuedSingleRunUpgradeIds, purchaseGroupId);
        }

        public static bool CanPurchase(ProfileSaveData profile, string upgradeId)
        {
            return TryGetDefinition(upgradeId, out ShopUpgradeDefinition definition) && CanPurchase(profile, definition);
        }

        public static bool CanPurchase(ProfileSaveData profile, ShopUpgradeDefinition definition)
        {
            if (definition == null)
                return false;

            if (definition.IsPlaceholder)
                return true;

            int itemPurchaseCount = definition.IsSingleRunTemporary
                ? GetSingleRunHeldCount(profile, definition.Id)
                : GetPurchaseCount(profile, definition.Id);
            if (itemPurchaseCount >= Mathf.Max(1, definition.MaxPurchaseCount))
                return false;

            if (!string.IsNullOrWhiteSpace(definition.PurchaseGroupId) && definition.MaxGroupPurchaseCount < int.MaxValue)
            {
                int groupPurchaseCount = definition.IsSingleRunTemporary
                    ? GetSingleRunGroupHeldCount(profile, definition.PurchaseGroupId)
                    : GetGroupPurchaseCount(profile, definition.PurchaseGroupId);
                if (groupPurchaseCount >= Mathf.Max(1, definition.MaxGroupPurchaseCount))
                    return false;
            }

            return true;
        }

        public static List<string> GenerateOfferIds(ProfileSaveData profile, int offerCount, int randomSeed)
        {
            int clampedOfferCount = Mathf.Max(1, offerCount);
            System.Random random = new(randomSeed);
            List<ShopUpgradeDefinition> candidates = s_RealShopOffers
                .Where(candidate => CanPurchase(profile, candidate))
                .OrderBy(_ => random.Next())
                .ToList();

            List<string> selectedOfferIds = new(clampedOfferCount);
            Dictionary<string, int> selectedGroupCounts = new(StringComparer.Ordinal);

            for (int i = 0; i < candidates.Count && selectedOfferIds.Count < clampedOfferCount; i++)
            {
                ShopUpgradeDefinition candidate = candidates[i];
                if (!CanOfferAnotherCopyFromGroup(profile, candidate, selectedGroupCounts))
                    continue;

                selectedOfferIds.Add(candidate.Id);
                TrackSelectedGroup(candidate, selectedGroupCounts);
            }

            while (selectedOfferIds.Count < clampedOfferCount)
                selectedOfferIds.Add(s_PlaceholderOffer.Id);

            return selectedOfferIds;
        }

        public static int GetTotalEffectValue(ProfileSaveData profile, ShopUpgradeEffectType effectType)
        {
            if (profile == null || profile.purchasedUpgradeIds == null || profile.purchasedUpgradeIds.Count == 0)
                return 0;

            int total = 0;
            for (int i = 0; i < profile.purchasedUpgradeIds.Count; i++)
            {
                string purchasedId = profile.purchasedUpgradeIds[i];
                if (!TryGetDefinition(purchasedId, out ShopUpgradeDefinition definition))
                    continue;

                total += definition.GetEffectValue(effectType);
            }

            return total;
        }

        public static int GetTotalEffectValueForUpgradeIds(IReadOnlyList<string> upgradeIds, ShopUpgradeEffectType effectType)
        {
            if (upgradeIds == null || upgradeIds.Count == 0)
                return 0;

            int total = 0;
            for (int i = 0; i < upgradeIds.Count; i++)
            {
                string upgradeId = upgradeIds[i];
                if (!TryGetDefinition(upgradeId, out ShopUpgradeDefinition definition))
                    continue;

                total += definition.GetEffectValue(effectType);
            }

            return total;
        }

        public static List<string> BuildFullyPurchasedPersistentUpgradeIds()
        {
            List<string> purchasedIds = new();
            Dictionary<string, int> remainingGroupCapacities = new(StringComparer.Ordinal);

            for (int i = 0; i < s_RealShopOffers.Length; i++)
            {
                ShopUpgradeDefinition definition = s_RealShopOffers[i];
                if (definition == null || definition.IsPlaceholder || !definition.PersistsPurchase)
                    continue;

                int maxPerItem = Mathf.Max(1, definition.MaxPurchaseCount);
                int remainingItemCount = maxPerItem;

                if (!string.IsNullOrWhiteSpace(definition.PurchaseGroupId) && definition.MaxGroupPurchaseCount < int.MaxValue)
                {
                    if (!remainingGroupCapacities.TryGetValue(definition.PurchaseGroupId, out int remainingGroupCapacity))
                        remainingGroupCapacity = Mathf.Max(1, definition.MaxGroupPurchaseCount);

                    remainingItemCount = Mathf.Min(remainingItemCount, remainingGroupCapacity);
                    remainingGroupCapacities[definition.PurchaseGroupId] = Mathf.Max(0, remainingGroupCapacity - remainingItemCount);
                }

                for (int purchaseIndex = 0; purchaseIndex < remainingItemCount; purchaseIndex++)
                    purchasedIds.Add(definition.Id);
            }

            return purchasedIds;
        }

        public static List<string> BuildFullyHeldSingleRunUpgradeIds()
        {
            List<string> heldIds = new();
            Dictionary<string, int> remainingGroupCapacities = new(StringComparer.Ordinal);

            for (int i = 0; i < s_RealShopOffers.Length; i++)
            {
                ShopUpgradeDefinition definition = s_RealShopOffers[i];
                if (definition == null || definition.IsPlaceholder || !definition.IsSingleRunTemporary)
                    continue;

                int maxPerItem = Mathf.Max(1, definition.MaxPurchaseCount);
                int remainingItemCount = maxPerItem;

                if (!string.IsNullOrWhiteSpace(definition.PurchaseGroupId) && definition.MaxGroupPurchaseCount < int.MaxValue)
                {
                    if (!remainingGroupCapacities.TryGetValue(definition.PurchaseGroupId, out int remainingGroupCapacity))
                        remainingGroupCapacity = Mathf.Max(1, definition.MaxGroupPurchaseCount);

                    remainingItemCount = Mathf.Min(remainingItemCount, remainingGroupCapacity);
                    remainingGroupCapacities[definition.PurchaseGroupId] = Mathf.Max(0, remainingGroupCapacity - remainingItemCount);
                }

                for (int holdIndex = 0; holdIndex < remainingItemCount; holdIndex++)
                    heldIds.Add(definition.Id);
            }

            return heldIds;
        }

        static bool CanOfferAnotherCopyFromGroup(
            ProfileSaveData profile,
            ShopUpgradeDefinition definition,
            IReadOnlyDictionary<string, int> selectedGroupCounts)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.PurchaseGroupId))
                return true;

            int remainingCapacity = definition.MaxGroupPurchaseCount >= int.MaxValue
                ? int.MaxValue
                : Mathf.Max(
                    0,
                    definition.MaxGroupPurchaseCount - (definition.IsSingleRunTemporary
                        ? GetSingleRunGroupHeldCount(profile, definition.PurchaseGroupId)
                        : GetGroupPurchaseCount(profile, definition.PurchaseGroupId)));

            if (remainingCapacity <= 0)
                return false;

            int alreadySelectedFromGroup = selectedGroupCounts != null
                && selectedGroupCounts.TryGetValue(definition.PurchaseGroupId, out int count)
                    ? count
                    : 0;

            return alreadySelectedFromGroup < remainingCapacity;
        }

        static void TrackSelectedGroup(ShopUpgradeDefinition definition, IDictionary<string, int> selectedGroupCounts)
        {
            if (definition == null || selectedGroupCounts == null || string.IsNullOrWhiteSpace(definition.PurchaseGroupId))
                return;

            selectedGroupCounts.TryGetValue(definition.PurchaseGroupId, out int count);
            selectedGroupCounts[definition.PurchaseGroupId] = count + 1;
        }

        static int CountMatchingUpgradeIds(IReadOnlyList<string> upgradeIds, string upgradeId)
        {
            if (upgradeIds == null || string.IsNullOrWhiteSpace(upgradeId))
                return 0;

            int total = 0;
            for (int i = 0; i < upgradeIds.Count; i++)
            {
                if (string.Equals(upgradeIds[i], upgradeId, StringComparison.Ordinal))
                    total++;
            }

            return total;
        }

        static int CountMatchingGroupIds(IReadOnlyList<string> upgradeIds, string purchaseGroupId)
        {
            if (upgradeIds == null || string.IsNullOrWhiteSpace(purchaseGroupId))
                return 0;

            int total = 0;
            for (int i = 0; i < upgradeIds.Count; i++)
            {
                string upgradeId = upgradeIds[i];
                if (!TryGetDefinition(upgradeId, out ShopUpgradeDefinition definition))
                    continue;

                if (string.Equals(definition.PurchaseGroupId, purchaseGroupId, StringComparison.Ordinal))
                    total++;
            }

            return total;
        }
    }

    public static class ShopUpgradeDefinitionExtensions
    {
        public static int GetEffectValue(this ShopUpgradeDefinition definition, ShopUpgradeEffectType effectType)
        {
            if (definition == null || effectType == ShopUpgradeEffectType.None)
                return 0;

            int total = 0;
            if (definition.EffectType == effectType)
                total += definition.EffectValue;

            if (definition.SecondaryEffectType == effectType)
                total += definition.SecondaryEffectValue;

            if (definition.TertiaryEffectType == effectType)
                total += definition.TertiaryEffectValue;

            return total;
        }
    }
}
