using System;
using System.IO;
using UnityEngine;

namespace CIS5680VRGame.Generation
{
    public readonly struct RandomMazeRuntimeSelection
    {
        public RandomMazeRuntimeSelection(
            RandomMazeRuntimeProfileData profile,
            string profileRelativePath,
            string resolvedProfilePath,
            int effectiveSeed,
            bool usedFixedSeed)
        {
            Profile = profile;
            ProfileRelativePath = profileRelativePath;
            ResolvedProfilePath = resolvedProfilePath;
            EffectiveSeed = effectiveSeed;
            UsedFixedSeed = usedFixedSeed;
        }

        public RandomMazeRuntimeProfileData Profile { get; }
        public string ProfileRelativePath { get; }
        public string ResolvedProfilePath { get; }
        public int EffectiveSeed { get; }
        public bool UsedFixedSeed { get; }
    }

    public sealed class RandomMazeRuntimeConfigService : MonoBehaviour
    {
        const string DefaultProfilesFolderRelativePath = "RandomMazeProfiles";
        public const int CurrentProfileVersion = 1;

        static RandomMazeRuntimeConfigService s_Instance;
        static string s_ActiveProfileRelativePath;
        static string s_LastLoadedProfileRelativePath;
        static string s_RuntimeSeedProfileRelativePath;
        static RandomMazeRuntimeProfileData s_LastLoadedProfile;
        static int? s_RuntimeGeneratedSeed;
        static bool s_HasLoggedDiscoveryIssue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        public static string ActiveProfileRelativePath => s_ActiveProfileRelativePath;

        public static void SetActiveProfileRelativePath(string profileRelativePath)
        {
            EnsureCreated();
            s_Instance.SetActiveProfileInternal(profileRelativePath);
        }

        public static void ClearActiveProfile()
        {
            EnsureCreated();
            s_Instance.ClearActiveProfileInternal();
        }

        public static bool TryGetActiveSelection(out RandomMazeRuntimeSelection selection)
        {
            EnsureCreated();
            return s_Instance.TryResolveSelection(out selection);
        }

        static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            var root = new GameObject("RandomMazeRuntimeConfigService");
            s_Instance = root.AddComponent<RandomMazeRuntimeConfigService>();
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
        }

        void SetActiveProfileInternal(string profileRelativePath)
        {
            s_ActiveProfileRelativePath = NormalizeRelativePath(profileRelativePath);
            ResetCachedProfileData();
        }

        void ClearActiveProfileInternal()
        {
            s_ActiveProfileRelativePath = null;
            ResetCachedProfileData();
        }

        bool TryResolveSelection(out RandomMazeRuntimeSelection selection)
        {
            selection = default;

            if (!TryResolveProfileRelativePath(out string profileRelativePath))
                return false;

            string resolvedProfilePath = ResolveStreamingAssetsPath(profileRelativePath);
            if (!TryLoadProfile(profileRelativePath, resolvedProfilePath, out RandomMazeRuntimeProfileData profile))
                return false;

            bool useFixedSeed = profile != null && profile.useFixedSeed;
            int effectiveSeed = useFixedSeed
                ? profile.fixedSeed
                : ResolveRuntimeGeneratedSeed(profileRelativePath);

            selection = new RandomMazeRuntimeSelection(
                profile,
                profileRelativePath,
                resolvedProfilePath,
                effectiveSeed,
                useFixedSeed);

            return true;
        }

        static bool TryResolveProfileRelativePath(out string profileRelativePath)
        {
            if (!string.IsNullOrWhiteSpace(s_ActiveProfileRelativePath))
            {
                profileRelativePath = s_ActiveProfileRelativePath;
                return true;
            }

            string profilesDirectoryPath = ResolveStreamingAssetsPath(DefaultProfilesFolderRelativePath);
            if (!Directory.Exists(profilesDirectoryPath))
            {
                profileRelativePath = null;
                return false;
            }

            string[] jsonFiles = Directory.GetFiles(profilesDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
            if (jsonFiles.Length == 1)
            {
                profileRelativePath = CombineStreamingAssetsRelativePath(
                    DefaultProfilesFolderRelativePath,
                    Path.GetFileName(jsonFiles[0]));
                return true;
            }

            if (!s_HasLoggedDiscoveryIssue && jsonFiles.Length > 1)
            {
                Debug.LogWarning(
                    $"RandomMazeRuntimeConfigService found multiple profile JSON files in {profilesDirectoryPath}. " +
                    "Call SetActiveProfileRelativePath(...) before loading the maze scene to disambiguate.");
                s_HasLoggedDiscoveryIssue = true;
            }

            profileRelativePath = null;
            return false;
        }

        static bool TryLoadProfile(
            string profileRelativePath,
            string resolvedProfilePath,
            out RandomMazeRuntimeProfileData profile)
        {
            if (s_LastLoadedProfile != null && string.Equals(s_LastLoadedProfileRelativePath, profileRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                profile = s_LastLoadedProfile;
                return true;
            }

            if (!File.Exists(resolvedProfilePath))
            {
                Debug.LogWarning($"RandomMazeRuntimeConfigService could not find profile JSON at {resolvedProfilePath}.");
                profile = null;
                return false;
            }

            string json = File.ReadAllText(resolvedProfilePath);
            profile = JsonUtility.FromJson<RandomMazeRuntimeProfileData>(json);
            if (profile == null)
            {
                Debug.LogWarning($"RandomMazeRuntimeConfigService failed to parse profile JSON at {resolvedProfilePath}.");
                return false;
            }

            if (!TryValidateProfile(profile, resolvedProfilePath, out string validationMessage))
            {
                Debug.LogWarning(validationMessage);
                profile = null;
                return false;
            }

            s_LastLoadedProfileRelativePath = profileRelativePath;
            s_LastLoadedProfile = profile;
            return true;
        }

        static int ResolveRuntimeGeneratedSeed(string profileRelativePath)
        {
            if (s_RuntimeGeneratedSeed.HasValue
                && string.Equals(s_RuntimeSeedProfileRelativePath, profileRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                return s_RuntimeGeneratedSeed.Value;
            }

            s_RuntimeGeneratedSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            s_RuntimeSeedProfileRelativePath = profileRelativePath;
            return s_RuntimeGeneratedSeed.Value;
        }

        static string ResolveStreamingAssetsPath(string relativePath)
        {
            string normalizedRelativePath = NormalizeRelativePath(relativePath);
            string normalizedPath = normalizedRelativePath
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            return Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, normalizedPath));
        }

        static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            return relativePath.Trim().Replace('\\', '/');
        }

