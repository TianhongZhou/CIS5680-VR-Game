using UnityEngine;

namespace CIS5680VRGame.Balls
{
    public class SonarPulseImpactEffect : BallImpactEffect
    {
        [SerializeField] PulseManager m_PulseManager;
        [SerializeField] float m_PulseRadius = 10f;

        void Awake()
        {
            if (m_PulseManager == null)
                m_PulseManager = FindObjectOfType<PulseManager>();
        }

        public override void Apply(in BallImpactContext context)
        {
            if (m_PulseManager == null)
                m_PulseManager = FindObjectOfType<PulseManager>();

            if (m_PulseManager == null)
                return;

            m_PulseManager.SpawnPulse(context.HitPoint, context.HitNormal, m_PulseRadius, context.Collision.collider);
        }
    }
}
