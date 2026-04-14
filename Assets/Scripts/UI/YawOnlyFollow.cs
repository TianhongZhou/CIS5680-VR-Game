using UnityEngine;

namespace CIS5680VRGame.UI
{
    public class YawOnlyFollow : MonoBehaviour
    {
        [SerializeField] Transform m_Head;
        [SerializeField] float m_Distance = 0.7f;
        [SerializeField] float m_HeightOffset = -0.1f;

        void LateUpdate()
        {
            if (m_Head == null)
                return;

            Vector3 flatForward = Vector3.ProjectOnPlane(m_Head.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;

            transform.position = m_Head.position + flatForward * m_Distance + Vector3.up * m_HeightOffset;
            transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        }
    }
}
