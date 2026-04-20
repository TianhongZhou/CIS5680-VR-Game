using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CIS5680VRGame.Gameplay
{
    public sealed class EchoMemoryTrailRenderer : MonoBehaviour
    {
        const string GroundLayerName = "Ground";
        const string TrailRootName = "EchoMemoryTrailRuntime";
        const string ParticleSystemObjectName = "EchoMemoryParticles";
        const float RaycastStartHeight = 1.25f;
        const float RaycastDistance = 4f;
        const float SampleSpacing = 0.35f;
        const float TrailSurfaceOffset = 0.02f;
        const float MinimumGroundUpDot = 0.75f;
        const float MaximumSampleHeightDelta = 0.18f;
        const float ParticleSize = 0.045f;
        const float ClusterOffsetRadius = 0.028f;
        const float BaseAlpha = 0.24f;
        const int MaxParticleCapacity = 768;

        static readonly Color s_TrailColor = new(0.73f, 0.92f, 1f, BaseAlpha);
        static readonly Vector3[] s_ClusterOffsets =
        {
            new(0f, 0f, 0f),
            new(ClusterOffsetRadius, 0f, ClusterOffsetRadius * 0.45f),
            new(-ClusterOffsetRadius * 0.55f, 0f, -ClusterOffsetRadius),
        };

        readonly List<TrailPoint> m_TrailPoints = new();

        Transform m_TrailRoot;
        ParticleSystem m_ParticleSystem;
        Material m_RuntimeMaterial;
        Mesh m_SphereMesh;
        ParticleSystem.Particle[] m_ParticleBuffer;
        Vector3 m_LastGroundPoint;
        bool m_HasLastGroundPoint;
        float m_TrailLengthMeters;
        float m_TrailDurationSeconds;
        int m_GroundLayerMask;

        struct TrailPoint
        {
            public Vector3 Position;
            public float SpawnTime;
        }

        void LateUpdate()
        {
            if (m_TrailLengthMeters <= 0f || m_TrailDurationSeconds <= 0f)
            {
                ClearTrail();
                return;
            }

            if (TrySampleGroundPoint(out Vector3 groundPoint))
            {
                if (!m_HasLastGroundPoint)
                {
                    m_LastGroundPoint = groundPoint;
                    m_HasLastGroundPoint = true;
                }
                else if (Vector3.Distance(m_LastGroundPoint, groundPoint) >= SampleSpacing)
                {
                    if (Mathf.Abs(m_LastGroundPoint.y - groundPoint.y) <= MaximumSampleHeightDelta)
                        AddTrailPoint(groundPoint);

                    m_LastGroundPoint = groundPoint;
                }
            }

            UpdateParticles();
        }

        void OnDisable()
        {
            ClearTrail();
        }

        void OnEnable()
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            m_GroundLayerMask = groundLayer >= 0 ? (1 << groundLayer) : ~0;
        }

        void OnDestroy()
        {
            ClearTrail();

            if (m_TrailRoot != null)
                Destroy(m_TrailRoot.gameObject);

            if (m_RuntimeMaterial != null)
                Destroy(m_RuntimeMaterial);

            if (m_SphereMesh != null)
                Destroy(m_SphereMesh);
        }

        public void ApplyPersistentTrailSettings(int trailLengthMeters, int trailDurationSeconds)
        {
            m_TrailLengthMeters = Mathf.Max(0f, trailLengthMeters);
            m_TrailDurationSeconds = Mathf.Max(0f, trailDurationSeconds);

            if (m_TrailLengthMeters <= 0f || m_TrailDurationSeconds <= 0f)
                ClearTrail();
        }

        bool TrySampleGroundPoint(out Vector3 groundPoint)
        {
            Vector3 rayStart = transform.position + Vector3.up * RaycastStartHeight;
            RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, RaycastDistance, m_GroundLayerMask, QueryTriggerInteraction.Ignore);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, static (left, right) => left.distance.CompareTo(right.distance));
                for (int i = 0; i < hits.Length; i++)
                {
                    RaycastHit hit = hits[i];
                    if (hit.collider == null)
                        continue;

                    Vector3 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
                    if (Vector3.Dot(normal, Vector3.up) < MinimumGroundUpDot)
                        continue;

                    groundPoint = hit.point + normal * TrailSurfaceOffset;
                    return true;
                }
            }

            groundPoint = default;
            return false;
        }

        void AddTrailPoint(Vector3 point)
        {
            EnsureRuntimeResources();
            if (m_ParticleSystem == null)
                return;

            m_TrailPoints.Add(new TrailPoint
            {
                Position = point,
                SpawnTime = Time.time,
            });
        }

        void UpdateParticles()
        {
            EnsureRuntimeResources();
            if (m_ParticleSystem == null)
                return;

            TrimTrailPoints();

            int pointCount = m_TrailPoints.Count;
            if (pointCount <= 0)
            {
                m_ParticleSystem.Clear(true);
                return;
            }

            int maxRenderableParticles = Mathf.Min(pointCount * s_ClusterOffsets.Length, MaxParticleCapacity);
            if (m_ParticleBuffer == null || m_ParticleBuffer.Length < maxRenderableParticles)
                m_ParticleBuffer = new ParticleSystem.Particle[Mathf.Max(maxRenderableParticles, 32)];

            float now = Time.time;
            int firstIndex = Mathf.Max(0, m_TrailPoints.Count - (MaxParticleCapacity / s_ClusterOffsets.Length));
            int particleIndex = 0;
            for (int i = firstIndex; i < m_TrailPoints.Count; i++)
            {
                TrailPoint point = m_TrailPoints[i];
                float age = now - point.SpawnTime;
                if (age >= m_TrailDurationSeconds)
                    continue;

                float normalizedLifetime = Mathf.Clamp01(age / m_TrailDurationSeconds);
                Color color = s_TrailColor;
                color.a = BaseAlpha * Mathf.Pow(1f - normalizedLifetime, 2.4f);
                float particleSize = Mathf.Lerp(ParticleSize, ParticleSize * 0.35f, normalizedLifetime);

                for (int clusterIndex = 0; clusterIndex < s_ClusterOffsets.Length; clusterIndex++)
                {
                    if (particleIndex >= MaxParticleCapacity)
                        break;

                    m_ParticleBuffer[particleIndex] = new ParticleSystem.Particle
                    {
                        position = point.Position + s_ClusterOffsets[clusterIndex],
                        startLifetime = m_TrailDurationSeconds,
                        remainingLifetime = Mathf.Max(0.01f, m_TrailDurationSeconds - age),
                        startSize = particleSize,
                        rotation3D = Vector3.zero,
                        startColor = color,
                    };
                    particleIndex++;
                }
            }

            m_ParticleSystem.SetParticles(m_ParticleBuffer, particleIndex);
        }

        void TrimTrailPoints()
        {
            if (m_TrailPoints.Count == 0)
                return;

            float now = Time.time;
            while (m_TrailPoints.Count > 0 && now - m_TrailPoints[0].SpawnTime > m_TrailDurationSeconds)
                m_TrailPoints.RemoveAt(0);

            while (m_TrailPoints.Count > 1 && ComputeTrailLength() > m_TrailLengthMeters)
                m_TrailPoints.RemoveAt(0);
        }

        float ComputeTrailLength()
        {
            if (m_TrailPoints.Count < 2)
                return 0f;

            float totalLength = 0f;
            for (int i = 1; i < m_TrailPoints.Count; i++)
                totalLength += Vector3.Distance(m_TrailPoints[i - 1].Position, m_TrailPoints[i].Position);

            return totalLength;
        }

        void EnsureRuntimeResources()
        {
            if (m_TrailRoot == null)
            {
                GameObject existingRootObject = GameObject.Find(TrailRootName);
                if (existingRootObject != null)
                    m_TrailRoot = existingRootObject.transform;
                else
                {
                    GameObject root = new(TrailRootName);
                    root.hideFlags = HideFlags.DontSave;
                    m_TrailRoot = root.transform;
                }
            }

            if (m_RuntimeMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                    shader = Shader.Find("Standard");

                if (shader != null)
                {
                    m_RuntimeMaterial = new Material(shader)
                    {
                        name = "EchoMemoryTrailRuntimeMaterial",
                    };

                    if (m_RuntimeMaterial.HasProperty("_Surface"))
                        m_RuntimeMaterial.SetFloat("_Surface", 1f);
                    if (m_RuntimeMaterial.HasProperty("_Blend"))
                        m_RuntimeMaterial.SetFloat("_Blend", 0f);
                    if (m_RuntimeMaterial.HasProperty("_Mode"))
                        m_RuntimeMaterial.SetFloat("_Mode", 3f);
                    if (m_RuntimeMaterial.HasProperty("_AlphaClip"))
                        m_RuntimeMaterial.SetFloat("_AlphaClip", 0f);
                    if (m_RuntimeMaterial.HasProperty("_SrcBlend"))
                        m_RuntimeMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                    if (m_RuntimeMaterial.HasProperty("_DstBlend"))
                        m_RuntimeMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                    if (m_RuntimeMaterial.HasProperty("_ZWrite"))
                        m_RuntimeMaterial.SetInt("_ZWrite", 0);

                    ApplyColor(m_RuntimeMaterial, s_TrailColor);
                    m_RuntimeMaterial.renderQueue = (int)RenderQueue.Transparent;
                }
            }

            if (m_SphereMesh == null)
            {
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                primitive.hideFlags = HideFlags.HideAndDontSave;
                primitive.SetActive(false);
                MeshFilter meshFilter = primitive.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    m_SphereMesh = Instantiate(meshFilter.sharedMesh);
                Destroy(primitive);
            }

            if (m_ParticleSystem == null && m_TrailRoot != null)
            {
                Transform existingParticleTransform = m_TrailRoot.Find(ParticleSystemObjectName);
                if (existingParticleTransform != null)
                    m_ParticleSystem = existingParticleTransform.GetComponent<ParticleSystem>();

                if (m_ParticleSystem == null)
                {
                    GameObject particleObject = new(ParticleSystemObjectName);
                    particleObject.transform.SetParent(m_TrailRoot, false);
                    m_ParticleSystem = particleObject.AddComponent<ParticleSystem>();

                    ParticleSystem.MainModule main = m_ParticleSystem.main;
                    main.loop = false;
                    main.playOnAwake = false;
                    main.maxParticles = MaxParticleCapacity;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    main.scalingMode = ParticleSystemScalingMode.Local;
                    main.startLifetime = m_TrailDurationSeconds > 0f ? m_TrailDurationSeconds : 1f;
                    main.startSize = ParticleSize;
                    main.startColor = s_TrailColor;

                    ParticleSystem.EmissionModule emission = m_ParticleSystem.emission;
                    emission.enabled = false;

                    ParticleSystem.ShapeModule shape = m_ParticleSystem.shape;
                    shape.enabled = false;

                    ParticleSystemRenderer renderer = m_ParticleSystem.GetComponent<ParticleSystemRenderer>();
                    renderer.renderMode = ParticleSystemRenderMode.Mesh;
                    renderer.mesh = m_SphereMesh;
                    renderer.material = m_RuntimeMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    renderer.alignment = ParticleSystemRenderSpace.World;
                }
            }
        }

        void ClearTrail()
        {
            m_TrailPoints.Clear();
            m_HasLastGroundPoint = false;

            if (m_ParticleSystem != null)
                m_ParticleSystem.Clear(true);
        }

        static void ApplyColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }
    }
}
