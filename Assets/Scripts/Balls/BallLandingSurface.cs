using UnityEngine;

namespace CIS5680VRGame.Balls
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class BallLandingSurface : MonoBehaviour
    {
        [SerializeField] Vector3 m_SurfaceNormal = Vector3.up;

        Collider m_Collider;

        public Vector3 SurfaceNormal
        {
            get
            {
                Vector3 localNormal = m_SurfaceNormal.sqrMagnitude > 0.0001f
                    ? m_SurfaceNormal.normalized
                    : Vector3.up;
                Vector3 worldNormal = transform.TransformDirection(localNormal);
                return worldNormal.sqrMagnitude > 0.0001f ? worldNormal.normalized : Vector3.up;
            }
        }

        void Reset()
        {
            EnsureTriggerCollider();
        }

        void OnValidate()
        {
            if (m_SurfaceNormal.sqrMagnitude < 0.0001f)
                m_SurfaceNormal = Vector3.up;

            EnsureTriggerCollider();
        }

        void Awake()
        {
            EnsureTriggerCollider();
        }

        public Vector3 ResolveHitPoint(Vector3 fallbackPosition)
        {
            Vector3 normal = SurfaceNormal;

            if (TryGetComponent(out BoxCollider boxCollider))
            {
                Vector3 localPoint = transform.InverseTransformPoint(fallbackPosition);
                Vector3 localNormal = m_SurfaceNormal.sqrMagnitude > 0.0001f
                    ? m_SurfaceNormal.normalized
                    : Vector3.up;
                Vector3 center = boxCollider.center;
                Vector3 halfSize = boxCollider.size * 0.5f;

                localPoint -= localNormal * Vector3.Dot(localPoint - center, localNormal);
                localPoint.x = Mathf.Clamp(localPoint.x, center.x - halfSize.x, center.x + halfSize.x);
                localPoint.y = Mathf.Clamp(localPoint.y, center.y - halfSize.y, center.y + halfSize.y);
                localPoint.z = Mathf.Clamp(localPoint.z, center.z - halfSize.z, center.z + halfSize.z);
                return transform.TransformPoint(localPoint);
            }

            return fallbackPosition - normal * Vector3.Dot(fallbackPosition - transform.position, normal);
        }

        internal static bool TryGet(Collider hitCollider, out BallLandingSurface landingSurface)
        {
            landingSurface = null;
            if (hitCollider == null)
                return false;

            landingSurface = hitCollider.GetComponent<BallLandingSurface>();
            if (landingSurface == null)
                landingSurface = hitCollider.GetComponentInParent<BallLandingSurface>();

            return landingSurface != null;
        }

        void EnsureTriggerCollider()
        {
            if (m_Collider == null)
                m_Collider = GetComponent<Collider>();

            if (m_Collider != null)
                m_Collider.isTrigger = true;
        }
    }
}
