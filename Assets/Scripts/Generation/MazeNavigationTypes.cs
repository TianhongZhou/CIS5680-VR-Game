using System;
using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Generation
{
    public enum MazeNavigationAreaTag
    {
        Default = 0,
        NarrowBridge = 1,
        Stairs = 2,
        UpperLevel = 3,
        LowerLevel = 4,
        UnsafeEdge = 5,
        NoEnemy = 6,
        Custom = 100,
    }

    public enum MazeTraversalLinkType
    {
        Walk = 0,
        Bridge = 1,
        Stairs = 2,
        Jump = 3,
        Drop = 4,
        VerticalTransition = 5,
        OneWay = 6,
    }

    [Serializable]
    public struct MazeNavigationPortalDefinition
    {
        public string id;
        public Vector3 localPosition;
        public Vector3 localForward;
        public MazeNavigationAreaTag areaTag;
        public MazeTraversalLinkType traversalType;
        public bool bidirectional;
        public int navigationLayer;
    }

    [Serializable]
    public struct MazeNavigationLocalNodeDefinition
    {
        public string id;
        public Vector3 localPosition;
        public MazeNavigationAreaTag areaTag;
    }

    [Serializable]
    public struct MazeNavigationLocalEdgeDefinition
    {
        public string fromId;
        public string toId;
        public MazeTraversalLinkType traversalType;
        public bool bidirectional;
        public float costOverride;
    }

    [Serializable]
    public struct MazeRoleNavigationAuthoringOverride
    {
        public MazeCellRole role;
        public MazeModuleNavigationAuthoring authoring;
    }

    public sealed class MazePlacedModuleNavigationData
    {
        public MazePlacedModuleNavigationData(
            Vector2Int gridPosition,
            string sourceName,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Vector3 worldScale,
            Bounds moduleBounds,
            Bounds walkableBounds,
            MazeNavigationAreaTag defaultAreaTag,
            int navigationLayer,
            MazeModuleNavigationAuthoring authoring,
            IReadOnlyList<MazeNavigationPortalDefinition> resolvedPortals,
            IReadOnlyList<MazeNavigationLocalNodeDefinition> resolvedLocalNodes,
            IReadOnlyList<MazeNavigationLocalEdgeDefinition> resolvedLocalEdges)
        {
            GridPosition = gridPosition;
            SourceName = sourceName;
            WorldPosition = worldPosition;
            WorldRotation = worldRotation;
            WorldScale = worldScale;
            ModuleBounds = moduleBounds;
            WalkableBounds = walkableBounds;
            DefaultAreaTag = defaultAreaTag;
            NavigationLayer = navigationLayer;
            Authoring = authoring;
            ResolvedPortals = resolvedPortals;
            ResolvedLocalNodes = resolvedLocalNodes;
            ResolvedLocalEdges = resolvedLocalEdges;
        }

        public Vector2Int GridPosition { get; }
        public string SourceName { get; }
        public Vector3 WorldPosition { get; }
        public Quaternion WorldRotation { get; }
        public Vector3 WorldScale { get; }
        public Bounds ModuleBounds { get; }
        public Bounds WalkableBounds { get; }
        public MazeNavigationAreaTag DefaultAreaTag { get; }
        public int NavigationLayer { get; }
        public MazeModuleNavigationAuthoring Authoring { get; }
        public IReadOnlyList<MazeNavigationPortalDefinition> ResolvedPortals { get; }
        public IReadOnlyList<MazeNavigationLocalNodeDefinition> ResolvedLocalNodes { get; }
        public IReadOnlyList<MazeNavigationLocalEdgeDefinition> ResolvedLocalEdges { get; }
        public bool HasAuthoring => Authoring != null;
        public bool HasAuthoredLocalNavigation => ResolvedLocalNodes != null && ResolvedLocalEdges != null && ResolvedLocalEdges.Count > 0;
    }

    public sealed class MazeNavigationNode
    {
        public MazeNavigationNode(
            int id,
            int moduleId,
            string portalId,
            Vector3 worldPosition,
            Vector3 forward,
            MazeNavigationAreaTag areaTag,
            MazeTraversalLinkType traversalType,
            int navigationLayer,
            Vector2Int gridPosition,
            MazeCellConnection connection)
        {
            Id = id;
            ModuleId = moduleId;
            PortalId = portalId;
            WorldPosition = worldPosition;
            Forward = forward;
            AreaTag = areaTag;
            TraversalType = traversalType;
            NavigationLayer = navigationLayer;
            GridPosition = gridPosition;
            Connection = connection;
        }

        public int Id { get; }
        public int ModuleId { get; }
        public string PortalId { get; }
        public Vector3 WorldPosition { get; }
        public Vector3 Forward { get; }
        public MazeNavigationAreaTag AreaTag { get; }
        public MazeTraversalLinkType TraversalType { get; }
        public int NavigationLayer { get; }
        public Vector2Int GridPosition { get; }
        public MazeCellConnection Connection { get; }
    }

    public sealed class MazeNavigationEdge
    {
        public MazeNavigationEdge(
            int fromNodeId,
            int toNodeId,
            float cost,
            MazeTraversalLinkType traversalType,
            bool bidirectional)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Cost = cost;
            TraversalType = traversalType;
            Bidirectional = bidirectional;
        }

        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public float Cost { get; }
        public MazeTraversalLinkType TraversalType { get; }
        public bool Bidirectional { get; }
    }

    public sealed class MazeModuleLocalNavigationNodeRecord
    {
        public MazeModuleLocalNavigationNodeRecord(
            string id,
            Vector3 worldPosition,
            MazeNavigationAreaTag areaTag,
            bool isPortalNode,
            int portalNodeId = -1)
        {
            Id = id;
            WorldPosition = worldPosition;
            AreaTag = areaTag;
            IsPortalNode = isPortalNode;
            PortalNodeId = portalNodeId;
        }

        public string Id { get; }
        public Vector3 WorldPosition { get; }
        public MazeNavigationAreaTag AreaTag { get; }
        public bool IsPortalNode { get; }
        public int PortalNodeId { get; }
    }

    public sealed class MazeModuleLocalNavigationEdgeRecord
    {
        public MazeModuleLocalNavigationEdgeRecord(
            string fromNodeId,
            string toNodeId,
            float cost,
            MazeTraversalLinkType traversalType,
            bool bidirectional)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Cost = cost;
            TraversalType = traversalType;
            Bidirectional = bidirectional;
        }

        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public float Cost { get; }
        public MazeTraversalLinkType TraversalType { get; }
        public bool Bidirectional { get; }
    }

    public sealed class MazeNavigationModuleRecord
    {
        readonly List<int> m_NodeIds = new();
        readonly List<MazeModuleLocalNavigationNodeRecord> m_LocalNodes = new();
        readonly Dictionary<string, MazeModuleLocalNavigationNodeRecord> m_LocalNodesById =
            new(StringComparer.OrdinalIgnoreCase);
        readonly List<MazeModuleLocalNavigationEdgeRecord> m_LocalEdges = new();
        readonly Dictionary<string, List<MazeModuleLocalNavigationEdgeRecord>> m_LocalAdjacencyByNodeId =
            new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, float> m_LocalGScore = new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> m_LocalCameFrom = new(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> m_LocalClosedSet = new(StringComparer.OrdinalIgnoreCase);
        readonly List<string> m_LocalOpenSet = new();
        readonly List<string> m_LocalPathIds = new();

        public MazeNavigationModuleRecord(
            int id,
            string sourceName,
            Vector2Int gridPosition,
            Vector3 worldCenter,
            Bounds bounds,
            Bounds walkableBounds,
            MazeNavigationAreaTag defaultAreaTag,
            int navigationLayer,
            MazeModuleNavigationAuthoring authoring,
            bool usesAuthoredPortals)
        {
            Id = id;
            SourceName = sourceName;
            GridPosition = gridPosition;
            WorldCenter = worldCenter;
            Bounds = bounds;
            WalkableBounds = walkableBounds;
            DefaultAreaTag = defaultAreaTag;
            NavigationLayer = navigationLayer;
            Authoring = authoring;
            UsesAuthoredPortals = usesAuthoredPortals;
        }

        public int Id { get; }
        public string SourceName { get; }
        public Vector2Int GridPosition { get; }
        public Vector3 WorldCenter { get; }
        public Bounds Bounds { get; }
        public Bounds WalkableBounds { get; }
        public MazeNavigationAreaTag DefaultAreaTag { get; }
        public int NavigationLayer { get; }
        public MazeModuleNavigationAuthoring Authoring { get; }
        public bool UsesAuthoredPortals { get; }
        public IReadOnlyList<int> NodeIds => m_NodeIds;
        public IReadOnlyList<MazeModuleLocalNavigationNodeRecord> LocalNodes => m_LocalNodes;
        public IReadOnlyList<MazeModuleLocalNavigationEdgeRecord> LocalEdges => m_LocalEdges;
        public bool HasLocalNavigationGraph => m_LocalNodes.Count > 0 && m_LocalEdges.Count > 0;

        public void AddNode(int nodeId)
        {
            m_NodeIds.Add(nodeId);
        }

        public void ClearLocalNavigation()
        {
            m_LocalNodes.Clear();
            m_LocalNodesById.Clear();
            m_LocalEdges.Clear();
            m_LocalAdjacencyByNodeId.Clear();
            m_LocalGScore.Clear();
            m_LocalCameFrom.Clear();
            m_LocalClosedSet.Clear();
            m_LocalOpenSet.Clear();
            m_LocalPathIds.Clear();
        }

        public bool AddLocalNode(MazeModuleLocalNavigationNodeRecord node)
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id) || m_LocalNodesById.ContainsKey(node.Id))
                return false;

            m_LocalNodes.Add(node);
            m_LocalNodesById.Add(node.Id, node);
            return true;
        }

        public bool AddLocalEdge(MazeModuleLocalNavigationEdgeRecord edge)
        {
            if (edge == null
                || string.IsNullOrWhiteSpace(edge.FromNodeId)
                || string.IsNullOrWhiteSpace(edge.ToNodeId)
                || !m_LocalNodesById.ContainsKey(edge.FromNodeId)
                || !m_LocalNodesById.ContainsKey(edge.ToNodeId))
            {
                return false;
            }

            var normalizedEdge = new MazeModuleLocalNavigationEdgeRecord(
                edge.FromNodeId,
                edge.ToNodeId,
                Mathf.Max(0.01f, edge.Cost),
                edge.TraversalType,
                edge.Bidirectional);

            m_LocalEdges.Add(normalizedEdge);
            AddLocalAdjacencyEdge(normalizedEdge);
            if (normalizedEdge.Bidirectional && !string.Equals(normalizedEdge.FromNodeId, normalizedEdge.ToNodeId, StringComparison.OrdinalIgnoreCase))
            {
                AddLocalAdjacencyEdge(new MazeModuleLocalNavigationEdgeRecord(
                    normalizedEdge.ToNodeId,
                    normalizedEdge.FromNodeId,
                    normalizedEdge.Cost,
                    normalizedEdge.TraversalType,
                    normalizedEdge.Bidirectional));
            }

            return true;
        }

        public bool TryGetLocalNode(string id, out MazeModuleLocalNavigationNodeRecord node)
        {
            return m_LocalNodesById.TryGetValue(id, out node);
        }

        public bool TryGetNearestLocalNode(Vector3 worldPosition, out MazeModuleLocalNavigationNodeRecord node, bool preferInteriorNodes = true)
        {
            node = null;
            if (m_LocalNodes.Count == 0)
                return false;

            float bestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < m_LocalNodes.Count; i++)
            {
                MazeModuleLocalNavigationNodeRecord candidate = m_LocalNodes[i];
                if (preferInteriorNodes && candidate.IsPortalNode)
                    continue;

                float distanceSqr = GetPlanarDistanceSqr(candidate.WorldPosition, worldPosition);
                if (distanceSqr >= bestDistanceSqr)
                    continue;

                bestDistanceSqr = distanceSqr;
                node = candidate;
            }

            if (node != null || !preferInteriorNodes)
                return node != null;

            return TryGetNearestLocalNode(worldPosition, out node, preferInteriorNodes: false);
        }

        public bool TryFindLocalPath(
            string startNodeId,
            string targetNodeId,
            List<MazeModuleLocalNavigationNodeRecord> pathBuffer,
            out float totalCost)
        {
            totalCost = 0f;
            if (pathBuffer == null)
                return false;

            pathBuffer.Clear();
            if (string.IsNullOrWhiteSpace(startNodeId)
                || string.IsNullOrWhiteSpace(targetNodeId)
                || !m_LocalNodesById.ContainsKey(startNodeId)
                || !m_LocalNodesById.ContainsKey(targetNodeId))
            {
                return false;
            }

            if (string.Equals(startNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase))
            {
                pathBuffer.Add(m_LocalNodesById[startNodeId]);
                return true;
            }

            m_LocalGScore.Clear();
            m_LocalCameFrom.Clear();
            m_LocalClosedSet.Clear();
            m_LocalOpenSet.Clear();
            m_LocalPathIds.Clear();

            m_LocalGScore[startNodeId] = 0f;
            m_LocalCameFrom[startNodeId] = startNodeId;
            m_LocalOpenSet.Add(startNodeId);

            while (m_LocalOpenSet.Count > 0)
            {
                string currentNodeId = ExtractLowestCostOpenNodeId();
                if (currentNodeId == null)
                    break;

                if (string.Equals(currentNodeId, targetNodeId, StringComparison.OrdinalIgnoreCase))
                    break;

                m_LocalClosedSet.Add(currentNodeId);
                if (!m_LocalAdjacencyByNodeId.TryGetValue(currentNodeId, out List<MazeModuleLocalNavigationEdgeRecord> edges))
                    continue;

                float currentCost = m_LocalGScore[currentNodeId];
                for (int i = 0; i < edges.Count; i++)
                {
                    MazeModuleLocalNavigationEdgeRecord edge = edges[i];
                    string neighborNodeId = edge.ToNodeId;
                    if (m_LocalClosedSet.Contains(neighborNodeId))
                        continue;

                    float tentativeCost = currentCost + Mathf.Max(0.01f, edge.Cost);
                    if (m_LocalGScore.TryGetValue(neighborNodeId, out float existingCost) && tentativeCost >= existingCost)
                        continue;

                    m_LocalGScore[neighborNodeId] = tentativeCost;
                    m_LocalCameFrom[neighborNodeId] = currentNodeId;
                    if (!m_LocalOpenSet.Contains(neighborNodeId))
                        m_LocalOpenSet.Add(neighborNodeId);
                }
            }

            if (!m_LocalCameFrom.ContainsKey(targetNodeId))
                return false;

            string stepNodeId = targetNodeId;
            m_LocalPathIds.Add(stepNodeId);
            while (!string.Equals(stepNodeId, startNodeId, StringComparison.OrdinalIgnoreCase))
            {
                stepNodeId = m_LocalCameFrom[stepNodeId];
                m_LocalPathIds.Add(stepNodeId);
            }

            m_LocalPathIds.Reverse();
            for (int i = 0; i < m_LocalPathIds.Count; i++)
            {
                if (m_LocalNodesById.TryGetValue(m_LocalPathIds[i], out MazeModuleLocalNavigationNodeRecord node))
                    pathBuffer.Add(node);
            }

            totalCost = m_LocalGScore.TryGetValue(targetNodeId, out float resolvedCost) ? resolvedCost : 0f;
            return pathBuffer.Count > 0;
        }

        void AddLocalAdjacencyEdge(MazeModuleLocalNavigationEdgeRecord edge)
        {
            if (!m_LocalAdjacencyByNodeId.TryGetValue(edge.FromNodeId, out List<MazeModuleLocalNavigationEdgeRecord> adjacency))
            {
                adjacency = new List<MazeModuleLocalNavigationEdgeRecord>();
                m_LocalAdjacencyByNodeId.Add(edge.FromNodeId, adjacency);
            }

            adjacency.Add(edge);
        }

        string ExtractLowestCostOpenNodeId()
        {
            int bestIndex = -1;
            float bestCost = float.PositiveInfinity;
            for (int i = 0; i < m_LocalOpenSet.Count; i++)
            {
                string nodeId = m_LocalOpenSet[i];
                if (!m_LocalGScore.TryGetValue(nodeId, out float cost) || cost >= bestCost)
                    continue;

                bestCost = cost;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return null;

            string bestNodeId = m_LocalOpenSet[bestIndex];
            m_LocalOpenSet.RemoveAt(bestIndex);
            return bestNodeId;
        }

        static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }
    }
}
