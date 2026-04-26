using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        void ResolvePlayerReferences()
        {
            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_PlayerRig != null && m_PlayerView == null)
            {
                if (m_PlayerRig.Camera != null)
                    m_PlayerView = m_PlayerRig.Camera.transform;
                else
                {
                    Camera rigCamera = m_PlayerRig.GetComponentInChildren<Camera>(true);
                    if (rigCamera != null)
                        m_PlayerView = rigCamera.transform;
                }
            }

            if (m_PlayerView == null && Camera.main != null)
                m_PlayerView = Camera.main.transform;
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
            playerGroundPosition = transform.position;
            if (m_State == EnemyState.Patrol || m_PlayerView == null || EffectivePursuitLockDistance <= 0f)
                return false;

            Vector3 sensorPosition = GetSensorWorldPosition();
            Vector3 playerViewPosition = m_PlayerView.position;
            Vector3 lockSource = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            Vector3 playerAnchorPosition = m_PlayerRig != null ? m_PlayerRig.transform.position : m_PlayerView.position;
            Vector3 planarToPlayer = Vector3.ProjectOnPlane(playerAnchorPosition - lockSource, Vector3.up);
            if (planarToPlayer.magnitude > EffectivePursuitLockDistance)
                return false;

            if (!HasLineOfSight(sensorPosition, playerViewPosition))
                return false;

            playerGroundPosition = GetGroundPosition(playerAnchorPosition);
            return true;
        }

        bool TryMaintainOccludedSoftLock()
        {
            if (m_State == EnemyState.Patrol || m_PlayerView == null || EffectiveOccludedSoftLockDistance <= 0f)
                return false;

            Vector3 sensorPosition = GetSensorWorldPosition();
            Vector3 playerViewPosition = m_PlayerView.position;
            Vector3 playerAnchorPosition = m_PlayerRig != null ? m_PlayerRig.transform.position : playerViewPosition;
            Vector3 lockSource = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            Vector3 planarToPlayer = Vector3.ProjectOnPlane(playerAnchorPosition - lockSource, Vector3.up);
            if (planarToPlayer.magnitude > EffectiveOccludedSoftLockDistance)
                return false;

            Vector3 playerGroundPosition = GetGroundPosition(playerAnchorPosition);
            if (!CanReachMazeTarget(playerGroundPosition))
            {
                RecordNavigationDebugFailure(
                    "Occluded soft-lock target is currently unreachable through maze topology.",
                    playerGroundPosition);
                BeginBlockedMazeSearchAtCurrentPosition();
                return true;
            }

            if (HasLineOfSight(sensorPosition, playerViewPosition))
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
            playerGroundPosition = transform.position;

            if (m_PlayerView == null)
                return false;

            if (Time.time < s_GlobalDetectionSuppressedUntil)
                return false;

            Vector3 sensorPosition = GetSensorWorldPosition();
            Vector3 playerViewPosition = m_PlayerView.position;
            Vector3 planarToPlayer = Vector3.ProjectOnPlane(playerViewPosition - sensorPosition, Vector3.up);
            float planarDistance = planarToPlayer.magnitude;
            if (planarDistance > EffectiveDetectionRange)
                return false;

            if (planarDistance <= 0.01f)
            {
                playerGroundPosition = GetGroundPosition(playerViewPosition);
                return true;
            }

            float angleToPlayer = Vector3.Angle(transform.forward, planarToPlayer.normalized);
            if (angleToPlayer > EffectiveFieldOfViewDegrees * 0.5f)
                return false;

            if (!HasLineOfSight(sensorPosition, playerViewPosition))
                return false;

            playerGroundPosition = GetGroundPosition(m_PlayerRig != null ? m_PlayerRig.transform.position : playerViewPosition);
            return true;
        }

        bool HasLineOfSight(Vector3 origin, Vector3 destination)
        {
            Vector3 castDirection = destination - origin;
            float castDistance = castDirection.magnitude;
            if (castDistance <= 0.01f)
                return true;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                castDirection / castDistance,
                castDistance,
                m_ObstacleMask,
                QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
                return true;

            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || hitCollider.isTrigger)
                    continue;

                if (IsOwnCollider(hitCollider))
                    continue;

                Transform hitRoot = hitCollider.transform.root;
                if (m_PlayerRig != null && hitRoot == m_PlayerRig.transform)
                    continue;

                if (m_PlayerView != null && hitCollider.transform.IsChildOf(m_PlayerView.root))
                    continue;

                return false;
            }

            return true;
        }
    }
}
