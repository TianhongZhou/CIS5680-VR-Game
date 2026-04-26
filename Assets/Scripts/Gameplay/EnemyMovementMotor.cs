using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    internal static class EnemyMovementMotor
    {
        internal static void MoveTowards(
            Rigidbody body,
            Transform bodyTransform,
            Vector3 targetPosition,
            float moveSpeed,
            Transform steeringIgnoreRoot,
            bool prioritizeFacingTarget,
            MotorSettings settings,
            ProbeSettings probeSettings,
            ref Vector3 currentMoveDirection,
            ref float lastTurnLimitedAt)
        {
            if (body == null)
                return;

            Vector3 currentPosition = body.position;
            Vector3 desiredDirection = Vector3.ProjectOnPlane(targetPosition - currentPosition, Vector3.up);
            if (desiredDirection.sqrMagnitude < 0.0001f)
                return;

            MoveInDirection(
                body,
                bodyTransform,
                desiredDirection.normalized,
                moveSpeed,
                steeringIgnoreRoot,
                desiredDirection.normalized,
                prioritizeFacingTarget,
                settings,
                probeSettings,
                ref currentMoveDirection,
                ref lastTurnLimitedAt);
        }

        internal static void MoveInDirection(
            Rigidbody body,
            Transform bodyTransform,
            Vector3 direction,
            float moveSpeed,
            Transform steeringIgnoreRoot,
            Vector3 lookDirectionOverride,
            bool prioritizeFacingLookDirection,
            MotorSettings settings,
            ProbeSettings probeSettings,
            ref Vector3 currentMoveDirection,
            ref float lastTurnLimitedAt)
        {
            if (body == null)
                return;

            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (planarDirection.sqrMagnitude < 0.0001f)
                return;

            Vector3 avoidanceDirection = ResolveObstacleAvoidance(planarDirection.normalized, steeringIgnoreRoot, probeSettings);
            Vector3 finalDirection = GetSmoothedMoveDirection(avoidanceDirection, settings.InitialMoveDirection, settings.DirectionSmoothing, ref currentMoveDirection);
            Vector3 currentPosition = body.position;
            Vector3 nextPosition = currentPosition + finalDirection * Mathf.Max(0f, moveSpeed) * Time.fixedDeltaTime;
            nextPosition.y = settings.GroundY;

            Vector3 planarLookDirection = Vector3.ProjectOnPlane(lookDirectionOverride, Vector3.up);
            if (planarLookDirection.sqrMagnitude < 0.0001f)
                planarLookDirection = finalDirection;
            else
                planarLookDirection.Normalize();

            MoveRigidBody(body, bodyTransform, nextPosition, planarLookDirection, prioritizeFacingLookDirection, settings, ref lastTurnLimitedAt);
        }

        internal static void MoveRigidBody(
            Rigidbody body,
            Transform bodyTransform,
            Vector3 nextPosition,
            Vector3 lookDirection,
            bool prioritizeFacingLookDirection,
            MotorSettings settings,
            ref float lastTurnLimitedAt)
        {
            if (body == null)
                return;

            Vector3 currentPosition = body.position;
            Vector3 planarLookDirection = Vector3.ProjectOnPlane(lookDirection, Vector3.up);
            if (planarLookDirection.sqrMagnitude < 0.0001f)
                planarLookDirection = bodyTransform != null ? bodyTransform.forward : Vector3.forward;
            else
                planarLookDirection.Normalize();

            if (prioritizeFacingLookDirection)
            {
                float translationScale = GetPursuitTranslationScale(
                    body,
                    bodyTransform,
                    planarLookDirection,
                    settings.PursuitTurnSlowdownAngle,
                    settings.PursuitTurnInPlaceAngle);
                if (translationScale <= settings.TurnLimitedTranslationScaleThreshold)
                    lastTurnLimitedAt = Time.time;

                if (translationScale < 0.999f)
                {
                    Vector3 translation = nextPosition - currentPosition;
                    nextPosition = currentPosition + translation * translationScale;
                    nextPosition.y = settings.GroundY;
                }
            }

            body.MovePosition(nextPosition);

            Quaternion targetRotation = Quaternion.LookRotation(planarLookDirection, Vector3.up);
            Quaternion nextRotation = Quaternion.Slerp(body.rotation, targetRotation, Mathf.Clamp01(settings.RotationSpeed * Time.fixedDeltaTime));
            body.MoveRotation(nextRotation);
        }

        internal static float GetPursuitTranslationScale(
            Rigidbody body,
            Transform bodyTransform,
            Vector3 planarLookDirection,
            float pursuitTurnSlowdownAngle,
            float pursuitTurnInPlaceAngle)
        {
            if (body == null)
                return 1f;

            Vector3 currentForward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up);
            if (currentForward.sqrMagnitude < 0.0001f && bodyTransform != null)
                currentForward = Vector3.ProjectOnPlane(bodyTransform.forward, Vector3.up);

            if (currentForward.sqrMagnitude < 0.0001f)
                return 1f;

            currentForward.Normalize();
            float angleToLookDirection = Vector3.Angle(currentForward, planarLookDirection);
            if (angleToLookDirection >= pursuitTurnInPlaceAngle)
                return 0f;

            if (angleToLookDirection <= pursuitTurnSlowdownAngle)
                return 1f;

            return 1f - Mathf.InverseLerp(pursuitTurnSlowdownAngle, pursuitTurnInPlaceAngle, angleToLookDirection);
        }

        internal static Vector3 ResolveObstacleAvoidance(Vector3 desiredDirection, Transform ignoreRoot, ProbeSettings probeSettings)
        {
            if (!ProbeDirection(desiredDirection, ignoreRoot, probeSettings, out _))
                return desiredDirection.normalized;

            Vector3 leftDirection = Quaternion.Euler(0f, -probeSettings.AvoidanceAngle, 0f) * desiredDirection;
            Vector3 rightDirection = Quaternion.Euler(0f, probeSettings.AvoidanceAngle, 0f) * desiredDirection;

            bool leftBlocked = ProbeDirection(leftDirection, ignoreRoot, probeSettings, out float leftClearance);
            bool rightBlocked = ProbeDirection(rightDirection, ignoreRoot, probeSettings, out float rightClearance);

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

        internal static bool ProbeDirection(
            Vector3 direction,
            Transform ignoreRoot,
            ProbeSettings probeSettings,
            out float closestHitDistance)
        {
            closestHitDistance = Mathf.Max(0.01f, probeSettings.Distance);

            Vector3 normalizedDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (normalizedDirection.sqrMagnitude < 0.0001f)
                return false;

            normalizedDirection.Normalize();

            int hitCount = Physics.SphereCastNonAlloc(
                probeSettings.SensorPosition,
                Mathf.Max(0.05f, probeSettings.Radius),
                normalizedDirection,
                probeSettings.Hits,
                Mathf.Max(0.05f, probeSettings.Distance),
                probeSettings.ObstacleMask,
                QueryTriggerInteraction.Ignore);

            bool blocked = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = probeSettings.Hits[i].collider;
                if (hitCollider == null)
                    continue;

                if (IsOwnCollider(hitCollider, probeSettings.OwnerTransform))
                    continue;

                Transform hitRoot = hitCollider.transform.root;
                if (ignoreRoot != null && hitRoot == ignoreRoot)
                    continue;

                if (hitCollider.isTrigger)
                    continue;

                if (probeSettings.Hits[i].distance >= closestHitDistance)
                    continue;

                closestHitDistance = probeSettings.Hits[i].distance;
                blocked = true;
            }

            return blocked;
        }

        internal static bool IsOwnCollider(Collider candidate, Transform ownerTransform)
        {
            if (candidate == null || ownerTransform == null)
                return false;

            Transform candidateTransform = candidate.transform;
            return candidateTransform == ownerTransform || candidateTransform.IsChildOf(ownerTransform);
        }

        internal static Vector3 GetSmoothedMoveDirection(
            Vector3 desiredDirection,
            Vector3 initialMoveDirection,
            float directionSmoothing,
            ref Vector3 currentMoveDirection)
        {
            Vector3 planarDesiredDirection = Vector3.ProjectOnPlane(desiredDirection, Vector3.up);
            if (planarDesiredDirection.sqrMagnitude < 0.0001f)
                return currentMoveDirection.sqrMagnitude > 0.0001f ? currentMoveDirection : initialMoveDirection;

            planarDesiredDirection.Normalize();

            if (currentMoveDirection.sqrMagnitude < 0.0001f)
            {
                currentMoveDirection = planarDesiredDirection;
                return currentMoveDirection;
            }

            float blend = Mathf.Clamp01(directionSmoothing * Time.fixedDeltaTime);
            currentMoveDirection = Vector3.Slerp(currentMoveDirection, planarDesiredDirection, blend).normalized;
            return currentMoveDirection;
        }

        internal readonly struct MotorSettings
        {
            public MotorSettings(
                float groundY,
                float rotationSpeed,
                float directionSmoothing,
                float pursuitTurnSlowdownAngle,
                float pursuitTurnInPlaceAngle,
                float turnLimitedTranslationScaleThreshold,
                Vector3 initialMoveDirection)
            {
                GroundY = groundY;
                RotationSpeed = rotationSpeed;
                DirectionSmoothing = directionSmoothing;
                PursuitTurnSlowdownAngle = pursuitTurnSlowdownAngle;
                PursuitTurnInPlaceAngle = pursuitTurnInPlaceAngle;
                TurnLimitedTranslationScaleThreshold = turnLimitedTranslationScaleThreshold;
                InitialMoveDirection = initialMoveDirection;
            }

            public float GroundY { get; }
            public float RotationSpeed { get; }
            public float DirectionSmoothing { get; }
            public float PursuitTurnSlowdownAngle { get; }
            public float PursuitTurnInPlaceAngle { get; }
            public float TurnLimitedTranslationScaleThreshold { get; }
            public Vector3 InitialMoveDirection { get; }
        }

        internal readonly struct ProbeSettings
        {
            public ProbeSettings(
                Vector3 sensorPosition,
                float radius,
                float distance,
                float avoidanceAngle,
                LayerMask obstacleMask,
                RaycastHit[] hits,
                Transform ownerTransform)
            {
                SensorPosition = sensorPosition;
                Radius = radius;
                Distance = distance;
                AvoidanceAngle = avoidanceAngle;
                ObstacleMask = obstacleMask;
                Hits = hits;
                OwnerTransform = ownerTransform;
            }

            public Vector3 SensorPosition { get; }
            public float Radius { get; }
            public float Distance { get; }
            public float AvoidanceAngle { get; }
            public LayerMask ObstacleMask { get; }
            public RaycastHit[] Hits { get; }
            public Transform OwnerTransform { get; }
        }
    }
}
