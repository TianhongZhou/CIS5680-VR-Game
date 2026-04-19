using UnityEngine;
using CIS5680VRGame.Balls;
using CIS5680VRGame.Gameplay;
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
        const string GrayboxRootName = "GrayboxMaze";
        const string FloorsRootName = "Floors";
        const string WallsRootName = "Walls";
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
        const string DefaultRenderSkinMeshAssetPath = "Assets/Generated/Maze/RandomMazeRenderSkin.asset";
        const string DefaultRenderSkinMaterialAssetPath = "Assets/Generated/Maze/M_RandomMazeRenderSkinPulse.mat";
        const string GroundLayerName = "Ground";

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

        [Header("Markers")]
        [SerializeField] bool m_BuildRoleMarkers = true;
        [SerializeField, Min(0.1f)] float m_RoleMarkerHeight = 1.1f;
        [SerializeField, Min(0.1f)] float m_RoleMarkerDiameter = 0.5f;

        public void BuildModules(MazeLayout layout, MazeRunBootstrap bootstrap)
        {
            if (layout == null || bootstrap == null || bootstrap.ModulesRoot == null)
                return;

            AutoAssignDefaultAssets();
            ResolveTemplates();

            Transform grayboxRoot = EnsureChild(bootstrap.ModulesRoot, GrayboxRootName);
            RemoveLegacyChild(grayboxRoot, MarkersRootName);
            RemoveLegacyChild(grayboxRoot, StartRootName);

            Transform floorsRoot = EnsureChild(grayboxRoot, FloorsRootName);
            Transform wallsRoot = EnsureChild(grayboxRoot, WallsRootName);
            Transform supportRoot = EnsureChild(grayboxRoot, SupportRootName);
            Transform markersRoot = EnsureChild(bootstrap.ModulesRoot, MarkersRootName);
            Transform startRoot = EnsureChild(bootstrap.ModulesRoot, StartRootName);

            ClearChildren(floorsRoot);
            ClearChildren(wallsRoot);
            ClearChildren(supportRoot);
            ClearChildren(markersRoot);
            ClearChildren(startRoot);
            ClearChildren(bootstrap.HazardsRoot);
            ClearChildren(bootstrap.RefillsRoot);
            ClearChildren(bootstrap.RewardsRoot);
            ClearChildren(bootstrap.GoalRoot);

            EnsureGeneratedPreviewTools(grayboxRoot, floorsRoot);

            for (int i = 0; i < layout.Cells.Count; i++)
            {
                MazeCellData cell = layout.Cells[i];
                CreateFloor(floorsRoot, cell);
                BuildCellWalls(wallsRoot, layout, cell);
                PlaceSpecialObject(bootstrap, startRoot, cell);

                if (m_BuildRoleMarkers && ShouldCreateRoleMarker(cell))
                    CreateRoleMarker(markersRoot, cell);
            }

            CreateSupportSlab(supportRoot, layout);
            RebuildRenderSkin(grayboxRoot, floorsRoot);
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
            float wallHeight = ResolveWallHeight(isBoundaryWall);

            GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObject.name = $"Wall_{cell.GridPosition.x}_{cell.GridPosition.y}_{connectionFlag}";
            wallObject.transform.SetParent(parent, false);
            wallObject.transform.position = ResolveWallWorldPosition(cell.GridPosition, direction, wallHeight);
            wallObject.transform.localScale = ResolveWallScale(direction, wallHeight);

            Renderer wallRenderer = wallObject.GetComponent<Renderer>();
            if (wallRenderer != null)
                wallRenderer.sharedMaterial = ResolveWallMaterial();
        }

        void PlaceSpecialObject(MazeRunBootstrap bootstrap, Transform startRoot, MazeCellData cell)
        {
            switch (cell.Role)
            {
                case MazeCellRole.Start:
                    InstantiateTemplate(
                        m_StartPrefab,
                        startRoot,
                        cell,
                        "GeneratedStartPad",
                        m_StartLocalOffset);
                    break;
                case MazeCellRole.Goal:
                    InstantiateTemplate(
                        m_GoalPrefab,
                        bootstrap.GoalRoot,
                        cell,
                        "GeneratedGoalBeacon",
                        m_GoalLocalOffset);
                    break;
                case MazeCellRole.Refill:
                    InstantiateTemplate(
                        m_RefillTemplate != null ? m_RefillTemplate.gameObject : null,
                        bootstrap.RefillsRoot,
                        cell,
                        $"GeneratedRefill_{cell.GridPosition.x}_{cell.GridPosition.y}",
                        m_RefillLocalOffset);
                    break;
                case MazeCellRole.HealthRefill:
                    InstantiateTemplate(
                        m_HealthRefillTemplate != null ? m_HealthRefillTemplate.gameObject : null,
                        bootstrap.RefillsRoot,
                        cell,
                        $"GeneratedHealthRefill_{cell.GridPosition.x}_{cell.GridPosition.y}",
                        m_HealthRefillLocalOffset);
                    break;
                case MazeCellRole.Trap:
                    InstantiateTemplate(
                        m_TrapPrefab,
                        bootstrap.HazardsRoot,
                        cell,
                        $"GeneratedTrap_{cell.GridPosition.x}_{cell.GridPosition.y}",
                        m_TrapLocalOffset);
                    break;
                case MazeCellRole.Reward:
                    CreateRewardPlaceholder(
                        bootstrap.RewardsRoot,
                        cell,
                        $"GeneratedReward_{cell.GridPosition.x}_{cell.GridPosition.y}");
                    break;
            }
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

        void CreateRewardPlaceholder(Transform parent, MazeCellData cell, string instanceName)
        {
            if (parent == null)
                return;

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

            GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = "Crystal";
            crystal.transform.SetParent(rewardRoot.transform, false);
            crystal.transform.localPosition = Vector3.zero;
            crystal.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            crystal.transform.localScale = m_RewardPlaceholderScale;

            Collider crystalCollider = crystal.GetComponent<Collider>();
            if (crystalCollider != null)
                DestroyImmediateOrRuntime(crystalCollider);

            Renderer crystalRenderer = crystal.GetComponent<Renderer>();
            if (crystalRenderer != null)
                ApplyRendererMaterialAndColor(crystalRenderer, m_FloorRevealMaterial, new Color(1f, 0.76f, 0.14f, 1f));

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            core.transform.SetParent(rewardRoot.transform, false);
            core.transform.localPosition = Vector3.zero;
            core.transform.localScale = m_RewardPlaceholderScale * 0.42f;

            Collider coreCollider = core.GetComponent<Collider>();
            if (coreCollider != null)
                DestroyImmediateOrRuntime(coreCollider);

            Renderer coreRenderer = core.GetComponent<Renderer>();
            if (coreRenderer != null)
                ApplyRendererMaterialAndColor(coreRenderer, m_FloorRevealMaterial, new Color(1f, 0.95f, 0.68f, 1f));

            AttachPulseRevealVisual(
                rewardRoot,
                new Color(0.14f, 0.09f, 0.02f, 1f),
                new Color(1f, 0.78f, 0.18f, 1f),
                3f);
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

        void AttachPulseRevealVisual(GameObject target, Color backgroundColor, Color pulseColor, float emissionStrength)
        {
            if (target == null)
                return;

            PulseRevealVisual pulseVisual = target.GetComponent<PulseRevealVisual>();
            if (pulseVisual == null)
                pulseVisual = target.AddComponent<PulseRevealVisual>();

            pulseVisual.SetVisual(backgroundColor, pulseColor, emissionStrength);
        }

        Vector3 ResolveCellWorldPosition(Vector2Int gridPosition)
        {
            Vector3 origin = m_AlignStartToAnchor && m_StartAnchor != null
                ? m_StartAnchor.position
                : m_ManualOrigin;

            return origin + new Vector3(gridPosition.x * m_CellSize, 0f, gridPosition.y * m_CellSize);
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
#endif
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
