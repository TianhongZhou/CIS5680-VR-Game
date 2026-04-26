using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
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
            if (!m_NavigationGraph.GetConnectedModuleGridPositions(currentModule.GridPosition, m_NavigationNeighbors) || m_NavigationNeighbors.Count == 0)
                return false;

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
    }
}
