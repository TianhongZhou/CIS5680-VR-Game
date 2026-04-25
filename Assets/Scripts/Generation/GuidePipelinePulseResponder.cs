using System.Collections.Generic;
using UnityEngine;

namespace CIS5680VRGame.Generation
{
    [DisallowMultipleComponent]
    public sealed class GuidePipelinePulseResponder : MonoBehaviour
    {
        static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Color m_FlowColor = new(0.02f, 0.55f, 0.8f, 1f);
        [SerializeField, Min(0.1f)] float m_FlowSpeed = 3.2f;
        [SerializeField, Min(0.1f)] float m_FlowLifetime = 3.4f;
        [SerializeField, Min(0.1f)] float m_MaxFlowDistance = 10f;
        [SerializeField, Min(0f)] float m_ActivationRadiusPadding = 0.35f;
        [SerializeField, Min(0.05f)] float m_ActivationMergeDistance = 0.9f;
        [SerializeField, Range(1, 8)] int m_MaxActiveFlows = 4;
        [SerializeField, Range(8, 48)] int m_TailSampleCount = 36;
        [SerializeField, Min(0.04f)] float m_TailSpacing = 0.08f;
        [SerializeField, Min(0.05f)] float m_MinVisibleFlowLength = 0.16f;
        [SerializeField, Min(0f)] float m_EndInset = 2f;
        [SerializeField, Min(0f)] float m_WallOcclusionPadding = 0.08f;
        [SerializeField, Min(0f)] float m_WallEdgeFadeDistance = 0.22f;
        [SerializeField, Min(0.01f)] float m_FlowWidth = 0.08f;
        [SerializeField, Min(0f)] float m_SurfaceLift = 0.001f;
        [SerializeField, Min(0f)] float m_MinEmission = 0.08f;
        [SerializeField, Min(0f)] float m_MaxEmission = 0.75f;

        readonly List<Vector3> m_PathPoints = new();
        readonly List<float> m_PathDistances = new();
        readonly List<Vector2> m_OccludedIntervals = new();
        readonly List<ActiveFlow> m_ActiveFlows = new();
        MaterialPropertyBlock m_PropertyBlock;
        Material m_FlowMaterial;
        float m_TotalPathLength;

        public void Configure(IReadOnlyList<Vector3> pathPoints, float flowWidth, float surfaceLift)
        {
            Configure(pathPoints, null, flowWidth, surfaceLift);
        }

        public void Configure(
            IReadOnlyList<Vector3> pathPoints,
            IReadOnlyList<Vector2> occludedIntervals,
            float flowWidth,
            float surfaceLift)
        {
            m_PathPoints.Clear();
            if (pathPoints != null)
            {
                for (int i = 0; i < pathPoints.Count; i++)
                    AddPathPoint(pathPoints[i]);
            }

            m_FlowWidth = Mathf.Max(0.01f, flowWidth);
            m_SurfaceLift = Mathf.Max(0f, surfaceLift);
            RebuildPathDistances();
            SetOccludedIntervals(occludedIntervals);
            ClearActiveFlows();
        }

        void OnEnable()
        {
            PulseManager.PulseSpawned += HandlePulseSpawned;
        }

        void OnDisable()
        {
            PulseManager.PulseSpawned -= HandlePulseSpawned;
        }

        void OnDestroy()
        {
            ClearActiveFlows();
            if (m_FlowMaterial != null)
                DestroyImmediateOrRuntime(m_FlowMaterial);
        }

        void Update()
        {
            if (m_ActiveFlows.Count == 0 || m_TotalPathLength <= 0f)
                return;

            float deltaTime = Time.deltaTime;
            for (int i = m_ActiveFlows.Count - 1; i >= 0; i--)
            {
                ActiveFlow flow = m_ActiveFlows[i];
                if (flow.DelayRemaining > 0f)
                {
                    flow.DelayRemaining -= deltaTime;
                    SetFlowVisible(flow, false);
                    continue;
                }

                flow.Age += deltaTime;
                flow.TravelDistance += m_FlowSpeed * deltaTime;

                float lifeFade = 1f - flow.Age / Mathf.Max(0.01f, m_FlowLifetime);
                float distanceFade = 1f - flow.TravelDistance / Mathf.Max(0.01f, m_MaxFlowDistance);
                float intensity = flow.Strength * Mathf.Clamp01(Mathf.Min(lifeFade, distanceFade));
                float flowEndDistance = ResolveFlowEndDistance();
                float headDistance = Mathf.Min(flow.StartDistance + flow.TravelDistance, flowEndDistance);

                if (intensity <= 0.01f)
                {
                    RemoveFlowAt(i);
                    continue;
                }

                UpdateFlowVisual(flow, headDistance, intensity);
            }
        }

