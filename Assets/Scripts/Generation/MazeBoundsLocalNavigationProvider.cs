using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Generation
{
    [DisallowMultipleComponent]
    public class MazeBoundsLocalNavigationProvider : MazeLocalNavigationProvider
    {
        [SerializeField, Min(0f)] float m_VerticalPadding = 0.25f;
        [SerializeField] bool m_UseMergedBoundsAcrossAdjacentModules = true;
        readonly List<MazeModuleLocalNavigationNodeRecord> m_LocalPathBuffer = new();
        readonly List<MazeModuleLocalNavigationNodeRecord> m_FromLocalPathBuffer = new();
        readonly List<MazeModuleLocalNavigationNodeRecord> m_ToLocalPathBuffer = new();
        readonly List<MazeModuleLocalNavigationNodeRecord> m_FromPortalBuffer = new();
        readonly List<MazeModuleLocalNavigationNodeRecord> m_ToPortalBuffer = new();

        public override bool TryProjectPoint(
            MazeNavigationModuleRecord module,
            Vector3 desiredWorldPosition,
            out Vector3 projectedWorldPosition)
        {
            projectedWorldPosition = desiredWorldPosition;
            if (module == null)
                return false;

            projectedWorldPosition = ClampToBounds(module.WalkableBounds, desiredWorldPosition, m_VerticalPadding);
            return true;
        }

        public override bool TryBuildPath(
            MazeNavigationModuleRecord module,
            Vector3 startWorldPosition,
            Vector3 desiredWorldPosition,
            List<Vector3> waypointBuffer)
        {
            if (waypointBuffer == null)
                return false;

            waypointBuffer.Clear();
            if (!TryProjectPoint(module, desiredWorldPosition, out Vector3 projectedTarget))
                return false;

            Vector3 projectedStart = ClampToBounds(module.WalkableBounds, startWorldPosition, m_VerticalPadding);
            if (TryBuildLocalGraphPath(module, startWorldPosition, projectedStart, projectedTarget, waypointBuffer))
                return true;

            if ((projectedStart - projectedTarget).sqrMagnitude > 0.0004f)
                waypointBuffer.Add(projectedStart);

            waypointBuffer.Add(projectedTarget);
            return true;
        }

        public override bool TryBuildPath(
            MazeNavigationModuleRecord fromModule,
            MazeNavigationModuleRecord toModule,
            Vector3 startWorldPosition,
            Vector3 desiredWorldPosition,
            List<Vector3> waypointBuffer)
        {
            if (waypointBuffer == null)
                return false;

            waypointBuffer.Clear();
            if (fromModule == null && toModule == null)
                return false;

            if (toModule == null || fromModule == null || fromModule == toModule || !m_UseMergedBoundsAcrossAdjacentModules)
                return base.TryBuildPath(fromModule, toModule, startWorldPosition, desiredWorldPosition, waypointBuffer);

            Vector3 projectedStart = ClampToBounds(fromModule.WalkableBounds, startWorldPosition, m_VerticalPadding);
            Vector3 projectedTarget = ClampToBounds(toModule.WalkableBounds, desiredWorldPosition, m_VerticalPadding);
            if (TryBuildCrossModuleLocalGraphPath(fromModule, toModule, startWorldPosition, projectedStart, projectedTarget, waypointBuffer))
                return true;

            Bounds mergedBounds = fromModule.WalkableBounds;
            mergedBounds.Encapsulate(toModule.WalkableBounds.min);
            mergedBounds.Encapsulate(toModule.WalkableBounds.max);

            projectedStart = ClampToBounds(mergedBounds, startWorldPosition, m_VerticalPadding);
            projectedTarget = ClampToBounds(mergedBounds, desiredWorldPosition, m_VerticalPadding);
            if ((projectedStart - projectedTarget).sqrMagnitude > 0.0004f)
                waypointBuffer.Add(projectedStart);

            waypointBuffer.Add(projectedTarget);
            return true;
        }

        bool TryBuildLocalGraphPath(
            MazeNavigationModuleRecord module,
            Vector3 actualStart,
            Vector3 projectedStart,
            Vector3 projectedTarget,
            List<Vector3> waypointBuffer)
        {
            if (module == null || !module.HasLocalNavigationGraph)
                return false;

            if (!module.TryGetNearestLocalNode(projectedStart, out MazeModuleLocalNavigationNodeRecord startNode, preferInteriorNodes: false)
                || !module.TryGetNearestLocalNode(projectedTarget, out MazeModuleLocalNavigationNodeRecord targetNode))
            {
                return false;
            }

            if (!module.TryFindLocalPath(startNode.Id, targetNode.Id, m_LocalPathBuffer, out _)
                || m_LocalPathBuffer.Count == 0)
            {
                return false;
            }

            waypointBuffer.Clear();
            int firstPathIndex = ResolveFirstUsableLocalPathIndex(projectedStart, m_LocalPathBuffer);
            if (ShouldAddProjectedStartWaypoint(actualStart, projectedStart, m_LocalPathBuffer[firstPathIndex].WorldPosition))
                waypointBuffer.Add(projectedStart);

            for (int i = firstPathIndex; i < m_LocalPathBuffer.Count; i++)
            {
                Vector3 worldPosition = m_LocalPathBuffer[i].WorldPosition;
                if (waypointBuffer.Count > 0 && (waypointBuffer[waypointBuffer.Count - 1] - worldPosition).sqrMagnitude <= 0.0004f)
                    continue;

                waypointBuffer.Add(worldPosition);
            }

            if (waypointBuffer.Count == 0 || (waypointBuffer[waypointBuffer.Count - 1] - projectedTarget).sqrMagnitude > 0.0004f)
                waypointBuffer.Add(projectedTarget);

            return waypointBuffer.Count > 0;
        }

        bool TryBuildCrossModuleLocalGraphPath(
            MazeNavigationModuleRecord fromModule,
            MazeNavigationModuleRecord toModule,
            Vector3 actualStart,
            Vector3 projectedStart,
            Vector3 projectedTarget,
            List<Vector3> waypointBuffer)
        {
            if (fromModule == null || toModule == null || !fromModule.HasLocalNavigationGraph || !toModule.HasLocalNavigationGraph)
                return false;

            if (!fromModule.TryGetNearestLocalNode(projectedStart, out MazeModuleLocalNavigationNodeRecord startNode, preferInteriorNodes: false)
                || !toModule.TryGetNearestLocalNode(projectedTarget, out MazeModuleLocalNavigationNodeRecord targetNode))
            {
                return false;
            }

            CollectPortalNodes(fromModule, m_FromPortalBuffer);
            CollectPortalNodes(toModule, m_ToPortalBuffer);
            if (m_FromPortalBuffer.Count == 0 || m_ToPortalBuffer.Count == 0)
                return false;

            MazeModuleLocalNavigationNodeRecord bestFromPortal = null;
            MazeModuleLocalNavigationNodeRecord bestToPortal = null;
            float bestPairDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < m_FromPortalBuffer.Count; i++)
            {
                MazeModuleLocalNavigationNodeRecord fromPortal = m_FromPortalBuffer[i];
                for (int j = 0; j < m_ToPortalBuffer.Count; j++)
                {
                    MazeModuleLocalNavigationNodeRecord toPortal = m_ToPortalBuffer[j];
                    float pairDistanceSqr = GetPlanarDistanceSqr(fromPortal.WorldPosition, toPortal.WorldPosition);
                    if (pairDistanceSqr >= bestPairDistanceSqr)
                        continue;

                    bestPairDistanceSqr = pairDistanceSqr;
                    bestFromPortal = fromPortal;
                    bestToPortal = toPortal;
                }
            }

            if (bestFromPortal == null
                || bestToPortal == null
                || bestPairDistanceSqr > 0.25f)
            {
                return false;
            }

            if (!fromModule.TryFindLocalPath(startNode.Id, bestFromPortal.Id, m_FromLocalPathBuffer, out _)
                || !toModule.TryFindLocalPath(bestToPortal.Id, targetNode.Id, m_ToLocalPathBuffer, out _))
            {
                return false;
            }

            waypointBuffer.Clear();
            int firstFromPathIndex = ResolveFirstUsableLocalPathIndex(projectedStart, m_FromLocalPathBuffer);
            if (ShouldAddProjectedStartWaypoint(actualStart, projectedStart, m_FromLocalPathBuffer[firstFromPathIndex].WorldPosition))
                waypointBuffer.Add(projectedStart);

            AppendUniqueWaypoints(waypointBuffer, m_FromLocalPathBuffer, firstFromPathIndex);
            AppendUniqueWaypoint(waypointBuffer, bestToPortal.WorldPosition);
            AppendUniqueWaypoints(waypointBuffer, m_ToLocalPathBuffer);

            if (waypointBuffer.Count == 0 || (waypointBuffer[waypointBuffer.Count - 1] - projectedTarget).sqrMagnitude > 0.0004f)
                waypointBuffer.Add(projectedTarget);

            return waypointBuffer.Count > 0;
        }

        static int ResolveFirstUsableLocalPathIndex(Vector3 projectedStart, List<MazeModuleLocalNavigationNodeRecord> localPath)
        {
            if (localPath == null || localPath.Count < 2)
                return 0;

            Vector3 firstToSecond = ProjectPlanar(localPath[1].WorldPosition - localPath[0].WorldPosition);
            float segmentLengthSqr = firstToSecond.sqrMagnitude;
            if (segmentLengthSqr <= 0.0004f)
                return 0;

            Vector3 firstToStart = ProjectPlanar(projectedStart - localPath[0].WorldPosition);
            float progressAlongSegment = Vector3.Dot(firstToStart, firstToSecond) / segmentLengthSqr;
            return progressAlongSegment > 0.05f ? 1 : 0;
        }

        static bool ShouldAddProjectedStartWaypoint(Vector3 actualStart, Vector3 projectedStart, Vector3 nextWaypoint)
        {
            Vector3 actualToProjected = ProjectPlanar(projectedStart - actualStart);
            if (actualToProjected.sqrMagnitude <= 0.0004f)
                return false;

            Vector3 actualToNext = ProjectPlanar(nextWaypoint - actualStart);
            if (actualToNext.sqrMagnitude <= 0.0004f)
                return true;

            return Vector3.Dot(actualToProjected.normalized, actualToNext.normalized) > 0.05f;
        }

        static void CollectPortalNodes(MazeNavigationModuleRecord module, List<MazeModuleLocalNavigationNodeRecord> portalBuffer)
        {
            portalBuffer.Clear();
            if (module == null)
                return;

            IReadOnlyList<MazeModuleLocalNavigationNodeRecord> localNodes = module.LocalNodes;
            for (int i = 0; i < localNodes.Count; i++)
            {
                MazeModuleLocalNavigationNodeRecord localNode = localNodes[i];
                if (localNode.IsPortalNode)
                    portalBuffer.Add(localNode);
            }
        }

        static void AppendUniqueWaypoints(List<Vector3> waypointBuffer, List<MazeModuleLocalNavigationNodeRecord> localNodes, int startIndex = 0)
        {
            if (waypointBuffer == null || localNodes == null)
                return;

            for (int i = Mathf.Clamp(startIndex, 0, localNodes.Count); i < localNodes.Count; i++)
                AppendUniqueWaypoint(waypointBuffer, localNodes[i].WorldPosition);
        }

        static void AppendUniqueWaypoint(List<Vector3> waypointBuffer, Vector3 waypoint)
        {
            if (waypointBuffer.Count > 0 && (waypointBuffer[waypointBuffer.Count - 1] - waypoint).sqrMagnitude <= 0.0004f)
                return;

            waypointBuffer.Add(waypoint);
        }

        static float GetPlanarDistanceSqr(Vector3 from, Vector3 to)
        {
            float deltaX = from.x - to.x;
            float deltaZ = from.z - to.z;
            return deltaX * deltaX + deltaZ * deltaZ;
        }

        static Vector3 ProjectPlanar(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }

        static Vector3 ClampToBounds(Bounds bounds, Vector3 position, float verticalPadding)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            min.y -= Mathf.Max(0f, verticalPadding);
            max.y += Mathf.Max(0f, verticalPadding);

            return new Vector3(
                Mathf.Clamp(position.x, min.x, max.x),
                Mathf.Clamp(position.y, min.y, max.y),
                Mathf.Clamp(position.z, min.z, max.z));
        }
    }
}
