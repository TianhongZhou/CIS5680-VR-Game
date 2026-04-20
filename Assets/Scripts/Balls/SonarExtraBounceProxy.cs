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

        PulseManager m_PulseManager;
        float m_PulseRadius;
        LayerMask m_ValidGroundLayers;
        float m_MinGroundUpDot;
        bool m_RequireGroundContact;
        float m_SpawnTime;
        bool m_Consumed;

        public static void SpawnFromImpact(
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
            MeshFilter sourceMeshFilter = sourceBall.GetComponent<MeshFilter>();
            MeshRenderer sourceMeshRenderer = sourceBall.GetComponent<MeshRenderer>();
            SphereCollider sourceSphereCollider = sourceBall.GetComponent<SphereCollider>();
            Rigidbody sourceRigidBody = sourceBall.GetComponent<Rigidbody>();

            GameObject proxy = new($"{sourceBall.name}_ExtraBounce");
            proxy.layer = sourceBall.layer;
            proxy.transform.position = CalculateSpawnPosition(context, sourceSphereCollider, sourceBall.transform.lossyScale);
            proxy.transform.rotation = sourceBall.transform.rotation;
            proxy.transform.localScale = sourceBall.transform.lossyScale;

            if (sourceMeshFilter != null)
            {
                MeshFilter meshFilter = proxy.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = sourceMeshFilter.sharedMesh;
            }

            if (sourceMeshRenderer != null)
            {
                MeshRenderer meshRenderer = proxy.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterials = sourceMeshRenderer.sharedMaterials;
                meshRenderer.shadowCastingMode = sourceMeshRenderer.shadowCastingMode;
                meshRenderer.receiveShadows = sourceMeshRenderer.receiveShadows;
            }

            SphereCollider proxyCollider = proxy.AddComponent<SphereCollider>();
            if (sourceSphereCollider != null)
            {
                proxyCollider.radius = sourceSphereCollider.radius;
                proxyCollider.center = sourceSphereCollider.center;
                proxyCollider.material = sourceSphereCollider.material;
            }

            Rigidbody rigidbody = proxy.AddComponent<Rigidbody>();
            if (sourceRigidBody != null)
            {
                rigidbody.mass = sourceRigidBody.mass;
                rigidbody.angularDrag = sourceRigidBody.angularDrag;
                rigidbody.drag = sourceRigidBody.drag;
                rigidbody.useGravity = sourceRigidBody.useGravity;
                rigidbody.interpolation = sourceRigidBody.interpolation;
                rigidbody.collisionDetectionMode = sourceRigidBody.collisionDetectionMode;
            }

            rigidbody.velocity = CalculateBounceVelocity(context, sourceRigidBody);

            SonarExtraBounceProxy bounceProxy = proxy.AddComponent<SonarExtraBounceProxy>();
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
        }

        void Update()
        {
            if (Time.time - m_SpawnTime > DefaultLifetimeSeconds)
                Destroy(gameObject);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (m_Consumed)
                return;

            if (Time.time - m_SpawnTime < MinimumLifetimeSeconds)
                return;

            if (((1 << collision.gameObject.layer) & m_ValidGroundLayers.value) == 0)
                return;

            if (collision.contactCount <= 0)
                return;

            ContactPoint contact = collision.GetContact(0);
            if (m_RequireGroundContact && Vector3.Dot(contact.normal, Vector3.up) < m_MinGroundUpDot)
                return;

            m_Consumed = true;

            if (m_PulseManager != null)
                m_PulseManager.SpawnPulse(contact.point, contact.normal, m_PulseRadius, collision.collider);

            PulseAudioService.PlayPulse(contact.point);
            Destroy(gameObject);
        }

        static Vector3 CalculateSpawnPosition(in BallImpactContext context, SphereCollider sourceSphereCollider, Vector3 lossyScale)
        {
            float colliderRadius = sourceSphereCollider != null ? sourceSphereCollider.radius : 0.05f;
            float worldRadius = colliderRadius * Mathf.Max(lossyScale.x, Mathf.Max(lossyScale.y, lossyScale.z));
            float offset = Mathf.Max(0.05f, worldRadius * 1.25f);
            Vector3 surfaceNormal = context.HitNormal.sqrMagnitude > 0.0001f ? context.HitNormal.normalized : Vector3.up;
            return context.HitPoint + surfaceNormal * offset;
        }

        static Vector3 CalculateBounceVelocity(in BallImpactContext context, Rigidbody sourceRigidBody)
        {
            Vector3 incomingVelocity = context.Collision.relativeVelocity;
            if (incomingVelocity.sqrMagnitude < 0.0001f && sourceRigidBody != null)
                incomingVelocity = sourceRigidBody.velocity;

            if (incomingVelocity.sqrMagnitude < 0.0001f)
                incomingVelocity = Vector3.down * 4f;

            Vector3 surfaceNormal = context.HitNormal.sqrMagnitude > 0.0001f ? context.HitNormal.normalized : Vector3.up;
            Vector3 reflectedVelocity = Vector3.Reflect(incomingVelocity, surfaceNormal);
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(reflectedVelocity, Vector3.up) * 0.35f;
            float upwardSpeed = Mathf.Max(3.25f, Mathf.Abs(Vector3.Dot(incomingVelocity, surfaceNormal)) * 0.65f);
            Vector3 bounceVelocity = horizontalVelocity + Vector3.up * upwardSpeed;

            if (bounceVelocity.sqrMagnitude < 0.0001f)
                bounceVelocity = Vector3.up * 3.25f;

            return bounceVelocity;
        }
    }
}
