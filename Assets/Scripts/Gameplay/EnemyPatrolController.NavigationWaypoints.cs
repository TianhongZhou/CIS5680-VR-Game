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
            Vector3 currentForward = m_CurrentMoveDirection.sqrMagnitude > 0.0001f ? m_CurrentMoveDirection : transform.forward;
            if (!EnemyPathPlanner.TryBuildNavigationWaypointPath(
                    m_NavigationGraph,
                    startWorldPosition,
                    targetWorldPosition,
                    m_SpawnPosition.y,
                    m_Rigidbody != null ? m_Rigidbody.position : transform.position,
                    currentForward,
                    m_PathNodeArrivalDistance,
                    m_NavigationNodePath,
                    m_NavigationWaypointPath,
                    out EnemyPathPlanner.NavigationPathBuildFailure failure))
            {
                if (failure == EnemyPathPlanner.NavigationPathBuildFailure.NoGraphPath
                    && hasTargetCell
                    && CanUseGridNavigation
                    && m_GridNavigator.TryGetCellAtWorldPosition(startWorldPosition, out MazeCellData failedStartCell))
                {
                    RecordNavigationPathFailure(failedStartCell.GridPosition, targetCell);
                }

                string failureMessage = failure == EnemyPathPlanner.NavigationPathBuildFailure.EmptyAfterTrimming
                    ? "Navigation graph produced an empty waypoint path after trimming."
                    : "Navigation graph failed to find a node path.";
                RecordNavigationDebugFailure(failureMessage, targetWorldPosition);
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
            return EnemyPathPlanner.TryResolveNavigationModuleAtWorldPosition(
                m_GridNavigator,
                m_NavigationGraph,
                CanUseGridNavigation,
                CanUseNavigationGraph,
                worldPosition,
                out module);
        }

        bool TryGetNavigationModuleDestination(MazeNavigationModuleRecord module, out Vector3 destination)
        {
            return EnemyPathPlanner.TryGetNavigationModuleDestination(
                m_NavigationGraph,
                module,
                m_Rigidbody != null ? m_Rigidbody.position : transform.position,
                m_SpawnPosition.y,
                out destination);
        }

        void TrimConsumedNavigationWaypoints()
        {
            TrimConsumedNavigationWaypoints(m_NavigationWaypointPath);
        }

        void TrimConsumedNavigationWaypoints(List<Vector3> waypoints)
        {
            EnemyPathPlanner.TrimConsumedWaypoints(
                waypoints,
                m_Rigidbody != null ? m_Rigidbody.position : transform.position,
                m_PathNodeArrivalDistance);
        }

        void TrimPassedLeadNavigationWaypoints(List<Vector3> waypoints)
        {
            Vector3 currentForward = Vector3.ProjectOnPlane(m_CurrentMoveDirection.sqrMagnitude > 0.0001f ? m_CurrentMoveDirection : transform.forward, Vector3.up);
            EnemyPathPlanner.TrimPassedLeadWaypoints(
                waypoints,
                m_Rigidbody != null ? m_Rigidbody.position : transform.position,
                currentForward,
                m_SpawnPosition.y);
        }

        void RefreshNavigationTerminalWaypoint(List<Vector3> waypoints, Vector3 targetWorldPosition)
        {
            EnemyPathPlanner.RefreshTerminalWaypoint(waypoints, targetWorldPosition, m_SpawnPosition.y);
        }

        void TrimNonAdvancingChaseLeadWaypoints(List<Vector3> waypoints, Vector3 currentWorldPosition, Vector3 targetWorldPosition)
        {
            EnemyPathPlanner.TrimNonAdvancingChaseLeadWaypoints(
                waypoints,
                currentWorldPosition,
                targetWorldPosition,
                m_SpawnPosition.y);
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
