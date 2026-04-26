using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        void ResolvePlayerReferences()
        {
            EnemyPerceptionSensor.ResolvePlayerReferences(ref m_PlayerRig, ref m_PlayerView);
        }

        void UpdateAwareness()
        {
            if (TryGetPursuitLockPlayerPosition(out Vector3 lockedPlayerGroundPosition))
            {
                if (!CanReachMazeTarget(lockedPlayerGroundPosition))
                {
                    RecordNavigationDebugFailure(
                        "Pursuit lock target is currently unreachable through maze topology.",
                        lockedPlayerGroundPosition);
                    if (m_State == EnemyState.Chase)
                        BeginBlockedMazeSearchAtCurrentPosition();

                    return;
                }

                if (m_State != EnemyState.Chase)
                {
                    ClearGridNavigationState(keepPatrolTarget: false);
                    RecordNavigationDebugDecision("Pursuit lock reacquired player; entering chase.", lockedPlayerGroundPosition);
                }

                m_State = EnemyState.Chase;
                m_LastKnownPlayerPosition = lockedPlayerGroundPosition;
                m_LastSeenPlayerAt = Time.time;
                ResetSearchState();
                return;
            }

            if (TryDetectPlayer(out Vector3 playerGroundPosition))
            {
                if (!CanReachMazeTarget(playerGroundPosition))
                {
                    RecordNavigationDebugFailure(
                        "Detected player is currently unreachable through maze topology.",
                        playerGroundPosition);
                    if (m_State == EnemyState.Chase)
                        BeginBlockedMazeSearchAtCurrentPosition();

                    return;
                }

                if (m_State != EnemyState.Chase)
                {
                    ClearGridNavigationState(keepPatrolTarget: false);
                    RecordNavigationDebugDecision("Player detected; entering chase.", playerGroundPosition);
                }

                m_State = EnemyState.Chase;
                m_LastKnownPlayerPosition = playerGroundPosition;
                m_LastSeenPlayerAt = Time.time;
                ResetSearchState();
                return;
            }

            if (TryMaintainOccludedSoftLock())
                return;

            if (m_State == EnemyState.Search)
            {
                if (!m_IsWaitingAtSearchPoint)
                    return;

                if (Time.time - m_SearchWaitStartedAt <= m_SearchWaitDuration)
                    return;

                ExitSearchToPatrol();
                return;
            }

            if (m_State != EnemyState.Chase)
                return;

            if (Time.time - m_LastSeenPlayerAt <= m_LoseSightGraceDuration)
                return;

            EnterSearchState();
        }

        bool TryGetPursuitLockPlayerPosition(out Vector3 playerGroundPosition)
        {
            return EnemyPerceptionSensor.TryGetPursuitLockPlayerPosition(
                m_State != EnemyState.Patrol,
                m_PlayerRig,
                m_PlayerView,
                m_Rigidbody,
                transform,
                GetSensorWorldPosition(),
                m_SpawnPosition.y,
                EffectivePursuitLockDistance,
                m_ObstacleMask,
                m_SelfColliders,
                out playerGroundPosition);
        }

        bool TryMaintainOccludedSoftLock()
        {
            if (!EnemyPerceptionSensor.TryGetOccludedSoftLockPlayerPosition(
                    m_State != EnemyState.Patrol,
                    m_PlayerRig,
                    m_PlayerView,
                    m_Rigidbody,
                    transform,
                    m_SpawnPosition.y,
                    EffectiveOccludedSoftLockDistance,
                    out Vector3 playerGroundPosition))
            {
                return false;
            }

            if (!CanReachMazeTarget(playerGroundPosition))
            {
                RecordNavigationDebugFailure(
                    "Occluded soft-lock target is currently unreachable through maze topology.",
                    playerGroundPosition);
                BeginBlockedMazeSearchAtCurrentPosition();
                return true;
            }

            if (HasLineOfSight(GetSensorWorldPosition(), m_PlayerView.position))
                return false;

            if (m_State == EnemyState.Chase)
            {
                EnterSearchState();
                return true;
            }

            if (m_State != EnemyState.Search)
                return false;

            if (m_IsWaitingAtSearchPoint)
                m_SearchWaitStartedAt = Time.time;

            return true;
        }

        void EnterSearchState()
        {
            m_State = EnemyState.Search;
            m_SearchTargetPosition = m_LastKnownPlayerPosition;
            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            m_SearchBaseLookDirection = Vector3.ProjectOnPlane(m_LastKnownPlayerPosition - currentPosition, Vector3.up);
            if (m_SearchBaseLookDirection.sqrMagnitude < 0.0001f)
                m_SearchBaseLookDirection = m_CurrentMoveDirection.sqrMagnitude > 0.0001f ? m_CurrentMoveDirection : transform.forward;

            m_SearchBaseLookDirection.Normalize();
            m_IsWaitingAtSearchPoint = false;
            m_SearchWaitStartedAt = float.NegativeInfinity;
            m_IsCloseDirectChasing = false;
            m_IsUsingCloseLocalNavigation = false;
            ClearGridNavigationState(keepPatrolTarget: false);
            RecordNavigationDebugDecision("Lost sight of player; entering search.", m_SearchTargetPosition);
        }

        void ExitSearchToPatrol()
        {
            ResetSearchState();
            m_State = EnemyState.Patrol;
            m_IsCloseDirectChasing = false;
            m_IsUsingCloseLocalNavigation = false;
            ClearGridNavigationState(keepPatrolTarget: false);
            PickNewPatrolDirection(forceAnchorBias: true);
            RecordNavigationDebugDecision("Search expired; returning to patrol.", m_RoamCenter);
        }

        void ResetSearchState()
        {
            m_IsWaitingAtSearchPoint = false;
            m_SearchWaitStartedAt = float.NegativeInfinity;
            m_SearchBaseLookDirection = Vector3.zero;
            m_SearchTargetPosition = Vector3.zero;
            m_IsUsingCloseLocalNavigation = false;
        }

        bool TryDetectPlayer(out Vector3 playerGroundPosition)
        {
            return EnemyPerceptionSensor.TryDetectPlayer(
                Time.time < s_GlobalDetectionSuppressedUntil,
                m_PlayerRig,
                m_PlayerView,
                transform,
                GetSensorWorldPosition(),
                m_SpawnPosition.y,
                EffectiveDetectionRange,
                EffectiveFieldOfViewDegrees,
                m_ObstacleMask,
                m_SelfColliders,
                out playerGroundPosition);
        }

        bool HasLineOfSight(Vector3 origin, Vector3 destination)
        {
            return EnemyPerceptionSensor.HasLineOfSight(
                origin,
                destination,
                m_ObstacleMask,
                m_SelfColliders,
                m_PlayerRig,
                m_PlayerView);
        }
    }
}
