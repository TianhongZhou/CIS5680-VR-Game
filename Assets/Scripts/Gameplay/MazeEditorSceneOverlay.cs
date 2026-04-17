using System;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public class MazeEditorSceneOverlay : MonoBehaviour
    {
        [SerializeField] bool m_ShowOverlay = true;
        [SerializeField] bool m_AutoCollectColliders = true;
        [SerializeField] Collider[] m_TargetColliders = Array.Empty<Collider>();

        [Header("Floor")]
        [SerializeField] Color m_FloorFillColor = new(0.24f, 0.28f, 0.36f, 0.18f);
        [SerializeField] Color m_FloorOutlineColor = new(0.62f, 0.72f, 0.86f, 0.95f);

        [Header("Walls")]
        [SerializeField] Color m_WallFillColor = new(0.08f, 0.62f, 1f, 0.2f);
        [SerializeField] Color m_WallOutlineColor = new(0.38f, 0.86f, 1f, 1f);

        void Reset()
        {
            RefreshTargets();
        }

        void OnValidate()
        {
            RefreshTargets();
        }

        void OnTransformChildrenChanged()
        {
            RefreshTargets();
        }

        void OnDrawGizmos()
        {
            if (!m_ShowOverlay || Application.isPlaying)
                return;

            if (Camera.current != null && Camera.current.cameraType != CameraType.SceneView)
                return;

            if (m_AutoCollectColliders && (m_TargetColliders == null || m_TargetColliders.Length == 0))
                RefreshTargets();

            if (m_TargetColliders == null)
                return;

            for (int i = 0; i < m_TargetColliders.Length; i++)
            {
                Collider targetCollider = m_TargetColliders[i];
                if (!TryGetOverlayBounds(targetCollider, out Bounds bounds))
                    continue;

                bool isFloor = IsFloorSurface(targetCollider, bounds);
                DrawBounds(bounds, isFloor);
            }
        }

        void RefreshTargets()
        {
            if (!m_AutoCollectColliders)
                return;

            Collider[] childColliders = GetComponentsInChildren<Collider>(true);
            int validCount = 0;

            for (int i = 0; i < childColliders.Length; i++)
            {
                if (IsOverlayCandidate(childColliders[i]))
                    validCount++;
            }

            m_TargetColliders = new Collider[validCount];

            int writeIndex = 0;
            for (int i = 0; i < childColliders.Length; i++)
            {
                Collider childCollider = childColliders[i];
                if (!IsOverlayCandidate(childCollider))
                    continue;

                m_TargetColliders[writeIndex++] = childCollider;
            }
        }

        bool IsOverlayCandidate(Collider targetCollider)
        {
            if (targetCollider == null)
                return false;

            if (targetCollider.GetComponent<Renderer>() == null)
                return false;

            return targetCollider is BoxCollider || targetCollider is MeshCollider;
        }

        bool TryGetOverlayBounds(Collider targetCollider, out Bounds bounds)
        {
            bounds = default;

            if (targetCollider == null || !targetCollider.enabled)
                return false;

            Renderer targetRenderer = targetCollider.GetComponent<Renderer>();
            if (targetRenderer == null || !targetRenderer.enabled)
                return false;

            bounds = targetCollider.bounds;
            return bounds.size.sqrMagnitude > 0.0001f;
        }

        bool IsFloorSurface(Collider targetCollider, Bounds bounds)
        {
            if (targetCollider is MeshCollider)
                return true;

            float horizontalSize = Mathf.Max(bounds.size.x, bounds.size.z);
            if (horizontalSize <= 0.0001f)
                return false;

            return bounds.size.y <= horizontalSize * 0.08f;
        }

        void DrawBounds(Bounds bounds, bool isFloor)
        {
            Gizmos.color = isFloor ? m_FloorFillColor : m_WallFillColor;
            Gizmos.DrawCube(bounds.center, bounds.size);

            Gizmos.color = isFloor ? m_FloorOutlineColor : m_WallOutlineColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
