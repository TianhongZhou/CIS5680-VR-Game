using CIS5680VRGame.Generation;
using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class TutorialGuidePipelineHint : MonoBehaviour
    {
        static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

        const float DefaultClampMinSegmentSpacingRatio = 0.85f;

        [SerializeField] Vector3[] m_WorldPathPoints =
        {
            new(1.3f, 0f, 24.1f),
            new(5f, 0f, 24.1f),
            new(5f, 0f, 24.45f),
            new(11.5f, 0f, 24.45f),
        };
        [SerializeField, Min(0f)] float m_FloorSurfaceY = 0.04f;
        [SerializeField, Min(0.01f)] float m_ConduitHeight = 0.028f;
        [SerializeField, Min(0.05f)] float m_ChannelWidth = 0.28f;
        [SerializeField, Min(0.02f)] float m_CoreWidth = 0.13f;
        [SerializeField, Min(0.1f)] float m_ClampSpacing = 1.36f;
        [SerializeField] Color m_BaseColor = new(0.001f, 0.008f, 0.01f, 1f);
        [SerializeField] Color m_BaseEmission = new(0f, 0.012f, 0.016f, 1f);
        [SerializeField] Color m_GlowColor = new(0.006f, 0.16f, 0.2f, 1f);
        [SerializeField] Color m_GlowEmission = new(0.004f, 0.1f, 0.13f, 1f);
        [SerializeField] Color m_HaloColor = new(0f, 0.025f, 0.032f, 1f);
        [SerializeField] Color m_HaloEmission = new(0f, 0.03f, 0.04f, 1f);

        readonly List<Vector3> m_ResponsePath = new();
        readonly List<Vector2> m_OccludedIntervals = new();
        Material m_BaseMaterial;
        Material m_GlowMaterial;
        Material m_HaloMaterial;

        public void Configure(IReadOnlyList<Vector3> worldPathPoints, float floorSurfaceY)
        {
            if (worldPathPoints != null && worldPathPoints.Count >= 2)
            {
                m_WorldPathPoints = new Vector3[worldPathPoints.Count];
                for (int i = 0; i < worldPathPoints.Count; i++)
                    m_WorldPathPoints[i] = worldPathPoints[i];
            }

            m_FloorSurfaceY = Mathf.Max(0f, floorSurfaceY);
            Rebuild();
        }

        void OnEnable()
        {
            Rebuild();
        }

        void OnDestroy()
        {
            DestroyImmediateOrRuntime(m_BaseMaterial);
            DestroyImmediateOrRuntime(m_GlowMaterial);
            DestroyImmediateOrRuntime(m_HaloMaterial);
        }

        void Rebuild()
        {
            if (m_WorldPathPoints == null || m_WorldPathPoints.Length < 2)
                return;

            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

            ClearGeneratedChildren();
            m_ResponsePath.Clear();
            m_OccludedIntervals.Clear();

            float topOffset = ResolveTopOffset();
            float flowY = ResolveFlowY();
            float currentDistance = 0f;
            for (int i = 0; i < m_WorldPathPoints.Length; i++)
            {
                Vector3 point = FlattenToFloor(m_WorldPathPoints[i], flowY);
                if (m_ResponsePath.Count == 0 || Vector3.Distance(m_ResponsePath[m_ResponsePath.Count - 1], point) > 0.02f)
                    m_ResponsePath.Add(point);
            }

            for (int i = 1; i < m_ResponsePath.Count; i++)
            {
                Vector3 start = FlattenToFloor(m_ResponsePath[i - 1], m_FloorSurfaceY);
                Vector3 end = FlattenToFloor(m_ResponsePath[i], m_FloorSurfaceY);
                float segmentLength = Vector3.Distance(start, end);
                if (segmentLength <= 0.02f)
                    continue;

                float segmentStartDistance = currentDistance;
                float segmentEndDistance = currentDistance + segmentLength;
                CreateSegment($"TutorialGuidePipeline_{i}", start, end, topOffset);
                AddClampOcclusionIntervals(segmentStartDistance, segmentEndDistance, segmentLength);
                currentDistance = segmentEndDistance;
            }

            CreateHubs(topOffset);
            ConfigurePulseRevealVisual();
            ConfigurePulseResponder();
        }

        Vector3 FlattenToFloor(Vector3 point, float y)
        {
            return new Vector3(point.x, y, point.z);
        }

        float ResolveTopOffset()
        {
            return m_FloorSurfaceY + m_ConduitHeight * 0.5f + 0.018f;
        }

        float ResolveFlowY()
        {
            return ResolveTopOffset() + m_ConduitHeight * 0.76f;
        }

        void CreateSegment(string name, Vector3 start, Vector3 end, float topOffset)
        {
            Vector3 delta = Vector3.ProjectOnPlane(end - start, Vector3.up);
            float length = delta.magnitude;
            if (length <= 0.02f)
                return;

            Quaternion rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
            Vector3 center = (start + end) * 0.5f + Vector3.up * (topOffset - m_FloorSurfaceY);
            Vector3 channelScale = new(m_ChannelWidth, m_ConduitHeight, length + m_ChannelWidth);
            Vector3 haloScale = new(m_ChannelWidth * 1.7f, m_ConduitHeight * 0.2f, length + m_ChannelWidth * 1.35f);
            Vector3 coreScale = new(m_CoreWidth, m_ConduitHeight * 0.34f, length + m_CoreWidth);

            CreatePiece($"{name}_Halo", center + Vector3.down * (m_ConduitHeight * 0.42f), rotation, haloScale, ResolveHaloMaterial());
            CreatePiece($"{name}_Channel", center, rotation, channelScale, ResolveBaseMaterial());
            CreatePiece($"{name}_Core", center + Vector3.up * (m_ConduitHeight * 0.58f), rotation, coreScale, ResolveGlowMaterial());
            CreateClamps(name, start, end, topOffset, rotation, length);
        }

        void CreateClamps(string name, Vector3 start, Vector3 end, float topOffset, Quaternion rotation, float length)
        {
            int clampCount = ResolveClampCount(length, m_ClampSpacing);
            if (clampCount <= 0)
                return;

            for (int i = 1; i <= clampCount; i++)
            {
                float t = i / (clampCount + 1f);
                Vector3 position = Vector3.Lerp(start, end, t) + Vector3.up * (topOffset - m_FloorSurfaceY + m_ConduitHeight * 0.62f);
                Vector3 scale = new(m_ChannelWidth * 1.32f, m_ConduitHeight * 0.36f, m_CoreWidth * 1.2f);
                CreatePiece($"{name}_Clamp_{i}", position, rotation, scale, ResolveBaseMaterial());
            }
        }

        void CreateHubs(float topOffset)
        {
            for (int i = 1; i < m_ResponsePath.Count - 1; i++)
            {
                Vector3 previous = Vector3.ProjectOnPlane(m_ResponsePath[i] - m_ResponsePath[i - 1], Vector3.up);
                Vector3 next = Vector3.ProjectOnPlane(m_ResponsePath[i + 1] - m_ResponsePath[i], Vector3.up);
                if (previous.sqrMagnitude <= 0.0001f || next.sqrMagnitude <= 0.0001f)
                    continue;

                if (Vector3.Dot(previous.normalized, next.normalized) > 0.985f)
                    continue;

                Vector3 hubPosition = FlattenToFloor(m_ResponsePath[i], m_FloorSurfaceY) + Vector3.up * (topOffset - m_FloorSurfaceY);
                CreatePiece(
                    $"TutorialGuidePipelineHub_{i}",
                    hubPosition,
                    Quaternion.identity,
                    new Vector3(m_ChannelWidth * 1.2f, m_ConduitHeight, m_ChannelWidth * 1.2f),
                    ResolveBaseMaterial());
                CreatePiece(
                    $"TutorialGuidePipelineHubCore_{i}",
                    hubPosition + Vector3.up * (m_ConduitHeight * 0.58f),
                    Quaternion.identity,
                    new Vector3(m_CoreWidth * 1.45f, m_ConduitHeight * 0.38f, m_CoreWidth * 1.45f),
                    ResolveGlowMaterial());
            }
        }

        void CreatePiece(string name, Vector3 worldPosition, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(transform, false);
            piece.transform.SetPositionAndRotation(worldPosition, rotation);
            piece.transform.localScale = scale;
            piece.layer = ResolveGroundLayer();

            Collider pieceCollider = piece.GetComponent<Collider>();
            if (pieceCollider != null)
                DestroyImmediateOrRuntime(pieceCollider);

            Renderer pieceRenderer = piece.GetComponent<Renderer>();
            if (pieceRenderer != null)
                pieceRenderer.sharedMaterial = material;
        }

        void AddClampOcclusionIntervals(float segmentStartDistance, float segmentEndDistance, float segmentLength)
        {
            int clampCount = ResolveClampCount(segmentLength, m_ClampSpacing);
            if (clampCount <= 0)
                return;

            float halfLength = Mathf.Max(m_CoreWidth * 0.6f, 0.035f);
            for (int i = 1; i <= clampCount; i++)
            {
                float t = i / (clampCount + 1f);
                float centerDistance = Mathf.Lerp(segmentStartDistance, segmentEndDistance, t);
                m_OccludedIntervals.Add(new Vector2(centerDistance - halfLength, centerDistance + halfLength));
            }
        }

        void ConfigurePulseRevealVisual()
        {
            PulseRevealVisual pulseVisual = GetComponent<PulseRevealVisual>();
            if (pulseVisual == null)
                pulseVisual = gameObject.AddComponent<PulseRevealVisual>();

            pulseVisual.SetVisual(
                new Color(0.001f, 0.004f, 0.005f, 1f),
                new Color(0.03f, 0.18f, 0.22f, 1f),
                0.35f);
            pulseVisual.RefreshTargets();
        }

        void ConfigurePulseResponder()
        {
            GuidePipelinePulseResponder responder = GetComponent<GuidePipelinePulseResponder>();
            if (responder == null)
                responder = gameObject.AddComponent<GuidePipelinePulseResponder>();

            float flowWidth = Mathf.Clamp(m_CoreWidth * 0.82f, 0.04f, m_ChannelWidth * 0.38f);
            responder.Configure(m_ResponsePath, m_OccludedIntervals, flowWidth, Mathf.Min(0.0015f, m_ConduitHeight * 0.04f));
        }

        void ClearGeneratedChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediateOrRuntime(transform.GetChild(i).gameObject);
        }

        Material ResolveBaseMaterial()
        {
            if (m_BaseMaterial == null)
                m_BaseMaterial = CreateMaterial("TutorialGuidePipelineBaseMaterial", m_BaseColor, m_BaseEmission);

            return m_BaseMaterial;
        }

        Material ResolveGlowMaterial()
        {
            if (m_GlowMaterial == null)
                m_GlowMaterial = CreateMaterial("TutorialGuidePipelineGlowMaterial", m_GlowColor, m_GlowEmission);

            return m_GlowMaterial;
        }

        Material ResolveHaloMaterial()
        {
            if (m_HaloMaterial == null)
                m_HaloMaterial = CreateMaterial("TutorialGuidePipelineHaloMaterial", m_HaloColor, m_HaloEmission);

            return m_HaloMaterial;
        }

        static Material CreateMaterial(string name, Color color, Color emissionColor)
        {
            Shader shader = ResolveSimpleUnlitShader();
            if (shader == null)
                return null;

            Material material = new(shader)
            {
                name = name,
            };
            ApplyMaterialColor(material, color);
            ApplyMaterialEmission(material, emissionColor);
            return material;
        }

        static Shader ResolveSimpleUnlitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");

            return shader;
        }

        static int ResolveClampCount(float segmentLength, float clampSpacing)
        {
            float safeSpacing = Mathf.Max(0.45f, clampSpacing);
            if (segmentLength < safeSpacing * DefaultClampMinSegmentSpacingRatio)
                return 0;

            return Mathf.Clamp(Mathf.FloorToInt(segmentLength / safeSpacing), 1, 3);
        }

        static int ResolveGroundLayer()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            return groundLayer >= 0 ? groundLayer : 0;
        }

        static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            if (material.HasProperty(BaseColorPropertyId))
                material.SetColor(BaseColorPropertyId, color);
            if (material.HasProperty(ColorPropertyId))
                material.SetColor(ColorPropertyId, color);
        }

        static void ApplyMaterialEmission(Material material, Color emissionColor)
        {
            if (material == null)
                return;

            if (material.HasProperty(EmissionColorPropertyId))
            {
                material.SetColor(EmissionColorPropertyId, emissionColor);
                material.EnableKeyword("_EMISSION");
            }
        }

        static void DestroyImmediateOrRuntime(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
