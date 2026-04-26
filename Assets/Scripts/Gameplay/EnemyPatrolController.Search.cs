using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
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
            RecordNavigationDebugMode(
                EnemyNavigationDebugMode.SearchWait,
                m_SearchTargetPosition,
                "Search wait started.");
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
    }
}
