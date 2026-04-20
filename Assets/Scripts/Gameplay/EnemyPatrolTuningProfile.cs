using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [System.Serializable]
    public struct EnemyPatrolTuningProfile
    {
        public float PatrolSpeed;
        public float ChaseSpeed;
        public float PhysicsMass;
        public float ExternalPushDrag;
        public float ExternalPushAngularDrag;
        public float ClosePursuitDistance;
        public int ClosePursuitModuleDistance;
        public float RotationSpeed;
        public float DirectionSmoothing;
        public float EyeTrackingSmoothing;
        public float DetectionRange;
        public float PursuitLockDistance;
        public float OccludedSoftLockDistance;
        public float PursuitTurnSlowdownAngle;
        public float PursuitTurnInPlaceAngle;
        public float FieldOfViewDegrees;
        public float LoseSightGraceDuration;
        public float SearchWaitDuration;
        public float SearchSweepAngle;
        public float SearchSweepFrequency;
        public float PatrolRadius;
        public float DirectionChangeIntervalMin;
        public float DirectionChangeIntervalMax;
        public float AnchorPullDistance;
        public float ObstacleProbeDistance;
        public float ObstacleProbeRadius;
        public float ObstacleProbeHeight;
        public float ObstacleAvoidanceAngle;
        public LayerMask ObstacleMask;
        public float BobAmplitude;
        public float BobFrequency;
        public float EyeOrbitRadius;
        public float EyeOrbitHeight;
        public float EyeVerticalLead;
        public float CorridorCenteringSpeed;
        public float PathNodeArrivalDistance;

        public static EnemyPatrolTuningProfile CreateDefault()
        {
            return new EnemyPatrolTuningProfile
            {
                PatrolSpeed = 0.7f,
                ChaseSpeed = 1.1f,
                PhysicsMass = 250f,
                ExternalPushDrag = 10f,
                ExternalPushAngularDrag = 10f,
                ClosePursuitDistance = 4.75f,
                ClosePursuitModuleDistance = 1,
                RotationSpeed = 14f,
                DirectionSmoothing = 14f,
                EyeTrackingSmoothing = 14f,
                DetectionRange = 12f,
                PursuitLockDistance = 6f,
                OccludedSoftLockDistance = 6f,
                PursuitTurnSlowdownAngle = 55f,
                PursuitTurnInPlaceAngle = 110f,
                FieldOfViewDegrees = 160f,
                LoseSightGraceDuration = 1.6f,
                SearchWaitDuration = 5f,
                SearchSweepAngle = 60f,
                SearchSweepFrequency = 0.75f,
                PatrolRadius = 5.25f,
                DirectionChangeIntervalMin = 1.2f,
                DirectionChangeIntervalMax = 3f,
                AnchorPullDistance = 4.1f,
                ObstacleProbeDistance = 1.2f,
                ObstacleProbeRadius = 0.36f,
                ObstacleProbeHeight = 1.18f,
                ObstacleAvoidanceAngle = 58f,
                ObstacleMask = Physics.DefaultRaycastLayers,
                BobAmplitude = 0.06f,
                BobFrequency = 2.3f,
                EyeOrbitRadius = 0.52f,
                EyeOrbitHeight = 0.744f,
                EyeVerticalLead = 0.12f,
                CorridorCenteringSpeed = 3.1f,
                PathNodeArrivalDistance = 0.4f,
            };
        }

        public EnemyPatrolTuningProfile GetSanitized()
        {
            EnemyPatrolTuningProfile sanitized = this;
            sanitized.PatrolSpeed = Mathf.Max(0f, sanitized.PatrolSpeed);
            sanitized.ChaseSpeed = Mathf.Max(0f, sanitized.ChaseSpeed);
            sanitized.PhysicsMass = Mathf.Max(1f, sanitized.PhysicsMass);
            sanitized.ExternalPushDrag = Mathf.Max(0f, sanitized.ExternalPushDrag);
            sanitized.ExternalPushAngularDrag = Mathf.Max(0f, sanitized.ExternalPushAngularDrag);
            sanitized.ClosePursuitDistance = Mathf.Max(0f, sanitized.ClosePursuitDistance);
            sanitized.ClosePursuitModuleDistance = Mathf.Max(0, sanitized.ClosePursuitModuleDistance);
            sanitized.RotationSpeed = Mathf.Max(0f, sanitized.RotationSpeed);
            sanitized.DirectionSmoothing = Mathf.Max(0f, sanitized.DirectionSmoothing);
            sanitized.EyeTrackingSmoothing = Mathf.Max(0f, sanitized.EyeTrackingSmoothing);
            sanitized.DetectionRange = Mathf.Max(0.1f, sanitized.DetectionRange);
            sanitized.PursuitLockDistance = Mathf.Max(0f, sanitized.PursuitLockDistance);
            sanitized.OccludedSoftLockDistance = Mathf.Max(0f, sanitized.OccludedSoftLockDistance);
            sanitized.PursuitTurnSlowdownAngle = Mathf.Clamp(sanitized.PursuitTurnSlowdownAngle, 0f, 180f);
            sanitized.PursuitTurnInPlaceAngle = Mathf.Clamp(sanitized.PursuitTurnInPlaceAngle, 10f, 180f);
            sanitized.FieldOfViewDegrees = Mathf.Clamp(sanitized.FieldOfViewDegrees, 10f, 360f);
            sanitized.LoseSightGraceDuration = Mathf.Max(0f, sanitized.LoseSightGraceDuration);
            sanitized.SearchWaitDuration = Mathf.Max(0f, sanitized.SearchWaitDuration);
            sanitized.SearchSweepAngle = Mathf.Clamp(sanitized.SearchSweepAngle, 10f, 180f);
            sanitized.SearchSweepFrequency = Mathf.Max(0f, sanitized.SearchSweepFrequency);
            sanitized.PatrolRadius = Mathf.Max(0f, sanitized.PatrolRadius);
            sanitized.DirectionChangeIntervalMin = Mathf.Max(0f, sanitized.DirectionChangeIntervalMin);
            sanitized.DirectionChangeIntervalMax = Mathf.Max(sanitized.DirectionChangeIntervalMin, sanitized.DirectionChangeIntervalMax);
            sanitized.AnchorPullDistance = Mathf.Max(0f, sanitized.AnchorPullDistance);
            sanitized.ObstacleProbeDistance = Mathf.Max(0.05f, sanitized.ObstacleProbeDistance);
            sanitized.ObstacleProbeRadius = Mathf.Max(0.01f, sanitized.ObstacleProbeRadius);
            sanitized.ObstacleProbeHeight = Mathf.Max(0f, sanitized.ObstacleProbeHeight);
            sanitized.ObstacleAvoidanceAngle = Mathf.Clamp(sanitized.ObstacleAvoidanceAngle, 5f, 85f);
            sanitized.BobAmplitude = Mathf.Max(0f, sanitized.BobAmplitude);
            sanitized.BobFrequency = Mathf.Max(0f, sanitized.BobFrequency);
            sanitized.EyeOrbitRadius = Mathf.Max(0f, sanitized.EyeOrbitRadius);
            sanitized.EyeOrbitHeight = Mathf.Max(0f, sanitized.EyeOrbitHeight);
            sanitized.EyeVerticalLead = Mathf.Max(0f, sanitized.EyeVerticalLead);
            sanitized.CorridorCenteringSpeed = Mathf.Max(0f, sanitized.CorridorCenteringSpeed);
            sanitized.PathNodeArrivalDistance = Mathf.Max(0.01f, sanitized.PathNodeArrivalDistance);
            return sanitized;
        }
    }
}
