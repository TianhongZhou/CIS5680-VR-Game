using System;
using System.Collections.Generic;

namespace CIS5680VRGame.Progression
{
    [Serializable]
    public sealed class ProfileSaveData
    {
        public int saveVersion = ProfileService.CurrentSaveVersion;
        public long createdUtcTicks;
        public long lastSavedUtcTicks;
        public string continueSceneName;
        public int totalGold;
        public int completedRandomMazeRuns;
        public List<string> purchasedUpgradeIds = new();
        public List<string> queuedSingleRunUpgradeIds = new();
        public ProfileShopStateSaveData shopState = new();
    }

    [Serializable]
    public sealed class ProfileShopStateSaveData
    {
        public List<string> currentOfferIds = new();
        public List<int> consumedOfferIndices = new();
        public int freeRefreshCount;
        public int refreshCost;
    }
}
