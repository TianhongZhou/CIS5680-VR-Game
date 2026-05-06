using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CIS5680VRGame.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class TutorialWaypointMarkerVisual : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int FlowStrengthId = Shader.PropertyToID("_FlowStrength");

        const string VisualRootName = "TutorialWaypoint_EnergyVisual";

        [Header("Energy Body")]
        [SerializeField] Color m_ShellColor = new(0.04f, 0.48f, 0.78f, 1f);
        [SerializeField] Color m_CoreColor = new(0.62f, 0.96f, 1f, 1f);
        [SerializeField, Min(0f)] float m_BodyCenterDrop = 0.64f;
        [SerializeField, Min(0.05f)] float m_ShellRadius = 0.19f;
        [SerializeField, Min(0.02f)] float m_CoreRadius = 0.055f;
        [SerializeField, Min(0.05f)] float m_RingRadius = 0.28f;
        [SerializeField, Min(0.005f)] float m_RingWidth = 0.012f;
        [SerializeField, Min(0.05f)] float m_FloorRippleRadius = 0.52f;
        [SerializeField, Min(0f)] float m_FloorAnchorDrop = 1.08f;

        [Header("Motion")]
        [SerializeField, Min(0.01f)] float m_AppearDuration = 0.45f;
        [SerializeField, Min(0.01f)] float m_DisappearDuration = 0.32f;
        [SerializeField, Min(0.1f)] float m_BreathPeriod = 2.2f;
        [SerializeField, Min(0f)] float m_BreathScale = 0.045f;
        [SerializeField, Min(0f)] float m_RingRotationSpeed = 32f;

        [Header("Light")]
        [SerializeField] bool m_UsePointLight = true;
        [SerializeField, Min(0f)] float m_LightIntensity = 0.82f;
        [SerializeField, Min(0f)] float m_LightRange = 2f;

        [Header("Particles")]
        [SerializeField] bool m_UseEnergyMotes = true;
        [SerializeField, Min(0f)] float m_MoteRate = 6f;

        Transform m_VisualRoot;
        Transform m_ShellTransform;
        Transform m_CoreTransform;
        Transform m_UpperRingTransform;
        Transform m_MidRingTransform;
        Transform m_LowerRingTransform;
        Transform m_FloorRippleTransform;
        Renderer m_ShellRenderer;
        Renderer m_CoreRenderer;
        Renderer[] m_RingRenderers;
        Renderer m_FloorRippleRenderer;
        ParticleSystem m_Motes;
        Light m_EnergyLight;
        Collider[] m_ControlledColliders;
        Material m_ShellMaterial;
        Material m_CoreMaterial;
        Material m_RingMaterial;
        Material m_FloorRippleMaterial;
        bool m_TargetVisible = true;
        bool m_HasReceivedVisibilityCommand;
        float m_Visibility = 1f;
        float m_LocalTime;

        public void SetVisible(bool visible)
        {
            SetVisible(visible, false);
        }

        public void SetVisible(bool visible, bool immediate)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            EnsureVisuals();
            m_HasReceivedVisibilityCommand = true;
            m_TargetVisible = visible;
            SetCollidersEnabled(visible);

            if (immediate)
                m_Visibility = visible ? 1f : 0f;

            ApplyVisualState();
        }

        void Reset()
        {
            if (!Application.isPlaying)
                return;

            EnsureVisuals();
            SetVisible(true, true);
        }

        void Awake()
        {
            if (!Application.isPlaying)
                return;

            EnsureVisuals();
        }

        void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            EnsureVisuals();
            if (!m_HasReceivedVisibilityCommand)
                SetCollidersEnabled(true);
            ApplyVisualState();
        }

        void OnValidate()
        {
        }

        void OnDestroy()
        {
            DestroyImmediateOrRuntime(m_ShellMaterial);
            DestroyImmediateOrRuntime(m_CoreMaterial);
            DestroyImmediateOrRuntime(m_RingMaterial);
            DestroyImmediateOrRuntime(m_FloorRippleMaterial);
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            EnsureVisuals();
            UpdateVisibility(Time.deltaTime);
            ApplyVisualState();
        }

        void UpdateVisibility(float deltaTime)
        {
            if (!Application.isPlaying)
                deltaTime = 1f / 60f;

            m_LocalTime += Mathf.Max(0f, deltaTime);
            float target = m_TargetVisible ? 1f : 0f;
            if (Mathf.Approximately(m_Visibility, target))
                return;

            float duration = m_TargetVisible ? m_AppearDuration : m_DisappearDuration;
            float step = duration <= 0.01f ? 1f : deltaTime / duration;
            m_Visibility = Mathf.MoveTowards(m_Visibility, target, step);
        }

        void EnsureVisuals()
        {
            DisableLegacyVisuals();
            ResolveLight();
            ResolveColliders();
            EnsureMaterials();
            EnsureVisualRoot();
            EnsureEnergyBody();
            EnsureRings();
            EnsureMotes();
        }

        void DisableLegacyVisuals()
        {
            Renderer[] legacyRenderers = GetComponents<Renderer>();
            for (int i = 0; i < legacyRenderers.Length; i++)
            {
                if (legacyRenderers[i] != null)
                    legacyRenderers[i].enabled = false;
            }

            PulseRevealVisual legacyPulseVisual = GetComponent<PulseRevealVisual>();
            if (legacyPulseVisual != null)
                legacyPulseVisual.enabled = false;
        }

        void ResolveLight()
        {
            if (m_EnergyLight == null)
                m_EnergyLight = GetComponent<Light>();

            if (m_EnergyLight == null && m_UsePointLight)
                m_EnergyLight = gameObject.AddComponent<Light>();

            if (m_EnergyLight == null)
                return;

            m_EnergyLight.type = LightType.Point;
            m_EnergyLight.color = m_CoreColor;
            m_EnergyLight.range = m_LightRange;
            m_EnergyLight.shadows = LightShadows.None;
        }

        void ResolveColliders()
        {
            m_ControlledColliders = GetComponents<Collider>();
            for (int i = 0; i < m_ControlledColliders.Length; i++)
            {
                if (m_ControlledColliders[i] != null)
                    m_ControlledColliders[i].isTrigger = true;
            }
        }

        void EnsureMaterials()
        {
            Shader shader = Shader.Find("CIS5680VRGame/TutorialWaypointEnergy");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                return;

            if (m_ShellMaterial == null)
                m_ShellMaterial = CreateRuntimeMaterial(shader, "TutorialWaypointShell");
            if (m_CoreMaterial == null)
                m_CoreMaterial = CreateRuntimeMaterial(shader, "TutorialWaypointCore");
            if (m_RingMaterial == null)
                m_RingMaterial = CreateRuntimeMaterial(shader, "TutorialWaypointOrbit");
            if (m_FloorRippleMaterial == null)
                m_FloorRippleMaterial = CreateRuntimeMaterial(shader, "TutorialWaypointFloorRipple");
        }

        Material CreateRuntimeMaterial(Shader shader, string materialName)
        {
            Material material = new(shader)
            {
                name = $"{materialName}_{name}",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return material;
        }

        void EnsureVisualRoot()
        {
            if (m_VisualRoot != null)
                return;

            Transform existing = transform.Find(VisualRootName);
            if (existing != null)
            {
                m_VisualRoot = existing;
                MarkGeneratedHierarchyUnsaved(m_VisualRoot);
                return;
            }

            GameObject visualRootObject = new(VisualRootName)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            m_VisualRoot = visualRootObject.transform;
            m_VisualRoot.SetParent(transform, false);
            m_VisualRoot.localPosition = Vector3.zero;
            m_VisualRoot.localRotation = Quaternion.identity;
        }

        void EnsureEnergyBody()
        {
            if (m_ShellTransform == null)
            {
                GameObject shell = CreatePrimitiveChild("EnergyShell", PrimitiveType.Sphere, m_ShellMaterial);
                m_ShellTransform = shell.transform;
                m_ShellRenderer = shell.GetComponent<Renderer>();
            }

            if (m_CoreTransform == null)
            {
                GameObject core = CreatePrimitiveChild("EnergyCore", PrimitiveType.Sphere, m_CoreMaterial);
                m_CoreTransform = core.transform;
                m_CoreRenderer = core.GetComponent<Renderer>();
            }
        }

        GameObject CreatePrimitiveChild(string childName, PrimitiveType primitiveType, Material material)
        {
            Transform existing = m_VisualRoot.Find(childName);
            if (existing != null)
            {
                existing.gameObject.hideFlags = HideFlags.HideAndDontSave;
                return existing.gameObject;
            }

            GameObject child = GameObject.CreatePrimitive(primitiveType);
            child.name = childName;
            child.hideFlags = HideFlags.HideAndDontSave;
            child.transform.SetParent(m_VisualRoot, false);

            Collider childCollider = child.GetComponent<Collider>();
            if (childCollider != null)
                DestroyImmediateOrRuntime(childCollider);

            Renderer childRenderer = child.GetComponent<Renderer>();
            if (childRenderer != null)
            {
                childRenderer.sharedMaterial = material;
                childRenderer.shadowCastingMode = ShadowCastingMode.Off;
                childRenderer.receiveShadows = false;
                childRenderer.lightProbeUsage = LightProbeUsage.Off;
                childRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }

            return child;
        }

        void EnsureRings()
        {
            if (m_RingRenderers == null || m_RingRenderers.Length != 3)
                m_RingRenderers = new Renderer[3];

            EnsureRing(ref m_UpperRingTransform, 0, "UpperEnergyOrbit", 0.17f, new Vector3(13f, 0f, 8f));
            EnsureRing(ref m_MidRingTransform, 1, "MiddleEnergyOrbit", 0f, new Vector3(0f, 0f, -7f));
            EnsureRing(ref m_LowerRingTransform, 2, "LowerEnergyOrbit", -0.19f, new Vector3(-11f, 0f, 16f));
            EnsureFloorRipple();
        }

        void EnsureRing(ref Transform ringTransform, int rendererIndex, string ringName, float yOffset, Vector3 eulerAngles)
        {
            if (ringTransform == null)
            {
                Transform existing = m_VisualRoot.Find(ringName);
                if (existing != null)
                    ringTransform = existing;
                else
                    ringTransform = CreateRingObject(ringName, m_RingRadius, m_RingWidth, 4, 0.68f, m_RingMaterial).transform;
            }

            ringTransform.localPosition = Vector3.down * m_BodyCenterDrop + Vector3.up * yOffset;
            ringTransform.localRotation = Quaternion.Euler(eulerAngles);

            if (m_RingRenderers[rendererIndex] == null)
                m_RingRenderers[rendererIndex] = ringTransform.GetComponent<Renderer>();
        }

        void EnsureFloorRipple()
        {
            if (m_FloorRippleTransform == null)
            {
                Transform existing = m_VisualRoot.Find("FloorSonarRipple");
                if (existing != null)
                    m_FloorRippleTransform = existing;
                else
                    m_FloorRippleTransform = CreateRingObject("FloorSonarRipple", m_FloorRippleRadius, m_RingWidth * 1.25f, 6, 0.52f, m_FloorRippleMaterial).transform;
            }

            m_FloorRippleTransform.localPosition = Vector3.down * m_FloorAnchorDrop;
            m_FloorRippleTransform.localRotation = Quaternion.identity;
            if (m_FloorRippleRenderer == null)
                m_FloorRippleRenderer = m_FloorRippleTransform.GetComponent<Renderer>();
        }

        GameObject CreateRingObject(string ringName, float radius, float width, int arcCount, float arcFill, Material material)
        {
            GameObject ringObject = new(ringName)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            ringObject.transform.SetParent(m_VisualRoot, false);

            MeshFilter meshFilter = ringObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = ringObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = BuildSegmentedRingMesh(ringName, radius, width, arcCount, arcFill, 14);
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return ringObject;
        }

        static Mesh BuildSegmentedRingMesh(string meshName, float radius, float width, int arcCount, float arcFill, int subdivisions)
        {
            List<Vector3> vertices = new();
            List<int> triangles = new();
            List<Color> colors = new();
            List<Vector2> uvs = new();

            int safeArcCount = Mathf.Max(1, arcCount);
            int safeSubdivisions = Mathf.Max(2, subdivisions);
            float safeFill = Mathf.Clamp01(arcFill);
            float innerRadius = Mathf.Max(0.001f, radius - width * 0.5f);
            float outerRadius = Mathf.Max(innerRadius + 0.001f, radius + width * 0.5f);
            float arcStride = Mathf.PI * 2f / safeArcCount;
            float arcLength = arcStride * safeFill;

            for (int arcIndex = 0; arcIndex < safeArcCount; arcIndex++)
            {
                float arcStart = arcIndex * arcStride + arcStride * (1f - safeFill) * 0.5f;
                int vertexStart = vertices.Count;

                for (int i = 0; i <= safeSubdivisions; i++)
                {
                    float t = i / (float)safeSubdivisions;
                    float angle = arcStart + arcLength * t;
                    float alpha = Mathf.Sin(t * Mathf.PI);
                    float sin = Mathf.Sin(angle);
                    float cos = Mathf.Cos(angle);

                    vertices.Add(new Vector3(cos * innerRadius, 0f, sin * innerRadius));
                    vertices.Add(new Vector3(cos * outerRadius, 0f, sin * outerRadius));
                    colors.Add(new Color(1f, 1f, 1f, alpha));
                    colors.Add(new Color(1f, 1f, 1f, alpha));
                    uvs.Add(new Vector2(t, 0f));
                    uvs.Add(new Vector2(t, 1f));
                }

                for (int i = 0; i < safeSubdivisions; i++)
                {
                    int index = vertexStart + i * 2;
                    triangles.Add(index);
                    triangles.Add(index + 1);
                    triangles.Add(index + 2);
                    triangles.Add(index + 2);
                    triangles.Add(index + 1);
                    triangles.Add(index + 3);
                }
            }

            Mesh mesh = new()
            {
                name = meshName,
                hideFlags = HideFlags.HideAndDontSave,
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            return mesh;
        }

        void EnsureMotes()
        {
            if (!m_UseEnergyMotes)
                return;

            if (m_Motes != null)
                return;

            Transform existing = m_VisualRoot.Find("EnergyMotes");
            GameObject motesObject;
            if (existing != null)
            {
                motesObject = existing.gameObject;
                motesObject.hideFlags = HideFlags.HideAndDontSave;
                m_Motes = motesObject.GetComponent<ParticleSystem>();
            }
            else
            {
                motesObject = new("EnergyMotes")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                motesObject.transform.SetParent(m_VisualRoot, false);
                m_Motes = motesObject.AddComponent<ParticleSystem>();
            }

            motesObject.transform.localPosition = Vector3.down * m_BodyCenterDrop;
            ConfigureMotes();
        }

        void ConfigureMotes()
        {
            if (m_Motes == null)
                return;

            ParticleSystem.MainModule main = m_Motes.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.07f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.009f, 0.024f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.32f, 0.88f, 1f, 0.28f), new Color(0.78f, 0.98f, 1f, 0.72f));
            main.maxParticles = 42;

            ParticleSystem.EmissionModule emission = m_Motes.emission;
            emission.enabled = true;

            ParticleSystem.ShapeModule shape = m_Motes.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = m_ShellRadius * 0.95f;

            ParticleSystem.VelocityOverLifetimeModule velocity = m_Motes.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.015f, 0.09f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystemRenderer motesRenderer = m_Motes.GetComponent<ParticleSystemRenderer>();
            if (motesRenderer != null)
            {
                motesRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                motesRenderer.alignment = ParticleSystemRenderSpace.View;
                motesRenderer.sharedMaterial = m_CoreMaterial;
                motesRenderer.shadowCastingMode = ShadowCastingMode.Off;
                motesRenderer.receiveShadows = false;
                motesRenderer.lightProbeUsage = LightProbeUsage.Off;
                motesRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        void ApplyVisualState()
        {
            if (m_VisualRoot == null)
                return;

            float eased = Smooth01(m_Visibility);
            bool visible = eased > 0.005f;
            m_VisualRoot.gameObject.SetActive(visible);

            ApplyScaleCompensation();
            AnimateTransforms(eased);
            ApplyMaterials(eased);
            ApplyRendererVisibility(visible);
            ApplyLight(eased);
            ApplyMotes(eased, visible);
        }

        void ApplyScaleCompensation()
        {
            if (m_VisualRoot == null)
                return;

            Vector3 lossyScale = transform.lossyScale;
            m_VisualRoot.localScale = new Vector3(
                SafeInverse(lossyScale.x),
                SafeInverse(lossyScale.y),
                SafeInverse(lossyScale.z));
            m_VisualRoot.localPosition = Vector3.zero;
        }

        void AnimateTransforms(float eased)
        {
            float breath = 1f + Mathf.Sin(m_LocalTime * Mathf.PI * 2f / Mathf.Max(0.01f, m_BreathPeriod)) * m_BreathScale * eased;
            float appearScale = Mathf.Lerp(0.18f, 1f, eased) * breath;

            if (m_ShellTransform != null)
            {
                m_ShellTransform.localPosition = Vector3.down * m_BodyCenterDrop;
                m_ShellTransform.localScale = Vector3.one * (m_ShellRadius * 2f * appearScale);
            }

            if (m_CoreTransform != null)
            {
                m_CoreTransform.localPosition = Vector3.down * m_BodyCenterDrop;
                m_CoreTransform.localScale = Vector3.one * (m_CoreRadius * 2f * Mathf.Lerp(0.45f, 1.08f, eased) * breath);
            }

            float rotation = m_LocalTime * m_RingRotationSpeed;
            if (m_UpperRingTransform != null)
            {
                m_UpperRingTransform.localPosition = Vector3.down * m_BodyCenterDrop + Vector3.up * 0.17f;
                m_UpperRingTransform.localEulerAngles = new Vector3(13f, rotation, 8f);
            }
            if (m_MidRingTransform != null)
            {
                m_MidRingTransform.localPosition = Vector3.down * m_BodyCenterDrop;
                m_MidRingTransform.localEulerAngles = new Vector3(0f, -rotation * 0.72f, -7f);
            }
            if (m_LowerRingTransform != null)
            {
                m_LowerRingTransform.localPosition = Vector3.down * m_BodyCenterDrop + Vector3.down * 0.19f;
                m_LowerRingTransform.localEulerAngles = new Vector3(-11f, rotation * 1.18f, 16f);
            }

            if (m_FloorRippleTransform != null)
            {
                float rippleScale = Mathf.Lerp(0.35f, 1f, eased) * (1f + Mathf.Sin(m_LocalTime * 1.7f) * 0.025f * eased);
                m_FloorRippleTransform.localScale = Vector3.one * rippleScale;
            }
        }

        void ApplyMaterials(float eased)
        {
            ApplyEnergyMaterial(m_ShellMaterial, m_ShellColor, m_CoreColor, 0.44f * eased, 0.38f);
            ApplyEnergyMaterial(m_CoreMaterial, m_CoreColor, Color.white, 0.85f * eased, 0.12f);
            ApplyEnergyMaterial(m_RingMaterial, m_ShellColor, m_CoreColor, 0.56f * eased, 0.2f);
            ApplyEnergyMaterial(m_FloorRippleMaterial, new Color(0.02f, 0.26f, 0.34f, 1f), m_ShellColor, 0.35f * eased, 0.18f);
        }

        static void ApplyEnergyMaterial(Material material, Color baseColor, Color emissionColor, float alpha, float flowStrength)
        {
            if (material == null)
                return;

            if (material.HasProperty(BaseColorId))
                material.SetColor(BaseColorId, baseColor);
            if (material.HasProperty(EmissionColorId))
                material.SetColor(EmissionColorId, emissionColor);
            if (material.HasProperty(AlphaId))
                material.SetFloat(AlphaId, Mathf.Clamp01(alpha));
            if (material.HasProperty(FlowStrengthId))
                material.SetFloat(FlowStrengthId, Mathf.Clamp01(flowStrength));
        }

        void ApplyRendererVisibility(bool visible)
        {
            SetRendererVisible(m_ShellRenderer, visible);
            SetRendererVisible(m_CoreRenderer, visible);
            SetRendererVisible(m_FloorRippleRenderer, visible);

            if (m_RingRenderers == null)
                return;

            for (int i = 0; i < m_RingRenderers.Length; i++)
                SetRendererVisible(m_RingRenderers[i], visible);
        }

        static void SetRendererVisible(Renderer renderer, bool visible)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }

        void ApplyLight(float eased)
        {
            if (m_EnergyLight == null)
                return;

            m_EnergyLight.enabled = m_UsePointLight && eased > 0.01f;
            m_EnergyLight.color = m_CoreColor;
            m_EnergyLight.range = m_LightRange;
            m_EnergyLight.intensity = m_LightIntensity * eased * (0.82f + Mathf.Sin(m_LocalTime * 2.4f) * 0.08f);
        }

        void ApplyMotes(float eased, bool visible)
        {
            if (m_Motes == null)
                return;

            ParticleSystem.EmissionModule emission = m_Motes.emission;
            emission.rateOverTime = m_MoteRate * eased;

            if (visible && !m_Motes.isPlaying)
                m_Motes.Play();
            else if (!visible && m_Motes.isPlaying)
                m_Motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void SetCollidersEnabled(bool enabled)
        {
            if (m_ControlledColliders == null)
                ResolveColliders();

            if (m_ControlledColliders == null)
                return;

            for (int i = 0; i < m_ControlledColliders.Length; i++)
            {
                if (m_ControlledColliders[i] != null)
                    m_ControlledColliders[i].enabled = enabled;
            }
        }

        static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        static float SafeInverse(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
        }

        static void MarkGeneratedHierarchyUnsaved(Transform root)
        {
            if (root == null)
                return;

            Transform[] generatedTransforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < generatedTransforms.Length; i++)
            {
                Transform generatedTransform = generatedTransforms[i];
                if (generatedTransform == null)
                    continue;

                generatedTransform.gameObject.hideFlags = HideFlags.HideAndDontSave;

                MeshFilter meshFilter = generatedTransform.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                    meshFilter.sharedMesh.hideFlags = HideFlags.HideAndDontSave;
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
