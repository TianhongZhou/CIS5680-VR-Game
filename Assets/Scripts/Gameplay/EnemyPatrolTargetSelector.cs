using System.Collections.Generic;
using CIS5680VRGame.Generation;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    internal static class EnemyPatrolTargetSelector
    {
        internal static bool TrySelectNextGridPatrolCell(
            MazeGridNavigator gridNavigator,
            bool canUseGridNavigation,
            MazeCellData currentCell,
            Vector3 roamCenter,
            float patrolRadius,
            Vector2Int lastPatrolCell,
            bool hasLastPatrolCell,
            List<Vector2Int> neighborBuffer,
            out Vector2Int patrolTarget)
        {
            patrolTarget = currentCell != null ? currentCell.GridPosition : default;
            if (currentCell == null || !canUseGridNavigation || gridNavigator == null || neighborBuffer == null)
                return false;

            gridNavigator.GetNeighbors(currentCell.GridPosition, neighborBuffer);
            if (neighborBuffer.Count == 0)
                return false;

            if (!gridNavigator.TryGetNearestCell(roamCenter, out MazeCellData roamCell))
                roamCell = currentCell;

            int patrolRadiusCells = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.5f, patrolRadius) / Mathf.Max(0.01f, gridNavigator.CellSize)));
            int currentDistanceToRoam = GetGridDistance(currentCell.GridPosition, roamCell.GridPosition);

            if (currentDistanceToRoam > patrolRadiusCells)
            {
                patrolTarget = SelectClosestToAnchor(neighborBuffer, roamCell.GridPosition);
                return true;
            }

            int validCount = KeepCandidatesWithinRadius(neighborBuffer, roamCell.GridPosition, patrolRadiusCells);
            if (validCount == 0)
            {
                patrolTarget = SelectClosestToAnchor(neighborBuffer, roamCell.GridPosition);
                return true;
            }

            validCount = FilterLastPatrolCell(neighborBuffer, validCount, lastPatrolCell, hasLastPatrolCell);
            patrolTarget = neighborBuffer[Random.Range(0, validCount)];
            return true;
        }

        internal static bool TrySelectNextNavigationPatrolTarget(
            MazeGridNavigator gridNavigator,
            MazeNavigationGraph navigationGraph,
            bool canUseGridNavigation,
            bool canUseNavigationGraph,
            MazeNavigationModuleRecord currentModule,
            Vector3 roamCenter,
            float patrolRadius,
            float navigationCellSize,
            Vector2Int lastPatrolCell,
            bool hasLastPatrolCell,
            List<Vector2Int> neighborBuffer,
            out Vector2Int patrolTarget)
        {
            patrolTarget = currentModule != null ? currentModule.GridPosition : default;
            if (currentModule == null || !canUseNavigationGraph || navigationGraph == null || neighborBuffer == null)
                return false;

            neighborBuffer.Clear();
            if (!navigationGraph.GetConnectedModuleGridPositions(currentModule.GridPosition, neighborBuffer) || neighborBuffer.Count == 0)
                return false;

            MazeNavigationModuleRecord roamModule = currentModule;
            if (!EnemyPathPlanner.TryResolveNavigationModuleAtWorldPosition(
                    gridNavigator,
                    navigationGraph,
                    canUseGridNavigation,
                    canUseNavigationGraph,
                    roamCenter,
                    out roamModule))
            {
                roamModule = currentModule;
            }

            int patrolRadiusCells = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.5f, patrolRadius) / Mathf.Max(0.01f, navigationCellSize)));
            int currentDistanceToRoam = GetGridDistance(currentModule.GridPosition, roamModule.GridPosition);
            if (currentDistanceToRoam > patrolRadiusCells)
            {
                patrolTarget = SelectClosestToAnchor(neighborBuffer, roamModule.GridPosition);
                return true;
            }

            int validCount = KeepCandidatesWithinRadius(neighborBuffer, roamModule.GridPosition, patrolRadiusCells);
            if (validCount == 0)
            {
                patrolTarget = SelectClosestToAnchor(neighborBuffer, roamModule.GridPosition);
                return true;
            }

            validCount = FilterLastPatrolCell(neighborBuffer, validCount, lastPatrolCell, hasLastPatrolCell);
            patrolTarget = neighborBuffer[Random.Range(0, validCount)];
            return true;
        }

        static int KeepCandidatesWithinRadius(List<Vector2Int> candidates, Vector2Int anchorCell, int patrolRadiusCells)
        {
            int validCount = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (GetGridDistance(candidates[i], anchorCell) > patrolRadiusCells)
                    continue;

                candidates[validCount++] = candidates[i];
            }

            return validCount;
        }

        static int FilterLastPatrolCell(List<Vector2Int> candidates, int candidateCount, Vector2Int lastPatrolCell, bool hasLastPatrolCell)
        {
            if (candidateCount <= 1 || !hasLastPatrolCell)
                return candidateCount;

            int filteredCount = 0;
            for (int i = 0; i < candidateCount; i++)
            {
                if (candidates[i] == lastPatrolCell)
                    continue;

                candidates[filteredCount++] = candidates[i];
            }

            return filteredCount > 0 ? filteredCount : candidateCount;
        }

        static Vector2Int SelectClosestToAnchor(List<Vector2Int> candidates, Vector2Int anchorCell)
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

        static int GetGridDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }
    }
}
