using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        bool TryEnsureNavigationWaypointPath(EnemyMovementRequest request, Vector2Int targetCell, bool hasTargetCell)
        {
            bool targetChanged = hasTargetCell
                ? !m_HasCurrentNavigationGoalCell || m_CurrentNavigationGoalCell != targetCell
                : m_NavigationWaypointPath.Count == 0;

            if (targetChanged || m_NavigationWaypointPath.Count == 0)
                return TryRebuildNavigationPath(m_Rigidbody.position, request.TargetPosition, targetCell, hasTargetCell);

            TrimConsumedNavigationWaypoints();
            if (m_NavigationWaypointPath.Count == 0)
                return TryRebuildNavigationPath(m_Rigidbody.position, request.TargetPosition, targetCell, hasTargetCell);

            return true;
        }

        bool TryRebuildNavigationPath(Vector3 startWorldPosition, Vector3 targetWorldPosition, Vector2Int targetCell, bool hasTargetCell)
        {
            m_NavigationNodePath.Clear();
            m_NavigationWaypointPath.Clear();

            if (hasTargetCell
                && CanUseGridNavigation
                && m_GridNavigator.TryGetCellAtWorldPosition(startWorldPosition, out MazeCellData startCell))
            {
                if (ShouldSkipFailedNavigationPathRetry(startCell.GridPosition, targetCell, targetWorldPosition))
                    return false;

                if (!TryGetCachedGridReachability(startCell.GridPosition, targetCell, targetWorldPosition))
                {
                    RecordNavigationDebugFailure(
                        $"Navigation graph skipped: grid target unreachable {FormatDebugCell(startCell.GridPosition)} -> {FormatDebugCell(targetCell)}.",
                        targetWorldPosition);
                    RecordNavigationPathFailure(startCell.GridPosition, targetCell);
                    return false;
                }
            }

            m_DebugPathQueryCount++;
            if (!m_NavigationGraph.TryFindPath(startWorldPosition, targetWorldPosition, m_NavigationNodePath))
            {
                if (hasTargetCell && CanUseGridNavigation && m_GridNavigator.TryGetCellAtWorldPosition(startWorldPosition, out MazeCellData failedStartCell))
                    RecordNavigationPathFailure(failedStartCell.GridPosition, targetCell);

                RecordNavigationDebugFailure("Navigation graph failed to find a node path.", targetWorldPosition);
                return false;
            }

            m_NavigationGraph.GetPathWorldPositions(m_NavigationNodePath, m_NavigationWaypointPath);
            TrimConsumedNavigationWaypoints(m_NavigationWaypointPath);
            TrimPassedLeadNavigationWaypoints(m_NavigationWaypointPath);
            RefreshNavigationTerminalWaypoint(m_NavigationWaypointPath, targetWorldPosition);
            if (m_NavigationWaypointPath.Count == 0)
            {
                RecordNavigationDebugFailure("Navigation graph produced an empty waypoint path after trimming.", targetWorldPosition);
                return false;
            }

            m_CurrentNavigationGoalCell = targetCell;
            m_HasCurrentNavigationGoalCell = hasTargetCell;
            if (hasTargetCell && CanUseGridNavigation && m_GridNavigator.TryGetCellAtWorldPosition(startWorldPosition, out MazeCellData clearStartCell))
                ClearNavigationPathFailure(clearStartCell.GridPosition, targetCell);

            return true;
        }

        bool TryUpdateNavigationGraphPatrol(EnemyMovementRequest request)
        {
            if (!CanUseNavigationGraph || !TryResolveNavigationModuleAtWorldPosition(m_Rigidbody.position, out MazeNavigationModuleRecord currentModule))
                return false;

            if (!m_HasCurrentPatrolTargetCell)
            {
                if (!TrySelectNextPatrolNavigationTarget(currentModule, out Vector2Int initialPatrolTarget))
                    return false;

                m_LastPatrolCell = currentModule.GridPosition;
                m_HasLastPatrolCell = true;
                m_CurrentPatrolTargetCell = initialPatrolTarget;
                m_HasCurrentPatrolTargetCell = true;
                ClearNavigationPathState();
            }

            if (!m_NavigationGraph.TryGetModule(m_CurrentPatrolTargetCell, out MazeNavigationModuleRecord targetModule)
                || !TryGetNavigationModuleDestination(targetModule, out Vector3 patrolDestination))
            {
                m_HasCurrentPatrolTargetCell = false;
                ClearNavigationPathState();
                return false;
            }

            if (currentModule.GridPosition == targetModule.GridPosition && IsNearWorldPosition(patrolDestination))
            {
                if (!TrySelectNextPatrolNavigationTarget(currentModule, out Vector2Int nextPatrolTarget))
                    return false;

                m_LastPatrolCell = currentModule.GridPosition;
                m_HasLastPatrolCell = true;
                m_CurrentPatrolTargetCell = nextPatrolTarget;
                m_HasCurrentPatrolTargetCell = true;
                ClearNavigationPathState();

                if (!m_NavigationGraph.TryGetModule(m_CurrentPatrolTargetCell, out targetModule)
                    || !TryGetNavigationModuleDestination(targetModule, out patrolDestination))
                {
                    return false;
                }
            }

            EnemyMovementRequest patrolRequest = request.WithTarget(patrolDestination);
            if (TryMoveAlongLocalNavigationRequest(patrolRequest, currentModule.GridPosition, targetModule.GridPosition, EnemyNavigationDebugMode.LocalPatrol, "Patrol using close-range local navigation."))
                return true;

            if (!TryEnsureNavigationWaypointPath(patrolRequest, targetModule.GridPosition, hasTargetCell: true))
                return false;

            bool moved = MoveAlongNavigationWaypoints(m_NavigationWaypointPath, patrolRequest.MoveSpeed, patrolDestination);
            if (moved)
                RecordNavigationDebugMode(EnemyNavigationDebugMode.NavigationGraphPatrol, patrolDestination, $"Navigation graph patrol target {FormatDebugCell(m_CurrentPatrolTargetCell)}.");

            return moved;
        }

        bool TryResolveNavigationModuleAtWorldPosition(Vector3 worldPosition, out MazeNavigationModuleRecord module)
        {
            module = null;
            if (!CanUseNavigationGraph)
                return false;

            if (CanUseGridNavigation
                && m_GridNavigator.TryGetCellAtWorldPosition(worldPosition, out MazeCellData cell)
                && m_NavigationGraph.TryGetModule(cell.GridPosition, out module))
            {
                return true;
            }

            return m_NavigationGraph.TryGetNearestModule(worldPosition, out module);
        }

        bool TryGetNavigationModuleDestination(MazeNavigationModuleRecord module, out Vector3 destination)
        {
            destination = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            if (module == null)
                return false;

            destination = module.WorldCenter;
            if (m_NavigationGraph.TryProjectLocalPoint(module.GridPosition, destination, out Vector3 projectedDestination))
                destination = projectedDestination;

            destination.y = m_SpawnPosition.y;
            return true;
        }

        void TrimConsumedNavigationWaypoints()
        {
            TrimConsumedNavigationWaypoints(m_NavigationWaypointPath);
        }

        void TrimConsumedNavigationWaypoints(List<Vector3> waypoints)
        {
            while (waypoints.Count > 0 && IsNearWorldPosition(waypoints[0]))
                waypoints.RemoveAt(0);
        }

        void TrimPassedLeadNavigationWaypoints(List<Vector3> waypoints)
        {
            if (waypoints == null || waypoints.Count <= 1)
                return;

            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            currentPosition.y = m_SpawnPosition.y;
            Vector3 currentForward = Vector3.ProjectOnPlane(m_CurrentMoveDirection.sqrMagnitude > 0.0001f ? m_CurrentMoveDirection : transform.forward, Vector3.up);
            if (currentForward.sqrMagnitude < 0.0001f)
                return;

            currentForward.Normalize();
            while (waypoints.Count > 1)
            {
                Vector3 toLead = Vector3.ProjectOnPlane(waypoints[0] - currentPosition, Vector3.up);
                if (toLead.sqrMagnitude <= 0.0001f || Vector3.Dot(toLead.normalized, currentForward) < -0.15f)
                {
                    waypoints.RemoveAt(0);
                    continue;
                }

                break;
            }
        }

        void RefreshNavigationTerminalWaypoint(List<Vector3> waypoints, Vector3 targetWorldPosition)
        {
            if (waypoints == null || waypoints.Count == 0)
                return;

            Vector3 targetPosition = new(targetWorldPosition.x, m_SpawnPosition.y, targetWorldPosition.z);
            waypoints[waypoints.Count - 1] = targetPosition;
        }

        void TrimNonAdvancingChaseLeadWaypoints(List<Vector3> waypoints, Vector3 currentWorldPosition, Vector3 targetWorldPosition)
        {
            if (waypoints == null || waypoints.Count <= 1)
                return;

            Vector3 currentPosition = new(currentWorldPosition.x, m_SpawnPosition.y, currentWorldPosition.z);
            Vector3 targetPosition = new(targetWorldPosition.x, m_SpawnPosition.y, targetWorldPosition.z);
            Vector3 currentToTarget = Vector3.ProjectOnPlane(targetPosition - currentPosition, Vector3.up);
            float currentDistanceSqr = currentToTarget.sqrMagnitude;
            if (currentDistanceSqr <= 0.0001f)
                return;

            Vector3 currentToTargetDirection = currentToTarget.normalized;
            const float distanceEpsilon = 0.01f;

            while (waypoints.Count > 1)
            {
                Vector3 leadWaypoint = new(waypoints[0].x, m_SpawnPosition.y, waypoints[0].z);
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

        bool MoveAlongNavigationWaypoints(List<Vector3> waypoints, float moveSpeed, Vector3 lookTarget, bool prioritizeFacingLookTarget = false)
        {
            if (waypoints == null)
                return false;

            TrimConsumedNavigationWaypoints(waypoints);
            TrimPassedLeadNavigationWaypoints(waypoints);
            if (waypoints.Count == 0)
                return false;

            MoveTowardsNavigationPoint(waypoints[0], moveSpeed, lookTarget, prioritizeFacingLookTarget);
            return true;
        }

        void MoveTowardsNavigationPoint(Vector3 waypoint, float moveSpeed, Vector3 lookTarget, bool prioritizeFacingLookTarget = false)
        {
            Vector3 currentPosition = m_Rigidbody.position;
            Vector3 targetPosition = new(waypoint.x, m_SpawnPosition.y, waypoint.z);
            Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, Mathf.Max(0f, moveSpeed) * Time.fixedDeltaTime);

            Vector3 planarLookDirection = prioritizeFacingLookTarget
                ? Vector3.ProjectOnPlane(lookTarget - currentPosition, Vector3.up)
                : Vector3.ProjectOnPlane(targetPosition - currentPosition, Vector3.up);
            if (planarLookDirection.sqrMagnitude < 0.0001f)
                planarLookDirection = Vector3.ProjectOnPlane(targetPosition - currentPosition, Vector3.up);

            if (planarLookDirection.sqrMagnitude < 0.0001f)
                planarLookDirection = m_CurrentMoveDirection.sqrMagnitude > 0.0001f ? m_CurrentMoveDirection : transform.forward;
            else
                planarLookDirection.Normalize();

            m_CurrentMoveDirection = planarLookDirection;
            MoveRigidBody(nextPosition, planarLookDirection, prioritizeFacingLookDirection: prioritizeFacingLookTarget);
        }
    }
}
