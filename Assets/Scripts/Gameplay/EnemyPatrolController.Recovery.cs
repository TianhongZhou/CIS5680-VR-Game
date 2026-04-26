using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        bool TryRecoverFromMovementStall()
        {
            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            Vector3 planarDelta = Vector3.ProjectOnPlane(currentPosition - m_LastProgressPosition, Vector3.up);
            float progressThreshold = Mathf.Max(0.01f, m_StuckProgressDistance);
            if (planarDelta.sqrMagnitude >= progressThreshold * progressThreshold)
            {
                m_StuckRecoveryAttemptCount = 0;
                MarkMovementProgress(currentPosition);
                return false;
            }

            if (!ShouldExpectMovementProgress())
            {
                m_StuckRecoveryAttemptCount = 0;
                MarkMovementProgress(currentPosition);
                return false;
            }

            if (WasRecentlyTurnLimited())
            {
                m_StuckRecoveryAttemptCount = 0;
                MarkMovementProgress(currentPosition);
                RecordNavigationDebugDecision(
                    "Stuck timer paused: enemy is intentionally turning in place before moving.",
                    ResolveCurrentMovementTarget(currentPosition, out _, out _, out _));
                return false;
            }

            if (Time.time - m_LastProgressAt < Mathf.Max(0.25f, m_StuckRecoveryDelay))
                return false;

            m_StuckRecoveryAttemptCount++;
            if (m_StuckRecoveryAttemptCount <= m_StuckSoftRecoveryAttempts
                && TrySoftRecoverFromMovementStall(currentPosition, m_StuckRecoveryAttemptCount))
            {
                MarkMovementProgress(currentPosition);
                return true;
            }

            RecoverFromMovementStall(currentPosition);
            m_StuckRecoveryAttemptCount = 0;
            MarkMovementProgress(currentPosition);
            return true;
        }

        bool ShouldExpectMovementProgress()
        {
            if (m_State == EnemyState.Search && m_IsWaitingAtSearchPoint)
                return false;

            if (m_State == EnemyState.Chase || m_State == EnemyState.Search)
                return true;

            return m_State == EnemyState.Patrol
                || m_HasCurrentPatrolTargetCell
                || m_HasCurrentNavigationGoalCell
                || m_HasCurrentPathGoalCell
                || m_NavigationWaypointPath.Count > 0
                || m_GridPath.Count > 0
                || m_LocalNavigationWaypoints.Count > 0;
        }

        bool TrySoftRecoverFromMovementStall(Vector3 currentPosition, int attempt)
        {
            Vector3 targetPosition = ResolveCurrentMovementTarget(
                currentPosition,
                out bool hasTarget,
                out Transform ignoreRoot,
                out float moveSpeed);

            if (attempt > 1
                && hasTarget
                && TryMoveAlongStuckLocalRecoveryPath(currentPosition, targetPosition, ignoreRoot, moveSpeed, attempt))
            {
                return true;
            }

            bool keepPatrolTarget = m_State == EnemyState.Patrol && attempt <= 1;
            ClearPathCachesForStuckRecovery(keepPatrolTarget);

            if (m_State == EnemyState.Patrol && attempt > 1)
            {
                m_HasLastPatrolCell = false;
                PickNewPatrolDirection(forceAnchorBias: true);
            }
            else if (hasTarget)
            {
                m_CurrentMoveDirection = ResolveRecoveryDirection(currentPosition, targetPosition);
            }

            RecordNavigationDebugMode(
                EnemyNavigationDebugMode.StuckRecovery,
                targetPosition,
                $"Movement stall soft recovery {attempt}/{m_StuckSoftRecoveryAttempts}: cleared cached paths and will retry navigation.");
            return true;
        }

        bool TryMoveAlongStuckLocalRecoveryPath(
            Vector3 currentPosition,
            Vector3 targetPosition,
            Transform ignoreRoot,
            float moveSpeed,
            int attempt)
        {
            if (!CanUseNavigationGraph || !CanUseGridNavigation)
                return false;

            if (!m_GridNavigator.TryGetCellAtWorldPosition(currentPosition, out MazeCellData currentCell)
                || !m_GridNavigator.TryGetCellAtWorldPosition(targetPosition, out MazeCellData targetCell))
            {
                return false;
            }

            if (!ShouldUseLocalNavigation(
                    currentCell.GridPosition,
                    targetCell.GridPosition,
                    targetPosition,
                    ignoreRoot,
                    useCloseNavigationHysteresis: m_State == EnemyState.Chase || m_State == EnemyState.Search))
                return false;

            ClearPathCachesForStuckRecovery(keepPatrolTarget: true);
            if (!m_NavigationGraph.TryBuildLocalPath(
                    currentCell.GridPosition,
                    targetCell.GridPosition,
                    currentPosition,
                    targetPosition,
                    m_LocalNavigationWaypoints))
            {
                RecordNavigationDebugFailure("Movement stall local recovery failed to build a local path.", targetPosition);
                return false;
            }

            bool moved = MoveAlongNavigationWaypoints(
                m_LocalNavigationWaypoints,
                Mathf.Max(0f, moveSpeed),
                targetPosition,
                prioritizeFacingLookTarget: false);
            if (!moved)
            {
                RecordNavigationDebugFailure("Movement stall local recovery built a path but could not move along it.", targetPosition);
                return false;
            }

            RecordNavigationDebugMode(
                EnemyNavigationDebugMode.StuckRecovery,
                targetPosition,
                $"Movement stall soft recovery {attempt}/{m_StuckSoftRecoveryAttempts}: moved along local recovery path with {m_LocalNavigationWaypoints.Count} waypoints.");
            return true;
        }

        void ClearPathCachesForStuckRecovery(bool keepPatrolTarget)
        {
            m_GridPath.Clear();
            m_HasCurrentPathGoalCell = false;
            ClearNavigationPathState();
            ClearGridSegmentState();
            ClearNavigationQueryCaches();
            if (!keepPatrolTarget)
                m_HasCurrentPatrolTargetCell = false;
        }

        Vector3 ResolveCurrentMovementTarget(
            Vector3 currentPosition,
            out bool hasTarget,
            out Transform ignoreRoot,
            out float moveSpeed)
        {
            hasTarget = true;
            ignoreRoot = null;
            moveSpeed = Mathf.Max(0f, m_PatrolSpeed);

            if (m_State == EnemyState.Chase)
            {
                EnemyMovementRequest chaseRequest = CreateChaseMovementRequest();
                ignoreRoot = chaseRequest.IgnoreRoot;
                moveSpeed = chaseRequest.MoveSpeed;
                return chaseRequest.TargetPosition;
            }

            if (m_State == EnemyState.Search)
            {
                EnemyMovementRequest searchRequest = CreateSearchMovementRequest();
                ignoreRoot = searchRequest.IgnoreRoot;
                moveSpeed = searchRequest.MoveSpeed;
                return searchRequest.TargetPosition;
            }

            if (m_HasCurrentPatrolTargetCell && CanUseGridNavigation)
            {
                EnemyMovementRequest patrolRequest = CreatePatrolMovementRequest(m_GridNavigator.GetCellWorldCenter(m_CurrentPatrolTargetCell));
                ignoreRoot = patrolRequest.IgnoreRoot;
                moveSpeed = patrolRequest.MoveSpeed;
                return patrolRequest.TargetPosition;
            }

            if (m_HasCurrentNavigationGoalCell && m_NavigationGraph != null
                && m_NavigationGraph.TryGetModule(m_CurrentNavigationGoalCell, out MazeNavigationModuleRecord module))
            {
                EnemyMovementRequest patrolRequest = CreatePatrolMovementRequest(module.WorldCenter);
                ignoreRoot = patrolRequest.IgnoreRoot;
                moveSpeed = patrolRequest.MoveSpeed;
                return patrolRequest.TargetPosition;
            }

            hasTarget = false;
            return currentPosition;
        }

        Vector3 ResolveRecoveryDirection(Vector3 currentPosition, Vector3 targetPosition)
        {
            Vector3 targetDirection = Vector3.ProjectOnPlane(targetPosition - currentPosition, Vector3.up);
            if (targetDirection.sqrMagnitude >= 0.0001f)
                return targetDirection.normalized;

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude >= 0.0001f)
                return forward.normalized;

            return Vector3.forward;
        }

        bool WasRecentlyTurnLimited()
        {
            if (m_State != EnemyState.Chase && m_State != EnemyState.Search)
                return false;

            float graceDuration = Mathf.Max(Time.fixedDeltaTime * 2f, m_StuckTurnInPlaceGraceDuration);
            return Time.time - m_LastTurnLimitedAt <= graceDuration;
        }

        void RecoverFromMovementStall(Vector3 currentPosition)
        {
            ClearGridNavigationState(keepPatrolTarget: false);
            ClearNavigationQueryCaches();
            m_CurrentMoveDirection = Vector3.zero;

            if (m_State == EnemyState.Chase || m_State == EnemyState.Search)
            {
                m_State = EnemyState.Search;
                m_LastKnownPlayerPosition = currentPosition;
                m_SearchTargetPosition = currentPosition;
                m_IsWaitingAtSearchPoint = false;
                BeginSearchWait();
                RecordNavigationDebugMode(
                    EnemyNavigationDebugMode.StuckRecovery,
                    currentPosition,
                    "Movement stall recovery: chase/search converted to local search wait.");
                return;
            }

            m_HasLastPatrolCell = false;
            PickNewPatrolDirection(forceAnchorBias: true);
            RecordNavigationDebugMode(
                EnemyNavigationDebugMode.StuckRecovery,
                currentPosition,
                "Movement stall recovery: patrol direction reset.");
        }

        void MarkMovementProgress()
        {
            MarkMovementProgress(m_Rigidbody != null ? m_Rigidbody.position : transform.position);
        }

        void MarkMovementProgress(Vector3 currentPosition)
        {
            m_LastProgressPosition = currentPosition;
            m_LastProgressAt = Time.time;
        }
    }
}
