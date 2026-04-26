using System.Collections.Generic;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public partial class EnemyPatrolController
    {
        Vector3 GetInitialMoveDirection()
        {
            Vector3 initialDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (initialDirection.sqrMagnitude < 0.0001f)
                initialDirection = Vector3.forward;

            return initialDirection.normalized;
        }

        void UpdateEyeTracking()
        {
            if (m_VisualRoot == null || m_SensorOrigin == null)
                return;

            Vector3 desiredLocalOffset = GetDefaultEyeLocalOffset();
            float blend = Mathf.Clamp01(m_EyeTrackingSmoothing * Time.deltaTime);
            m_CurrentEyeLocalOffset = Vector3.Lerp(m_CurrentEyeLocalOffset, desiredLocalOffset, blend);
            m_SensorOrigin.localPosition = m_CurrentEyeLocalOffset;
            m_SensorOrigin.localRotation = Quaternion.Slerp(m_SensorOrigin.localRotation, Quaternion.identity, blend);
        }

        Vector3 GetDefaultEyeLocalOffset()
        {
            if (m_SensorOrigin != null)
                return new Vector3(0f, m_EyeOrbitHeight, Mathf.Abs(m_SensorOrigin.localPosition.z) > 0.01f ? Mathf.Abs(m_SensorOrigin.localPosition.z) : m_EyeOrbitRadius);

            return new Vector3(0f, m_EyeOrbitHeight, m_EyeOrbitRadius);
        }

        void ClearGridNavigationState(bool keepPatrolTarget)
        {
            m_GridPath.Clear();
            m_HasCurrentPathGoalCell = false;
            ClearNavigationPathState();
            ClearGridSegmentState();
            if (!keepPatrolTarget)
                m_HasCurrentPatrolTargetCell = false;
        }

        void ClearNavigationPathState()
        {
            m_NavigationNodePath.Clear();
            m_NavigationWaypointPath.Clear();
            m_LocalNavigationWaypoints.Clear();
            m_HasCurrentNavigationGoalCell = false;
            m_IsUsingCloseLocalNavigation = false;
        }

        void ClearGridSegmentState()
        {
            m_HasLastGridSegmentStep = false;
        }

        bool IsNearWorldPosition(Vector3 worldPosition)
        {
            Vector3 current = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            Vector3 delta = Vector3.ProjectOnPlane(worldPosition - current, Vector3.up);
            return delta.sqrMagnitude <= m_PathNodeArrivalDistance * m_PathNodeArrivalDistance;
        }

        bool IsNearSearchTarget(Vector3 worldPosition)
        {
            Vector3 current = m_Rigidbody != null ? m_Rigidbody.position : transform.position;
            Vector3 delta = Vector3.ProjectOnPlane(worldPosition - current, Vector3.up);
            float arrivalDistance = Mathf.Max(m_PathNodeArrivalDistance, m_SearchArrivalDistance);
            return delta.sqrMagnitude <= arrivalDistance * arrivalDistance;
        }

        float ResolveClosePursuitExitDistance()
        {
            return m_ClosePursuitDistance + Mathf.Max(0f, m_ClosePursuitDistanceHysteresis);
        }

        bool IsNearGridCell(Vector2Int gridPosition)
        {
            if (!CanUseGridNavigation)
                return false;

            return IsNearWorldPosition(m_GridNavigator.GetCellWorldCenter(gridPosition));
        }

        float ResolveNavigationCellSize()
        {
            if (CanUseGridNavigation)
                return Mathf.Max(0.01f, m_GridNavigator.CellSize);

            if (m_NavigationGraph != null && m_NavigationGraph.ModulePlacer != null)
                return Mathf.Max(0.01f, m_NavigationGraph.ModulePlacer.CellSize);

            return 4f;
        }

        static int GetGridDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        static bool IsCardinalGridStep(Vector2Int step)
        {
            return Mathf.Abs(step.x) + Mathf.Abs(step.y) == 1;
        }

        Vector3 ResolveRoamCenter()
        {
            if (m_RoamAnchor != null)
                return m_RoamAnchor.position;

            if (m_SpawnPosition != Vector3.zero || Application.isPlaying)
                return m_SpawnPosition;

            return transform.position;
        }

        Vector3 GetSensorWorldPosition()
        {
            if (m_SensorOrigin != null)
                return m_SensorOrigin.position;

            return transform.position + Vector3.up * Mathf.Max(0.2f, m_ObstacleProbeHeight);
        }

        Vector3 GetGroundPosition(Vector3 worldPosition)
        {
            return new Vector3(worldPosition.x, m_SpawnPosition.y, worldPosition.z);
        }
    }
}
