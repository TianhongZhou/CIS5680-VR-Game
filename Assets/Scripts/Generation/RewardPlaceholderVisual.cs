using UnityEngine;

namespace CIS5680VRGame.Generation
{
    [DisallowMultipleComponent]
    public class RewardPlaceholderVisual : MonoBehaviour
    {
        [SerializeField] float m_RotationSpeed = 65f;
        [SerializeField] float m_BobAmplitude = 0.12f;
        [SerializeField] float m_BobFrequency = 1.6f;

        Vector3 m_BaseLocalPosition;

        void Awake()
        {
            m_BaseLocalPosition = transform.localPosition;
        }

        void Update()
        {
            float bobOffset = Mathf.Sin(Time.time * Mathf.PI * 2f * m_BobFrequency) * m_BobAmplitude;
            transform.localPosition = m_BaseLocalPosition + Vector3.up * bobOffset;
            transform.Rotate(Vector3.up, m_RotationSpeed * Time.deltaTime, Space.Self);
        }
    }
}
