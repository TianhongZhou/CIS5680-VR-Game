using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        void RecordNavigationDebugMode(EnemyNavigationDebugMode mode, Vector3 targetPosition, string decision)
        {
            if (!EnemyNavigationDebugReporter.TryRecordMode(
                    m_DebugNavigationObservability,
                    ref m_DebugLastNavigationMode,
                    ref m_DebugLastDecision,
                    ref m_DebugTargetPosition,
                    ref m_DebugHasTargetPosition,
                    mode,
                    targetPosition,
                    decision,
                    out bool changed))
            {
                return;
            }

            UpdateNavigationDebugSnapshot();
            LogNavigationDebugChange(changed);
        }

        void RecordNavigationDebugDecision(string decision, Vector3 targetPosition)
        {
            if (!EnemyNavigationDebugReporter.TryRecordDecision(
                    m_DebugNavigationObservability,
                    ref m_DebugLastDecision,
                    ref m_DebugTargetPosition,
                    ref m_DebugHasTargetPosition,
                    decision,
                    targetPosition,
                    out bool changed))
            {
                return;
            }

            UpdateNavigationDebugSnapshot();
            LogNavigationDebugChange(changed);
        }

        void RecordNavigationDebugFailure(string failure, Vector3 targetPosition)
        {
            if (!EnemyNavigationDebugReporter.TryRecordFailure(
                    m_DebugNavigationObservability,
                    ref m_DebugLastFailure,
                    ref m_DebugTargetPosition,
                    ref m_DebugHasTargetPosition,
                    failure,
                    targetPosition,
                    out bool changed))
            {
                return;
            }

            UpdateNavigationDebugSnapshot();
            LogNavigationDebugChange(changed);
        }

        void UpdateNavigationDebugSnapshot()
        {
            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            bool hasCurrentCell = TryGetDebugCell(currentPosition, out Vector2Int currentGridPosition);
            Vector2Int targetGridPosition = default;
            bool hasTargetCell = m_DebugHasTargetPosition && TryGetDebugCell(m_DebugTargetPosition, out targetGridPosition);
            float stalledFor = Application.isPlaying ? Mathf.Max(0f, Time.time - m_LastProgressAt) : 0f;

            EnemyNavigationDebugReporter.DebugSnapshot snapshot = EnemyNavigationDebugReporter.BuildSnapshot(
                new EnemyNavigationDebugReporter.SnapshotContext
                {
                    State = m_State,
                    LastMode = m_DebugLastNavigationMode,
                    IsWaitingAtSearchPoint = m_IsWaitingAtSearchPoint,
                    CurrentPosition = currentPosition,
                    HasCurrentCell = hasCurrentCell,
                    CurrentCell = currentGridPosition,
                    HasTargetPosition = m_DebugHasTargetPosition,
                    TargetPosition = m_DebugTargetPosition,
                    HasTargetCell = hasTargetCell,
                    TargetCell = targetGridPosition,
                    StalledFor = stalledFor,
                    StuckRecoveryAttemptCount = m_StuckRecoveryAttemptCount,
                    NavigationNodeCount = m_NavigationNodePath.Count,
                    NavigationWaypointCount = m_NavigationWaypointPath.Count,
                    GridPathCount = m_GridPath.Count,
                    LocalWaypointCount = m_LocalNavigationWaypoints.Count,
                    CurrentPathGoalCell = m_CurrentPathGoalCell,
                    HasCurrentPathGoalCell = m_HasCurrentPathGoalCell,
                    CurrentNavigationGoalCell = m_CurrentNavigationGoalCell,
                    HasCurrentNavigationGoalCell = m_HasCurrentNavigationGoalCell,
                    CurrentPatrolTargetCell = m_CurrentPatrolTargetCell,
                    HasCurrentPatrolTargetCell = m_HasCurrentPatrolTargetCell,
                    PathQueryCount = m_DebugPathQueryCount,
                    PathCacheHitCount = m_DebugPathCacheHitCount,
                    ReachabilityQueryCount = m_DebugReachabilityQueryCount,
                    ReachabilityCacheHitCount = m_DebugReachabilityCacheHitCount,
                });

            m_DebugStateSummary = snapshot.StateSummary;
            m_DebugPathSummary = snapshot.PathSummary;
            m_DebugTargetSummary = snapshot.TargetSummary;
        }

        void LogNavigationDebugChange(bool changed)
        {
            float now = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            if (!EnemyNavigationDebugReporter.ShouldLogDecision(
                    m_DebugLogNavigationDecisions,
                    changed,
                    now,
                    m_DebugLastLogAt,
                    m_DebugLogMinInterval))
            {
                return;
            }

            m_DebugLastLogAt = now;
            Debug.Log(
                EnemyNavigationDebugReporter.BuildLogMessage(
                    m_DebugLastDecision,
                    m_DebugStateSummary,
                    m_DebugPathSummary,
                    m_DebugLastFailure),
                this);
        }

        bool TryGetDebugCell(Vector3 worldPosition, out Vector2Int gridPosition)
        {
            gridPosition = default;
            return CanUseGridNavigation && m_GridNavigator.TryGetGridPosition(worldPosition, out gridPosition);
        }

        static string FormatDebugCell(Vector2Int cell)
        {
            return EnemyNavigationDebugReporter.FormatCell(cell);
        }

        static string FormatDebugCell(Vector2Int cell, bool hasCell)
        {
            return EnemyNavigationDebugReporter.FormatCell(cell, hasCell);
        }

        static string FormatDebugVector(Vector3 value)
        {
            return EnemyNavigationDebugReporter.FormatVector(value);
        }

        void DrawNavigationDebugGizmos(Vector3 sensorPosition)
        {
            if (!m_DebugNavigationObservability || !m_DebugDrawNavigationGizmos)
                return;

            Vector3 debugOrigin = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            EnemyNavigationDebugReporter.DrawWaypointPath(
                m_NavigationWaypointPath,
                debugOrigin,
                new Color(0.15f, 0.7f, 1f, 0.85f),
                0.08f);
            EnemyNavigationDebugReporter.DrawWaypointPath(
                m_LocalNavigationWaypoints,
                debugOrigin,
                new Color(0.25f, 1f, 0.45f, 0.85f),
                0.1f);
            EnemyNavigationDebugReporter.DrawGridPath(m_GridPath, m_GridNavigator, debugOrigin);

            if (m_DebugHasTargetPosition)
                EnemyNavigationDebugReporter.DrawTarget(sensorPosition, m_DebugTargetPosition, m_State);

#if UNITY_EDITOR
            if (m_DebugDrawNavigationLabels)
            {
                EnemyNavigationDebugReporter.DrawLabel(
                    transform.position + Vector3.up * 2.1f,
                    name,
                    m_DebugStateSummary,
                    m_DebugPathSummary,
                    m_DebugLastDecision,
                    m_DebugLastFailure);
            }
#endif
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

            DrawNavigationDebugGizmos(sensorPosition);
        }
    }
}