        void HandlePulseSpawned(Vector3 origin, Vector3 normal, float maxRadius, Collider sourceCollider)
        {
            if (m_TotalPathLength <= 0f || maxRadius <= 0f)
                return;

            if (!TryFindClosestPathDistance(origin, out float pathDistance, out float worldDistance))
                return;

            if (worldDistance > maxRadius + m_ActivationRadiusPadding)
                return;

            float flowEndDistance = ResolveFlowEndDistance();
            if (pathDistance >= flowEndDistance - m_MinVisibleFlowLength)
                return;

            float normalizedCloseness = 1f - Mathf.Clamp01(worldDistance / Mathf.Max(0.01f, maxRadius));
            float strength = Mathf.Lerp(0.55f, 1f, normalizedCloseness);
            float pulseSpeed = PulseManager.Instance != null ? Mathf.Max(0.01f, PulseManager.Instance.pulseSpeed) : 8f;
            float delay = worldDistance / pulseSpeed;
            ActivateFlow(pathDistance, delay, strength);
        }

        void ActivateFlow(float pathDistance, float delay, float strength)
        {
            for (int i = 0; i < m_ActiveFlows.Count; i++)
            {
                ActiveFlow flow = m_ActiveFlows[i];
                if (Mathf.Abs(flow.StartDistance - pathDistance) > m_ActivationMergeDistance)
                    continue;

                float currentStrength = EstimateCurrentStrength(flow);
                if (strength + 0.02f < currentStrength)
                    return;

                ResetFlow(flow, pathDistance, delay, strength);
                return;
            }

            if (m_ActiveFlows.Count >= Mathf.Max(1, m_MaxActiveFlows))
                RemoveWeakestFlow();

            ActiveFlow newFlow = CreateFlow();
            ResetFlow(newFlow, pathDistance, delay, strength);
            m_ActiveFlows.Add(newFlow);
        }

        float EstimateCurrentStrength(ActiveFlow flow)
        {
            if (flow == null)
                return 0f;

            if (flow.DelayRemaining > 0f)
                return flow.Strength;

            float lifeFade = 1f - flow.Age / Mathf.Max(0.01f, m_FlowLifetime);
            float distanceFade = 1f - flow.TravelDistance / Mathf.Max(0.01f, m_MaxFlowDistance);
            return flow.Strength * Mathf.Clamp01(Mathf.Min(lifeFade, distanceFade));
        }

        void ResetFlow(ActiveFlow flow, float pathDistance, float delay, float strength)
        {
            if (flow == null)
                return;

            flow.StartDistance = pathDistance;
            flow.TravelDistance = 0f;
            flow.Age = 0f;
            flow.DelayRemaining = Mathf.Max(0f, delay);
            flow.Strength = Mathf.Clamp01(strength);
            SetFlowVisible(flow, false);
        }

        ActiveFlow CreateFlow()
        {
            var flow = new ActiveFlow
            {
                Root = new GameObject("GuidePipelinePulseFlow")
            };
            flow.Root.transform.SetParent(transform, false);

            MeshFilter meshFilter = flow.Root.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = flow.Root.AddComponent<MeshRenderer>();
            Mesh mesh = new()
            {
                name = "GuidePipelinePulseFlowMesh",
            };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;

            meshRenderer.sharedMaterial = ResolveFlowMaterial();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;

            flow.Mesh = mesh;
            flow.Renderer = meshRenderer;
            return flow;
        }

