using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Generation
{
    [DisallowMultipleComponent]
    public class MazeModuleNavigationAuthoring : MonoBehaviour
    {
        [SerializeField] string m_ModuleId = "Module";
        [SerializeField] int m_NavigationLayer;
        [SerializeField] MazeNavigationAreaTag m_DefaultAreaTag = MazeNavigationAreaTag.Default;
        [SerializeField] bool m_AutoGenerateCardinalPortals = true;
        [SerializeField, Min(0f)] float m_AutoPortalInset = 0.05f;
        [SerializeField] bool m_AutoGenerateSimpleLocalGraph = true;
        [SerializeField] bool m_UseWalkableBoundsOverride;
        [SerializeField] Vector3 m_WalkableBoundsCenter = Vector3.zero;
        [SerializeField] Vector3 m_WalkableBoundsSize = new(4f, 2f, 4f);
        [SerializeField] List<MazeNavigationPortalDefinition> m_Portals = new();
        [SerializeField] List<MazeNavigationLocalNodeDefinition> m_LocalNodes = new();
        [SerializeField] List<MazeNavigationLocalEdgeDefinition> m_LocalEdges = new();

        public string ModuleId => string.IsNullOrWhiteSpace(m_ModuleId) ? gameObject.name : m_ModuleId;
        public int NavigationLayer => m_NavigationLayer;
        public MazeNavigationAreaTag DefaultAreaTag => m_DefaultAreaTag;
        public IReadOnlyList<MazeNavigationPortalDefinition> Portals => m_Portals;
        public IReadOnlyList<MazeNavigationLocalNodeDefinition> LocalNodes => m_LocalNodes;
        public IReadOnlyList<MazeNavigationLocalEdgeDefinition> LocalEdges => m_LocalEdges;
        public bool UsesWalkableBoundsOverride => m_UseWalkableBoundsOverride;

        public Bounds GetResolvedLocalWalkableBounds(float cellSize)
        {
            if (!m_UseWalkableBoundsOverride)
            {
                return new Bounds(
                    Vector3.zero,
                    new Vector3(
                        Mathf.Max(0.01f, cellSize),
                        Mathf.Max(0.5f, cellSize * 0.5f),
                        Mathf.Max(0.01f, cellSize)));
            }

            Vector3 clampedSize = new(
                Mathf.Max(0.01f, m_WalkableBoundsSize.x),
                Mathf.Max(0.01f, m_WalkableBoundsSize.y),
                Mathf.Max(0.01f, m_WalkableBoundsSize.z));
            return new Bounds(m_WalkableBoundsCenter, clampedSize);
        }

        public void GetResolvedPortals(float cellSize, List<MazeNavigationPortalDefinition> results)
        {
            if (results == null)
                return;

            results.Clear();
            for (int i = 0; i < m_Portals.Count; i++)
            {
                MazeNavigationPortalDefinition portal = m_Portals[i];
                if (string.IsNullOrWhiteSpace(portal.id))
                    portal.id = $"Portal_{i}";

                portal.localForward = ResolvePortalForward(portal.localForward);
                portal.navigationLayer = m_NavigationLayer;
                if (portal.areaTag == default)
                    portal.areaTag = m_DefaultAreaTag;

                results.Add(portal);
            }

            if (!m_AutoGenerateCardinalPortals || results.Count > 0)
                return;

            float halfSpan = Mathf.Max(0.01f, cellSize * 0.5f - m_AutoPortalInset);
            AddAutoPortal(results, "North", new Vector3(0f, 0f, halfSpan), Vector3.forward);
            AddAutoPortal(results, "East", new Vector3(halfSpan, 0f, 0f), Vector3.right);
            AddAutoPortal(results, "South", new Vector3(0f, 0f, -halfSpan), Vector3.back);
            AddAutoPortal(results, "West", new Vector3(-halfSpan, 0f, 0f), Vector3.left);
        }

        public void GetResolvedLocalNodes(float cellSize, List<MazeNavigationLocalNodeDefinition> results)
        {
            if (results == null)
                return;

            results.Clear();
            if (m_LocalNodes.Count == 0 && m_AutoGenerateSimpleLocalGraph)
            {
                Bounds localBounds = GetResolvedLocalWalkableBounds(cellSize);
                results.Add(new MazeNavigationLocalNodeDefinition
                {
                    id = "Center",
                    localPosition = localBounds.center,
                    areaTag = m_DefaultAreaTag,
                });

                var resolvedPortals = new List<MazeNavigationPortalDefinition>();
                GetResolvedPortals(cellSize, resolvedPortals);
                for (int i = 0; i < resolvedPortals.Count; i++)
                {
                    MazeNavigationPortalDefinition portal = resolvedPortals[i];
                    if (string.IsNullOrWhiteSpace(portal.id))
                        continue;
                }

                return;
            }

            for (int i = 0; i < m_LocalNodes.Count; i++)
            {
                MazeNavigationLocalNodeDefinition node = m_LocalNodes[i];
                if (string.IsNullOrWhiteSpace(node.id))
                    node.id = $"Node_{i}";

                if (node.areaTag == default)
                    node.areaTag = m_DefaultAreaTag;

                results.Add(node);
            }
        }

        public void GetResolvedLocalEdges(float cellSize, List<MazeNavigationLocalEdgeDefinition> results)
        {
            if (results == null)
                return;

            results.Clear();
            if (m_LocalEdges.Count == 0 && m_AutoGenerateSimpleLocalGraph)
            {
                var resolvedPortals = new List<MazeNavigationPortalDefinition>();
                GetResolvedPortals(cellSize, resolvedPortals);
                for (int i = 0; i < resolvedPortals.Count; i++)
                {
                    MazeNavigationPortalDefinition portal = resolvedPortals[i];
                    if (string.IsNullOrWhiteSpace(portal.id))
                        continue;

                    results.Add(new MazeNavigationLocalEdgeDefinition
                    {
                        fromId = "Center",
                        toId = portal.id,
                        traversalType = portal.traversalType,
                        bidirectional = portal.bidirectional,
                        costOverride = 0f,
                    });
                }

                return;
            }

            for (int i = 0; i < m_LocalEdges.Count; i++)
            {
                MazeNavigationLocalEdgeDefinition edge = m_LocalEdges[i];
                if (string.IsNullOrWhiteSpace(edge.fromId) || string.IsNullOrWhiteSpace(edge.toId))
                    continue;

                if (edge.costOverride < 0f)
                    edge.costOverride = 0f;

                results.Add(edge);
            }
        }

        void OnValidate()
        {
            m_AutoPortalInset = Mathf.Max(0f, m_AutoPortalInset);
            m_WalkableBoundsSize = new Vector3(
                Mathf.Max(0.01f, m_WalkableBoundsSize.x),
                Mathf.Max(0.01f, m_WalkableBoundsSize.y),
                Mathf.Max(0.01f, m_WalkableBoundsSize.z));
            for (int i = 0; i < m_Portals.Count; i++)
            {
                MazeNavigationPortalDefinition portal = m_Portals[i];
                portal.localForward = ResolvePortalForward(portal.localForward);
                portal.navigationLayer = m_NavigationLayer;
                m_Portals[i] = portal;
            }

            for (int i = 0; i < m_LocalNodes.Count; i++)
            {
                MazeNavigationLocalNodeDefinition node = m_LocalNodes[i];
                if (string.IsNullOrWhiteSpace(node.id))
                    node.id = $"Node_{i}";

                if (node.areaTag == default)
                    node.areaTag = m_DefaultAreaTag;

                m_LocalNodes[i] = node;
            }

            for (int i = 0; i < m_LocalEdges.Count; i++)
            {
                MazeNavigationLocalEdgeDefinition edge = m_LocalEdges[i];
                if (edge.costOverride < 0f)
                    edge.costOverride = 0f;

                m_LocalEdges[i] = edge;
            }
        }

        void AddAutoPortal(List<MazeNavigationPortalDefinition> results, string id, Vector3 localPosition, Vector3 localForward)
        {
            results.Add(new MazeNavigationPortalDefinition
            {
                id = id,
                localPosition = localPosition,
                localForward = localForward,
                areaTag = m_DefaultAreaTag,
                traversalType = MazeTraversalLinkType.Walk,
                bidirectional = true,
                navigationLayer = m_NavigationLayer,
            });
        }

        static Vector3 ResolvePortalForward(Vector3 rawForward)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(rawForward, Vector3.up);
            if (planarForward.sqrMagnitude < 0.0001f)
                return Vector3.forward;

            return planarForward.normalized;
        }
    }
}
