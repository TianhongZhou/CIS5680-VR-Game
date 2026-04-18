using UnityEngine;
using Unity.XR.CoreUtils;
using CIS5680VRGame.Gameplay;

namespace CIS5680VRGame.Balls
{
    public class TeleportImpactEffect : BallImpactEffect
    {
        [SerializeField] XROrigin m_XROrigin;
        [SerializeField] Transform m_FallbackRigRoot;
        [SerializeField] float m_SurfaceOffset = 0.05f;

        void Awake()
        {
            if (m_XROrigin == null)
                m_XROrigin = FindObjectOfType<XROrigin>();
        }

        public override void Apply(in BallImpactContext context)
        {
            if (m_XROrigin == null && m_FallbackRigRoot == null)
                return;

            var destination = context.HitPoint + context.HitNormal * m_SurfaceOffset;

            if (m_XROrigin != null)
            {
                destination += Vector3.up * m_XROrigin.CameraInOriginSpaceHeight;
                m_XROrigin.MoveCameraToWorldLocation(destination);
                PulseAudioService.PlayTeleportArrival(0.96f);
                return;
            }

            m_FallbackRigRoot.position = destination;
            PulseAudioService.PlayTeleportArrival(0.96f);
        }
    }
}
