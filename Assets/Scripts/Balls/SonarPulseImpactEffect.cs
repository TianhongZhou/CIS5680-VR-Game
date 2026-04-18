using System;
using UnityEngine;
using CIS5680VRGame.Gameplay;

namespace CIS5680VRGame.Balls
{
    public class SonarPulseImpactEffect : BallImpactEffect
    {
        public static event Action<Vector3, float, Collider> PulseSpawned;

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
            PulseAudioService.PlayPulse(context.HitPoint);
            PulseSpawned?.Invoke(context.HitPoint, m_PulseRadius, context.Collision.collider);
        }
    }
}
