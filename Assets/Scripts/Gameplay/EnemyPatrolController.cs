using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyPatrolController : MonoBehaviour
    {
        static readonly HashSet<EnemyPatrolController> s_ActiveControllers = new();
        static float s_GlobalDetectionRangeReductionMeters;
        static float s_GlobalFieldOfViewReductionDegrees;
        static float s_GlobalDetectionSuppressedUntil = float.NegativeInfinity;
        static readonly Vector3 s_DefaultBodyColliderCenter = new(0f, 0.783f, 0f);
        const float DefaultBodyColliderRadius = 0.48f;
        const string BodyBlockerName = "BodyBlocker";
        const string PulseAttachSurfaceName = "PulseAttachSurface";
        const float DefaultPulseAttachSurfaceRadius = 0.42f;
        const int MaxPulseAttachMeshTrianglesForConvexCollider = 240;

        enum EnemyState
        {
            Patrol,
            Chase,
            Search,
        }

        [SerializeField] MazeGridNavigator m_GridNavigator;
        [SerializeField] MazeNavigationGraph m_NavigationGraph;
        [SerializeField] Transform m_VisualRoot;
        [SerializeField] Transform m_SensorOrigin;
        [SerializeField] Transform m_RoamAnchor;
        [SerializeField] Vector3 m_VisualLocalOffset = new(0f, 1.05f, 0f);
        [SerializeField] bool m_OverrideSharedDefaults;
        [SerializeField] float m_PatrolSpeed = 0.7f;
        [SerializeField] float m_ChaseSpeed = 1.1f;
        [SerializeField, Min(1f)] float m_PhysicsMass = 250f;
        [SerializeField, Min(0f)] float m_ExternalPushDrag = 10f;
        [SerializeField, Min(0f)] float m_ExternalPushAngularDrag = 10f;
        [SerializeField] float m_ClosePursuitDistance = 4.75f;
        [SerializeField, Min(0)] int m_ClosePursuitModuleDistance = 1;
        [SerializeField] float m_RotationSpeed = 14f;
        [SerializeField] float m_DirectionSmoothing = 14f;
        [SerializeField] float m_EyeTrackingSmoothing = 14f;
        [SerializeField] float m_DetectionRange = 12f;
        [SerializeField] float m_PursuitLockDistance = 6f;
        [SerializeField] float m_OccludedSoftLockDistance = 6f;
        [SerializeField, Range(0f, 180f)] float m_PursuitTurnSlowdownAngle = 55f;
        [SerializeField, Range(10f, 180f)] float m_PursuitTurnInPlaceAngle = 110f;
        [SerializeField, Range(10f, 360f)] float m_FieldOfViewDegrees = 160f;
        [SerializeField] float m_LoseSightGraceDuration = 1.6f;
        [SerializeField] float m_SearchWaitDuration = 5f;
        [SerializeField, Range(10f, 180f)] float m_SearchSweepAngle = 60f;
        [SerializeField] float m_SearchSweepFrequency = 0.75f;
        [SerializeField] float m_PatrolRadius = 5.25f;
        [SerializeField] float m_DirectionChangeIntervalMin = 1.2f;
        [SerializeField] float m_DirectionChangeIntervalMax = 3f;
        [SerializeField] float m_AnchorPullDistance = 4.1f;
        [SerializeField] float m_ObstacleProbeDistance = 1.2f;
        [SerializeField] float m_ObstacleProbeRadius = 0.36f;
        [SerializeField] float m_ObstacleProbeHeight = 1.18f;
        [SerializeField, Range(5f, 85f)] float m_ObstacleAvoidanceAngle = 58f;
        [SerializeField] LayerMask m_ObstacleMask = default;
        [SerializeField] float m_BobAmplitude = 0.06f;
        [SerializeField] float m_BobFrequency = 2.3f;
        [SerializeField] float m_EyeOrbitRadius = 0.52f;
        [SerializeField] float m_EyeOrbitHeight = 0.744f;
        [SerializeField] float m_EyeVerticalLead = 0.12f;
        [SerializeField] float m_CorridorCenteringSpeed = 3.1f;
        [SerializeField] float m_PathNodeArrivalDistance = 0.4f;
        [SerializeField, Min(0.01f)] float m_StuckProgressDistance = 0.08f;
        [SerializeField, Min(0.25f)] float m_StuckRecoveryDelay = 1.4f;

        readonly RaycastHit[] m_ProbeHits = new RaycastHit[8];
        readonly List<Vector2Int> m_GridPath = new(16);
        readonly List<Vector2Int> m_GridNeighbors = new(4);
        readonly List<Vector2Int> m_NavigationNeighbors = new(8);
        readonly List<int> m_NavigationNodePath = new(16);
        readonly List<Vector3> m_NavigationWaypointPath = new(16);
        readonly List<Vector3> m_LocalNavigationWaypoints = new(8);
        readonly List<Vector2Int> m_GridReachabilityPath = new(16);
        readonly List<int> m_NavigationReachabilityPath = new(16);

        Rigidbody m_Rigidbody;
        Collider[] m_SelfColliders;
        XROrigin m_PlayerRig;
        Transform m_PlayerView;
        EnemyState m_State;
        Vector3 m_SpawnPosition;
        Vector3 m_RoamCenter;
        Vector3 m_CurrentMoveDirection;
        Vector3 m_WanderDirection;
        Vector3 m_LastKnownPlayerPosition;
        Vector3 m_SearchTargetPosition;
        Vector3 m_SearchBaseLookDirection;
        Vector3 m_CurrentEyeLocalOffset;
        float m_NextDirectionChangeAt;
        float m_LastSeenPlayerAt = float.NegativeInfinity;
        float m_SearchWaitStartedAt = float.NegativeInfinity;
        float m_RuntimeDetectionRangeMultiplier = 1f;
        float m_RuntimePursuitLockDistanceMultiplier = 1f;
        float m_RuntimeFieldOfViewMultiplier = 1f;
        Vector2Int m_CurrentPathGoalCell;
        Vector2Int m_CurrentNavigationGoalCell;
        Vector2Int m_CurrentPatrolTargetCell;
        Vector2Int m_LastPatrolCell;
        Vector2Int m_LastGridSegmentStep;
        bool m_HasCurrentPathGoalCell;
        bool m_HasCurrentNavigationGoalCell;
        bool m_HasCurrentPatrolTargetCell;
        bool m_HasLastPatrolCell;
        bool m_HasLastGridSegmentStep;
        bool m_IsWaitingAtSearchPoint;
        Vector3 m_LastProgressPosition;
        float m_LastProgressAt;

        public bool IsChasing => m_State == EnemyState.Chase;
        public bool IsAlerted => m_State == EnemyState.Chase || m_State == EnemyState.Search;
        public float EffectiveDetectionRange => Mathf.Max(
            0.1f,
            Mathf.Max(0.1f, m_DetectionRange - Mathf.Max(0f, s_GlobalDetectionRangeReductionMeters))
            * Mathf.Max(0.1f, m_RuntimeDetectionRangeMultiplier));
        public float EffectivePursuitLockDistance => Mathf.Max(0f, m_PursuitLockDistance * Mathf.Max(0f, m_RuntimePursuitLockDistanceMultiplier));
        public float EffectiveOccludedSoftLockDistance => Mathf.Max(0f, m_OccludedSoftLockDistance * Mathf.Max(0f, m_RuntimePursuitLockDistanceMultiplier));
        public float EffectiveFieldOfViewDegrees => Mathf.Clamp(
            Mathf.Max(5f, m_FieldOfViewDegrees - Mathf.Max(0f, s_GlobalFieldOfViewReductionDegrees))
            * Mathf.Max(0.1f, m_RuntimeFieldOfViewMultiplier),
            5f,
            360f);
        bool CanUseGridNavigation => m_GridNavigator != null && m_GridNavigator.HasActiveLayout;
        bool CanUseNavigationGraph => m_NavigationGraph != null && m_NavigationGraph.NodeCount > 0;

        public static bool TryGetPulseAttachSurfaceOwner(Collider collider, out EnemyPatrolController owner)
        {
            owner = null;
            if (collider == null)
                return false;

            if (!string.Equals(collider.transform.name, PulseAttachSurfaceName, System.StringComparison.Ordinal))
                return false;

            owner = collider.GetComponentInParent<EnemyPatrolController>();
            return owner != null;
        }

        public static bool HasAlertedEnemy()
        {
            foreach (EnemyPatrolController controller in s_ActiveControllers)
            {
                if (controller != null && controller.isActiveAndEnabled && controller.IsAlerted)
                    return true;
            }

            return false;
        }

        public static bool HasChasingEnemy()
        {
            foreach (EnemyPatrolController controller in s_ActiveControllers)
            {
                if (controller != null && controller.isActiveAndEnabled && controller.IsChasing)
                    return true;
            }

            return false;
        }

        public static void NotifyMazeNavigationTopologyChanged()
        {
            foreach (EnemyPatrolController controller in s_ActiveControllers)
            {
                if (controller != null && controller.isActiveAndEnabled)
                    controller.HandleMazeNavigationTopologyChanged();
            }
        }

        public static void SetGlobalDetectionRangeReductionMeters(float reductionMeters)
        {
            s_GlobalDetectionRangeReductionMeters = Mathf.Max(0f, reductionMeters);
        }

        public static void SetGlobalFieldOfViewReductionDegrees(float reductionDegrees)
        {
            s_GlobalFieldOfViewReductionDegrees = Mathf.Max(0f, reductionDegrees);
        }

        public static void SetGlobalDetectionSuppressedDuration(float durationSeconds)
        {
            if (durationSeconds <= 0f)
            {
                s_GlobalDetectionSuppressedUntil = float.NegativeInfinity;
                return;
            }

            s_GlobalDetectionSuppressedUntil = Time.time + durationSeconds;
        }

        void Reset()
        {
            ResolveReferences();
            ConfigureRigidbody();
            ApplySharedDefaults();
            ClampConfiguration();
        }

        void Awake()
        {
            ResolveReferences();
            ConfigureRigidbody();
            ApplySharedDefaults();
            ClampConfiguration();
            ResolvePlayerReferences();

            m_SpawnPosition = transform.position;
            m_RoamCenter = ResolveRoamCenter();
            m_CurrentMoveDirection = GetInitialMoveDirection();
            m_CurrentEyeLocalOffset = GetDefaultEyeLocalOffset();
            MarkMovementProgress();
            PickNewPatrolDirection(forceAnchorBias: false);
        }

        void OnEnable()
        {
            s_ActiveControllers.Add(this);
            MarkMovementProgress();
        }

        void OnDisable()
        {
            s_ActiveControllers.Remove(this);
        }

        void OnValidate()
        {
            ResolveReferences(ensureGeneratedColliders: false);
            if (m_Rigidbody == null)
                m_Rigidbody = GetComponent<Rigidbody>();

            ConfigureRigidbody();
            ApplySharedDefaults();
            ClampConfiguration();
        }

        void Update()
        {
            if (m_VisualRoot == null)
                return;

            float bobOffset = Mathf.Sin(Time.time * Mathf.Max(0.01f, m_BobFrequency)) * Mathf.Max(0f, m_BobAmplitude);
            m_VisualRoot.localPosition = m_VisualLocalOffset + Vector3.up * bobOffset;
            UpdateEyeTracking();
        }

        void FixedUpdate()
        {
            ResolvePlayerReferences();
            UpdateAwareness();

            if (TryRecoverFromMovementStall())
                return;

            if (m_State == EnemyState.Chase)
            {
                if (TryUpdateNavigationGraphChase())
                    return;

                if (TryUpdateGridChase())
                    return;

                if (ShouldStopAtUnreachableMazeTarget(m_LastKnownPlayerPosition))
                {
                    BeginBlockedMazeSearchAtCurrentPosition();
                    return;
                }

                ClearNavigationPathState();
                ClearGridSegmentState();
                MoveTowards(
                    m_LastKnownPlayerPosition,
                    Mathf.Max(0f, m_ChaseSpeed),
                    ignoreRoot: m_PlayerRig != null ? m_PlayerRig.transform : null,
                    prioritizeFacingTarget: true);
                return;
            }

            if (m_State == EnemyState.Search)
            {
                if (TryUpdateSearch())
                    return;

                ExitSearchToPatrol();
                return;
            }

            if (TryUpdateNavigationGraphPatrol())
                return;

            if (TryUpdateGridPatrol())
                return;

            ClearGridSegmentState();
            UpdatePatrol();
        }

        public void SetDetectionRangeMultiplier(float multiplier)
        {
            m_RuntimeDetectionRangeMultiplier = Mathf.Clamp(multiplier, 0.1f, 4f);
        }

        public void SetPursuitLockDistanceMultiplier(float multiplier)
        {
            m_RuntimePursuitLockDistanceMultiplier = Mathf.Clamp(multiplier, 0f, 4f);
        }

        public void SetFieldOfViewMultiplier(float multiplier)
        {
            m_RuntimeFieldOfViewMultiplier = Mathf.Clamp(multiplier, 0.1f, 4f);
        }

        public void SetRoamCenter(Vector3 worldPosition)
        {
            m_RoamCenter = worldPosition;
        }

        public void FaceToward(Vector3 worldPosition)
        {
            Vector3 lookDirection = Vector3.ProjectOnPlane(worldPosition - transform.position, Vector3.up);
            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            lookDirection.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            transform.rotation = targetRotation;
            m_CurrentMoveDirection = lookDirection;

            if (m_Rigidbody == null)
                m_Rigidbody = GetComponent<Rigidbody>();

            if (m_Rigidbody != null)
            {
                m_Rigidbody.rotation = targetRotation;
                m_Rigidbody.velocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }
        }

        public void ForceChaseTarget(Vector3 worldPosition)
        {
            ResolvePlayerReferences();
            FaceToward(worldPosition);

            if (m_Rigidbody == null)
                m_Rigidbody = GetComponent<Rigidbody>();

            if (m_Rigidbody != null)
                m_SpawnPosition = m_Rigidbody.position;
            else
                m_SpawnPosition = transform.position;

            Vector3 targetGroundPosition = GetGroundPosition(worldPosition);
            m_State = EnemyState.Chase;
            m_LastKnownPlayerPosition = targetGroundPosition;
            m_LastSeenPlayerAt = Time.time;
            ResetSearchState();
            ClearGridNavigationState(keepPatrolTarget: false);
        }

        public void SetPatrolRadius(float radius)
        {
            m_PatrolRadius = Mathf.Max(0.5f, radius);
        }

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
            m_StuckProgressDistance = Mathf.Max(0.01f, m_StuckProgressDistance);
            m_StuckRecoveryDelay = Mathf.Max(0.25f, m_StuckRecoveryDelay);
        }

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
                    if (m_State == EnemyState.Chase)
                        BeginBlockedMazeSearchAtCurrentPosition();

                    return;
                }

                if (m_State != EnemyState.Chase)
                    ClearGridNavigationState(keepPatrolTarget: false);

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
                    if (m_State == EnemyState.Chase)
                        BeginBlockedMazeSearchAtCurrentPosition();

                    return;
                }

                if (m_State != EnemyState.Chase)
                    ClearGridNavigationState(keepPatrolTarget: false);

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

            if (!CanReachMazeTarget(GetGroundPosition(playerAnchorPosition)))
            {
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
            m_SearchBaseLookDirection = Vector3.ProjectOnPlane(m_LastKnownPlayerPosition - m_Rigidbody.position, Vector3.up);
            if (m_SearchBaseLookDirection.sqrMagnitude < 0.0001f)
                m_SearchBaseLookDirection = m_CurrentMoveDirection.sqrMagnitude > 0.0001f ? m_CurrentMoveDirection : transform.forward;

            m_SearchBaseLookDirection.Normalize();
            m_IsWaitingAtSearchPoint = false;
            m_SearchWaitStartedAt = float.NegativeInfinity;
            ClearGridNavigationState(keepPatrolTarget: false);
        }

        void ExitSearchToPatrol()
        {
            ResetSearchState();
            m_State = EnemyState.Patrol;
            ClearGridNavigationState(keepPatrolTarget: false);
            PickNewPatrolDirection(forceAnchorBias: true);
        }

        void ResetSearchState()
        {
            m_IsWaitingAtSearchPoint = false;
            m_SearchWaitStartedAt = float.NegativeInfinity;
            m_SearchBaseLookDirection = Vector3.zero;
            m_SearchTargetPosition = Vector3.zero;
        }

        bool TryUpdateSearch()
        {
            if (m_IsWaitingAtSearchPoint)
            {
                UpdateSearchSweep();
                return true;
            }

            if (TryUpdateNavigationGraphSearch())
                return true;

            if (TryUpdateGridSearch())
                return true;

            if (ShouldStopAtUnreachableMazeTarget(m_SearchTargetPosition))
            {
                BeginBlockedMazeSearchAtCurrentPosition();
                return true;
            }

            if (IsNearWorldPosition(m_SearchTargetPosition))
            {
                BeginSearchWait();
                return true;
            }

            ClearNavigationPathState();
            ClearGridSegmentState();
            MoveTowards(
                m_SearchTargetPosition,
                Mathf.Max(0f, m_ChaseSpeed),
                ignoreRoot: null,
                prioritizeFacingTarget: true);
            return true;
        }

        bool TryUpdateGridChase()
        {
            if (!CanUseGridNavigation)
                return false;

            if (!m_GridNavigator.TryGetCellAtWorldPosition(m_Rigidbody.position, out MazeCellData currentCell)
                || !m_GridNavigator.TryGetCellAtWorldPosition(m_LastKnownPlayerPosition, out MazeCellData targetCell))
            {
                return false;
            }

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
                MoveTowards(
                    m_LastKnownPlayerPosition,
                    Mathf.Max(0f, m_ChaseSpeed),
                    ignoreRoot: m_PlayerRig != null ? m_PlayerRig.transform : null,
                    prioritizeFacingTarget: true);
                return true;
            }

            if (!MoveAlongGridSegment(
                    currentCell.GridPosition,
                    m_GridPath[1],
                    Mathf.Max(0f, m_ChaseSpeed),
                    m_LastKnownPlayerPosition,
                    prioritizeFacingLookTarget: false))
            {
                ClearGridSegmentState();
                Vector3 nextWaypoint = m_GridNavigator.GetCellWorldCenter(m_GridPath[1]);
                MoveTowards(
                    nextWaypoint,
                    Mathf.Max(0f, m_ChaseSpeed),
                    ignoreRoot: m_PlayerRig != null ? m_PlayerRig.transform : null,
                    prioritizeFacingTarget: false);
            }

            return true;
        }

        bool TryUpdateNavigationGraphChase()
        {
            if (!CanUseNavigationGraph)
                return false;

            MazeCellData currentCell = null;
            MazeCellData targetCell = null;
            bool hasCurrentCell = CanUseGridNavigation && m_GridNavigator.TryGetCellAtWorldPosition(m_Rigidbody.position, out currentCell);
            bool hasTargetCell = CanUseGridNavigation && m_GridNavigator.TryGetCellAtWorldPosition(m_LastKnownPlayerPosition, out targetCell);

            bool targetChanged = hasTargetCell
                ? !m_HasCurrentNavigationGoalCell || m_CurrentNavigationGoalCell != targetCell.GridPosition
                : m_NavigationWaypointPath.Count == 0;

            if (targetChanged || m_NavigationWaypointPath.Count == 0)
            {
                if (!TryRebuildNavigationPath(m_Rigidbody.position, m_LastKnownPlayerPosition, hasTargetCell ? targetCell.GridPosition : default, hasTargetCell))
                    return false;
            }
            else
            {
                TrimConsumedNavigationWaypoints();
                if (m_NavigationWaypointPath.Count == 0
                    && !TryRebuildNavigationPath(m_Rigidbody.position, m_LastKnownPlayerPosition, hasTargetCell ? targetCell.GridPosition : default, hasTargetCell))
                {
                    return false;
                }
            }

            TrimNonAdvancingChaseLeadWaypoints(m_NavigationWaypointPath, m_Rigidbody.position, m_LastKnownPlayerPosition);
            RefreshNavigationTerminalWaypoint(m_NavigationWaypointPath, m_LastKnownPlayerPosition);

            return MoveAlongNavigationWaypoints(
                m_NavigationWaypointPath,
                Mathf.Max(0f, m_ChaseSpeed),
                m_LastKnownPlayerPosition,
                prioritizeFacingLookTarget: false);
        }

        bool TryUpdateNavigationGraphSearch()
        {
            if (!CanUseNavigationGraph)
                return false;

            MazeCellData currentCell = null;
            MazeCellData targetCell = null;
            bool hasCurrentCell = CanUseGridNavigation && m_GridNavigator.TryGetCellAtWorldPosition(m_Rigidbody.position, out currentCell);
            bool hasTargetCell = CanUseGridNavigation && m_GridNavigator.TryGetCellAtWorldPosition(m_SearchTargetPosition, out targetCell);

            if (hasCurrentCell
                && hasTargetCell
                && ShouldUseLocalNavigation(currentCell.GridPosition, targetCell.GridPosition, m_SearchTargetPosition))
            {
                if (m_NavigationGraph.TryBuildLocalPath(
                        currentCell.GridPosition,
                        targetCell.GridPosition,
                        m_Rigidbody.position,
                        m_SearchTargetPosition,
                        m_LocalNavigationWaypoints)
                    && MoveAlongNavigationWaypoints(
                        m_LocalNavigationWaypoints,
                        Mathf.Max(0f, m_ChaseSpeed),
                        m_SearchTargetPosition,
                        prioritizeFacingLookTarget: false))
                {
                    m_NavigationNodePath.Clear();
                    m_NavigationWaypointPath.Clear();
                    m_HasCurrentNavigationGoalCell = false;
                    if (IsNearWorldPosition(m_SearchTargetPosition))
                        BeginSearchWait();

                    return true;
                }
            }

            bool targetChanged = hasTargetCell
                ? !m_HasCurrentNavigationGoalCell || m_CurrentNavigationGoalCell != targetCell.GridPosition
                : m_NavigationWaypointPath.Count == 0;

            if (targetChanged || m_NavigationWaypointPath.Count == 0)
            {
                if (!TryRebuildNavigationPath(m_Rigidbody.position, m_SearchTargetPosition, hasTargetCell ? targetCell.GridPosition : default, hasTargetCell))
                    return false;
            }
            else
            {
                TrimConsumedNavigationWaypoints();
                if (m_NavigationWaypointPath.Count == 0
                    && !TryRebuildNavigationPath(m_Rigidbody.position, m_SearchTargetPosition, hasTargetCell ? targetCell.GridPosition : default, hasTargetCell))
                {
                    return false;
                }
            }

            bool moved = MoveAlongNavigationWaypoints(
                m_NavigationWaypointPath,
                Mathf.Max(0f, m_ChaseSpeed),
                m_SearchTargetPosition,
                prioritizeFacingLookTarget: false);
            if (moved && IsNearWorldPosition(m_SearchTargetPosition))
                BeginSearchWait();

            return moved;
        }

        bool TryRebuildNavigationPath(Vector3 startWorldPosition, Vector3 targetWorldPosition, Vector2Int targetCell, bool hasTargetCell)
        {
            m_NavigationNodePath.Clear();
            m_NavigationWaypointPath.Clear();
            if (hasTargetCell
                && CanUseGridNavigation
                && m_GridNavigator.TryGetCellAtWorldPosition(startWorldPosition, out MazeCellData startCell)
                && !m_GridNavigator.TryFindPath(startCell.GridPosition, targetCell, m_GridReachabilityPath))
            {
                return false;
            }

            if (!m_NavigationGraph.TryFindPath(startWorldPosition, targetWorldPosition, m_NavigationNodePath))
                return false;

            m_NavigationGraph.GetPathWorldPositions(m_NavigationNodePath, m_NavigationWaypointPath);
            TrimConsumedNavigationWaypoints();
            m_CurrentNavigationGoalCell = targetCell;
            m_HasCurrentNavigationGoalCell = hasTargetCell;
            return m_NavigationWaypointPath.Count > 0;
        }

        bool TryUpdateNavigationGraphPatrol()
        {
            if (!CanUseNavigationGraph || !TryResolveNavigationModuleAtWorldPosition(m_Rigidbody.position, out MazeNavigationModuleRecord currentModule))
                return false;

            if (!m_HasCurrentPatrolTargetCell)
            {
                if (!TrySelectNextPatrolNavigationTarget(currentModule, out Vector2Int initialPatrolTarget))
                    return false;

                m_LastPatrolCell = currentModule.GridPosition;
                m_HasLastPatrolCell = true;
                m_CurrentPatrolTargetCell = initialPatrolTarget;
                m_HasCurrentPatrolTargetCell = true;
                ClearNavigationPathState();
            }

            if (!m_NavigationGraph.TryGetModule(m_CurrentPatrolTargetCell, out MazeNavigationModuleRecord targetModule)
                || !TryGetNavigationModuleDestination(targetModule, out Vector3 patrolDestination))
            {
                m_HasCurrentPatrolTargetCell = false;
                ClearNavigationPathState();
                return false;
            }

            if (currentModule.GridPosition == targetModule.GridPosition && IsNearWorldPosition(patrolDestination))
            {
                if (!TrySelectNextPatrolNavigationTarget(currentModule, out Vector2Int nextPatrolTarget))
                    return false;

                m_LastPatrolCell = currentModule.GridPosition;
                m_HasLastPatrolCell = true;
                m_CurrentPatrolTargetCell = nextPatrolTarget;
                m_HasCurrentPatrolTargetCell = true;
                ClearNavigationPathState();

                if (!m_NavigationGraph.TryGetModule(m_CurrentPatrolTargetCell, out targetModule)
                    || !TryGetNavigationModuleDestination(targetModule, out patrolDestination))
                {
                    return false;
                }
            }

            if (ShouldUseLocalNavigation(currentModule.GridPosition, targetModule.GridPosition, patrolDestination))
            {
                if (m_NavigationGraph.TryBuildLocalPath(
                        currentModule.GridPosition,
                        targetModule.GridPosition,
                        m_Rigidbody.position,
                        patrolDestination,
                        m_LocalNavigationWaypoints)
                    && MoveAlongNavigationWaypoints(m_LocalNavigationWaypoints, Mathf.Max(0f, m_PatrolSpeed), patrolDestination))
                {
                    m_NavigationNodePath.Clear();
                    m_NavigationWaypointPath.Clear();
                    m_HasCurrentNavigationGoalCell = false;
                    return true;
                }
            }

            bool targetChanged = !m_HasCurrentNavigationGoalCell || m_CurrentNavigationGoalCell != targetModule.GridPosition;
            if (targetChanged || m_NavigationWaypointPath.Count == 0)
            {
                if (!TryRebuildNavigationPath(m_Rigidbody.position, patrolDestination, targetModule.GridPosition, hasTargetCell: true))
                    return false;
            }
            else
            {
                TrimConsumedNavigationWaypoints();
                if (m_NavigationWaypointPath.Count == 0
                    && !TryRebuildNavigationPath(m_Rigidbody.position, patrolDestination, targetModule.GridPosition, hasTargetCell: true))
                {
                    return false;
                }
            }

            return MoveAlongNavigationWaypoints(m_NavigationWaypointPath, Mathf.Max(0f, m_PatrolSpeed), patrolDestination);
        }

        bool TryResolveNavigationModuleAtWorldPosition(Vector3 worldPosition, out MazeNavigationModuleRecord module)
        {
            module = null;
            if (!CanUseNavigationGraph)
                return false;

            if (CanUseGridNavigation
                && m_GridNavigator.TryGetCellAtWorldPosition(worldPosition, out MazeCellData cell)
                && m_NavigationGraph.TryGetModule(cell.GridPosition, out module))
            {
                return true;
            }

            return m_NavigationGraph.TryGetNearestModule(worldPosition, out module);
        }

        bool TryGetNavigationModuleDestination(MazeNavigationModuleRecord module, out Vector3 destination)
        {
            destination = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            if (module == null)
                return false;

            destination = module.WorldCenter;
            if (m_NavigationGraph.TryProjectLocalPoint(module.GridPosition, destination, out Vector3 projectedDestination))
                destination = projectedDestination;

            destination.y = m_SpawnPosition.y;
            return true;
        }

        void TrimConsumedNavigationWaypoints()
        {
            while (m_NavigationWaypointPath.Count > 0 && IsNearWorldPosition(m_NavigationWaypointPath[0]))
                m_NavigationWaypointPath.RemoveAt(0);
        }

        void RefreshNavigationTerminalWaypoint(List<Vector3> waypoints, Vector3 targetWorldPosition)
        {
            if (waypoints == null || waypoints.Count == 0)
                return;

            Vector3 targetPosition = new(targetWorldPosition.x, m_SpawnPosition.y, targetWorldPosition.z);
            waypoints[waypoints.Count - 1] = targetPosition;
        }

        void TrimNonAdvancingChaseLeadWaypoints(List<Vector3> waypoints, Vector3 currentWorldPosition, Vector3 targetWorldPosition)
        {
            if (waypoints == null || waypoints.Count <= 1)
                return;

            Vector3 currentPosition = new(currentWorldPosition.x, m_SpawnPosition.y, currentWorldPosition.z);
            Vector3 targetPosition = new(targetWorldPosition.x, m_SpawnPosition.y, targetWorldPosition.z);
            Vector3 currentToTarget = Vector3.ProjectOnPlane(targetPosition - currentPosition, Vector3.up);
            float currentDistanceSqr = currentToTarget.sqrMagnitude;
            if (currentDistanceSqr <= 0.0001f)
                return;

            Vector3 currentToTargetDirection = currentToTarget.normalized;
            const float distanceEpsilon = 0.01f;

            while (waypoints.Count > 1)
            {
                Vector3 leadWaypoint = new(waypoints[0].x, m_SpawnPosition.y, waypoints[0].z);
                Vector3 currentToLead = Vector3.ProjectOnPlane(leadWaypoint - currentPosition, Vector3.up);
                if (currentToLead.sqrMagnitude <= 0.0001f)
                {
                    waypoints.RemoveAt(0);
                    continue;
                }

                float leadDistanceSqr = Vector3.ProjectOnPlane(targetPosition - leadWaypoint, Vector3.up).sqrMagnitude;
                float directionDot = Vector3.Dot(currentToLead.normalized, currentToTargetDirection);
                bool isBehind = directionDot <= 0f;
                bool isNonAdvancing = leadDistanceSqr >= currentDistanceSqr - distanceEpsilon;
                if (!isBehind && !isNonAdvancing)
                    break;

                waypoints.RemoveAt(0);
            }
        }

        bool MoveAlongNavigationWaypoints(List<Vector3> waypoints, float moveSpeed, Vector3 lookTarget, bool prioritizeFacingLookTarget = false)
        {
            if (waypoints == null)
                return false;

            while (waypoints.Count > 0 && IsNearWorldPosition(waypoints[0]))
                waypoints.RemoveAt(0);

            if (waypoints.Count == 0)
                return false;

            MoveTowardsNavigationPoint(waypoints[0], moveSpeed, lookTarget, prioritizeFacingLookTarget);
            return true;
        }

        void MoveTowardsNavigationPoint(Vector3 waypoint, float moveSpeed, Vector3 lookTarget, bool prioritizeFacingLookTarget = false)
        {
            Vector3 currentPosition = m_Rigidbody.position;
            Vector3 targetPosition = new(waypoint.x, m_SpawnPosition.y, waypoint.z);
            Vector3 nextPosition = Vector3.MoveTowards(currentPosition, targetPosition, Mathf.Max(0f, moveSpeed) * Time.fixedDeltaTime);

            Vector3 planarLookDirection = Vector3.ProjectOnPlane(lookTarget - currentPosition, Vector3.up);
            if (planarLookDirection.sqrMagnitude < 0.0001f)
                planarLookDirection = Vector3.ProjectOnPlane(targetPosition - currentPosition, Vector3.up);

            if (planarLookDirection.sqrMagnitude < 0.0001f)
                planarLookDirection = m_CurrentMoveDirection.sqrMagnitude > 0.0001f ? m_CurrentMoveDirection : transform.forward;
            else
                planarLookDirection.Normalize();

            m_CurrentMoveDirection = planarLookDirection;
            MoveRigidBody(nextPosition, planarLookDirection, prioritizeFacingLookDirection: prioritizeFacingLookTarget);
        }

        void BeginSearchWait()
        {
            if (m_IsWaitingAtSearchPoint)
                return;

            m_IsWaitingAtSearchPoint = true;
            m_SearchWaitStartedAt = Time.time;
            if (m_CurrentMoveDirection.sqrMagnitude > 0.0001f)
                m_SearchBaseLookDirection = m_CurrentMoveDirection.normalized;
            else
                m_SearchBaseLookDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

            if (m_SearchBaseLookDirection.sqrMagnitude < 0.0001f)
                m_SearchBaseLookDirection = Vector3.forward;

            ClearNavigationPathState();
            ClearGridSegmentState();
        }

        void UpdateSearchSweep()
        {
            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            currentPosition.y = m_SpawnPosition.y;

            Vector3 baseDirection = Vector3.ProjectOnPlane(m_SearchBaseLookDirection, Vector3.up);
            if (baseDirection.sqrMagnitude < 0.0001f)
                baseDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

            if (baseDirection.sqrMagnitude < 0.0001f)
                baseDirection = Vector3.forward;

            baseDirection.Normalize();
            float elapsed = Mathf.Max(0f, Time.time - m_SearchWaitStartedAt);
            float sweepOffset = Mathf.Sin(elapsed * Mathf.Max(0.05f, m_SearchSweepFrequency) * Mathf.PI * 2f) * m_SearchSweepAngle;
            Vector3 lookDirection = Quaternion.Euler(0f, sweepOffset, 0f) * baseDirection;
            m_CurrentMoveDirection = lookDirection.normalized;
            MoveRigidBody(currentPosition, m_CurrentMoveDirection);
        }

        bool ShouldStopAtUnreachableMazeTarget(Vector3 targetWorldPosition)
        {
            return (CanUseNavigationGraph || CanUseGridNavigation) && !CanReachMazeTarget(targetWorldPosition);
        }

        bool CanReachMazeTarget(Vector3 targetWorldPosition)
        {
            Vector3 startWorldPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            bool usedMazeNavigation = false;

            if (CanUseGridNavigation
                && m_GridNavigator.TryGetCellAtWorldPosition(startWorldPosition, out MazeCellData startCell)
                && m_GridNavigator.TryGetCellAtWorldPosition(targetWorldPosition, out MazeCellData targetCell))
            {
                usedMazeNavigation = true;
                return m_GridNavigator.TryFindPath(startCell.GridPosition, targetCell.GridPosition, m_GridReachabilityPath);
            }

            if (CanUseNavigationGraph)
            {
                usedMazeNavigation = true;
                if (m_NavigationGraph.TryFindPath(startWorldPosition, targetWorldPosition, m_NavigationReachabilityPath))
                    return true;
            }

            if (CanUseGridNavigation)
            {
                usedMazeNavigation = true;
                if (m_GridNavigator.TryFindPath(startWorldPosition, targetWorldPosition, m_GridReachabilityPath))
                    return true;
            }

            return !usedMazeNavigation;
        }

        void BeginBlockedMazeSearchAtCurrentPosition()
        {
            if (m_State == EnemyState.Search && m_IsWaitingAtSearchPoint)
                return;

            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            currentPosition.y = m_SpawnPosition.y;
            m_State = EnemyState.Search;
            m_LastKnownPlayerPosition = currentPosition;
            m_SearchTargetPosition = currentPosition;
            m_IsWaitingAtSearchPoint = false;
            ClearGridNavigationState(keepPatrolTarget: false);
            BeginSearchWait();
            MarkMovementProgress();
        }

        void HandleMazeNavigationTopologyChanged()
        {
            ClearGridNavigationState(keepPatrolTarget: false);
            MarkMovementProgress();
        }

        bool TryRecoverFromMovementStall()
        {
            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            Vector3 planarDelta = Vector3.ProjectOnPlane(currentPosition - m_LastProgressPosition, Vector3.up);
            float progressThreshold = Mathf.Max(0.01f, m_StuckProgressDistance);
            if (planarDelta.sqrMagnitude >= progressThreshold * progressThreshold)
            {
                MarkMovementProgress(currentPosition);
                return false;
            }

            if (!ShouldExpectMovementProgress())
            {
                MarkMovementProgress(currentPosition);
                return false;
            }

            if (Time.time - m_LastProgressAt < Mathf.Max(0.25f, m_StuckRecoveryDelay))
                return false;

            RecoverFromMovementStall(currentPosition);
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

        void RecoverFromMovementStall(Vector3 currentPosition)
        {
            ClearGridNavigationState(keepPatrolTarget: false);
            m_CurrentMoveDirection = Vector3.zero;

            if (m_State == EnemyState.Chase || m_State == EnemyState.Search)
            {
                m_State = EnemyState.Search;
                m_LastKnownPlayerPosition = currentPosition;
                m_SearchTargetPosition = currentPosition;
                m_IsWaitingAtSearchPoint = false;
                BeginSearchWait();
                return;
            }

            m_HasLastPatrolCell = false;
            PickNewPatrolDirection(forceAnchorBias: true);
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

        bool ShouldUseLocalNavigation(Vector2Int currentCell, Vector2Int targetCell, Vector3 targetWorldPosition, Transform ignoreRoot = null)
        {
            if (CanUseGridNavigation && !m_GridNavigator.TryFindPath(currentCell, targetCell, m_GridReachabilityPath))
                return false;

            int allowedModuleDistance = Mathf.Max(1, m_ClosePursuitModuleDistance);
            int moduleDistance = GetGridDistance(currentCell, targetCell);
            if (moduleDistance > allowedModuleDistance)
                return false;

            if (moduleDistance == 0)
                return true;

            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            Vector3 planarOffset = Vector3.ProjectOnPlane(targetWorldPosition - currentPosition, Vector3.up);
            if (planarOffset.magnitude > m_ClosePursuitDistance)
                return false;

            return !HasDirectLocalNavigationPath(targetWorldPosition, ignoreRoot);
        }

        bool HasDirectLocalNavigationPath(Vector3 targetWorldPosition, Transform ignoreRoot)
        {
            Vector3 origin = GetLocalNavigationProbeWorldPosition();
            Vector3 target = new(targetWorldPosition.x, origin.y, targetWorldPosition.z);
            Vector3 planarDirection = target - origin;
            float distance = planarDirection.magnitude;
            if (distance <= 0.05f)
                return true;

            planarDirection /= distance;
            float probeRadius = GetNavigationClearanceRadius();

            int hitCount = Physics.SphereCastNonAlloc(
                origin,
                probeRadius,
                planarDirection,
                m_ProbeHits,
                distance,
                m_ObstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = m_ProbeHits[i].collider;
                if (hitCollider == null || hitCollider.isTrigger)
                    continue;

                if (IsOwnCollider(hitCollider))
                    continue;

                Transform hitRoot = hitCollider.transform.root;
                if (ignoreRoot != null && hitRoot == ignoreRoot)
                    continue;

                return false;
            }

            return true;
        }

        float GetNavigationClearanceRadius()
        {
            return Mathf.Max(0.05f, m_ObstacleProbeRadius);
        }

        Vector3 GetLocalNavigationProbeWorldPosition()
        {
            Vector3 basePosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            float probeHeight = Mathf.Clamp(m_ObstacleProbeHeight * 0.6f, 0.3f, 1.2f);
            return basePosition + Vector3.up * probeHeight;
        }

        bool TryRebuildChasePath(Vector2Int currentCell, Vector2Int targetCell)
        {
            if (!m_GridNavigator.TryFindPath(currentCell, targetCell, m_GridPath))
                return false;

            m_CurrentPathGoalCell = targetCell;
            m_HasCurrentPathGoalCell = true;
            return true;
        }

        bool TryAlignChasePathToCurrentCell(Vector2Int currentCell)
        {
            if (m_GridPath.Count == 0)
                return false;

            int currentIndex = -1;
            for (int i = 0; i < m_GridPath.Count; i++)
            {
                if (m_GridPath[i] != currentCell)
                    continue;

                currentIndex = i;
                break;
            }

            if (currentIndex < 0)
                return false;

            if (currentIndex > 0)
                m_GridPath.RemoveRange(0, currentIndex);

            return true;
        }

        bool TryUpdateGridSearch()
        {
            if (!CanUseGridNavigation)
                return false;

            if (!m_GridNavigator.TryGetCellAtWorldPosition(m_Rigidbody.position, out MazeCellData currentCell)
                || !m_GridNavigator.TryGetCellAtWorldPosition(m_SearchTargetPosition, out MazeCellData targetCell))
            {
                return false;
            }

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
                if (IsNearWorldPosition(m_SearchTargetPosition))
                    BeginSearchWait();
                else
                    MoveTowards(
                        m_SearchTargetPosition,
                        Mathf.Max(0f, m_ChaseSpeed),
                        ignoreRoot: null,
                        prioritizeFacingTarget: true);

                return true;
            }

            if (!MoveAlongGridSegment(
                    currentCell.GridPosition,
                    m_GridPath[1],
                    Mathf.Max(0f, m_ChaseSpeed),
                    m_SearchTargetPosition,
                    prioritizeFacingLookTarget: false))
            {
                ClearGridSegmentState();
                Vector3 nextWaypoint = m_GridNavigator.GetCellWorldCenter(m_GridPath[1]);
                MoveTowards(
                    nextWaypoint,
                    Mathf.Max(0f, m_ChaseSpeed),
                    ignoreRoot: null,
                    prioritizeFacingTarget: false);
            }

            if (IsNearWorldPosition(m_SearchTargetPosition))
                BeginSearchWait();

            return true;
        }

        bool TryUpdateGridPatrol()
        {
            if (!CanUseGridNavigation)
                return false;

            if (!m_GridNavigator.TryGetCellAtWorldPosition(m_Rigidbody.position, out MazeCellData currentCell))
                return false;

            if (!m_HasCurrentPatrolTargetCell || currentCell.GridPosition == m_CurrentPatrolTargetCell || IsNearGridCell(m_CurrentPatrolTargetCell))
            {
                if (!TrySelectNextPatrolCell(currentCell, out Vector2Int patrolTarget))
                    return false;

                m_LastPatrolCell = currentCell.GridPosition;
                m_HasLastPatrolCell = true;
                m_CurrentPatrolTargetCell = patrolTarget;
                m_HasCurrentPatrolTargetCell = true;
            }

            if (!MoveAlongGridSegment(currentCell.GridPosition, m_CurrentPatrolTargetCell, Mathf.Max(0f, m_PatrolSpeed)))
            {
                ClearGridSegmentState();
                Vector3 patrolTargetWorld = m_GridNavigator.GetCellWorldCenter(m_CurrentPatrolTargetCell);
                MoveTowards(patrolTargetWorld, Mathf.Max(0f, m_PatrolSpeed), ignoreRoot: null);
            }

            return true;
        }

        bool MoveAlongGridSegment(
            Vector2Int currentCellPosition,
            Vector2Int nextCellPosition,
            float moveSpeed,
            Vector3 lookTargetOverride = default,
            bool prioritizeFacingLookTarget = false)
        {
            if (!CanUseGridNavigation)
                return false;

            Vector2Int segmentStep = nextCellPosition - currentCellPosition;
            if (!IsCardinalGridStep(segmentStep))
                return false;

            Vector3 currentCellCenter = m_GridNavigator.GetCellWorldCenter(currentCellPosition);
            Vector3 nextCellCenter = m_GridNavigator.GetCellWorldCenter(nextCellPosition);
            Vector3 segmentDirection = new(segmentStep.x, 0f, segmentStep.y);
            segmentDirection.Normalize();

            if (!m_HasLastGridSegmentStep || m_LastGridSegmentStep != segmentStep)
            {
                m_CurrentMoveDirection = segmentDirection;
                m_LastGridSegmentStep = segmentStep;
                m_HasLastGridSegmentStep = true;
            }

            float deltaTime = Time.fixedDeltaTime;
            float forwardStep = Mathf.Max(0f, moveSpeed) * deltaTime;
            float centeringStep = Mathf.Max(forwardStep, m_CorridorCenteringSpeed * deltaTime);
            Vector3 currentPosition = m_Rigidbody.position;
            Vector3 nextPosition = currentPosition;

            if (segmentStep.x != 0)
            {
                nextPosition.x = Mathf.MoveTowards(currentPosition.x, nextCellCenter.x, forwardStep);
                nextPosition.z = Mathf.MoveTowards(currentPosition.z, currentCellCenter.z, centeringStep);
            }
            else
            {
                nextPosition.x = Mathf.MoveTowards(currentPosition.x, currentCellCenter.x, centeringStep);
                nextPosition.z = Mathf.MoveTowards(currentPosition.z, nextCellCenter.z, forwardStep);
            }

            nextPosition.y = m_SpawnPosition.y;
            Vector3 planarLookDirection = segmentDirection;
            if (prioritizeFacingLookTarget)
            {
                Vector3 targetLookDirection = Vector3.ProjectOnPlane(lookTargetOverride - currentPosition, Vector3.up);
                if (targetLookDirection.sqrMagnitude >= 0.0001f)
                    planarLookDirection = targetLookDirection.normalized;
            }

            MoveRigidBody(nextPosition, planarLookDirection, prioritizeFacingLookDirection: prioritizeFacingLookTarget);
            return true;
        }

        bool TrySelectNextPatrolCell(MazeCellData currentCell, out Vector2Int patrolTarget)
        {
            patrolTarget = currentCell != null ? currentCell.GridPosition : default;
            if (currentCell == null || !CanUseGridNavigation)
                return false;

            m_GridNavigator.GetNeighbors(currentCell.GridPosition, m_GridNeighbors);
            if (m_GridNeighbors.Count == 0)
                return false;

            if (!m_GridNavigator.TryGetNearestCell(m_RoamCenter, out MazeCellData roamCell))
                roamCell = currentCell;

            int patrolRadiusCells = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.5f, m_PatrolRadius) / Mathf.Max(0.01f, m_GridNavigator.CellSize)));
            int currentDistanceToRoam = GetGridDistance(currentCell.GridPosition, roamCell.GridPosition);

            if (currentDistanceToRoam > patrolRadiusCells)
            {
                patrolTarget = SelectNeighborClosestToAnchor(roamCell.GridPosition);
                return true;
            }

            List<Vector2Int> candidatesWithinRadius = m_GridNeighbors;
            int validCount = 0;
            for (int i = 0; i < candidatesWithinRadius.Count; i++)
            {
                if (GetGridDistance(candidatesWithinRadius[i], roamCell.GridPosition) > patrolRadiusCells)
                    continue;

                candidatesWithinRadius[validCount++] = candidatesWithinRadius[i];
            }

            if (validCount == 0)
            {
                patrolTarget = SelectNeighborClosestToAnchor(roamCell.GridPosition);
                return true;
            }

            if (validCount > 1 && m_HasLastPatrolCell)
            {
                int filteredCount = 0;
                for (int i = 0; i < validCount; i++)
                {
                    if (candidatesWithinRadius[i] == m_LastPatrolCell)
                        continue;

                    candidatesWithinRadius[filteredCount++] = candidatesWithinRadius[i];
                }

                if (filteredCount > 0)
                    validCount = filteredCount;
            }

            patrolTarget = candidatesWithinRadius[Random.Range(0, validCount)];
            return true;
        }

        bool TrySelectNextPatrolNavigationTarget(MazeNavigationModuleRecord currentModule, out Vector2Int patrolTarget)
        {
            patrolTarget = currentModule != null ? currentModule.GridPosition : default;
            if (currentModule == null || !CanUseNavigationGraph)
                return false;

            m_NavigationNeighbors.Clear();
            if (!m_NavigationGraph.GetConnectedModuleGridPositions(currentModule.GridPosition, m_NavigationNeighbors)
                || m_NavigationNeighbors.Count == 0)
            {
                return false;
            }

            MazeNavigationModuleRecord roamModule = currentModule;
            if (!TryResolveNavigationModuleAtWorldPosition(m_RoamCenter, out roamModule))
                roamModule = currentModule;

            int patrolRadiusCells = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.5f, m_PatrolRadius) / ResolveNavigationCellSize()));
            int currentDistanceToRoam = GetGridDistance(currentModule.GridPosition, roamModule.GridPosition);
            if (currentDistanceToRoam > patrolRadiusCells)
            {
                patrolTarget = SelectConnectedModuleClosestToAnchor(m_NavigationNeighbors, roamModule.GridPosition);
                return true;
            }

            int validCount = 0;
            for (int i = 0; i < m_NavigationNeighbors.Count; i++)
            {
                if (GetGridDistance(m_NavigationNeighbors[i], roamModule.GridPosition) > patrolRadiusCells)
                    continue;

                m_NavigationNeighbors[validCount++] = m_NavigationNeighbors[i];
            }

            if (validCount == 0)
            {
                patrolTarget = SelectConnectedModuleClosestToAnchor(m_NavigationNeighbors, roamModule.GridPosition);
                return true;
            }

            if (validCount > 1 && m_HasLastPatrolCell)
            {
                int filteredCount = 0;
                for (int i = 0; i < validCount; i++)
                {
                    if (m_NavigationNeighbors[i] == m_LastPatrolCell)
                        continue;

                    m_NavigationNeighbors[filteredCount++] = m_NavigationNeighbors[i];
                }

                if (filteredCount > 0)
                    validCount = filteredCount;
            }

            patrolTarget = m_NavigationNeighbors[Random.Range(0, validCount)];
            return true;
        }

        Vector2Int SelectNeighborClosestToAnchor(Vector2Int anchorCell)
        {
            Vector2Int best = m_GridNeighbors[0];
            int bestDistance = GetGridDistance(best, anchorCell);
            for (int i = 1; i < m_GridNeighbors.Count; i++)
            {
                int distance = GetGridDistance(m_GridNeighbors[i], anchorCell);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = m_GridNeighbors[i];
            }

            return best;
        }

        static Vector2Int SelectConnectedModuleClosestToAnchor(List<Vector2Int> candidates, Vector2Int anchorCell)
        {
            Vector2Int best = candidates[0];
            int bestDistance = GetGridDistance(best, anchorCell);
            for (int i = 1; i < candidates.Count; i++)
            {
                int distance = GetGridDistance(candidates[i], anchorCell);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = candidates[i];
            }

            return best;
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
            if (!Physics.Linecast(origin, destination, out RaycastHit hit, m_ObstacleMask, QueryTriggerInteraction.Ignore))
                return true;

            Transform hitRoot = hit.transform != null ? hit.transform.root : null;
            if (hitRoot == transform)
                return true;

            if (m_PlayerRig != null && hitRoot == m_PlayerRig.transform)
                return true;

            return false;
        }

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

            MoveInDirection(
                desiredDirection.normalized,
                moveSpeed,
                ignoreRoot,
                desiredDirection.normalized,
                prioritizeFacingLookDirection: prioritizeFacingTarget);
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

            int hitCount = Physics.SphereCastNonAlloc(
                GetSensorWorldPosition(),
                Mathf.Max(0.05f, m_ObstacleProbeRadius),
                normalizedDirection,
                m_ProbeHits,
                Mathf.Max(0.05f, m_ObstacleProbeDistance),
                m_ObstacleMask,
                QueryTriggerInteraction.Ignore);

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

        Vector3 GetInitialMoveDirection()
        {
            Vector3 initialDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (initialDirection.sqrMagnitude < 0.0001f)
                initialDirection = Vector3.forward;

            return initialDirection.normalized;
        }

        void UpdateEyeTracking()
        {
            if (m_VisualRoot == null || m_SensorOrigin == null)
                return;

            Vector3 desiredLocalOffset = GetDefaultEyeLocalOffset();
            float blend = Mathf.Clamp01(m_EyeTrackingSmoothing * Time.deltaTime);
            m_CurrentEyeLocalOffset = Vector3.Lerp(m_CurrentEyeLocalOffset, desiredLocalOffset, blend);
            m_SensorOrigin.localPosition = m_CurrentEyeLocalOffset;
            m_SensorOrigin.localRotation = Quaternion.Slerp(m_SensorOrigin.localRotation, Quaternion.identity, blend);
        }

        Vector3 GetDefaultEyeLocalOffset()
        {
            if (m_SensorOrigin != null)
                return new Vector3(0f, m_EyeOrbitHeight, Mathf.Abs(m_SensorOrigin.localPosition.z) > 0.01f ? Mathf.Abs(m_SensorOrigin.localPosition.z) : m_EyeOrbitRadius);

            return new Vector3(0f, m_EyeOrbitHeight, m_EyeOrbitRadius);
        }

        void ClearGridNavigationState(bool keepPatrolTarget)
        {
            m_GridPath.Clear();
            m_HasCurrentPathGoalCell = false;
            ClearNavigationPathState();
            ClearGridSegmentState();
            if (!keepPatrolTarget)
                m_HasCurrentPatrolTargetCell = false;
        }

        void ClearNavigationPathState()
        {
            m_NavigationNodePath.Clear();
            m_NavigationWaypointPath.Clear();
            m_LocalNavigationWaypoints.Clear();
            m_HasCurrentNavigationGoalCell = false;
        }

        void ClearGridSegmentState()
        {
            m_HasLastGridSegmentStep = false;
        }

        bool IsNearWorldPosition(Vector3 worldPosition)
        {
            Vector3 current = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            Vector3 delta = Vector3.ProjectOnPlane(worldPosition - current, Vector3.up);
            return delta.sqrMagnitude <= m_PathNodeArrivalDistance * m_PathNodeArrivalDistance;
        }

        bool IsNearGridCell(Vector2Int gridPosition)
        {
            if (!CanUseGridNavigation)
                return false;

            return IsNearWorldPosition(m_GridNavigator.GetCellWorldCenter(gridPosition));
        }

        float ResolveNavigationCellSize()
        {
            if (CanUseGridNavigation)
                return Mathf.Max(0.01f, m_GridNavigator.CellSize);

            if (m_NavigationGraph != null && m_NavigationGraph.ModulePlacer != null)
                return Mathf.Max(0.01f, m_NavigationGraph.ModulePlacer.CellSize);

            return 4f;
        }

        static int GetGridDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        static bool IsCardinalGridStep(Vector2Int step)
        {
            return Mathf.Abs(step.x) + Mathf.Abs(step.y) == 1;
        }

        Vector3 ResolveRoamCenter()
        {
            if (m_RoamAnchor != null)
                return m_RoamAnchor.position;

            if (m_SpawnPosition != Vector3.zero || Application.isPlaying)
                return m_SpawnPosition;

            return transform.position;
        }

        Vector3 GetSensorWorldPosition()
        {
            if (m_SensorOrigin != null)
                return m_SensorOrigin.position;

            return transform.position + Vector3.up * Mathf.Max(0.2f, m_ObstacleProbeHeight);
        }

        Vector3 GetGroundPosition(Vector3 worldPosition)
        {
            return new Vector3(worldPosition.x, m_SpawnPosition.y, worldPosition.z);
        }

        void OnDrawGizmosSelected()
        {
            Vector3 roamCenter = m_RoamAnchor != null ? m_RoamAnchor.position : transform.position;
            Gizmos.color = new Color(1f, 0.12f, 0.1f, 0.3f);
            Gizmos.DrawWireSphere(roamCenter, Mathf.Max(0.5f, m_PatrolRadius));

            Vector3 sensorPosition = m_SensorOrigin != null ? m_SensorOrigin.position : transform.position + Vector3.up * Mathf.Max(0.2f, m_ObstacleProbeHeight);
            float halfFov = Mathf.Clamp(m_FieldOfViewDegrees, 10f, 360f) * 0.5f;
            Vector3 leftBoundary = Quaternion.Euler(0f, -halfFov, 0f) * transform.forward;
            Vector3 rightBoundary = Quaternion.Euler(0f, halfFov, 0f) * transform.forward;

            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.85f);
            Gizmos.DrawLine(sensorPosition, sensorPosition + leftBoundary.normalized * Mathf.Max(0.1f, m_DetectionRange));
            Gizmos.DrawLine(sensorPosition, sensorPosition + rightBoundary.normalized * Mathf.Max(0.1f, m_DetectionRange));
            Gizmos.DrawWireSphere(sensorPosition, 0.05f);

            Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.7f);
            Gizmos.DrawLine(sensorPosition, sensorPosition + transform.forward * Mathf.Max(0.1f, m_ObstacleProbeDistance));
        }
    }
}
