using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CIS5680VRGame.Progression
{
    public sealed class ProfileService : MonoBehaviour
    {
        const string SaveFileName = "profile-save.json";
        const int DefaultShopRefreshCost = 1;
        const string TutorialSceneName = "TutorialLevel";
        const string Maze1SceneName = "Maze1";
        const string RandomMazeSceneName = "random-maze";

        static ProfileService s_Instance;
        static ProfileSaveData s_CurrentProfile;
        static string s_PendingNewGameSceneName;
        static bool s_HasAttemptedInitialLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        public const int CurrentSaveVersion = 1;

        public static bool HasSaveData
        {
            get
            {
                EnsureCreated();
                return s_CurrentProfile != null && !string.IsNullOrWhiteSpace(s_CurrentProfile.continueSceneName);
            }
        }

        public static string SaveFilePath => ResolveSaveFilePath();

        public static void BeginNewGameOnSceneLoad(string targetSceneName)
        {
            EnsureCreated();
            s_Instance.QueueNewGameInitialization(targetSceneName);
        }

        public static bool TryGetCurrentProfile(out ProfileSaveData profile)
        {
            EnsureCreated();
            profile = s_CurrentProfile;
            return profile != null;
        }

        public static bool TryGetContinueSceneName(out string continueSceneName)
        {
            EnsureCreated();
            continueSceneName = null;
            if (s_CurrentProfile == null || string.IsNullOrWhiteSpace(s_CurrentProfile.continueSceneName))
                return false;

            continueSceneName = s_CurrentProfile.continueSceneName;
            return true;
        }

        public static int GetTotalGold()
        {
            EnsureCreated();
            return s_CurrentProfile != null ? Mathf.Max(0, s_CurrentProfile.totalGold) : 0;
        }

        public static void AddGold(int amount)
        {
            if (amount <= 0)
                return;

            EnsureCreated();
            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            profile.totalGold = Mathf.Max(0, profile.totalGold) + amount;
            s_Instance.SaveNowInternal();
        }

        public static bool CanAfford(int amount)
        {
            EnsureCreated();
            if (amount <= 0)
                return true;

            return s_CurrentProfile != null && s_CurrentProfile.totalGold >= amount;
        }

        public static bool SpendGold(int amount)
        {
            if (amount <= 0)
                return true;

            EnsureCreated();
            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            if (profile.totalGold < amount)
                return false;

            profile.totalGold -= amount;
            s_Instance.SaveNowInternal();
            return true;
        }

        public static bool HasUpgrade(string upgradeId)
        {
            EnsureCreated();
            if (s_CurrentProfile == null || string.IsNullOrWhiteSpace(upgradeId))
                return false;

            return GetUpgradePurchaseCount(upgradeId) > 0;
        }

        public static int GetUpgradePurchaseCount(string upgradeId)
        {
            EnsureCreated();
            return ShopUpgradeCatalog.GetPurchaseCount(s_CurrentProfile, upgradeId);
        }

        public static bool CanPurchaseUpgrade(string upgradeId)
        {
            EnsureCreated();
            return ShopUpgradeCatalog.CanPurchase(s_CurrentProfile, upgradeId);
        }

        public static bool UnlockUpgrade(string upgradeId)
        {
            EnsureCreated();
            if (string.IsNullOrWhiteSpace(upgradeId))
                return false;

            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            if (!ShopUpgradeCatalog.TryGetDefinition(upgradeId, out ShopUpgradeDefinition definition))
                return false;

            if (!definition.PersistsPurchase || !ShopUpgradeCatalog.CanPurchase(profile, definition))
                return false;

            profile.purchasedUpgradeIds.Add(definition.Id);
            s_Instance.SaveNowInternal();
            return true;
        }

        public static bool TryPurchaseUpgrade(string upgradeId, int cost)
        {
            EnsureCreated();
            if (string.IsNullOrWhiteSpace(upgradeId) || cost < 0)
                return false;

            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            if (!ShopUpgradeCatalog.TryGetDefinition(upgradeId, out ShopUpgradeDefinition definition))
                return false;

            int effectiveCost = Mathf.Max(0, definition.Cost);
            if (profile.totalGold < effectiveCost)
                return false;

            if (!ShopUpgradeCatalog.CanPurchase(profile, definition))
                return false;

            profile.totalGold -= effectiveCost;
            if (definition.IsSingleRunTemporary)
                profile.queuedSingleRunUpgradeIds.Add(definition.Id);
            else if (definition.PersistsPurchase)
                profile.purchasedUpgradeIds.Add(definition.Id);

            s_Instance.SaveNowInternal();
            return true;
        }

        public static IReadOnlyList<string> ConsumeQueuedSingleRunUpgradesForRandomMazeEntry()
        {
            EnsureCreated();
            return s_Instance.ConsumeQueuedSingleRunUpgradesForRandomMazeEntryInternal();
        }

        public static IReadOnlyList<string> GetOrCreateCurrentShopOfferIds(int offerCount = ShopUpgradeCatalog.DefaultOfferCount)
        {
            EnsureCreated();
            return s_Instance.GetOrCreateCurrentShopOfferIdsInternal(offerCount);
        }

        public static IReadOnlyList<string> RefreshShopOffers(int offerCount = ShopUpgradeCatalog.DefaultOfferCount)
        {
            EnsureCreated();
            return s_Instance.RefreshShopOffersInternal(offerCount);
        }

        public static int GetCurrentShopRefreshCost()
        {
            EnsureCreated();
            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            int configuredCost = Mathf.Max(0, profile.shopState.refreshCost);
            if (profile.shopState.freeRefreshCount > 0)
                return 0;

            return configuredCost;
        }

        public static bool CanRefreshShopOffers()
        {
            EnsureCreated();
            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            int currentCost = GetCurrentShopRefreshCost();
            return profile.shopState.freeRefreshCount > 0 || profile.totalGold >= currentCost;
        }

        public static bool TryRefreshShopOffers(int offerCount = ShopUpgradeCatalog.DefaultOfferCount)
        {
            EnsureCreated();
            return s_Instance.TryRefreshShopOffersInternal(offerCount);
        }

        public static bool IsCurrentShopOfferConsumed(int offerIndex)
        {
            EnsureCreated();
            if (s_CurrentProfile?.shopState?.consumedOfferIndices == null || offerIndex < 0)
                return false;

            return s_CurrentProfile.shopState.consumedOfferIndices.Contains(offerIndex);
        }

        public static bool TryPurchaseCurrentShopOffer(string upgradeId, int offerIndex)
        {
            EnsureCreated();
            if (offerIndex < 0 || string.IsNullOrWhiteSpace(upgradeId))
                return false;

            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            if (profile.shopState?.currentOfferIds == null || offerIndex >= profile.shopState.currentOfferIds.Count)
                return false;

            if (!string.Equals(profile.shopState.currentOfferIds[offerIndex], upgradeId, StringComparison.Ordinal))
                return false;

            if (profile.shopState.consumedOfferIndices.Contains(offerIndex))
                return false;

            if (!ShopUpgradeCatalog.TryGetDefinition(upgradeId, out ShopUpgradeDefinition definition))
                return false;

            int effectiveCost = Mathf.Max(0, definition.Cost);
            if (profile.totalGold < effectiveCost)
                return false;

            if (!ShopUpgradeCatalog.CanPurchase(profile, definition))
                return false;

            profile.totalGold -= effectiveCost;
            if (definition.IsSingleRunTemporary)
                profile.queuedSingleRunUpgradeIds.Add(definition.Id);
            else if (definition.PersistsPurchase)
                profile.purchasedUpgradeIds.Add(definition.Id);

            profile.shopState.consumedOfferIndices.Add(offerIndex);
            s_Instance.SaveNowInternal();
            return true;
        }

        public static void DebugSetTotalGold(int totalGold)
        {
            EnsureCreated();
            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            profile.totalGold = Mathf.Max(0, totalGold);
            s_Instance.SaveNowInternal();
        }

        public static void DebugClearAllPurchasedUpgrades()
        {
            EnsureCreated();
            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            profile.purchasedUpgradeIds.Clear();
            profile.queuedSingleRunUpgradeIds.Clear();
            profile.shopState.currentOfferIds.Clear();
            profile.shopState.consumedOfferIndices.Clear();
            s_Instance.SaveNowInternal();
        }

        public static void DebugUnlockAllPurchasableUpgrades()
        {
            EnsureCreated();
            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            profile.purchasedUpgradeIds.Clear();
            profile.purchasedUpgradeIds.AddRange(ShopUpgradeCatalog.BuildFullyPurchasedPersistentUpgradeIds());
            profile.queuedSingleRunUpgradeIds.Clear();
            profile.queuedSingleRunUpgradeIds.AddRange(ShopUpgradeCatalog.BuildFullyHeldSingleRunUpgradeIds());
            profile.shopState.currentOfferIds.Clear();
            profile.shopState.consumedOfferIndices.Clear();
            s_Instance.SaveNowInternal();
        }

        public static bool DebugForceShopOffer(string upgradeId, int offerCount = ShopUpgradeCatalog.DefaultOfferCount)
        {
            EnsureCreated();
            if (!ShopUpgradeCatalog.TryGetDefinition(upgradeId, out ShopUpgradeDefinition forcedDefinition))
                return false;

            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            int clampedOfferCount = Mathf.Max(1, offerCount);
            List<string> nextOfferIds = new(clampedOfferCount)
            {
                forcedDefinition.Id
            };

            List<string> generatedOfferIds = ShopUpgradeCatalog.GenerateOfferIds(
                profile,
                clampedOfferCount,
                GenerateShopOfferSeed());

            for (int i = 0; i < generatedOfferIds.Count && nextOfferIds.Count < clampedOfferCount; i++)
            {
                string generatedOfferId = generatedOfferIds[i];
                if (string.Equals(generatedOfferId, forcedDefinition.Id, StringComparison.Ordinal))
                    continue;

                nextOfferIds.Add(generatedOfferId);
            }

            while (nextOfferIds.Count < clampedOfferCount)
                nextOfferIds.Add(ShopUpgradeCatalog.PlaceholderOffer.Id);

            profile.shopState.currentOfferIds.Clear();
            profile.shopState.currentOfferIds.AddRange(nextOfferIds);
            profile.shopState.consumedOfferIndices.Clear();
            s_Instance.SaveNowInternal();
            return true;
        }

        public static void SaveNow()
        {
            EnsureCreated();
            s_Instance.SaveNowInternal();
        }

        public static int RecordRandomMazeEscapeAndSave(int earnedGold = 0)
        {
            EnsureCreated();
            ProfileSaveData profile = s_Instance.EnsureProfileExistsInternal();
            profile.totalGold = Mathf.Max(0, profile.totalGold) + Mathf.Max(0, earnedGold);
            profile.completedRandomMazeRuns = Mathf.Max(0, profile.completedRandomMazeRuns) + 1;
            profile.shopState.freeRefreshCount = Mathf.Max(0, profile.shopState.freeRefreshCount) + 1;
            s_Instance.SaveNowInternal();
            return profile.totalGold;
        }

        static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            GameObject root = new("ProfileService");
            s_Instance = root.AddComponent<ProfileService>();
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
            LoadExistingProfileIfPresentInternal();
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void QueueNewGameInitialization(string targetSceneName)
        {
            string normalizedSceneName = NormalizeSceneName(targetSceneName);
            if (string.IsNullOrEmpty(normalizedSceneName))
                return;

            s_PendingNewGameSceneName = normalizedSceneName;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            bool didCreateNewProfile = false;
            if (string.IsNullOrEmpty(s_PendingNewGameSceneName))
            {
                // no-op
            }
            else if (string.Equals(scene.name, s_PendingNewGameSceneName, StringComparison.Ordinal))
            {
                s_CurrentProfile = CreateFreshProfile();
                s_PendingNewGameSceneName = null;
                didCreateNewProfile = true;
                Debug.Log($"ProfileService created a new save after entering {scene.name}.", this);
            }
            else
            {
                Debug.LogWarning(
                    $"ProfileService cleared a pending new-game save request for {s_PendingNewGameSceneName} " +
                    $"because scene {scene.name} loaded instead.",
                    this);
                s_PendingNewGameSceneName = null;
            }

            if (!ShouldTrackContinueScene(scene.name))
                return;

            ProfileSaveData profile = EnsureProfileExistsInternal();
            string normalizedSceneName = NormalizeSceneName(scene.name);
            if (string.Equals(profile.continueSceneName, normalizedSceneName, StringComparison.Ordinal) && !didCreateNewProfile)
                return;

            profile.continueSceneName = normalizedSceneName;
            SaveNowInternal();
        }

        void LoadExistingProfileIfPresentInternal()
        {
            if (s_HasAttemptedInitialLoad)
                return;

            s_HasAttemptedInitialLoad = true;
            string saveFilePath = ResolveSaveFilePath();
            if (!File.Exists(saveFilePath))
            {
                s_CurrentProfile = null;
                return;
            }

            try
            {
                string json = File.ReadAllText(saveFilePath);
                ProfileSaveData loadedProfile = JsonUtility.FromJson<ProfileSaveData>(json);
                if (!TryValidateLoadedProfile(loadedProfile, out string validationMessage))
                {
                    Debug.LogWarning(validationMessage, this);
                    s_CurrentProfile = null;
                    return;
                }

                NormalizeProfileData(loadedProfile);
                s_CurrentProfile = loadedProfile;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"ProfileService failed to load save data at {saveFilePath}: {ex.Message}", this);
                s_CurrentProfile = null;
            }
        }

        ProfileSaveData EnsureProfileExistsInternal()
        {
            if (s_CurrentProfile != null)
                return s_CurrentProfile;

            s_CurrentProfile = CreateFreshProfile();
            return s_CurrentProfile;
        }

        IReadOnlyList<string> GetOrCreateCurrentShopOfferIdsInternal(int offerCount)
        {
            ProfileSaveData profile = EnsureProfileExistsInternal();
            int clampedOfferCount = Mathf.Max(1, offerCount);

            if (HasValidStoredShopOffers(profile, clampedOfferCount))
                return profile.shopState.currentOfferIds.ToArray();

            return RefreshShopOffersInternal(clampedOfferCount);
        }

        IReadOnlyList<string> ConsumeQueuedSingleRunUpgradesForRandomMazeEntryInternal()
        {
            ProfileSaveData profile = EnsureProfileExistsInternal();
            if (profile.queuedSingleRunUpgradeIds == null || profile.queuedSingleRunUpgradeIds.Count == 0)
                return Array.Empty<string>();

            string[] consumedUpgradeIds = profile.queuedSingleRunUpgradeIds.ToArray();
            profile.queuedSingleRunUpgradeIds.Clear();
            SaveNowInternal();
            return consumedUpgradeIds;
        }

        IReadOnlyList<string> RefreshShopOffersInternal(int offerCount)
        {
            ProfileSaveData profile = EnsureProfileExistsInternal();
            int clampedOfferCount = Mathf.Max(1, offerCount);
            int randomSeed = GenerateShopOfferSeed();
            List<string> nextOffers = ShopUpgradeCatalog.GenerateOfferIds(profile, clampedOfferCount, randomSeed);

            profile.shopState.currentOfferIds.Clear();
            profile.shopState.currentOfferIds.AddRange(nextOffers);
            profile.shopState.consumedOfferIndices.Clear();
            SaveNowInternal();
            return nextOffers.ToArray();
        }

        bool TryRefreshShopOffersInternal(int offerCount)
        {
            ProfileSaveData profile = EnsureProfileExistsInternal();
            int currentCost = GetCurrentShopRefreshCost();
            if (profile.shopState.freeRefreshCount <= 0 && profile.totalGold < currentCost)
                return false;

            if (profile.shopState.freeRefreshCount > 0)
                profile.shopState.freeRefreshCount = Mathf.Max(0, profile.shopState.freeRefreshCount - 1);
            else
                profile.totalGold = Mathf.Max(0, profile.totalGold - currentCost);

            RefreshShopOffersInternal(offerCount);
            return true;
        }

        bool SaveNowInternal()
        {
            ProfileSaveData profile = EnsureProfileExistsInternal();
            NormalizeProfileData(profile);
            profile.lastSavedUtcTicks = DateTime.UtcNow.Ticks;

            string saveFilePath = ResolveSaveFilePath();
            string saveDirectory = Path.GetDirectoryName(saveFilePath);
            try
            {
                if (!string.IsNullOrWhiteSpace(saveDirectory) && !Directory.Exists(saveDirectory))
                    Directory.CreateDirectory(saveDirectory);

                string json = JsonUtility.ToJson(profile, true);
                File.WriteAllText(saveFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"ProfileService failed to save profile data to {saveFilePath}: {ex.Message}",
                    this);
                return false;
            }
        }

        static ProfileSaveData CreateFreshProfile()
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            return new ProfileSaveData
            {
                saveVersion = CurrentSaveVersion,
                createdUtcTicks = nowTicks,
                lastSavedUtcTicks = nowTicks,
                continueSceneName = null,
                totalGold = 0,
                completedRandomMazeRuns = 0,
                purchasedUpgradeIds = new System.Collections.Generic.List<string>(),
                queuedSingleRunUpgradeIds = new System.Collections.Generic.List<string>(),
                shopState = new ProfileShopStateSaveData
                {
                    refreshCost = DefaultShopRefreshCost
                }
            };
        }

        static string ResolveSaveFilePath()
        {
            return Path.Combine(Application.persistentDataPath, SaveFileName);
        }

        static string NormalizeSceneName(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? null : sceneName.Trim();
        }

        static bool ShouldTrackContinueScene(string sceneName)
        {
            string normalizedSceneName = NormalizeSceneName(sceneName);
            if (string.IsNullOrEmpty(normalizedSceneName))
                return false;

            return string.Equals(normalizedSceneName, TutorialSceneName, StringComparison.Ordinal)
                || string.Equals(normalizedSceneName, Maze1SceneName, StringComparison.Ordinal)
                || string.Equals(normalizedSceneName, RandomMazeSceneName, StringComparison.Ordinal);
        }

        static bool TryValidateLoadedProfile(ProfileSaveData profile, out string validationMessage)
        {
            if (profile == null)
            {
                validationMessage = $"ProfileService could not deserialize save data from {ResolveSaveFilePath()}.";
                return false;
            }

            if (profile.saveVersion != CurrentSaveVersion)
            {
                validationMessage =
                    $"ProfileService rejected save data from {ResolveSaveFilePath()} because saveVersion {profile.saveVersion} " +
                    $"does not match current version {CurrentSaveVersion}.";
                return false;
            }

            validationMessage = null;
            return true;
        }

        static void NormalizeProfileData(ProfileSaveData profile)
        {
            if (profile == null)
                return;

            if (profile.purchasedUpgradeIds == null)
                profile.purchasedUpgradeIds = new System.Collections.Generic.List<string>();

            if (profile.queuedSingleRunUpgradeIds == null)
                profile.queuedSingleRunUpgradeIds = new System.Collections.Generic.List<string>();

            profile.shopState ??= new ProfileShopStateSaveData();
            profile.shopState.currentOfferIds ??= new System.Collections.Generic.List<string>();
            profile.shopState.consumedOfferIndices ??= new System.Collections.Generic.List<int>();
            profile.shopState.freeRefreshCount = Mathf.Max(0, profile.shopState.freeRefreshCount);
            profile.shopState.refreshCost = profile.shopState.refreshCost <= 0 ? DefaultShopRefreshCost : profile.shopState.refreshCost;
            profile.continueSceneName = NormalizeSceneName(profile.continueSceneName);
            if (!ShouldTrackContinueScene(profile.continueSceneName))
                profile.continueSceneName = null;
            profile.totalGold = Mathf.Max(0, profile.totalGold);
            profile.completedRandomMazeRuns = Mathf.Max(0, profile.completedRandomMazeRuns);

            if (profile.createdUtcTicks <= 0)
                profile.createdUtcTicks = DateTime.UtcNow.Ticks;
        }

        static bool HasValidStoredShopOffers(ProfileSaveData profile, int offerCount)
        {
            if (profile?.shopState?.currentOfferIds == null)
                return false;

            if (profile.shopState.currentOfferIds.Count != Mathf.Max(1, offerCount))
                return false;

            for (int i = 0; i < profile.shopState.currentOfferIds.Count; i++)
            {
                if (!ShopUpgradeCatalog.TryGetDefinition(profile.shopState.currentOfferIds[i], out _))
                    return false;
            }

            return true;
        }

        static int GenerateShopOfferSeed()
        {
            return Guid.NewGuid().GetHashCode();
        }
    }
}
