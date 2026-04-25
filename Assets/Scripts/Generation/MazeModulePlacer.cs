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

        Vector3 m_LastResolvedOrigin;
        bool m_HasResolvedOrigin;
        Material m_RuntimeHiddenDoorInteractionPointMaterial;
        readonly Dictionary<Vector2Int, MazePlacedModuleNavigationData> m_PlacedNavigationModules = new();

        public float CellSize => m_CellSize;
        public bool HasResolvedPlacementOrigin => m_HasResolvedOrigin;
        public Vector3 ResolvedPlacementOrigin => m_HasResolvedOrigin ? m_LastResolvedOrigin : ResolvePlacementOrigin();
        public bool TryGetPlacedModuleNavigationData(Vector2Int gridPosition, out MazePlacedModuleNavigationData data)
        {
            return m_PlacedNavigationModules.TryGetValue(gridPosition, out data);
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
            Transform hiddenDoorsRoot = EnsureChild(bootstrap.ModulesRoot, HiddenDoorsRootName);
            Transform markersRoot = EnsureChild(bootstrap.ModulesRoot, MarkersRootName);
            Transform startRoot = EnsureChild(bootstrap.ModulesRoot, StartRootName);

            ClearChildren(floorsRoot);
            ClearChildren(wallsRoot);
            ClearChildren(supportRoot);
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
                BuildCellWalls(wallsRoot, layout, cell);
                BuildCellHiddenDoors(hiddenDoorsRoot, layout, bootstrap, cell);
                GameObject placedSpecialObject = PlaceSpecialObject(bootstrap, startRoot, cell);
                RegisterPlacedModuleNavigation(cell, placedSpecialObject);

                if (m_BuildRoleMarkers && ShouldCreateRoleMarker(cell))
                    CreateRoleMarker(markersRoot, cell);
            }

            CreateSupportSlab(supportRoot, layout);
            RebuildRenderSkin(grayboxRoot, floorsRoot);
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

        void BuildCellWalls(Transform parent, MazeLayout layout, MazeCellData cell)
        {
            TryCreateWall(parent, layout, cell, MazeCellConnection.North, Vector2Int.up);
            TryCreateWall(parent, layout, cell, MazeCellConnection.East, Vector2Int.right);
            TryCreateWall(parent, layout, cell, MazeCellConnection.South, Vector2Int.down);
            TryCreateWall(parent, layout, cell, MazeCellConnection.West, Vector2Int.left);
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

        Material ResolveHiddenDoorInteractionPointMaterial()
        {
            if (m_HiddenDoorInteractionPointMaterial != null)
                return m_HiddenDoorInteractionPointMaterial;

            if (m_RuntimeHiddenDoorInteractionPointMaterial != null)
                return m_RuntimeHiddenDoorInteractionPointMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
                return ResolveWallMaterial();

            m_RuntimeHiddenDoorInteractionPointMaterial = new Material(shader)
            {
                name = "GeneratedHiddenDoorInteractionPointMaterial",
            };
            ApplyMaterialColor(m_RuntimeHiddenDoorInteractionPointMaterial, m_HiddenDoorInteractionPointColor);
            return m_RuntimeHiddenDoorInteractionPointMaterial;
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
