using System.Collections.Generic;
using System.Text;
using CIS5680VRGame.Balls;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CIS5680VRGame.Gameplay
{
    [DefaultExecutionOrder(250)]
    public class RefillStationLocatorGuidance : MonoBehaviour
    {
        const string k_MainCameraPath = "XR Origin (XR Rig)/Camera Offset/Main Camera";
        const string k_HighlightMaterialResourcePath = "Materials/M_RefillStationLocatorHighlight";
        const string k_SectorShellMaterialResourcePath = "Materials/M_RefillStationSectorShell";
        const string k_ResponseRingMaterialResourcePath = "Materials/M_RefillStationResponseRing";
        static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int s_PulseId = Shader.PropertyToID("_Pulse");
        static readonly int s_ProgressId = Shader.PropertyToID("_Progress");
        static readonly int s_AlphaId = Shader.PropertyToID("_Alpha");
        static readonly List<InputDevice> s_RightControllerDevices = new();
        static Mesh s_CubeMesh;
        static Mesh s_QuadMesh;

        [SerializeField] XROrigin m_XROrigin;
        [SerializeField] Transform m_ViewTransform;
        [SerializeField] float m_Cooldown = 5f;
        [SerializeField] float m_DisplayDuration = 5f;
        [SerializeField] float m_MaxPingDistance = 50f;
        [SerializeField] float m_ConeAngle = 120f;
        [SerializeField] float m_ForwardThreshold = 0.55f;
        [SerializeField] float m_ResetThreshold = 0.2f;
        [SerializeField] float m_MaxHorizontalMagnitude = 0.55f;
        [SerializeField] Vector3 m_PadScaleMultiplier = new(1.18f, 1.6f, 1.18f);
        [SerializeField] float m_PadLift = 0.02f;
        [SerializeField] float m_PulseFrequency = 2.4f;
        [SerializeField] float m_SectorShellDuration = 0.7f;
        [SerializeField] float m_SectorShellHeight = 1.25f;
        [SerializeField] float m_SectorShellThickness = 0.95f;
        [SerializeField] float m_SectorShellFloorOffset = 0.03f;
        [SerializeField] int m_SectorShellSegments = 24;
        [SerializeField] float m_ResponseRingDuration = 0.65f;
        [SerializeField] Vector3 m_ResponseRingScaleMultiplier = new(1.7f, 1.7f, 1f);
        [SerializeField] Color m_UnvisitedColor = new(1f, 0.64f, 0.12f, 0.9f);
        [SerializeField] Color m_VisitedColor = new(0.18f, 0.84f, 0.96f, 0.92f);
        [SerializeField] Color m_SectorShellColor = new(0.98f, 0.74f, 0.24f, 0.38f);

        readonly List<XRBaseInteractor> m_PlayerInteractors = new();
        readonly List<StationMarker> m_StationMarkers = new();

        Material m_HighlightMaterial;
        Material m_SectorShellMaterial;
        Material m_ResponseRingMaterial;
        MaterialPropertyBlock m_PropertyBlock;
        float m_NextAvailablePingTime;
        bool m_ForwardInputLatched;
        GameObject m_SectorShellObject;
        MeshFilter m_SectorShellMeshFilter;
        MeshRenderer m_SectorShellRenderer;
        Mesh m_SectorShellMesh;
        float m_SectorShellStartedAt = -999f;

        sealed class StationMarker
        {
            public BallRefillStation Station;
            public GameObject RootObject;
            public Transform PadTransform;
            public MeshRenderer PadRenderer;
            public Transform ResponseTransform;
            public MeshRenderer ResponseRenderer;
            public float VisibleFrom;
            public float VisibleUntil;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureGuidanceExists()
        {
            if (FindObjectOfType<RefillStationLocatorGuidance>(true) != null)
                return;

            if (FindObjectOfType<PlayerEnergy>() == null)
                return;

            var locatorObject = new GameObject("RefillStationLocatorGuidance");
            locatorObject.AddComponent<RefillStationLocatorGuidance>();
        }

        void Awake()
        {
            if (!Application.isPlaying)
                return;

            m_PropertyBlock = new MaterialPropertyBlock();
            ResolveReferences();
            EnsureHighlightMaterial();
            EnsureSectorShellMaterial();
            EnsureResponseRingMaterial();
            RefreshStations(forceRebuild: true);
            HideAllMarkers();
            EnsureSectorShellObject();
        }

        void OnDisable()
        {
            if (!Application.isPlaying)
                CleanupRuntimeObjects();
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            ResolveReferences();
            RefreshStations();
            UpdateTriggerState();
            UpdateMarkerVisibility();
        }

        void OnDestroy()
        {
            CleanupRuntimeObjects();
        }

        void CleanupRuntimeObjects()
        {
            for (int i = 0; i < m_StationMarkers.Count; i++)
                DestroyMarker(m_StationMarkers[i]);

            m_StationMarkers.Clear();

            if (m_SectorShellObject != null)
            {
                if (Application.isPlaying)
                    Destroy(m_SectorShellObject);
                else
                    DestroyImmediate(m_SectorShellObject);
            }

            if (m_SectorShellMesh != null)
            {
                if (Application.isPlaying)
                    Destroy(m_SectorShellMesh);
                else
                    DestroyImmediate(m_SectorShellMesh);
            }

            m_SectorShellObject = null;
            m_SectorShellMeshFilter = null;
            m_SectorShellRenderer = null;
            m_SectorShellMesh = null;
        }

        void ResolveReferences()
        {
            if (m_XROrigin == null)
                m_XROrigin = FindObjectOfType<XROrigin>();

            if (m_ViewTransform == null)
            {
                var cameraObject = GameObject.Find(k_MainCameraPath);
                if (cameraObject != null)
                    m_ViewTransform = cameraObject.transform;
                else if (Camera.main != null)
                    m_ViewTransform = Camera.main.transform;
            }

            if (m_PlayerInteractors.Count == 0 && m_XROrigin != null)
            {
                var interactors = m_XROrigin.GetComponentsInChildren<XRBaseInteractor>(true);
                for (int i = 0; i < interactors.Length; i++)
                {
                    XRBaseInteractor interactor = interactors[i];
                    if (interactor == null || interactor is XRSocketInteractor)
                        continue;

                    m_PlayerInteractors.Add(interactor);
                }
            }
        }

        void EnsureHighlightMaterial()
        {
            if (m_HighlightMaterial != null)
                return;

            var loadedMaterial = Resources.Load<Material>(k_HighlightMaterialResourcePath);
            if (loadedMaterial != null)
            {
                m_HighlightMaterial = loadedMaterial;
                return;
            }

            Shader fallbackShader = Shader.Find("CIS5680VRGame/ThroughWallLocatorHighlight");
            if (fallbackShader != null)
                m_HighlightMaterial = new Material(fallbackShader);
        }

        void EnsureSectorShellMaterial()
        {
            if (m_SectorShellMaterial != null)
                return;

            var loadedMaterial = Resources.Load<Material>(k_SectorShellMaterialResourcePath);
            if (loadedMaterial != null)
            {
                m_SectorShellMaterial = loadedMaterial;
                return;
            }

            Shader fallbackShader = Shader.Find("CIS5680VRGame/LocatorSectorShell");
            if (fallbackShader != null)
                m_SectorShellMaterial = new Material(fallbackShader);
        }

        void EnsureResponseRingMaterial()
        {
            if (m_ResponseRingMaterial != null)
                return;

            var loadedMaterial = Resources.Load<Material>(k_ResponseRingMaterialResourcePath);
            if (loadedMaterial != null)
            {
                m_ResponseRingMaterial = loadedMaterial;
                return;
            }

            Shader fallbackShader = Shader.Find("CIS5680VRGame/LocatorResponseRing");
            if (fallbackShader != null)
                m_ResponseRingMaterial = new Material(fallbackShader);
        }

        void EnsureSectorShellObject()
        {
            if (m_SectorShellObject != null || m_SectorShellMaterial == null)
                return;

            var sectorShellObject = new GameObject("LocatorSectorShell");
            sectorShellObject.hideFlags = HideFlags.DontSave;
            m_SectorShellMeshFilter = sectorShellObject.AddComponent<MeshFilter>();
            m_SectorShellRenderer = sectorShellObject.AddComponent<MeshRenderer>();
            m_SectorShellRenderer.sharedMaterial = m_SectorShellMaterial;
            m_SectorShellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_SectorShellRenderer.receiveShadows = false;
            m_SectorShellRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            m_SectorShellRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            m_SectorShellRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            m_SectorShellMesh = new Mesh { name = "LocatorSectorShellMesh" };
            m_SectorShellMesh.MarkDynamic();
            m_SectorShellMeshFilter.sharedMesh = m_SectorShellMesh;

            sectorShellObject.SetActive(false);
            m_SectorShellObject = sectorShellObject;
        }

        void RefreshStations(bool forceRebuild = false)
        {
            var stations = FindObjectsOfType<BallRefillStation>(true);
            if (!forceRebuild && stations.Length == m_StationMarkers.Count)
            {
                bool matches = true;
                for (int i = 0; i < m_StationMarkers.Count; i++)
                {
                    if (m_StationMarkers[i].Station != null)
                        continue;

                    matches = false;
                    break;
                }

                if (matches)
                    return;
            }

            for (int i = 0; i < m_StationMarkers.Count; i++)
                DestroyMarker(m_StationMarkers[i]);

            m_StationMarkers.Clear();

            for (int i = 0; i < stations.Length; i++)
            {
                if (stations[i] == null)
                    continue;

                m_StationMarkers.Add(CreateMarker(stations[i]));
            }
        }

        StationMarker CreateMarker(BallRefillStation station)
        {
            var markerRoot = new GameObject($"{station.name}_LocatorHighlight");
            markerRoot.hideFlags = HideFlags.DontSave;

            Mesh cubeMesh = GetPrimitiveMesh(PrimitiveType.Cube);
            Mesh quadMesh = GetPrimitiveMesh(PrimitiveType.Quad);

            Transform padTransform = CreateHighlightMesh("Pad", markerRoot.transform, cubeMesh, out MeshRenderer padRenderer);
            Transform responseTransform = CreateHighlightMesh("ResponseRing", markerRoot.transform, quadMesh, out MeshRenderer responseRenderer, m_ResponseRingMaterial);
            if (responseRenderer != null)
                responseRenderer.enabled = false;

            markerRoot.SetActive(false);

            return new StationMarker
            {
                Station = station,
                RootObject = markerRoot,
                PadTransform = padTransform,
                PadRenderer = padRenderer,
                ResponseTransform = responseTransform,
                ResponseRenderer = responseRenderer,
                VisibleFrom = 0f,
                VisibleUntil = 0f,
            };
        }

        Transform CreateHighlightMesh(string name, Transform parent, Mesh mesh, out MeshRenderer renderer, Material materialOverride = null)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);

            var meshFilter = child.AddComponent<MeshFilter>();
            renderer = child.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = mesh;
            renderer.sharedMaterial = materialOverride != null ? materialOverride : m_HighlightMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            return child.transform;
        }

        void DestroyMarker(StationMarker marker)
        {
            if (marker?.RootObject == null)
                return;

            if (Application.isPlaying)
                Destroy(marker.RootObject);
            else
                DestroyImmediate(marker.RootObject);
        }

        void HideAllMarkers()
        {
            for (int i = 0; i < m_StationMarkers.Count; i++)
            {
                StationMarker marker = m_StationMarkers[i];
                marker.VisibleFrom = 0f;
                marker.VisibleUntil = 0f;
                if (marker.RootObject != null)
                    marker.RootObject.SetActive(false);
            }
        }

        void UpdateTriggerState()
        {
            if (!TryGetRightStickValue(out Vector2 stickValue))
            {
                m_ForwardInputLatched = false;
                return;
            }

            bool canResetLatch = stickValue.y <= m_ResetThreshold;
            if (!m_ForwardInputLatched && stickValue.y >= m_ForwardThreshold && Mathf.Abs(stickValue.x) <= m_MaxHorizontalMagnitude)
            {
                m_ForwardInputLatched = true;
                TryTriggerGuidancePing();
                return;
            }

            if (canResetLatch)
                m_ForwardInputLatched = false;
        }

        bool TryGetRightStickValue(out Vector2 stickValue)
        {
            stickValue = Vector2.zero;
            s_RightControllerDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand,
                s_RightControllerDevices);

            for (int i = 0; i < s_RightControllerDevices.Count; i++)
            {
                InputDevice device = s_RightControllerDevices[i];
                if (!device.isValid)
                    continue;

                if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out stickValue))
                    return true;
            }

            return false;
        }

        void TryTriggerGuidancePing()
        {
            if (Time.unscaledTime < m_NextAvailablePingTime)
                return;

            if (IsPlayerHoldingObject())
                return;

            Vector3 origin = GetPingOrigin();
            Vector3 forward = GetPingForward();
            float maxDistance = Mathf.Max(0.01f, m_MaxPingDistance);
            float maxAngle = Mathf.Clamp(m_ConeAngle, 1f, 180f) * 0.5f;
            float shellDuration = Mathf.Max(0.05f, m_SectorShellDuration);
            float now = Time.unscaledTime;

            for (int i = 0; i < m_StationMarkers.Count; i++)
            {
                StationMarker marker = m_StationMarkers[i];
                if (marker.Station == null)
                    continue;

                Vector3 stationOffset = marker.Station.transform.position - origin;
                if (stationOffset.sqrMagnitude > maxDistance * maxDistance)
                    continue;

                if (Vector3.Angle(forward, stationOffset) > maxAngle)
                    continue;

                Vector3 planarOffset = Vector3.ProjectOnPlane(stationOffset, Vector3.up);
                float delay = planarOffset.magnitude / maxDistance * shellDuration;
                marker.VisibleFrom = now + delay;
                marker.VisibleUntil = marker.VisibleFrom + Mathf.Max(0.1f, m_DisplayDuration);
                if (marker.RootObject != null)
                    marker.RootObject.SetActive(false);
            }

            TriggerSectorShell(origin, forward);
            m_NextAvailablePingTime = Time.unscaledTime + Mathf.Max(0.1f, m_Cooldown);
        }

        bool IsPlayerHoldingObject()
        {
            for (int i = 0; i < m_PlayerInteractors.Count; i++)
            {
                XRBaseInteractor interactor = m_PlayerInteractors[i];
                if (interactor != null && interactor.hasSelection)
                    return true;
            }

            return false;
        }

        Vector3 GetPingOrigin()
        {
            if (m_ViewTransform != null)
                return m_ViewTransform.position;

            if (m_XROrigin != null)
                return m_XROrigin.transform.position;

            return transform.position;
        }

        Vector3 GetPingForward()
        {
            Vector3 forward = m_ViewTransform != null ? m_ViewTransform.forward : transform.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f && m_XROrigin != null)
                forward = Vector3.ProjectOnPlane(m_XROrigin.transform.forward, Vector3.up);

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            return forward.normalized;
        }

        void UpdateMarkerVisibility()
        {
            for (int i = 0; i < m_StationMarkers.Count; i++)
            {
                StationMarker marker = m_StationMarkers[i];
                float now = Time.unscaledTime;
                bool shouldShow = marker.Station != null && now >= marker.VisibleFrom && now < marker.VisibleUntil;

                if (!shouldShow)
                {
                    if (marker.RootObject != null && marker.RootObject.activeSelf)
                        marker.RootObject.SetActive(false);

                    continue;
                }

                if (marker.RootObject == null)
                    continue;

                if (!marker.RootObject.activeSelf)
                    marker.RootObject.SetActive(true);

                UpdateMarkerTransform(marker);
                UpdateMarkerVisuals(marker);
            }

            UpdateSectorShell();
        }

        void UpdateMarkerTransform(StationMarker marker)
        {
            Transform stationTransform = marker.Station.transform;
            Vector3 stationScale = stationTransform.lossyScale;
            Vector3 stationUp = stationTransform.up.sqrMagnitude < 0.0001f ? Vector3.up : stationTransform.up.normalized;

            marker.PadTransform.SetPositionAndRotation(stationTransform.position + stationUp * Mathf.Max(0.001f, m_PadLift), stationTransform.rotation);
            marker.PadTransform.localScale = Vector3.Scale(stationScale, m_PadScaleMultiplier);

            if (marker.ResponseTransform != null)
            {
                marker.ResponseTransform.SetPositionAndRotation(
                    stationTransform.position + stationUp * Mathf.Max(0.002f, m_PadLift + 0.01f),
                    stationTransform.rotation * Quaternion.Euler(90f, 0f, 0f));
                marker.ResponseTransform.localScale = Vector3.Scale(stationScale, m_ResponseRingScaleMultiplier);
            }
        }

        void UpdateMarkerVisuals(StationMarker marker)
        {
            Color targetColor = marker.Station.HasBeenVisited ? m_VisitedColor : m_UnvisitedColor;
            float wave = 0.76f + 0.24f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Max(0.01f, m_PulseFrequency) * Mathf.PI * 2f));
            Color animatedColor = Color.Lerp(targetColor, Color.white, 0.12f * wave);
            animatedColor.a = targetColor.a * Mathf.Lerp(0.85f, 1f, wave);

            ApplyMarkerProperties(marker.PadRenderer, animatedColor, wave);

            UpdateResponseRing(marker, targetColor);
        }

        void ApplyMarkerProperties(Renderer renderer, Color color, float pulseValue)
        {
            if (renderer == null)
                return;

            renderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(s_BaseColorId, color);
            m_PropertyBlock.SetFloat(s_PulseId, pulseValue);
            renderer.SetPropertyBlock(m_PropertyBlock);
        }

        void UpdateResponseRing(StationMarker marker, Color baseColor)
        {
            if (marker.ResponseRenderer == null)
                return;

            float elapsed = Time.unscaledTime - marker.VisibleFrom;
            float duration = Mathf.Max(0.05f, m_ResponseRingDuration);
            if (elapsed < 0f || elapsed > duration)
            {
                marker.ResponseRenderer.enabled = false;
                return;
            }

            float progress = Mathf.Clamp01(elapsed / duration);
            float pulse = 1f - progress;
            Color ringColor = Color.Lerp(baseColor, Color.white, 0.3f);
            ringColor.a = baseColor.a * Mathf.Lerp(0.75f, 0.08f, progress);

            marker.ResponseRenderer.enabled = true;
            marker.ResponseRenderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(s_BaseColorId, ringColor);
            m_PropertyBlock.SetFloat(s_ProgressId, progress);
            m_PropertyBlock.SetFloat(s_AlphaId, pulse);
            marker.ResponseRenderer.SetPropertyBlock(m_PropertyBlock);
        }

        void TriggerSectorShell(Vector3 origin, Vector3 forward)
        {
            EnsureSectorShellObject();
            if (m_SectorShellObject == null || m_SectorShellRenderer == null || m_SectorShellMesh == null)
                return;

            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;

            Vector3 worldOrigin = origin;
            if (m_XROrigin != null)
                worldOrigin.y = m_XROrigin.transform.position.y + Mathf.Max(0.001f, m_SectorShellFloorOffset);

            m_SectorShellObject.transform.SetPositionAndRotation(
                worldOrigin,
                Quaternion.LookRotation(flatForward.normalized, Vector3.up));
            m_SectorShellStartedAt = Time.unscaledTime;
            m_SectorShellObject.SetActive(true);
            UpdateSectorShell();
        }

        void UpdateSectorShell()
        {
            if (m_SectorShellObject == null || m_SectorShellRenderer == null || m_SectorShellMesh == null)
                return;

            if (!m_SectorShellObject.activeSelf)
                return;

            float duration = Mathf.Max(0.05f, m_SectorShellDuration);
            float elapsed = Time.unscaledTime - m_SectorShellStartedAt;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (progress >= 1f)
            {
                m_SectorShellObject.SetActive(false);
                return;
            }

            float currentRadius = Mathf.Max(0.08f, Mathf.Lerp(0.1f, m_MaxPingDistance, progress));
            float shellThickness = Mathf.Max(0.08f, m_SectorShellThickness);
            float innerRadius = Mathf.Max(0.02f, currentRadius - shellThickness);
            float outerRadius = currentRadius;
            RebuildSectorShellMesh(m_SectorShellMesh, innerRadius, outerRadius, m_ConeAngle * 0.5f, Mathf.Max(0.2f, m_SectorShellHeight), Mathf.Max(6, m_SectorShellSegments));

            Color pulseColor = m_SectorShellColor;
            float fade = 1f - progress;
            pulseColor.a *= Mathf.Lerp(1f, 0.12f, progress);

            m_SectorShellRenderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetColor(s_BaseColorId, pulseColor);
            m_PropertyBlock.SetFloat(s_ProgressId, progress);
            m_PropertyBlock.SetFloat(s_AlphaId, fade);
            m_SectorShellRenderer.SetPropertyBlock(m_PropertyBlock);
        }

        static void RebuildSectorShellMesh(Mesh mesh, float innerRadius, float outerRadius, float halfAngleDeg, float height, int segments)
        {
            if (mesh == null)
                return;

            segments = Mathf.Max(3, segments);
            innerRadius = Mathf.Max(0.01f, innerRadius);
            outerRadius = Mathf.Max(innerRadius + 0.01f, outerRadius);
            height = Mathf.Max(0.05f, height);

            var vertices = new List<Vector3>(segments * 8 + 8);
            var normals = new List<Vector3>(segments * 8 + 8);
            var uvs = new List<Vector2>(segments * 8 + 8);
            var triangles = new List<int>(segments * 36);

            BuildCurvedFace(vertices, normals, uvs, triangles, innerRadius, halfAngleDeg, height, segments, false);
            BuildCurvedFace(vertices, normals, uvs, triangles, outerRadius, halfAngleDeg, height, segments, true);
            BuildRadialCap(vertices, normals, uvs, triangles, innerRadius, outerRadius, -halfAngleDeg, height, false);
            BuildRadialCap(vertices, normals, uvs, triangles, innerRadius, outerRadius, halfAngleDeg, height, true);
            BuildHorizontalCap(vertices, normals, uvs, triangles, innerRadius, outerRadius, halfAngleDeg, height, false);
            BuildHorizontalCap(vertices, normals, uvs, triangles, innerRadius, outerRadius, halfAngleDeg, 0f, true);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
        }

        static void BuildCurvedFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            float radius,
            float halfAngleDeg,
            float height,
            int segments,
            bool facingOutward)
        {
            int start = vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = Mathf.Lerp(-halfAngleDeg, halfAngleDeg, t) * Mathf.Deg2Rad;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                Vector3 radial = new(sin, 0f, cos);
                Vector3 normal = facingOutward ? radial : -radial;
                Vector3 bottom = radial * radius;
                Vector3 top = bottom + Vector3.up * height;

                vertices.Add(bottom);
                vertices.Add(top);
                normals.Add(normal);
                normals.Add(normal);
                uvs.Add(new Vector2(t, 0f));
                uvs.Add(new Vector2(t, 1f));
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = start + i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;

                if (facingOutward)
                {
                    triangles.Add(i0); triangles.Add(i1); triangles.Add(i2);
                    triangles.Add(i2); triangles.Add(i1); triangles.Add(i3);
                }
                else
                {
                    triangles.Add(i0); triangles.Add(i2); triangles.Add(i1);
                    triangles.Add(i2); triangles.Add(i3); triangles.Add(i1);
                }
            }
        }

        static void BuildRadialCap(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            float innerRadius,
            float outerRadius,
            float angleDeg,
            float height,
            bool facingPositive)
        {
            int start = vertices.Count;
            float angle = angleDeg * Mathf.Deg2Rad;
            float sin = Mathf.Sin(angle);
            float cos = Mathf.Cos(angle);
            Vector3 radial = new(sin, 0f, cos);
            Vector3 tangent = new(cos, 0f, -sin);
            Vector3 normal = facingPositive ? tangent : -tangent;

            Vector3 innerBottom = radial * innerRadius;
            Vector3 innerTop = innerBottom + Vector3.up * height;
            Vector3 outerBottom = radial * outerRadius;
            Vector3 outerTop = outerBottom + Vector3.up * height;

            vertices.Add(innerBottom);
            vertices.Add(innerTop);
            vertices.Add(outerBottom);
            vertices.Add(outerTop);

            for (int i = 0; i < 4; i++)
                normals.Add(normal);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));

            if (facingPositive)
            {
                triangles.Add(start + 0); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
            }
            else
            {
                triangles.Add(start + 0); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start + 2); triangles.Add(start + 3); triangles.Add(start + 1);
            }
        }

        static void BuildHorizontalCap(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> triangles,
            float innerRadius,
            float outerRadius,
            float halfAngleDeg,
            float y,
            bool facingDown)
        {
            int start = vertices.Count;
            Vector3 normal = facingDown ? Vector3.down : Vector3.up;
            Vector3 innerLeft = DirectionFromAngle(-halfAngleDeg) * innerRadius + Vector3.up * y;
            Vector3 innerRight = DirectionFromAngle(halfAngleDeg) * innerRadius + Vector3.up * y;
            Vector3 outerLeft = DirectionFromAngle(-halfAngleDeg) * outerRadius + Vector3.up * y;
            Vector3 outerRight = DirectionFromAngle(halfAngleDeg) * outerRadius + Vector3.up * y;

            vertices.Add(innerLeft);
            vertices.Add(innerRight);
            vertices.Add(outerLeft);
            vertices.Add(outerRight);

            for (int i = 0; i < 4; i++)
                normals.Add(normal);

            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));

            if (facingDown)
            {
                triangles.Add(start + 0); triangles.Add(start + 2); triangles.Add(start + 1);
                triangles.Add(start + 2); triangles.Add(start + 3); triangles.Add(start + 1);
            }
            else
            {
                triangles.Add(start + 0); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start + 3);
            }
        }

        static Vector3 DirectionFromAngle(float angleDeg)
        {
            float angle = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }

        static Mesh GetPrimitiveMesh(PrimitiveType primitiveType)
        {
            switch (primitiveType)
            {
                case PrimitiveType.Cube:
                    if (s_CubeMesh == null)
                        s_CubeMesh = ExtractPrimitiveMesh(primitiveType);
                    return s_CubeMesh;
                case PrimitiveType.Quad:
                    if (s_QuadMesh == null)
                        s_QuadMesh = ExtractPrimitiveMesh(primitiveType);
                    return s_QuadMesh;
                default:
                    return ExtractPrimitiveMesh(primitiveType);
            }
        }

        static Mesh ExtractPrimitiveMesh(PrimitiveType primitiveType)
        {
            var primitive = GameObject.CreatePrimitive(primitiveType);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;

            if (Application.isPlaying)
                Destroy(primitive);
            else
                DestroyImmediate(primitive);

            return mesh;
        }
    }
}
