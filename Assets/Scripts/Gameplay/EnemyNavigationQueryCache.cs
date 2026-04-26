using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    internal sealed class EnemyNavigationQueryCache
    {
        Vector2Int m_LastFailedNavigationPathStartCell;
        Vector2Int m_LastFailedNavigationPathTargetCell;
        Vector2Int m_LastFailedGridPathStartCell;
        Vector2Int m_LastFailedGridPathTargetCell;
        Vector2Int m_CachedReachabilityStartCell;
        Vector2Int m_CachedReachabilityTargetCell;
        bool m_HasFailedNavigationPathCache;
        bool m_HasFailedGridPathCache;
        bool m_HasReachabilityCache;
        bool m_CachedReachabilityResult;
        float m_LastFailedNavigationPathAt = float.NegativeInfinity;
        float m_LastFailedGridPathAt = float.NegativeInfinity;
        float m_LastReachabilityCheckedAt = float.NegativeInfinity;

        internal int PathQueryCount { get; private set; }
        internal int PathCacheHitCount { get; private set; }
        internal int ReachabilityQueryCount { get; private set; }
        internal int ReachabilityCacheHitCount { get; private set; }

        internal void RecordPathQuery()
        {
            PathQueryCount++;
        }

        internal void RecordReachabilityQuery()
        {
            ReachabilityQueryCount++;
        }

        internal bool TryUseCachedGridReachabilityPath(
            Vector2Int startCell,
            Vector2Int targetCell,
            float cacheDuration,
            List<Vector2Int> cachedPath,
            List<Vector2Int> pathBuffer)
        {
            if (!IsRecent(m_LastReachabilityCheckedAt, cacheDuration)
                || !m_HasReachabilityCache
                || !m_CachedReachabilityResult
                || m_CachedReachabilityStartCell != startCell
                || m_CachedReachabilityTargetCell != targetCell
                || cachedPath == null
                || cachedPath.Count == 0)
            {
                return false;
            }

            PathCacheHitCount++;
            pathBuffer.Clear();
            pathBuffer.AddRange(cachedPath);
            return true;
        }

        internal bool TryGetCachedGridReachabilityResult(
            Vector2Int startCell,
            Vector2Int targetCell,
            float cacheDuration,
            out bool reachable)
        {
            reachable = false;
            if (!IsRecent(m_LastReachabilityCheckedAt, cacheDuration)
                || !m_HasReachabilityCache
                || m_CachedReachabilityStartCell != startCell
                || m_CachedReachabilityTargetCell != targetCell)
            {
                return false;
            }

            ReachabilityCacheHitCount++;
            reachable = m_CachedReachabilityResult;
            return true;
        }

        internal void CacheGridReachabilityResult(Vector2Int startCell, Vector2Int targetCell, bool reachable)
        {
            m_CachedReachabilityStartCell = startCell;
            m_CachedReachabilityTargetCell = targetCell;
            m_CachedReachabilityResult = reachable;
            m_HasReachabilityCache = true;
            m_LastReachabilityCheckedAt = Time.time;
        }

        internal bool ShouldSkipFailedNavigationPathRetry(Vector2Int startCell, Vector2Int targetCell, float retryDelay)
        {
            return m_HasFailedNavigationPathCache
                && m_LastFailedNavigationPathStartCell == startCell
                && m_LastFailedNavigationPathTargetCell == targetCell
                && IsRecent(m_LastFailedNavigationPathAt, retryDelay);
        }

        internal bool ShouldSkipFailedGridPathRetry(Vector2Int startCell, Vector2Int targetCell, float retryDelay)
        {
            return m_HasFailedGridPathCache
                && m_LastFailedGridPathStartCell == startCell
                && m_LastFailedGridPathTargetCell == targetCell
                && IsRecent(m_LastFailedGridPathAt, retryDelay);
        }

        internal void RecordNavigationPathFailure(Vector2Int startCell, Vector2Int targetCell)
        {
            m_LastFailedNavigationPathStartCell = startCell;
            m_LastFailedNavigationPathTargetCell = targetCell;
            m_HasFailedNavigationPathCache = true;
            m_LastFailedNavigationPathAt = Time.time;
        }

        internal void RecordGridPathFailure(Vector2Int startCell, Vector2Int targetCell)
        {
            m_LastFailedGridPathStartCell = startCell;
            m_LastFailedGridPathTargetCell = targetCell;
            m_HasFailedGridPathCache = true;
            m_LastFailedGridPathAt = Time.time;
        }

        internal void ClearNavigationPathFailure(Vector2Int startCell, Vector2Int targetCell)
        {
            if (m_HasFailedNavigationPathCache
                && m_LastFailedNavigationPathStartCell == startCell
                && m_LastFailedNavigationPathTargetCell == targetCell)
            {
                m_HasFailedNavigationPathCache = false;
            }
        }

        internal void ClearGridPathFailure(Vector2Int startCell, Vector2Int targetCell)
        {
            if (m_HasFailedGridPathCache
                && m_LastFailedGridPathStartCell == startCell
                && m_LastFailedGridPathTargetCell == targetCell)
            {
                m_HasFailedGridPathCache = false;
            }
        }

        internal void Clear(List<Vector2Int> gridReachabilityPath, List<int> navigationReachabilityPath)
        {
            m_HasFailedNavigationPathCache = false;
            m_HasFailedGridPathCache = false;
            m_HasReachabilityCache = false;
            gridReachabilityPath?.Clear();
            navigationReachabilityPath?.Clear();
        }

        static bool IsRecent(float lastTime, float duration)
        {
            return duration > 0f && Time.time - lastTime <= duration;
        }
    }
}
