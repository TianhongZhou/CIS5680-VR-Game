using System.Collections.Generic;
using UnityEngine;
using CIS5680VRGame.Balls;
using CIS5680VRGame.Gameplay;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CIS5680VRGame.Generation
{
    [DisallowMultipleComponent]
    public class MazeModulePlacer : MonoBehaviour
    {
        static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
        static readonly Vector2Int[] HiddenDoorSecurityDirections =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left,
        };
        const string GrayboxRootName = "GrayboxMaze";
        const string FloorsRootName = "Floors";
        const string WallsRootName = "Walls";
        const string HiddenDoorsRootName = "HiddenDoors";
        const string TerrainDetailsRootName = "TerrainDetails";
        const string MarkersRootName = "Markers";
        const string StartRootName = "Start";
        const string SupportRootName = "Support";
        const string DefaultFloorMaterialPath = "Assets/Materials/Environment/M_Maze1PulseFloorBlack.mat";
        const string DefaultWallMaterialPath = "Assets/Materials/Environment/M_Maze1PulseWallBlack.mat";
        const string DefaultEditorPreviewFloorMaterialPath = "Assets/Materials/Environment/M_SampleScenePulseFloorBlue.mat";
        const string DefaultEditorPreviewWallMaterialPath = "Assets/Materials/Environment/M_SampleScenePulseWall.mat";
        const string DefaultTrapPrefabPath = "Assets/Prefabs/Gameplay/DamageTrap.prefab";
        const string DefaultGoalPrefabPath = "Assets/Prefabs/Gameplay/MazeGoalBeacon.prefab";
        const string DefaultStartPrefabPath = "Assets/Prefabs/Gameplay/MazeStartPad.prefab";
        const string DefaultHealthRefillPrefabPath = "Assets/Prefabs/Gameplay/HealthRefillStation.prefab";
        const string DefaultRobotScoutEnemyPrefabPath = "Assets/Prefabs/Enemies/RobotScoutEnemy.prefab";
        const string DefaultRenderSkinMeshAssetPath = "Assets/Generated/Maze/RandomMazeRenderSkin.asset";
        const string DefaultRenderSkinMaterialAssetPath = "Assets/Generated/Maze/M_RandomMazeRenderSkinPulse.mat";
        const string HiddenDoorPulseHitClipResourcePath = "Audio/Gameplay/HiddenDoor/pulse_hit_hidden_wall";
        const string HiddenDoorTriggeredClipResourcePath = "Audio/Gameplay/HiddenDoor/triggered";
        const string HiddenDoorOpenedClipResourcePath = "Audio/Gameplay/HiddenDoor/door_open";
        const string HiddenDoorProgressResetClipResourcePath = "Audio/Gameplay/HiddenDoor/reset_progress";
        const string GroundLayerName = "Ground";
        const float GuidePipelineClampMinSegmentSpacingRatio = 0.85f;

        static AudioClip s_DefaultHiddenDoorPulseHitClip;
        static AudioClip s_DefaultHiddenDoorTriggeredClip;
        static AudioClip s_DefaultHiddenDoorOpenedClip;
        static AudioClip s_DefaultHiddenDoorProgressResetClip;

        [Header("Placement")]
        [SerializeField] bool m_AlignStartToAnchor = true;
        [SerializeField] Transform m_StartAnchor;
        [SerializeField] Vector3 m_ManualOrigin = Vector3.zero;

        [Header("Templates")]
        [SerializeField] GameObject m_StartPrefab;
        [SerializeField] GameObject m_GoalPrefab;
        [SerializeField] BallRefillStation m_RefillTemplate;
        [SerializeField] HealthRefillStation m_HealthRefillTemplate;
        [SerializeField] GameObject m_TrapPrefab;
        [SerializeField] Material m_FloorRevealMaterial;
        [SerializeField] Material m_WallRevealMaterial;
        [SerializeField] Material m_EditorPreviewFloorMaterial;
        [SerializeField] Material m_EditorPreviewWallMaterial;
        [SerializeField] MazeModuleNavigationAuthoring m_DefaultNavigationAuthoring;
        [SerializeField] List<MazeRoleNavigationAuthoringOverride> m_RoleNavigationAuthoringOverrides = new();

        [Header("Graybox Dimensions")]
        [SerializeField, Min(1f)] float m_CellSize = 4f;
        [SerializeField, Min(0.05f)] float m_FloorThickness = 0.2f;
        [SerializeField, Min(1f)] float m_WallHeight = 2.75f;
        [SerializeField, Min(1f)] float m_InternalWallHeightMultiplier = 1.5f;
        [SerializeField, Min(1f)] float m_BoundaryWallHeightMultiplier = 3f;
        [SerializeField, Min(0.05f)] float m_WallThickness = 0.2f;
        [SerializeField, Min(0f)] float m_SupportPadding = 1.5f;
        [SerializeField, Min(0.05f)] float m_SupportThickness = 0.6f;

        [Header("Terrain Variants")]
        [SerializeField, Min(0.1f)] float m_BeveledCornerRun = 1.1f;
        [SerializeField, Min(0.5f)] float m_BeveledCornerThicknessMultiplier = 1.15f;
        [SerializeField, Min(0.01f)] float m_FloorRidgeHeight = 0.055f;
        [SerializeField, Min(0.02f)] float m_FloorRidgeWidth = 0.1f;
        [SerializeField, Min(0.2f)] float m_FloorRidgeLength = 2.45f;
        [SerializeField, Min(0.05f)] float m_FloorRidgeSpacing = 0.46f;
        [SerializeField, Min(0.05f)] float m_GuidePipelineEdgeInset = 0.5f;

        [Header("Special Placement")]
        [SerializeField] Vector3 m_StartLocalOffset = Vector3.zero;
        [SerializeField] Vector3 m_GoalLocalOffset = Vector3.zero;
        [SerializeField] Vector3 m_RefillLocalOffset = new(0f, 0.03f, 0f);
        [SerializeField] Vector3 m_HealthRefillLocalOffset = new(0f, 0.03f, 0f);
        [SerializeField] Vector3 m_TrapLocalOffset = new(0f, 0.02f, 0f);
        [SerializeField] Vector3 m_RewardLocalOffset = new(0f, 0.9f, 0f);
        [SerializeField] Vector3 m_RewardPlaceholderScale = new(0.42f, 0.42f, 0.42f);
        [SerializeField, Min(1)] int m_DefaultRewardAmount = 1;
        [SerializeField, Min(0.05f)] float m_RewardTriggerRadius = 0.7f;

        [Header("Resonance Hidden Doors")]
        [SerializeField, Min(1)] int m_HiddenDoorRequiredPulseHits = 3;
        [SerializeField, Min(0f)] float m_HiddenDoorProgressResetDelay = 12f;
        [SerializeField, Min(0.05f)] float m_HiddenDoorInteractionPointDiameter = 0.34f;
        [SerializeField, Min(0f)] float m_HiddenDoorSideInset = 0.08f;
        [SerializeField, Min(0f)] float m_HiddenDoorDepthBias = 0.035f;
        [SerializeField] Color m_HiddenDoorInteractionPointColor = new(1f, 0.78f, 0.18f, 1f);
        [SerializeField] Color m_HiddenDoorInteractionPlateColor = new(0.01f, 0.018f, 0.02f, 1f);
        [SerializeField] Color m_HiddenDoorInteractionAccentColor = new(0.08f, 0.8f, 1f, 1f);
        [SerializeField, Min(0.15f)] float m_HiddenDoorInteractionPlateWidth = 0.56f;
        [SerializeField, Min(0.25f)] float m_HiddenDoorInteractionPlateHeight = 1.05f;
        [SerializeField, Min(0.02f)] float m_HiddenDoorInteractionDepth = 0.12f;
        [SerializeField] Material m_HiddenDoorInteractionPointMaterial;
        [SerializeField] AudioClip m_HiddenDoorPulseHitClip;
        [SerializeField] AudioClip m_HiddenDoorTriggeredClip;
        [SerializeField] AudioClip m_HiddenDoorOpenedClip;
        [SerializeField] AudioClip m_HiddenDoorProgressResetClip;

        [Header("Enemy Prototype")]
        [SerializeField] GameObject m_RobotScoutEnemyPrefab;
        [SerializeField] Vector3 m_EnemyLocalOffset = Vector3.zero;
        [SerializeField, Min(0.5f)] float m_EnemyPatrolRadius = 10f;

        [Header("Markers")]
        [SerializeField] bool m_BuildRoleMarkers = true;
        [SerializeField, Min(0.1f)] float m_RoleMarkerHeight = 1.1f;
        [SerializeField, Min(0.1f)] float m_RoleMarkerDiameter = 0.5f;

        [SerializeField, HideInInspector] Vector3 m_LastResolvedOrigin;
        [SerializeField, HideInInspector] bool m_HasResolvedOrigin;
        Material m_RuntimeHiddenDoorInteractionPointMaterial;
        Material m_RuntimeGuidePipelineBaseMaterial;
        Material m_RuntimeGuidePipelineGlowMaterial;
        Material m_RuntimeGuidePipelineHaloMaterial;
        readonly Dictionary<Vector2Int, MazePlacedModuleNavigationData> m_PlacedNavigationModules = new();

        public float CellSize => m_CellSize;
        public bool HasResolvedPlacementOrigin => m_HasResolvedOrigin;
        public Vector3 ResolvedPlacementOrigin => m_HasResolvedOrigin ? m_LastResolvedOrigin : ResolvePlacementOrigin();
        public bool TryGetPlacedModuleNavigationData(Vector2Int gridPosition, out MazePlacedModuleNavigationData data)
        {
            return m_PlacedNavigationModules.TryGetValue(gridPosition, out data);
        }

        public void EnsurePlacementOriginCached()
        {
            if (!m_HasResolvedOrigin)
                CachePlacementOrigin();
        }

        public void BuildModules(MazeLayout layout, MazeRunBootstrap bootstrap)
        {
            if (layout == null || bootstrap == null || bootstrap.ModulesRoot == null)
                return;

            AutoAssignDefaultAssets();
            ResolveTemplates();
            CachePlacementOrigin();
            m_PlacedNavigationModules.Clear();

            Transform grayboxRoot = EnsureChild(bootstrap.ModulesRoot, GrayboxRootName);
            RemoveLegacyChild(grayboxRoot, MarkersRootName);
            RemoveLegacyChild(grayboxRoot, StartRootName);

            Transform floorsRoot = EnsureChild(grayboxRoot, FloorsRootName);
            Transform wallsRoot = EnsureChild(grayboxRoot, WallsRootName);
            Transform supportRoot = EnsureChild(grayboxRoot, SupportRootName);
            Transform terrainDetailsRoot = EnsureChild(grayboxRoot, TerrainDetailsRootName);
            Transform hiddenDoorsRoot = EnsureChild(bootstrap.ModulesRoot, HiddenDoorsRootName);
            Transform markersRoot = EnsureChild(bootstrap.ModulesRoot, MarkersRootName);
            Transform startRoot = EnsureChild(bootstrap.ModulesRoot, StartRootName);

            ClearChildren(floorsRoot);
            ClearChildren(wallsRoot);
            ClearChildren(supportRoot);
            ClearChildren(terrainDetailsRoot);
            ClearChildren(hiddenDoorsRoot);
            ClearChildren(markersRoot);
            ClearChildren(startRoot);
            ClearChildren(bootstrap.HazardsRoot);
            ClearChildren(bootstrap.RefillsRoot);
            ClearChildren(bootstrap.RewardsRoot);
            ClearChildren(bootstrap.EnemiesRoot);
            ClearChildren(bootstrap.GoalRoot);

            EnsureGeneratedPreviewTools(grayboxRoot, floorsRoot);

            for (int i = 0; i < layout.Cells.Count; i++)
            {
                MazeCellData cell = layout.Cells[i];
                CreateFloor(floorsRoot, cell);
                BuildCellTerrainDetails(terrainDetailsRoot, cell);
                BuildCellWalls(wallsRoot, layout, cell);
                BuildCellHiddenDoors(hiddenDoorsRoot, layout, bootstrap, cell);
                GameObject placedSpecialObject = PlaceSpecialObject(bootstrap, startRoot, cell);
                RegisterPlacedModuleNavigation(cell, placedSpecialObject);

                if (m_BuildRoleMarkers && ShouldCreateRoleMarker(cell))
                    CreateRoleMarker(markersRoot, cell);
            }

            CreateSupportSlab(supportRoot, layout);
            RebuildRenderSkin(grayboxRoot, floorsRoot);
            ConfigureGuidePipelinePulseResponder(terrainDetailsRoot, layout);
            PlacePrototypeEnemies(layout, bootstrap);
        }

        void Reset()
        {
            AutoAssignDefaultAssets();
            ResolveTemplates();

            if (m_StartAnchor == null)
            {
                GameObject xrRig = GameObject.Find("XR Origin (XR Rig)");
                if (xrRig != null)
                    m_StartAnchor = xrRig.transform;
            }
        }

        void OnValidate()
        {
            AutoAssignDefaultAssets();
        }

        void ResolveTemplates()
        {
            if (m_StartPrefab == null)
            {
                Transform startTemplate = FindTemplate<Transform>("MazeStartPad");
                if (startTemplate != null)
                    m_StartPrefab = startTemplate.gameObject;
            }

            if (m_GoalPrefab == null)
            {
                Transform goalTemplate = FindTemplate<Transform>("MazeGoalBeacon");
                if (goalTemplate != null)
                    m_GoalPrefab = goalTemplate.gameObject;
            }

            if (m_RefillTemplate == null)
                m_RefillTemplate = FindTemplate<BallRefillStation>("SonarRefillPad");

            if (m_HealthRefillTemplate == null)
                m_HealthRefillTemplate = FindTemplate<HealthRefillStation>("HealthRefillStation");
        }

        void CreateFloor(Transform parent, MazeCellData cell)
        {
            GameObject floorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorObject.name = $"Floor_{cell.GridPosition.x}_{cell.GridPosition.y}";
            floorObject.transform.SetParent(parent, false);
            floorObject.transform.position = ResolveCellWorldPosition(cell.GridPosition) + Vector3.down * (m_FloorThickness * 0.5f);
            floorObject.transform.localScale = new Vector3(m_CellSize, m_FloorThickness, m_CellSize);
            floorObject.layer = ResolveGroundLayer();

            Renderer floorRenderer = floorObject.GetComponent<Renderer>();
            if (floorRenderer != null)
                floorRenderer.sharedMaterial = ResolveFloorMaterial();
        }

        void BuildCellTerrainDetails(Transform parent, MazeCellData cell)
        {
            if (parent == null || cell == null)
                return;

            if (cell.GuidePipelineConnections != MazeCellConnection.None)
                CreateGuidePipeline(parent, cell);
        }

        void CreateGuidePipeline(Transform parent, MazeCellData cell)
        {
            if (parent == null || cell == null)
                return;

            GameObject pipelineRoot = new($"GuidePipeline_{cell.GridPosition.x}_{cell.GridPosition.y}");
            pipelineRoot.transform.SetParent(parent, false);
            pipelineRoot.transform.position = ResolveCellWorldPosition(cell.GridPosition);

            ResolveGuidePipelineVisualMetrics(
                out float conduitHeight,
                out float channelWidth,
                out float coreWidth,
                out float halfCellReach,
                out float laneOffset,
                out float topOffset);

            CreateGuidePipelineSegmentSet(
                pipelineRoot.transform,
                cell,
                laneOffset,
                halfCellReach,
                topOffset,
                conduitHeight,
                channelWidth,
                coreWidth);

            AttachPulseRevealVisual(
                pipelineRoot,
                new Color(0.001f, 0.004f, 0.005f, 1f),
                new Color(0.03f, 0.18f, 0.22f, 1f),
                0.35f);
        }

        void CreateGuidePipelineSegmentSet(
            Transform parent,
            MazeCellData cell,
            float laneOffset,
            float halfCellReach,
            float topOffset,
            float conduitHeight,
            float channelWidth,
            float coreWidth)
        {
            if (parent == null || cell == null)
                return;

            MazeCellConnection connections = cell.GuidePipelineConnections;
            if (connections == MazeCellConnection.None)
                return;

            float horizontalLaneZ = ResolveGuidePipelineHorizontalLaneZ(cell, laneOffset);
            float verticalLaneX = ResolveGuidePipelineVerticalLaneX(cell, laneOffset);
            bool hasHorizontal = connections.HasFlag(MazeCellConnection.East)
                || connections.HasFlag(MazeCellConnection.West);
            bool hasVertical = connections.HasFlag(MazeCellConnection.North)
                || connections.HasFlag(MazeCellConnection.South);

            if (connections.HasFlag(MazeCellConnection.East)
                && connections.HasFlag(MazeCellConnection.West)
                && !hasVertical)
            {
                CreateGuidePipelineSegment(
                    parent,
                    "GuidePipeline_EastWest",
                    new Vector3(-halfCellReach, 0f, horizontalLaneZ),
                    new Vector3(halfCellReach, 0f, horizontalLaneZ),
                    topOffset,
                    conduitHeight,
                    channelWidth,
                    coreWidth);
                return;
            }

            if (connections.HasFlag(MazeCellConnection.North)
                && connections.HasFlag(MazeCellConnection.South)
                && !hasHorizontal)
            {
                CreateGuidePipelineSegment(
                    parent,
                    "GuidePipeline_NorthSouth",
                    new Vector3(verticalLaneX, 0f, -halfCellReach),
                    new Vector3(verticalLaneX, 0f, halfCellReach),
                    topOffset,
                    conduitHeight,
                    channelWidth,
                    coreWidth);
                return;
            }

            Vector3 hub = new(verticalLaneX, 0f, horizontalLaneZ);
            TryCreateGuidePipelineArm(parent, connections, MazeCellConnection.North, hub, verticalLaneX, horizontalLaneZ, halfCellReach, topOffset, conduitHeight, channelWidth, coreWidth);
            TryCreateGuidePipelineArm(parent, connections, MazeCellConnection.East, hub, verticalLaneX, horizontalLaneZ, halfCellReach, topOffset, conduitHeight, channelWidth, coreWidth);
            TryCreateGuidePipelineArm(parent, connections, MazeCellConnection.South, hub, verticalLaneX, horizontalLaneZ, halfCellReach, topOffset, conduitHeight, channelWidth, coreWidth);
            TryCreateGuidePipelineArm(parent, connections, MazeCellConnection.West, hub, verticalLaneX, horizontalLaneZ, halfCellReach, topOffset, conduitHeight, channelWidth, coreWidth);

            Vector3 hubPosition = hub + Vector3.up * topOffset;
            CreateGuidePipelinePiece(
                parent,
                "GuidePipelineHub",
                hubPosition,
                new Vector3(channelWidth * 1.2f, conduitHeight, channelWidth * 1.2f),
                ResolveGuidePipelineBaseMaterial());
            CreateGuidePipelinePiece(
                parent,
                "GuidePipelineHubCore",
                hubPosition + Vector3.up * (conduitHeight * 0.58f),
                new Vector3(coreWidth * 1.45f, conduitHeight * 0.38f, coreWidth * 1.45f),
                ResolveGuidePipelineGlowMaterial());
        }

        void TryCreateGuidePipelineArm(
            Transform parent,
            MazeCellConnection connections,
            MazeCellConnection connection,
            Vector3 hub,
            float verticalLaneX,
            float horizontalLaneZ,
            float halfCellReach,
            float topOffset,
            float conduitHeight,
            float channelWidth,
            float coreWidth)
        {
            if (!connections.HasFlag(connection))
                return;

            Vector2Int direction = ResolveConnectionDirection(connection);
            Vector3 end = direction.x != 0
                ? new Vector3(direction.x * halfCellReach, 0f, horizontalLaneZ)
                : new Vector3(verticalLaneX, 0f, direction.y * halfCellReach);
            CreateGuidePipelineSegment(parent, $"GuidePipeline_{connection}", hub, end, topOffset, conduitHeight, channelWidth, coreWidth);
        }

        void CreateGuidePipelineSegment(
            Transform parent,
            string name,
            Vector3 localStart,
            Vector3 localEnd,
            float topOffset,
            float conduitHeight,
            float channelWidth,
            float coreWidth)
        {
            if (parent == null)
                return;

            Vector3 delta = localEnd - localStart;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.z) <= 0.01f)
                return;

            bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z);
            Vector3 center = (localStart + localEnd) * 0.5f + Vector3.up * topOffset;
            float length = Mathf.Max(0.04f, horizontal ? Mathf.Abs(delta.x) : Mathf.Abs(delta.z));

            Vector3 channelScale = horizontal
                ? new Vector3(length + channelWidth, conduitHeight, channelWidth)
                : new Vector3(channelWidth, conduitHeight, length + channelWidth);
            Vector3 haloScale = horizontal
                ? new Vector3(length + channelWidth * 1.35f, conduitHeight * 0.2f, channelWidth * 1.7f)
                : new Vector3(channelWidth * 1.7f, conduitHeight * 0.2f, length + channelWidth * 1.35f);
            Vector3 coreScale = horizontal
                ? new Vector3(length + coreWidth, conduitHeight * 0.34f, coreWidth)
                : new Vector3(coreWidth, conduitHeight * 0.34f, length + coreWidth);

            CreateGuidePipelinePiece(parent, $"{name}_Halo", center + Vector3.down * (conduitHeight * 0.42f), haloScale, ResolveGuidePipelineHaloMaterial());
            CreateGuidePipelinePiece(parent, $"{name}_Channel", center, channelScale, ResolveGuidePipelineBaseMaterial());
            CreateGuidePipelinePiece(parent, $"{name}_Core", center + Vector3.up * (conduitHeight * 0.58f), coreScale, ResolveGuidePipelineGlowMaterial());
            CreateGuidePipelineSegmentClamps(parent, name, localStart, localEnd, topOffset, conduitHeight, channelWidth, coreWidth);
        }

        void CreateGuidePipelineSegmentClamps(
            Transform parent,
            string name,
            Vector3 localStart,
            Vector3 localEnd,
            float topOffset,
            float conduitHeight,
            float channelWidth,
            float coreWidth)
        {
            Vector3 delta = localEnd - localStart;
            bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.z);
            float length = Mathf.Max(0.04f, horizontal ? Mathf.Abs(delta.x) : Mathf.Abs(delta.z));
            int clampCount = ResolveGuidePipelineClampCount(length, ResolveGuidePipelineClampSpacing());
            if (clampCount <= 0)
                return;

            Material clampMaterial = ResolveGuidePipelineBaseMaterial();

            for (int i = 1; i <= clampCount; i++)
            {
                float t = i / (clampCount + 1f);
                Vector3 position = Vector3.Lerp(localStart, localEnd, t) + Vector3.up * (topOffset + conduitHeight * 0.62f);
                Vector3 scale = horizontal
                    ? new Vector3(coreWidth * 1.2f, conduitHeight * 0.36f, channelWidth * 1.32f)
                    : new Vector3(channelWidth * 1.32f, conduitHeight * 0.36f, coreWidth * 1.2f);
                CreateGuidePipelinePiece(parent, $"{name}_Clamp_{i}", position, scale, clampMaterial);
            }
        }

        void CreateGuidePipelinePiece(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = localScale;
            piece.layer = ResolveGroundLayer();

            Collider pieceCollider = piece.GetComponent<Collider>();
            if (pieceCollider != null)
                DestroyImmediateOrRuntime(pieceCollider);

            Renderer pieceRenderer = piece.GetComponent<Renderer>();
            if (pieceRenderer != null)
                pieceRenderer.sharedMaterial = material != null ? material : ResolveFloorMaterial();
        }

        void ConfigureGuidePipelinePulseResponder(Transform terrainDetailsRoot, MazeLayout layout)
        {
            if (terrainDetailsRoot == null)
                return;

            GuidePipelineFlowBuildResult flowData = BuildGuidePipelineFlowPath(layout);
            GuidePipelinePulseResponder responder = terrainDetailsRoot.GetComponent<GuidePipelinePulseResponder>();
            if (flowData.Points.Count < 2)
            {
                if (responder != null)
                    responder.Configure(null, m_FloorRidgeWidth, 0f);
                return;
            }

            if (responder == null)
                responder = terrainDetailsRoot.gameObject.AddComponent<GuidePipelinePulseResponder>();

            ResolveGuidePipelineVisualMetrics(
                out float conduitHeight,
                out float channelWidth,
                out float coreWidth,
                out _,
                out _,
                out _);
            float flowWidth = Mathf.Clamp(coreWidth * 0.82f, 0.04f, channelWidth * 0.38f);
            responder.Configure(flowData.Points, flowData.OccludedIntervals, flowWidth, Mathf.Min(0.0015f, conduitHeight * 0.04f));
        }

        GuidePipelineFlowBuildResult BuildGuidePipelineFlowPath(MazeLayout layout)
        {
            var flowData = new GuidePipelineFlowBuildResult();
            if (layout == null || layout.Cells == null)
                return flowData;

            Dictionary<Vector2Int, MazeCellData> guideCells = BuildGuidePipelineCellLookup(layout);
            if (guideCells.Count < 2)
                return flowData;

            Dictionary<Vector2Int, List<Vector2Int>> adjacency = BuildGuidePipelineAdjacency(guideCells);
            if (adjacency.Count < 2)
                return flowData;

            List<Vector2Int> endpoints = BuildGuidePipelineEndpoints(adjacency);
            IReadOnlyList<Vector2Int> endpointCandidates = endpoints.Count > 0
                ? endpoints
                : new List<Vector2Int>(guideCells.Keys);
            Vector2Int goalPosition = ResolveGuidePipelineGoalPosition(layout);
            Vector2Int goalEndpoint = SelectGuidePipelineEndpoint(endpointCandidates, goalPosition, preferClosest: true);
            Vector2Int startEndpoint = SelectGuidePipelineEndpoint(endpointCandidates, goalPosition, preferClosest: false);

            if (startEndpoint == goalEndpoint && endpointCandidates.Count > 1)
                startEndpoint = SelectGuidePipelineEndpoint(endpointCandidates, goalEndpoint, preferClosest: false);

            List<Vector2Int> orderedCells = FindGuidePipelineCellPath(adjacency, startEndpoint, goalEndpoint);
            if (orderedCells.Count < 2)
                return flowData;

            ResolveGuidePipelineVisualMetrics(
                out float conduitHeight,
                out float channelWidth,
                out float coreWidth,
                out float halfCellReach,
                out float laneOffset,
                out float topOffset);
            float clampHalfLength = ResolveGuidePipelineClampOcclusionHalfLength(coreWidth);
            float clampSpacingDivisor = ResolveGuidePipelineClampSpacing();

            for (int i = 0; i < orderedCells.Count; i++)
            {
                Vector2Int currentPosition = orderedCells[i];
                if (!guideCells.TryGetValue(currentPosition, out MazeCellData currentCell))
                    continue;

                if (i == 0)
                    flowData.AddPoint(ResolveGuidePipelineAnchorWorldPosition(currentCell, conduitHeight, channelWidth, laneOffset, topOffset));

                if (i > 0)
                {
                    Vector2Int previousPosition = orderedCells[i - 1];
                    Vector2Int direction = currentPosition - previousPosition;
                    MazeCellData previousCell = null;
                    bool hasPreviousCell = guideCells.TryGetValue(previousPosition, out previousCell);
                    float crossingStartDistance = flowData.CurrentDistance;
                    if (hasPreviousCell)
                    {
                        crossingStartDistance = flowData.AddPointWithStructuralClamps(
                            ResolveGuidePipelineConnectionWorldPosition(previousCell, direction, conduitHeight, channelWidth, halfCellReach, laneOffset, topOffset),
                            clampHalfLength,
                            clampSpacingDivisor);
                    }

                    float crossingEndDistance = flowData.AddPoint(
                        ResolveGuidePipelineConnectionWorldPosition(currentCell, -direction, conduitHeight, channelWidth, halfCellReach, laneOffset, topOffset));

                    if (hasPreviousCell && ShouldOccludeGuidePipelineTransition(previousCell, currentCell))
                        flowData.AddOccludedInterval(crossingStartDistance, crossingEndDistance);
                }

                flowData.AddPointWithStructuralClamps(
                    ResolveGuidePipelineAnchorWorldPosition(currentCell, conduitHeight, channelWidth, laneOffset, topOffset),
                    clampHalfLength,
                    clampSpacingDivisor);

                if (IsStraightGuidePipelineCell(currentCell))
                    flowData.AddOccludedInterval(flowData.CurrentDistance - clampHalfLength, flowData.CurrentDistance + clampHalfLength);
            }

            return flowData;
        }

        static bool IsStraightGuidePipelineCell(MazeCellData cell)
        {
            if (cell == null)
                return false;

            bool eastWest = cell.GuidePipelineConnections.HasFlag(MazeCellConnection.East)
                && cell.GuidePipelineConnections.HasFlag(MazeCellConnection.West)
                && !cell.GuidePipelineConnections.HasFlag(MazeCellConnection.North)
                && !cell.GuidePipelineConnections.HasFlag(MazeCellConnection.South);
            bool northSouth = cell.GuidePipelineConnections.HasFlag(MazeCellConnection.North)
                && cell.GuidePipelineConnections.HasFlag(MazeCellConnection.South)
                && !cell.GuidePipelineConnections.HasFlag(MazeCellConnection.East)
                && !cell.GuidePipelineConnections.HasFlag(MazeCellConnection.West);
            return eastWest || northSouth;
        }

        static bool ShouldOccludeGuidePipelineTransition(MazeCellData previousCell, MazeCellData currentCell)
        {
            if (previousCell == null || currentCell == null)
                return false;

            MazeCellConnection previousConnection = MazeLayout.ResolveConnectionFlag(previousCell.GridPosition, currentCell.GridPosition);
            MazeCellConnection currentConnection = MazeLayout.ResolveConnectionFlag(currentCell.GridPosition, previousCell.GridPosition);
            if (previousConnection == MazeCellConnection.None || currentConnection == MazeCellConnection.None)
                return false;

            return !previousCell.Connections.HasFlag(previousConnection)
                || !currentCell.Connections.HasFlag(currentConnection)
                || previousCell.HiddenDoorConnections.HasFlag(previousConnection)
                || currentCell.HiddenDoorConnections.HasFlag(currentConnection);
        }

        Dictionary<Vector2Int, MazeCellData> BuildGuidePipelineCellLookup(MazeLayout layout)
        {
            var guideCells = new Dictionary<Vector2Int, MazeCellData>();
            if (layout == null)
                return guideCells;

            for (int i = 0; i < layout.Cells.Count; i++)
            {
                MazeCellData cell = layout.Cells[i];
                if (cell != null && cell.GuidePipelineConnections != MazeCellConnection.None)
                    guideCells[cell.GridPosition] = cell;
            }

            return guideCells;
        }

        static Dictionary<Vector2Int, List<Vector2Int>> BuildGuidePipelineAdjacency(IReadOnlyDictionary<Vector2Int, MazeCellData> guideCells)
        {
            var adjacency = new Dictionary<Vector2Int, List<Vector2Int>>();
            if (guideCells == null)
                return adjacency;

            foreach (KeyValuePair<Vector2Int, MazeCellData> entry in guideCells)
            {
                Vector2Int position = entry.Key;
                MazeCellData cell = entry.Value;
                var neighbors = new List<Vector2Int>(4);
                TryAddGuidePipelineNeighbor(guideCells, cell, MazeCellConnection.North, neighbors);
                TryAddGuidePipelineNeighbor(guideCells, cell, MazeCellConnection.East, neighbors);
                TryAddGuidePipelineNeighbor(guideCells, cell, MazeCellConnection.South, neighbors);
                TryAddGuidePipelineNeighbor(guideCells, cell, MazeCellConnection.West, neighbors);
                adjacency[position] = neighbors;
            }

            return adjacency;
        }

        static void TryAddGuidePipelineNeighbor(
            IReadOnlyDictionary<Vector2Int, MazeCellData> guideCells,
            MazeCellData cell,
            MazeCellConnection connection,
            ICollection<Vector2Int> neighbors)
        {
            if (guideCells == null || cell == null || neighbors == null || !cell.GuidePipelineConnections.HasFlag(connection))
                return;

            Vector2Int neighborPosition = cell.GridPosition + ResolveConnectionDirection(connection);
            if (guideCells.ContainsKey(neighborPosition))
                neighbors.Add(neighborPosition);
        }

        static List<Vector2Int> BuildGuidePipelineEndpoints(Dictionary<Vector2Int, List<Vector2Int>> adjacency)
        {
            var endpoints = new List<Vector2Int>();
            if (adjacency == null)
                return endpoints;

            foreach (KeyValuePair<Vector2Int, List<Vector2Int>> entry in adjacency)
            {
                if (entry.Value == null || entry.Value.Count <= 1)
                    endpoints.Add(entry.Key);
            }

            return endpoints;
        }

        static Vector2Int ResolveGuidePipelineGoalPosition(MazeLayout layout)
        {
            if (layout != null)
            {
                for (int i = 0; i < layout.Cells.Count; i++)
                {
                    MazeCellData cell = layout.Cells[i];
                    if (cell != null && cell.Role == MazeCellRole.Goal)
                        return cell.GridPosition;
                }

                if (layout.MainPath != null && layout.MainPath.Count > 0)
                    return layout.MainPath[layout.MainPath.Count - 1];
            }

            return Vector2Int.zero;
        }

        static Vector2Int SelectGuidePipelineEndpoint(IReadOnlyList<Vector2Int> candidates, Vector2Int goalPosition, bool preferClosest)
        {
            if (candidates == null || candidates.Count == 0)
                return Vector2Int.zero;

            Vector2Int selected = candidates[0];
            int selectedDistance = Mathf.Abs(selected.x - goalPosition.x) + Mathf.Abs(selected.y - goalPosition.y);
            for (int i = 1; i < candidates.Count; i++)
            {
                Vector2Int candidate = candidates[i];
                int distance = Mathf.Abs(candidate.x - goalPosition.x) + Mathf.Abs(candidate.y - goalPosition.y);
                bool shouldSelect = preferClosest
                    ? distance < selectedDistance
                    : distance > selectedDistance;
                if (!shouldSelect)
                    continue;

                selected = candidate;
                selectedDistance = distance;
            }

            return selected;
        }

        static List<Vector2Int> FindGuidePipelineCellPath(
            Dictionary<Vector2Int, List<Vector2Int>> adjacency,
            Vector2Int start,
            Vector2Int target)
        {
            var path = new List<Vector2Int>();
            if (adjacency == null || !adjacency.ContainsKey(start) || !adjacency.ContainsKey(target))
                return path;

            var frontier = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                if (current == target)
                    break;

                List<Vector2Int> neighbors = adjacency[current];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Vector2Int neighbor = neighbors[i];
                    if (!visited.Add(neighbor))
                        continue;

                    previous[neighbor] = current;
                    frontier.Enqueue(neighbor);
                }
            }

            if (start != target && !previous.ContainsKey(target))
                return path;

            Vector2Int step = target;
            path.Add(step);
            while (step != start)
            {
                step = previous[step];
                path.Add(step);
            }

            path.Reverse();
            return path;
        }

        Vector3 ResolveGuidePipelineAnchorWorldPosition(
            MazeCellData cell,
            float conduitHeight,
            float channelWidth,
            float laneOffset,
            float topOffset)
        {
            if (cell == null)
                return Vector3.zero;

            bool hasHorizontal = cell.GuidePipelineConnections.HasFlag(MazeCellConnection.East)
                || cell.GuidePipelineConnections.HasFlag(MazeCellConnection.West);
            bool hasVertical = cell.GuidePipelineConnections.HasFlag(MazeCellConnection.North)
                || cell.GuidePipelineConnections.HasFlag(MazeCellConnection.South);
            float horizontalLaneZ = ResolveGuidePipelineHorizontalLaneZ(cell, laneOffset);
            float verticalLaneX = ResolveGuidePipelineVerticalLaneX(cell, laneOffset);
            float y = ResolveGuidePipelineFlowHeight(conduitHeight, topOffset);

            Vector3 localPosition = hasHorizontal && !hasVertical
                ? new Vector3(0f, y, horizontalLaneZ)
                : hasVertical && !hasHorizontal
                    ? new Vector3(verticalLaneX, y, 0f)
                    : new Vector3(verticalLaneX, y, horizontalLaneZ);
            return ResolveCellWorldPosition(cell.GridPosition) + localPosition;
        }

        Vector3 ResolveGuidePipelineConnectionWorldPosition(
            MazeCellData cell,
            Vector2Int direction,
            float conduitHeight,
            float channelWidth,
            float halfCellReach,
            float laneOffset,
            float topOffset)
        {
            if (cell == null)
                return Vector3.zero;

            float horizontalLaneZ = ResolveGuidePipelineHorizontalLaneZ(cell, laneOffset);
            float verticalLaneX = ResolveGuidePipelineVerticalLaneX(cell, laneOffset);
            float y = ResolveGuidePipelineFlowHeight(conduitHeight, topOffset);
            Vector3 localPosition = direction.x != 0
                ? new Vector3(Mathf.Sign(direction.x) * halfCellReach, y, horizontalLaneZ)
                : new Vector3(verticalLaneX, y, Mathf.Sign(direction.y) * halfCellReach);
            return ResolveCellWorldPosition(cell.GridPosition) + localPosition;
        }

        static float ResolveGuidePipelineFlowHeight(float conduitHeight, float topOffset)
        {
            return topOffset + conduitHeight * 0.76f;
        }

        void AddGuidePipelineFlowPoint(List<Vector3> path, Vector3 point)
        {
            if (path == null)
                return;

            if (path.Count > 0 && Vector3.Distance(path[path.Count - 1], point) <= 0.02f)
                return;

            path.Add(point);
        }

        sealed class GuidePipelineFlowBuildResult
        {
            public readonly List<Vector3> Points = new();
            public readonly List<Vector2> OccludedIntervals = new();

            public float CurrentDistance { get; private set; }

            public float AddPoint(Vector3 point)
            {
                if (Points.Count > 0)
                {
                    Vector3 previousPoint = Points[Points.Count - 1];
                    if (Vector3.Distance(previousPoint, point) <= 0.02f)
                        return CurrentDistance;

                    CurrentDistance += Vector3.Distance(previousPoint, point);
                }

                Points.Add(point);
                return CurrentDistance;
            }

            public float AddPointWithStructuralClamps(Vector3 point, float clampHalfLength, float clampSpacingDivisor)
            {
                float segmentStartDistance = CurrentDistance;
                if (Points.Count > 0)
                {
                    Vector3 previousPoint = Points[Points.Count - 1];
                    float segmentLength = Vector3.Distance(previousPoint, point);
                    if (segmentLength <= 0.02f)
                        return CurrentDistance;

                    CurrentDistance += segmentLength;
                    Points.Add(point);
                    AddStructuralClampOcclusions(segmentStartDistance, CurrentDistance, segmentLength, clampHalfLength, clampSpacingDivisor);
                    return CurrentDistance;
                }

                Points.Add(point);
                return CurrentDistance;
            }

            public void AddOccludedInterval(float startDistance, float endDistance)
            {
                float start = Mathf.Min(startDistance, endDistance);
                float end = Mathf.Max(startDistance, endDistance);
                if (end - start > 0.02f)
                    OccludedIntervals.Add(new Vector2(start, end));
            }

            void AddStructuralClampOcclusions(
                float segmentStartDistance,
                float segmentEndDistance,
                float segmentLength,
                float clampHalfLength,
                float clampSpacingDivisor)
            {
                if (segmentLength <= 0.04f)
                    return;

                int clampCount = ResolveGuidePipelineClampCount(segmentLength, clampSpacingDivisor);
                if (clampCount <= 0)
                    return;

                float halfLength = Mathf.Max(0.01f, clampHalfLength);
                for (int i = 1; i <= clampCount; i++)
                {
                    float t = i / (clampCount + 1f);
                    float centerDistance = Mathf.Lerp(segmentStartDistance, segmentEndDistance, t);
                    AddOccludedInterval(centerDistance - halfLength, centerDistance + halfLength);
                }
            }
        }

        void ResolveGuidePipelineVisualMetrics(
            out float conduitHeight,
            out float channelWidth,
            out float coreWidth,
            out float halfCellReach,
            out float laneOffset,
            out float topOffset)
        {
            conduitHeight = Mathf.Clamp(m_FloorRidgeHeight * 0.5f, 0.012f, Mathf.Max(0.012f, m_FloorThickness * 0.28f));
            channelWidth = Mathf.Clamp(m_FloorRidgeWidth * 2.8f, 0.16f, Mathf.Max(0.16f, m_CellSize * 0.11f));
            coreWidth = Mathf.Clamp(channelWidth * Mathf.Clamp(m_FloorRidgeSpacing, 0.28f, 0.62f), 0.055f, channelWidth * 0.5f);
            halfCellReach = Mathf.Clamp(
                m_FloorRidgeLength,
                Mathf.Max(0.1f, m_CellSize * 0.25f),
                Mathf.Max(0.1f, m_CellSize * 0.5f + channelWidth * 0.55f));
            laneOffset = Mathf.Clamp(
                m_CellSize * 0.5f - Mathf.Max(channelWidth, m_GuidePipelineEdgeInset),
                m_CellSize * 0.18f,
                m_CellSize * 0.44f);
            topOffset = conduitHeight * 0.5f + 0.018f;
        }

        float ResolveGuidePipelineClampSpacing()
        {
            return Mathf.Max(0.45f, m_CellSize * 0.34f);
        }

        static int ResolveGuidePipelineClampCount(float segmentLength, float clampSpacing)
        {
            float safeSpacing = Mathf.Max(0.45f, clampSpacing);
            if (segmentLength < safeSpacing * GuidePipelineClampMinSegmentSpacingRatio)
                return 0;

            return Mathf.Clamp(Mathf.FloorToInt(segmentLength / safeSpacing), 1, 3);
        }

        static float ResolveGuidePipelineClampOcclusionHalfLength(float coreWidth)
        {
            return Mathf.Max(coreWidth * 0.6f, 0.035f);
        }

        static float ResolveGuidePipelineHorizontalLaneZ(MazeCellData cell, float laneOffset)
        {
            return ResolveGuidePipelineHorizontalLaneSide(cell) * laneOffset;
        }

        static float ResolveGuidePipelineVerticalLaneX(MazeCellData cell, float laneOffset)
        {
            return ResolveGuidePipelineVerticalLaneSide(cell) * laneOffset;
        }

        static int ResolveGuidePipelineHorizontalLaneSide(MazeCellData cell)
        {
            if (cell == null || cell.GuidePipelineHorizontalLaneSide == 0)
                return ResolveStableGuidePipelineSide(cell != null ? cell.GridPosition.y : 0);

            return cell.GuidePipelineHorizontalLaneSide > 0 ? 1 : -1;
        }

        static int ResolveGuidePipelineVerticalLaneSide(MazeCellData cell)
        {
            if (cell == null || cell.GuidePipelineVerticalLaneSide == 0)
                return ResolveStableGuidePipelineSide(cell != null ? cell.GridPosition.x : 0);

            return cell.GuidePipelineVerticalLaneSide > 0 ? 1 : -1;
        }

        static int ResolveStableGuidePipelineSide(int coordinate)
        {
            unchecked
            {
                int hash = coordinate * 1103515245 + 12345;
                return (hash & 1) == 0 ? 1 : -1;
            }
        }

        void BuildCellWalls(Transform parent, MazeLayout layout, MazeCellData cell)
        {
            TryCreateWall(parent, layout, cell, MazeCellConnection.North, Vector2Int.up);
            TryCreateWall(parent, layout, cell, MazeCellConnection.East, Vector2Int.right);
            TryCreateWall(parent, layout, cell, MazeCellConnection.South, Vector2Int.down);
            TryCreateWall(parent, layout, cell, MazeCellConnection.West, Vector2Int.left);

            if (cell.TerrainVariant == MazeCellTerrainVariant.BeveledWallCorners)
                TryCreateBeveledWallCorner(parent, layout, cell);
        }

        void TryCreateBeveledWallCorner(Transform parent, MazeLayout layout, MazeCellData cell)
        {
            if (parent == null || cell == null || !TryResolveBeveledCorner(layout, cell, out Vector2Int corner))
                return;

            float wallHeight = ResolveWallHeight(isBoundaryWall: false);
            float cornerRun = Mathf.Clamp(m_BeveledCornerRun, m_WallThickness * 2f, Mathf.Max(m_WallThickness * 2f, m_CellSize * 0.45f));
            float wallOverlapInset = Mathf.Clamp(m_WallThickness * 0.35f, 0.01f, Mathf.Max(0.01f, m_WallThickness * 0.45f));
            float centerInset = cornerRun * 0.5f + wallOverlapInset;
            Vector3 cellCenter = ResolveCellWorldPosition(cell.GridPosition);
            Vector3 cornerOffset = new(
                corner.x * (m_CellSize * 0.5f - centerInset),
                wallHeight * 0.5f - m_FloorThickness * 0.5f,
                corner.y * (m_CellSize * 0.5f - centerInset));
            Vector3 wallNormal = new Vector3(corner.x, 0f, corner.y).normalized;
            float bevelDepth = Mathf.Max(
                m_WallThickness * 1.6f,
                m_WallThickness * Mathf.Max(0.5f, m_BeveledCornerThicknessMultiplier));

            GameObject bevelObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bevelObject.name = $"BeveledCorner_{cell.GridPosition.x}_{cell.GridPosition.y}_{corner.x}_{corner.y}";
            bevelObject.transform.SetParent(parent, false);
            bevelObject.transform.SetPositionAndRotation(
                cellCenter + cornerOffset,
                Quaternion.LookRotation(wallNormal, Vector3.up));
            bevelObject.transform.localScale = new Vector3(
                cornerRun * 1.41421356f,
                wallHeight,
                bevelDepth);

            Renderer bevelRenderer = bevelObject.GetComponent<Renderer>();
            if (bevelRenderer != null)
                bevelRenderer.sharedMaterial = ResolveWallMaterial();
        }

        static bool TryResolveBeveledCorner(MazeLayout layout, MazeCellData cell, out Vector2Int corner)
        {
            corner = Vector2Int.zero;
            if (layout == null || cell == null)
                return false;

            List<Vector2Int> candidates = new(4);
            TryAddInternalClosedCorner(layout, cell, MazeCellConnection.North, MazeCellConnection.East, new Vector2Int(1, 1), candidates);
            TryAddInternalClosedCorner(layout, cell, MazeCellConnection.East, MazeCellConnection.South, new Vector2Int(1, -1), candidates);
            TryAddInternalClosedCorner(layout, cell, MazeCellConnection.South, MazeCellConnection.West, new Vector2Int(-1, -1), candidates);
            TryAddInternalClosedCorner(layout, cell, MazeCellConnection.West, MazeCellConnection.North, new Vector2Int(-1, 1), candidates);

            if (candidates.Count == 0)
                return false;

            int hash = ComputeGridHash(cell.GridPosition);
            if (hash < 0)
                hash = ~hash;

            corner = candidates[hash % candidates.Count];
            return true;
        }

        static void TryAddInternalClosedCorner(
            MazeLayout layout,
            MazeCellData cell,
            MazeCellConnection firstWall,
            MazeCellConnection secondWall,
            Vector2Int corner,
            ICollection<Vector2Int> candidates)
        {
            if (layout == null || cell == null || candidates == null)
                return;

            if (!cell.Connections.HasFlag(firstWall)
                && !cell.Connections.HasFlag(secondWall)
                && HasNeighborInDirection(layout, cell, firstWall)
                && HasNeighborInDirection(layout, cell, secondWall))
            {
                candidates.Add(corner);
            }
        }

        static int ComputeGridHash(Vector2Int gridPosition)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + gridPosition.x;
                hash = hash * 31 + gridPosition.y;
                return hash;
            }
        }

        void TryCreateWall(
            Transform parent,
            MazeLayout layout,
            MazeCellData cell,
            MazeCellConnection connectionFlag,
            Vector2Int direction)
        {
            if (cell.Connections.HasFlag(connectionFlag))
                return;

            Vector2Int neighborPosition = cell.GridPosition + direction;
            bool hasNeighbor = layout.TryGetCell(neighborPosition, out _);
            if ((connectionFlag == MazeCellConnection.South || connectionFlag == MazeCellConnection.West) && hasNeighbor)
            {
                return;
            }

            bool isBoundaryWall = !hasNeighbor;
            bool useHiddenDoorSecurityHeight = !isBoundaryWall && ShouldUseHiddenDoorSecurityWallHeight(layout, cell, neighborPosition);
            float wallHeight = ResolveWallHeight(isBoundaryWall || useHiddenDoorSecurityHeight);

            GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObject.name = $"Wall_{cell.GridPosition.x}_{cell.GridPosition.y}_{connectionFlag}";
            wallObject.transform.SetParent(parent, false);
            wallObject.transform.position = ResolveWallWorldPosition(cell.GridPosition, direction, wallHeight);
            wallObject.transform.localScale = ResolveWallScale(direction, wallHeight);

            Renderer wallRenderer = wallObject.GetComponent<Renderer>();
            if (wallRenderer != null)
                wallRenderer.sharedMaterial = ResolveWallMaterial();
        }

        bool ShouldUseHiddenDoorSecurityWallHeight(MazeLayout layout, MazeCellData cell, Vector2Int neighborPosition)
        {
            if (layout == null || cell == null)
                return false;

            if (IsInHiddenDoorSecurityZone(layout, cell))
                return true;

            return layout.TryGetCell(neighborPosition, out MazeCellData neighbor)
                && IsInHiddenDoorSecurityZone(layout, neighbor);
        }

        bool IsInHiddenDoorSecurityZone(MazeLayout layout, MazeCellData cell)
        {
            if (layout == null || cell == null)
                return false;

            if (TouchesHiddenDoorConnection(layout, cell))
                return true;

            for (int i = 0; i < HiddenDoorSecurityDirections.Length; i++)
            {
                if (!layout.TryGetCell(cell.GridPosition + HiddenDoorSecurityDirections[i], out MazeCellData neighbor))
                    continue;

                if (TouchesHiddenDoorConnection(layout, neighbor))
                    return true;
            }

            return false;
        }

        bool TouchesHiddenDoorConnection(MazeLayout layout, MazeCellData cell)
        {
            if (layout == null || cell == null)
                return false;

            if (cell.HiddenDoorConnections != MazeCellConnection.None)
                return true;

            for (int i = 0; i < HiddenDoorSecurityDirections.Length; i++)
            {
                Vector2Int neighborPosition = cell.GridPosition + HiddenDoorSecurityDirections[i];
                if (!layout.TryGetCell(neighborPosition, out MazeCellData neighbor) || neighbor == null)
                    continue;

                MazeCellConnection neighborConnectionToCell = MazeLayout.ResolveConnectionFlag(neighbor.GridPosition, cell.GridPosition);
                if (neighbor.HiddenDoorConnections.HasFlag(neighborConnectionToCell))
                    return true;
            }

            return false;
        }

        void BuildCellHiddenDoors(Transform parent, MazeLayout layout, MazeRunBootstrap bootstrap, MazeCellData cell)
        {
            if (parent == null || layout == null || cell == null || cell.HiddenDoorConnections == MazeCellConnection.None)
                return;

            TryCreateHiddenDoor(parent, layout, bootstrap, cell, MazeCellConnection.North, Vector2Int.up);
            TryCreateHiddenDoor(parent, layout, bootstrap, cell, MazeCellConnection.East, Vector2Int.right);
            TryCreateHiddenDoor(parent, layout, bootstrap, cell, MazeCellConnection.South, Vector2Int.down);
            TryCreateHiddenDoor(parent, layout, bootstrap, cell, MazeCellConnection.West, Vector2Int.left);
        }

        void TryCreateHiddenDoor(
            Transform parent,
            MazeLayout layout,
            MazeRunBootstrap bootstrap,
            MazeCellData cell,
            MazeCellConnection connectionFlag,
            Vector2Int direction)
        {
            if (!cell.HiddenDoorConnections.HasFlag(connectionFlag) || !cell.Connections.HasFlag(connectionFlag))
                return;

            if (!layout.TryGetCell(cell.GridPosition + direction, out _))
                return;

            float wallHeight = ResolveWallHeight(isBoundaryWall: true);
            GameObject doorObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorObject.name = $"ResonanceHiddenDoor_{cell.GridPosition.x}_{cell.GridPosition.y}_{connectionFlag}";
            doorObject.transform.SetParent(parent, false);
            doorObject.transform.position = ResolveHiddenDoorWorldPosition(cell.GridPosition, direction, wallHeight);
            doorObject.transform.localScale = ResolveHiddenDoorScale(direction, wallHeight);

            Collider blockingCollider = doorObject.GetComponent<Collider>();
            if (blockingCollider != null)
                blockingCollider.isTrigger = false;

            Renderer doorRenderer = doorObject.GetComponent<Renderer>();
            if (doorRenderer != null)
                doorRenderer.sharedMaterial = ResolveWallMaterial();

            Renderer[] unstableSurfaceRenderers = doorRenderer != null
                ? new[] { doorRenderer }
                : System.Array.Empty<Renderer>();

            Transform interactionPointRoot = CreateHiddenDoorInteractionPoint(doorObject.transform, direction, wallHeight);
            Renderer[] interactionPointRenderers = interactionPointRoot != null
                ? interactionPointRoot.GetComponentsInChildren<Renderer>(true)
                : System.Array.Empty<Renderer>();
            Collider interactionCollider = interactionPointRoot != null
                ? interactionPointRoot.GetComponent<Collider>()
                : null;

            AttachPulseRevealVisual(
                doorObject,
                new Color(0f, 0f, 0f, 1f),
                new Color(0.16f, 0.9f, 1f, 1f),
                2.5f);

            XRSimpleInteractable interactable = doorObject.GetComponent<XRSimpleInteractable>();
            if (interactable == null)
                interactable = doorObject.AddComponent<XRSimpleInteractable>();
            if (interactable != null)
            {
                interactable.colliders.Clear();
                if (interactionCollider != null)
                    interactable.colliders.Add(interactionCollider);
            }

            var hiddenDoor = doorObject.GetComponent<ResonanceHiddenDoor>();
            if (hiddenDoor == null)
                hiddenDoor = doorObject.AddComponent<ResonanceHiddenDoor>();

            hiddenDoor.ConfigureGeneratedDoor(
                doorObject.transform,
                blockingCollider,
                interactionPointRoot,
                unstableSurfaceRenderers,
                interactionPointRenderers,
                Vector3.up * (wallHeight + 0.35f),
                m_HiddenDoorRequiredPulseHits,
                m_HiddenDoorProgressResetDelay,
                ResolveHiddenDoorPulseHitClip(),
                ResolveHiddenDoorTriggeredClip(),
                ResolveHiddenDoorOpenedClip(),
                ResolveHiddenDoorProgressResetClip());
            hiddenDoor.ConfigureNavigationState(bootstrap, cell.GridPosition, connectionFlag);

            if (interactable != null)
                interactable.enabled = false;
        }

        Transform CreateHiddenDoorInteractionPoint(Transform doorTransform, Vector2Int direction, float wallHeight)
        {
            if (doorTransform == null)
                return null;

            GameObject pointObject = new("InteractionPoint");
            pointObject.name = "InteractionPoint";
            pointObject.transform.SetParent(doorTransform, false);
            pointObject.transform.localPosition = Vector3.zero;
            pointObject.transform.localRotation = Quaternion.identity;
            pointObject.transform.localScale = Vector3.one;

            float plateWidth = Mathf.Max(0.15f, m_HiddenDoorInteractionPlateWidth);
            float plateHeight = Mathf.Max(0.25f, m_HiddenDoorInteractionPlateHeight);
            float hitboxWidth = Mathf.Max(plateWidth, m_HiddenDoorInteractionPointDiameter * 1.35f);
            float hitboxHeight = Mathf.Max(plateHeight, m_HiddenDoorInteractionPointDiameter * 2.5f);
            float hitboxDepth = Mathf.Max(0.02f, m_HiddenDoorInteractionDepth);
            Vector2 centerOffset = new(0f, ResolveHiddenDoorInteractionPointHeightOffset(wallHeight));

            BoxCollider pointCollider = pointObject.AddComponent<BoxCollider>();
            pointCollider.isTrigger = true;
            ApplyHiddenDoorFaceCollider(
                pointCollider,
                doorTransform,
                direction,
                centerOffset,
                new Vector2(hitboxWidth, hitboxHeight),
                hitboxDepth);

            CreateHiddenDoorLatchPiece(
                pointObject.transform,
                doorTransform,
                direction,
                "RecessedBackplate",
                centerOffset,
                new Vector2(plateWidth, plateHeight),
                hitboxDepth * 0.32f,
                m_HiddenDoorInteractionPlateColor);

            CreateHiddenDoorLatchPiece(
                pointObject.transform,
                doorTransform,
                direction,
                "VerticalGrip",
                centerOffset + new Vector2(0f, -plateHeight * 0.02f),
                new Vector2(plateWidth * 0.2f, plateHeight * 0.68f),
                hitboxDepth * 0.58f,
                m_HiddenDoorInteractionPointColor);

            CreateHiddenDoorLatchPiece(
                pointObject.transform,
                doorTransform,
                direction,
                "TopGripBracket",
                centerOffset + new Vector2(0f, plateHeight * 0.38f),
                new Vector2(plateWidth * 0.58f, plateHeight * 0.055f),
                hitboxDepth * 0.55f,
                m_HiddenDoorInteractionPointColor);

            CreateHiddenDoorLatchPiece(
                pointObject.transform,
                doorTransform,
                direction,
                "BottomGripBracket",
                centerOffset + new Vector2(0f, -plateHeight * 0.42f),
                new Vector2(plateWidth * 0.58f, plateHeight * 0.055f),
                hitboxDepth * 0.55f,
                m_HiddenDoorInteractionPointColor);

            CreateHiddenDoorLatchPiece(
                pointObject.transform,
                doorTransform,
                direction,
                "LeftStatusEdge",
                centerOffset + new Vector2(-plateWidth * 0.43f, 0f),
                new Vector2(plateWidth * 0.045f, plateHeight * 0.72f),
                hitboxDepth * 0.42f,
                m_HiddenDoorInteractionAccentColor);

            CreateHiddenDoorLatchPiece(
                pointObject.transform,
                doorTransform,
                direction,
                "RightStatusEdge",
                centerOffset + new Vector2(plateWidth * 0.43f, 0f),
                new Vector2(plateWidth * 0.045f, plateHeight * 0.72f),
                hitboxDepth * 0.42f,
                m_HiddenDoorInteractionAccentColor);

            pointObject.SetActive(false);
            return pointObject.transform;
        }

        void CreateHiddenDoorLatchPiece(
            Transform root,
            Transform doorTransform,
            Vector2Int direction,
            string pieceName,
            Vector2 faceOffset,
            Vector2 faceSize,
            float depth,
            Color color)
        {
            if (root == null || doorTransform == null)
                return;

            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = pieceName;
            piece.transform.SetParent(root, false);
            ApplyHiddenDoorFaceTransform(piece.transform, doorTransform, direction, faceOffset, faceSize, depth);

            Collider pieceCollider = piece.GetComponent<Collider>();
            if (pieceCollider != null)
                pieceCollider.enabled = false;

            Renderer pieceRenderer = piece.GetComponent<Renderer>();
            if (pieceRenderer != null)
            {
                pieceRenderer.sharedMaterial = ResolveHiddenDoorInteractionPointMaterial();
                ApplyRendererMaterialAndColor(pieceRenderer, null, color);
            }
        }

        float ResolveHiddenDoorInteractionPointHeightOffset(float wallHeight)
        {
            float doorCenterHeight = wallHeight * 0.5f - m_FloorThickness * 0.5f;
            return Mathf.Clamp(1.45f - doorCenterHeight, -wallHeight * 0.42f, wallHeight * 0.08f);
        }

        void ApplyHiddenDoorFaceCollider(
            BoxCollider target,
            Transform doorTransform,
            Vector2Int direction,
            Vector2 faceOffset,
            Vector2 faceSize,
            float depth)
        {
            if (target == null || doorTransform == null)
                return;

            Vector3 parentScale = doorTransform.localScale;
            float safeDepth = Mathf.Max(0.005f, depth);
            bool northSouth = direction.y != 0;
            Vector3 desiredLocalOffset;
            Vector3 desiredWorldSize;

            if (northSouth)
            {
                float faceDepthOffset = parentScale.z * 0.5f + safeDepth * 0.5f + 0.012f;
                desiredLocalOffset = new Vector3(faceOffset.x, faceOffset.y, Mathf.Sign(direction.y) * faceDepthOffset);
                desiredWorldSize = new Vector3(Mathf.Max(0.01f, faceSize.x), Mathf.Max(0.01f, faceSize.y), safeDepth);
            }
            else
            {
                float faceDepthOffset = parentScale.x * 0.5f + safeDepth * 0.5f + 0.012f;
                desiredLocalOffset = new Vector3(Mathf.Sign(direction.x) * faceDepthOffset, faceOffset.y, faceOffset.x);
                desiredWorldSize = new Vector3(safeDepth, Mathf.Max(0.01f, faceSize.y), Mathf.Max(0.01f, faceSize.x));
            }

            target.center = DivideByScale(desiredLocalOffset, parentScale);
            target.size = DivideByScale(desiredWorldSize, parentScale);
        }

        void ApplyHiddenDoorFaceTransform(
            Transform target,
            Transform doorTransform,
            Vector2Int direction,
            Vector2 faceOffset,
            Vector2 faceSize,
            float depth)
        {
            if (target == null || doorTransform == null)
                return;

            Vector3 parentScale = doorTransform.localScale;
            float safeDepth = Mathf.Max(0.005f, depth);
            bool northSouth = direction.y != 0;
            Vector3 desiredLocalOffset;
            Vector3 desiredWorldSize;

            if (northSouth)
            {
                float faceDepthOffset = parentScale.z * 0.5f + safeDepth * 0.5f + 0.006f;
                desiredLocalOffset = new Vector3(faceOffset.x, faceOffset.y, Mathf.Sign(direction.y) * faceDepthOffset);
                desiredWorldSize = new Vector3(Mathf.Max(0.01f, faceSize.x), Mathf.Max(0.01f, faceSize.y), safeDepth);
            }
            else
            {
                float faceDepthOffset = parentScale.x * 0.5f + safeDepth * 0.5f + 0.006f;
                desiredLocalOffset = new Vector3(Mathf.Sign(direction.x) * faceDepthOffset, faceOffset.y, faceOffset.x);
                desiredWorldSize = new Vector3(safeDepth, Mathf.Max(0.01f, faceSize.y), Mathf.Max(0.01f, faceSize.x));
            }

            target.localPosition = DivideByScale(desiredLocalOffset, parentScale);
            target.localRotation = Quaternion.identity;
            target.localScale = DivideByScale(desiredWorldSize, parentScale);
        }

        GameObject PlaceSpecialObject(MazeRunBootstrap bootstrap, Transform startRoot, MazeCellData cell)
        {
            switch (cell.Role)
            {
                case MazeCellRole.Start:
                    return InstantiateTemplate(
                        m_StartPrefab,
                        startRoot,
                        cell,
                        "GeneratedStartPad",
                        m_StartLocalOffset);
                case MazeCellRole.Goal:
                    return InstantiateTemplate(
                        m_GoalPrefab,
                        bootstrap.GoalRoot,
                        cell,
                        "GeneratedGoalBeacon",
                        m_GoalLocalOffset);
                case MazeCellRole.Refill:
                    return InstantiateTemplate(
                        m_RefillTemplate != null ? m_RefillTemplate.gameObject : null,
                        bootstrap.RefillsRoot,
                        cell,
                        $"GeneratedRefill_{cell.GridPosition.x}_{cell.GridPosition.y}",
                        m_RefillLocalOffset);
                case MazeCellRole.HealthRefill:
                    return InstantiateTemplate(
                        m_HealthRefillTemplate != null ? m_HealthRefillTemplate.gameObject : null,
                        bootstrap.RefillsRoot,
                        cell,
                        $"GeneratedHealthRefill_{cell.GridPosition.x}_{cell.GridPosition.y}",
                        m_HealthRefillLocalOffset);
                case MazeCellRole.Trap:
                    return InstantiateTemplate(
                        m_TrapPrefab,
                        bootstrap.HazardsRoot,
                        cell,
                        $"GeneratedTrap_{cell.GridPosition.x}_{cell.GridPosition.y}",
                        m_TrapLocalOffset);
                case MazeCellRole.Reward:
                    return CreateRewardPlaceholder(
                        bootstrap.RewardsRoot,
                        cell,
                        $"GeneratedReward_{cell.GridPosition.x}_{cell.GridPosition.y}");
            }

            return null;
        }

        GameObject InstantiateTemplate(
            GameObject template,
            Transform parent,
            MazeCellData cell,
            string instanceName,
            Vector3 localOffset)
        {
            if (template == null || parent == null)
                return null;

            GameObject instance = Instantiate(template, parent);
            instance.name = instanceName;
            instance.transform.SetPositionAndRotation(
                ResolveCellWorldPosition(cell.GridPosition) + localOffset,
                template.transform.rotation);
            instance.transform.localScale = template.transform.localScale;
            instance.SetActive(true);
            return instance;
        }

        GameObject CreateRewardPlaceholder(Transform parent, MazeCellData cell, string instanceName)
        {
            if (parent == null)
                return null;

            var rewardRoot = new GameObject(instanceName);
            rewardRoot.transform.SetParent(parent, false);
            rewardRoot.transform.position = ResolveCellWorldPosition(cell.GridPosition) + m_RewardLocalOffset;
            rewardRoot.transform.localScale = Vector3.one;
            rewardRoot.AddComponent<RewardPlaceholderVisual>();

            SphereCollider trigger = rewardRoot.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = m_RewardTriggerRadius;

            RewardPickup pickup = rewardRoot.AddComponent<RewardPickup>();
            pickup.Configure(RunRewardType.Coin, m_DefaultRewardAmount);

            CreateCoinVisual(rewardRoot.transform);

            AttachPulseRevealVisual(
                rewardRoot,
                new Color(0.08f, 0.048f, 0.005f, 1f),
                new Color(1f, 0.84f, 0.18f, 1f),
                3f);

            return rewardRoot;
        }

        void CreateCoinVisual(Transform parent)
        {
            float diameter = Mathf.Max(
                0.48f,
                Mathf.Max(Mathf.Abs(m_RewardPlaceholderScale.x), Mathf.Abs(m_RewardPlaceholderScale.z)) * 1.45f);
            float faceDiameter = diameter * 0.78f;
            float thickness = Mathf.Max(0.08f, Mathf.Abs(m_RewardPlaceholderScale.y) * 0.22f);
            float faceThickness = Mathf.Max(0.008f, thickness * 0.08f);
            float faceOffset = thickness * 0.62f;

            CreateCoinCylinder(
                parent,
                "CoinRim",
                diameter,
                thickness,
                Vector3.zero,
                new Color(0.95f, 0.55f, 0.05f, 1f));

            CreateCoinCylinder(
                parent,
                "CoinFaceFront",
                faceDiameter,
                faceThickness,
                Vector3.forward * faceOffset,
                new Color(1f, 0.79f, 0.16f, 1f));

            CreateCoinCylinder(
                parent,
                "CoinFaceBack",
                faceDiameter,
                faceThickness,
                Vector3.back * faceOffset,
                new Color(1f, 0.73f, 0.12f, 1f));

            CreateCoinFaceBars(parent, faceDiameter, faceOffset + faceThickness * 1.2f, true);
            CreateCoinFaceBars(parent, faceDiameter, -(faceOffset + faceThickness * 1.2f), false);
        }

        void CreateCoinCylinder(Transform parent, string name, float diameter, float thickness, Vector3 localPosition, Color color)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            piece.name = name;
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = localPosition;
            piece.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            piece.transform.localScale = new Vector3(diameter, thickness * 0.5f, diameter);

            Collider pieceCollider = piece.GetComponent<Collider>();
            if (pieceCollider != null)
                DestroyImmediateOrRuntime(pieceCollider);

            Renderer pieceRenderer = piece.GetComponent<Renderer>();
            if (pieceRenderer != null)
                ApplyRendererMaterialAndColor(pieceRenderer, m_FloorRevealMaterial, color);
        }

        void CreateCoinFaceBars(Transform parent, float faceDiameter, float zOffset, bool frontFace)
        {
            float barWidth = faceDiameter * 0.58f;
            float barHeight = faceDiameter * 0.065f;
            float barDepth = Mathf.Max(0.01f, Mathf.Abs(zOffset) * 0.12f);
            float verticalSpacing = faceDiameter * 0.16f;

            for (int i = 0; i < 3; i++)
            {
                float verticalOffset = (i - 1) * verticalSpacing;
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = frontFace ? $"CoinFrontMark_{i + 1}" : $"CoinBackMark_{i + 1}";
                bar.transform.SetParent(parent, false);
                bar.transform.localPosition = new Vector3(0f, verticalOffset, zOffset);
                bar.transform.localRotation = Quaternion.identity;
                bar.transform.localScale = new Vector3(barWidth, barHeight, barDepth);

                Collider barCollider = bar.GetComponent<Collider>();
                if (barCollider != null)
                    DestroyImmediateOrRuntime(barCollider);

                Renderer barRenderer = bar.GetComponent<Renderer>();
                if (barRenderer != null)
                    ApplyRendererMaterialAndColor(barRenderer, m_FloorRevealMaterial, new Color(1f, 0.94f, 0.48f, 1f));
            }
        }

        void CreateRoleMarker(Transform parent, MazeCellData cell)
        {
            if (cell.Role == MazeCellRole.Neutral || cell.Role == MazeCellRole.Safe)
                return;

            GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markerObject.name = $"Marker_{cell.Role}_{cell.GridPosition.x}_{cell.GridPosition.y}";
            markerObject.transform.SetParent(parent, false);
            markerObject.transform.position = ResolveCellWorldPosition(cell.GridPosition)
                + Vector3.up * (m_RoleMarkerHeight * 0.5f + 0.02f);
            markerObject.transform.localScale = new Vector3(
                m_RoleMarkerDiameter,
                m_RoleMarkerHeight * 0.5f,
                m_RoleMarkerDiameter);

            Collider markerCollider = markerObject.GetComponent<Collider>();
            if (markerCollider != null)
                DestroyImmediateOrRuntime(markerCollider);

            Renderer markerRenderer = markerObject.GetComponent<Renderer>();
            if (markerRenderer != null)
                ApplyRendererMaterialAndColor(markerRenderer, null, ResolveMarkerColor(cell.Role));
        }

        static void ApplyRendererMaterialAndColor(Renderer renderer, Material baseMaterial, Color color)
        {
            if (renderer == null)
                return;

            if (baseMaterial != null)
                renderer.sharedMaterial = baseMaterial;

            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(ColorPropertyId, color);
            propertyBlock.SetColor(BaseColorPropertyId, color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        bool ShouldCreateRoleMarker(MazeCellData cell)
        {
            return cell.Role == MazeCellRole.Start && m_StartPrefab == null;
        }

        void RegisterPlacedModuleNavigation(MazeCellData cell, GameObject placedSpecialObject)
        {
            if (cell == null)
                return;

            MazeModuleNavigationAuthoring authoring = ResolveNavigationAuthoringSource(cell, placedSpecialObject);
            var resolvedPortals = new List<MazeNavigationPortalDefinition>();
            var resolvedLocalNodes = new List<MazeNavigationLocalNodeDefinition>();
            var resolvedLocalEdges = new List<MazeNavigationLocalEdgeDefinition>();
            Vector3 worldCenter = ResolveCellWorldPosition(cell.GridPosition);
            Quaternion worldRotation = ResolveNavigationRotation(placedSpecialObject);
            Vector3 worldScale = Vector3.one;
            Bounds moduleBounds = ResolveModuleBounds(worldCenter);
            Bounds walkableBounds = ResolveWalkableBounds(authoring, worldCenter, worldRotation, worldScale, moduleBounds);
            string sourceName = ResolveNavigationSourceName(cell, authoring, placedSpecialObject);
            MazeNavigationAreaTag defaultAreaTag = authoring != null
                ? authoring.DefaultAreaTag
                : ResolveFallbackAreaTag(cell);
            int navigationLayer = authoring != null ? authoring.NavigationLayer : 0;

            if (authoring != null)
            {
                authoring.GetResolvedPortals(Mathf.Max(0.01f, m_CellSize), resolvedPortals);
                authoring.GetResolvedLocalNodes(Mathf.Max(0.01f, m_CellSize), resolvedLocalNodes);
                authoring.GetResolvedLocalEdges(Mathf.Max(0.01f, m_CellSize), resolvedLocalEdges);
            }

            m_PlacedNavigationModules[cell.GridPosition] = new MazePlacedModuleNavigationData(
                cell.GridPosition,
                sourceName,
                worldCenter,
                worldRotation,
                worldScale,
                moduleBounds,
                walkableBounds,
                defaultAreaTag,
                navigationLayer,
                authoring,
                resolvedPortals,
                resolvedLocalNodes,
                resolvedLocalEdges);
        }

        MazeModuleNavigationAuthoring ResolveNavigationAuthoringSource(MazeCellData cell, GameObject placedSpecialObject)
        {
            if (placedSpecialObject != null)
            {
                MazeModuleNavigationAuthoring instanceAuthoring = placedSpecialObject.GetComponentInChildren<MazeModuleNavigationAuthoring>(true);
                if (instanceAuthoring != null)
                    return instanceAuthoring;
            }

            for (int i = 0; i < m_RoleNavigationAuthoringOverrides.Count; i++)
            {
                MazeRoleNavigationAuthoringOverride roleOverride = m_RoleNavigationAuthoringOverrides[i];
                if (roleOverride.role == cell.Role && roleOverride.authoring != null)
                    return roleOverride.authoring;
            }

            return m_DefaultNavigationAuthoring;
        }

        static string ResolveNavigationSourceName(MazeCellData cell, MazeModuleNavigationAuthoring authoring, GameObject placedSpecialObject)
        {
            if (authoring != null)
                return authoring.ModuleId;

            if (placedSpecialObject != null)
                return placedSpecialObject.name;

            return cell != null
                ? $"Cell_{cell.GridPosition.x}_{cell.GridPosition.y}"
                : "Cell";
        }

        static Quaternion ResolveNavigationRotation(GameObject placedSpecialObject)
        {
            return placedSpecialObject != null
                ? placedSpecialObject.transform.rotation
                : Quaternion.identity;
        }

        Bounds ResolveModuleBounds(Vector3 worldCenter)
        {
            return new Bounds(
                worldCenter,
                new Vector3(
                    Mathf.Max(0.01f, m_CellSize),
                    Mathf.Max(0.5f, ResolveWallHeight(isBoundaryWall: false)),
                    Mathf.Max(0.01f, m_CellSize)));
        }

        static Bounds ResolveWalkableBounds(
            MazeModuleNavigationAuthoring authoring,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 worldScale,
            Bounds fallbackBounds)
        {
            if (authoring == null)
                return fallbackBounds;

            Bounds localBounds = authoring.GetResolvedLocalWalkableBounds(fallbackBounds.size.x);
            return TransformLocalBounds(localBounds, worldPosition, worldRotation, worldScale);
        }

        static Bounds TransformLocalBounds(Bounds localBounds, Vector3 worldPosition, Quaternion worldRotation, Vector3 worldScale)
        {
            Vector3 scaledCenter = Vector3.Scale(localBounds.center, worldScale);
            Vector3 worldCenter = worldPosition + worldRotation * scaledCenter;

            Vector3 extents = localBounds.extents;
            Vector3 axisX = worldRotation * Vector3.Scale(new Vector3(extents.x, 0f, 0f), worldScale);
            Vector3 axisY = worldRotation * Vector3.Scale(new Vector3(0f, extents.y, 0f), worldScale);
            Vector3 axisZ = worldRotation * Vector3.Scale(new Vector3(0f, 0f, extents.z), worldScale);
            Vector3 worldExtents = new(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));

            return new Bounds(worldCenter, worldExtents * 2f);
        }

        static MazeNavigationAreaTag ResolveFallbackAreaTag(MazeCellData cell)
        {
            if (cell == null)
                return MazeNavigationAreaTag.Default;

            return cell.Role switch
            {
                MazeCellRole.Trap => MazeNavigationAreaTag.UnsafeEdge,
                _ => MazeNavigationAreaTag.Default,
            };
        }

        void AttachPulseRevealVisual(GameObject target, Color backgroundColor, Color pulseColor, float emissionStrength)
        {
            if (target == null)
                return;

            PulseRevealVisual pulseVisual = target.GetComponent<PulseRevealVisual>();
            if (pulseVisual == null)
                pulseVisual = target.AddComponent<PulseRevealVisual>();

            pulseVisual.SetVisual(backgroundColor, pulseColor, emissionStrength);
        }

        AudioClip ResolveHiddenDoorPulseHitClip()
        {
            return m_HiddenDoorPulseHitClip != null
                ? m_HiddenDoorPulseHitClip
                : ResolveHiddenDoorResourceClip(HiddenDoorPulseHitClipResourcePath, ref s_DefaultHiddenDoorPulseHitClip);
        }

        AudioClip ResolveHiddenDoorTriggeredClip()
        {
            return m_HiddenDoorTriggeredClip != null
                ? m_HiddenDoorTriggeredClip
                : ResolveHiddenDoorResourceClip(HiddenDoorTriggeredClipResourcePath, ref s_DefaultHiddenDoorTriggeredClip);
        }

        AudioClip ResolveHiddenDoorOpenedClip()
        {
            return m_HiddenDoorOpenedClip != null
                ? m_HiddenDoorOpenedClip
                : ResolveHiddenDoorResourceClip(HiddenDoorOpenedClipResourcePath, ref s_DefaultHiddenDoorOpenedClip);
        }

        AudioClip ResolveHiddenDoorProgressResetClip()
        {
            return m_HiddenDoorProgressResetClip != null
                ? m_HiddenDoorProgressResetClip
                : ResolveHiddenDoorResourceClip(HiddenDoorProgressResetClipResourcePath, ref s_DefaultHiddenDoorProgressResetClip);
        }

        static AudioClip ResolveHiddenDoorResourceClip(string resourcePath, ref AudioClip cachedClip)
        {
            if (cachedClip == null && !string.IsNullOrWhiteSpace(resourcePath))
                cachedClip = Resources.Load<AudioClip>(resourcePath);

            return cachedClip;
        }

        void PlacePrototypeEnemies(MazeLayout layout, MazeRunBootstrap bootstrap)
        {
            if (layout == null || bootstrap == null || bootstrap.EnemiesRoot == null)
                return;

            int requestedEnemyCount = Mathf.Max(0, bootstrap.PrototypeEnemyCount);
            if (requestedEnemyCount <= 0 || m_RobotScoutEnemyPrefab == null)
                return;

            List<MazeCellData> spawnCandidates = BuildPrototypeEnemySpawnCandidates(layout, bootstrap);
            int spawnedCount = 0;
            for (int i = 0; i < spawnCandidates.Count && spawnedCount < requestedEnemyCount; i++)
            {
                SpawnPrototypeEnemy(bootstrap, layout, spawnCandidates[i], spawnedCount);
                spawnedCount++;
            }
        }

        List<MazeCellData> BuildPrototypeEnemySpawnCandidates(MazeLayout layout, MazeRunBootstrap bootstrap)
        {
            var orderedCandidates = new List<MazeCellData>();
            if (layout == null || bootstrap == null)
                return orderedCandidates;

            MazeCellData startCell = FindCellByRole(layout, MazeCellRole.Start);
            MazeCellData goalCell = FindCellByRole(layout, MazeCellRole.Goal);
            var addedPositions = new HashSet<Vector2Int>();
            var rng = new System.Random(unchecked(layout.Seed * 397) ^ 0x4D455A45);

            AppendEnemySpawnCandidates(
                orderedCandidates,
                addedPositions,
                layout,
                bootstrap,
                startCell,
                goalCell,
                rng,
                cell => cell.IsMainPath
                    && cell.PathZone == MazePathZone.Late
                    && GetConnectionCount(cell.Connections) >= 2);

            AppendEnemySpawnCandidates(
                orderedCandidates,
                addedPositions,
                layout,
                bootstrap,
                startCell,
                goalCell,
                rng,
                cell => cell.IsMainPath
                    && cell.PathZone == MazePathZone.Mid
                    && GetConnectionCount(cell.Connections) >= 2);

            AppendEnemySpawnCandidates(
                orderedCandidates,
                addedPositions,
                layout,
                bootstrap,
                startCell,
                goalCell,
                rng,
                cell => !cell.IsMainPath
                    && ResolveBranchZone(cell, bootstrap, layout.MainPath.Count) == MazePathZone.Late
                    && GetConnectionCount(cell.Connections) >= 2);

            AppendEnemySpawnCandidates(
                orderedCandidates,
                addedPositions,
                layout,
                bootstrap,
                startCell,
                goalCell,
                rng,
                cell => !cell.IsMainPath
                    && ResolveBranchZone(cell, bootstrap, layout.MainPath.Count) == MazePathZone.Mid
                    && GetConnectionCount(cell.Connections) >= 2);

            AppendEnemySpawnCandidates(
                orderedCandidates,
                addedPositions,
                layout,
                bootstrap,
                startCell,
                goalCell,
                rng,
                cell => ResolveBranchZone(cell, bootstrap, layout.MainPath.Count) != MazePathZone.Start
                    && GetConnectionCount(cell.Connections) >= 2);

            AppendEnemySpawnCandidates(
                orderedCandidates,
                addedPositions,
                layout,
                bootstrap,
                startCell,
                goalCell,
                rng,
                cell => ResolveBranchZone(cell, bootstrap, layout.MainPath.Count) != MazePathZone.Start
                    && GetConnectionCount(cell.Connections) >= 1);

            return orderedCandidates;
        }

        void AppendEnemySpawnCandidates(
            List<MazeCellData> orderedCandidates,
            HashSet<Vector2Int> addedPositions,
            MazeLayout layout,
            MazeRunBootstrap bootstrap,
            MazeCellData startCell,
            MazeCellData goalCell,
            System.Random rng,
            System.Predicate<MazeCellData> stageFilter)
        {
            if (orderedCandidates == null || addedPositions == null || layout == null || bootstrap == null || stageFilter == null)
                return;

            var stageCandidates = new List<MazeCellData>();
            for (int i = 0; i < layout.Cells.Count; i++)
            {
                MazeCellData cell = layout.Cells[i];
                if (!IsEligibleEnemySpawnCell(cell, bootstrap, startCell, goalCell))
                    continue;

                if (!stageFilter(cell))
                    continue;

                stageCandidates.Add(cell);
            }

            Shuffle(stageCandidates, rng);
            for (int i = 0; i < stageCandidates.Count; i++)
            {
                MazeCellData cell = stageCandidates[i];
                if (!addedPositions.Add(cell.GridPosition))
                    continue;

                orderedCandidates.Add(cell);
            }
        }

        bool IsEligibleEnemySpawnCell(MazeCellData cell, MazeRunBootstrap bootstrap, MazeCellData startCell, MazeCellData goalCell)
        {
            if (cell == null || bootstrap == null)
                return false;

            if (cell.Role != MazeCellRole.Neutral)
                return false;

            if (cell.IsMainPath && cell.PathZone == MazePathZone.Start)
                return false;

            if (startCell != null && GetGridDistance(cell.GridPosition, startCell.GridPosition) <= bootstrap.StartSafeDistance)
                return false;

            if (goalCell != null && GetGridDistance(cell.GridPosition, goalCell.GridPosition) <= bootstrap.GoalSafeDistance)
                return false;

            return true;
        }

        void SpawnPrototypeEnemy(MazeRunBootstrap bootstrap, MazeLayout layout, MazeCellData spawnCell, int enemyIndex)
        {
            if (bootstrap == null || layout == null || spawnCell == null || bootstrap.EnemiesRoot == null || m_RobotScoutEnemyPrefab == null)
                return;

            GameObject enemyInstance = Instantiate(m_RobotScoutEnemyPrefab, bootstrap.EnemiesRoot);
            enemyInstance.name = enemyIndex == 0
                ? "GeneratedRobotScoutEnemy"
                : $"GeneratedRobotScoutEnemy_{enemyIndex + 1}";

            Vector3 spawnPosition = ResolveCellWorldPosition(spawnCell.GridPosition) + m_EnemyLocalOffset;
            Quaternion spawnRotation = ResolveEnemySpawnRotation(layout, spawnCell);
            enemyInstance.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            enemyInstance.transform.localScale = m_RobotScoutEnemyPrefab.transform.localScale;
            enemyInstance.SetActive(true);

            EnemyPatrolController patrolController = enemyInstance.GetComponent<EnemyPatrolController>();
            if (patrolController != null)
            {
                patrolController.SetRoamCenter(ResolveCellWorldPosition(spawnCell.GridPosition));
                patrolController.SetPatrolRadius(Mathf.Max(m_CellSize * 2.5f, m_EnemyPatrolRadius));
            }
        }

        Quaternion ResolveEnemySpawnRotation(MazeLayout layout, MazeCellData spawnCell)
        {
            if (layout == null || spawnCell == null)
                return m_RobotScoutEnemyPrefab != null ? m_RobotScoutEnemyPrefab.transform.rotation : Quaternion.identity;

            Vector2Int preferredDirection = Vector2Int.zero;
            int preferredDistance = int.MinValue;

            TrySelectFacingDirection(layout, spawnCell, MazeCellConnection.North, Vector2Int.up, ref preferredDirection, ref preferredDistance);
            TrySelectFacingDirection(layout, spawnCell, MazeCellConnection.East, Vector2Int.right, ref preferredDirection, ref preferredDistance);
            TrySelectFacingDirection(layout, spawnCell, MazeCellConnection.South, Vector2Int.down, ref preferredDirection, ref preferredDistance);
            TrySelectFacingDirection(layout, spawnCell, MazeCellConnection.West, Vector2Int.left, ref preferredDirection, ref preferredDistance);

            if (preferredDirection == Vector2Int.zero)
                return m_RobotScoutEnemyPrefab != null ? m_RobotScoutEnemyPrefab.transform.rotation : Quaternion.identity;

            Vector3 forward = new Vector3(preferredDirection.x, 0f, preferredDirection.y);
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        void TrySelectFacingDirection(
            MazeLayout layout,
            MazeCellData spawnCell,
            MazeCellConnection requiredConnection,
            Vector2Int direction,
            ref Vector2Int preferredDirection,
            ref int preferredDistance)
        {
            if (layout == null || spawnCell == null || !spawnCell.Connections.HasFlag(requiredConnection))
                return;

            if (!layout.TryGetCell(spawnCell.GridPosition + direction, out MazeCellData neighbor) || neighbor == null)
                return;

            if (neighbor.DistanceFromStart < preferredDistance)
                return;

            preferredDistance = neighbor.DistanceFromStart;
            preferredDirection = direction;
        }

        Vector3 ResolveCellWorldPosition(Vector2Int gridPosition)
        {
            return GetCellWorldPosition(gridPosition);
        }

        public Vector3 GetCellWorldPosition(Vector2Int gridPosition)
        {
            Vector3 origin = ResolvedPlacementOrigin;
            return origin + new Vector3(gridPosition.x * m_CellSize, 0f, gridPosition.y * m_CellSize);
        }

        public Vector2Int WorldToGridPosition(Vector3 worldPosition)
        {
            Vector3 origin = ResolvedPlacementOrigin;
            float normalizedX = (worldPosition.x - origin.x) / Mathf.Max(0.01f, m_CellSize);
            float normalizedZ = (worldPosition.z - origin.z) / Mathf.Max(0.01f, m_CellSize);
            return new Vector2Int(
                Mathf.RoundToInt(normalizedX),
                Mathf.RoundToInt(normalizedZ));
        }

        void CachePlacementOrigin()
        {
            m_LastResolvedOrigin = ResolvePlacementOrigin();
            m_HasResolvedOrigin = true;
        }

        Vector3 ResolvePlacementOrigin()
        {
            return m_AlignStartToAnchor && m_StartAnchor != null
                ? m_StartAnchor.position
                : m_ManualOrigin;
        }

        static MazeCellData FindCellByRole(MazeLayout layout, MazeCellRole role)
        {
            if (layout == null)
                return null;

            for (int i = 0; i < layout.Cells.Count; i++)
            {
                MazeCellData cell = layout.Cells[i];
                if (cell.Role == role)
                    return cell;
            }

            return null;
        }

        static MazePathZone ResolveBranchZone(MazeCellData cell, MazeRunBootstrap bootstrap, int mainPathCount)
        {
            if (cell == null || bootstrap == null)
                return MazePathZone.None;

            if (cell.IsMainPath)
                return cell.PathZone;

            if (mainPathCount <= 2)
                return MazePathZone.None;

            int interiorCount = Mathf.Max(0, mainPathCount - 2);
            if (interiorCount == 0)
                return MazePathZone.None;

            int startInteriorCount = Mathf.Clamp(Mathf.FloorToInt(interiorCount * bootstrap.StartZoneRatio), 0, interiorCount);
            int remainingAfterStart = Mathf.Max(0, interiorCount - startInteriorCount);
            int midInteriorCount = Mathf.Clamp(Mathf.FloorToInt(interiorCount * bootstrap.MidZoneRatio), 0, remainingAfterStart);
            int index = Mathf.Clamp(cell.DistanceFromStart, 0, mainPathCount - 1);

            if (index <= 0)
                return MazePathZone.Start;

            if (index >= mainPathCount - 1)
                return MazePathZone.Late;

            if (index <= startInteriorCount)
                return MazePathZone.Start;

            if (index <= startInteriorCount + midInteriorCount)
                return MazePathZone.Mid;

            return MazePathZone.Late;
        }

        static int GetGridDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        static int GetConnectionCount(MazeCellConnection connections)
        {
            int count = 0;
            if (connections.HasFlag(MazeCellConnection.North))
                count++;
            if (connections.HasFlag(MazeCellConnection.East))
                count++;
            if (connections.HasFlag(MazeCellConnection.South))
                count++;
            if (connections.HasFlag(MazeCellConnection.West))
                count++;
            return count;
        }

        static void Shuffle<T>(IList<T> items, System.Random rng)
        {
            if (items == null || rng == null)
                return;

            for (int i = items.Count - 1; i > 0; i--)
            {
                int swapIndex = rng.Next(i + 1);
                (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
            }
        }

        float ResolveWallHeight(bool isBoundaryWall)
        {
            float multiplier = isBoundaryWall ? m_BoundaryWallHeightMultiplier : m_InternalWallHeightMultiplier;
            return m_WallHeight * multiplier;
        }

        Vector3 ResolveWallWorldPosition(Vector2Int gridPosition, Vector2Int direction, float wallHeight)
        {
            Vector3 cellCenter = ResolveCellWorldPosition(gridPosition);
            Vector3 halfStep = new Vector3(direction.x * m_CellSize * 0.5f, 0f, direction.y * m_CellSize * 0.5f);
            return cellCenter + halfStep + Vector3.up * (wallHeight * 0.5f - m_FloorThickness * 0.5f);
        }

        Vector3 ResolveWallScale(Vector2Int direction, float wallHeight)
        {
            bool northSouth = direction.y != 0;
            return northSouth
                ? new Vector3(m_CellSize + m_WallThickness, wallHeight, m_WallThickness)
                : new Vector3(m_WallThickness, wallHeight, m_CellSize + m_WallThickness);
        }

        static bool HasNeighborInDirection(MazeLayout layout, MazeCellData cell, MazeCellConnection connection)
        {
            return layout != null
                && cell != null
                && layout.TryGetCell(cell.GridPosition + ResolveConnectionDirection(connection), out _);
        }

        static Vector2Int ResolveConnectionDirection(MazeCellConnection connection)
        {
            return connection switch
            {
                MazeCellConnection.North => Vector2Int.up,
                MazeCellConnection.East => Vector2Int.right,
                MazeCellConnection.South => Vector2Int.down,
                MazeCellConnection.West => Vector2Int.left,
                _ => Vector2Int.zero,
            };
        }

        Vector3 ResolveHiddenDoorWorldPosition(Vector2Int gridPosition, Vector2Int direction, float wallHeight)
        {
            Vector3 wallPosition = ResolveWallWorldPosition(gridPosition, direction, wallHeight);
            Vector3 wallNormal = new(direction.x, 0f, direction.y);
            float depthBias = Mathf.Min(Mathf.Max(0f, m_HiddenDoorDepthBias), Mathf.Max(0.001f, m_WallThickness) * 0.25f);
            return wallPosition - wallNormal * depthBias;
        }

        Vector3 ResolveHiddenDoorScale(Vector2Int direction, float wallHeight)
        {
            Vector3 scale = ResolveWallScale(direction, wallHeight);
            float sideInset = Mathf.Min(
                Mathf.Max(0f, m_HiddenDoorSideInset),
                Mathf.Max(0f, (m_CellSize - m_WallThickness) * 0.45f));

            if (direction.y != 0)
                scale.x = Mathf.Max(m_WallThickness, scale.x - sideInset * 2f);
            else
                scale.z = Mathf.Max(m_WallThickness, scale.z - sideInset * 2f);

            return scale;
        }

        void CreateSupportSlab(Transform parent, MazeLayout layout)
        {
            if (parent == null || layout == null || layout.Cells.Count == 0)
                return;

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity;
            float maxZ = float.NegativeInfinity;

            for (int i = 0; i < layout.Cells.Count; i++)
            {
                Vector3 center = ResolveCellWorldPosition(layout.Cells[i].GridPosition);
                minX = Mathf.Min(minX, center.x - (m_CellSize * 0.5f));
                maxX = Mathf.Max(maxX, center.x + (m_CellSize * 0.5f));
                minZ = Mathf.Min(minZ, center.z - (m_CellSize * 0.5f));
                maxZ = Mathf.Max(maxZ, center.z + (m_CellSize * 0.5f));
            }

            float width = (maxX - minX) + (m_SupportPadding * 2f);
            float depth = (maxZ - minZ) + (m_SupportPadding * 2f);
            Vector3 centerPosition = new(
                (minX + maxX) * 0.5f,
                -m_FloorThickness - (m_SupportThickness * 0.5f),
                (minZ + maxZ) * 0.5f);

            GameObject supportObject = new("SupportSlab");
            supportObject.transform.SetParent(parent, false);
            supportObject.transform.position = centerPosition;

            BoxCollider supportCollider = supportObject.AddComponent<BoxCollider>();
            supportCollider.size = new Vector3(width, m_SupportThickness, depth);
        }

        static Color ResolveMarkerColor(MazeCellRole role)
        {
            return role switch
            {
                MazeCellRole.Start => new Color(0.3f, 1f, 0.44f, 1f),
                MazeCellRole.Goal => new Color(0.16f, 0.88f, 1f, 1f),
                MazeCellRole.Refill => new Color(1f, 0.82f, 0.22f, 1f),
                MazeCellRole.HealthRefill => new Color(0.34f, 1f, 0.58f, 1f),
                MazeCellRole.Reward => new Color(1f, 0.52f, 0.14f, 1f),
                MazeCellRole.Trap => new Color(1f, 0.28f, 0.28f, 1f),
                _ => new Color(0.8f, 0.8f, 0.84f, 1f),
            };
        }

        T FindTemplate<T>(string preferredName) where T : Component
        {
            T[] candidates = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null)
                    continue;

                if (candidate.transform.IsChildOf(transform))
                    continue;

                if (candidate.gameObject.name == preferredName || candidate.gameObject.name.StartsWith(preferredName))
                    return candidate;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                T candidate = candidates[i];
                if (candidate == null)
                    continue;

                if (candidate.transform.IsChildOf(transform))
                    continue;

                return candidate;
            }

            return null;
        }

        void AutoAssignDefaultAssets()
        {
            EnsureDefaultNavigationAuthoringSource();

#if UNITY_EDITOR
            if (m_FloorRevealMaterial == null)
                m_FloorRevealMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultFloorMaterialPath);

            if (m_WallRevealMaterial == null)
                m_WallRevealMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultWallMaterialPath);

            if (m_EditorPreviewFloorMaterial == null)
                m_EditorPreviewFloorMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultEditorPreviewFloorMaterialPath);

            if (m_EditorPreviewWallMaterial == null)
                m_EditorPreviewWallMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultEditorPreviewWallMaterialPath);

            if (m_TrapPrefab == null)
                m_TrapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTrapPrefabPath);

            if (m_GoalPrefab == null)
                m_GoalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultGoalPrefabPath);

            if (m_StartPrefab == null)
                m_StartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultStartPrefabPath);

            if (m_HealthRefillTemplate == null)
                m_HealthRefillTemplate = AssetDatabase.LoadAssetAtPath<HealthRefillStation>(DefaultHealthRefillPrefabPath);

            if (m_RobotScoutEnemyPrefab == null)
                m_RobotScoutEnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultRobotScoutEnemyPrefabPath);
