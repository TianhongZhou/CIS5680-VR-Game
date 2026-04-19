using System;
using System.Collections.Generic;
using CIS5680VRGame.Balls;
using CIS5680VRGame.Gameplay;
using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.SceneManagement;

namespace CIS5680VRGame.Progression
{
    public sealed class ProfileUpgradeRuntimeApplier : MonoBehaviour
    {
        const string Maze1SceneName = "Maze1";
        const string RandomMazeSceneName = "random-maze";
        const float SurveyBurstRevealDurationSeconds = 20f;
        const float GoalRevealDurationSeconds = 10f;
        const float SurveyBurstMinimumRadius = 60f;
        const float SurveyBurstRadiusPadding = 4f;

        static ProfileUpgradeRuntimeApplier s_Instance;

        readonly System.Collections.Generic.List<string> m_ActiveSingleRunUpgradeIds = new();
        string m_LastLoadedSceneName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            GameObject root = new("ProfileUpgradeRuntimeApplier");
            s_Instance = root.AddComponent<ProfileUpgradeRuntimeApplier>();
        }

        public static int GetActiveSingleRunEffectValue(ShopUpgradeEffectType effectType)
        {
            EnsureCreated();
            if (s_Instance == null)
                return 0;

            return ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(s_Instance.m_ActiveSingleRunUpgradeIds, effectType);
        }

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            UpdateSingleRunUpgradeState(scene.name);

            if (!ShouldApplyInScene(scene.name))
                return;

