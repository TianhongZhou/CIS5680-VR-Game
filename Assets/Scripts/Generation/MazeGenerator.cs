using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CIS5680VRGame.Generation
{
    public enum MazeMainPathBuildMode
    {
        Normal = 0,
        DeterministicFallbackOnly = 1,
        SnakeOnly = 2,
    }

    public enum MazeResolvedMainPathMode
    {
        None = 0,
        ConstrainedSpine = 1,
        DeterministicFallbackSpiral = 2,
        DeterministicFallbackUTurn = 3,
        SnakeFallback = 4,
    }

    [DisallowMultipleComponent]
    public class MazeGenerator : MonoBehaviour
    {
        static readonly Vector2Int[] CardinalDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left,
        };

        bool m_EmitDiagnostics = true;

        [Header("Grid")]
        [SerializeField, HideInInspector, Min(5)] int m_GridWidth = 9;
        [SerializeField, HideInInspector, Min(5)] int m_GridHeight = 9;

        [Header("Main Path")]
        [SerializeField, HideInInspector, Min(4)] int m_MinMainPathLength = 7;
        [SerializeField, HideInInspector, Min(4)] int m_MaxMainPathLength = 11;
        [SerializeField, HideInInspector, Min(1)] int m_MaxMainPathAttempts = 48;

        [Header("Branches")]
        [SerializeField, HideInInspector, Min(0)] int m_MinBranchCount = 1;
        [SerializeField, HideInInspector, Min(0)] int m_MaxBranchCount = 3;
        [SerializeField, HideInInspector, Min(1)] int m_MinBranchLength = 1;
        [SerializeField, HideInInspector, Min(1)] int m_MaxBranchLength = 3;

        [Header("Role Marking")]
        [SerializeField, HideInInspector, Min(0)] int m_EnergyRefillCount = 1;
        [SerializeField, HideInInspector, Min(0)] int m_HealthRefillCount = 1;
        [SerializeField, HideInInspector, Min(0)] int m_TrapCount = 1;
        [SerializeField, HideInInspector, Min(0)] int m_RewardCount = 2;
        [SerializeField, HideInInspector, Min(0)] int m_HiddenDoorCount = 1;

        [Header("Placement Rules")]
        [SerializeField, HideInInspector, Min(0)] int m_MinSameTypeCellDistance = 2;
        [SerializeField, HideInInspector, Min(0)] int m_MinCrossTypeCellDistance = 2;
        [SerializeField, HideInInspector, Min(0)] int m_StartSafeDistance = 2;
        [SerializeField, HideInInspector, Min(0)] int m_GoalSafeDistance = 2;
        [SerializeField, HideInInspector, Min(0)] int m_RewardMinBranchDepth = 2;

        [Header("Skeleton Rules")]
        [SerializeField, HideInInspector, Min(4)] int m_MinGoalDistanceFromStart = 7;
        [SerializeField, HideInInspector, Min(0)] int m_BranchFreeStartCells = 3;
        [SerializeField, HideInInspector, Min(0)] int m_PreferredStraightStartCells = 3;
        [SerializeField, HideInInspector] float m_StartZoneRatio = 0.25f;
        [SerializeField, HideInInspector] float m_MidZoneRatio = 0.35f;

        public MazeLayout GenerateLayout(int seed)
        {
            return GenerateLayout(seed, MazeMainPathBuildMode.Normal, true);
        }

        public MazeLayout GenerateLayout(int seed, MazeMainPathBuildMode buildMode)
        {
            return GenerateLayout(seed, buildMode, true);
        }

        public MazeLayout GenerateLayout(int seed, MazeMainPathBuildMode buildMode, bool emitDiagnostics)
        {
            bool previousEmitDiagnostics = m_EmitDiagnostics;
            m_EmitDiagnostics = emitDiagnostics;

            try
            {
                NormalizeSerializedValues();

                var rng = new System.Random(seed);
                RectInt bounds = BuildBounds();

                if (!TryGenerateMainPath(
                        seed,
                        rng,
                        bounds,
                        buildMode,
                        out List<Vector2Int> mainPath,
                        out MazeResolvedMainPathMode resolvedMainPathMode,
                        out int targetMainPathLength))
                {
                    if (m_EmitDiagnostics)
                        Debug.LogError($"MazeGenerator failed to build a main path with seed {seed} using mode {buildMode}.", this);

                    return null;
                }

                var layout = new MazeLayout(seed)
                {
                    ResolvedMainPathMode = resolvedMainPathMode,
                    TargetMainPathLength = targetMainPathLength,
                };

                BuildMainPath(layout, mainPath);
                AddBranches(layout, rng, bounds, mainPath);
                ApplyCellRoles(layout, rng, mainPath);
                return layout;
            }
            finally
            {
                m_EmitDiagnostics = previousEmitDiagnostics;
            }
        }

        public void ApplySquareSizeProfile(int mazeSize)
        {
            MazeGeneratorSizeProfile profile = BuildSquareSizeProfile(mazeSize);

            m_GridWidth = profile.GridWidth;
            m_GridHeight = profile.GridHeight;
            m_MinMainPathLength = profile.MinMainPathLength;
            m_MaxMainPathLength = profile.MaxMainPathLength;
            m_MaxMainPathAttempts = profile.MaxMainPathAttempts;
            m_MinBranchCount = profile.MinBranchCount;
            m_MaxBranchCount = profile.MaxBranchCount;
            m_MinBranchLength = profile.MinBranchLength;
            m_MaxBranchLength = profile.MaxBranchLength;

            NormalizeSerializedValues();
        }

        public void ApplyPlacementProfile(int energyRefillCount, int healthRefillCount, int trapCount, int rewardCount, int hiddenDoorCount)
        {
            m_EnergyRefillCount = Mathf.Max(0, energyRefillCount);
            m_HealthRefillCount = Mathf.Max(0, healthRefillCount);
            m_TrapCount = Mathf.Max(0, trapCount);
            m_RewardCount = Mathf.Max(0, rewardCount);
            m_HiddenDoorCount = Mathf.Max(0, hiddenDoorCount);
            NormalizeSerializedValues();
        }

        public void ApplyPlacementRuleProfile(
            int minSameTypeCellDistance,
            int minCrossTypeCellDistance,
            int startSafeDistance,
            int goalSafeDistance,
            int rewardMinBranchDepth)
        {
            m_MinSameTypeCellDistance = Mathf.Max(0, minSameTypeCellDistance);
            m_MinCrossTypeCellDistance = Mathf.Max(0, minCrossTypeCellDistance);
            m_StartSafeDistance = Mathf.Max(0, startSafeDistance);
            m_GoalSafeDistance = Mathf.Max(0, goalSafeDistance);
            m_RewardMinBranchDepth = Mathf.Max(0, rewardMinBranchDepth);
            NormalizeSerializedValues();
        }

        public void ApplySkeletonProfile(
            int minGoalDistanceFromStart,
            int branchFreeStartCells,
            int preferredStraightStartCells,
            float startZoneRatio,
            float midZoneRatio)
        {
            m_MinGoalDistanceFromStart = Mathf.Max(4, minGoalDistanceFromStart);
            m_BranchFreeStartCells = Mathf.Max(0, branchFreeStartCells);
            m_PreferredStraightStartCells = Mathf.Max(0, preferredStraightStartCells);
            m_StartZoneRatio = Mathf.Clamp01(startZoneRatio);
            m_MidZoneRatio = Mathf.Clamp01(midZoneRatio);
            NormalizeSerializedValues();
        }

        public static int EstimateGuaranteedSpecialSlotCountForSquareSize(int mazeSize)
        {
            MazeGeneratorSizeProfile profile = BuildSquareSizeProfile(mazeSize);
            int minimumGeneratedCells = profile.MinMainPathLength + (profile.MinBranchCount * profile.MinBranchLength);

            // Start, goal, and the early "safe" cell stay reserved.
            return Mathf.Max(0, minimumGeneratedCells - 3);
        }

        public static MazeGeneratorSizeProfile BuildSquareSizeProfile(int mazeSize)
        {
            int normalizedSize = Mathf.Max(5, mazeSize);
            int maxCells = Mathf.Max(4, normalizedSize * normalizedSize);

            return new MazeGeneratorSizeProfile(
                normalizedSize,
                normalizedSize,
                Mathf.Clamp(normalizedSize - 2, 4, maxCells),
                Mathf.Clamp(normalizedSize + 2, 4, maxCells),
                Mathf.Max(48, normalizedSize * normalizedSize),
                Mathf.Max(1, Mathf.FloorToInt(normalizedSize / 8f)),
                Mathf.Max(Mathf.Max(1, Mathf.FloorToInt(normalizedSize / 8f)), Mathf.CeilToInt(normalizedSize / 3f)),
                Mathf.Max(1, Mathf.FloorToInt(normalizedSize / 6f)),
                Mathf.Max(Mathf.Max(1, Mathf.FloorToInt(normalizedSize / 6f)), Mathf.CeilToInt(normalizedSize / 3f)));
        }

        void OnValidate()
        {
            NormalizeSerializedValues();
        }

        void NormalizeSerializedValues()
        {
            m_GridWidth = Mathf.Max(5, m_GridWidth);
            m_GridHeight = Mathf.Max(5, m_GridHeight);
            m_MaxMainPathAttempts = Mathf.Max(1, m_MaxMainPathAttempts);

            int maxCells = Mathf.Max(4, m_GridWidth * m_GridHeight);
            m_MinMainPathLength = Mathf.Clamp(m_MinMainPathLength, 4, maxCells);
            m_MaxMainPathLength = Mathf.Clamp(m_MaxMainPathLength, m_MinMainPathLength, maxCells);

            m_MinBranchCount = Mathf.Max(0, m_MinBranchCount);
            m_MaxBranchCount = Mathf.Max(m_MinBranchCount, m_MaxBranchCount);
            m_MinBranchLength = Mathf.Max(1, m_MinBranchLength);
            m_MaxBranchLength = Mathf.Max(m_MinBranchLength, m_MaxBranchLength);
            m_EnergyRefillCount = Mathf.Max(0, m_EnergyRefillCount);
            m_HealthRefillCount = Mathf.Max(0, m_HealthRefillCount);
            m_TrapCount = Mathf.Max(0, m_TrapCount);
            m_RewardCount = Mathf.Max(0, m_RewardCount);
            m_HiddenDoorCount = Mathf.Max(0, m_HiddenDoorCount);
            m_MinSameTypeCellDistance = Mathf.Max(0, m_MinSameTypeCellDistance);
            m_MinCrossTypeCellDistance = Mathf.Max(0, m_MinCrossTypeCellDistance);
            m_StartSafeDistance = Mathf.Max(0, m_StartSafeDistance);
            m_GoalSafeDistance = Mathf.Max(0, m_GoalSafeDistance);
            m_RewardMinBranchDepth = Mathf.Max(0, m_RewardMinBranchDepth);
            m_MinGoalDistanceFromStart = Mathf.Max(4, m_MinGoalDistanceFromStart);
            m_BranchFreeStartCells = Mathf.Max(0, m_BranchFreeStartCells);
            m_PreferredStraightStartCells = Mathf.Max(0, m_PreferredStraightStartCells);
            m_StartZoneRatio = Mathf.Clamp01(m_StartZoneRatio);
            m_MidZoneRatio = Mathf.Clamp01(m_MidZoneRatio);

            if (m_StartZoneRatio + m_MidZoneRatio >= 0.99f)
                m_MidZoneRatio = Mathf.Clamp01(0.98f - m_StartZoneRatio);
        }

        RectInt BuildBounds()
        {
            int minX = -m_GridWidth / 2;
            int minY = -m_GridHeight / 2;
            return new RectInt(minX, minY, m_GridWidth, m_GridHeight);
        }

        bool TryGenerateMainPath(
            int seed,
            System.Random rng,
            RectInt bounds,
            MazeMainPathBuildMode buildMode,
            out List<Vector2Int> mainPath,
            out MazeResolvedMainPathMode resolvedMainPathMode,
            out int targetLength)
        {
            int minimumTargetLength = Mathf.Max(m_MinMainPathLength, m_MinGoalDistanceFromStart);
            int maximumTargetLength = Mathf.Max(minimumTargetLength, m_MaxMainPathLength);
            targetLength = rng.Next(minimumTargetLength, maximumTargetLength + 1);
            Vector2Int start = Vector2Int.zero;

            if (!bounds.Contains(start))
                start = new Vector2Int(bounds.xMin + bounds.width / 2, bounds.yMin + bounds.height / 2);

            if (buildMode == MazeMainPathBuildMode.DeterministicFallbackOnly)
            {
                if (TryBuildFallbackMainPath(seed, bounds, targetLength, start, out mainPath, out FallbackMainPathTemplate forcedFallbackTemplate))
                {
                    resolvedMainPathMode = ResolveResolvedMainPathMode(forcedFallbackTemplate);
                    return true;
                }

                resolvedMainPathMode = MazeResolvedMainPathMode.None;
                return false;
            }

            if (buildMode == MazeMainPathBuildMode.SnakeOnly)
            {
                bool snakeBuilt = TryBuildSnakeFallbackPath(seed, bounds, targetLength, start, out mainPath, out _);
                resolvedMainPathMode = snakeBuilt ? MazeResolvedMainPathMode.SnakeFallback : MazeResolvedMainPathMode.None;
                return snakeBuilt;
            }

            var occupied = new HashSet<Vector2Int> { start };
            mainPath = new List<Vector2Int> { start };
            int remainingSearchBudget = Mathf.Max(m_MaxMainPathAttempts, targetLength * 4);

            if (TryBuildConstrainedSpine(rng, bounds, occupied, mainPath, targetLength, start, ref remainingSearchBudget))
            {
                resolvedMainPathMode = MazeResolvedMainPathMode.ConstrainedSpine;
                return true;
            }

            if (TryBuildFallbackMainPath(seed, bounds, targetLength, start, out mainPath, out FallbackMainPathTemplate fallbackTemplate))
            {
                if (m_EmitDiagnostics)
                {
                    Debug.LogWarning(
                        $"MazeGenerator fallback main path builder used template {fallbackTemplate} for seed {seed}.",
                        this);
                }

                resolvedMainPathMode = ResolveResolvedMainPathMode(fallbackTemplate);
                return true;
            }

            if (TryBuildSnakeFallbackPath(seed, bounds, targetLength, start, out mainPath, out int snakeLength))
            {
                if (m_EmitDiagnostics)
                {
                    string lengthNote = snakeLength == targetLength
                        ? "at full target length"
                        : $"at degraded length {snakeLength}/{targetLength}";

                    Debug.LogWarning(
                        $"MazeGenerator final snake fallback used for seed {seed} {lengthNote}.",
                        this);
                }

                resolvedMainPathMode = MazeResolvedMainPathMode.SnakeFallback;
                return true;
            }

            mainPath = null;
            resolvedMainPathMode = MazeResolvedMainPathMode.None;
            return false;
        }

        bool TryBuildFallbackMainPath(
            int seed,
            RectInt bounds,
            int targetLength,
            Vector2Int start,
            out List<Vector2Int> mainPath,
            out FallbackMainPathTemplate fallbackTemplate)
        {
            var fallbackRng = new System.Random(DeriveFallbackSeed(seed, targetLength, bounds));
            List<FallbackTemplateVariant> variants = BuildFallbackVariants(fallbackRng);
            mainPath = null;

            for (int i = 0; i < variants.Count; i++)
            {
                FallbackTemplateVariant variant = variants[i];
                bool built = variant.Template switch
                {
                    FallbackMainPathTemplate.Spiral => TryBuildSpiralFallbackPath(bounds, start, targetLength, variant, out mainPath),
                    FallbackMainPathTemplate.UTurn => TryBuildUTurnFallbackPath(bounds, start, targetLength, variant, out mainPath),
                    _ => false,
                };

                if (built)
                {
                    fallbackTemplate = variant.Template;
                    return true;
                }
            }

            fallbackTemplate = FallbackMainPathTemplate.None;
            return false;
        }

        bool TryBuildSnakeFallbackPath(
            int seed,
            RectInt bounds,
            int targetLength,
            Vector2Int start,
            out List<Vector2Int> mainPath,
            out int generatedLength)
        {
            var snakeRng = new System.Random(DeriveSnakeFallbackSeed(seed, targetLength, bounds));
            List<FallbackTemplateVariant> variants = BuildSnakeVariants(snakeRng);

            int bestCapacity = -1;
            FallbackTemplateVariant bestVariant = default;
            for (int i = 0; i < variants.Count; i++)
            {
                int capacity = MeasureSnakeSweepCapacity(bounds, start, variants[i]);
                if (capacity <= bestCapacity)
                    continue;

                bestCapacity = capacity;
                bestVariant = variants[i];
            }

            if (bestCapacity <= 1)
            {
                mainPath = null;
                generatedLength = 0;
                return false;
            }

            generatedLength = Mathf.Min(targetLength, bestCapacity);
            return TryBuildSnakeSweepPath(bounds, start, generatedLength, bestVariant, out mainPath);
        }

        bool TryBuildConstrainedSpine(
            System.Random rng,
            RectInt bounds,
            HashSet<Vector2Int> occupied,
            List<Vector2Int> currentPath,
            int targetLength,
            Vector2Int start,
            ref int remainingSearchBudget)
        {
            if (currentPath.Count >= targetLength)
                return true;

            if (remainingSearchBudget-- <= 0)
                return false;

            Vector2Int current = currentPath[currentPath.Count - 1];
            List<Vector2Int> directions = BuildRankedSpineDirections(rng, bounds, occupied, currentPath, current, start);

            for (int i = 0; i < directions.Count; i++)
            {
                Vector2Int next = current + directions[i];
                if (!CanOccupy(bounds, occupied, next))
                    continue;

                int remainingStepsAfterMove = targetLength - (currentPath.Count + 1);
                if (remainingStepsAfterMove > 0 && CountOpenNeighbors(bounds, occupied, next) == 0)
                    continue;

                occupied.Add(next);
                currentPath.Add(next);

                if (!HasSufficientSpineCapacity(bounds, occupied, next, remainingStepsAfterMove + 1))
                {
                    currentPath.RemoveAt(currentPath.Count - 1);
                    occupied.Remove(next);
                    continue;
                }

                if (TryBuildConstrainedSpine(rng, bounds, occupied, currentPath, targetLength, start, ref remainingSearchBudget))
                    return true;

                currentPath.RemoveAt(currentPath.Count - 1);
                occupied.Remove(next);
            }

            return false;
        }

        List<Vector2Int> BuildRankedSpineDirections(
            System.Random rng,
            RectInt bounds,
            HashSet<Vector2Int> occupied,
            IReadOnlyList<Vector2Int> currentPath,
            Vector2Int current,
            Vector2Int start)
        {
            List<Vector2Int> shuffledDirections = BuildShuffledDirections(rng);
            var candidates = new List<SpineDirectionCandidate>(shuffledDirections.Count);

            for (int i = 0; i < shuffledDirections.Count; i++)
            {
                Vector2Int direction = shuffledDirections[i];
                Vector2Int next = current + direction;
                candidates.Add(new SpineDirectionCandidate(
                    direction,
                    ResolveDirectionPreference(currentPath, direction),
                    CountOpenNeighbors(bounds, occupied, next),
                    ResolveDistanceFromStartPreference(start, next),
                    i));
            }

            candidates.Sort((left, right) =>
            {
                int comparison = right.StraightPreference.CompareTo(left.StraightPreference);
                if (comparison != 0)
                    return comparison;

                comparison = right.OpenNeighborCount.CompareTo(left.OpenNeighborCount);
                if (comparison != 0)
                    return comparison;

                comparison = right.DistanceFromStart.CompareTo(left.DistanceFromStart);
                if (comparison != 0)
                    return comparison;

                return left.TieBreaker.CompareTo(right.TieBreaker);
            });

            var rankedDirections = new List<Vector2Int>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
                rankedDirections.Add(candidates[i].Direction);

            return rankedDirections;
        }

        bool HasSufficientSpineCapacity(
            RectInt bounds,
            HashSet<Vector2Int> occupied,
            Vector2Int start,
            int requiredReachableCellCount)
        {
            if (requiredReachableCellCount <= 1)
                return true;

            var visited = new HashSet<Vector2Int> { start };
            var frontier = new Queue<Vector2Int>();
            frontier.Enqueue(start);
            int reachableCount = 0;

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                reachableCount++;
                if (reachableCount >= requiredReachableCellCount)
                    return true;

                for (int i = 0; i < CardinalDirections.Length; i++)
                {
                    Vector2Int neighbor = current + CardinalDirections[i];
                    if (!bounds.Contains(neighbor) || !visited.Add(neighbor))
                        continue;

                    if (neighbor != start && occupied.Contains(neighbor))
                        continue;

                    frontier.Enqueue(neighbor);
                }
            }

            return false;
        }

        List<FallbackTemplateVariant> BuildFallbackVariants(System.Random rng)
        {
            List<Vector2Int> directions = BuildShuffledDirections(rng);
            var turnSteps = new List<int> { 1, -1 };
            Shuffle(turnSteps, rng);

            var templates = new List<FallbackMainPathTemplate>
            {
                FallbackMainPathTemplate.Spiral,
                FallbackMainPathTemplate.UTurn,
            };
            Shuffle(templates, rng);

            var variants = new List<FallbackTemplateVariant>();
            for (int templateIndex = 0; templateIndex < templates.Count; templateIndex++)
            {
                for (int directionIndex = 0; directionIndex < directions.Count; directionIndex++)
                {
                    for (int turnIndex = 0; turnIndex < turnSteps.Count; turnIndex++)
                    {
                        variants.Add(new FallbackTemplateVariant(
                            templates[templateIndex],
                            directions[directionIndex],
                            turnSteps[turnIndex]));
                    }
                }
            }

            return variants;
        }

        List<FallbackTemplateVariant> BuildSnakeVariants(System.Random rng)
        {
            List<Vector2Int> directions = BuildShuffledDirections(rng);
            var turnSteps = new List<int> { 1, -1 };
            Shuffle(turnSteps, rng);

            var variants = new List<FallbackTemplateVariant>();
            for (int directionIndex = 0; directionIndex < directions.Count; directionIndex++)
            {
                for (int turnIndex = 0; turnIndex < turnSteps.Count; turnIndex++)
                {
                    variants.Add(new FallbackTemplateVariant(
                        FallbackMainPathTemplate.Snake,
                        directions[directionIndex],
                        turnSteps[turnIndex]));
                }
            }

            return variants;
        }

        bool TryBuildSpiralFallbackPath(
            RectInt bounds,
            Vector2Int start,
            int targetLength,
            FallbackTemplateVariant variant,
            out List<Vector2Int> mainPath)
        {
            var occupied = new HashSet<Vector2Int> { start };
            mainPath = new List<Vector2Int> { start };

            Vector2Int direction = variant.InitialDirection;
            int segmentLength = Mathf.Max(1, m_PreferredStraightStartCells);
            int completedSegments = 0;

            while (mainPath.Count < targetLength)
            {
                int stepsThisSegment = Mathf.Min(segmentLength, targetLength - mainPath.Count);
                if (!TryAdvancePath(bounds, occupied, mainPath, direction, stepsThisSegment))
                {
                    mainPath = null;
                    return false;
                }

                direction = RotateCardinalDirection(direction, variant.TurnStep);
                completedSegments++;
                if (completedSegments % 2 == 0)
                    segmentLength++;
            }

            return true;
        }

        bool TryBuildUTurnFallbackPath(
            RectInt bounds,
            Vector2Int start,
            int targetLength,
            FallbackTemplateVariant variant,
            out List<Vector2Int> mainPath)
        {
            var occupied = new HashSet<Vector2Int> { start };
            mainPath = new List<Vector2Int> { start };

            Vector2Int primaryDirection = variant.InitialDirection;
            Vector2Int lateralDirection = RotateCardinalDirection(primaryDirection, variant.TurnStep);

            int initialRun = Mathf.Max(1, m_PreferredStraightStartCells);
            int initialAvailable = MeasureAvailableRun(bounds, occupied, mainPath[mainPath.Count - 1], primaryDirection);
            if (initialAvailable <= 0)
            {
                mainPath = null;
                return false;
            }

            int initialSteps = Mathf.Min(initialRun, Mathf.Min(initialAvailable, targetLength - mainPath.Count));
            if (!TryAdvancePath(bounds, occupied, mainPath, primaryDirection, initialSteps))
            {
                mainPath = null;
                return false;
            }

            while (mainPath.Count < targetLength)
            {
                int forwardAvailable = MeasureAvailableRun(bounds, occupied, mainPath[mainPath.Count - 1], primaryDirection);
                if (forwardAvailable > 0)
                {
                    int forwardSteps = Mathf.Min(forwardAvailable, targetLength - mainPath.Count);
                    if (!TryAdvancePath(bounds, occupied, mainPath, primaryDirection, forwardSteps))
                    {
                        mainPath = null;
                        return false;
                    }

                    if (mainPath.Count >= targetLength)
                        return true;
                }

                if (!TryAdvancePath(bounds, occupied, mainPath, lateralDirection, 1))
                {
                    mainPath = null;
                    return false;
                }

                primaryDirection = -primaryDirection;
            }

            return true;
        }

        bool TryAdvancePath(
            RectInt bounds,
            HashSet<Vector2Int> occupied,
            List<Vector2Int> path,
            Vector2Int direction,
            int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                Vector2Int next = path[path.Count - 1] + direction;
                if (!CanOccupy(bounds, occupied, next))
                    return false;

                occupied.Add(next);
                path.Add(next);
            }

            return true;
        }

        bool TryBuildSnakeSweepPath(
            RectInt bounds,
            Vector2Int start,
            int targetLength,
            FallbackTemplateVariant variant,
            out List<Vector2Int> mainPath)
        {
            var occupied = new HashSet<Vector2Int> { start };
            mainPath = new List<Vector2Int> { start };

            Vector2Int primaryDirection = variant.InitialDirection;
            Vector2Int lateralDirection = RotateCardinalDirection(primaryDirection, variant.TurnStep);

            int initialAvailable = MeasureAvailableRun(bounds, occupied, mainPath[mainPath.Count - 1], primaryDirection);
            int initialSteps = Mathf.Min(initialAvailable, targetLength - mainPath.Count);
            if (initialSteps > 0 && !TryAdvancePath(bounds, occupied, mainPath, primaryDirection, initialSteps))
            {
                mainPath = null;
                return false;
            }

            while (mainPath.Count < targetLength)
            {
                if (!TryAdvancePath(bounds, occupied, mainPath, lateralDirection, 1))
                {
                    mainPath = null;
                    return false;
                }

                primaryDirection = -primaryDirection;
                int forwardAvailable = MeasureAvailableRun(bounds, occupied, mainPath[mainPath.Count - 1], primaryDirection);
                int forwardSteps = Mathf.Min(forwardAvailable, targetLength - mainPath.Count);
                if (forwardSteps > 0 && !TryAdvancePath(bounds, occupied, mainPath, primaryDirection, forwardSteps))
                {
                    mainPath = null;
                    return false;
                }
            }

            return mainPath.Count == targetLength;
        }

        int MeasureSnakeSweepCapacity(RectInt bounds, Vector2Int start, FallbackTemplateVariant variant)
        {
            var occupied = new HashSet<Vector2Int> { start };
            var mainPath = new List<Vector2Int> { start };

            Vector2Int primaryDirection = variant.InitialDirection;
            Vector2Int lateralDirection = RotateCardinalDirection(primaryDirection, variant.TurnStep);

            int initialAvailable = MeasureAvailableRun(bounds, occupied, mainPath[mainPath.Count - 1], primaryDirection);
            if (initialAvailable > 0)
                TryAdvancePath(bounds, occupied, mainPath, primaryDirection, initialAvailable);

            while (TryAdvancePath(bounds, occupied, mainPath, lateralDirection, 1))
            {
                primaryDirection = -primaryDirection;
                int forwardAvailable = MeasureAvailableRun(bounds, occupied, mainPath[mainPath.Count - 1], primaryDirection);
                if (forwardAvailable > 0)
                    TryAdvancePath(bounds, occupied, mainPath, primaryDirection, forwardAvailable);
            }

            return mainPath.Count;
        }

        int MeasureAvailableRun(
            RectInt bounds,
            HashSet<Vector2Int> occupied,
            Vector2Int origin,
            Vector2Int direction)
        {
            int available = 0;
            Vector2Int current = origin;

            while (true)
            {
                Vector2Int next = current + direction;
                if (!CanOccupy(bounds, occupied, next))
                    return available;

                available++;
                current = next;
            }
        }

        void BuildMainPath(MazeLayout layout, List<Vector2Int> mainPath)
        {
            for (int i = 0; i < mainPath.Count; i++)
            {
                MazeCellData cell = layout.GetOrCreateCell(mainPath[i]);
                cell.IsMainPath = true;
                cell.DistanceFromStart = i;

                if (i == 0)
                    cell.Role = MazeCellRole.Start;
                else if (i == mainPath.Count - 1)
                    cell.Role = MazeCellRole.Goal;
                else if (i == 1)
                    cell.Role = MazeCellRole.Safe;

                if (i > 0)
                    layout.Connect(mainPath[i - 1], mainPath[i]);
            }

            layout.SetMainPath(mainPath);
            AssignMainPathZones(layout, mainPath);
        }

        void AssignMainPathZones(MazeLayout layout, IReadOnlyList<Vector2Int> mainPath)
        {
            if (layout == null || mainPath == null || mainPath.Count == 0)
                return;

            int interiorCount = Mathf.Max(0, mainPath.Count - 2);
            int startInteriorCount = Mathf.Clamp(Mathf.FloorToInt(interiorCount * m_StartZoneRatio), 0, interiorCount);
            int remainingAfterStart = Mathf.Max(0, interiorCount - startInteriorCount);
            int midInteriorCount = Mathf.Clamp(Mathf.FloorToInt(interiorCount * m_MidZoneRatio), 0, remainingAfterStart);

            for (int i = 0; i < mainPath.Count; i++)
            {
                MazeCellData cell = layout.GetCell(mainPath[i]);
                cell.PathZone = ResolvePathZoneForIndex(i, mainPath.Count, startInteriorCount, midInteriorCount);
            }
        }

        static MazePathZone ResolvePathZoneForIndex(
            int mainPathIndex,
            int mainPathCount,
            int startInteriorCount,
            int midInteriorCount)
        {
            if (mainPathCount <= 0)
                return MazePathZone.None;

            if (mainPathIndex <= 0)
                return MazePathZone.Start;

            if (mainPathIndex >= mainPathCount - 1)
                return MazePathZone.Late;

            int interiorIndex = mainPathIndex - 1;
            if (interiorIndex < startInteriorCount)
                return MazePathZone.Start;

            if (interiorIndex < startInteriorCount + midInteriorCount)
                return MazePathZone.Mid;

            return MazePathZone.Late;
        }

        void AddBranches(MazeLayout layout, System.Random rng, RectInt bounds, List<Vector2Int> mainPath)
        {
            if (mainPath.Count < 3 || m_MaxBranchCount <= 0)
                return;

            int requestedBranchCount = rng.Next(m_MinBranchCount, m_MaxBranchCount + 1);
            if (requestedBranchCount <= 0)
                return;

            int branchSafeStartIndex = Mathf.Clamp(m_BranchFreeStartCells, 1, mainPath.Count - 2);
            int midLateStartIndex = Mathf.Max(branchSafeStartIndex, Mathf.CeilToInt((mainPath.Count - 1) * 0.5f));
            var anchorCandidates = new List<int>();
            for (int i = midLateStartIndex; i < mainPath.Count - 1; i++)
                anchorCandidates.Add(i);

            Shuffle(anchorCandidates, rng);

            int branchesBuilt = 0;
            for (int i = 0; i < anchorCandidates.Count && branchesBuilt < requestedBranchCount; i++)
            {
                Vector2Int anchor = mainPath[anchorCandidates[i]];
                int desiredLength = rng.Next(m_MinBranchLength, m_MaxBranchLength + 1);
                if (!TryBuildBranch(layout, rng, bounds, anchor, desiredLength))
                    continue;

                branchesBuilt++;
            }

            layout.BranchCount = branchesBuilt;
        }

        bool TryBuildBranch(
            MazeLayout layout,
            System.Random rng,
            RectInt bounds,
            Vector2Int anchor,
            int desiredLength)
        {
            List<Vector2Int> initialDirections = BuildShuffledDirections(rng);

            for (int i = 0; i < initialDirections.Count; i++)
            {
                Vector2Int first = anchor + initialDirections[i];
                if (!CanOccupy(bounds, layout.OccupiedPositions, first))
                    continue;

                var branchPath = new List<Vector2Int> { first };
                var reserved = new HashSet<Vector2Int>(layout.OccupiedPositions) { first };

                if (!TryExtendBranch(rng, bounds, reserved, branchPath, desiredLength))
                    continue;

                Vector2Int previous = anchor;
                for (int branchStep = 0; branchStep < branchPath.Count; branchStep++)
                {
                    Vector2Int position = branchPath[branchStep];
                    MazeCellData cell = layout.GetOrCreateCell(position);
                    cell.DistanceFromStart = layout.GetCell(previous).DistanceFromStart + 1;

                    layout.Connect(previous, position);
                    previous = position;
                }

                return true;
            }

            return false;
        }

        bool TryExtendBranch(
            System.Random rng,
            RectInt bounds,
            HashSet<Vector2Int> reserved,
            List<Vector2Int> branchPath,
            int targetLength)
        {
            if (branchPath.Count >= targetLength)
                return true;

            Vector2Int current = branchPath[branchPath.Count - 1];
            List<Vector2Int> directions = BuildShuffledDirections(rng);

            for (int i = 0; i < directions.Count; i++)
            {
                Vector2Int next = current + directions[i];
                if (!CanOccupy(bounds, reserved, next))
                    continue;

                int remainingSteps = targetLength - branchPath.Count - 1;
                if (remainingSteps > 0 && CountOpenNeighbors(bounds, reserved, next) == 0)
                    continue;

                reserved.Add(next);
                branchPath.Add(next);

                if (TryExtendBranch(rng, bounds, reserved, branchPath, targetLength))
                    return true;

                branchPath.RemoveAt(branchPath.Count - 1);
                reserved.Remove(next);
            }

            return false;
        }

        void ApplyCellRoles(MazeLayout layout, System.Random rng, List<Vector2Int> mainPath)
        {
            if (layout == null || mainPath == null || mainPath.Count <= 2)
                return;
            AssignEnergyRefills(layout, rng, mainPath);
            AssignHealthRefills(layout, rng, mainPath);
            AssignTraps(layout, rng, mainPath);
            AssignRewards(layout, rng, mainPath);
            AssignHiddenDoors(layout, rng, mainPath);
        }

        void AssignEnergyRefills(MazeLayout layout, System.Random rng, List<Vector2Int> mainPath)
        {
            int remaining = m_EnergyRefillCount;
            if (remaining <= 0)
                return;

            var stageDebug = new List<string>();
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledMainPathZoneCandidates(layout, rng, MazePathZone.Mid),
                MazeCellRole.Refill,
                remaining,
                "strict: main-path mid zone",
                stageDebug);
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledBranchZoneCandidates(layout, rng, mainPath.Count, MazePathZone.Mid),
                MazeCellRole.Refill,
                remaining,
                "fallback-1: branch mid zone",
                stageDebug);
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledMainPathZoneCandidates(layout, rng, MazePathZone.Late),
                MazeCellRole.Refill,
                remaining,
                "fallback-2: main-path late zone",
                stageDebug);
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledBranchZoneCandidates(layout, rng, mainPath.Count, MazePathZone.Late),
                MazeCellRole.Refill,
                remaining,
                "fallback-3: branch late zone",
                stageDebug);

            WarnIfPlacementShortfall(MazeCellRole.Refill, m_EnergyRefillCount, remaining, layout.Seed, stageDebug, "hard-demand");
        }

        void AssignHealthRefills(MazeLayout layout, System.Random rng, List<Vector2Int> mainPath)
        {
            int remaining = m_HealthRefillCount;
            if (remaining <= 0)
                return;

            var stageDebug = new List<string>();
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledMainPathZoneCandidates(layout, rng, MazePathZone.Late),
                MazeCellRole.HealthRefill,
                remaining,
                "strict: main-path late zone",
                stageDebug);
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledMainPathZoneCandidates(layout, rng, MazePathZone.Mid),
                MazeCellRole.HealthRefill,
                remaining,
                "fallback-1: main-path mid zone",
                stageDebug);
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledBranchZoneCandidates(layout, rng, mainPath.Count, MazePathZone.Late),
                MazeCellRole.HealthRefill,
                remaining,
                "fallback-2: branch late zone",
                stageDebug);
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledBranchZoneCandidates(layout, rng, mainPath.Count, MazePathZone.Mid),
                MazeCellRole.HealthRefill,
                remaining,
                "fallback-3: branch mid zone",
                stageDebug);

            WarnIfPlacementShortfall(MazeCellRole.HealthRefill, m_HealthRefillCount, remaining, layout.Seed, stageDebug, "medium-priority");
        }

        void AssignTraps(MazeLayout layout, System.Random rng, List<Vector2Int> mainPath)
        {
            int remaining = m_TrapCount;
            if (remaining <= 0)
                return;

            var stageDebug = new List<string>();
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledMainPathZoneCandidates(layout, rng, MazePathZone.Late),
                MazeCellRole.Trap,
                remaining,
                "strict: main-path late zone",
                stageDebug);
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledBranchZoneCandidates(layout, rng, mainPath.Count, MazePathZone.Late),
                MazeCellRole.Trap,
                remaining,
                "fallback-1: branch late zone",
                stageDebug);
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledBranchZoneCandidates(layout, rng, mainPath.Count, MazePathZone.Mid),
                MazeCellRole.Trap,
                remaining,
                "fallback-2: branch mid zone",
                stageDebug);

            WarnIfPlacementShortfall(MazeCellRole.Trap, m_TrapCount, remaining, layout.Seed, stageDebug, "soft-target");
        }

        void AssignRewards(MazeLayout layout, System.Random rng, List<Vector2Int> mainPath)
        {
            int remaining = m_RewardCount;
            if (remaining <= 0)
                return;

            var stageDebug = new List<string>();
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledCandidates(
                    layout,
                    rng,
                    cell => !cell.IsMainPath
                        && cell.Role == MazeCellRole.Neutral
                        && GetConnectionCount(cell.Connections) == 1
                        && ResolveBranchDepth(layout, cell) >= m_RewardMinBranchDepth
                        && ResolveZoneForCell(cell, mainPath.Count) != MazePathZone.Start),
                MazeCellRole.Reward,
                remaining,
                "strict: non-start branch dead-end",
                stageDebug);
            remaining -= AssignRoleToCandidates(
                layout,
                BuildShuffledCandidates(
                    layout,
                    rng,
                    cell => !cell.IsMainPath
                        && cell.Role == MazeCellRole.Neutral
                        && ResolveBranchDepth(layout, cell) >= m_RewardMinBranchDepth
                        && ResolveZoneForCell(cell, mainPath.Count) != MazePathZone.Start),
                MazeCellRole.Reward,
                remaining,
                "fallback-1: non-start branch cell",
                stageDebug);

            WarnIfPlacementShortfall(MazeCellRole.Reward, m_RewardCount, remaining, layout.Seed, stageDebug, "soft-target");
        }

        void AssignHiddenDoors(MazeLayout layout, System.Random rng, List<Vector2Int> mainPath)
        {
            int remaining = m_HiddenDoorCount;
            if (remaining <= 0)
                return;

            var stageDebug = new List<string>();
            remaining -= AssignHiddenDoorsToCandidates(
                layout,
                BuildShuffledCandidates(
                    layout,
                    rng,
                    cell => cell.Role == MazeCellRole.Reward
                        && !cell.IsMainPath
                        && GetConnectionCount(cell.Connections) == 1
                        && ResolveBranchDepth(layout, cell) >= m_RewardMinBranchDepth),
                remaining,
                "strict: reward branch dead-end",
                stageDebug);
            remaining -= AssignHiddenDoorsToCandidates(
                layout,
                BuildShuffledCandidates(
                    layout,
                    rng,
                    cell => cell.Role == MazeCellRole.Reward
                        && !cell.IsMainPath
                        && ResolveBranchDepth(layout, cell) >= m_RewardMinBranchDepth),
                remaining,
                "fallback-1: reward branch cell",
                stageDebug);
            remaining -= AssignHiddenDoorsToCandidates(
                layout,
                BuildShuffledCandidates(
                    layout,
                    rng,
                    cell => !cell.IsMainPath
                        && cell.Role is MazeCellRole.Neutral or MazeCellRole.Refill or MazeCellRole.HealthRefill
                        && GetConnectionCount(cell.Connections) == 1
                        && ResolveZoneForCell(cell, mainPath.Count) != MazePathZone.Start),
                remaining,
                "fallback-2: non-start branch dead-end",
                stageDebug);
            remaining -= AssignHiddenDoorsToCandidates(
                layout,
                BuildShuffledCandidates(
                    layout,
                    rng,
                    cell => !cell.IsMainPath
                        && cell.Role is MazeCellRole.Neutral or MazeCellRole.Refill or MazeCellRole.HealthRefill
                        && ResolveZoneForCell(cell, mainPath.Count) != MazePathZone.Start),
                remaining,
                "fallback-3: non-start branch cell",
                stageDebug);

            WarnIfHiddenDoorPlacementShortfall(m_HiddenDoorCount, remaining, layout.Seed, stageDebug);
        }

        int AssignHiddenDoorsToCandidates(
            MazeLayout layout,
            IList<Vector2Int> candidates,
            int maxAssignments,
            string stageLabel,
            ICollection<string> stageDebug)
        {
            if (layout == null || candidates == null || maxAssignments <= 0)
                return 0;

            int assigned = 0;
            for (int i = 0; i < candidates.Count && assigned < maxAssignments; i++)
            {
                if (!TryAssignHiddenDoorAt(layout, candidates[i]))
                    continue;

                assigned++;
            }

            stageDebug?.Add($"{stageLabel}: {assigned}/{maxAssignments}");
            return assigned;
        }

        bool TryAssignHiddenDoorAt(MazeLayout layout, Vector2Int position)
        {
            if (layout == null || !layout.TryGetCell(position, out MazeCellData cell))
                return false;

            if (cell == null || cell.IsMainPath || cell.HiddenDoorConnections != MazeCellConnection.None)
                return false;

            if (cell.Role is MazeCellRole.Start or MazeCellRole.Goal or MazeCellRole.Trap)
                return false;

            MazeCellData predecessor = FindPathPredecessor(layout, cell);
            if (predecessor == null)
                return false;

            MazeCellConnection doorConnection = MazeLayout.ResolveConnectionFlag(cell.GridPosition, predecessor.GridPosition);
            if (!cell.Connections.HasFlag(doorConnection))
                return false;

            cell.HiddenDoorConnections |= doorConnection;
            return true;
        }

        int AssignRoleToCandidates(
            MazeLayout layout,
            IList<Vector2Int> candidates,
            MazeCellRole role,
            int maxAssignments,
            string stageLabel,
            ICollection<string> stageDebug)
        {
            if (layout == null || candidates == null || maxAssignments <= 0)
                return 0;

            int assignedWithSoftRules = 0;
            for (int i = 0; i < candidates.Count && assignedWithSoftRules < maxAssignments; i++)
            {
                if (!TryAssignRoleAt(layout, candidates[i], role, requireSoftCrossTypeSpacing: true))
                    continue;

                assignedWithSoftRules++;
            }

            int remainingAfterSoftRules = maxAssignments - assignedWithSoftRules;
            int assignedWithRelaxedSoftRules = 0;
            for (int i = 0; i < candidates.Count && assignedWithRelaxedSoftRules < remainingAfterSoftRules; i++)
            {
                if (!TryAssignRoleAt(layout, candidates[i], role, requireSoftCrossTypeSpacing: false))
                    continue;

                assignedWithRelaxedSoftRules++;
            }

            int totalAssigned = assignedWithSoftRules + assignedWithRelaxedSoftRules;
            if (stageDebug != null)
            {
                stageDebug.Add(
                    $"{stageLabel}: {totalAssigned}/{maxAssignments} " +
                    $"(soft {assignedWithSoftRules}, relaxed {assignedWithRelaxedSoftRules})");
            }

            return totalAssigned;
        }

        bool TryAssignRoleAt(MazeLayout layout, Vector2Int position, MazeCellRole role, bool requireSoftCrossTypeSpacing)
        {
            MazeCellData cell = layout.GetCell(position);
            if (cell.Role != MazeCellRole.Neutral)
                return false;

            if (!SatisfiesHardPlacementRules(layout, cell, role))
                return false;

            if (!HasSameTypeSpacing(layout, position, role))
                return false;

            if (requireSoftCrossTypeSpacing && !HasCrossTypeSpacing(layout, position, role))
                return false;

            cell.Role = role;
            return true;
        }

        bool SatisfiesHardPlacementRules(MazeLayout layout, MazeCellData cell, MazeCellRole role)
        {
            if (layout == null || cell == null)
                return false;

            return role switch
            {
                MazeCellRole.Trap => !IsWithinRoleSafetyDistance(layout, cell.GridPosition, MazeCellRole.Start, m_StartSafeDistance)
                    && !IsWithinRoleSafetyDistance(layout, cell.GridPosition, MazeCellRole.Goal, m_GoalSafeDistance),
                MazeCellRole.Reward => !IsWithinRoleSafetyDistance(layout, cell.GridPosition, MazeCellRole.Start, m_StartSafeDistance)
                    && ResolveBranchDepth(layout, cell) >= m_RewardMinBranchDepth,
                MazeCellRole.Refill => !IsWithinRoleSafetyDistance(layout, cell.GridPosition, MazeCellRole.Goal, m_GoalSafeDistance),
                MazeCellRole.HealthRefill => !IsWithinRoleSafetyDistance(layout, cell.GridPosition, MazeCellRole.Goal, m_GoalSafeDistance),
                _ => true,
            };
        }

        bool HasSameTypeSpacing(MazeLayout layout, Vector2Int position, MazeCellRole role)
        {
            if (layout == null || m_MinSameTypeCellDistance <= 0)
                return true;

            for (int i = 0; i < layout.Cells.Count; i++)
            {
                MazeCellData existing = layout.Cells[i];
                if (existing.Role != role)
                    continue;

                int taxicabDistance =
                    Mathf.Abs(existing.GridPosition.x - position.x) +
                    Mathf.Abs(existing.GridPosition.y - position.y);

                if (taxicabDistance < m_MinSameTypeCellDistance)
                    return false;
            }

            return true;
        }

        bool HasCrossTypeSpacing(MazeLayout layout, Vector2Int position, MazeCellRole role)
        {
            if (layout == null || m_MinCrossTypeCellDistance <= 0 || !IsCrossTypeSpacingRole(role))
                return true;

            for (int i = 0; i < layout.Cells.Count; i++)
            {
                MazeCellData existing = layout.Cells[i];
                if (!IsCrossTypeSpacingRole(existing.Role) || existing.Role == role)
                    continue;

                int taxicabDistance =
                    Mathf.Abs(existing.GridPosition.x - position.x) +
                    Mathf.Abs(existing.GridPosition.y - position.y);

                if (taxicabDistance < m_MinCrossTypeCellDistance)
                    return false;
            }

            return true;
        }

        bool IsWithinRoleSafetyDistance(MazeLayout layout, Vector2Int position, MazeCellRole role, int inclusiveDistance)
        {
            if (layout == null || inclusiveDistance <= 0)
                return false;

            for (int i = 0; i < layout.Cells.Count; i++)
            {
                MazeCellData existing = layout.Cells[i];
                if (existing.Role != role)
                    continue;

                int taxicabDistance =
                    Mathf.Abs(existing.GridPosition.x - position.x) +
                    Mathf.Abs(existing.GridPosition.y - position.y);

                if (taxicabDistance <= inclusiveDistance)
                    return true;
            }

            return false;
        }

        static bool IsCrossTypeSpacingRole(MazeCellRole role)
        {
            return role is MazeCellRole.Refill
                or MazeCellRole.HealthRefill
                or MazeCellRole.Trap
                or MazeCellRole.Reward;
        }

        static List<Vector2Int> BuildShuffledMainPathCandidates(
            MazeLayout layout,
            IReadOnlyList<Vector2Int> mainPath,
            int startIndex,
            int endIndex,
            System.Random rng)
        {
            var candidates = new List<Vector2Int>();
            if (layout == null || mainPath == null || mainPath.Count == 0)
                return candidates;

            int clampedStart = Mathf.Clamp(startIndex, 0, mainPath.Count - 1);
            int clampedEnd = Mathf.Clamp(endIndex, clampedStart, mainPath.Count - 1);

            for (int i = clampedStart; i <= clampedEnd; i++)
            {
                MazeCellData cell = layout.GetCell(mainPath[i]);
                if (cell.Role == MazeCellRole.Neutral)
                    candidates.Add(cell.GridPosition);
            }

            Shuffle(candidates, rng);
            return candidates;
        }

        static List<Vector2Int> BuildShuffledMainPathZoneCandidates(
            MazeLayout layout,
            System.Random rng,
            MazePathZone zone)
        {
            return BuildShuffledCandidates(
                layout,
                rng,
                cell => cell.IsMainPath && cell.PathZone == zone && cell.Role == MazeCellRole.Neutral);
        }

        List<Vector2Int> BuildShuffledBranchZoneCandidates(
            MazeLayout layout,
            System.Random rng,
            int mainPathCount,
            MazePathZone zone)
        {
            return BuildShuffledCandidates(
                layout,
                rng,
                cell => !cell.IsMainPath
                    && cell.Role == MazeCellRole.Neutral
                    && ResolveZoneForCell(cell, mainPathCount) == zone);
        }

        static List<Vector2Int> BuildShuffledCandidates(
            MazeLayout layout,
            System.Random rng,
            Func<MazeCellData, bool> predicate)
        {
            var candidates = new List<Vector2Int>();
            if (layout == null || predicate == null)
                return candidates;

            for (int i = 0; i < layout.Cells.Count; i++)
            {
                MazeCellData cell = layout.Cells[i];
                if (predicate(cell))
                    candidates.Add(cell.GridPosition);
            }

            Shuffle(candidates, rng);
            return candidates;
        }

        static int GetConnectionCount(MazeCellConnection connections)
        {
            int count = 0;
            if ((connections & MazeCellConnection.North) != 0)
                count++;
            if ((connections & MazeCellConnection.East) != 0)
                count++;
            if ((connections & MazeCellConnection.South) != 0)
                count++;
            if ((connections & MazeCellConnection.West) != 0)
                count++;
            return count;
        }

        void WarnIfPlacementShortfall(
            MazeCellRole role,
            int requestedCount,
            int remainingCount,
            int seed,
            IList<string> stageDebug,
            string priority)
        {
            if (!m_EmitDiagnostics)
                return;

            string stageSummary = stageDebug != null && stageDebug.Count > 0
                ? string.Join(", ", stageDebug)
                : "no stage data";

            int placedCount = requestedCount - remainingCount;
            if (remainingCount <= 0)
            {
                Debug.Log(
                    $"MazeGenerator placed {placedCount}/{requestedCount} {role} cells for seed {seed} [{priority}]. Stages: {stageSummary}",
                    this);
                return;
            }

            Debug.LogWarning(
                $"MazeGenerator could only place {placedCount}/{requestedCount} {role} cells for seed {seed} [{priority}]. Stages: {stageSummary}",
                this);
        }

        void WarnIfHiddenDoorPlacementShortfall(
            int requestedCount,
            int remainingCount,
            int seed,
            IList<string> stageDebug)
        {
            if (!m_EmitDiagnostics)
                return;

            string stageSummary = stageDebug != null && stageDebug.Count > 0
                ? string.Join(", ", stageDebug)
                : "no stage data";

            int placedCount = requestedCount - remainingCount;
            if (remainingCount <= 0)
            {
                Debug.Log(
                    $"MazeGenerator placed {placedCount}/{requestedCount} resonance hidden doors for seed {seed} [optional-branch terrain]. Stages: {stageSummary}",
                    this);
                return;
            }

            Debug.LogWarning(
                $"MazeGenerator could only place {placedCount}/{requestedCount} resonance hidden doors for seed {seed} [optional-branch terrain]. Stages: {stageSummary}",
                this);
        }

        int ResolveDirectionPreference(IReadOnlyList<Vector2Int> currentPath, Vector2Int direction)
        {
            if (currentPath == null || currentPath.Count < 2 || m_PreferredStraightStartCells <= 0)
                return 0;

            int currentIndex = currentPath.Count - 1;
            if (currentIndex > m_PreferredStraightStartCells)
                return 0;

            Vector2Int previousDirection = currentPath[currentPath.Count - 1] - currentPath[currentPath.Count - 2];
            return direction == previousDirection ? 1 : 0;
        }

        static int ResolveDistanceFromStartPreference(Vector2Int start, Vector2Int candidate)
        {
            return Mathf.Abs(candidate.x - start.x) + Mathf.Abs(candidate.y - start.y);
        }

        static MazeResolvedMainPathMode ResolveResolvedMainPathMode(FallbackMainPathTemplate template)
        {
            return template switch
            {
                FallbackMainPathTemplate.Spiral => MazeResolvedMainPathMode.DeterministicFallbackSpiral,
                FallbackMainPathTemplate.UTurn => MazeResolvedMainPathMode.DeterministicFallbackUTurn,
                FallbackMainPathTemplate.Snake => MazeResolvedMainPathMode.SnakeFallback,
                _ => MazeResolvedMainPathMode.None,
            };
        }

        static int DeriveFallbackSeed(int seed, int targetLength, RectInt bounds)
        {
            unchecked
            {
                int hash = seed;
                hash = (hash * 397) ^ targetLength;
                hash = (hash * 397) ^ bounds.xMin;
                hash = (hash * 397) ^ bounds.yMin;
                hash = (hash * 397) ^ bounds.width;
                hash = (hash * 397) ^ bounds.height;
                hash ^= 0x5F3759DF;
                return hash;
            }
        }

        static int DeriveSnakeFallbackSeed(int seed, int targetLength, RectInt bounds)
        {
            unchecked
            {
                int hash = DeriveFallbackSeed(seed, targetLength, bounds);
                hash ^= unchecked((int)0x9E3779B9);
                return hash;
            }
        }

        static Vector2Int RotateCardinalDirection(Vector2Int direction, int turnStep)
        {
            int currentIndex = Array.IndexOf(CardinalDirections, direction);
            if (currentIndex < 0)
                return direction;

            int nextIndex = (currentIndex + turnStep) % CardinalDirections.Length;
            if (nextIndex < 0)
                nextIndex += CardinalDirections.Length;

            return CardinalDirections[nextIndex];
        }

        MazePathZone ResolveZoneForCell(MazeCellData cell, int mainPathCount)
        {
            if (cell == null)
                return MazePathZone.None;

            if (cell.IsMainPath)
                return cell.PathZone;

            int interiorCount = Mathf.Max(0, mainPathCount - 2);
            int startInteriorCount = Mathf.Clamp(Mathf.FloorToInt(interiorCount * m_StartZoneRatio), 0, interiorCount);
            int remainingAfterStart = Mathf.Max(0, interiorCount - startInteriorCount);
            int midInteriorCount = Mathf.Clamp(Mathf.FloorToInt(interiorCount * m_MidZoneRatio), 0, remainingAfterStart);
            return ResolvePathZoneForIndex(cell.DistanceFromStart, mainPathCount, startInteriorCount, midInteriorCount);
        }

        int ResolveBranchDepth(MazeLayout layout, MazeCellData cell)
        {
            if (layout == null || cell == null || cell.IsMainPath)
                return 0;

            int branchDepth = 0;
            MazeCellData current = cell;
            var visited = new HashSet<Vector2Int>();

            while (current != null && !current.IsMainPath && visited.Add(current.GridPosition))
            {
                MazeCellData previous = FindPathPredecessor(layout, current);
                if (previous == null)
                    break;

                branchDepth++;
                current = previous;
            }

            return branchDepth;
        }

        MazeCellData FindPathPredecessor(MazeLayout layout, MazeCellData cell)
        {
            if (layout == null || cell == null)
                return null;

            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector2Int neighborPosition = cell.GridPosition + CardinalDirections[i];
                if (!layout.TryGetCell(neighborPosition, out MazeCellData neighbor))
                    continue;

                if (!AreCellsConnected(cell, neighbor))
                    continue;

                if (neighbor.DistanceFromStart == cell.DistanceFromStart - 1)
                    return neighbor;
            }

            return null;
        }

        static bool AreCellsConnected(MazeCellData source, MazeCellData target)
        {
            if (source == null || target == null)
                return false;

            Vector2Int delta = target.GridPosition - source.GridPosition;
            return delta switch
            {
                var d when d == Vector2Int.up => source.Connections.HasFlag(MazeCellConnection.North),
                var d when d == Vector2Int.right => source.Connections.HasFlag(MazeCellConnection.East),
                var d when d == Vector2Int.down => source.Connections.HasFlag(MazeCellConnection.South),
                var d when d == Vector2Int.left => source.Connections.HasFlag(MazeCellConnection.West),
                _ => false,
            };
        }

        bool CanOccupy(RectInt bounds, HashSet<Vector2Int> occupied, Vector2Int position)
        {
            return bounds.Contains(position) && !occupied.Contains(position);
        }

        int CountOpenNeighbors(RectInt bounds, HashSet<Vector2Int> occupied, Vector2Int position)
        {
            if (!bounds.Contains(position) || occupied.Contains(position))
                return 0;

            int count = 0;
            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector2Int neighbor = position + CardinalDirections[i];
                if (CanOccupy(bounds, occupied, neighbor))
                    count++;
            }

            return count;
        }

        List<Vector2Int> BuildShuffledDirections(System.Random rng)
        {
            var directions = new List<Vector2Int>(CardinalDirections);
            Shuffle(directions, rng);
            return directions;
        }

        readonly struct SpineDirectionCandidate
        {
            public SpineDirectionCandidate(
                Vector2Int direction,
                int straightPreference,
                int openNeighborCount,
                int distanceFromStart,
                int tieBreaker)
            {
                Direction = direction;
                StraightPreference = straightPreference;
                OpenNeighborCount = openNeighborCount;
                DistanceFromStart = distanceFromStart;
                TieBreaker = tieBreaker;
            }

            public Vector2Int Direction { get; }
            public int StraightPreference { get; }
            public int OpenNeighborCount { get; }
            public int DistanceFromStart { get; }
            public int TieBreaker { get; }
        }

        readonly struct FallbackTemplateVariant
        {
            public FallbackTemplateVariant(FallbackMainPathTemplate template, Vector2Int initialDirection, int turnStep)
            {
                Template = template;
                InitialDirection = initialDirection;
                TurnStep = turnStep;
            }

            public FallbackMainPathTemplate Template { get; }
            public Vector2Int InitialDirection { get; }
            public int TurnStep { get; }
        }

        static void Shuffle<T>(IList<T> items, System.Random rng)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int swapIndex = rng.Next(i + 1);
                (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
            }
        }
    }

    [Flags]
    public enum MazeCellConnection
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3,
    }

    public enum MazeCellRole
    {
        Neutral = 0,
        Start = 1,
        Goal = 2,
        Reward = 3,
        Refill = 4,
        HealthRefill = 5,
        Trap = 6,
        Safe = 7,
    }

    public enum MazePathZone
    {
        None = 0,
        Start = 1,
        Mid = 2,
        Late = 3,
    }

    enum FallbackMainPathTemplate
    {
        None = 0,
        Spiral = 1,
        UTurn = 2,
        Snake = 3,
    }

    [Serializable]
    public sealed class MazeCellData
    {
        public MazeCellData(Vector2Int gridPosition)
        {
            GridPosition = gridPosition;
            Role = MazeCellRole.Neutral;
            PathZone = MazePathZone.None;
            Connections = MazeCellConnection.None;
        }

        public Vector2Int GridPosition { get; }
        public bool IsMainPath { get; set; }
        public int DistanceFromStart { get; set; }
        public MazeCellRole Role { get; set; }
        public MazePathZone PathZone { get; set; }
        public MazeCellConnection Connections { get; set; }
        public MazeCellConnection HiddenDoorConnections { get; set; }
    }

    public sealed class MazeLayout
    {
        readonly Dictionary<Vector2Int, MazeCellData> m_CellsByPosition = new();
        readonly List<MazeCellData> m_Cells = new();
        readonly HashSet<Vector2Int> m_OccupiedPositions = new();
        readonly List<Vector2Int> m_MainPath = new();

        public MazeLayout(int seed)
        {
            Seed = seed;
        }

        public int Seed { get; }
        public int BranchCount { get; set; }
        public int TargetMainPathLength { get; set; }
        public MazeResolvedMainPathMode ResolvedMainPathMode { get; set; }
        public int MainPathLength => m_MainPath.Count;
        public int TotalCellCount => m_Cells.Count;
        public bool UsedDegradedMainPathLength => TargetMainPathLength > 0 && MainPathLength < TargetMainPathLength;
        public IReadOnlyList<MazeCellData> Cells => m_Cells;
        public IReadOnlyList<Vector2Int> MainPath => m_MainPath;
        public HashSet<Vector2Int> OccupiedPositions => m_OccupiedPositions;

        public MazeCellData GetCell(Vector2Int position)
        {
            return m_CellsByPosition[position];
        }

        public bool TryGetCell(Vector2Int position, out MazeCellData cell)
        {
            return m_CellsByPosition.TryGetValue(position, out cell);
        }

        public MazeCellData GetOrCreateCell(Vector2Int position)
        {
            if (m_CellsByPosition.TryGetValue(position, out MazeCellData existing))
                return existing;

            var created = new MazeCellData(position);
            m_CellsByPosition.Add(position, created);
            m_Cells.Add(created);
            m_OccupiedPositions.Add(position);
            return created;
        }

        public void SetMainPath(IReadOnlyList<Vector2Int> mainPath)
        {
            m_MainPath.Clear();
            for (int i = 0; i < mainPath.Count; i++)
                m_MainPath.Add(mainPath[i]);
        }

        public void Connect(Vector2Int from, Vector2Int to)
        {
            MazeCellData source = GetOrCreateCell(from);
            MazeCellData target = GetOrCreateCell(to);

            MazeCellConnection sourceFlag = ResolveConnectionFlag(from, to);
            MazeCellConnection targetFlag = ResolveConnectionFlag(to, from);

            source.Connections |= sourceFlag;
            target.Connections |= targetFlag;
        }

        public static MazeCellConnection ResolveConnectionFlag(Vector2Int from, Vector2Int to)
        {
            Vector2Int delta = to - from;
            if (delta == Vector2Int.up)
                return MazeCellConnection.North;
            if (delta == Vector2Int.right)
                return MazeCellConnection.East;
            if (delta == Vector2Int.down)
                return MazeCellConnection.South;
            if (delta == Vector2Int.left)
                return MazeCellConnection.West;

            throw new ArgumentException($"Cells {from} and {to} are not cardinal neighbors.");
        }
    }

    public readonly struct MazeGeneratorSizeProfile
    {
        public MazeGeneratorSizeProfile(
            int gridWidth,
            int gridHeight,
            int minMainPathLength,
            int maxMainPathLength,
            int maxMainPathAttempts,
            int minBranchCount,
            int maxBranchCount,
            int minBranchLength,
            int maxBranchLength)
        {
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            MinMainPathLength = minMainPathLength;
            MaxMainPathLength = maxMainPathLength;
            MaxMainPathAttempts = maxMainPathAttempts;
            MinBranchCount = minBranchCount;
            MaxBranchCount = maxBranchCount;
            MinBranchLength = minBranchLength;
            MaxBranchLength = maxBranchLength;
        }

        public int GridWidth { get; }
        public int GridHeight { get; }
        public int MinMainPathLength { get; }
        public int MaxMainPathLength { get; }
        public int MaxMainPathAttempts { get; }
        public int MinBranchCount { get; }
        public int MaxBranchCount { get; }
        public int MinBranchLength { get; }
        public int MaxBranchLength { get; }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(MazeGenerator))]
    sealed class MazeGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            MazeGenerator generator = (MazeGenerator)target;
            MazeRunBootstrap bootstrap = generator.GetComponent<MazeRunBootstrap>();

            EditorGUILayout.HelpBox(
                "Random maze tuning is centralized on MazeRunBootstrap under GeneratedMazeRoot. MazeGenerator now acts as the internal layout worker.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(bootstrap == null))
            {
                if (GUILayout.Button("Select MazeRunBootstrap") && bootstrap != null)
                    Selection.activeObject = bootstrap;
            }
        }
    }
#endif
}