        static string CombineStreamingAssetsRelativePath(string left, string right)
        {
            string combined = string.IsNullOrWhiteSpace(left)
                ? right
                : $"{NormalizeRelativePath(left).TrimEnd('/')}/{NormalizeRelativePath(right).TrimStart('/')}";

            return NormalizeRelativePath(combined);
        }

        static void ResetCachedProfileData()
        {
            s_LastLoadedProfileRelativePath = null;
            s_LastLoadedProfile = null;
            s_RuntimeGeneratedSeed = null;
            s_RuntimeSeedProfileRelativePath = null;
            s_HasLoggedDiscoveryIssue = false;
        }

        static bool TryValidateProfile(RandomMazeRuntimeProfileData profile, string resolvedProfilePath, out string validationMessage)
        {
            if (profile == null)
            {
                validationMessage = $"RandomMazeRuntimeConfigService loaded a null profile from {resolvedProfilePath}.";
                return false;
            }

            var errors = new System.Collections.Generic.List<string>();

            if (profile.profileVersion != CurrentProfileVersion)
            {
                errors.Add(
                    $"Unsupported profileVersion {profile.profileVersion}. Expected {CurrentProfileVersion}. " +
                    "Re-export this random maze profile with the current toolchain.");
            }

            if (profile.mazeSize <= 0)
                errors.Add("mazeSize must be greater than 0.");

            if (profile.featureCounts == null)
                errors.Add("Missing required object: featureCounts.");

            if (profile.placementRules == null)
                errors.Add("Missing required object: placementRules.");

            if (profile.skeletonRules == null)
                errors.Add("Missing required object: skeletonRules.");

            if (errors.Count == 0)
            {
                validationMessage = string.Empty;
                return true;
            }

            validationMessage =
                $"RandomMazeRuntimeConfigService rejected profile JSON at {resolvedProfilePath}:\n" +
                string.Join("\n", errors);
            return false;
        }
    }

    [Serializable]
    public sealed class RandomMazeRuntimeProfileData
    {
        public int profileVersion = RandomMazeRuntimeConfigService.CurrentProfileVersion;
        public bool useFixedSeed = true;
        public int fixedSeed = 12345;
        public int mazeSize = 9;
        public RandomMazeRuntimeFeatureCounts featureCounts;
        public RandomMazeRuntimeTerrainVariants terrainVariants;
        public RandomMazeRuntimePlacementRules placementRules;
        public RandomMazeRuntimeSkeletonRules skeletonRules;
    }

    [Serializable]
    public sealed class RandomMazeRuntimeFeatureCounts
    {
        public int energyRefillCount = 1;
        public int healthRefillCount = 1;
        public int trapCount = 1;
        public int rewardCount = 2;
        public int hiddenDoorCount = 1;
    }

    [Serializable]
    public sealed class RandomMazeRuntimeTerrainVariants
    {
        public int beveledCornerCellCount = 6;
        public int floorRidgeCellCount = 8;
    }

    [Serializable]
    public sealed class RandomMazeRuntimePlacementRules
    {
        public int minSameTypeCellDistance = 2;
        public int minCrossTypeCellDistance = 2;
        public int startSafeDistance = 2;
        public int goalSafeDistance = 2;
        public int rewardMinBranchDepth = 2;
    }

    [Serializable]
    public sealed class RandomMazeRuntimeSkeletonRules
    {
        public int minGoalDistanceFromStart = 7;
        public int branchFreeStartCells = 3;
        public int preferredStraightStartCells = 3;
        public float startZoneRatio = 0.25f;
        public float midZoneRatio = 0.35f;
    }
}
