using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Generation
{
    [DisallowMultipleComponent]
    public class MazeNavigationGraph : MonoBehaviour
    {
        readonly struct PortalKey
        {
            public PortalKey(Vector2Int gridPosition, MazeCellConnection connection)
            {
                GridPosition = gridPosition;
                Connection = connection;
            }

            public Vector2Int GridPosition { get; }
            public MazeCellConnection Connection { get; }
        }

        readonly struct OpenNode
        {
            public OpenNode(int nodeId, float priority, float heuristic)
            {
                NodeId = nodeId;
                Priority = priority;
                Heuristic = heuristic;
            }

            public int NodeId { get; }
            public float Priority { get; }
            public float Heuristic { get; }
        }

        sealed class OpenNodeMinHeap
        {
            readonly List<OpenNode> m_Items = new();

            public int Count => m_Items.Count;

            public void Clear()
            {
                m_Items.Clear();
            }

            public void Enqueue(OpenNode node)
            {
                m_Items.Add(node);
                SiftUp(m_Items.Count - 1);
            }

            public OpenNode Dequeue()
            {
                OpenNode root = m_Items[0];
                int lastIndex = m_Items.Count - 1;
                m_Items[0] = m_Items[lastIndex];
                m_Items.RemoveAt(lastIndex);

                if (m_Items.Count > 0)
                    SiftDown(0);

                return root;
            }

            void SiftUp(int index)
            {
                while (index > 0)
                {
                    int parentIndex = (index - 1) / 2;
                    if (!IsHigherPriority(index, parentIndex))
                        break;

                    Swap(index, parentIndex);
                    index = parentIndex;
                }
            }

            void SiftDown(int index)
            {
                while (true)
                {
                    int leftChild = index * 2 + 1;
                    int rightChild = leftChild + 1;
                    int bestIndex = index;

                    if (leftChild < m_Items.Count && IsHigherPriority(leftChild, bestIndex))
                        bestIndex = leftChild;

                    if (rightChild < m_Items.Count && IsHigherPriority(rightChild, bestIndex))
                        bestIndex = rightChild;

                    if (bestIndex == index)
                        return;

                    Swap(index, bestIndex);
                    index = bestIndex;
                }
            }

            bool IsHigherPriority(int leftIndex, int rightIndex)
            {
                OpenNode left = m_Items[leftIndex];
                OpenNode right = m_Items[rightIndex];
                if (!Mathf.Approximately(left.Priority, right.Priority))
                    return left.Priority < right.Priority;

                return left.Heuristic < right.Heuristic;
            }

            void Swap(int a, int b)
            {
                (m_Items[a], m_Items[b]) = (m_Items[b], m_Items[a]);
            }
        }

        [SerializeField] MazeRunBootstrap m_Bootstrap;
        [SerializeField] MazeModulePlacer m_ModulePlacer;
        [SerializeField] MazeLocalNavigationProvider m_LocalNavigationProvider;
        [SerializeField] bool m_DrawDebugGraph;
        [SerializeField, Min(0.01f)] float m_DebugNodeRadius = 0.12f;

        readonly OpenNodeMinHeap m_OpenNodes = new();
        readonly Dictionary<int, MazeNavigationNode> m_NodesById = new();
        readonly Dictionary<int, MazeNavigationModuleRecord> m_ModulesById = new();
        readonly Dictionary<Vector2Int, MazeNavigationModuleRecord> m_ModulesByGridPosition = new();
        readonly Dictionary<int, List<MazeNavigationEdge>> m_AdjacencyByNodeId = new();
        readonly Dictionary<PortalKey, int> m_NodeIdsByPortalKey = new();
        readonly Dictionary<int, int> m_CameFrom = new();
        readonly Dictionary<int, float> m_GScore = new();
        readonly HashSet<int> m_ClosedSet = new();
        readonly HashSet<int> m_ModuleNeighborIds = new();
        readonly List<MazeNavigationNode> m_NodeList = new();
        readonly List<MazeNavigationEdge> m_EdgeList = new();
        readonly List<MazeNavigationPortalDefinition> m_ResolvedPortalBuffer = new();
        readonly List<MazeModuleLocalNavigationNodeRecord> m_LocalNodePathBuffer = new();
        readonly List<MazeModuleLocalNavigationNodeRecord> m_LocalPortalNodeBuffer = new();

        public MazeRunBootstrap Bootstrap => m_Bootstrap;
        public MazeModulePlacer ModulePlacer => m_ModulePlacer;
        public MazeLocalNavigationProvider LocalNavigationProvider => m_LocalNavigationProvider;
        public bool HasGraph => m_NodeList.Count > 0;
        public int ModuleCount => m_ModulesById.Count;
        public int NodeCount => m_NodeList.Count;
        public int EdgeCount => m_EdgeList.Count;
        public IReadOnlyList<MazeNavigationNode> Nodes => m_NodeList;
        public IReadOnlyList<MazeNavigationEdge> Edges => m_EdgeList;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void OnValidate()
        {
            ResolveReferences();
            m_DebugNodeRadius = Mathf.Max(0.01f, m_DebugNodeRadius);
        }

        public void Clear()
        {
            m_OpenNodes.Clear();
            m_NodesById.Clear();
            m_ModulesById.Clear();
            m_ModulesByGridPosition.Clear();
            m_AdjacencyByNodeId.Clear();
            m_NodeIdsByPortalKey.Clear();
            m_CameFrom.Clear();
            m_GScore.Clear();
            m_ClosedSet.Clear();
            m_NodeList.Clear();
            m_EdgeList.Clear();
        }

        public void BuildFromLayout(MazeLayout layout)
        {
            ResolveReferences();
            Clear();

            if (layout == null || m_ModulePlacer == null)
                return;

            for (int i = 0; i < layout.Cells.Count; i++)
                RegisterModule(layout.Cells[i]);

            ConnectIntraModuleEdges();
            ConnectInterModuleEdges();
        }

        public bool TryGetNearestNode(Vector3 worldPosition, out MazeNavigationNode node, int navigationLayer = int.MinValue)
        {
            node = null;
            if (m_NodeList.Count == 0)
                return false;

            float bestDistanceSqr = float.PositiveInfinity;
            Vector3 planarPosition = new(worldPosition.x, 0f, worldPosition.z);
            for (int i = 0; i < m_NodeList.Count; i++)
            {
                MazeNavigationNode candidate = m_NodeList[i];
                if (navigationLayer != int.MinValue && candidate.NavigationLayer != navigationLayer)
                    continue;

                Vector3 planarCandidate = new(candidate.WorldPosition.x, 0f, candidate.WorldPosition.z);
                float distanceSqr = (planarCandidate - planarPosition).sqrMagnitude;
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                node = candidate;
            }

            return node != null;
        }

        public bool TryFindPath(int startNodeId, int targetNodeId, List<int> pathBuffer)
        {
            if (pathBuffer == null)
                return false;

            pathBuffer.Clear();
            if (!m_NodesById.ContainsKey(startNodeId) || !m_NodesById.ContainsKey(targetNodeId))
                return false;

            if (startNodeId == targetNodeId)
            {
                pathBuffer.Add(startNodeId);
                return true;
            }

            m_OpenNodes.Clear();
            m_CameFrom.Clear();
            m_GScore.Clear();
            m_ClosedSet.Clear();

            m_CameFrom[startNodeId] = startNodeId;
            m_GScore[startNodeId] = 0f;
            float startHeuristic = EstimateRemainingCost(startNodeId, targetNodeId);
            m_OpenNodes.Enqueue(new OpenNode(startNodeId, startHeuristic, startHeuristic));

            while (m_OpenNodes.Count > 0)
            {
                OpenNode openNode = m_OpenNodes.Dequeue();
                int currentNodeId = openNode.NodeId;
                if (m_ClosedSet.Contains(currentNodeId))
                    continue;

                if (!m_GScore.TryGetValue(currentNodeId, out float currentGScore))
                    continue;

                float expectedPriority = currentGScore + EstimateRemainingCost(currentNodeId, targetNodeId);
                if (openNode.Priority > expectedPriority + 0.0001f)
                    continue;

                if (currentNodeId == targetNodeId)
                    break;

                m_ClosedSet.Add(currentNodeId);
                if (!m_AdjacencyByNodeId.TryGetValue(currentNodeId, out List<MazeNavigationEdge> edges))
                    continue;

                for (int i = 0; i < edges.Count; i++)
                {
                    MazeNavigationEdge edge = edges[i];
                    if (!IsNavigationEdgeTraversable(edge))
                        continue;

                    int neighborId = edge.ToNodeId;
                    if (m_ClosedSet.Contains(neighborId))
                        continue;

                    float tentativeGScore = currentGScore + Mathf.Max(0.01f, edge.Cost);
                    if (m_GScore.TryGetValue(neighborId, out float existingScore) && tentativeGScore >= existingScore)
                        continue;

                    float heuristic = EstimateRemainingCost(neighborId, targetNodeId);
                    m_CameFrom[neighborId] = currentNodeId;
                    m_GScore[neighborId] = tentativeGScore;
                    m_OpenNodes.Enqueue(new OpenNode(neighborId, tentativeGScore + heuristic, heuristic));
                }
            }

            if (!m_CameFrom.ContainsKey(targetNodeId))
                return false;

            int step = targetNodeId;
            pathBuffer.Add(step);
            while (step != startNodeId)
            {
                step = m_CameFrom[step];
                pathBuffer.Add(step);
            }

            pathBuffer.Reverse();
            return true;
        }

        public bool TryFindPath(Vector3 startWorldPosition, Vector3 targetWorldPosition, List<int> pathBuffer, int navigationLayer = int.MinValue)
        {
            if (!TryGetNearestNode(startWorldPosition, out MazeNavigationNode startNode, navigationLayer)
                || !TryGetNearestNode(targetWorldPosition, out MazeNavigationNode targetNode, navigationLayer))
            {
                return false;
            }

            return TryFindPath(startNode.Id, targetNode.Id, pathBuffer);
        }

        public void GetPathWorldPositions(IList<int> nodePath, List<Vector3> worldPositions)
        {
            if (worldPositions == null)
                return;

            worldPositions.Clear();
            if (nodePath == null)
                return;

            for (int i = 0; i < nodePath.Count; i++)
            {
                if (!m_NodesById.TryGetValue(nodePath[i], out MazeNavigationNode node))
                    continue;

                worldPositions.Add(node.WorldPosition);
            }
        }

        public bool TryGetModule(Vector2Int gridPosition, out MazeNavigationModuleRecord module)
        {
            return m_ModulesByGridPosition.TryGetValue(gridPosition, out module);
        }

        public bool TryGetNearestModule(Vector3 worldPosition, out MazeNavigationModuleRecord module, int navigationLayer = int.MinValue)
        {
            module = null;
            if (m_ModulesById.Count == 0)
                return false;

            float bestDistanceSqr = float.PositiveInfinity;
            foreach (KeyValuePair<int, MazeNavigationModuleRecord> pair in m_ModulesById)
            {
                MazeNavigationModuleRecord candidate = pair.Value;
                if (navigationLayer != int.MinValue && candidate.NavigationLayer != navigationLayer)
                    continue;

                float distanceSqr = GetPlanarBoundsDistanceSqr(candidate.WalkableBounds, worldPosition);
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                module = candidate;
            }

            return module != null;
        }

        public bool GetConnectedModuleGridPositions(Vector2Int gridPosition, List<Vector2Int> results)
        {
            if (results == null)
                return false;

            results.Clear();
            if (!TryGetModule(gridPosition, out MazeNavigationModuleRecord module))
                return false;

            m_ModuleNeighborIds.Clear();
            IReadOnlyList<int> nodeIds = module.NodeIds;
            for (int i = 0; i < nodeIds.Count; i++)
            {
                if (!m_AdjacencyByNodeId.TryGetValue(nodeIds[i], out List<MazeNavigationEdge> edges))
                    continue;

                for (int j = 0; j < edges.Count; j++)
                {
                    MazeNavigationEdge edge = edges[j];
                    if (!IsNavigationEdgeTraversable(edge))
                        continue;

                    if (!m_NodesById.TryGetValue(edge.ToNodeId, out MazeNavigationNode neighborNode))
                        continue;

                    int neighborModuleId = neighborNode.ModuleId;
                    if (neighborModuleId == module.Id || !m_ModuleNeighborIds.Add(neighborModuleId))
                        continue;

                    if (m_ModulesById.TryGetValue(neighborModuleId, out MazeNavigationModuleRecord neighborModule))
                        results.Add(neighborModule.GridPosition);
                }
            }

            return results.Count > 0;
        }

        public bool TryProjectLocalPoint(Vector2Int moduleGridPosition, Vector3 desiredWorldPosition, out Vector3 projectedWorldPosition)
        {
            projectedWorldPosition = desiredWorldPosition;
            if (m_LocalNavigationProvider == null || !TryGetModule(moduleGridPosition, out MazeNavigationModuleRecord module))
                return false;

            return m_LocalNavigationProvider.TryProjectPoint(module, desiredWorldPosition, out projectedWorldPosition);
        }

        public bool TryBuildLocalPath(
            Vector2Int moduleGridPosition,
            Vector3 startWorldPosition,
            Vector3 desiredWorldPosition,
            List<Vector3> waypointBuffer)
        {
            if (m_LocalNavigationProvider == null || !TryGetModule(moduleGridPosition, out MazeNavigationModuleRecord module))
                return false;

            return m_LocalNavigationProvider.TryBuildPath(module, startWorldPosition, desiredWorldPosition, waypointBuffer);
        }

        public bool TryBuildLocalPath(
            Vector2Int fromModuleGridPosition,
            Vector2Int toModuleGridPosition,
            Vector3 startWorldPosition,
            Vector3 desiredWorldPosition,
            List<Vector3> waypointBuffer)
        {
            if (m_LocalNavigationProvider == null
                || !TryGetModule(fromModuleGridPosition, out MazeNavigationModuleRecord fromModule)
                || !TryGetModule(toModuleGridPosition, out MazeNavigationModuleRecord toModule))
            {
                return false;
            }

            return m_LocalNavigationProvider.TryBuildPath(fromModule, toModule, startWorldPosition, desiredWorldPosition, waypointBuffer);
        }

        void RegisterModule(MazeCellData cell)
        {
            if (cell == null || m_ModulePlacer == null)
                return;

            if (m_ModulePlacer.TryGetPlacedModuleNavigationData(cell.GridPosition, out MazePlacedModuleNavigationData placedData) && placedData != null)
            {
                RegisterPlacedModule(cell, placedData);
                return;
            }

            RegisterImplicitCellModule(cell);
        }

        void RegisterPlacedModule(MazeCellData cell, MazePlacedModuleNavigationData placedData)
        {
            if (cell == null || placedData == null)
                return;

            int moduleId = m_ModulesById.Count;
            var module = new MazeNavigationModuleRecord(
                moduleId,
                placedData.SourceName,
                cell.GridPosition,
                placedData.WorldPosition,
                placedData.ModuleBounds,
                placedData.WalkableBounds,
                placedData.DefaultAreaTag,
                placedData.NavigationLayer,
                placedData.Authoring,
                placedData.HasAuthoring);

            m_ModulesById.Add(moduleId, module);
            m_ModulesByGridPosition[cell.GridPosition] = module;

            if (placedData.HasAuthoring)
            {
                RegisterAuthoredPortals(module, cell, placedData);
                RegisterAuthoredLocalNavigation(module, placedData);
                if (module.NodeIds.Count > 0)
                    return;
            }

            RegisterImplicitPortals(module, cell);
        }

        void RegisterImplicitCellModule(MazeCellData cell)
        {
            if (cell == null || m_ModulePlacer == null)
                return;

            int moduleId = m_ModulesById.Count;
            Vector3 worldCenter = m_ModulePlacer.GetCellWorldPosition(cell.GridPosition);
            float cellSize = Mathf.Max(0.01f, m_ModulePlacer.CellSize);
            var module = new MazeNavigationModuleRecord(
                moduleId,
                $"Cell_{cell.GridPosition.x}_{cell.GridPosition.y}",
                cell.GridPosition,
                worldCenter,
                new Bounds(worldCenter, new Vector3(cellSize, 0.2f, cellSize)),
                new Bounds(worldCenter, new Vector3(cellSize, Mathf.Max(0.5f, cellSize * 0.5f), cellSize)),
                ResolveDefaultAreaTag(cell),
                0,
                null,
                false);

            m_ModulesById.Add(moduleId, module);
            m_ModulesByGridPosition[cell.GridPosition] = module;

            RegisterImplicitPortals(module, cell);
        }

        void RegisterImplicitPortals(MazeNavigationModuleRecord module, MazeCellData cell)
        {
            RegisterPortalIfOpen(module, cell, MazeCellConnection.North, Vector2Int.up);
            RegisterPortalIfOpen(module, cell, MazeCellConnection.East, Vector2Int.right);
            RegisterPortalIfOpen(module, cell, MazeCellConnection.South, Vector2Int.down);
            RegisterPortalIfOpen(module, cell, MazeCellConnection.West, Vector2Int.left);
        }

        void RegisterAuthoredPortals(MazeNavigationModuleRecord module, MazeCellData cell, MazePlacedModuleNavigationData placedData)
        {
            if (module == null || cell == null || placedData?.Authoring == null)
                return;

            IReadOnlyList<MazeNavigationPortalDefinition> resolvedPortals = placedData.ResolvedPortals;
            if (resolvedPortals == null)
                return;

            for (int i = 0; i < resolvedPortals.Count; i++)
            {
                MazeNavigationPortalDefinition portal = resolvedPortals[i];
                MazeCellConnection connection = ResolveConnectionFromPortal(portal.localForward, placedData.WorldRotation);
                if (connection != MazeCellConnection.None && !cell.Connections.HasFlag(connection))
                    continue;

                int nodeId = m_NodeList.Count;
                Vector3 worldPosition = placedData.WorldPosition + placedData.WorldRotation * Vector3.Scale(portal.localPosition, placedData.WorldScale);
                Vector3 worldForward = placedData.WorldRotation * portal.localForward;
                int navigationLayer = portal.navigationLayer;
                if (navigationLayer == 0 && placedData.NavigationLayer != 0)
                    navigationLayer = placedData.NavigationLayer;

                MazeNavigationAreaTag areaTag = portal.areaTag == default
                    ? module.DefaultAreaTag
                    : portal.areaTag;
                string portalId = string.IsNullOrWhiteSpace(portal.id)
                    ? $"Portal_{nodeId}"
                    : portal.id;

                var node = new MazeNavigationNode(
                    nodeId,
                    module.Id,
                    portalId,
                    worldPosition,
                    worldForward.sqrMagnitude > 0.0001f ? worldForward.normalized : Vector3.forward,
                    areaTag,
                    portal.traversalType,
                    navigationLayer,
                    cell.GridPosition,
                    connection);

                m_NodeList.Add(node);
                m_NodesById[nodeId] = node;
                module.AddNode(nodeId);

                if (connection != MazeCellConnection.None)
                    m_NodeIdsByPortalKey[new PortalKey(cell.GridPosition, connection)] = nodeId;
            }
        }

        void RegisterAuthoredLocalNavigation(MazeNavigationModuleRecord module, MazePlacedModuleNavigationData placedData)
        {
            if (module == null || placedData == null)
                return;

            module.ClearLocalNavigation();

            IReadOnlyList<MazeNavigationPortalDefinition> resolvedPortals = placedData.ResolvedPortals;
            if (resolvedPortals != null)
            {
                for (int i = 0; i < resolvedPortals.Count; i++)
                {
                    MazeNavigationPortalDefinition portal = resolvedPortals[i];
                    if (string.IsNullOrWhiteSpace(portal.id))
                        continue;

                    if (!TryFindPortalNode(module, portal.id, out MazeNavigationNode portalNode))
                        continue;

                    module.AddLocalNode(new MazeModuleLocalNavigationNodeRecord(
                        portalNode.PortalId,
                        portalNode.WorldPosition,
                        portalNode.AreaTag,
                        isPortalNode: true,
                        portalNodeId: portalNode.Id));
                }
            }

            IReadOnlyList<MazeNavigationLocalNodeDefinition> resolvedLocalNodes = placedData.ResolvedLocalNodes;
            if (resolvedLocalNodes != null)
            {
                for (int i = 0; i < resolvedLocalNodes.Count; i++)
                {
                    MazeNavigationLocalNodeDefinition localNode = resolvedLocalNodes[i];
                    if (string.IsNullOrWhiteSpace(localNode.id))
                        continue;

                    Vector3 worldPosition = placedData.WorldPosition
                        + placedData.WorldRotation * Vector3.Scale(localNode.localPosition, placedData.WorldScale);
                    module.AddLocalNode(new MazeModuleLocalNavigationNodeRecord(
                        localNode.id,
                        worldPosition,
                        localNode.areaTag == default ? module.DefaultAreaTag : localNode.areaTag,
                        isPortalNode: false));
                }
            }

            IReadOnlyList<MazeNavigationLocalEdgeDefinition> resolvedLocalEdges = placedData.ResolvedLocalEdges;
            if (resolvedLocalEdges == null)
                return;

            for (int i = 0; i < resolvedLocalEdges.Count; i++)
            {
                MazeNavigationLocalEdgeDefinition localEdge = resolvedLocalEdges[i];
                if (!module.TryGetLocalNode(localEdge.fromId, out MazeModuleLocalNavigationNodeRecord fromNode)
                    || !module.TryGetLocalNode(localEdge.toId, out MazeModuleLocalNavigationNodeRecord toNode))
                {
                    continue;
                }

                float edgeCost = localEdge.costOverride > 0f
                    ? localEdge.costOverride
                    : Vector3.Distance(fromNode.WorldPosition, toNode.WorldPosition);
                module.AddLocalEdge(new MazeModuleLocalNavigationEdgeRecord(
                    localEdge.fromId,
                    localEdge.toId,
                    edgeCost,
                    localEdge.traversalType,
                    localEdge.bidirectional));
            }
        }

        bool TryFindPortalNode(MazeNavigationModuleRecord module, string portalId, out MazeNavigationNode portalNode)
        {
            portalNode = null;
            if (module == null || string.IsNullOrWhiteSpace(portalId))
                return false;

            IReadOnlyList<int> nodeIds = module.NodeIds;
            for (int i = 0; i < nodeIds.Count; i++)
            {
                if (!m_NodesById.TryGetValue(nodeIds[i], out MazeNavigationNode candidate))
                    continue;

                if (!string.Equals(candidate.PortalId, portalId, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                portalNode = candidate;
                return true;
            }

            return false;
        }

        void RegisterPortalIfOpen(MazeNavigationModuleRecord module, MazeCellData cell, MazeCellConnection connection, Vector2Int direction)
        {
            if (module == null || cell == null || !cell.Connections.HasFlag(connection))
                return;

            int nodeId = m_NodeList.Count;
            Vector3 portalWorldPosition = ResolvePortalWorldPosition(cell.GridPosition, direction);
            Vector3 forward = new(direction.x, 0f, direction.y);
            string portalId = $"{cell.GridPosition.x}_{cell.GridPosition.y}_{connection}";
            var node = new MazeNavigationNode(
                nodeId,
                module.Id,
                portalId,
                portalWorldPosition,
                forward.normalized,
                module.DefaultAreaTag,
                MazeTraversalLinkType.Walk,
                module.NavigationLayer,
                cell.GridPosition,
                connection);

            m_NodeList.Add(node);
            m_NodesById[nodeId] = node;
            m_NodeIdsByPortalKey[new PortalKey(cell.GridPosition, connection)] = nodeId;
            module.AddNode(nodeId);
        }

        void ConnectIntraModuleEdges()
        {
            foreach (KeyValuePair<int, MazeNavigationModuleRecord> pair in m_ModulesById)
            {
                MazeNavigationModuleRecord module = pair.Value;
                if (TryConnectIntraModuleLocalGraph(module))
                    continue;

                IReadOnlyList<int> nodeIds = module.NodeIds;
                for (int i = 0; i < nodeIds.Count; i++)
                {
                    for (int j = i + 1; j < nodeIds.Count; j++)
                    {
                        MazeNavigationNode fromNode = m_NodesById[nodeIds[i]];
                        MazeNavigationNode toNode = m_NodesById[nodeIds[j]];
                        float cost = Vector3.Distance(fromNode.WorldPosition, toNode.WorldPosition);
                        AddBidirectionalEdge(fromNode.Id, toNode.Id, cost, MazeTraversalLinkType.Walk);
                    }
                }
            }
        }

        bool TryConnectIntraModuleLocalGraph(MazeNavigationModuleRecord module)
        {
            if (module == null || !module.HasLocalNavigationGraph)
                return false;

            m_LocalPortalNodeBuffer.Clear();
            IReadOnlyList<MazeModuleLocalNavigationNodeRecord> localNodes = module.LocalNodes;
            for (int i = 0; i < localNodes.Count; i++)
            {
                MazeModuleLocalNavigationNodeRecord localNode = localNodes[i];
                if (!localNode.IsPortalNode || localNode.PortalNodeId < 0)
                    continue;

                m_LocalPortalNodeBuffer.Add(localNode);
            }

            if (m_LocalPortalNodeBuffer.Count < 2)
                return false;

            bool connectedAny = false;
            for (int i = 0; i < m_LocalPortalNodeBuffer.Count; i++)
            {
                MazeModuleLocalNavigationNodeRecord fromNode = m_LocalPortalNodeBuffer[i];
                for (int j = i + 1; j < m_LocalPortalNodeBuffer.Count; j++)
                {
                    MazeModuleLocalNavigationNodeRecord toNode = m_LocalPortalNodeBuffer[j];
                    if (!module.TryFindLocalPath(fromNode.Id, toNode.Id, m_LocalNodePathBuffer, out float localPathCost)
                        || localPathCost <= 0.01f)
                    {
                        continue;
                    }

                    AddBidirectionalEdge(fromNode.PortalNodeId, toNode.PortalNodeId, localPathCost, MazeTraversalLinkType.Walk);
                    connectedAny = true;
                }
            }

            return connectedAny;
        }

        void ConnectInterModuleEdges()
        {
            for (int i = 0; i < m_NodeList.Count; i++)
            {
                MazeNavigationNode node = m_NodeList[i];
                Vector2Int neighborGridPosition = node.GridPosition + ResolveDirection(node.Connection);
                MazeCellConnection oppositeConnection = ResolveOppositeConnection(node.Connection);
                var neighborKey = new PortalKey(neighborGridPosition, oppositeConnection);
                if (!m_NodeIdsByPortalKey.TryGetValue(neighborKey, out int neighborNodeId))
                    continue;

                if (neighborNodeId <= node.Id)
                    continue;

                MazeNavigationNode neighborNode = m_NodesById[neighborNodeId];
                float cost = Vector3.Distance(node.WorldPosition, neighborNode.WorldPosition);
                AddBidirectionalEdge(node.Id, neighborNode.Id, cost, node.TraversalType);
            }
        }

        void AddBidirectionalEdge(int fromNodeId, int toNodeId, float cost, MazeTraversalLinkType traversalType)
        {
            AddEdge(fromNodeId, toNodeId, cost, traversalType, true);
            AddEdge(toNodeId, fromNodeId, cost, traversalType, true);
        }

        void AddEdge(int fromNodeId, int toNodeId, float cost, MazeTraversalLinkType traversalType, bool bidirectional)
        {
            var edge = new MazeNavigationEdge(fromNodeId, toNodeId, Mathf.Max(0.01f, cost), traversalType, bidirectional);
            m_EdgeList.Add(edge);

            if (!m_AdjacencyByNodeId.TryGetValue(fromNodeId, out List<MazeNavigationEdge> adjacency))
            {
                adjacency = new List<MazeNavigationEdge>();
                m_AdjacencyByNodeId.Add(fromNodeId, adjacency);
            }

            adjacency.Add(edge);
        }

        bool IsNavigationEdgeTraversable(MazeNavigationEdge edge)
        {
            if (edge == null || m_Bootstrap == null)
                return edge != null;

            if (!m_NodesById.TryGetValue(edge.FromNodeId, out MazeNavigationNode fromNode)
                || !m_NodesById.TryGetValue(edge.ToNodeId, out MazeNavigationNode toNode))
            {
                return false;
            }

            if (fromNode.GridPosition == toNode.GridPosition || fromNode.Connection == MazeCellConnection.None)
                return true;

            Vector2Int expectedNeighborPosition = fromNode.GridPosition + ResolveDirection(fromNode.Connection);
            if (expectedNeighborPosition != toNode.GridPosition)
                return true;

            return m_Bootstrap.IsMazeConnectionTraversable(fromNode.GridPosition, fromNode.Connection);
        }

        Vector3 ResolvePortalWorldPosition(Vector2Int gridPosition, Vector2Int direction)
        {
            Vector3 cellCenter = m_ModulePlacer.GetCellWorldPosition(gridPosition);
            float halfSpan = Mathf.Max(0.01f, m_ModulePlacer.CellSize * 0.5f);
            return cellCenter + new Vector3(direction.x * halfSpan, 0f, direction.y * halfSpan);
        }

        float EstimateRemainingCost(int fromNodeId, int toNodeId)
        {
            MazeNavigationNode fromNode = m_NodesById[fromNodeId];
            MazeNavigationNode toNode = m_NodesById[toNodeId];
            return Vector3.Distance(fromNode.WorldPosition, toNode.WorldPosition);
        }

        static float GetPlanarBoundsDistanceSqr(Bounds bounds, Vector3 worldPosition)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            float deltaX = 0f;
            if (worldPosition.x < min.x)
                deltaX = min.x - worldPosition.x;
            else if (worldPosition.x > max.x)
                deltaX = worldPosition.x - max.x;

            float deltaZ = 0f;
            if (worldPosition.z < min.z)
                deltaZ = min.z - worldPosition.z;
            else if (worldPosition.z > max.z)
                deltaZ = worldPosition.z - max.z;

            return deltaX * deltaX + deltaZ * deltaZ;
        }

        void ResolveReferences()
        {
            if (m_Bootstrap == null)
                m_Bootstrap = GetComponent<MazeRunBootstrap>();

            if (m_Bootstrap == null)
                m_Bootstrap = FindObjectOfType<MazeRunBootstrap>();

            if (m_ModulePlacer == null && m_Bootstrap != null)
                m_ModulePlacer = m_Bootstrap.ModulePlacer;

            if (m_ModulePlacer == null)
                m_ModulePlacer = GetComponent<MazeModulePlacer>();

            if (m_ModulePlacer == null)
                m_ModulePlacer = FindObjectOfType<MazeModulePlacer>();

            if (m_LocalNavigationProvider == null && m_Bootstrap != null)
                m_LocalNavigationProvider = m_Bootstrap.LocalNavigationProvider;

            if (m_LocalNavigationProvider == null)
                m_LocalNavigationProvider = GetComponent<MazeLocalNavigationProvider>();

            if (m_LocalNavigationProvider == null)
                m_LocalNavigationProvider = FindObjectOfType<MazeLocalNavigationProvider>();
        }

        void OnDrawGizmosSelected()
        {
            if (!m_DrawDebugGraph || m_NodeList.Count == 0)
                return;

            Gizmos.color = new Color(1f, 0.35f, 0.15f, 0.95f);
            for (int i = 0; i < m_NodeList.Count; i++)
            {
                MazeNavigationNode node = m_NodeList[i];
                Gizmos.DrawSphere(node.WorldPosition + Vector3.up * 0.05f, m_DebugNodeRadius);
                Gizmos.DrawLine(
                    node.WorldPosition + Vector3.up * 0.05f,
                    node.WorldPosition + Vector3.up * 0.05f + node.Forward.normalized * Mathf.Max(m_DebugNodeRadius, 0.18f));
            }

            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.55f);
            for (int i = 0; i < m_EdgeList.Count; i++)
            {
                MazeNavigationEdge edge = m_EdgeList[i];
                if (!m_NodesById.TryGetValue(edge.FromNodeId, out MazeNavigationNode fromNode)
                    || !m_NodesById.TryGetValue(edge.ToNodeId, out MazeNavigationNode toNode))
                {
                    continue;
                }

                Gizmos.DrawLine(fromNode.WorldPosition + Vector3.up * 0.05f, toNode.WorldPosition + Vector3.up * 0.05f);
            }
        }

        static MazeNavigationAreaTag ResolveDefaultAreaTag(MazeCellData cell)
        {
            if (cell == null)
                return MazeNavigationAreaTag.Default;

            return cell.Role switch
            {
                MazeCellRole.Trap => MazeNavigationAreaTag.UnsafeEdge,
                _ => MazeNavigationAreaTag.Default,
            };
        }

        static MazeCellConnection ResolveOppositeConnection(MazeCellConnection connection)
        {
            return connection switch
            {
                MazeCellConnection.North => MazeCellConnection.South,
                MazeCellConnection.East => MazeCellConnection.West,
                MazeCellConnection.South => MazeCellConnection.North,
                MazeCellConnection.West => MazeCellConnection.East,
                _ => MazeCellConnection.None,
            };
        }

        static Vector2Int ResolveDirection(MazeCellConnection connection)
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

        static MazeCellConnection ResolveConnectionFromPortal(Vector3 localForward, Quaternion worldRotation)
        {
            Vector3 worldForward = worldRotation * localForward;
            Vector3 planarForward = Vector3.ProjectOnPlane(worldForward, Vector3.up);
            if (planarForward.sqrMagnitude < 0.0001f)
                return MazeCellConnection.None;

            Vector3 normalized = planarForward.normalized;
            float north = Vector3.Dot(normalized, Vector3.forward);
            float east = Vector3.Dot(normalized, Vector3.right);
            float south = Vector3.Dot(normalized, Vector3.back);
            float west = Vector3.Dot(normalized, Vector3.left);

            float best = Mathf.Max(Mathf.Max(north, east), Mathf.Max(south, west));
            if (best < 0.6f)
                return MazeCellConnection.None;

            if (Mathf.Approximately(best, north))
                return MazeCellConnection.North;

            if (Mathf.Approximately(best, east))
                return MazeCellConnection.East;

            if (Mathf.Approximately(best, south))
                return MazeCellConnection.South;

            return MazeCellConnection.West;
        }
    }
}
