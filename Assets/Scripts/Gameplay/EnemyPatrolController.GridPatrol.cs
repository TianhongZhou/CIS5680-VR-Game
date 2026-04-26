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
            return EnemyPatrolTargetSelector.TrySelectNextGridPatrolCell(
                m_GridNavigator,
                CanUseGridNavigation,
                currentCell,
                m_RoamCenter,
                m_PatrolRadius,
                m_LastPatrolCell,
                m_HasLastPatrolCell,
                m_GridNeighbors,
                out patrolTarget);
        }

        bool TrySelectNextPatrolNavigationTarget(MazeNavigationModuleRecord currentModule, out Vector2Int patrolTarget)
        {
            return EnemyPatrolTargetSelector.TrySelectNextNavigationPatrolTarget(
                m_GridNavigator,
                m_NavigationGraph,
                CanUseGridNavigation,
                CanUseNavigationGraph,
                currentModule,
                m_RoamCenter,
                m_PatrolRadius,
                ResolveNavigationCellSize(),
                m_LastPatrolCell,
                m_HasLastPatrolCell,
                m_NavigationNeighbors,
                out patrolTarget);
        }
    }
}
