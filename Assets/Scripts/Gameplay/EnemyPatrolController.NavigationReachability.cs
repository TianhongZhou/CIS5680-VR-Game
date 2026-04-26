using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
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
                return TryGetCachedGridReachability(startCell.GridPosition, targetCell.GridPosition, targetWorldPosition);
            }

            if (CanUseNavigationGraph)
            {
                usedMazeNavigation = true;
                m_NavigationQueryCache.RecordReachabilityQuery();
                if (m_NavigationGraph.TryFindPath(startWorldPosition, targetWorldPosition, m_NavigationReachabilityPath))
                    return true;
            }

            if (CanUseGridNavigation)
            {
                usedMazeNavigation = true;
                m_NavigationQueryCache.RecordReachabilityQuery();
                if (m_GridNavigator.TryFindPath(startWorldPosition, targetWorldPosition, m_GridReachabilityPath))
                    return true;
            }

            return !usedMazeNavigation;
        }

        bool TryGetCachedGridReachability(Vector2Int startCell, Vector2Int targetCell, Vector3 targetWorldPosition)
        {
            if (TryGetCachedGridReachabilityResult(startCell, targetCell, out bool reachable))
                return reachable;

            m_NavigationQueryCache.RecordReachabilityQuery();
            bool result = m_GridNavigator.TryFindPath(startCell, targetCell, m_GridReachabilityPath);
            CacheGridReachabilityResult(startCell, targetCell, result);
            return result;
        }

        bool TryUseCachedGridReachabilityPath(Vector2Int startCell, Vector2Int targetCell, List<Vector2Int> pathBuffer)
        {
            return m_NavigationQueryCache.TryUseCachedGridReachabilityPath(
                startCell,
                targetCell,
                m_ReachabilityCacheDuration,
                m_GridReachabilityPath,
                pathBuffer);
        }

        bool TryGetCachedGridReachabilityResult(Vector2Int startCell, Vector2Int targetCell, out bool reachable)
        {
            return m_NavigationQueryCache.TryGetCachedGridReachabilityResult(
                startCell,
                targetCell,
                m_ReachabilityCacheDuration,
                out reachable);
        }

        void CacheGridReachabilityResult(Vector2Int startCell, Vector2Int targetCell, bool reachable)
        {
            m_NavigationQueryCache.CacheGridReachabilityResult(startCell, targetCell, reachable);
        }

        bool ShouldSkipFailedNavigationPathRetry(Vector2Int startCell, Vector2Int targetCell, Vector3 targetWorldPosition)
        {
            if (!m_NavigationQueryCache.ShouldSkipFailedNavigationPathRetry(startCell, targetCell, m_PathFailureRetryDelay))
                return false;

            RecordNavigationDebugDecision($"Navigation graph retry skipped for recent failed path {FormatDebugCell(startCell)} -> {FormatDebugCell(targetCell)}.", targetWorldPosition);
            return true;
        }

        bool ShouldSkipFailedGridPathRetry(Vector2Int startCell, Vector2Int targetCell, Vector3 targetWorldPosition)
        {
            if (!m_NavigationQueryCache.ShouldSkipFailedGridPathRetry(startCell, targetCell, m_PathFailureRetryDelay))
                return false;

            RecordNavigationDebugDecision($"Grid path retry skipped for recent failed path {FormatDebugCell(startCell)} -> {FormatDebugCell(targetCell)}.", targetWorldPosition);
            return true;
        }

        void RecordNavigationPathFailure(Vector2Int startCell, Vector2Int targetCell)
        {
            m_NavigationQueryCache.RecordNavigationPathFailure(startCell, targetCell);
        }

        void RecordGridPathFailure(Vector2Int startCell, Vector2Int targetCell)
        {
            m_NavigationQueryCache.RecordGridPathFailure(startCell, targetCell);
        }

        void ClearNavigationPathFailure(Vector2Int startCell, Vector2Int targetCell)
        {
            m_NavigationQueryCache.ClearNavigationPathFailure(startCell, targetCell);
        }

        void ClearGridPathFailure(Vector2Int startCell, Vector2Int targetCell)
        {
            m_NavigationQueryCache.ClearGridPathFailure(startCell, targetCell);
        }

        void ClearNavigationQueryCaches()
        {
            m_NavigationQueryCache.Clear(m_GridReachabilityPath, m_NavigationReachabilityPath);
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
            m_IsCloseDirectChasing = false;
            m_IsUsingCloseLocalNavigation = false;
            ClearGridNavigationState(keepPatrolTarget: false);
            BeginSearchWait();
            MarkMovementProgress();
            RecordNavigationDebugMode(EnemyNavigationDebugMode.SearchWait, currentPosition, "Blocked by maze topology; waiting/searching at current reachable point.");
        }

        void HandleMazeNavigationTopologyChanged()
        {
            ClearGridNavigationState(keepPatrolTarget: false);
            ClearNavigationQueryCaches();
            MarkMovementProgress();
            RecordNavigationDebugMode(EnemyNavigationDebugMode.TopologyRefresh, m_Rigidbody != null ? m_Rigidbody.position : transform.position, "Maze navigation topology changed; cleared cached path state.");
        }

        void SyncMazeNavigationTopologyVersion()
        {
            if (m_KnownMazeNavigationTopologyVersion == s_MazeNavigationTopologyVersion)
                return;

            m_KnownMazeNavigationTopologyVersion = s_MazeNavigationTopologyVersion;
            HandleMazeNavigationTopologyChanged();
        }

        bool EnsureMazeNavigationDataForMovement()
        {
            if (CanUseGridNavigation || CanUseNavigationGraph)
                return true;

            MazeRunBootstrap bootstrap = ResolveMazeRunBootstrap();
            if (bootstrap == null || !bootstrap.EnsureRuntimeNavigationData())
            {
                ClearGridNavigationState(keepPatrolTarget: false);
                RecordNavigationDebugFailure("Maze navigation data is unavailable; cleared stale patrol/path state and using free patrol fallback.", m_Rigidbody != null ? m_Rigidbody.position : transform.position);
                return false;
            }

            if (m_GridNavigator == null)
                m_GridNavigator = bootstrap.GridNavigator;

            if (m_NavigationGraph == null)
                m_NavigationGraph = bootstrap.NavigationGraph;

            ClearNavigationQueryCaches();
            RecordNavigationDebugMode(EnemyNavigationDebugMode.TopologyRefresh, m_Rigidbody != null ? m_Rigidbody.position : transform.position, "Maze navigation data restored for enemy movement.");
            return CanUseGridNavigation || CanUseNavigationGraph;
        }

        MazeRunBootstrap ResolveMazeRunBootstrap()
        {
            if (m_GridNavigator != null && m_GridNavigator.Bootstrap != null)
                return m_GridNavigator.Bootstrap;

            if (m_NavigationGraph != null && m_NavigationGraph.Bootstrap != null)
                return m_NavigationGraph.Bootstrap;

            return FindObjectOfType<MazeRunBootstrap>();
        }
    }
}
