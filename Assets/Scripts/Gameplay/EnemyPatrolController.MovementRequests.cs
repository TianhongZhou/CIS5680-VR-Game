using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        EnemyMovementRequest CreateChaseMovementRequest()
        {
            return new EnemyMovementRequest(
                EnemyState.Chase,
                m_LastKnownPlayerPosition,
                Mathf.Max(0f, m_ChaseSpeed),
                m_PlayerRig != null ? m_PlayerRig.transform : null,
                prioritizeFacingTargetOnDirect: true);
        }

        EnemyMovementRequest CreateSearchMovementRequest()
        {
            return new EnemyMovementRequest(
                EnemyState.Search,
                m_SearchTargetPosition,
                Mathf.Max(0f, m_ChaseSpeed),
                ignoreRoot: null,
                prioritizeFacingTargetOnDirect: true);
        }

        EnemyMovementRequest CreatePatrolMovementRequest()
        {
            return CreatePatrolMovementRequest(ResolveRoamCenter());
        }

        EnemyMovementRequest CreatePatrolMovementRequest(Vector3 targetPosition)
        {
            return new EnemyMovementRequest(
                EnemyState.Patrol,
                targetPosition,
                Mathf.Max(0f, m_PatrolSpeed),
                ignoreRoot: null,
                prioritizeFacingTargetOnDirect: false);
        }

        bool TryExecuteCurrentMovement()
        {
            if (m_State == EnemyState.Chase)
                return TryExecuteMovementRequest(CreateChaseMovementRequest());

            if (m_State == EnemyState.Search)
            {
                if (TryExecuteMovementRequest(CreateSearchMovementRequest()))
                    return true;

                ExitSearchToPatrol();
                return true;
            }

            return TryExecuteMovementRequest(CreatePatrolMovementRequest());
        }

        bool TryExecuteMovementRequest(EnemyMovementRequest request)
        {
            return request.State switch
            {
                EnemyState.Chase => TryExecuteChaseMovementRequest(request),
                EnemyState.Search => TryExecuteSearchMovementRequest(request),
                EnemyState.Patrol => TryExecutePatrolMovementRequest(request),
                _ => false,
            };
        }

        bool TryExecuteChaseMovementRequest(EnemyMovementRequest request)
        {
            if (TryUpdateNavigationGraphChase(request))
                return true;

            if (TryUpdateGridChase(request))
                return true;

            if (ShouldStopAtUnreachableMazeTarget(request.TargetPosition))
            {
                RecordNavigationDebugFailure("Chase target is currently unreachable through maze topology.", request.TargetPosition);
                BeginBlockedMazeSearchAtCurrentPosition();
                return true;
            }

            return TryMoveDirectlyWithRequest(
                request,
                EnemyNavigationDebugMode.DirectChaseFallback,
                "Chase fallback: moving directly toward last known player position.",
                clearNavigationPath: true,
                clearGridSegment: true);
        }

        bool TryExecuteSearchMovementRequest(EnemyMovementRequest request)
        {
            if (m_IsWaitingAtSearchPoint)
            {
                RecordNavigationDebugMode(
                    EnemyNavigationDebugMode.SearchWait,
                    m_SearchTargetPosition,
                    "Search wait: enemy is sweeping at the last reachable point.");
                UpdateSearchSweep();
                return true;
            }

            if (TryUpdateNavigationGraphSearch(request))
                return true;

            if (TryUpdateGridSearch(request))
                return true;

            if (ShouldStopAtUnreachableMazeTarget(request.TargetPosition))
            {
                RecordNavigationDebugFailure("Search target is currently unreachable through maze topology.", request.TargetPosition);
                BeginBlockedMazeSearchAtCurrentPosition();
                return true;
            }

            if (IsNearSearchTarget(request.TargetPosition))
            {
                BeginSearchWait();
                return true;
            }

            return TryMoveDirectlyWithRequest(
                request,
                EnemyNavigationDebugMode.DirectSearchFallback,
                "Search fallback: moving directly toward search target.",
                clearNavigationPath: true,
                clearGridSegment: true);
        }

        bool TryExecutePatrolMovementRequest(EnemyMovementRequest request)
        {
            if (TryUpdateNavigationGraphPatrol(request))
                return true;

            if (TryUpdateGridPatrol(request))
                return true;

            ClearGridSegmentState();
            RecordNavigationDebugMode(
                EnemyNavigationDebugMode.WanderPatrol,
                request.TargetPosition,
                "Patrol fallback: free wander with obstacle avoidance.");
            UpdatePatrol();
            return true;
        }

        bool TryMoveDirectlyWithRequest(
            EnemyMovementRequest request,
            EnemyNavigationDebugMode debugMode,
            string decision,
            bool clearNavigationPath,
            bool clearGridSegment)
        {
            if (clearNavigationPath)
                ClearNavigationPathState();

            if (clearGridSegment)
                ClearGridSegmentState();

            RecordNavigationDebugMode(debugMode, request.TargetPosition, decision);
            MoveTowards(
                request.TargetPosition,
                request.MoveSpeed,
                request.IgnoreRoot,
                request.PrioritizeFacingTargetOnDirect);
            return true;
        }

    }
}
