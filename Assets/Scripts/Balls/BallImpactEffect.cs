using UnityEngine;

namespace CIS5680VRGame.Balls
{
    public enum BallType
    {
        Teleport = 0,
        Reserved1 = 1,
        Reserved2 = 2,
    }

    public readonly struct BallImpactContext
    {
        public readonly Vector3 HitPoint;
        public readonly Vector3 HitNormal;
        public readonly GameObject BallObject;
        public readonly Collision Collision;

        public BallImpactContext(Vector3 hitPoint, Vector3 hitNormal, GameObject ballObject, Collision collision)
        {
            HitPoint = hitPoint;
            HitNormal = hitNormal;
            BallObject = ballObject;
            Collision = collision;
        }
    }

    public abstract class BallImpactEffect : MonoBehaviour
    {
        [SerializeField] BallType m_BallType = BallType.Teleport;

        public BallType BallType => m_BallType;

        public abstract void Apply(in BallImpactContext context);
    }
}