using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        bool TryUpdateGridChase(EnemyMovementRequest request)
        {
            if (!CanUseGridNavigation)
                return false;

            if (!m_GridNavigator.TryGetCellAtWorldPosition(m_Rigidbody.position, out MazeCellData currentCell)
                || !m_GridNavigator.TryGetCellAtWorldPosition(request.TargetPosition, out MazeCellData targetCell))
            {
                return false;
            }

            if (TryMoveDirectlyForCloseChase(request))
                return true;

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
                MoveTowards(request.TargetPosition, request.MoveSpeed, request.IgnoreRoot, request.PrioritizeFacingTargetOnDirect);
                return true;
            }

            if (!MoveAlongGridSegment(currentCell.GridPosition, m_GridPath[1], request.MoveSpeed, request.TargetPosition, prioritizeFacingLookTarget: true))
            {
                ClearGridSegmentState();
                Vector3 nextWaypoint = m_GridNavigator.GetCellWorldCenter(m_GridPath[1]);
                MoveTowards(nextWaypoint, request.MoveSpeed, request.IgnoreRoot, prioritizeFacingTarget: false);
            }

            RecordNavigationDebugMode(EnemyNavigationDebugMode.GridChase, request.TargetPosition, $"Grid chase path accepted with {m_GridPath.Count} cells.");
            return true;
        }

        bool TryUpdateNavigationGraphChase(EnemyMovementRequest request)
        {
            if (!CanUseNavigationGraph)
                return false;

            if (TryMoveDirectlyForCloseChase(request))
                return true;

            MazeCellData targetCell = null;
            bool hasTargetCell = CanUseGridNavigation && m_GridNavigator.TryGetCellAtWorldPosition(request.TargetPosition, out targetCell);
            if (!TryEnsureNavigationWaypointPath(request, hasTargetCell ? targetCell.GridPosition : default, hasTargetCell))
                return false;

            TrimNonAdvancingChaseLeadWaypoints(m_NavigationWaypointPath, m_Rigidbody.position, request.TargetPosition);
            RefreshNavigationTerminalWaypoint(m_NavigationWaypointPath, request.TargetPosition);

            bool moved = MoveAlongNavigationWaypoints(m_NavigationWaypointPath, request.MoveSpeed, request.TargetPosition, prioritizeFacingLookTarget: true);
            if (moved)
            {
                RecordNavigationDebugMode(
                    EnemyNavigationDebugMode.NavigationGraphChase,
                    request.TargetPosition,
                    $"Navigation graph chase path accepted with {m_NavigationWaypointPath.Count} waypoints.");
            }

            return moved;
        }

        bool TryMoveDirectlyForCloseChase(EnemyMovementRequest request)
        {
            if (request.State != EnemyState.Chase)
                return false;

            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            Vector3 planarOffset = Vector3.ProjectOnPlane(request.TargetPosition - currentPosition, Vector3.up);
            float directChaseLimit = m_IsCloseDirectChasing ? ResolveClosePursuitExitDistance() : m_ClosePursuitDistance;
            if (planarOffset.magnitude > directChaseLimit)
            {
                m_IsCloseDirectChasing = false;
                return false;
            }

            if (!HasDirectLocalNavigationPath(request.TargetPosition, request.IgnoreRoot))
                return false;

            m_IsCloseDirectChasing = true;
            ClearNavigationPathState();
            ClearGridSegmentState();
            RecordNavigationDebugMode(EnemyNavigationDebugMode.DirectChaseFallback, request.TargetPosition, $"Close chase direct pursuit within {directChaseLimit:0.00}m.");
            MoveTowards(request.TargetPosition, request.MoveSpeed, request.IgnoreRoot, prioritizeFacingTarget: true);
            return true;
        }

        bool TryUpdateNavigationGraphSearch(EnemyMovementRequest request)
        {
            if (!CanUseNavigationGraph)
                return false;

            MazeCellData currentCell = null;
            MazeCellData targetCell = null;
            bool hasCurrentCell = CanUseGridNavigation && m_GridNavigator.TryGetCellAtWorldPosition(m_Rigidbody.position, out currentCell);
            bool hasTargetCell = CanUseGridNavigation && m_GridNavigator.TryGetCellAtWorldPosition(request.TargetPosition, out targetCell);

            if (hasCurrentCell
                && hasTargetCell
                && TryMoveAlongLocalNavigationRequest(request, currentCell.GridPosition, targetCell.GridPosition, EnemyNavigationDebugMode.LocalSearch, "Search using close-range local navigation.", useCloseNavigationHysteresis: true))
            {
                if (IsNearSearchTarget(request.TargetPosition))
                    BeginSearchWait();

                return true;
            }

            if (!TryEnsureNavigationWaypointPath(request, hasTargetCell ? targetCell.GridPosition : default, hasTargetCell))
                return false;

            bool moved = MoveAlongNavigationWaypoints(m_NavigationWaypointPath, request.MoveSpeed, request.TargetPosition, prioritizeFacingLookTarget: false);
            if (moved && IsNearSearchTarget(request.TargetPosition))
                BeginSearchWait();

            if (moved)
                RecordNavigationDebugMode(EnemyNavigationDebugMode.NavigationGraphSearch, request.TargetPosition, $"Navigation graph search path accepted with {m_NavigationWaypointPath.Count} waypoints.");

            return moved;
        }

        bool TryMoveAlongLocalNavigationRequest(
            EnemyMovementRequest request,
            Vector2Int currentCell,
            Vector2Int targetCell,
            EnemyNavigationDebugMode debugMode,
            string decision,
            bool useCloseNavigationHysteresis = false)
        {
            if (!ShouldUseLocalNavigation(currentCell, targetCell, request.TargetPosition, request.IgnoreRoot, useCloseNavigationHysteresis))
                return false;

            if (!m_NavigationGraph.TryBuildLocalPath(currentCell, targetCell, m_Rigidbody.position, request.TargetPosition, m_LocalNavigationWaypoints))
            {
                RecordNavigationDebugFailure("Local navigation failed to build a local waypoint path.", request.TargetPosition);
                return false;
            }

            bool moved = MoveAlongNavigationWaypoints(m_LocalNavigationWaypoints, request.MoveSpeed, request.TargetPosition, prioritizeFacingLookTarget: request.State == EnemyState.Chase);
            if (!moved)
            {
                RecordNavigationDebugFailure("Local navigation built a path but could not move along it.", request.TargetPosition);
                return false;
            }

            m_NavigationNodePath.Clear();
            m_NavigationWaypointPath.Clear();
            m_HasCurrentNavigationGoalCell = false;
            if (useCloseNavigationHysteresis)
                m_IsUsingCloseLocalNavigation = true;

            RecordNavigationDebugMode(debugMode, request.TargetPosition, decision);
            return true;
        }
    }
}
