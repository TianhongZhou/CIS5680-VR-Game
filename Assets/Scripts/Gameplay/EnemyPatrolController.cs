using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;
namespace CIS5680VRGame.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    public partial class EnemyPatrolController : MonoBehaviour
    {
        static readonly HashSet<EnemyPatrolController> s_ActiveControllers = new();
        static float s_GlobalDetectionRangeReductionMeters;
        static float s_GlobalFieldOfViewReductionDegrees;
        static float s_GlobalDetectionSuppressedUntil = float.NegativeInfinity;
        static int s_MazeNavigationTopologyVersion;
        static readonly Vector3 s_DefaultBodyColliderCenter = new(0f, 0.783f, 0f);
        const float DefaultBodyColliderRadius = 0.48f;
        const string BodyBlockerName = "BodyBlocker";
        const string PulseAttachSurfaceName = "PulseAttachSurface";
        const float DefaultPulseAttachSurfaceRadius = 0.42f;
        const int MaxPulseAttachMeshTrianglesForConvexCollider = 240;
        const float TurnLimitedTranslationScaleThreshold = 0.05f;

        internal enum EnemyState
        {
            Patrol,
            Chase,
            Search,
        }

        internal enum EnemyNavigationDebugMode
        {
            None,
            NavigationGraphChase,
            GridChase,
            DirectChaseFallback,
            NavigationGraphSearch,
            GridSearch,
            DirectSearchFallback,
            NavigationGraphPatrol,
            GridPatrol,
            WanderPatrol,
            LocalSearch,
            LocalPatrol,
            SearchWait,
            StuckRecovery,
            TopologyRefresh,
        }

        readonly struct EnemyMovementRequest
        {
            public EnemyMovementRequest(
                EnemyState state,
                Vector3 targetPosition,
                float moveSpeed,
                Transform ignoreRoot,
                bool prioritizeFacingTargetOnDirect)
            {
                State = state;
                TargetPosition = targetPosition;
                MoveSpeed = moveSpeed;
                IgnoreRoot = ignoreRoot;
                PrioritizeFacingTargetOnDirect = prioritizeFacingTargetOnDirect;
            }

            public EnemyState State { get; }
            public Vector3 TargetPosition { get; }
            public float MoveSpeed { get; }
            public Transform IgnoreRoot { get; }
            public bool PrioritizeFacingTargetOnDirect { get; }

            public EnemyMovementRequest WithTarget(Vector3 targetPosition)
            {
                return new EnemyMovementRequest(
                    State,
                    targetPosition,
                    MoveSpeed,
                    IgnoreRoot,
                    PrioritizeFacingTargetOnDirect);
            }
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
        [SerializeField, Min(0f)] float m_ClosePursuitDistanceHysteresis = 0.5f;
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
        [SerializeField] float m_SearchArrivalDistance = 0.6f;
        [SerializeField, Min(0.01f)] float m_StuckProgressDistance = 0.08f;
        [SerializeField, Min(0.25f)] float m_StuckRecoveryDelay = 1.4f;
        [SerializeField, Min(0)] int m_StuckSoftRecoveryAttempts = 2;
        [SerializeField, Min(0f)] float m_StuckTurnInPlaceGraceDuration = 0.35f;
        [SerializeField, Min(0f)] float m_PathFailureRetryDelay = 0.35f;
        [SerializeField, Min(0f)] float m_ReachabilityCacheDuration = 0.2f;
        [Header("Debug Observability")]
        [SerializeField] bool m_DebugNavigationObservability;
        [SerializeField] bool m_DebugDrawNavigationGizmos = true;
        [SerializeField] bool m_DebugDrawNavigationLabels = true;
        [SerializeField] bool m_DebugLogNavigationDecisions;
        [SerializeField, Min(0.05f)] float m_DebugLogMinInterval = 0.5f;
        [SerializeField] EnemyNavigationDebugMode m_DebugLastNavigationMode = EnemyNavigationDebugMode.None;
        [SerializeField] string m_DebugLastDecision = "None";
        [SerializeField] string m_DebugLastFailure = "None";
        [SerializeField, TextArea(2, 4)] string m_DebugStateSummary = "Not running";
        [SerializeField, TextArea(2, 4)] string m_DebugPathSummary = "No path data";
        [SerializeField, TextArea(2, 4)] string m_DebugTargetSummary = "No target";

        readonly RaycastHit[] m_ProbeHits = new RaycastHit[8];
        readonly List<Vector2Int> m_GridPath = new(16);
        readonly List<Vector2Int> m_GridNeighbors = new(4);
        readonly List<Vector2Int> m_NavigationNeighbors = new(8);
        readonly List<int> m_NavigationNodePath = new(16);
        readonly List<Vector3> m_NavigationWaypointPath = new(16);
        readonly List<Vector3> m_LocalNavigationWaypoints = new(8);
        readonly List<Vector2Int> m_GridReachabilityPath = new(16);
        readonly List<int> m_NavigationReachabilityPath = new(16);
        readonly EnemyNavigationQueryCache m_NavigationQueryCache = new();

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
        int m_StuckRecoveryAttemptCount;
        int m_KnownMazeNavigationTopologyVersion;
        float m_LastTurnLimitedAt = float.NegativeInfinity;
        bool m_IsCloseDirectChasing;
        bool m_IsUsingCloseLocalNavigation;
        Vector3 m_DebugTargetPosition;
        bool m_DebugHasTargetPosition;
        float m_DebugLastLogAt = float.NegativeInfinity;

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
            s_MazeNavigationTopologyVersion++;
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
            m_DebugLogMinInterval = Mathf.Max(0.05f, m_DebugLogMinInterval);
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
            m_KnownMazeNavigationTopologyVersion = s_MazeNavigationTopologyVersion;
            MarkMovementProgress();
            PickNewPatrolDirection(forceAnchorBias: false);
        }

        void OnEnable()
        {
            s_ActiveControllers.Add(this);
            SyncMazeNavigationTopologyVersion();
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
            SyncMazeNavigationTopologyVersion();
            EnsureMazeNavigationDataForMovement();
            ResolvePlayerReferences();
            UpdateAwareness();

            if (TryRecoverFromMovementStall())
                return;

            TryExecuteCurrentMovement();
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
            RecordNavigationDebugDecision("Forced chase target assigned.", targetGroundPosition);
        }

        public void SetPatrolRadius(float radius)
        {
            m_PatrolRadius = Mathf.Max(0.5f, radius);
        }

    }
}
