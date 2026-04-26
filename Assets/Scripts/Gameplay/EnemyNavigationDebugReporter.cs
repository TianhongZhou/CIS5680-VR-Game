using System.Collections.Generic;
using CIS5680VRGame.Generation;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CIS5680VRGame.Gameplay
{
    internal static class EnemyNavigationDebugReporter
    {
        internal struct SnapshotContext
        {
            public EnemyPatrolController.EnemyState State;
            public EnemyPatrolController.EnemyNavigationDebugMode LastMode;
            public bool IsWaitingAtSearchPoint;
            public Vector3 CurrentPosition;
            public bool HasCurrentCell;
            public Vector2Int CurrentCell;
            public bool HasTargetPosition;
            public Vector3 TargetPosition;
            public bool HasTargetCell;
            public Vector2Int TargetCell;
            public float StalledFor;
            public int StuckRecoveryAttemptCount;
            public int NavigationNodeCount;
            public int NavigationWaypointCount;
            public int GridPathCount;
            public int LocalWaypointCount;
            public Vector2Int CurrentPathGoalCell;
            public bool HasCurrentPathGoalCell;
            public Vector2Int CurrentNavigationGoalCell;
            public bool HasCurrentNavigationGoalCell;
            public Vector2Int CurrentPatrolTargetCell;
            public bool HasCurrentPatrolTargetCell;
            public int PathQueryCount;
            public int PathCacheHitCount;
            public int ReachabilityQueryCount;
            public int ReachabilityCacheHitCount;
        }

        internal readonly struct DebugSnapshot
        {
            public DebugSnapshot(string stateSummary, string pathSummary, string targetSummary)
            {
                StateSummary = stateSummary;
                PathSummary = pathSummary;
                TargetSummary = targetSummary;
            }

            public string StateSummary { get; }
            public string PathSummary { get; }
            public string TargetSummary { get; }
        }

        internal static bool TryRecordMode(
            bool enabled,
            ref EnemyPatrolController.EnemyNavigationDebugMode lastMode,
            ref string lastDecision,
            ref Vector3 targetPosition,
            ref bool hasTargetPosition,
            EnemyPatrolController.EnemyNavigationDebugMode mode,
            Vector3 newTargetPosition,
            string decision,
            out bool changed)
        {
            changed = false;
            if (!enabled)
                return false;

            changed = mode != lastMode || !string.Equals(decision, lastDecision, System.StringComparison.Ordinal);
            lastMode = mode;
            lastDecision = decision;
            targetPosition = newTargetPosition;
            hasTargetPosition = true;
            return true;
        }

        internal static bool TryRecordDecision(
            bool enabled,
            ref string lastDecision,
            ref Vector3 targetPosition,
            ref bool hasTargetPosition,
            string decision,
            Vector3 newTargetPosition,
            out bool changed)
        {
            changed = false;
            if (!enabled)
                return false;

            changed = !string.Equals(decision, lastDecision, System.StringComparison.Ordinal);
            lastDecision = decision;
            targetPosition = newTargetPosition;
            hasTargetPosition = true;
            return true;
        }

        internal static bool TryRecordFailure(
            bool enabled,
            ref string lastFailure,
            ref Vector3 targetPosition,
            ref bool hasTargetPosition,
            string failure,
            Vector3 newTargetPosition,
            out bool changed)
        {
            changed = false;
            if (!enabled)
                return false;

            changed = !string.Equals(failure, lastFailure, System.StringComparison.Ordinal);
            lastFailure = failure;
            targetPosition = newTargetPosition;
            hasTargetPosition = true;
            return true;
        }

        internal static DebugSnapshot BuildSnapshot(SnapshotContext context)
        {
            string currentCell = context.HasCurrentCell ? FormatCell(context.CurrentCell) : "Unknown";
            string targetCell = context.HasTargetPosition && context.HasTargetCell ? FormatCell(context.TargetCell) : "Unknown";

            string stateSummary =
                $"State={context.State}; Mode={context.LastMode}; Waiting={context.IsWaitingAtSearchPoint}; " +
                $"CurrentCell={currentCell}; StalledFor={context.StalledFor:0.00}s; StuckAttempts={context.StuckRecoveryAttemptCount}";
            string pathSummary =
                $"GraphNodes={context.NavigationNodeCount}; GraphWaypoints={context.NavigationWaypointCount}; " +
                $"GridCells={context.GridPathCount}; LocalWaypoints={context.LocalWaypointCount}; " +
                $"GridGoal={FormatCell(context.CurrentPathGoalCell, context.HasCurrentPathGoalCell)}; " +
                $"GraphGoal={FormatCell(context.CurrentNavigationGoalCell, context.HasCurrentNavigationGoalCell)}; " +
                $"PatrolGoal={FormatCell(context.CurrentPatrolTargetCell, context.HasCurrentPatrolTargetCell)}; " +
                $"PathQueries={context.PathQueryCount}; PathCacheHits={context.PathCacheHitCount}; " +
                $"ReachabilityQueries={context.ReachabilityQueryCount}; ReachabilityCacheHits={context.ReachabilityCacheHitCount}";
            string targetSummary = context.HasTargetPosition
                ? $"Target={FormatVector(context.TargetPosition)}; TargetCell={targetCell}; Distance={Vector3.ProjectOnPlane(context.TargetPosition - context.CurrentPosition, Vector3.up).magnitude:0.00}m"
                : "No target";

            return new DebugSnapshot(stateSummary, pathSummary, targetSummary);
        }

        internal static bool ShouldLogDecision(bool logEnabled, bool changed, float now, float lastLogAt, float minInterval)
        {
            return logEnabled && changed && now - lastLogAt >= Mathf.Max(0.05f, minInterval);
        }

        internal static string BuildLogMessage(string lastDecision, string stateSummary, string pathSummary, string lastFailure)
        {
            return $"Enemy navigation debug: {lastDecision}\n" +
                   $"{stateSummary}\n{pathSummary}\n" +
                   $"LastFailure={lastFailure}";
        }

        internal static string FormatCell(Vector2Int cell)
        {
            return $"({cell.x},{cell.y})";
        }

        internal static string FormatCell(Vector2Int cell, bool hasCell)
        {
            return hasCell ? FormatCell(cell) : "None";
        }

        internal static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";
        }

        internal static Color ResolveTargetColor(EnemyPatrolController.EnemyState state)
        {
            return state switch
            {
                EnemyPatrolController.EnemyState.Chase => new Color(1f, 0.1f, 0.08f, 0.95f),
                EnemyPatrolController.EnemyState.Search => new Color(1f, 0.78f, 0.12f, 0.95f),
                _ => new Color(0.2f, 0.95f, 0.45f, 0.95f),
            };
        }

        internal static void DrawWaypointPath(IReadOnlyList<Vector3> waypoints, Vector3 origin, Color color, float nodeRadius)
        {
            if (waypoints == null || waypoints.Count == 0)
                return;

            Gizmos.color = color;
            Vector3 previous = origin + Vector3.up * 0.08f;
            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector3 waypoint = waypoints[i] + Vector3.up * 0.08f;
                Gizmos.DrawLine(previous, waypoint);
                Gizmos.DrawWireSphere(waypoint, nodeRadius);
                previous = waypoint;
            }
        }

        internal static void DrawGridPath(IReadOnlyList<Vector2Int> gridPath, MazeGridNavigator gridNavigator, Vector3 origin)
        {
            if (gridPath == null || gridPath.Count == 0 || gridNavigator == null || !gridNavigator.HasActiveLayout)
                return;

            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.85f);
            Vector3 previous = origin + Vector3.up * 0.16f;
            for (int i = 0; i < gridPath.Count; i++)
            {
                Vector3 waypoint = gridNavigator.GetCellWorldCenter(gridPath[i]) + Vector3.up * 0.16f;
                Gizmos.DrawLine(previous, waypoint);
                Gizmos.DrawWireCube(waypoint, Vector3.one * 0.18f);
                previous = waypoint;
            }
        }

        internal static void DrawTarget(Vector3 sensorPosition, Vector3 targetPosition, EnemyPatrolController.EnemyState state)
        {
            Gizmos.color = ResolveTargetColor(state);
            Vector3 raisedTargetPosition = targetPosition + Vector3.up * 0.12f;
            Gizmos.DrawWireSphere(raisedTargetPosition, 0.28f);
            Gizmos.DrawLine(sensorPosition, raisedTargetPosition);
        }

#if UNITY_EDITOR
        internal static void DrawLabel(
            Vector3 position,
            string objectName,
            string stateSummary,
            string pathSummary,
            string lastDecision,
            string lastFailure)
        {
            Handles.color = Color.white;
            Handles.Label(
                position,
                $"{objectName}\n{stateSummary}\n{pathSummary}\nDecision={lastDecision}\nFailure={lastFailure}");
        }
#endif
    }
}
