using CIS5680VRGame.Gameplay;
using UnityEngine;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class SonarExtraBounceProxy : MonoBehaviour
    {
        const float MinimumLifetimeSeconds = 0.2f;
        const float DefaultLifetimeSeconds = 5f;
        const float PostPulseLingerSeconds = 0.2f;
        const float PostPulseFadeSeconds = 0.55f;
        const float WallBounceVelocityScale = 0.55f;
        const float FloorBounceVelocityScale = 0.32f;
        const float MinWallSurfaceExitSpeed = 1.35f;
        const float MinFloorSurfaceExitSpeed = 0.25f;
        const float MinWallUpSpeed = 1.45f;
        const float MinFloorUpSpeed = 3.05f;
        const float MaxWallUpSpeed = 3.25f;
        const float MaxFloorUpSpeed = 4.35f;
        const float MaxWallHorizontalSpeed = 2.75f;
        const float MaxFloorHorizontalSpeed = 2.25f;

        PulseManager m_PulseManager;
        float m_PulseRadius;
        LayerMask m_ValidGroundLayers;
        float m_MinGroundUpDot;
        bool m_RequireGroundContact;
        float m_SpawnTime;
        bool m_Consumed;

        public bool IsWaitingForExtraBounce => !m_Consumed;

        public static void BeginFromImpact(
            in BallImpactContext context,
            PulseManager pulseManager,
            float pulseRadius,
            LayerMask validGroundLayers,
            float minGroundUpDot,
            bool requireGroundContact)
        {
            if (context.BallObject == null || pulseManager == null || pulseRadius <= 0f)
                return;

            GameObject sourceBall = context.BallObject;
            Rigidbody sourceRigidBody = sourceBall.GetComponent<Rigidbody>();

            if (sourceRigidBody == null)
                return;

            sourceRigidBody.velocity = CalculateBounceVelocity(context, sourceRigidBody);

            SonarExtraBounceProxy bounceProxy = sourceBall.GetComponent<SonarExtraBounceProxy>();
            if (bounceProxy == null)
                bounceProxy = sourceBall.AddComponent<SonarExtraBounceProxy>();

            bounceProxy.Initialize(pulseManager, pulseRadius, validGroundLayers, minGroundUpDot, requireGroundContact);
        }

        void Initialize(
            PulseManager pulseManager,
            float pulseRadius,
            LayerMask validGroundLayers,
            float minGroundUpDot,
            bool requireGroundContact)
        {
            m_PulseManager = pulseManager;
            m_PulseRadius = pulseRadius;
            m_ValidGroundLayers = validGroundLayers;
            m_MinGroundUpDot = minGroundUpDot;
            m_RequireGroundContact = requireGroundContact;
            m_SpawnTime = Time.time;
            m_Consumed = false;
        }

        void Update()
        {
            if (!m_Consumed && Time.time - m_SpawnTime > DefaultLifetimeSeconds)
                Destroy(gameObject);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!CanConsumeImpact())
                return;

            bool allowSpecialLandingSurface = ThrowableBall.IsSpecialLandingSurface(collision.collider);
            bool validGroundImpact = ((1 << collision.collider.gameObject.layer) & m_ValidGroundLayers.value) != 0;
            if (!allowSpecialLandingSurface && !validGroundImpact)
                return;

            if (collision.contactCount <= 0)
                return;

            ContactPoint contact = collision.GetContact(0);
            if (m_RequireGroundContact && Vector3.Dot(contact.normal, Vector3.up) < m_MinGroundUpDot)
                return;

            ConsumeImpact(contact.point, contact.normal, collision.collider);
        }

        void OnTriggerEnter(Collider other)
        {
            TryConsumeTriggerImpact(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryConsumeTriggerImpact(other);
        }

        bool TryConsumeTriggerImpact(Collider other)
        {
            if (!CanConsumeImpact() || !BallLandingSurface.TryGet(other, out BallLandingSurface landingSurface))
                return false;

            Vector3 hitPoint = landingSurface.ResolveHitPoint(transform.position);
            ConsumeImpact(hitPoint, landingSurface.SurfaceNormal, other);
            return true;
        }

        bool CanConsumeImpact()
        {
            if (m_Consumed)
                return false;

            return Time.time - m_SpawnTime >= MinimumLifetimeSeconds;
        }

        void ConsumeImpact(Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider)
        {
            m_Consumed = true;

            if (m_PulseManager != null)
                m_PulseManager.SpawnPulse(hitPoint, hitNormal, m_PulseRadius, hitCollider);

            PulseAudioService.PlayPulse(hitPoint);
            BallFadeOut.Begin(gameObject, PostPulseLingerSeconds, PostPulseFadeSeconds);
        }

        static Vector3 CalculateBounceVelocity(in BallImpactContext context, Rigidbody sourceRigidBody)
        {
            Vector3 surfaceNormal = context.HitNormal.sqrMagnitude > 0.0001f ? context.HitNormal.normalized : Vector3.up;
            float floorFactor = Mathf.Clamp01(surfaceNormal.y);
            Vector3 outgoingVelocity = ResolveOutgoingVelocity(context, sourceRigidBody, surfaceNormal);

            float velocityScale = Mathf.Lerp(WallBounceVelocityScale, FloorBounceVelocityScale, floorFactor);
            Vector3 bounceVelocity = outgoingVelocity * velocityScale;

            float minimumSurfaceExitSpeed = Mathf.Lerp(MinWallSurfaceExitSpeed, MinFloorSurfaceExitSpeed, floorFactor);
            float surfaceExitSpeed = Vector3.Dot(bounceVelocity, surfaceNormal);
            if (surfaceExitSpeed < minimumSurfaceExitSpeed)
                bounceVelocity += surfaceNormal * (minimumSurfaceExitSpeed - surfaceExitSpeed);

            float minimumUpSpeed = Mathf.Lerp(MinWallUpSpeed, MinFloorUpSpeed, floorFactor);
            float maximumUpSpeed = Mathf.Lerp(MaxWallUpSpeed, MaxFloorUpSpeed, floorFactor);
            bounceVelocity.y = Mathf.Clamp(bounceVelocity.y, minimumUpSpeed, maximumUpSpeed);

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(bounceVelocity, Vector3.up);
            float maximumHorizontalSpeed = Mathf.Lerp(MaxWallHorizontalSpeed, MaxFloorHorizontalSpeed, floorFactor);
            if (horizontalVelocity.sqrMagnitude > maximumHorizontalSpeed * maximumHorizontalSpeed)
                bounceVelocity = horizontalVelocity.normalized * maximumHorizontalSpeed + Vector3.up * bounceVelocity.y;

            return bounceVelocity;
        }

        static Vector3 ResolveOutgoingVelocity(in BallImpactContext context, Rigidbody sourceRigidBody, Vector3 surfaceNormal)
        {
            Vector3 rigidbodyVelocity = sourceRigidBody != null ? sourceRigidBody.velocity : Vector3.zero;
            Vector3 relativeVelocity = context.Collision != null ? context.Collision.relativeVelocity : Vector3.zero;

            Vector3 incomingVelocity = ChooseIncomingVelocity(rigidbodyVelocity, relativeVelocity, surfaceNormal);
            if (incomingVelocity.sqrMagnitude < 0.0001f)
                incomingVelocity = Vector3.down * 4f;

            return Vector3.Dot(incomingVelocity, surfaceNormal) < 0f
                ? Vector3.Reflect(incomingVelocity, surfaceNormal)
                : incomingVelocity;
        }

        static Vector3 ChooseIncomingVelocity(Vector3 rigidbodyVelocity, Vector3 relativeVelocity, Vector3 surfaceNormal)
        {
            Vector3 orientedRelativeVelocity = OrientIntoSurface(relativeVelocity, surfaceNormal);
            bool rigidbodyIsMovingIntoSurface = Vector3.Dot(rigidbodyVelocity, surfaceNormal) <= 0f;

            if (rigidbodyVelocity.sqrMagnitude < 0.0001f)
                return orientedRelativeVelocity;

            if (rigidbodyIsMovingIntoSurface)
                return rigidbodyVelocity;

            return orientedRelativeVelocity.sqrMagnitude > rigidbodyVelocity.sqrMagnitude
                ? orientedRelativeVelocity
                : rigidbodyVelocity;
        }

        static Vector3 OrientIntoSurface(Vector3 velocity, Vector3 surfaceNormal)
        {
            if (velocity.sqrMagnitude < 0.0001f)
                return velocity;

            return Vector3.Dot(velocity, surfaceNormal) <= 0f ? velocity : -velocity;
        }
    }
}
