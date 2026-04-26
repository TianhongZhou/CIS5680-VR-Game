using System.Collections.Generic;
using CIS5680VRGame.Generation;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    internal static class EnemyPathPlanner
    {
        internal enum NavigationPathBuildFailure
        {
            None,
            NoGraphPath,
            EmptyAfterTrimming,
        }

        internal static bool TryBuildNavigationWaypointPath(
            MazeNavigationGraph navigationGraph,
            Vector3 startWorldPosition,
            Vector3 targetWorldPosition,
            float groundY,
            Vector3 currentWorldPosition,
            Vector3 currentForward,
            float arrivalDistance,
            List<int> nodePath,
            List<Vector3> waypointPath,
            out NavigationPathBuildFailure failure)
        {
            failure = NavigationPathBuildFailure.None;
            nodePath.Clear();
            waypointPath.Clear();

            if (navigationGraph == null || !navigationGraph.TryFindPath(startWorldPosition, targetWorldPosition, nodePath))
            {
                failure = NavigationPathBuildFailure.NoGraphPath;
                return false;
            }

            navigationGraph.GetPathWorldPositions(nodePath, waypointPath);
            TrimConsumedWaypoints(waypointPath, currentWorldPosition, arrivalDistance);
            TrimPassedLeadWaypoints(waypointPath, currentWorldPosition, currentForward, groundY);
            RefreshTerminalWaypoint(waypointPath, targetWorldPosition, groundY);
            if (waypointPath.Count == 0)
            {
                failure = NavigationPathBuildFailure.EmptyAfterTrimming;
                return false;
            }

            return true;
        }

        internal static bool TryResolveNavigationModuleAtWorldPosition(
            MazeGridNavigator gridNavigator,
            MazeNavigationGraph navigationGraph,
            bool canUseGridNavigation,
            bool canUseNavigationGraph,
            Vector3 worldPosition,
            out MazeNavigationModuleRecord module)
        {
            module = null;
            if (!canUseNavigationGraph || navigationGraph == null)
                return false;

            if (canUseGridNavigation
                && gridNavigator != null
                && gridNavigator.TryGetCellAtWorldPosition(worldPosition, out MazeCellData cell)
                && navigationGraph.TryGetModule(cell.GridPosition, out module))
            {
                return true;
            }

            return navigationGraph.TryGetNearestModule(worldPosition, out module);
        }

        internal static bool TryGetNavigationModuleDestination(
            MazeNavigationGraph navigationGraph,
            MazeNavigationModuleRecord module,
            Vector3 fallbackPosition,
            float groundY,
            out Vector3 destination)
        {
            destination = fallbackPosition;
            if (module == null)
                return false;

            destination = module.WorldCenter;
            if (navigationGraph != null && navigationGraph.TryProjectLocalPoint(module.GridPosition, destination, out Vector3 projectedDestination))
                destination = projectedDestination;

            destination.y = groundY;
            return true;
        }

        internal static void TrimConsumedWaypoints(List<Vector3> waypoints, Vector3 currentWorldPosition, float arrivalDistance)
        {
            if (waypoints == null)
                return;

            float distanceThreshold = Mathf.Max(0f, arrivalDistance);
            float distanceThresholdSqr = distanceThreshold * distanceThreshold;
            while (waypoints.Count > 0)
            {
                Vector3 delta = Vector3.ProjectOnPlane(waypoints[0] - currentWorldPosition, Vector3.up);
                if (delta.sqrMagnitude > distanceThresholdSqr)
                    break;

                waypoints.RemoveAt(0);
            }
        }

        internal static void TrimPassedLeadWaypoints(
            List<Vector3> waypoints,
            Vector3 currentWorldPosition,
            Vector3 currentForward,
            float groundY)
        {
            if (waypoints == null || waypoints.Count <= 1)
                return;

            Vector3 currentPosition = new(currentWorldPosition.x, groundY, currentWorldPosition.z);
            Vector3 planarForward = Vector3.ProjectOnPlane(currentForward, Vector3.up);
            if (planarForward.sqrMagnitude < 0.0001f)
                return;

            planarForward.Normalize();
            while (waypoints.Count > 1)
            {
                Vector3 toLead = Vector3.ProjectOnPlane(waypoints[0] - currentPosition, Vector3.up);
                if (toLead.sqrMagnitude <= 0.0001f || Vector3.Dot(toLead.normalized, planarForward) < -0.15f)
                {
                    waypoints.RemoveAt(0);
                    continue;
                }

                break;
            }
        }

        internal static void RefreshTerminalWaypoint(List<Vector3> waypoints, Vector3 targetWorldPosition, float groundY)
        {
            if (waypoints == null || waypoints.Count == 0)
                return;

            Vector3 targetPosition = new(targetWorldPosition.x, groundY, targetWorldPosition.z);
            waypoints[waypoints.Count - 1] = targetPosition;
        }

        internal static void TrimNonAdvancingChaseLeadWaypoints(
            List<Vector3> waypoints,
            Vector3 currentWorldPosition,
            Vector3 targetWorldPosition,
            float groundY)
        {
            if (waypoints == null || waypoints.Count <= 1)
                return;

            Vector3 currentPosition = new(currentWorldPosition.x, groundY, currentWorldPosition.z);
            Vector3 targetPosition = new(targetWorldPosition.x, groundY, targetWorldPosition.z);
            Vector3 currentToTarget = Vector3.ProjectOnPlane(targetPosition - currentPosition, Vector3.up);
            float currentDistanceSqr = currentToTarget.sqrMagnitude;
            if (currentDistanceSqr <= 0.0001f)
                return;

            Vector3 currentToTargetDirection = currentToTarget.normalized;
            const float distanceEpsilon = 0.01f;

            while (waypoints.Count > 1)
            {
                Vector3 leadWaypoint = new(waypoints[0].x, groundY, waypoints[0].z);
                Vector3 currentToLead = Vector3.ProjectOnPlane(leadWaypoint - currentPosition, Vector3.up);
                if (currentToLead.sqrMagnitude <= 0.0001f)
                {
                    waypoints.RemoveAt(0);
                    continue;
                }

                float leadDistanceSqr = Vector3.ProjectOnPlane(targetPosition - leadWaypoint, Vector3.up).sqrMagnitude;
                float directionDot = Vector3.Dot(currentToLead.normalized, currentToTargetDirection);
                bool isBehind = directionDot <= 0f;
                bool isNonAdvancing = leadDistanceSqr >= currentDistanceSqr - distanceEpsilon;
                if (!isBehind && !isNonAdvancing)
                    break;

                waypoints.RemoveAt(0);
            }
        }

        internal static bool TryAlignGridPathToCurrentCell(List<Vector2Int> gridPath, Vector2Int currentCell)
        {
            if (gridPath == null || gridPath.Count == 0)
                return false;

            int currentIndex = -1;
            for (int i = 0; i < gridPath.Count; i++)
            {
                if (gridPath[i] != currentCell)
                    continue;

                currentIndex = i;
                break;
            }

            if (currentIndex < 0)
                return false;

            if (currentIndex > 0)
                gridPath.RemoveRange(0, currentIndex);

            return true;
        }

        internal static bool HasDirectLocalNavigationPath(
            Vector3 origin,
            Vector3 targetWorldPosition,
            float probeRadius,
            LayerMask obstacleMask,
            RaycastHit[] hits,
            Transform ownerTransform,
            Transform ignoreRoot)
        {
            Vector3 target = new(targetWorldPosition.x, origin.y, targetWorldPosition.z);
            Vector3 planarDirection = target - origin;
            float distance = planarDirection.magnitude;
            if (distance <= 0.05f)
                return true;

            planarDirection /= distance;
            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                Mathf.Max(0.05f, probeRadius),
                planarDirection,
                hits,
                distance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.isTrigger)
                    continue;

                if (EnemyMovementMotor.IsOwnCollider(hitCollider, ownerTransform))
                    continue;

                Transform hitRoot = hitCollider.transform.root;
                if (ignoreRoot != null && hitRoot == ignoreRoot)
                    continue;

                return false;
            }

            return true;
        }
    }
}
