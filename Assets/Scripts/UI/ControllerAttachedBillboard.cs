using UnityEngine;

namespace CIS5680VRGame.UI
{
    [ExecuteAlways]
    public class ControllerAttachedBillboard : MonoBehaviour
    {
        enum RotationMode
        {
            FaceTarget = 0,
            FollowTarget = 1,
        }

        [SerializeField] Transform m_Target;
        [SerializeField] Transform m_FaceTarget;
        [SerializeField] Vector3 m_LocalOffset = new(0.05f, 0.03f, 0.018f);
        [SerializeField] RotationMode m_RotationMode = RotationMode.FollowTarget;
        [SerializeField] Vector3 m_TargetRotationOffsetEuler = new(90f, 0f, 0f);
        [SerializeField] Vector3 m_ShakeAmplitude = new(0.006f, 0.004f, 0f);
        [SerializeField] float m_ShakeDuration = 0.18f;
        [SerializeField] float m_ShakeFrequency = 22f;

        float m_ShakeStartTime;
        float m_ShakeUntilTime;

        public void SetTargets(Transform target, Transform faceTarget)
        {
            m_Target = target;
            m_FaceTarget = faceTarget;
        }

        public void SetLocalOffset(Vector3 offset)
        {
            m_LocalOffset = offset;
        }

        public void TriggerShake()
        {
            if (!Application.isPlaying)
                return;

            m_ShakeStartTime = Time.unscaledTime;
            m_ShakeUntilTime = m_ShakeStartTime + Mathf.Max(0.01f, m_ShakeDuration);
        }

        void LateUpdate()
        {
            ResolveTargets();

            if (m_Target == null)
                return;

            Vector3 worldPosition = m_Target.TransformPoint(m_LocalOffset + GetShakeOffset());
            transform.position = worldPosition;

            if (m_RotationMode == RotationMode.FollowTarget)
            {
                transform.rotation = m_Target.rotation * Quaternion.Euler(m_TargetRotationOffsetEuler);
                return;
            }

            if (m_FaceTarget == null)
                return;

            Vector3 facing = m_FaceTarget.position - worldPosition;
            if (facing.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
        }

        Vector3 GetShakeOffset()
        {
            if (!Application.isPlaying || Time.unscaledTime >= m_ShakeUntilTime)
                return Vector3.zero;

            float elapsed = Time.unscaledTime - m_ShakeStartTime;
            float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, m_ShakeDuration));
            float falloff = 1f - normalized;
            float wave = Mathf.Sin(elapsed * Mathf.Max(1f, m_ShakeFrequency) * Mathf.PI * 2f);
            return m_ShakeAmplitude * (wave * falloff);
        }

        void ResolveTargets()
        {
            if (m_Target == null)
            {
                var targetObject = GameObject.Find("XR Origin (XR Rig)/Camera Offset/Right Controller");
                if (targetObject != null)
                    m_Target = targetObject.transform;
            }

            if (m_FaceTarget == null)
            {
                var headObject = GameObject.Find("XR Origin (XR Rig)/Camera Offset/Main Camera");
                if (headObject != null)
                    m_FaceTarget = headObject.transform;
                else if (Camera.main != null)
                    m_FaceTarget = Camera.main.transform;
            }
        }
    }
}