        void UpdateFlowVisual(ActiveFlow flow, float headDistance, float intensity)
        {
            if (flow == null || flow.Mesh == null || flow.Renderer == null)
                return;

            if (flow.Root != null && !flow.Root.activeSelf)
                flow.Root.SetActive(true);

            float tailLength = Mathf.Max(m_MinVisibleFlowLength, Mathf.Max(1, m_TailSampleCount - 1) * m_TailSpacing);
            float tailDistance = Mathf.Max(flow.StartDistance, headDistance - tailLength);
            if (headDistance - tailDistance < m_MinVisibleFlowLength)
            {
                flow.Renderer.enabled = false;
                return;
            }

            if (!BuildFlowMesh(flow, tailDistance, headDistance, intensity))
            {
                flow.Renderer.enabled = false;
                return;
            }

            flow.Renderer.enabled = true;
            ApplyFlowIntensity(flow.Renderer, intensity);
        }

        bool BuildFlowMesh(ActiveFlow flow, float tailDistance, float headDistance, float intensity)
        {
            flow.Vertices.Clear();
            flow.Triangles.Clear();
            flow.Colors.Clear();
            flow.Uvs.Clear();

            int segmentCount = Mathf.Clamp(m_TailSampleCount - 1, 7, 47);
            for (int i = 0; i < segmentCount; i++)
            {
                float t0 = i / (float)segmentCount;
                float t1 = (i + 1) / (float)segmentCount;
                float segmentStart = Mathf.Lerp(tailDistance, headDistance, t0);
                float segmentEnd = Mathf.Lerp(tailDistance, headDistance, t1);

                AppendVisibleRanges(segmentStart, segmentEnd, flow.VisibleRanges);
                for (int rangeIndex = 0; rangeIndex < flow.VisibleRanges.Count; rangeIndex++)
                {
                    Vector2 range = flow.VisibleRanges[rangeIndex];
                    if (range.y - range.x <= 0.015f)
                        continue;

                    AppendRibbonSegment(flow, range.x, range.y, tailDistance, headDistance, intensity);
                }
            }

            if (flow.Triangles.Count == 0)
            {
                flow.Mesh.Clear();
                return false;
            }

            flow.Mesh.Clear();
            flow.Mesh.SetVertices(flow.Vertices);
            flow.Mesh.SetColors(flow.Colors);
            flow.Mesh.SetUVs(0, flow.Uvs);
            flow.Mesh.SetTriangles(flow.Triangles, 0, false);
            flow.Mesh.RecalculateBounds();
            return true;
        }

        void AppendRibbonSegment(
            ActiveFlow flow,
            float startDistance,
            float endDistance,
            float tailDistance,
            float headDistance,
            float intensity)
        {
            if (!TrySamplePath(startDistance, out Vector3 startPosition, out Vector3 startTangent)
                || !TrySamplePath(endDistance, out Vector3 endPosition, out Vector3 endTangent))
            {
                return;
            }

            if ((endPosition - startPosition).sqrMagnitude <= 0.0001f)
                return;

            float startWidth = ResolveFlowWidth(startDistance, tailDistance, headDistance, intensity);
            float endWidth = ResolveFlowWidth(endDistance, tailDistance, headDistance, intensity);
            if (startWidth <= 0.001f && endWidth <= 0.001f)
                return;

            Vector3 startRight = ResolveRibbonRight(startTangent);
            Vector3 endRight = ResolveRibbonRight(endTangent);
            Color startColor = ResolveFlowColor(startDistance, tailDistance, headDistance, intensity);
            Color endColor = ResolveFlowColor(endDistance, tailDistance, headDistance, intensity);
            int baseIndex = flow.Vertices.Count;

            flow.Vertices.Add(startPosition - startRight * (startWidth * 0.5f));
            flow.Vertices.Add(startPosition + startRight * (startWidth * 0.5f));
            flow.Vertices.Add(endPosition - endRight * (endWidth * 0.5f));
            flow.Vertices.Add(endPosition + endRight * (endWidth * 0.5f));

            flow.Colors.Add(startColor);
            flow.Colors.Add(startColor);
            flow.Colors.Add(endColor);
            flow.Colors.Add(endColor);

            float startU = Mathf.InverseLerp(tailDistance, headDistance, startDistance);
            float endU = Mathf.InverseLerp(tailDistance, headDistance, endDistance);
            flow.Uvs.Add(new Vector2(startU, 0f));
            flow.Uvs.Add(new Vector2(startU, 1f));
            flow.Uvs.Add(new Vector2(endU, 0f));
            flow.Uvs.Add(new Vector2(endU, 1f));

            flow.Triangles.Add(baseIndex);
            flow.Triangles.Add(baseIndex + 2);
            flow.Triangles.Add(baseIndex + 1);
            flow.Triangles.Add(baseIndex + 1);
            flow.Triangles.Add(baseIndex + 2);
            flow.Triangles.Add(baseIndex + 3);
        }