            ApplyCurrentProfileUpgrades();
        }

        static bool ShouldApplyInScene(string sceneName)
        {
            return string.Equals(sceneName, Maze1SceneName, StringComparison.Ordinal)
                || string.Equals(sceneName, RandomMazeSceneName, StringComparison.Ordinal);
        }

        void UpdateSingleRunUpgradeState(string sceneName)
        {
            bool isRandomMaze = string.Equals(sceneName, RandomMazeSceneName, StringComparison.Ordinal);
            bool wasRandomMaze = string.Equals(m_LastLoadedSceneName, RandomMazeSceneName, StringComparison.Ordinal);

            if (!isRandomMaze || wasRandomMaze)
            {
                m_ActiveSingleRunUpgradeIds.Clear();
            }
            else
            {
                m_ActiveSingleRunUpgradeIds.Clear();
                m_ActiveSingleRunUpgradeIds.AddRange(ProfileService.ConsumeQueuedSingleRunUpgradesForRandomMazeEntry());
            }

            m_LastLoadedSceneName = sceneName;
        }

        void ApplyCurrentProfileUpgrades()
        {
            if (!ProfileService.TryGetCurrentProfile(out ProfileSaveData profile) || profile == null)
                return;

            int healthBonus = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.MaxHealthBonus);
            int energyBonus = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.MaxEnergyBonus);
            int startingEnergyBonus = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.StartingEnergyBonus);
            int energyRegenIntervalReductionPercent = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.EnergyRegenIntervalReductionPercent);
            int sonarCostReduction = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.SonarCostReduction);
            int stickyPulseCostReduction = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.StickyPulseCostReduction);
            int stickyPulseExtraPulseCount = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.StickyPulseExtraPulseCount);
            int revealDurationBonusSeconds = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.PulseRevealDurationBonusSeconds);
            int pulseRadiusBonusPercent = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.PulseRadiusBonusPercent);
            int sonarExtraBouncePulseRadiusPercent = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.SonarExtraBouncePulseRadiusPercent);
            int teleportLandingPulseRadiusPercent = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.TeleportLandingPulseRadiusPercent);
            int teleportLandingPulseRevealDurationSeconds = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.TeleportLandingPulseRevealDurationSeconds);
            int echoMemoryTrailLengthMeters = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.EchoMemoryTrailLengthMeters);
            int echoMemoryTrailDurationSeconds = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.EchoMemoryTrailDurationSeconds);
            int locatorCooldownReductionPercent = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.LocatorCooldownReductionPercent);
            int refillBoostPercent = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.RefillBoostPercent);
            int refillStationExtraUses = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.RefillStationExtraUses);
            int refillStationCooldownReductionPercent = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.RefillStationCooldownReductionPercent);
            int healthRegenCapBonus = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.HealthRegenCapBonus);
            int healthRegenIntervalReductionPercent = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.HealthRegenIntervalReductionPercent);
            int healthRegenDelayReductionSeconds = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.HealthRegenDelayReductionSeconds);
            int treasureSenseRangeMeters = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.TreasureSenseRangeMeters);
            int locatorSupportSenseRangeMeters = ShopUpgradeCatalog.GetTotalEffectValue(profile, ShopUpgradeEffectType.LocatorSupportSenseRangeMeters);
            int lifeInsuranceCharges = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunLifeInsuranceCharge);
            int surveyBurstCharges = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunSurveyBurstCharge);
            int goalRevealCharges = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunGoalRevealCharge);
            int glassBatteryEnergyBonus = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunEnergyCapacityAndStartingEnergyBonus);
            int glassBatteryEnergyRegenReductionPercent = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunEnergyRegenIntervalReductionPercent);
            int glassBatteryHealthTradeoffReductionPercent = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunHealthTradeoffReductionPercent);
            int overclockedPulseRadiusBonusPercent = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunPulseRadiusBonusPercent);
            int overclockedRevealHoldDurationBonusPercent = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunRevealHoldDurationBonusPercent);
            int overclockedSonarAndStickyEnergyCostPenalty = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunSonarAndStickyEnergyCostPenalty);
            int greedyCoreRefillUsePenalty = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunRefillStationUsePenalty);
            int greedyCoreRefillAmountPenaltyPercent = ShopUpgradeCatalog.GetTotalEffectValueForUpgradeIds(
                m_ActiveSingleRunUpgradeIds,
                ShopUpgradeEffectType.NextRunRefillAmountPenaltyPercent);

            PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ApplyPersistentMaxHealthBonus(healthBonus);
                playerHealth.ApplyPersistentHealthRegenCapBonus(healthRegenCapBonus);
                playerHealth.ApplyPersistentHealthRegenIntervalReductionPercent(healthRegenIntervalReductionPercent);
                playerHealth.ApplyPersistentHealthRegenDelayReductionSeconds(healthRegenDelayReductionSeconds);
                playerHealth.ApplySingleRunLifeInsuranceCharges(lifeInsuranceCharges);
                playerHealth.ApplySingleRunHealthTradeoffReductionPercent(glassBatteryHealthTradeoffReductionPercent);
            }

            PlayerEnergy playerEnergy = FindObjectOfType<PlayerEnergy>();
            if (playerEnergy != null)
            {
                playerEnergy.ApplyPersistentMaxEnergyBonus(energyBonus + glassBatteryEnergyBonus, restoreToFull: false);
                playerEnergy.ApplyPersistentStartingEnergyBonus(startingEnergyBonus + glassBatteryEnergyBonus);
                playerEnergy.ApplyPersistentEnergyRegenIntervalReductionPercent(
                    energyRegenIntervalReductionPercent + glassBatteryEnergyRegenReductionPercent);
            }

            XROrigin playerRig = FindObjectOfType<XROrigin>();
            if (playerRig != null)
            {
                EchoMemoryTrailRenderer echoMemoryTrail = playerRig.GetComponent<EchoMemoryTrailRenderer>();
                if (echoMemoryTrail == null)
                    echoMemoryTrail = playerRig.gameObject.AddComponent<EchoMemoryTrailRenderer>();

                echoMemoryTrail.ApplyPersistentTrailSettings(echoMemoryTrailLengthMeters, echoMemoryTrailDurationSeconds);
            }

            PulseManager pulseManager = PulseManager.Instance != null ? PulseManager.Instance : FindObjectOfType<PulseManager>();
            if (pulseManager != null)
            {
                pulseManager.ApplyPersistentRevealHoldDurationBonus(revealDurationBonusSeconds);
                pulseManager.ApplySingleRunRevealHoldDurationBonusPercent(overclockedRevealHoldDurationBonusPercent);
                if (surveyBurstCharges > 0 && string.Equals(SceneManager.GetActiveScene().name, RandomMazeSceneName, StringComparison.Ordinal))
                    TriggerSurveyBurst(pulseManager);
            }

            RefillStationLocatorGuidance locatorGuidance = FindObjectOfType<RefillStationLocatorGuidance>(true);
            if (locatorGuidance != null)
            {
                locatorGuidance.ApplyPersistentCooldownReductionPercent(locatorCooldownReductionPercent);
                locatorGuidance.ApplyPersistentLocatorSupportSenseRangeMeters(locatorSupportSenseRangeMeters);
                locatorGuidance.ApplyPersistentCoinSenseRangeMeters(treasureSenseRangeMeters);
                if (goalRevealCharges > 0 && string.Equals(SceneManager.GetActiveScene().name, RandomMazeSceneName, StringComparison.Ordinal))
                    locatorGuidance.RevealGoalMarkersForDuration(GoalRevealDurationSeconds);
            }

            BallHolsterSlot[] holsterSlots = FindObjectsOfType<BallHolsterSlot>(true);
            for (int i = 0; i < holsterSlots.Length; i++)
            {
                BallHolsterSlot holsterSlot = holsterSlots[i];
                if (holsterSlot == null)
                    continue;

                int modifier = holsterSlot.BallType switch
                {
                    BallType.Sonar => -Mathf.Max(0, sonarCostReduction) + Mathf.Max(0, overclockedSonarAndStickyEnergyCostPenalty),
                    BallType.StickyPulse => -Mathf.Max(0, stickyPulseCostReduction) + Mathf.Max(0, overclockedSonarAndStickyEnergyCostPenalty),
                    _ => 0,
                };
                holsterSlot.SetPersistentEnergyCostModifier(modifier);
            }

            SonarPulseImpactEffect[] sonarPulseEffects = FindObjectsOfType<SonarPulseImpactEffect>(true);
            for (int i = 0; i < sonarPulseEffects.Length; i++)
            {
                if (sonarPulseEffects[i] != null)
                {
                    sonarPulseEffects[i].ApplyPersistentPulseRadiusBonusPercent(pulseRadiusBonusPercent);
                    sonarPulseEffects[i].ApplySingleRunPulseRadiusBonusPercent(overclockedPulseRadiusBonusPercent);
                    sonarPulseEffects[i].ApplyPersistentExtraBouncePulseRadiusPercent(sonarExtraBouncePulseRadiusPercent);
                }
            }

            StickyPulseImpactEffect[] stickyPulseEffects = FindObjectsOfType<StickyPulseImpactEffect>(true);
            for (int i = 0; i < stickyPulseEffects.Length; i++)
            {
                if (stickyPulseEffects[i] != null)
                {
                    stickyPulseEffects[i].ApplyPersistentPulseRadiusBonusPercent(pulseRadiusBonusPercent);
                    stickyPulseEffects[i].ApplySingleRunPulseRadiusBonusPercent(overclockedPulseRadiusBonusPercent);
                    stickyPulseEffects[i].ApplyPersistentExtraPulseCount(stickyPulseExtraPulseCount);
                }
            }

            TeleportImpactEffect[] teleportEffects = FindObjectsOfType<TeleportImpactEffect>(true);
            for (int i = 0; i < teleportEffects.Length; i++)
            {
                if (teleportEffects[i] != null)
                {
                    teleportEffects[i].ApplyPersistentLandingPulseRadiusPercent(teleportLandingPulseRadiusPercent);
                    teleportEffects[i].ApplyPersistentLandingPulseRevealDurationSeconds(teleportLandingPulseRevealDurationSeconds);
                    teleportEffects[i].ApplySingleRunLandingPulseRevealDurationBonusPercent(overclockedRevealHoldDurationBonusPercent);
                }
            }

            BallRefillStation[] ballRefillStations = FindObjectsOfType<BallRefillStation>();
            for (int i = 0; i < ballRefillStations.Length; i++)
            {
                if (ballRefillStations[i] != null)
                {
                    ballRefillStations[i].SetPersistentChargeBonus(refillStationExtraUses);
                    ballRefillStations[i].SetPersistentCooldownReductionPercent(refillStationCooldownReductionPercent);
                    ballRefillStations[i].SetPersistentRefillBonusPercent(refillBoostPercent);
                    ballRefillStations[i].SetSingleRunChargePenalty(greedyCoreRefillUsePenalty);
                    ballRefillStations[i].SetSingleRunRefillMultiplierPercent(Mathf.Clamp(100 - greedyCoreRefillAmountPenaltyPercent, 1, 1000));
                }
            }

            HealthRefillStation[] healthRefillStations = FindObjectsOfType<HealthRefillStation>();
            for (int i = 0; i < healthRefillStations.Length; i++)
            {
                if (healthRefillStations[i] != null)
                {
                    healthRefillStations[i].SetPersistentUseBonus(refillStationExtraUses);
                    healthRefillStations[i].SetPersistentCooldownReductionPercent(refillStationCooldownReductionPercent);
                    healthRefillStations[i].SetPersistentRestoreBonusPercent(refillBoostPercent);
                    healthRefillStations[i].SetSingleRunUsePenalty(greedyCoreRefillUsePenalty);
                    healthRefillStations[i].SetSingleRunRestoreMultiplierPercent(Mathf.Clamp(100 - greedyCoreRefillAmountPenaltyPercent, 1, 1000));
                }
            }
        }

        void TriggerSurveyBurst(PulseManager pulseManager)
        {
            if (pulseManager == null)
                return;

            XROrigin playerRig = FindObjectOfType<XROrigin>();
            Vector3 origin = playerRig != null ? playerRig.transform.position : Vector3.zero;
            float radius = ComputeSurveyBurstRadius(origin, playerRig);
            pulseManager.SpawnPulse(origin, Vector3.up, radius, null, SurveyBurstRevealDurationSeconds);
            PulseAudioService.PlayPulse(origin);
        }

        float ComputeSurveyBurstRadius(Vector3 origin, XROrigin playerRig)
        {
            Collider[] colliders = FindObjectsOfType<Collider>(true);
            bool foundAny = false;
            float maxRadius = 0f;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    continue;

                if (playerRig != null && collider.transform.IsChildOf(playerRig.transform))
                    continue;

                if (collider.GetComponentInParent<Canvas>() != null)
                    continue;

                Bounds bounds = collider.bounds;
                if (bounds.extents.sqrMagnitude <= 0.0001f)
                    continue;

                foundAny = true;
                foreach (Vector3 corner in EnumerateBoundsCorners(bounds))
                {
                    float distance = Vector3.Distance(origin, corner);
                    if (distance > maxRadius)
                        maxRadius = distance;
                }
            }

            if (!foundAny)
                return SurveyBurstMinimumRadius;

            return Mathf.Max(SurveyBurstMinimumRadius, maxRadius + SurveyBurstRadiusPadding);
        }

        static IEnumerable<Vector3> EnumerateBoundsCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            yield return new Vector3(min.x, min.y, min.z);
            yield return new Vector3(min.x, min.y, max.z);
            yield return new Vector3(min.x, max.y, min.z);
            yield return new Vector3(min.x, max.y, max.z);
            yield return new Vector3(max.x, min.y, min.z);
            yield return new Vector3(max.x, min.y, max.z);
            yield return new Vector3(max.x, max.y, min.z);
            yield return new Vector3(max.x, max.y, max.z);
        }
    }
}
