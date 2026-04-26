using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        void ResolveReferences(bool ensureGeneratedColliders = true)
        {
            if (m_VisualRoot == null)
            {
                Transform visualRoot = transform.Find("VisualRoot");
                if (visualRoot != null)
                    m_VisualRoot = visualRoot;
            }

            if (m_SensorOrigin == null)
            {
                Transform sensorOrigin = transform.Find("VisualRoot/DangerEye");
                if (sensorOrigin == null)
                    sensorOrigin = transform.Find("DangerEye");

                if (sensorOrigin != null)
                    m_SensorOrigin = sensorOrigin;
            }

            if (m_Rigidbody == null)
                m_Rigidbody = GetComponent<Rigidbody>();

            if (ensureGeneratedColliders)
            {
                EnsurePulseAttachSurface();
                EnsureSolidBodyCollider();
            }

            m_SelfColliders = GetComponentsInChildren<Collider>(true);

            if (m_GridNavigator == null)
                m_GridNavigator = FindObjectOfType<MazeGridNavigator>();

            if (m_NavigationGraph == null)
                m_NavigationGraph = FindObjectOfType<MazeNavigationGraph>();
        }

        void EnsureSolidBodyCollider()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger)
                    return;
            }

            Transform bodyBlocker = transform.Find(BodyBlockerName);
            if (bodyBlocker == null)
            {
                var bodyObject = new GameObject(BodyBlockerName);
                bodyBlocker = bodyObject.transform;
                bodyBlocker.SetParent(transform, false);
            }

            SphereCollider bodyCollider = bodyBlocker.GetComponent<SphereCollider>();
            if (bodyCollider == null)
                bodyCollider = bodyBlocker.gameObject.AddComponent<SphereCollider>();

            bodyCollider.isTrigger = false;
            bodyCollider.radius = DefaultBodyColliderRadius;
            bodyCollider.center = s_DefaultBodyColliderCenter;
        }

        void EnsurePulseAttachSurface()
        {
            Transform attachParent = m_VisualRoot != null ? m_VisualRoot : transform;
            Transform attachSurface = attachParent.Find(PulseAttachSurfaceName);
            bool createdAttachSurface = false;
            if (attachSurface == null)
            {
                var attachObject = new GameObject(PulseAttachSurfaceName);
                attachSurface = attachObject.transform;
                attachSurface.SetParent(attachParent, false);
                createdAttachSurface = true;
            }

            if (createdAttachSurface)
            {
                attachSurface.localPosition = Vector3.zero;
                attachSurface.localRotation = Quaternion.identity;
                attachSurface.localScale = Vector3.one;
            }

            MeshFilter sourceMeshFilter = ResolvePulseAttachSourceMeshFilter();
            Mesh sourceMesh = sourceMeshFilter != null ? sourceMeshFilter.sharedMesh : null;
            if (sourceMesh != null)
            {
                AlignPulseAttachSurfaceToSourceMesh(attachSurface, sourceMeshFilter.transform);

                if (CanUsePulseAttachMeshCollider(sourceMesh))
                {
                    RemovePulseAttachFallbackCollider(attachSurface);

                    MeshCollider meshCollider = attachSurface.GetComponent<MeshCollider>();
                    if (meshCollider == null)
                        meshCollider = attachSurface.gameObject.AddComponent<MeshCollider>();

                    meshCollider.sharedMesh = sourceMesh;
                    meshCollider.convex = true;
                    meshCollider.isTrigger = true;
                    return;
                }

                RemovePulseAttachMeshCollider(attachSurface);
            }

            MeshCollider existingMeshCollider = attachSurface.GetComponent<MeshCollider>();
            if (existingMeshCollider != null)
                existingMeshCollider.isTrigger = true;

            SphereCollider surfaceCollider = attachSurface.GetComponent<SphereCollider>();
            if (surfaceCollider == null)
            {
                surfaceCollider = attachSurface.gameObject.AddComponent<SphereCollider>();
                surfaceCollider.radius = DefaultPulseAttachSurfaceRadius;
                surfaceCollider.center = Vector3.zero;
            }

            surfaceCollider.isTrigger = true;
        }

        bool CanUsePulseAttachMeshCollider(Mesh mesh)
        {
            if (mesh == null)
                return false;

            int triangleCount = mesh.triangles != null ? mesh.triangles.Length / 3 : 0;
            return triangleCount > 0 && triangleCount <= MaxPulseAttachMeshTrianglesForConvexCollider;
        }

        MeshFilter ResolvePulseAttachSourceMeshFilter()
        {
            if (m_VisualRoot == null)
                return null;

            MeshFilter[] meshFilters = m_VisualRoot.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter bestFilter = null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                Transform meshTransform = meshFilter.transform;
                if (string.Equals(meshTransform.name, PulseAttachSurfaceName, System.StringComparison.Ordinal))
                    continue;

                if (m_SensorOrigin != null && (meshTransform == m_SensorOrigin || meshTransform.IsChildOf(m_SensorOrigin)))
                    continue;

                Renderer renderer = meshFilter.GetComponent<Renderer>();
                Vector3 worldSize = renderer != null
                    ? renderer.bounds.size
                    : Vector3.Scale(meshFilter.sharedMesh.bounds.size, AbsVector(meshTransform.lossyScale));
                float score = worldSize.x * worldSize.y * worldSize.z;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestFilter = meshFilter;
            }

            return bestFilter;
        }

        void AlignPulseAttachSurfaceToSourceMesh(Transform attachSurface, Transform sourceTransform)
        {
            if (attachSurface == null || sourceTransform == null)
                return;

            attachSurface.position = sourceTransform.position;
            attachSurface.rotation = sourceTransform.rotation;

            Transform attachParent = attachSurface.parent;
            if (attachParent == null)
            {
                attachSurface.localScale = sourceTransform.lossyScale;
                return;
            }

            attachSurface.localScale = DivideScale(sourceTransform.lossyScale, attachParent.lossyScale);
        }

        void RemovePulseAttachFallbackCollider(Transform attachSurface)
        {
            if (attachSurface == null)
                return;

            SphereCollider sphereCollider = attachSurface.GetComponent<SphereCollider>();
            if (sphereCollider == null)
                return;

            if (Application.isPlaying)
                Destroy(sphereCollider);
            else
                DestroyImmediate(sphereCollider);
        }

        void RemovePulseAttachMeshCollider(Transform attachSurface)
        {
            if (attachSurface == null)
                return;

            MeshCollider meshCollider = attachSurface.GetComponent<MeshCollider>();
            if (meshCollider == null)
                return;

            if (Application.isPlaying)
                Destroy(meshCollider);
            else
                DestroyImmediate(meshCollider);
        }

        static Vector3 AbsVector(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        static Vector3 DivideScale(Vector3 numerator, Vector3 denominator)
        {
            return new Vector3(
                SafeDivideScaleComponent(numerator.x, denominator.x),
                SafeDivideScaleComponent(numerator.y, denominator.y),
                SafeDivideScaleComponent(numerator.z, denominator.z));
        }

        static float SafeDivideScaleComponent(float numerator, float denominator)
        {
            if (Mathf.Abs(denominator) < 0.0001f)
                return numerator;

            return numerator / denominator;
        }

        void ConfigureRigidbody()
        {
            if (m_Rigidbody == null)
                return;

            m_Rigidbody.isKinematic = false;
            m_Rigidbody.useGravity = false;
            m_Rigidbody.mass = Mathf.Max(1f, m_PhysicsMass);
            m_Rigidbody.drag = Mathf.Max(0f, m_ExternalPushDrag);
            m_Rigidbody.angularDrag = Mathf.Max(0f, m_ExternalPushAngularDrag);
            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            m_Rigidbody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        void ApplySharedDefaults()
        {
            if (m_OverrideSharedDefaults)
                return;

            EnemyPatrolTuningProfile sharedProfile = EnemyPatrolSharedSettings.ResolveActiveProfile();
            m_PatrolSpeed = sharedProfile.PatrolSpeed;
            m_ChaseSpeed = sharedProfile.ChaseSpeed;
            m_PhysicsMass = sharedProfile.PhysicsMass;
            m_ExternalPushDrag = sharedProfile.ExternalPushDrag;
            m_ExternalPushAngularDrag = sharedProfile.ExternalPushAngularDrag;
            m_ClosePursuitDistance = sharedProfile.ClosePursuitDistance;
            m_ClosePursuitModuleDistance = sharedProfile.ClosePursuitModuleDistance;
            m_RotationSpeed = sharedProfile.RotationSpeed;
            m_DirectionSmoothing = sharedProfile.DirectionSmoothing;
            m_EyeTrackingSmoothing = sharedProfile.EyeTrackingSmoothing;
            m_DetectionRange = sharedProfile.DetectionRange;
            m_PursuitLockDistance = sharedProfile.PursuitLockDistance;
            m_OccludedSoftLockDistance = sharedProfile.OccludedSoftLockDistance;
            m_PursuitTurnSlowdownAngle = sharedProfile.PursuitTurnSlowdownAngle;
            m_PursuitTurnInPlaceAngle = sharedProfile.PursuitTurnInPlaceAngle;
            m_FieldOfViewDegrees = sharedProfile.FieldOfViewDegrees;
            m_LoseSightGraceDuration = sharedProfile.LoseSightGraceDuration;
            m_SearchWaitDuration = sharedProfile.SearchWaitDuration;
            m_SearchSweepAngle = sharedProfile.SearchSweepAngle;
            m_SearchSweepFrequency = sharedProfile.SearchSweepFrequency;
            m_PatrolRadius = sharedProfile.PatrolRadius;
            m_DirectionChangeIntervalMin = sharedProfile.DirectionChangeIntervalMin;
            m_DirectionChangeIntervalMax = sharedProfile.DirectionChangeIntervalMax;
            m_AnchorPullDistance = sharedProfile.AnchorPullDistance;
            m_ObstacleProbeDistance = sharedProfile.ObstacleProbeDistance;
            m_ObstacleProbeRadius = sharedProfile.ObstacleProbeRadius;
            m_ObstacleProbeHeight = sharedProfile.ObstacleProbeHeight;
            m_ObstacleAvoidanceAngle = sharedProfile.ObstacleAvoidanceAngle;
            m_ObstacleMask = sharedProfile.ObstacleMask;
            m_BobAmplitude = sharedProfile.BobAmplitude;
            m_BobFrequency = sharedProfile.BobFrequency;
            m_EyeOrbitRadius = sharedProfile.EyeOrbitRadius;
            m_EyeOrbitHeight = sharedProfile.EyeOrbitHeight;
            m_EyeVerticalLead = sharedProfile.EyeVerticalLead;
            m_CorridorCenteringSpeed = sharedProfile.CorridorCenteringSpeed;
            m_PathNodeArrivalDistance = sharedProfile.PathNodeArrivalDistance;
        }

        void ClampConfiguration()
        {
            m_PatrolSpeed = Mathf.Max(0f, m_PatrolSpeed);
            m_ChaseSpeed = Mathf.Max(0f, m_ChaseSpeed);
            m_PhysicsMass = Mathf.Max(1f, m_PhysicsMass);
            m_ExternalPushDrag = Mathf.Max(0f, m_ExternalPushDrag);
            m_ExternalPushAngularDrag = Mathf.Max(0f, m_ExternalPushAngularDrag);
            m_ClosePursuitDistance = Mathf.Max(0.25f, m_ClosePursuitDistance);
            m_ClosePursuitDistanceHysteresis = Mathf.Max(0f, m_ClosePursuitDistanceHysteresis);
            m_ClosePursuitModuleDistance = Mathf.Max(0, m_ClosePursuitModuleDistance);
            m_RotationSpeed = Mathf.Max(0f, m_RotationSpeed);
            m_DirectionSmoothing = Mathf.Max(0.1f, m_DirectionSmoothing);
            m_EyeTrackingSmoothing = Mathf.Max(0.1f, m_EyeTrackingSmoothing);
            m_DetectionRange = Mathf.Max(0.1f, m_DetectionRange);
            m_PursuitLockDistance = Mathf.Max(0f, m_PursuitLockDistance);
            m_OccludedSoftLockDistance = Mathf.Max(0f, m_OccludedSoftLockDistance);
            m_PursuitTurnSlowdownAngle = Mathf.Clamp(m_PursuitTurnSlowdownAngle, 0f, 170f);
            m_PursuitTurnInPlaceAngle = Mathf.Clamp(m_PursuitTurnInPlaceAngle, Mathf.Max(10f, m_PursuitTurnSlowdownAngle + 1f), 180f);
            m_FieldOfViewDegrees = Mathf.Clamp(m_FieldOfViewDegrees, 10f, 360f);
            m_LoseSightGraceDuration = Mathf.Max(0f, m_LoseSightGraceDuration);
            m_SearchWaitDuration = Mathf.Max(0f, m_SearchWaitDuration);
            m_SearchSweepAngle = Mathf.Clamp(m_SearchSweepAngle, 5f, 180f);
            m_SearchSweepFrequency = Mathf.Max(0.05f, m_SearchSweepFrequency);
            m_PatrolRadius = Mathf.Max(0.5f, m_PatrolRadius);
            m_DirectionChangeIntervalMin = Mathf.Max(0.1f, m_DirectionChangeIntervalMin);
            m_DirectionChangeIntervalMax = Mathf.Max(m_DirectionChangeIntervalMin, m_DirectionChangeIntervalMax);
            m_AnchorPullDistance = Mathf.Max(0.5f, m_AnchorPullDistance);
            m_ObstacleProbeDistance = Mathf.Max(0.1f, m_ObstacleProbeDistance);
            m_ObstacleProbeRadius = Mathf.Clamp(m_ObstacleProbeRadius, 0.05f, 2f);
            m_ObstacleProbeHeight = Mathf.Clamp(m_ObstacleProbeHeight, 0.2f, 3f);
            m_ObstacleAvoidanceAngle = Mathf.Clamp(m_ObstacleAvoidanceAngle, 5f, 85f);
            m_BobAmplitude = Mathf.Max(0f, m_BobAmplitude);
            m_BobFrequency = Mathf.Max(0.01f, m_BobFrequency);
            m_EyeOrbitRadius = Mathf.Clamp(m_EyeOrbitRadius, 0.05f, 2f);
            m_EyeOrbitHeight = Mathf.Clamp(m_EyeOrbitHeight, 0.05f, 3f);
            m_EyeVerticalLead = Mathf.Clamp(m_EyeVerticalLead, -0.5f, 0.5f);
            m_CorridorCenteringSpeed = Mathf.Max(0.1f, m_CorridorCenteringSpeed);
            m_PathNodeArrivalDistance = Mathf.Clamp(m_PathNodeArrivalDistance, 0.05f, 2f);
            m_SearchArrivalDistance = Mathf.Clamp(m_SearchArrivalDistance, m_PathNodeArrivalDistance, 2f);
            m_StuckProgressDistance = Mathf.Max(0.01f, m_StuckProgressDistance);
            m_StuckRecoveryDelay = Mathf.Max(0.25f, m_StuckRecoveryDelay);
            m_StuckSoftRecoveryAttempts = Mathf.Max(0, m_StuckSoftRecoveryAttempts);
            m_StuckTurnInPlaceGraceDuration = Mathf.Max(0f, m_StuckTurnInPlaceGraceDuration);
            m_PathFailureRetryDelay = Mathf.Max(0f, m_PathFailureRetryDelay);
            m_ReachabilityCacheDuration = Mathf.Max(0f, m_ReachabilityCacheDuration);
            m_DebugLogMinInterval = Mathf.Max(0.05f, m_DebugLogMinInterval);
        }
    }
}
