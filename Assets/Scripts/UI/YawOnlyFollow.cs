using UnityEngine;

namespace CIS5680VRGame.UI
{
    public class YawOnlyFollow : MonoBehaviour
    {
        [SerializeField] Transform m_Head;
        [SerializeField] float m_Distance = 0.7f;
        [SerializeField] float m_HeightOffset = -0.1f;
        [SerializeField] float m_RightOffset = 0f;

        void LateUpdate()
        {
            if (m_Head == null)
                return;

            Vector3 flatForward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;

            var flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;
            transform.position = m_Head.position + flatForward * m_Distance + flatRight * m_RightOffset + Vector3.up * m_HeightOffset;
            transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        }
    }
}
