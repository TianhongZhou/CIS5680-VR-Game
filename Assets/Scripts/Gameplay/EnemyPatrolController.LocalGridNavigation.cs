using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        bool ShouldUseLocalNavigation(
            Vector2Int currentCell,
            Vector2Int targetCell,
            Vector3 targetWorldPosition,
            Transform ignoreRoot = null,
            bool useCloseNavigationHysteresis = false)
        {
            if (CanUseGridNavigation && !TryGetCachedGridReachability(currentCell, targetCell, targetWorldPosition))
            {
                RecordNavigationDebugFailure($"Local navigation skipped: no traversable grid path {FormatDebugCell(currentCell)} -> {FormatDebugCell(targetCell)}.", targetWorldPosition);
                return false;
            }

            int allowedModuleDistance = Mathf.Max(1, m_ClosePursuitModuleDistance);
            int moduleDistance = GetGridDistance(currentCell, targetCell);
            if (moduleDistance > allowedModuleDistance)
            {
                RecordNavigationDebugDecision($"Local navigation skipped: module distance {moduleDistance} exceeds {allowedModuleDistance}.", targetWorldPosition);
                return false;
            }

            if (moduleDistance == 0)
                return true;

            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            Vector3 planarOffset = Vector3.ProjectOnPlane(targetWorldPosition - currentPosition, Vector3.up);
            float localNavigationDistanceLimit = useCloseNavigationHysteresis && m_IsUsingCloseLocalNavigation
                ? ResolveClosePursuitExitDistance()
                : m_ClosePursuitDistance;
            if (planarOffset.magnitude > localNavigationDistanceLimit)
            {
                if (useCloseNavigationHysteresis)
                    m_IsUsingCloseLocalNavigation = false;

                RecordNavigationDebugDecision($"Local navigation skipped: target distance {planarOffset.magnitude:0.00}m exceeds {localNavigationDistanceLimit:0.00}m.", targetWorldPosition);
                return false;
            }

            bool directPathClear = HasDirectLocalNavigationPath(targetWorldPosition, ignoreRoot);
            if (directPathClear)
            {
                if (useCloseNavigationHysteresis)
                    m_IsUsingCloseLocalNavigation = false;

                RecordNavigationDebugDecision("Local navigation skipped: direct path probe is clear.", targetWorldPosition);
                return false;
            }

            return true;
        }

        bool HasDirectLocalNavigationPath(Vector3 targetWorldPosition, Transform ignoreRoot)
        {
            return EnemyPathPlanner.HasDirectLocalNavigationPath(
                GetLocalNavigationProbeWorldPosition(),
                targetWorldPosition,
                GetNavigationClearanceRadius(),
                m_ObstacleMask,
                m_ProbeHits,
                transform,
                ignoreRoot);
        }

        float GetNavigationClearanceRadius()
        {
            return Mathf.Max(0.05f, m_ObstacleProbeRadius);
        }

        Vector3 GetLocalNavigationProbeWorldPosition()
        {
            Vector3 basePosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            float probeHeight = Mathf.Clamp(m_ObstacleProbeHeight * 0.6f, 0.3f, 1.2f);
            return basePosition + Vector3.up * probeHeight;
        }

        bool TryRebuildChasePath(Vector2Int currentCell, Vector2Int targetCell)
        {
            Vector3 targetWorldPosition = m_GridNavigator.GetCellWorldCenter(targetCell);
            if (ShouldSkipFailedGridPathRetry(currentCell, targetCell, targetWorldPosition))
                return false;

            if (TryUseCachedGridReachabilityPath(currentCell, targetCell, m_GridPath))
            {
                m_CurrentPathGoalCell = targetCell;
                m_HasCurrentPathGoalCell = true;
                ClearGridPathFailure(currentCell, targetCell);
                return true;
            }

            m_NavigationQueryCache.RecordPathQuery();
            if (!m_GridNavigator.TryFindPath(currentCell, targetCell, m_GridPath))
            {
                RecordNavigationDebugFailure($"Grid path rebuild failed: {FormatDebugCell(currentCell)} -> {FormatDebugCell(targetCell)}.", targetWorldPosition);
                RecordGridPathFailure(currentCell, targetCell);
                return false;
            }

            m_CurrentPathGoalCell = targetCell;
            m_HasCurrentPathGoalCell = true;
            ClearGridPathFailure(currentCell, targetCell);
            return true;
        }

        bool TryAlignChasePathToCurrentCell(Vector2Int currentCell)
        {
            return EnemyPathPlanner.TryAlignGridPathToCurrentCell(m_GridPath, currentCell);
        }

        bool TryUpdateGridSearch(EnemyMovementRequest request)
        {
            if (!CanUseGridNavigation)
                return false;

            if (!m_GridNavigator.TryGetCellAtWorldPosition(m_Rigidbody.position, out MazeCellData currentCell)
                || !m_GridNavigator.TryGetCellAtWorldPosition(request.TargetPosition, out MazeCellData targetCell))
            {
                return false;
            }

            bool targetCellChanged = !m_HasCurrentPathGoalCell || m_CurrentPathGoalCell != targetCell.GridPosition;
            if (targetCellChanged || m_GridPath.Count == 0)
            {
                if (!TryRebuildChasePath(currentCell.GridPosition, targetCell.GridPosition))
                    return false;
            }
            else if (!TryAlignChasePathToCurrentCell(currentCell.GridPosition))
            {
                if (!TryRebuildChasePath(currentCell.GridPosition, targetCell.GridPosition))
                    return false;
            }

            if (m_GridPath.Count <= 1)
            {
                ClearGridSegmentState();
                if (IsNearSearchTarget(request.TargetPosition))
                    BeginSearchWait();
                else
                    MoveTowards(request.TargetPosition, request.MoveSpeed, request.IgnoreRoot, request.PrioritizeFacingTargetOnDirect);

                return true;
            }

            if (!MoveAlongGridSegment(currentCell.GridPosition, m_GridPath[1], request.MoveSpeed, request.TargetPosition, prioritizeFacingLookTarget: false))
            {
                ClearGridSegmentState();
                Vector3 nextWaypoint = m_GridNavigator.GetCellWorldCenter(m_GridPath[1]);
                MoveTowards(nextWaypoint, request.MoveSpeed, request.IgnoreRoot, prioritizeFacingTarget: false);
            }

            RecordNavigationDebugMode(EnemyNavigationDebugMode.GridSearch, request.TargetPosition, $"Grid search path accepted with {m_GridPath.Count} cells.");
            if (IsNearSearchTarget(request.TargetPosition))
                BeginSearchWait();

            return true;
        }

        bool TryUpdateGridPatrol(EnemyMovementRequest request)
        {
            if (!CanUseGridNavigation)
                return false;

            if (!m_GridNavigator.TryGetCellAtWorldPosition(m_Rigidbody.position, out MazeCellData currentCell))
                return false;

            if (!m_HasCurrentPatrolTargetCell || currentCell.GridPosition == m_CurrentPatrolTargetCell || IsNearGridCell(m_CurrentPatrolTargetCell))
            {
                if (!TrySelectNextPatrolCell(currentCell, out Vector2Int patrolTarget))
                    return false;

                m_LastPatrolCell = currentCell.GridPosition;
                m_HasLastPatrolCell = true;
                m_CurrentPatrolTargetCell = patrolTarget;
                m_HasCurrentPatrolTargetCell = true;
            }

            EnemyMovementRequest patrolRequest = request.WithTarget(m_GridNavigator.GetCellWorldCenter(m_CurrentPatrolTargetCell));
            if (!MoveAlongGridSegment(currentCell.GridPosition, m_CurrentPatrolTargetCell, patrolRequest.MoveSpeed))
            {
                ClearGridSegmentState();
                MoveTowards(patrolRequest.TargetPosition, patrolRequest.MoveSpeed, patrolRequest.IgnoreRoot, patrolRequest.PrioritizeFacingTargetOnDirect);
            }

            RecordNavigationDebugMode(EnemyNavigationDebugMode.GridPatrol, patrolRequest.TargetPosition, $"Grid patrol target {FormatDebugCell(m_CurrentPatrolTargetCell)}.");
            return true;
        }
    }
}