        void AppendVisibleRanges(float startDistance, float endDistance, List<Vector2> visibleRanges)
        {
            visibleRanges.Clear();
            if (endDistance <= startDistance)
                return;

            if (m_OccludedIntervals.Count == 0)
            {
                visibleRanges.Add(new Vector2(startDistance, endDistance));
                return;
            }

            float currentStart = startDistance;
            for (int i = 0; i < m_OccludedIntervals.Count; i++)
            {
                Vector2 interval = ResolvePaddedOcclusionInterval(m_OccludedIntervals[i]);
                if (interval.y <= currentStart)
                    continue;

                if (interval.x >= endDistance)
                    break;

                if (interval.x > currentStart)
                    visibleRanges.Add(new Vector2(currentStart, Mathf.Min(interval.x, endDistance)));

                currentStart = Mathf.Max(currentStart, interval.y);
                if (currentStart >= endDistance)
                    break;
            }

            if (currentStart < endDistance)
                visibleRanges.Add(new Vector2(currentStart, endDistance));
        }

        Vector3 ResolveRibbonRight(Vector3 tangent)
        {
            Vector3 right = Vector3.Cross(Vector3.up, tangent);
            if (right.sqrMagnitude <= 0.0001f)
                right = Vector3.right;

            return right.normalized;
        }

