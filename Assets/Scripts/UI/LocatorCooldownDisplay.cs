using System.Collections.Generic;
using CIS5680VRGame.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace CIS5680VRGame.UI
{
    [DefaultExecutionOrder(275)]
    public sealed class LocatorCooldownDisplay : MonoBehaviour
    {
        const string k_RightControllerPath = "XR Origin (XR Rig)/Camera Offset/Right Controller";
        const string k_MainCameraPath = "XR Origin (XR Rig)/Camera Offset/Main Camera";

        static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int s_ColorId = Shader.PropertyToID("_Color");
        static readonly int s_EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] RefillStationLocatorGuidance m_LocatorGuidance;
        [SerializeField] Transform m_Target;
        [SerializeField] Vector3 m_LocalOffset = new(0.115f, 0.018f, -0.052f);
        [SerializeField] Vector3 m_TargetRotationOffsetEuler = new(90f, 90f, 0f);
        [SerializeField, Range(6, 24)] int m_SegmentCount = 12;
        [SerializeField, Min(0.005f)] float m_InnerRadius = 0.017f;
        [SerializeField, Min(0.006f)] float m_OuterRadius = 0.029f;
        [SerializeField, Range(0f, 10f)] float m_SegmentGapDegrees = 4f;
        [SerializeField] Color m_BackgroundColor = new(0.01f, 0.055f, 0.06f, 0.52f);
        [SerializeField] Color m_CooldownDimColor = new(0.02f, 0.16f, 0.17f, 0.48f);
        [SerializeField] Color m_CooldownFillColor = new(0.16f, 0.78f, 0.68f, 0.92f);
        [SerializeField] Color m_ReadyColor = new(0.28f, 1f, 0.82f, 1f);
        [SerializeField] Color m_PingFlashColor = new(0.52f, 1f, 0.92f, 0.7f);
        [SerializeField, Min(0.1f)] float m_ReadyBreathFrequency = 1.35f;
        [SerializeField, Min(0.05f)] float m_PingFlashDuration = 0.45f;

        readonly List<MeshRenderer> m_SegmentRenderers = new();
        MaterialPropertyBlock m_PropertyBlock;
        Material m_RuntimeMaterial;
        MeshRenderer m_BackgroundRenderer;
        MeshRenderer m_CoreRenderer;
        MeshRenderer m_PingRenderer;
        Transform m_PingTransform;
        bool m_VisualsReady;
        bool m_WasReady;
        float m_PingFlashStartedAt = -999f;

        public void Initialize(RefillStationLocatorGuidance locatorGuidance, Vector3 localOffset)
        {
            m_LocatorGuidance = locatorGuidance;
            m_LocalOffset = localOffset;
            ResolveTarget();
        }

        void Awake()
        {
            m_PropertyBlock = new MaterialPropertyBlock();
            ResolveTarget();
        }

        void OnEnable()
        {
            RefillStationLocatorGuidance.GuidancePingTriggered += OnGuidancePingTriggered;
            EnsureVisuals();
            m_WasReady = m_LocatorGuidance == null || m_LocatorGuidance.IsCooldownReady;
        }

        void OnDisable()
        {
            RefillStationLocatorGuidance.GuidancePingTriggered -= OnGuidancePingTriggered;
        }

        void OnDestroy()
        {
            if (m_RuntimeMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(m_RuntimeMaterial);
            else
                DestroyImmediate(m_RuntimeMaterial);
        }

        void LateUpdate()
        {
            ResolveGuidance();
            ResolveTarget();
            FollowController();
            EnsureVisuals();
            UpdateVisuals();
        }

        void ResolveGuidance()
        {
            if (m_LocatorGuidance == null)
                m_LocatorGuidance = FindObjectOfType<RefillStationLocatorGuidance>();
        }

        void ResolveTarget()
        {
            if (m_Target != null)
                return;

            GameObject targetObject = GameObject.Find(k_RightControllerPath);
            if (targetObject != null)
                m_Target = targetObject.transform;
        }

        void FollowController()
        {
            if (m_Target == null)
                return;

            transform.SetPositionAndRotation(
                m_Target.TransformPoint(m_LocalOffset),
                m_Target.rotation * Quaternion.Euler(m_TargetRotationOffsetEuler));
        }

        void EnsureVisuals()
        {
            if (m_VisualsReady)
                return;

            EnsureMaterial();
            m_BackgroundRenderer = CreateMeshObject("LocatorCooldown_BackRing", CreateArcMesh(m_InnerRadius, m_OuterRadius, -180f, 180f, 96)).Renderer;

            m_SegmentRenderers.Clear();
            float segmentStep = 360f / Mathf.Max(1, m_SegmentCount);
            for (int i = 0; i < m_SegmentCount; i++)
            {
                float clockwiseStart = i * segmentStep + m_SegmentGapDegrees * 0.5f;
                float clockwiseEnd = (i + 1) * segmentStep - m_SegmentGapDegrees * 0.5f;
                float startAngle = 90f - clockwiseEnd;
                float endAngle = 90f - clockwiseStart;
                MeshRenderer segmentRenderer = CreateMeshObject(
                    $"LocatorCooldown_Segment_{i + 1:00}",
                    CreateArcMesh(m_InnerRadius, m_OuterRadius, startAngle, endAngle, 6)).Renderer;
                m_SegmentRenderers.Add(segmentRenderer);
            }

            m_CoreRenderer = CreateMeshObject("LocatorCooldown_ReadyCore", CreateDiscMesh(m_InnerRadius * 0.45f, 24)).Renderer;

            MeshObject ping = CreateMeshObject("LocatorCooldown_PingFlash", CreateArcMesh(m_OuterRadius * 1.06f, m_OuterRadius * 1.18f, -180f, 180f, 96));
            m_PingRenderer = ping.Renderer;
            m_PingTransform = ping.Transform;
            m_PingRenderer.enabled = false;

            m_VisualsReady = true;
        }

        void EnsureMaterial()
        {
            if (m_RuntimeMaterial != null)
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            m_RuntimeMaterial = new Material(shader)
            {
                name = "Runtime_LocatorCooldownDisplay",
                renderQueue = (int)RenderQueue.Transparent,
            };
            ConfigureTransparentMaterial(m_RuntimeMaterial);
        }

        static void ConfigureTransparentMaterial(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        MeshObject CreateMeshObject(string objectName, Mesh mesh)
        {
            var child = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            MeshFilter meshFilter = child.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = m_RuntimeMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            return new MeshObject(child.transform, meshRenderer);
        }

        void UpdateVisuals()
        {
            if (m_LocatorGuidance == null)
                return;

            float progress = m_LocatorGuidance.CooldownProgress;
            bool ready = m_LocatorGuidance.IsCooldownReady;
            float breath = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * m_ReadyBreathFrequency * Mathf.PI * 2f);

            if (ready && !m_WasReady)
                m_PingFlashStartedAt = Time.unscaledTime;
            m_WasReady = ready;

            ApplyColor(m_BackgroundRenderer, m_BackgroundColor);

            for (int i = 0; i < m_SegmentRenderers.Count; i++)
            {
                float segmentStart = i / (float)m_SegmentRenderers.Count;
                float segmentEnd = (i + 1f) / m_SegmentRenderers.Count;
                float segmentFill = Mathf.InverseLerp(segmentStart, segmentEnd, progress);
                Color segmentColor = ready
                    ? ScaleRgb(m_ReadyColor, Mathf.Lerp(0.82f, 1.35f, breath))
                    : Color.Lerp(m_CooldownDimColor, m_CooldownFillColor, Mathf.Clamp01(segmentFill));
                ApplyColor(m_SegmentRenderers[i], segmentColor);
            }

            Color coreColor = ready
                ? ScaleRgb(m_ReadyColor, Mathf.Lerp(0.72f, 1.55f, breath))
                : Color.Lerp(m_CooldownDimColor, m_CooldownFillColor, Mathf.Clamp01(progress * 0.8f));
            ApplyColor(m_CoreRenderer, coreColor);

            UpdatePingFlash();
        }

        void UpdatePingFlash()
        {
            if (m_PingRenderer == null || m_PingTransform == null)
                return;

            float elapsed = Time.unscaledTime - m_PingFlashStartedAt;
            float duration = Mathf.Max(0.05f, m_PingFlashDuration);
            if (elapsed < 0f || elapsed > duration)
            {
                m_PingRenderer.enabled = false;
                return;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            m_PingRenderer.enabled = true;
            m_PingTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.78f, t);
            Color flashColor = m_PingFlashColor;
            flashColor.a *= (1f - t) * (1f - t);
            ApplyColor(m_PingRenderer, flashColor);
        }

        void OnGuidancePingTriggered()
        {
            m_PingFlashStartedAt = Time.unscaledTime;
        }

        void ApplyColor(Renderer targetRenderer, Color color)
        {
            if (targetRenderer == null)
                return;

            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(s_BaseColorId, color);
            m_PropertyBlock.SetColor(s_ColorId, color);
            m_PropertyBlock.SetColor(s_EmissionColorId, color);
            targetRenderer.SetPropertyBlock(m_PropertyBlock);
        }

        static Color ScaleRgb(Color color, float multiplier)
        {
            color.r *= multiplier;
            color.g *= multiplier;
            color.b *= multiplier;
            return color;
        }

        static Mesh CreateArcMesh(float innerRadius, float outerRadius, float startDegrees, float endDegrees, int steps)
        {
            steps = Mathf.Max(2, steps);
            if (endDegrees < startDegrees)
                (startDegrees, endDegrees) = (endDegrees, startDegrees);

            var vertices = new Vector3[(steps + 1) * 2];
            var triangles = new int[steps * 6];
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = Mathf.Lerp(startDegrees, endDegrees, t) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices[i * 2] = direction * innerRadius;
                vertices[i * 2 + 1] = direction * outerRadius;
            }

            for (int i = 0; i < steps; i++)
            {
                int vertex = i * 2;
                int triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 1;
                triangles[triangle + 2] = vertex + 3;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }

            var mesh = new Mesh { name = "LocatorCooldownArc" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh CreateDiscMesh(float radius, int steps)
        {
            steps = Mathf.Max(8, steps);
            var vertices = new Vector3[steps + 1];
            var triangles = new int[steps * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < steps; i++)
            {
                float angle = i / (float)steps * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }

            for (int i = 0; i < steps; i++)
            {
                int next = i == steps - 1 ? 1 : i + 2;
                int triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = i + 1;
                triangles[triangle + 2] = next;
            }

            var mesh = new Mesh { name = "LocatorCooldownDisc" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        readonly struct MeshObject
        {
            public MeshObject(Transform transform, MeshRenderer renderer)
            {
                Transform = transform;
                Renderer = renderer;
            }

            public Transform Transform { get; }
            public MeshRenderer Renderer { get; }
        }
    }
}
