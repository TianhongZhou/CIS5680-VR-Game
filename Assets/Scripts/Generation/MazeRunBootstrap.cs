using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace CIS5680VRGame.Generation
{
    [DisallowMultipleComponent]
    public class MazeRunBootstrap : MonoBehaviour
    {
        const string GeneratedModulesName = "GeneratedModules";
        const string GeneratedHazardsName = "GeneratedHazards";
        const string GeneratedRefillsName = "GeneratedRefills";
        const string GeneratedRewardsName = "GeneratedRewards";
        const string GeneratedGoalName = "GeneratedGoal";
        const int MinSupportedMazeSize = 5;
        const int MaxSupportedMazeSize = 200;
        const int MaxBatchSeedTestCount = 10000;
        const string DefaultParameterExportRelativeFolderPath = "Local Docs/Exported Random Maze Parameters";

        [Header("Generation Roots")]
        [SerializeField] MazeGenerator m_Generator;
        [SerializeField] MazeModulePlacer m_ModulePlacer;
        [SerializeField] Transform m_ModulesRoot;
        [SerializeField] Transform m_HazardsRoot;
        [SerializeField] Transform m_RefillsRoot;
        [SerializeField] Transform m_RewardsRoot;
        [SerializeField] Transform m_GoalRoot;

        [Header("Startup")]
        [SerializeField] bool m_ClearGeneratedContentOnAwake = true;
        [SerializeField] bool m_UseFixedSeed = true;
        [SerializeField] int m_FixedSeed = 12345;

        [Header("Runtime Profile Override")]
        [SerializeField] bool m_UseRuntimeProfileOverride = true;

        [Header("Maze Size")]
        [SerializeField] int m_MazeSize = 9;

        [Header("Feature Counts")]
        [SerializeField] int m_EnergyRefillCount = 1;
        [SerializeField] int m_HealthRefillCount = 1;
        [SerializeField] int m_TrapCount = 1;
        [SerializeField] int m_RewardCount = 2;

        [Header("Placement Rules")]
        [SerializeField] int m_MinSameTypeCellDistance = 2;
        [SerializeField] int m_MinCrossTypeCellDistance = 2;
        [SerializeField] int m_StartSafeDistance = 2;
        [SerializeField] int m_GoalSafeDistance = 2;
        [SerializeField] int m_RewardMinBranchDepth = 2;

        [Header("Skeleton Rules")]
        [SerializeField] int m_MinGoalDistanceFromStart = 7;
        [SerializeField] int m_BranchFreeStartCells = 3;
        [SerializeField] int m_PreferredStraightStartCells = 3;
        [SerializeField] float m_StartZoneRatio = 0.25f;
        [SerializeField] float m_MidZoneRatio = 0.35f;

        [Header("Batch Seed Smoke Test")]
        [SerializeField] int m_BatchTestStartSeed = 0;
        [SerializeField, Min(1)] int m_BatchTestSeedCount = 100;

        [Header("Parameter Export")]
        [SerializeField] string m_ParameterExportRelativeFolderPath = DefaultParameterExportRelativeFolderPath;

        [SerializeField] bool m_BuildLogicDebugPreview = true;
        [SerializeField] Vector3 m_DebugOrigin = Vector3.zero;
        [SerializeField, Min(0.25f)] float m_DebugCellSize = 2f;
        [SerializeField, Min(0.01f)] float m_DebugNodeHeight = 0.12f;
        [SerializeField, Min(0.01f)] float m_DebugConnectionThickness = 0.1f;
        [SerializeField] bool m_LogBootstrap = true;

        [System.NonSerialized] string m_LastBatchSeedTestSummary = "No batch seed smoke test has been run yet.";
        [System.NonSerialized] string m_LastParameterExportPath = "No parameter export has been created yet.";
        [System.NonSerialized] string m_LastRuntimeProfileSummary = "No runtime random maze profile override applied.";

        public int CurrentSeed { get; private set; }
        public MazeLayout CurrentLayout { get; private set; }
        public MazeMainPathBuildMode LastGeneratedMainPathBuildMode { get; private set; } = MazeMainPathBuildMode.Normal;
        public string LastBatchSeedTestSummary => string.IsNullOrWhiteSpace(m_LastBatchSeedTestSummary)
            ? "No batch seed smoke test has been run yet."
            : m_LastBatchSeedTestSummary;
        public string LastParameterExportPath => string.IsNullOrWhiteSpace(m_LastParameterExportPath)
            ? "No parameter export has been created yet."
            : m_LastParameterExportPath;
        public string LastRuntimeProfileSummary => string.IsNullOrWhiteSpace(m_LastRuntimeProfileSummary)
            ? "No runtime random maze profile override applied."
            : m_LastRuntimeProfileSummary;
        public Transform ModulesRoot => m_ModulesRoot;
        public Transform HazardsRoot => m_HazardsRoot;
        public Transform RefillsRoot => m_RefillsRoot;
        public Transform RewardsRoot => m_RewardsRoot;
        public Transform GoalRoot => m_GoalRoot;
        public bool UseFixedSeed => m_UseFixedSeed;
        public bool UseRuntimeProfileOverride => m_UseRuntimeProfileOverride;
        public int FixedSeed => m_FixedSeed;
        public int MazeSize => m_MazeSize;
        public int EnergyRefillCount => m_EnergyRefillCount;
        public int HealthRefillCount => m_HealthRefillCount;
        public int TrapCount => m_TrapCount;
        public int RewardCount => m_RewardCount;
        public int MinSameTypeCellDistance => m_MinSameTypeCellDistance;
        public int MinCrossTypeCellDistance => m_MinCrossTypeCellDistance;
        public int StartSafeDistance => m_StartSafeDistance;
        public int GoalSafeDistance => m_GoalSafeDistance;
        public int RewardMinBranchDepth => m_RewardMinBranchDepth;
        public int MinGoalDistanceFromStart => m_MinGoalDistanceFromStart;
        public int BranchFreeStartCells => m_BranchFreeStartCells;
        public int PreferredStraightStartCells => m_PreferredStraightStartCells;
        public float StartZoneRatio => m_StartZoneRatio;
        public float MidZoneRatio => m_MidZoneRatio;
        public int BatchTestStartSeed => m_BatchTestStartSeed;
        public int BatchTestSeedCount => m_BatchTestSeedCount;
        public string ParameterExportRelativeFolderPath => m_ParameterExportRelativeFolderPath;

        public string GetParameterExportDirectoryPath()
        {
            return ResolveProjectRelativePath(m_ParameterExportRelativeFolderPath);
        }

        public static string GetDefaultParameterExportDirectoryPath()
        {
            return ResolveProjectRelativePath(DefaultParameterExportRelativeFolderPath);
        }

        void Reset()
        {
            EnsureGeneratedHierarchy();
            ResolveGenerator();
            ResolveModulePlacer();
        }

        void Awake()
        {
            EnsureGeneratedHierarchy();
            ResolveGenerator();
            ResolveModulePlacer();

            if (m_UseRuntimeProfileOverride)
                TryApplyRuntimeProfileOverride();
            else
                m_LastRuntimeProfileSummary = "Runtime random maze profile override disabled in inspector.";

            CurrentSeed = ResolveSeed();

            if (m_ClearGeneratedContentOnAwake)
                ClearGeneratedContent();

            GenerateLogicLayout();
        }

        [ContextMenu("Ensure Generated Hierarchy")]
        public void EnsureGeneratedHierarchy()
        {
            m_ModulesRoot = EnsureChild(transform, GeneratedModulesName);
            m_HazardsRoot = EnsureChild(transform, GeneratedHazardsName);
            m_RefillsRoot = EnsureChild(transform, GeneratedRefillsName);
            m_RewardsRoot = EnsureChild(transform, GeneratedRewardsName);
            m_GoalRoot = EnsureChild(transform, GeneratedGoalName);
        }

        [ContextMenu("Clear Generated Content")]
        public void ClearGeneratedContent()
        {
            EnsureGeneratedHierarchy();
            ClearChildren(m_ModulesRoot);
            ClearChildren(m_HazardsRoot);
            ClearChildren(m_RefillsRoot);
            ClearChildren(m_RewardsRoot);
            ClearChildren(m_GoalRoot);
        }

        [ContextMenu("Generate Logic Layout")]
        public void GenerateLogicLayout()
        {
            GenerateLogicLayout(MazeMainPathBuildMode.Normal);
        }

        public void GenerateLogicLayout(MazeMainPathBuildMode buildMode)
        {
            ResolveGenerator();

            if (!TryGetGenerationValidationMessage(out string validationMessage))
            {
                Debug.LogError($"MazeRunBootstrap configuration is invalid:\n{validationMessage}", this);
                return;
            }

            ApplyConfiguredGeneratorSettings();

            if (!Application.isPlaying)
                CurrentSeed = ResolveSeed();

            if (m_Generator == null)
            {
                Debug.LogWarning("MazeRunBootstrap could not find a MazeGenerator component.", this);
                return;
            }

            if (!Application.isPlaying)
                ClearGeneratedContent();

            LastGeneratedMainPathBuildMode = buildMode;
            CurrentLayout = m_Generator.GenerateLayout(CurrentSeed, buildMode);
            if (CurrentLayout == null)
            {
                Debug.LogError($"MazeRunBootstrap failed to generate a maze layout using mode {buildMode}.", this);
                return;
            }

            if (m_ModulePlacer != null)
                m_ModulePlacer.BuildModules(CurrentLayout, this);

            if (m_BuildLogicDebugPreview)
                BuildLogicDebugPreview(CurrentLayout);

            if (m_LogBootstrap)
            {
                Debug.Log(
                    $"MazeRunBootstrap initialized with seed {CurrentSeed} using mode {buildMode}. " +
                    $"Cells={CurrentLayout.TotalCellCount}, MainPath={CurrentLayout.MainPathLength}, Branches={CurrentLayout.BranchCount}.",
                    this);
            }
        }

        public void SetFixedSeed(int seed)
        {
            m_UseFixedSeed = true;
            m_FixedSeed = seed;
            CurrentSeed = seed;
        }

        public int RandomizeFixedSeed()
        {
            int seed = Random.Range(int.MinValue, int.MaxValue);
            SetFixedSeed(seed);
            return seed;
        }

        void TryApplyRuntimeProfileOverride()
        {
            if (!Application.isPlaying)
                return;

            if (!RandomMazeRuntimeConfigService.TryGetActiveSelection(out RandomMazeRuntimeSelection selection)
                || selection.Profile == null)
            {
                m_LastRuntimeProfileSummary = "No runtime random maze profile override applied.";
                return;
            }

            ApplyRuntimeProfile(selection.Profile, selection.EffectiveSeed);
            m_LastRuntimeProfileSummary =
                $"Applied runtime random maze profile from {selection.ResolvedProfilePath} using seed {selection.EffectiveSeed} " +
                $"({(selection.UsedFixedSeed ? "profile fixed seed" : "runtime-generated seed")}).";

            if (m_LogBootstrap)
                Debug.Log(m_LastRuntimeProfileSummary, this);
        }

        void ApplyRuntimeProfile(RandomMazeRuntimeProfileData profile, int effectiveSeed)
        {
            if (profile == null)
                return;

            m_UseFixedSeed = true;
            m_FixedSeed = effectiveSeed;
            m_MazeSize = Mathf.Clamp(profile.mazeSize, MinSupportedMazeSize, MaxSupportedMazeSize);

            if (profile.featureCounts != null)
            {
                m_EnergyRefillCount = Mathf.Max(0, profile.featureCounts.energyRefillCount);
                m_HealthRefillCount = Mathf.Max(0, profile.featureCounts.healthRefillCount);
                m_TrapCount = Mathf.Max(0, profile.featureCounts.trapCount);
                m_RewardCount = Mathf.Max(0, profile.featureCounts.rewardCount);
            }

            if (profile.placementRules != null)
            {
                m_MinSameTypeCellDistance = Mathf.Max(0, profile.placementRules.minSameTypeCellDistance);
                m_MinCrossTypeCellDistance = Mathf.Max(0, profile.placementRules.minCrossTypeCellDistance);
                m_StartSafeDistance = Mathf.Max(0, profile.placementRules.startSafeDistance);
                m_GoalSafeDistance = Mathf.Max(0, profile.placementRules.goalSafeDistance);
                m_RewardMinBranchDepth = Mathf.Max(0, profile.placementRules.rewardMinBranchDepth);
            }

            if (profile.skeletonRules != null)
            {
                m_MinGoalDistanceFromStart = Mathf.Max(4, profile.skeletonRules.minGoalDistanceFromStart);
                m_BranchFreeStartCells = Mathf.Max(0, profile.skeletonRules.branchFreeStartCells);
                m_PreferredStraightStartCells = Mathf.Max(0, profile.skeletonRules.preferredStraightStartCells);
                m_StartZoneRatio = Mathf.Clamp01(profile.skeletonRules.startZoneRatio);
                m_MidZoneRatio = Mathf.Clamp01(profile.skeletonRules.midZoneRatio);
            }
        }

        public void ApplyConfiguredGeneratorSettings()
        {
            if (m_Generator != null)
            {
                m_Generator.ApplySquareSizeProfile(m_MazeSize);
                m_Generator.ApplyPlacementProfile(
                    m_EnergyRefillCount,
                    m_HealthRefillCount,
                    m_TrapCount,
                    m_RewardCount);
                m_Generator.ApplyPlacementRuleProfile(
                    m_MinSameTypeCellDistance,
                    m_MinCrossTypeCellDistance,
                    m_StartSafeDistance,
                    m_GoalSafeDistance,
                    m_RewardMinBranchDepth);
                m_Generator.ApplySkeletonProfile(
                    m_MinGoalDistanceFromStart,
                    m_BranchFreeStartCells,
                    m_PreferredStraightStartCells,
                    m_StartZoneRatio,
                    m_MidZoneRatio);
            }
        }

        public int GetGuaranteedSpecialSlotCount()
        {
            if (m_MazeSize < MinSupportedMazeSize || m_MazeSize > MaxSupportedMazeSize)
                return 0;

            return MazeGenerator.EstimateGuaranteedSpecialSlotCountForSquareSize(m_MazeSize);
        }

        public int GetTotalRequestedSpecialCount()
        {
            return m_EnergyRefillCount + m_HealthRefillCount + m_TrapCount + m_RewardCount;
        }

        public bool TryGetGenerationValidationMessage(out string validationMessage)
        {
            var errors = new System.Collections.Generic.List<string>();

            if (m_MazeSize < MinSupportedMazeSize)
                errors.Add($"Maze Size must be at least {MinSupportedMazeSize}.");
            else if (m_MazeSize > MaxSupportedMazeSize)
                errors.Add($"Maze Size must be at most {MaxSupportedMazeSize} to avoid runaway editor stalls.");

            ValidateNonNegative(errors, "Energy Refill Count", m_EnergyRefillCount);
            ValidateNonNegative(errors, "Health Refill Count", m_HealthRefillCount);
            ValidateNonNegative(errors, "Trap Count", m_TrapCount);
            ValidateNonNegative(errors, "Reward Count", m_RewardCount);
            ValidateNonNegative(errors, "Min Same-Type Cell Distance", m_MinSameTypeCellDistance);
            ValidateNonNegative(errors, "Min Cross-Type Cell Distance", m_MinCrossTypeCellDistance);
            ValidateNonNegative(errors, "Start Safe Distance", m_StartSafeDistance);
            ValidateNonNegative(errors, "Goal Safe Distance", m_GoalSafeDistance);
            ValidateNonNegative(errors, "Reward Min Branch Depth", m_RewardMinBranchDepth);
            ValidateMinimum(errors, "Min Goal Distance From Start", m_MinGoalDistanceFromStart, 4);
            ValidateNonNegative(errors, "Branch-Free Start Cells", m_BranchFreeStartCells);
            ValidateNonNegative(errors, "Preferred Straight Start Cells", m_PreferredStraightStartCells);
            ValidateRatio(errors, "Start Zone Ratio", m_StartZoneRatio);
            ValidateRatio(errors, "Mid Zone Ratio", m_MidZoneRatio);

            if (errors.Count == 0)
            {
                int guaranteedSlots = GetGuaranteedSpecialSlotCount();
                int totalRequested = GetTotalRequestedSpecialCount();
                MazeGeneratorSizeProfile sizeProfile = MazeGenerator.BuildSquareSizeProfile(m_MazeSize);
                int effectiveMinimumPathLength = Mathf.Max(sizeProfile.MinMainPathLength, m_MinGoalDistanceFromStart);
                int effectiveMaximumPathLength = Mathf.Max(sizeProfile.MaxMainPathLength, effectiveMinimumPathLength);

                ValidatePerRoleCapacity(errors, "Energy Refill Count", m_EnergyRefillCount, guaranteedSlots);
                ValidatePerRoleCapacity(errors, "Health Refill Count", m_HealthRefillCount, guaranteedSlots);
                ValidatePerRoleCapacity(errors, "Trap Count", m_TrapCount, guaranteedSlots);
                ValidatePerRoleCapacity(errors, "Reward Count", m_RewardCount, guaranteedSlots);

                if (m_MinGoalDistanceFromStart > m_MazeSize * m_MazeSize)
                    errors.Add($"Min Goal Distance From Start cannot exceed the total grid cell budget ({m_MazeSize * m_MazeSize}).");

                if (m_BranchFreeStartCells >= effectiveMaximumPathLength - 1)
                {
                    errors.Add(
                        $"Branch-Free Start Cells must stay below {Mathf.Max(1, effectiveMaximumPathLength - 1)} so branches still have room to anchor on the main path.");
                }

                if (m_PreferredStraightStartCells >= effectiveMaximumPathLength)
                {
                    errors.Add(
                        $"Preferred Straight Start Cells must stay below {effectiveMaximumPathLength} for the current maze profile.");
                }

                if (m_StartZoneRatio + m_MidZoneRatio >= 0.99f)
                    errors.Add("Start Zone Ratio + Mid Zone Ratio must stay below 0.99 so the late zone always has room.");

                if (totalRequested > guaranteedSlots)
                {
                    errors.Add(
                        $"Requested special feature count ({totalRequested}) exceeds the guaranteed placement budget ({guaranteedSlots}) for Maze Size {m_MazeSize}.");
                }
            }

            validationMessage = string.Join("\n", errors);
            return errors.Count == 0;
        }

        public bool TryGetBatchSeedTestValidationMessage(out string validationMessage)
        {
            var errors = new System.Collections.Generic.List<string>();

            if (m_BatchTestSeedCount < 1)
                errors.Add("Batch Test Seed Count must be at least 1.");
            else if (m_BatchTestSeedCount > MaxBatchSeedTestCount)
                errors.Add($"Batch Test Seed Count must be at most {MaxBatchSeedTestCount} to keep editor smoke tests manageable.");

            validationMessage = string.Join("\n", errors);
            return errors.Count == 0;
        }

        public string RunBatchSeedSmokeTest()
        {
            ResolveGenerator();

            if (!TryGetGenerationValidationMessage(out string generationValidationMessage))
            {
                m_LastBatchSeedTestSummary = $"Batch seed smoke test did not run because maze settings are invalid:\n{generationValidationMessage}";
                Debug.LogError(m_LastBatchSeedTestSummary, this);
                return m_LastBatchSeedTestSummary;
            }

            if (!TryGetBatchSeedTestValidationMessage(out string batchValidationMessage))
            {
                m_LastBatchSeedTestSummary = $"Batch seed smoke test did not run because batch settings are invalid:\n{batchValidationMessage}";
                Debug.LogError(m_LastBatchSeedTestSummary, this);
                return m_LastBatchSeedTestSummary;
            }

            if (m_Generator == null)
            {
                m_LastBatchSeedTestSummary = "Batch seed smoke test did not run because MazeGenerator could not be resolved.";
                Debug.LogError(m_LastBatchSeedTestSummary, this);
                return m_LastBatchSeedTestSummary;
            }

            ApplyConfiguredGeneratorSettings();

            var report = new BatchSeedSmokeTestReport(m_BatchTestStartSeed, m_BatchTestSeedCount);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < m_BatchTestSeedCount; i++)
            {
                int seed = unchecked(m_BatchTestStartSeed + i);
                MazeLayout layout = m_Generator.GenerateLayout(seed, MazeMainPathBuildMode.Normal, false);
                report.Record(seed, layout);
            }

            stopwatch.Stop();
            report.SetElapsedMilliseconds(stopwatch.ElapsedMilliseconds);

            m_LastBatchSeedTestSummary = report.BuildSummary();
            Debug.Log(m_LastBatchSeedTestSummary, this);
            return m_LastBatchSeedTestSummary;
        }

        public string ExportCurrentParameterSnapshot()
        {
            ResolveGenerator();
            ApplyConfiguredGeneratorSettings();

            bool isValid = TryGetGenerationValidationMessage(out string validationMessage);
            MazeGeneratorSizeProfile sizeProfile = MazeGenerator.BuildSquareSizeProfile(m_MazeSize);
            int effectiveMinimumPathLength = Mathf.Max(sizeProfile.MinMainPathLength, m_MinGoalDistanceFromStart);
            int effectiveMaximumPathLength = Mathf.Max(sizeProfile.MaxMainPathLength, effectiveMinimumPathLength);
            int exportSeed = CurrentLayout != null
                ? CurrentLayout.Seed
                : (m_UseFixedSeed ? m_FixedSeed : CurrentSeed);

            var snapshot = new MazeParameterExportSnapshot
            {
                exportedAtLocal = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                exportedAtUtc = System.DateTime.UtcNow.ToString("o"),
                sourceScenePath = gameObject.scene.path,
                profileVersion = RandomMazeRuntimeConfigService.CurrentProfileVersion,
                useFixedSeed = m_UseFixedSeed,
                fixedSeed = m_FixedSeed,
                currentSeed = CurrentSeed,
                exportSeed = exportSeed,
                isGenerationConfigurationValid = isValid,
                generationValidationMessage = validationMessage,
                lastGeneratedMainPathBuildMode = LastGeneratedMainPathBuildMode.ToString(),
                mazeSize = m_MazeSize,
                featureCounts = new MazeFeatureCountExport
                {
                    energyRefillCount = m_EnergyRefillCount,
                    healthRefillCount = m_HealthRefillCount,
                    trapCount = m_TrapCount,
                    rewardCount = m_RewardCount,
                },
                placementRules = new MazePlacementRuleExport
                {
                    minSameTypeCellDistance = m_MinSameTypeCellDistance,
                    minCrossTypeCellDistance = m_MinCrossTypeCellDistance,
                    startSafeDistance = m_StartSafeDistance,
                    goalSafeDistance = m_GoalSafeDistance,
                    rewardMinBranchDepth = m_RewardMinBranchDepth,
                },
                skeletonRules = new MazeSkeletonRuleExport
                {
                    minGoalDistanceFromStart = m_MinGoalDistanceFromStart,
                    branchFreeStartCells = m_BranchFreeStartCells,
                    preferredStraightStartCells = m_PreferredStraightStartCells,
                    startZoneRatio = m_StartZoneRatio,
                    midZoneRatio = m_MidZoneRatio,
                },
                derivedProfile = new MazeDerivedProfileExport
                {
                    gridWidth = sizeProfile.GridWidth,
                    gridHeight = sizeProfile.GridHeight,
                    minMainPathLength = effectiveMinimumPathLength,
                    maxMainPathLength = effectiveMaximumPathLength,
                    maxMainPathAttempts = sizeProfile.MaxMainPathAttempts,
                    minBranchCount = sizeProfile.MinBranchCount,
                    maxBranchCount = sizeProfile.MaxBranchCount,
                    minBranchLength = sizeProfile.MinBranchLength,
                    maxBranchLength = sizeProfile.MaxBranchLength,
                },
                batchSeedSmokeTest = new MazeBatchSeedExport
                {
                    startSeed = m_BatchTestStartSeed,
                    seedCount = m_BatchTestSeedCount,
                },
                currentLayoutSummary = BuildCurrentLayoutSummary(),
            };

            string exportDirectoryPath = GetParameterExportDirectoryPath();
            System.IO.Directory.CreateDirectory(exportDirectoryPath);

            string fileName = $"random-maze-params-seed-{exportSeed}-{System.DateTime.Now:yyyyMMdd-HHmmss}.json";
            string exportPath = System.IO.Path.Combine(exportDirectoryPath, fileName);
            string json = JsonUtility.ToJson(snapshot, true);
            System.IO.File.WriteAllText(exportPath, json);

            m_LastParameterExportPath = exportPath;
            Debug.Log($"Exported current random maze parameter snapshot to:\n{exportPath}", this);
            return exportPath;
        }

        int ResolveSeed()
        {
            if (m_UseFixedSeed)
                return m_FixedSeed;

            return Random.Range(int.MinValue, int.MaxValue);
        }

        void ResolveGenerator()
        {
            if (m_Generator == null)
                m_Generator = GetComponent<MazeGenerator>();
        }

        void ResolveModulePlacer()
        {
            if (m_ModulePlacer == null)
                m_ModulePlacer = GetComponent<MazeModulePlacer>();
        }

        static void ValidateNonNegative(System.Collections.Generic.ICollection<string> errors, string label, int value)
        {
            if (value < 0)
                errors.Add($"{label} must be 0 or greater.");
        }

        static void ValidateMinimum(System.Collections.Generic.ICollection<string> errors, string label, int value, int minimum)
        {
            if (value < minimum)
                errors.Add($"{label} must be at least {minimum}.");
        }

        static void ValidateRatio(System.Collections.Generic.ICollection<string> errors, string label, float value)
        {
            if (value < 0f || value > 1f)
                errors.Add($"{label} must stay between 0 and 1.");
        }

        static void ValidatePerRoleCapacity(System.Collections.Generic.ICollection<string> errors, string label, int value, int guaranteedSlots)
        {
            if (value > guaranteedSlots)
                errors.Add($"{label} cannot exceed the guaranteed placement budget ({guaranteedSlots}) for the current Maze Size.");
        }

        static string ResolveProjectRelativePath(string relativePath)
        {
            string normalizedRelativePath = string.IsNullOrWhiteSpace(relativePath)
                ? DefaultParameterExportRelativeFolderPath
                : relativePath.Trim();

            string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
            string normalizedPath = normalizedRelativePath
                .Replace('\\', System.IO.Path.DirectorySeparatorChar)
                .Replace('/', System.IO.Path.DirectorySeparatorChar);

            return System.IO.Path.GetFullPath(System.IO.Path.Combine(projectRoot, normalizedPath));
        }

        void BuildLogicDebugPreview(MazeLayout layout)
        {
            if (layout == null || m_ModulesRoot == null)
                return;

            Transform previewRoot = EnsureChild(m_ModulesRoot, "LogicDebugPreview");
            ClearChildren(previewRoot);

            foreach (MazeCellData cell in layout.Cells)
                CreateDebugNode(previewRoot, cell);

            foreach (MazeCellData cell in layout.Cells)
                CreateDebugConnections(previewRoot, layout, cell);
        }

        void CreateDebugNode(Transform parent, MazeCellData cell)
        {
            GameObject nodeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nodeObject.name = $"{cell.Role}_{cell.GridPosition.x}_{cell.GridPosition.y}";
            nodeObject.transform.SetParent(parent, false);
            nodeObject.transform.localPosition = ResolveCellLocalPosition(cell.GridPosition);
            nodeObject.transform.localScale = new Vector3(m_DebugCellSize * 0.42f, m_DebugNodeHeight, m_DebugCellSize * 0.42f);

            Collider nodeCollider = nodeObject.GetComponent<Collider>();
            if (nodeCollider != null)
                DestroyImmediateOrRuntime(nodeCollider);

            Renderer nodeRenderer = nodeObject.GetComponent<Renderer>();
            if (nodeRenderer != null)
                nodeRenderer.material.color = ResolveRoleColor(cell);
        }

        void CreateDebugConnections(Transform parent, MazeLayout layout, MazeCellData cell)
        {
            TryCreateConnection(parent, layout, cell, MazeCellConnection.East, Vector2Int.right);
            TryCreateConnection(parent, layout, cell, MazeCellConnection.North, Vector2Int.up);
        }

        void TryCreateConnection(
            Transform parent,
            MazeLayout layout,
            MazeCellData sourceCell,
            MazeCellConnection connectionFlag,
            Vector2Int neighborOffset)
        {
            if (!sourceCell.Connections.HasFlag(connectionFlag))
                return;

            if (!layout.TryGetCell(sourceCell.GridPosition + neighborOffset, out _))
                return;

            Vector3 sourcePosition = ResolveCellLocalPosition(sourceCell.GridPosition);
            Vector3 targetPosition = ResolveCellLocalPosition(sourceCell.GridPosition + neighborOffset);
            Vector3 midpoint = (sourcePosition + targetPosition) * 0.5f;
            Vector3 delta = targetPosition - sourcePosition;

            GameObject linkObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            linkObject.name = $"Link_{sourceCell.GridPosition.x}_{sourceCell.GridPosition.y}_{neighborOffset.x}_{neighborOffset.y}";
            linkObject.transform.SetParent(parent, false);
            linkObject.transform.localPosition = midpoint;

            bool horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
            linkObject.transform.localScale = horizontal
                ? new Vector3(m_DebugCellSize, m_DebugNodeHeight * 0.5f, m_DebugConnectionThickness)
                : new Vector3(m_DebugConnectionThickness, m_DebugNodeHeight * 0.5f, m_DebugCellSize);

            Collider linkCollider = linkObject.GetComponent<Collider>();
            if (linkCollider != null)
                DestroyImmediateOrRuntime(linkCollider);

            Renderer linkRenderer = linkObject.GetComponent<Renderer>();
            if (linkRenderer != null)
                linkRenderer.material.color = new Color(0.28f, 0.34f, 0.4f, 1f);
        }

        Vector3 ResolveCellLocalPosition(Vector2Int gridPosition)
        {
            return m_DebugOrigin + new Vector3(gridPosition.x * m_DebugCellSize, 0f, gridPosition.y * m_DebugCellSize);
        }

        static void DestroyImmediateOrRuntime(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        static Color ResolveRoleColor(MazeCellData cell)
        {
            return cell.Role switch
            {
                MazeCellRole.Start => new Color(0.32f, 1f, 0.46f, 1f),
                MazeCellRole.Goal => new Color(0.18f, 0.9f, 1f, 1f),
                MazeCellRole.Refill => new Color(1f, 0.78f, 0.22f, 1f),
                MazeCellRole.HealthRefill => new Color(0.36f, 1f, 0.58f, 1f),
                MazeCellRole.Reward => new Color(1f, 0.55f, 0.14f, 1f),
                MazeCellRole.Trap => new Color(1f, 0.28f, 0.28f, 1f),
                MazeCellRole.Safe => new Color(0.86f, 0.92f, 1f, 1f),
                _ when cell.IsMainPath && cell.PathZone == MazePathZone.Start => new Color(0.72f, 0.9f, 1f, 1f),
                _ when cell.IsMainPath && cell.PathZone == MazePathZone.Mid => new Color(0.5f, 0.72f, 0.98f, 1f),
                _ when cell.IsMainPath && cell.PathZone == MazePathZone.Late => new Color(0.34f, 0.56f, 0.94f, 1f),
                _ when cell.IsMainPath => new Color(0.74f, 0.82f, 0.92f, 1f),
                _ => new Color(0.52f, 0.52f, 0.56f, 1f),
            };
        }

        static Transform EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
                return child;

            var childObject = new GameObject(childName);
            childObject.transform.SetParent(parent, false);
            return childObject.transform;
        }

        static void ClearChildren(Transform root)
        {
            if (root == null)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject childObject = root.GetChild(i).gameObject;
                DestroyGeneratedObject(childObject);
            }
        }

        static void DestroyGeneratedObject(GameObject target)
        {
            if (target == null)
                return;

            // Runtime generation rebuilds content immediately. Deferred Destroy would leave
            // "doomed" parent nodes in place for the current frame, and newly generated
            // children would be attached under them and disappear at frame end.
            Object.DestroyImmediate(target);
        }

        sealed class BatchSeedSmokeTestReport
        {
            readonly int m_StartSeed;
            readonly int m_SeedCount;
            readonly System.Collections.Generic.List<int> m_FailedSeeds = new();

            long m_TotalMainPathLength;
            long m_TotalTargetMainPathLength;
            long m_TotalBranchCount;
            long m_TotalCellCount;
            long m_ElapsedMilliseconds;
            int m_MinMainPathLength = int.MaxValue;
            int m_MaxMainPathLength = int.MinValue;
            int m_MinTargetMainPathLength = int.MaxValue;
            int m_MaxTargetMainPathLength = int.MinValue;
            int m_MinBranchCount = int.MaxValue;
            int m_MaxBranchCount = int.MinValue;
            int m_MinCellCount = int.MaxValue;
            int m_MaxCellCount = int.MinValue;

            public BatchSeedSmokeTestReport(int startSeed, int seedCount)
            {
                m_StartSeed = startSeed;
                m_SeedCount = seedCount;
            }

            public int SuccessCount { get; private set; }
            public int FailureCount { get; private set; }
            public int ConstrainedSpineCount { get; private set; }
            public int DeterministicFallbackSpiralCount { get; private set; }
            public int DeterministicFallbackUTurnCount { get; private set; }
            public int SnakeFallbackCount { get; private set; }
            public int DegradedSnakeFallbackCount { get; private set; }

            public void Record(int seed, MazeLayout layout)
            {
                if (layout == null)
                {
                    FailureCount++;
                    if (m_FailedSeeds.Count < 16)
                        m_FailedSeeds.Add(seed);

                    return;
                }

                SuccessCount++;
                RecordMode(layout);
                RecordStat(ref m_MinMainPathLength, ref m_MaxMainPathLength, ref m_TotalMainPathLength, layout.MainPathLength);
                RecordStat(ref m_MinTargetMainPathLength, ref m_MaxTargetMainPathLength, ref m_TotalTargetMainPathLength, layout.TargetMainPathLength);
                RecordStat(ref m_MinBranchCount, ref m_MaxBranchCount, ref m_TotalBranchCount, layout.BranchCount);
                RecordStat(ref m_MinCellCount, ref m_MaxCellCount, ref m_TotalCellCount, layout.TotalCellCount);
            }

            public void SetElapsedMilliseconds(long elapsedMilliseconds)
            {
                m_ElapsedMilliseconds = elapsedMilliseconds;
            }

            public string BuildSummary()
            {
                int deterministicFallbackCount = DeterministicFallbackSpiralCount + DeterministicFallbackUTurnCount;
                double averageMillisecondsPerSeed = m_SeedCount > 0
                    ? (double)m_ElapsedMilliseconds / m_SeedCount
                    : 0d;

                var lines = new System.Collections.Generic.List<string>
                {
                    $"Batch Seed Smoke Test",
                    $"Seeds: {m_StartSeed} -> {unchecked(m_StartSeed + Mathf.Max(0, m_SeedCount - 1))} ({m_SeedCount} total)",
                    $"Results: {SuccessCount} success / {FailureCount} failure",
                    $"Elapsed: {m_ElapsedMilliseconds} ms total ({averageMillisecondsPerSeed:F2} ms/seed)",
                    $"Main path builders: Constrained={ConstrainedSpineCount}, DeterministicFallback={deterministicFallbackCount} (Spiral={DeterministicFallbackSpiralCount}, UTurn={DeterministicFallbackUTurnCount}), Snake={SnakeFallbackCount} (Degraded={DegradedSnakeFallbackCount})",
                };

                if (SuccessCount > 0)
                {
                    lines.Add(
                        $"Main path length: min {m_MinMainPathLength}, max {m_MaxMainPathLength}, avg {FormatAverage(m_TotalMainPathLength, SuccessCount)} (target avg {FormatAverage(m_TotalTargetMainPathLength, SuccessCount)})");
                    lines.Add($"Branch count: min {m_MinBranchCount}, max {m_MaxBranchCount}, avg {FormatAverage(m_TotalBranchCount, SuccessCount)}");
                    lines.Add($"Total cells: min {m_MinCellCount}, max {m_MaxCellCount}, avg {FormatAverage(m_TotalCellCount, SuccessCount)}");
                }

                lines.Add(
                    FailureCount > 0
                        ? $"Failed seeds (first {m_FailedSeeds.Count}): {string.Join(", ", m_FailedSeeds)}"
                        : "Failed seeds: none");

                return string.Join("\n", lines);
            }

            void RecordMode(MazeLayout layout)
            {
                switch (layout.ResolvedMainPathMode)
                {
                    case MazeResolvedMainPathMode.ConstrainedSpine:
                        ConstrainedSpineCount++;
                        break;
                    case MazeResolvedMainPathMode.DeterministicFallbackSpiral:
                        DeterministicFallbackSpiralCount++;
                        break;
                    case MazeResolvedMainPathMode.DeterministicFallbackUTurn:
                        DeterministicFallbackUTurnCount++;
                        break;
                    case MazeResolvedMainPathMode.SnakeFallback:
                        SnakeFallbackCount++;
                        if (layout.UsedDegradedMainPathLength)
                            DegradedSnakeFallbackCount++;
                        break;
                }
            }

            static void RecordStat(ref int minimum, ref int maximum, ref long total, int value)
            {
                minimum = Mathf.Min(minimum, value);
                maximum = Mathf.Max(maximum, value);
                total += value;
            }

            static string FormatAverage(long total, int count)
            {
                return count > 0
                    ? ((double)total / count).ToString("F2")
                    : "0.00";
            }
        }

        MazeCurrentLayoutSummaryExport BuildCurrentLayoutSummary()
        {
            if (CurrentLayout == null)
                return null;

            return new MazeCurrentLayoutSummaryExport
            {
                seed = CurrentLayout.Seed,
                mainPathLength = CurrentLayout.MainPathLength,
                targetMainPathLength = CurrentLayout.TargetMainPathLength,
                branchCount = CurrentLayout.BranchCount,
                totalCellCount = CurrentLayout.TotalCellCount,
                resolvedMainPathMode = CurrentLayout.ResolvedMainPathMode.ToString(),
                usedDegradedMainPathLength = CurrentLayout.UsedDegradedMainPathLength,
            };
        }

        [System.Serializable]
        sealed class MazeParameterExportSnapshot
        {
            public string exportedAtLocal;
            public string exportedAtUtc;
            public string sourceScenePath;
            public int profileVersion;
            public bool useFixedSeed;
            public int fixedSeed;
            public int currentSeed;
            public int exportSeed;
            public bool isGenerationConfigurationValid;
            public string generationValidationMessage;
            public string lastGeneratedMainPathBuildMode;
            public int mazeSize;
            public MazeFeatureCountExport featureCounts;
            public MazePlacementRuleExport placementRules;
            public MazeSkeletonRuleExport skeletonRules;
            public MazeDerivedProfileExport derivedProfile;
            public MazeBatchSeedExport batchSeedSmokeTest;
            public MazeCurrentLayoutSummaryExport currentLayoutSummary;
        }

        [System.Serializable]
        sealed class MazeFeatureCountExport
        {
            public int energyRefillCount;
            public int healthRefillCount;
            public int trapCount;
            public int rewardCount;
        }

        [System.Serializable]
        sealed class MazePlacementRuleExport
        {
            public int minSameTypeCellDistance;
            public int minCrossTypeCellDistance;
            public int startSafeDistance;
            public int goalSafeDistance;
            public int rewardMinBranchDepth;
        }

        [System.Serializable]
        sealed class MazeSkeletonRuleExport
        {
            public int minGoalDistanceFromStart;
            public int branchFreeStartCells;
            public int preferredStraightStartCells;
            public float startZoneRatio;
            public float midZoneRatio;
        }

        [System.Serializable]
        sealed class MazeDerivedProfileExport
        {
            public int gridWidth;
            public int gridHeight;
            public int minMainPathLength;
            public int maxMainPathLength;
            public int maxMainPathAttempts;
            public int minBranchCount;
            public int maxBranchCount;
            public int minBranchLength;
            public int maxBranchLength;
        }

        [System.Serializable]
        sealed class MazeBatchSeedExport
        {
            public int startSeed;
            public int seedCount;
        }

        [System.Serializable]
        sealed class MazeCurrentLayoutSummaryExport
        {
            public int seed;
            public int mainPathLength;
            public int targetMainPathLength;
            public int branchCount;
            public int totalCellCount;
            public string resolvedMainPathMode;
            public bool usedDegradedMainPathLength;
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(MazeRunBootstrap))]
    sealed class MazeRunBootstrapEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            MazeRunBootstrap bootstrap = (MazeRunBootstrap)target;
            bool isValid = bootstrap.TryGetGenerationValidationMessage(out string validationMessage);
            bool isBatchValid = bootstrap.TryGetBatchSeedTestValidationMessage(out string batchValidationMessage);
            int guaranteedSlots = bootstrap.GetGuaranteedSpecialSlotCount();
            int totalRequested = bootstrap.GetTotalRequestedSpecialCount();
            MazeGeneratorSizeProfile sizeProfile = MazeGenerator.BuildSquareSizeProfile(bootstrap.MazeSize);
            int effectiveMinimumPathLength = Mathf.Max(sizeProfile.MinMainPathLength, bootstrap.MinGoalDistanceFromStart);
            int effectiveMaximumPathLength = Mathf.Max(sizeProfile.MaxMainPathLength, effectiveMinimumPathLength);
            int earliestBranchAnchorIndex = Mathf.Max(
                Mathf.Clamp(bootstrap.BranchFreeStartCells, 1, Mathf.Max(1, effectiveMaximumPathLength - 2)),
                Mathf.CeilToInt((effectiveMaximumPathLength - 1) * 0.5f));
            int interiorCellCount = Mathf.Max(0, effectiveMaximumPathLength - 2);
            int startZoneInteriorCells = Mathf.Clamp(Mathf.FloorToInt(interiorCellCount * bootstrap.StartZoneRatio), 0, interiorCellCount);
            int remainingInteriorAfterStart = Mathf.Max(0, interiorCellCount - startZoneInteriorCells);
            int midZoneInteriorCells = Mathf.Clamp(Mathf.FloorToInt(interiorCellCount * bootstrap.MidZoneRatio), 0, remainingInteriorAfterStart);
            int lateZoneInteriorCells = Mathf.Max(0, interiorCellCount - startZoneInteriorCells - midZoneInteriorCells);

            EditorGUILayout.Space();
            if (isValid)
            {
                EditorGUILayout.HelpBox(
                    $"Inspector values are staged until you press Generate Maze.\nGuaranteed special slot budget: {guaranteedSlots}\nCurrently requested: {totalRequested}",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Generation is disabled until these values are fixed:\n{validationMessage}",
                    MessageType.Error);
            }

            EditorGUILayout.Space();
            if (isBatchValid)
            {
                EditorGUILayout.HelpBox(
                    "Batch seed smoke test runs logic-only generation across a contiguous seed range. It does not rebuild the scene each time.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Batch seed smoke test is disabled until these values are fixed:\n{batchValidationMessage}",
                    MessageType.Error);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Derived Generator Profile", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Grid", $"{sizeProfile.GridWidth} x {sizeProfile.GridHeight}");
            EditorGUILayout.LabelField("Main Path Length", $"{effectiveMinimumPathLength} - {effectiveMaximumPathLength}");
            EditorGUILayout.LabelField("Main Path Attempts", sizeProfile.MaxMainPathAttempts.ToString());
            EditorGUILayout.LabelField("Branch Count", $"{sizeProfile.MinBranchCount} - {sizeProfile.MaxBranchCount}");
            EditorGUILayout.LabelField("Branch Length", $"{sizeProfile.MinBranchLength} - {sizeProfile.MaxBranchLength}");
            EditorGUILayout.LabelField("Min Same-Type Cell Distance", bootstrap.MinSameTypeCellDistance.ToString());
            EditorGUILayout.LabelField("Min Cross-Type Cell Distance", bootstrap.MinCrossTypeCellDistance.ToString());
            EditorGUILayout.LabelField("Start Safe Distance", bootstrap.StartSafeDistance.ToString());
            EditorGUILayout.LabelField("Goal Safe Distance", bootstrap.GoalSafeDistance.ToString());
            EditorGUILayout.LabelField("Reward Min Branch Depth", bootstrap.RewardMinBranchDepth.ToString());
            EditorGUILayout.LabelField("Branch-Free Start Cells", bootstrap.BranchFreeStartCells.ToString());
            EditorGUILayout.LabelField("Preferred Straight Start Cells", bootstrap.PreferredStraightStartCells.ToString());
            EditorGUILayout.LabelField("Path Zones", $"Start {startZoneInteriorCells + 1} / Mid {midZoneInteriorCells} / Late {lateZoneInteriorCells + 1}");
            EditorGUILayout.LabelField("Earliest Branch Anchor", $"Step {earliestBranchAnchorIndex}+");

            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || !isValid))
            {
                if (GUILayout.Button("Generate Maze"))
                {
                    RegenerateBootstrap(bootstrap, MazeMainPathBuildMode.Normal);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Randomize Fixed Seed + Generate Maze"))
                {
                    Undo.RecordObject(bootstrap, "Randomize Maze Seed");
                    bootstrap.RandomizeFixedSeed();
                    EditorUtility.SetDirty(bootstrap);
                    RegenerateBootstrap(bootstrap, MazeMainPathBuildMode.Normal);
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Debug Main Path Builders", EditorStyles.boldLabel);

                if (GUILayout.Button("Generate Deterministic Fallback Preview"))
                {
                    RegenerateBootstrap(bootstrap, MazeMainPathBuildMode.DeterministicFallbackOnly);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Generate Snake Fallback Preview"))
                {
                    RegenerateBootstrap(bootstrap, MazeMainPathBuildMode.SnakeOnly);
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Batch Seed Smoke Test", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || Application.isPlaying || !isValid || !isBatchValid))
            {
                if (GUILayout.Button("Run Batch Seed Smoke Test"))
                {
                    bootstrap.RunBatchSeedSmokeTest();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.HelpBox(bootstrap.LastBatchSeedTestSummary, MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Parameter Export", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Resolved Export Folder", bootstrap.GetParameterExportDirectoryPath());
            EditorGUILayout.LabelField("Default Export Folder", MazeRunBootstrap.GetDefaultParameterExportDirectoryPath());
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling || Application.isPlaying))
            {
                if (GUILayout.Button("Export Current Maze Parameters"))
                {
                    bootstrap.ExportCurrentParameterSnapshot();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.HelpBox(bootstrap.LastParameterExportPath, MessageType.None);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Profile Override", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(bootstrap.LastRuntimeProfileSummary, MessageType.None);

            EditorGUILayout.LabelField("Current Generated Seed", bootstrap.CurrentSeed.ToString());
            EditorGUILayout.LabelField("Last Main Path Mode", bootstrap.LastGeneratedMainPathBuildMode.ToString());
        }

        static void RegenerateBootstrap(MazeRunBootstrap bootstrap, MazeMainPathBuildMode buildMode)
        {
            if (bootstrap == null)
                return;

            if (!bootstrap.TryGetGenerationValidationMessage(out string validationMessage))
            {
                Debug.LogWarning($"Cannot generate random maze because the configuration is invalid:\n{validationMessage}", bootstrap);
                return;
            }

            Undo.RecordObject(bootstrap, "Generate Random Maze");
            bootstrap.EnsureGeneratedHierarchy();
            bootstrap.GenerateLogicLayout(buildMode);
            EditorUtility.SetDirty(bootstrap);

            if (bootstrap.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
        }

        static MazeRunBootstrap FindBootstrapFromSelection()
        {
            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
                return null;

            return selectedObject.GetComponentInParent<MazeRunBootstrap>();
        }

        [MenuItem("GameObject/CIS5680/Random Maze/Generate Maze", false, 20)]
        static void RegenerateSelectedBootstrap()
        {
            MazeRunBootstrap bootstrap = FindBootstrapFromSelection();
            if (bootstrap == null)
            {
                Debug.LogWarning("Select the GeneratedMazeRoot or one of its children before generating the random maze.");
                return;
            }

            RegenerateBootstrap(bootstrap, MazeMainPathBuildMode.Normal);
            Selection.activeObject = bootstrap;
        }

        [MenuItem("GameObject/CIS5680/Random Maze/Generate Maze", true)]
        static bool ValidateRegenerateSelectedBootstrap()
        {
            return FindBootstrapFromSelection() != null;
        }

        [MenuItem("GameObject/CIS5680/Random Maze/Randomize Fixed Seed And Generate Maze", false, 21)]
        static void RandomizeAndRegenerateSelectedBootstrap()
        {
            MazeRunBootstrap bootstrap = FindBootstrapFromSelection();
            if (bootstrap == null)
            {
                Debug.LogWarning("Select the GeneratedMazeRoot or one of its children before generating the random maze.");
                return;
            }

            bootstrap.RandomizeFixedSeed();
            RegenerateBootstrap(bootstrap, MazeMainPathBuildMode.Normal);
            Selection.activeObject = bootstrap;
        }

        [MenuItem("GameObject/CIS5680/Random Maze/Randomize Fixed Seed And Generate Maze", true)]
        static bool ValidateRandomizeAndRegenerateSelectedBootstrap()
        {
            return FindBootstrapFromSelection() != null;
        }
    }
#endif
}