        float ResolveFlowWidth(float distance, float tailDistance, float headDistance, float intensity)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(tailDistance, headDistance, distance));
            float taper = Mathf.Sin(t * Mathf.PI);
            float edgeFade = ResolveOcclusionEdgeFade(distance);
            return m_FlowWidth * Mathf.Lerp(0.75f, 1.18f, Mathf.Clamp01(intensity)) * taper * edgeFade;
        }

        Color ResolveFlowColor(float distance, float tailDistance, float headDistance, float intensity)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(tailDistance, headDistance, distance));
            float envelope = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.35f);
            float headBias = Mathf.SmoothStep(0.2f, 0.94f, t);
            float occlusionFade = ResolveOcclusionEdgeFade(distance);
            float brightness = Mathf.Lerp(0.18f, 1.35f, headBias) * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(intensity));
            Color color = m_FlowColor * brightness;
            color.a = Mathf.Clamp01(Mathf.Lerp(0.05f, 0.72f, headBias) * envelope * Mathf.Clamp01(intensity) * occlusionFade);
            return color;
        }

        float ResolveOcclusionEdgeFade(float distance)
        {
            if (m_OccludedIntervals.Count == 0 || m_WallEdgeFadeDistance <= 0f)
                return 1f;

            float nearestEdgeDistance = float.MaxValue;
            for (int i = 0; i < m_OccludedIntervals.Count; i++)
            {
                Vector2 interval = ResolvePaddedOcclusionInterval(m_OccludedIntervals[i]);
                if (distance >= interval.x && distance <= interval.y)
                    return 0f;

                float edgeDistance = distance < interval.x
                    ? interval.x - distance
                    : distance - interval.y;
                if (edgeDistance < nearestEdgeDistance)
                    nearestEdgeDistance = edgeDistance;
            }

            if (nearestEdgeDistance == float.MaxValue)
                return 1f;

            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(nearestEdgeDistance / m_WallEdgeFadeDistance));
        }

        Vector2 ResolvePaddedOcclusionInterval(Vector2 interval)
        {
            return new Vector2(
                Mathf.Max(0f, interval.x - m_WallOcclusionPadding),
                Mathf.Min(m_TotalPathLength, interval.y + m_WallOcclusionPadding));
        }

        float ResolveFlowEndDistance()
        {
            if (m_TotalPathLength <= 0f)
                return 0f;

            float inset = Mathf.Max(m_EndInset, m_FlowWidth * 4f, m_TailSpacing * 2f);
            return Mathf.Max(0f, m_TotalPathLength - inset);
        }

        void SetFlowVisible(ActiveFlow flow, bool visible)
        {
            if (flow == null || flow.Root == null)
                return;

            flow.Root.SetActive(visible);
            if (flow.Renderer != null)
                flow.Renderer.enabled = visible;
        }

        void RemoveWeakestFlow()
        {
            if (m_ActiveFlows.Count == 0)
                return;

            int weakestIndex = 0;
            float weakestStrength = float.MaxValue;
            for (int i = 0; i < m_ActiveFlows.Count; i++)
            {
                float currentStrength = EstimateCurrentStrength(m_ActiveFlows[i]);
                if (currentStrength >= weakestStrength)
                    continue;

                weakestStrength = currentStrength;
                weakestIndex = i;
            }

            RemoveFlowAt(weakestIndex);
        }

        void RemoveFlowAt(int index)
        {
            if (index < 0 || index >= m_ActiveFlows.Count)
                return;

            ActiveFlow flow = m_ActiveFlows[index];
            if (flow != null)
            {
                if (flow.Mesh != null)
                    DestroyImmediateOrRuntime(flow.Mesh);
                if (flow.Root != null)
                    DestroyImmediateOrRuntime(flow.Root);
            }

            m_ActiveFlows.RemoveAt(index);
        }

        void ClearActiveFlows()
        {
            for (int i = m_ActiveFlows.Count - 1; i >= 0; i--)
                RemoveFlowAt(i);
        }

        void AddPathPoint(Vector3 point)
        {
            if (m_PathPoints.Count > 0 && Vector3.Distance(m_PathPoints[m_PathPoints.Count - 1], point) <= 0.02f)
                return;

            m_PathPoints.Add(point);
        }

        void RebuildPathDistances()
        {
            m_PathDistances.Clear();
            m_TotalPathLength = 0f;

            if (m_PathPoints.Count == 0)
                return;

            m_PathDistances.Add(0f);
            for (int i = 1; i < m_PathPoints.Count; i++)
            {
                m_TotalPathLength += Vector3.Distance(m_PathPoints[i - 1], m_PathPoints[i]);
                m_PathDistances.Add(m_TotalPathLength);
            }

            if (m_TotalPathLength <= 0.02f)
            {
                m_PathPoints.Clear();
                m_PathDistances.Clear();
                m_TotalPathLength = 0f;
            }
        }

        void SetOccludedIntervals(IReadOnlyList<Vector2> intervals)
        {
            m_OccludedIntervals.Clear();
            if (intervals == null || m_TotalPathLength <= 0f)
                return;

            for (int i = 0; i < intervals.Count; i++)
            {
                Vector2 interval = intervals[i];
                float start = Mathf.Clamp(Mathf.Min(interval.x, interval.y), 0f, m_TotalPathLength);
                float end = Mathf.Clamp(Mathf.Max(interval.x, interval.y), 0f, m_TotalPathLength);
                if (end - start > 0.02f)
                    m_OccludedIntervals.Add(new Vector2(start, end));
            }

            m_OccludedIntervals.Sort((a, b) => a.x.CompareTo(b.x));
            for (int i = m_OccludedIntervals.Count - 2; i >= 0; i--)
            {
                Vector2 current = m_OccludedIntervals[i];
                Vector2 next = m_OccludedIntervals[i + 1];
                if (current.y + 0.01f < next.x)
                    continue;

                m_OccludedIntervals[i] = new Vector2(current.x, Mathf.Max(current.y, next.y));
                m_OccludedIntervals.RemoveAt(i + 1);
            }
        }

        bool TryFindClosestPathDistance(Vector3 point, out float pathDistance, out float worldDistance)
        {
            pathDistance = 0f;
            worldDistance = float.MaxValue;
            if (m_PathPoints.Count < 2 || m_PathDistances.Count != m_PathPoints.Count)
                return false;

            float bestSqrDistance = float.MaxValue;
            for (int i = 1; i < m_PathPoints.Count; i++)
            {
                Vector3 start = m_PathPoints[i - 1];
                Vector3 end = m_PathPoints[i];
                Vector3 segment = end - start;
                float segmentLengthSqr = segment.sqrMagnitude;
                if (segmentLengthSqr <= 0.0001f)
                    continue;

                float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / segmentLengthSqr);
                Vector3 candidate = start + segment * t;
                float sqrDistance = (candidate - point).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                    continue;

                bestSqrDistance = sqrDistance;
                pathDistance = m_PathDistances[i - 1] + Mathf.Sqrt(segmentLengthSqr) * t;
            }

            if (bestSqrDistance == float.MaxValue)
                return false;

            worldDistance = Mathf.Sqrt(bestSqrDistance);
            return true;
        }

        bool TrySamplePath(float pathDistance, out Vector3 position, out Vector3 tangent)
        {
            position = Vector3.zero;
            tangent = Vector3.forward;

            if (m_PathPoints.Count < 2 || m_PathDistances.Count != m_PathPoints.Count)
                return false;

            pathDistance = Mathf.Clamp(pathDistance, 0f, m_TotalPathLength);
            for (int i = 1; i < m_PathPoints.Count; i++)
            {
                float segmentStart = m_PathDistances[i - 1];
                float segmentEnd = m_PathDistances[i];
                if (pathDistance > segmentEnd && i < m_PathPoints.Count - 1)
                    continue;

                Vector3 start = m_PathPoints[i - 1];
                Vector3 end = m_PathPoints[i];
                float segmentLength = segmentEnd - segmentStart;
                if (segmentLength <= 0.0001f)
                    continue;

                float t = Mathf.Clamp01((pathDistance - segmentStart) / segmentLength);
                position = Vector3.Lerp(start, end, t);
                position += Vector3.up * m_SurfaceLift;
                tangent = (end - start).normalized;
                return true;
            }

            return false;
        }

        void ApplyFlowIntensity(Renderer targetRenderer, float intensity)
        {
            if (targetRenderer == null)
                return;

            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            float clampedIntensity = Mathf.Clamp01(intensity);
            Color materialColor = m_FlowColor * Mathf.Lerp(0.35f, 1.1f, clampedIntensity);
            materialColor.a = Mathf.Lerp(0.65f, 1f, clampedIntensity);
            Color emission = m_FlowColor * Mathf.Lerp(m_MinEmission, m_MaxEmission, clampedIntensity);
            emission.a = 1f;

            targetRenderer.GetPropertyBlock(m_PropertyBlock);
            if (targetRenderer.sharedMaterial != null && targetRenderer.sharedMaterial.HasProperty(BaseColorPropertyId))
                m_PropertyBlock.SetColor(BaseColorPropertyId, materialColor);
            if (targetRenderer.sharedMaterial != null && targetRenderer.sharedMaterial.HasProperty(ColorPropertyId))
                m_PropertyBlock.SetColor(ColorPropertyId, materialColor);
            if (targetRenderer.sharedMaterial != null && targetRenderer.sharedMaterial.HasProperty(EmissionColorPropertyId))
                m_PropertyBlock.SetColor(EmissionColorPropertyId, emission);
            targetRenderer.SetPropertyBlock(m_PropertyBlock);
        }

        Material ResolveFlowMaterial()
        {
            if (m_FlowMaterial != null)
                return m_FlowMaterial;

            Shader shader = Shader.Find("CIS5680VRGame/GuidePipelineFlow");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            m_FlowMaterial = shader != null
                ? new Material(shader)
                : null;
            if (m_FlowMaterial == null)
                return null;

            m_FlowMaterial.name = "GeneratedGuidePipelinePulseFlowMaterial";
            m_FlowMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            ConfigureTransparentMaterial(m_FlowMaterial);
            ApplyMaterialColor(m_FlowMaterial, m_FlowColor * 0.2f);
            if (m_FlowMaterial.HasProperty(EmissionColorPropertyId))
            {
                m_FlowMaterial.SetColor(EmissionColorPropertyId, m_FlowColor * m_MinEmission);
                m_FlowMaterial.EnableKeyword("_EMISSION");
            }

            return m_FlowMaterial;
        }

        static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
                return;

            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
        }

        static void ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
                return;

            color.a = 1f;
            if (material.HasProperty(BaseColorPropertyId))
                material.SetColor(BaseColorPropertyId, color);
            if (material.HasProperty(ColorPropertyId))
                material.SetColor(ColorPropertyId, color);
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

        sealed class ActiveFlow
        {
            public GameObject Root;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public readonly List<Vector3> Vertices = new(192);
            public readonly List<int> Triangles = new(288);
            public readonly List<Color> Colors = new(192);
            public readonly List<Vector2> Uvs = new(192);
            public readonly List<Vector2> VisibleRanges = new(4);
            public float StartDistance;
            public float TravelDistance;
            public float Age;
            public float DelayRemaining;
            public float Strength;
        }
    }
}
