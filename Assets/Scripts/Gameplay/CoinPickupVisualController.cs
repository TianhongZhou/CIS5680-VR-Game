using System.Collections.Generic;
using CIS5680VRGame.Balls;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class CoinPickupVisualController : MonoBehaviour
    {
        static readonly int s_EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] Renderer[] m_TargetRenderers;
        [SerializeField] Color m_BodyEmissionColor = new(1f, 0.52f, 0.08f, 1f);
        [SerializeField] Color m_LampEmissionColor = new(1f, 0.36f, 0.03f, 1f);
        [SerializeField, Min(0f)] float m_BodyEmissionStrength = 0.22f;
        [SerializeField, Min(0f)] float m_LampEmissionMinStrength = 1.2f;
        [SerializeField, Min(0f)] float m_LampEmissionMaxStrength = 2f;
        [SerializeField, Min(0f)] float m_BreathSpeed = 1.25f;
        [SerializeField, Min(0f)] float m_PulseFlashDuration = 0.55f;
        [SerializeField, Min(0f)] float m_PulseFlashStrength = 3.2f;
        [SerializeField, Min(0f)] float m_PulseReachMargin = 0.75f;

        readonly List<MaterialSlot> m_MaterialSlots = new();

        float m_PhaseOffset;
        float m_PulseFlashUntil;

        void Awake()
        {
            m_PhaseOffset = Random.value * Mathf.PI * 2f;
            PrepareRuntimeMaterials();
            ApplyVisualState();
        }

        void OnEnable()
        {
            SonarPulseImpactEffect.PulseSpawned += OnPulseSpawned;
            StickyPulseImpactEffect.PulseSpawned += OnPulseSpawned;
            ApplyVisualState();
        }

        void OnDisable()
        {
            SonarPulseImpactEffect.PulseSpawned -= OnPulseSpawned;
            StickyPulseImpactEffect.PulseSpawned -= OnPulseSpawned;
        }

        void OnDestroy()
        {
            if (!Application.isPlaying)
                return;

            for (int i = 0; i < m_MaterialSlots.Count; i++)
            {
                Material material = m_MaterialSlots[i].Material;
                if (material != null)
                    Destroy(material);
            }
        }

        void Update()
        {
            ApplyVisualState();
        }

        public void RefreshTargets()
        {
            m_TargetRenderers = null;
            PrepareRuntimeMaterials();
            ApplyVisualState();
        }

        void PrepareRuntimeMaterials()
        {
            ResolveRenderers();
            m_MaterialSlots.Clear();

            for (int rendererIndex = 0; rendererIndex < m_TargetRenderers.Length; rendererIndex++)
            {
                Renderer targetRenderer = m_TargetRenderers[rendererIndex];
                if (targetRenderer == null)
                    continue;

                Material[] runtimeMaterials = targetRenderer.materials;
                for (int materialIndex = 0; materialIndex < runtimeMaterials.Length; materialIndex++)
                {
                    Material material = runtimeMaterials[materialIndex];
                    if (material == null || !material.HasProperty(s_EmissionColorId))
                        continue;

                    material.EnableKeyword("_EMISSION");
                    m_MaterialSlots.Add(new MaterialSlot(
                        material,
                        IsLampMaterial(targetRenderer, material)));
                }
            }
        }

        void ResolveRenderers()
        {
            if (m_TargetRenderers == null || m_TargetRenderers.Length == 0)
                m_TargetRenderers = GetComponentsInChildren<Renderer>(true);
        }

        void OnPulseSpawned(Vector3 origin, float radius, Collider sourceCollider)
        {
            float reach = Mathf.Max(0f, radius) + Mathf.Max(0f, m_PulseReachMargin);
            if ((transform.position - origin).sqrMagnitude > reach * reach)
                return;

            m_PulseFlashUntil = Mathf.Max(m_PulseFlashUntil, Time.time + m_PulseFlashDuration);
            ApplyVisualState();
        }

        void ApplyVisualState()
        {
            if (m_MaterialSlots.Count == 0)
                return;

            float breath = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f * m_BreathSpeed + m_PhaseOffset);
            float flash = ResolveFlash01();
            float lampStrength = Mathf.Lerp(m_LampEmissionMinStrength, m_LampEmissionMaxStrength, breath)
                + flash * m_PulseFlashStrength;
            float bodyStrength = m_BodyEmissionStrength + flash * (m_PulseFlashStrength * 0.3f);

            for (int i = 0; i < m_MaterialSlots.Count; i++)
            {
                MaterialSlot slot = m_MaterialSlots[i];
                Color emissionColor = slot.IsLamp
                    ? m_LampEmissionColor * lampStrength
                    : m_BodyEmissionColor * bodyStrength;
                emissionColor.a = 1f;
                slot.Material.SetColor(s_EmissionColorId, emissionColor);
            }
        }

        float ResolveFlash01()
        {
            if (m_PulseFlashDuration <= 0f || Time.time >= m_PulseFlashUntil)
                return 0f;

            float remaining = Mathf.Clamp01((m_PulseFlashUntil - Time.time) / m_PulseFlashDuration);
            return remaining * remaining;
        }

        static bool IsLampMaterial(Renderer targetRenderer, Material material)
        {
            string rendererName = targetRenderer != null ? targetRenderer.name.ToLowerInvariant() : string.Empty;
            string materialName = material != null ? material.name.ToLowerInvariant() : string.Empty;
            return rendererName.Contains("lamp")
                || rendererName.Contains("amber")
                || rendererName.Contains("tube")
                || materialName.Contains("lamp")
                || materialName.Contains("amber")
                || materialName.Contains("emission")
                || materialName.Contains("emissive");
        }

        readonly struct MaterialSlot
        {
            public MaterialSlot(Material material, bool isLamp)
            {
                Material = material;
                IsLamp = isLamp;
            }

            public Material Material { get; }
            public bool IsLamp { get; }
        }
    }
}