#endif
        }

        void EnsureDefaultNavigationAuthoringSource()
        {
            if (m_DefaultNavigationAuthoring != null)
                return;

            m_DefaultNavigationAuthoring = GetComponent<MazeModuleNavigationAuthoring>();
            if (m_DefaultNavigationAuthoring == null)
                m_DefaultNavigationAuthoring = gameObject.AddComponent<MazeModuleNavigationAuthoring>();
        }

        int ResolveGroundLayer()
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            return groundLayer >= 0 ? groundLayer : 0;
        }

        Material ResolveFloorMaterial()
        {
            return m_FloorRevealMaterial != null ? m_FloorRevealMaterial : m_EditorPreviewFloorMaterial;
        }

        Material ResolveWallMaterial()
        {
            return m_WallRevealMaterial != null ? m_WallRevealMaterial : m_EditorPreviewWallMaterial;
        }

        Material ResolveGuidePipelineBaseMaterial()
        {
            if (m_RuntimeGuidePipelineBaseMaterial != null)
                return m_RuntimeGuidePipelineBaseMaterial;

            Shader shader = ResolveSimpleUnlitShader();
            if (shader == null)
                return ResolveFloorMaterial();

            m_RuntimeGuidePipelineBaseMaterial = new Material(shader)
            {
                name = "GeneratedGuidePipelineBaseMaterial",
            };
            ApplyMaterialColor(m_RuntimeGuidePipelineBaseMaterial, new Color(0.001f, 0.008f, 0.01f, 1f));
            ApplyMaterialEmission(m_RuntimeGuidePipelineBaseMaterial, new Color(0f, 0.012f, 0.016f, 1f));
            return m_RuntimeGuidePipelineBaseMaterial;
        }

        Material ResolveGuidePipelineGlowMaterial()
        {
            if (m_RuntimeGuidePipelineGlowMaterial != null)
                return m_RuntimeGuidePipelineGlowMaterial;

            Shader shader = ResolveSimpleUnlitShader();
            if (shader == null)
                return ResolveFloorMaterial();

            m_RuntimeGuidePipelineGlowMaterial = new Material(shader)
            {
                name = "GeneratedGuidePipelineGlowMaterial",
            };
            ApplyMaterialColor(m_RuntimeGuidePipelineGlowMaterial, new Color(0.006f, 0.16f, 0.2f, 1f));
            ApplyMaterialEmission(m_RuntimeGuidePipelineGlowMaterial, new Color(0.004f, 0.1f, 0.13f, 1f));
            return m_RuntimeGuidePipelineGlowMaterial;
        }

        Material ResolveGuidePipelineHaloMaterial()
        {
            if (m_RuntimeGuidePipelineHaloMaterial != null)
                return m_RuntimeGuidePipelineHaloMaterial;

            Shader shader = ResolveSimpleUnlitShader();
            if (shader == null)
                return ResolveFloorMaterial();

            m_RuntimeGuidePipelineHaloMaterial = new Material(shader)
            {
                name = "GeneratedGuidePipelineHaloMaterial",
            };
            ApplyMaterialColor(m_RuntimeGuidePipelineHaloMaterial, new Color(0f, 0.025f, 0.032f, 1f));
            ApplyMaterialEmission(m_RuntimeGuidePipelineHaloMaterial, new Color(0f, 0.03f, 0.04f, 1f));
            return m_RuntimeGuidePipelineHaloMaterial;
        }

        Material ResolveHiddenDoorInteractionPointMaterial()
        {
            if (m_HiddenDoorInteractionPointMaterial != null)
                return m_HiddenDoorInteractionPointMaterial;

            if (m_RuntimeHiddenDoorInteractionPointMaterial != null)
                return m_RuntimeHiddenDoorInteractionPointMaterial;

            Shader shader = ResolveSimpleUnlitShader();

            if (shader == null)
                return ResolveWallMaterial();

            m_RuntimeHiddenDoorInteractionPointMaterial = new Material(shader)
            {
                name = "GeneratedHiddenDoorInteractionPointMaterial",
            };
            ApplyMaterialColor(m_RuntimeHiddenDoorInteractionPointMaterial, m_HiddenDoorInteractionPointColor);
            return m_RuntimeHiddenDoorInteractionPointMaterial;
        }

        static Shader ResolveSimpleUnlitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");

            return shader;
        }

        static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty(BaseColorPropertyId))
                material.SetColor(BaseColorPropertyId, color);
            if (material.HasProperty(ColorPropertyId))
                material.SetColor(ColorPropertyId, color);
        }

        static void ApplyMaterialEmission(Material material, Color emissionColor)
        {
            if (material == null)
                return;

            if (material.HasProperty(EmissionColorPropertyId))
            {
                material.SetColor(EmissionColorPropertyId, emissionColor);
                material.EnableKeyword("_EMISSION");
            }
        }

        void EnsureGeneratedPreviewTools(Transform grayboxRoot, Transform floorsRoot)
        {
            if (grayboxRoot == null)
                return;

            MazeEditorSceneOverlay overlay = grayboxRoot.GetComponent<MazeEditorSceneOverlay>();
            if (overlay == null)
                overlay = grayboxRoot.gameObject.AddComponent<MazeEditorSceneOverlay>();

            overlay.SetShowOverlayDuringPlayMode(true);

            MazeRenderSkinBuilder builder = grayboxRoot.GetComponent<MazeRenderSkinBuilder>();
            if (builder == null)
                builder = grayboxRoot.gameObject.AddComponent<MazeRenderSkinBuilder>();

            builder.ConfigureGeneratedMaze(
                grayboxRoot,
                floorsRoot,
                DefaultRenderSkinMeshAssetPath,
                DefaultRenderSkinMaterialAssetPath);
        }

        void RebuildRenderSkin(Transform grayboxRoot, Transform floorsRoot)
        {
            if (grayboxRoot == null)
                return;

            MazeRenderSkinBuilder builder = grayboxRoot.GetComponent<MazeRenderSkinBuilder>();
            if (builder == null)
                return;

            builder.ConfigureGeneratedMaze(
                grayboxRoot,
                floorsRoot,
                DefaultRenderSkinMeshAssetPath,
                DefaultRenderSkinMaterialAssetPath);
            builder.BuildRuntimeRenderSkin();
        }

        static Vector3 DivideByScale(Vector3 value, Vector3 scale)
        {
            return new Vector3(
                DivideByScaleComponent(value.x, scale.x),
                DivideByScaleComponent(value.y, scale.y),
                DivideByScaleComponent(value.z, scale.z));
        }

        static float DivideByScaleComponent(float value, float scale)
        {
            return Mathf.Abs(scale) > 0.0001f ? value / scale : value;
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

        static void RemoveLegacyChild(Transform parent, string childName)
        {
            if (parent == null)
                return;

            Transform child = parent.Find(childName);
            if (child == null)
                return;

            DestroyGeneratedObject(child.gameObject);
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

        static void DestroyImmediateOrRuntime(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        static void DestroyGeneratedObject(GameObject target)
        {
            if (target == null)
                return;

            // Generated maze content is rebuilt synchronously. Using deferred Destroy during
            // play mode leaves old roots alive until frame end, so new children can get parented
            // under objects that are about to be removed.
            Object.DestroyImmediate(target);
        }
    }
}
