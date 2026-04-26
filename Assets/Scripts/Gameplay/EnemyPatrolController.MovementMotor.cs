using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        void UpdatePatrol()
        {
            if (m_WanderDirection.sqrMagnitude < 0.0001f || Time.time >= m_NextDirectionChangeAt)
                PickNewPatrolDirection(forceAnchorBias: false);

            Vector3 patrolDirection = ResolvePatrolDirection();
            if (patrolDirection.sqrMagnitude < 0.0001f)
            {
                PickNewPatrolDirection(forceAnchorBias: true);
                patrolDirection = ResolvePatrolDirection();
            }

            MoveInDirection(patrolDirection, Mathf.Max(0f, m_PatrolSpeed), steeringIgnoreRoot: null);
        }

        Vector3 ResolvePatrolDirection()
        {
            Vector3 desiredDirection = m_WanderDirection;
            if (desiredDirection.sqrMagnitude < 0.0001f)
                desiredDirection = Random.insideUnitSphere;

            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude < 0.0001f)
                desiredDirection = transform.forward;

            desiredDirection.Normalize();

            Vector3 toAnchor = m_RoamCenter - m_Rigidbody.position;
            Vector3 planarToAnchor = Vector3.ProjectOnPlane(toAnchor, Vector3.up);
            float distanceFromAnchor = planarToAnchor.magnitude;
            if (distanceFromAnchor > Mathf.Max(0.1f, m_AnchorPullDistance))
            {
                Vector3 anchorBias = planarToAnchor / distanceFromAnchor;
                float blend = Mathf.InverseLerp(m_AnchorPullDistance, Mathf.Max(m_AnchorPullDistance + 0.01f, m_PatrolRadius), distanceFromAnchor);
                desiredDirection = Vector3.Slerp(desiredDirection, anchorBias, Mathf.Clamp01(0.35f + blend * 0.5f)).normalized;
            }

            return desiredDirection;
        }

        void PickNewPatrolDirection(bool forceAnchorBias)
        {
            m_RoamCenter = ResolveRoamCenter();

            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0f;
            if (randomDirection.sqrMagnitude < 0.0001f)
                randomDirection = transform.forward;

            randomDirection.Normalize();

            Vector3 toAnchor = m_RoamCenter - m_Rigidbody.position;
            Vector3 planarToAnchor = Vector3.ProjectOnPlane(toAnchor, Vector3.up);
            float distanceFromAnchor = planarToAnchor.magnitude;
            if (forceAnchorBias || distanceFromAnchor > Mathf.Max(0.1f, m_AnchorPullDistance))
            {
                Vector3 anchorDirection = distanceFromAnchor > 0.01f ? planarToAnchor / distanceFromAnchor : transform.forward;
                float anchorBlend = forceAnchorBias ? 0.8f : Mathf.InverseLerp(m_AnchorPullDistance, Mathf.Max(m_AnchorPullDistance + 0.01f, m_PatrolRadius), distanceFromAnchor);
                randomDirection = Vector3.Slerp(randomDirection, anchorDirection, Mathf.Clamp01(anchorBlend)).normalized;
            }

            m_WanderDirection = randomDirection;
            m_NextDirectionChangeAt = Time.time + Random.Range(m_DirectionChangeIntervalMin, m_DirectionChangeIntervalMax);
        }

        void MoveTowards(Vector3 targetPosition, float moveSpeed, Transform ignoreRoot, bool prioritizeFacingTarget = false)
        {
            EnemyMovementMotor.MoveTowards(
                m_Rigidbody,
                transform,
                targetPosition,
                moveSpeed,
                ignoreRoot,
                prioritizeFacingTarget,
                BuildMovementMotorSettings(),
                BuildMovementProbeSettings(),
                ref m_CurrentMoveDirection,
                ref m_LastTurnLimitedAt);
        }

        void MoveInDirection(
            Vector3 direction,
            float moveSpeed,
            Transform steeringIgnoreRoot,
            Vector3 lookDirectionOverride = default,
            bool prioritizeFacingLookDirection = false)
        {
            EnemyMovementMotor.MoveInDirection(
                m_Rigidbody,
                transform,
                direction,
                moveSpeed,
                steeringIgnoreRoot,
                lookDirectionOverride,
                prioritizeFacingLookDirection,
                BuildMovementMotorSettings(),
                BuildMovementProbeSettings(),
                ref m_CurrentMoveDirection,
                ref m_LastTurnLimitedAt);
        }

        void MoveRigidBody(Vector3 nextPosition, Vector3 lookDirection, bool prioritizeFacingLookDirection = false)
        {
            EnemyMovementMotor.MoveRigidBody(
                m_Rigidbody,
                transform,
                nextPosition,
                lookDirection,
                prioritizeFacingLookDirection,
                BuildMovementMotorSettings(),
                ref m_LastTurnLimitedAt);
        }

        float GetPursuitTranslationScale(Vector3 planarLookDirection)
        {
            return EnemyMovementMotor.GetPursuitTranslationScale(
                m_Rigidbody,
                transform,
                planarLookDirection,
                m_PursuitTurnSlowdownAngle,
                m_PursuitTurnInPlaceAngle);
        }

        Vector3 ResolveObstacleAvoidance(Vector3 desiredDirection, Transform ignoreRoot)
        {
            return EnemyMovementMotor.ResolveObstacleAvoidance(desiredDirection, ignoreRoot, BuildMovementProbeSettings());
        }

        bool ProbeDirection(Vector3 direction, Transform ignoreRoot, out float closestHitDistance)
        {
            return EnemyMovementMotor.ProbeDirection(direction, ignoreRoot, BuildMovementProbeSettings(), out closestHitDistance);
        }

        bool IsOwnCollider(Collider candidate)
        {
            return EnemyMovementMotor.IsOwnCollider(candidate, transform);
        }

        Vector3 GetSmoothedMoveDirection(Vector3 desiredDirection)
        {
            return EnemyMovementMotor.GetSmoothedMoveDirection(
                desiredDirection,
                GetInitialMoveDirection(),
                m_DirectionSmoothing,
                ref m_CurrentMoveDirection);
        }

        EnemyMovementMotor.MotorSettings BuildMovementMotorSettings()
        {
            return new EnemyMovementMotor.MotorSettings(
                m_SpawnPosition.y,
                m_RotationSpeed,
                m_DirectionSmoothing,
                m_PursuitTurnSlowdownAngle,
                m_PursuitTurnInPlaceAngle,
                TurnLimitedTranslationScaleThreshold,
                GetInitialMoveDirection());
        }

        EnemyMovementMotor.ProbeSettings BuildMovementProbeSettings()
        {
            return new EnemyMovementMotor.ProbeSettings(
                GetSensorWorldPosition(),
                m_ObstacleProbeRadius,
                m_ObstacleProbeDistance,
                m_ObstacleAvoidanceAngle,
                m_ObstacleMask,
                m_ProbeHits,
                transform);
        }
    }
}
