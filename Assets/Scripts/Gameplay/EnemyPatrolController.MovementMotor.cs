using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
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
            Vector3 currentPosition = m_Rigidbody.position;
            Vector3 desiredDirection = Vector3.ProjectOnPlane(targetPosition - currentPosition, Vector3.up);
            if (desiredDirection.sqrMagnitude < 0.0001f)
                return;

            MoveInDirection(desiredDirection.normalized, moveSpeed, ignoreRoot, desiredDirection.normalized, prioritizeFacingLookDirection: prioritizeFacingTarget);
        }

        void MoveInDirection(
            Vector3 direction,
            float moveSpeed,
            Transform steeringIgnoreRoot,
            Vector3 lookDirectionOverride = default,
            bool prioritizeFacingLookDirection = false)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (planarDirection.sqrMagnitude < 0.0001f)
                return;

            Vector3 avoidanceDirection = ResolveObstacleAvoidance(planarDirection.normalized, steeringIgnoreRoot);
            Vector3 finalDirection = GetSmoothedMoveDirection(avoidanceDirection);
            Vector3 currentPosition = m_Rigidbody.position;
            Vector3 nextPosition = currentPosition + finalDirection * Mathf.Max(0f, moveSpeed) * Time.fixedDeltaTime;
            nextPosition.y = m_SpawnPosition.y;

            Vector3 planarLookDirection = Vector3.ProjectOnPlane(lookDirectionOverride, Vector3.up);
            if (planarLookDirection.sqrMagnitude < 0.0001f)
                planarLookDirection = finalDirection;
            else
                planarLookDirection.Normalize();

            MoveRigidBody(nextPosition, planarLookDirection, prioritizeFacingLookDirection);
        }

        void MoveRigidBody(Vector3 nextPosition, Vector3 lookDirection, bool prioritizeFacingLookDirection = false)
        {
            Vector3 currentPosition = m_Rigidbody.position;
            Vector3 planarLookDirection = Vector3.ProjectOnPlane(lookDirection, Vector3.up);
            if (planarLookDirection.sqrMagnitude < 0.0001f)
                planarLookDirection = transform.forward;
            else
                planarLookDirection.Normalize();

            if (prioritizeFacingLookDirection)
            {
                float translationScale = GetPursuitTranslationScale(planarLookDirection);
                if (translationScale <= TurnLimitedTranslationScaleThreshold)
                    m_LastTurnLimitedAt = Time.time;

                if (translationScale < 0.999f)
                {
                    Vector3 translation = nextPosition - currentPosition;
                    nextPosition = currentPosition + translation * translationScale;
                    nextPosition.y = m_SpawnPosition.y;
                }
            }

            m_Rigidbody.MovePosition(nextPosition);

            Quaternion targetRotation = Quaternion.LookRotation(planarLookDirection, Vector3.up);
            Quaternion nextRotation = Quaternion.Slerp(m_Rigidbody.rotation, targetRotation, Mathf.Clamp01(m_RotationSpeed * Time.fixedDeltaTime));
            m_Rigidbody.MoveRotation(nextRotation);
        }

        float GetPursuitTranslationScale(Vector3 planarLookDirection)
        {
            Vector3 currentForward = Vector3.ProjectOnPlane(m_Rigidbody.rotation * Vector3.forward, Vector3.up);
            if (currentForward.sqrMagnitude < 0.0001f)
                currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

            if (currentForward.sqrMagnitude < 0.0001f)
                return 1f;

            currentForward.Normalize();
            float angleToLookDirection = Vector3.Angle(currentForward, planarLookDirection);
            if (angleToLookDirection >= m_PursuitTurnInPlaceAngle)
                return 0f;

            if (angleToLookDirection <= m_PursuitTurnSlowdownAngle)
                return 1f;

            return 1f - Mathf.InverseLerp(m_PursuitTurnSlowdownAngle, m_PursuitTurnInPlaceAngle, angleToLookDirection);
        }

        Vector3 ResolveObstacleAvoidance(Vector3 desiredDirection, Transform ignoreRoot)
        {
            if (!ProbeDirection(desiredDirection, ignoreRoot, out _))
                return desiredDirection.normalized;

            Vector3 leftDirection = Quaternion.Euler(0f, -m_ObstacleAvoidanceAngle, 0f) * desiredDirection;
            Vector3 rightDirection = Quaternion.Euler(0f, m_ObstacleAvoidanceAngle, 0f) * desiredDirection;

            bool leftBlocked = ProbeDirection(leftDirection, ignoreRoot, out float leftClearance);
            bool rightBlocked = ProbeDirection(rightDirection, ignoreRoot, out float rightClearance);

            if (!leftBlocked && rightBlocked)
                return Vector3.Slerp(desiredDirection.normalized, leftDirection.normalized, 0.8f).normalized;

            if (!rightBlocked && leftBlocked)
                return Vector3.Slerp(desiredDirection.normalized, rightDirection.normalized, 0.8f).normalized;

            if (!leftBlocked && !rightBlocked)
            {
                Vector3 preferredDirection = leftClearance >= rightClearance ? leftDirection : rightDirection;
                return Vector3.Slerp(desiredDirection.normalized, preferredDirection.normalized, 0.8f).normalized;
            }

            Vector3 fallbackDirection = leftClearance >= rightClearance ? leftDirection : rightDirection;
            return Vector3.Slerp(desiredDirection.normalized, fallbackDirection.normalized, 0.9f).normalized;
        }

        bool ProbeDirection(Vector3 direction, Transform ignoreRoot, out float closestHitDistance)
        {
            closestHitDistance = Mathf.Max(0.01f, m_ObstacleProbeDistance);

            Vector3 normalizedDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (normalizedDirection.sqrMagnitude < 0.0001f)
                return false;

            normalizedDirection.Normalize();

            int hitCount = Physics.SphereCastNonAlloc(GetSensorWorldPosition(), Mathf.Max(0.05f, m_ObstacleProbeRadius), normalizedDirection, m_ProbeHits, Mathf.Max(0.05f, m_ObstacleProbeDistance), m_ObstacleMask, QueryTriggerInteraction.Ignore);

            bool blocked = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = m_ProbeHits[i].collider;
                if (hitCollider == null)
                    continue;

                if (IsOwnCollider(hitCollider))
                    continue;

                Transform hitRoot = hitCollider.transform.root;
                if (ignoreRoot != null && hitRoot == ignoreRoot)
                    continue;

                if (hitCollider.isTrigger)
                    continue;

                if (m_ProbeHits[i].distance >= closestHitDistance)
                    continue;

                closestHitDistance = m_ProbeHits[i].distance;
                blocked = true;
            }

            return blocked;
        }

        bool IsOwnCollider(Collider candidate)
        {
            if (candidate == null)
                return false;

            Transform candidateTransform = candidate.transform;
            return candidateTransform == transform || candidateTransform.IsChildOf(transform);
        }

        Vector3 GetSmoothedMoveDirection(Vector3 desiredDirection)
        {
            Vector3 planarDesiredDirection = Vector3.ProjectOnPlane(desiredDirection, Vector3.up);
            if (planarDesiredDirection.sqrMagnitude < 0.0001f)
                return m_CurrentMoveDirection.sqrMagnitude > 0.0001f ? m_CurrentMoveDirection : GetInitialMoveDirection();

            planarDesiredDirection.Normalize();

            if (m_CurrentMoveDirection.sqrMagnitude < 0.0001f)
            {
                m_CurrentMoveDirection = planarDesiredDirection;
                return m_CurrentMoveDirection;
            }

            float blend = Mathf.Clamp01(m_DirectionSmoothing * Time.fixedDeltaTime);
            m_CurrentMoveDirection = Vector3.Slerp(m_CurrentMoveDirection, planarDesiredDirection, blend).normalized;
            return m_CurrentMoveDirection;
        }
    }
}
