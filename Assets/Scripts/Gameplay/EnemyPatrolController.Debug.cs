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
        void RecordNavigationDebugMode(EnemyNavigationDebugMode mode, Vector3 targetPosition, string decision)
        {
            if (!m_DebugNavigationObservability)
                return;

            bool changed = mode != m_DebugLastNavigationMode || !string.Equals(decision, m_DebugLastDecision, System.StringComparison.Ordinal);
            m_DebugLastNavigationMode = mode;
            m_DebugLastDecision = decision;
            m_DebugTargetPosition = targetPosition;
            m_DebugHasTargetPosition = true;
            UpdateNavigationDebugSnapshot();
            LogNavigationDebugChange(changed);
        }

        void RecordNavigationDebugDecision(string decision, Vector3 targetPosition)
        {
            if (!m_DebugNavigationObservability)
                return;

            bool changed = !string.Equals(decision, m_DebugLastDecision, System.StringComparison.Ordinal);
            m_DebugLastDecision = decision;
            m_DebugTargetPosition = targetPosition;
            m_DebugHasTargetPosition = true;
            UpdateNavigationDebugSnapshot();
            LogNavigationDebugChange(changed);
        }

        void RecordNavigationDebugFailure(string failure, Vector3 targetPosition)
        {
            if (!m_DebugNavigationObservability)
                return;

            bool changed = !string.Equals(failure, m_DebugLastFailure, System.StringComparison.Ordinal);
            m_DebugLastFailure = failure;
            m_DebugTargetPosition = targetPosition;
            m_DebugHasTargetPosition = true;
            UpdateNavigationDebugSnapshot();
            LogNavigationDebugChange(changed);
        }

        void UpdateNavigationDebugSnapshot()
        {
            Vector3 currentPosition = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            string currentCell = TryGetDebugCell(currentPosition, out Vector2Int currentGridPosition)
                ? FormatDebugCell(currentGridPosition)
                : "Unknown";
            string targetCell = m_DebugHasTargetPosition && TryGetDebugCell(m_DebugTargetPosition, out Vector2Int targetGridPosition)
                ? FormatDebugCell(targetGridPosition)
                : "Unknown";
            float stalledFor = Application.isPlaying ? Mathf.Max(0f, Time.time - m_LastProgressAt) : 0f;

            m_DebugStateSummary =
                $"State={m_State}; Mode={m_DebugLastNavigationMode}; Waiting={m_IsWaitingAtSearchPoint}; " +
                $"CurrentCell={currentCell}; StalledFor={stalledFor:0.00}s; StuckAttempts={m_StuckRecoveryAttemptCount}";
            m_DebugPathSummary =
                $"GraphNodes={m_NavigationNodePath.Count}; GraphWaypoints={m_NavigationWaypointPath.Count}; " +
                $"GridCells={m_GridPath.Count}; LocalWaypoints={m_LocalNavigationWaypoints.Count}; " +
                $"GridGoal={FormatDebugCell(m_CurrentPathGoalCell, m_HasCurrentPathGoalCell)}; " +
                $"GraphGoal={FormatDebugCell(m_CurrentNavigationGoalCell, m_HasCurrentNavigationGoalCell)}; " +
                $"PatrolGoal={FormatDebugCell(m_CurrentPatrolTargetCell, m_HasCurrentPatrolTargetCell)}; " +
                $"PathQueries={m_DebugPathQueryCount}; PathCacheHits={m_DebugPathCacheHitCount}; " +
                $"ReachabilityQueries={m_DebugReachabilityQueryCount}; ReachabilityCacheHits={m_DebugReachabilityCacheHitCount}";
            m_DebugTargetSummary = m_DebugHasTargetPosition
                ? $"Target={FormatDebugVector(m_DebugTargetPosition)}; TargetCell={targetCell}; Distance={Vector3.ProjectOnPlane(m_DebugTargetPosition - currentPosition, Vector3.up).magnitude:0.00}m"
                : "No target";
        }

        void LogNavigationDebugChange(bool changed)
        {
            if (!m_DebugLogNavigationDecisions || !changed)
                return;

            float now = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            if (now - m_DebugLastLogAt < Mathf.Max(0.05f, m_DebugLogMinInterval))
                return;

            m_DebugLastLogAt = now;
            Debug.Log(
                $"Enemy navigation debug: {m_DebugLastDecision}\n" +
                $"{m_DebugStateSummary}\n{m_DebugPathSummary}\n" +
                $"LastFailure={m_DebugLastFailure}",
                this);
        }

        bool TryGetDebugCell(Vector3 worldPosition, out Vector2Int gridPosition)
        {
            gridPosition = default;
            return CanUseGridNavigation && m_GridNavigator.TryGetGridPosition(worldPosition, out gridPosition);
        }

        static string FormatDebugCell(Vector2Int cell)
        {
            return $"({cell.x},{cell.y})";
        }

        static string FormatDebugCell(Vector2Int cell, bool hasCell)
        {
            return hasCell ? FormatDebugCell(cell) : "None";
        }

        static string FormatDebugVector(Vector3 value)
        {
            return $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";
        }

        void DrawNavigationDebugGizmos(Vector3 sensorPosition)
        {
            if (!m_DebugNavigationObservability || !m_DebugDrawNavigationGizmos)
                return;

            DrawDebugWaypointPath(m_NavigationWaypointPath, new Color(0.15f, 0.7f, 1f, 0.85f), 0.08f);
            DrawDebugWaypointPath(m_LocalNavigationWaypoints, new Color(0.25f, 1f, 0.45f, 0.85f), 0.1f);
            DrawDebugGridPath();

            if (m_DebugHasTargetPosition)
            {
                Gizmos.color = ResolveDebugTargetColor();
                Vector3 targetPosition = m_DebugTargetPosition + Vector3.up * 0.12f;
                Gizmos.DrawWireSphere(targetPosition, 0.28f);
                Gizmos.DrawLine(sensorPosition, targetPosition);
            }

#if UNITY_EDITOR
            if (m_DebugDrawNavigationLabels)
            {
                Handles.color = Color.white;
                Handles.Label(
                    transform.position + Vector3.up * 2.1f,
                    $"{name}\n{m_DebugStateSummary}\n{m_DebugPathSummary}\nDecision={m_DebugLastDecision}\nFailure={m_DebugLastFailure}");
            }
#endif
        }

        void DrawDebugWaypointPath(IReadOnlyList<Vector3> waypoints, Color color, float nodeRadius)
        {
            if (waypoints == null || waypoints.Count == 0)
                return;

            Gizmos.color = color;
            Vector3 previous = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            previous += Vector3.up * 0.08f;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 waypoint = waypoints[i] + Vector3.up * 0.08f;
                Gizmos.DrawLine(previous, waypoint);
                Gizmos.DrawWireSphere(waypoint, nodeRadius);
                previous = waypoint;
            }
        }

        void DrawDebugGridPath()
        {
            if (!CanUseGridNavigation || m_GridPath.Count == 0)
                return;

            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.85f);
            Vector3 previous = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            previous += Vector3.up * 0.16f;
            for (int i = 0; i < m_GridPath.Count; i++)
            {
                Vector3 waypoint = m_GridNavigator.GetCellWorldCenter(m_GridPath[i]) + Vector3.up * 0.16f;
                Gizmos.DrawLine(previous, waypoint);
                Gizmos.DrawWireCube(waypoint, Vector3.one * 0.18f);
                previous = waypoint;
            }
        }

        Color ResolveDebugTargetColor()
        {
            return m_State switch
            {
                EnemyState.Chase => new Color(1f, 0.1f, 0.08f, 0.95f),
                EnemyState.Search => new Color(1f, 0.78f, 0.12f, 0.95f),
                _ => new Color(0.2f, 0.95f, 0.45f, 0.95f),
            };
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
