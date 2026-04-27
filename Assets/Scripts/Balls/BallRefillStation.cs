using System.Collections.Generic;
using CIS5680VRGame.Gameplay;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class BallRefillStation : MonoBehaviour
    {
        static class SharedDefaults
        {
            public const int Charges = 2;
            public const float Cooldown = 20f;
            public const float BaseRefillAmount = 50f;
        }

        const string k_InteractionOnlyLayerName = "InteractionOnly";
        const int k_UnlimitedChargeCount = 999999;

        static readonly int k_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int k_ColorId = Shader.PropertyToID("_Color");
        static readonly int k_EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int k_RimColorId = Shader.PropertyToID("_RimColor");
        static bool s_InteractionOnlyLayerConfigured;

        [SerializeField] BallHolsterSlot[] m_TargetSlots;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] PlayerEnergy m_PlayerEnergy;
        [SerializeField] XRSimpleInteractable m_Interactable;
        [SerializeField] bool m_OverrideSharedDefaults;
        [SerializeField] bool m_IgnoreProgressionModifiers;
        [SerializeField] bool m_UnlimitedCharges;
        [SerializeField, Min(1)] int m_Charges = SharedDefaults.Charges;
        [SerializeField] float m_Cooldown = SharedDefaults.Cooldown;
        [SerializeField] float m_BaseRefillAmount = SharedDefaults.BaseRefillAmount;
        [SerializeField] Renderer[] m_VisitedStateRenderers;
        [SerializeField] Renderer[] m_StateLampRenderers;
        [SerializeField] Light[] m_StateLights;
        [SerializeField] Transform m_CoreVisualRoot;
        [SerializeField] Color m_ReadyLampColor = new(1f, 0.48f, 0.08f, 1f);
        [SerializeField] Color m_RechargingColorA = new(0.18f, 0.07f, 0.02f, 1f);
        [SerializeField] Color m_RechargingColorB = new(1f, 0.56f, 0.12f, 1f);
        [SerializeField] Color m_RechargingRimColor = new(1f, 0.88f, 0.5f, 0.34f);
        [SerializeField] Color m_DepletedLampColor = new(0.95f, 0.12f, 0.08f, 1f);
        [SerializeField] float m_ReadyEmissionStrength = 4.4f;
        [SerializeField] float m_RechargingEmissionStrength = 3.2f;
        [SerializeField] float m_DepletedEmissionStrength = 0.35f;
        [SerializeField] float m_ReadyLightIntensity = 1.65f;
        [SerializeField] float m_RechargingLightIntensity = 1.25f;
        [SerializeField] float m_DepletedLightIntensity = 0.16f;
        [SerializeField] float m_ReadyPulseAmplitude = 0.16f;
        [SerializeField] float m_RechargingFlashFrequency = 3.2f;
        [SerializeField] Color m_VisitedColor = new(0.18f, 0.84f, 0.96f, 1f);
        [SerializeField] Color m_VisitedRimColor = new(0.9f, 1f, 1f, 0.24f);
        [SerializeField] Vector3 m_HoverPromptLocalOffset = new(0f, 0.68f, 0f);
        [SerializeField] Vector2 m_HoverPromptPanelSize = new(700f, 170f);
        [SerializeField] float m_HoverPromptCanvasScale = 0.00175f;
        [SerializeField] float m_HoverPromptFontSize = 52f;
        [SerializeField] Color m_HoverPromptReadyColor = new(1f, 0.82f, 0.48f, 1f);
        [SerializeField] Color m_HoverPromptCooldownColor = new(1f, 0.72f, 0.28f, 1f);
        [SerializeField] Color m_HoverPromptDepletedColor = new(1f, 0.38f, 0.34f, 1f);
        [SerializeField] Color m_HoverPromptOutlineColor = new(0f, 0f, 0f, 0.88f);
        [SerializeField] Color m_HoverPromptPanelColor = new(0.03f, 0.04f, 0.04f, 0.88f);
        [SerializeField] Color m_HoverPromptPanelBorderColor = new(1f, 0.62f, 0.18f, 0.92f);
        [SerializeField] string m_ReadyPromptText = "Energy ready: step onto pad";
        [SerializeField] string m_CooldownPromptFormat = "Energy recharging... {0:0.0}s";
        [SerializeField] string m_DepletedPromptText = "Energy pad depleted";

        Collider m_Trigger;
        MaterialPropertyBlock m_PropertyBlock;
        float m_CooldownEndsAt = -999f;
        int m_ChargesRemaining;
        int m_PersistentRefillBonusPercent;
        int m_PersistentChargeBonus;
        int m_PersistentCooldownReductionPercent;
        int m_SingleRunChargePenalty;
        int m_SingleRunRefillMultiplierPercent = 100;
        readonly HashSet<IXRHoverInteractor> m_HoveringInteractors = new();
        GameObject m_HoverPromptObject;
        RectTransform m_HoverPromptRect;
        TextMeshProUGUI m_HoverPromptText;
        Transform m_PromptFaceTarget;
        Vector3 m_CoreBaseLocalScale = Vector3.one;

        public int MaxCharges
        {
            get
            {
                if (m_UnlimitedCharges)
                    return k_UnlimitedChargeCount;

                int chargeBonus = m_IgnoreProgressionModifiers ? 0 : Mathf.Max(0, m_PersistentChargeBonus);
                int chargePenalty = m_IgnoreProgressionModifiers ? 0 : Mathf.Max(0, m_SingleRunChargePenalty);
                return Mathf.Max(1, m_Charges + chargeBonus - chargePenalty);
            }
        }

        public int ChargesRemaining => m_UnlimitedCharges ? MaxCharges : Mathf.Max(0, m_ChargesRemaining);
        public bool IsDepleted => !m_UnlimitedCharges && ChargesRemaining <= 0;
        public bool IsReady => !IsDepleted && Time.time >= m_CooldownEndsAt;
        public bool IsLocatorAvailable => !IsDepleted;
        public bool HasBeenVisited => !m_UnlimitedCharges && IsDepleted;

        public void SetPersistentRefillBonusPercent(int bonusPercent)
        {
            m_PersistentRefillBonusPercent = Mathf.Max(0, bonusPercent);
        }

        public void SetPersistentChargeBonus(int bonusCharges)
        {
            int previousMaxCharges = MaxCharges;
            m_PersistentChargeBonus = Mathf.Max(0, bonusCharges);
            AdjustRemainingCharges(previousMaxCharges, MaxCharges);
        }

        public void SetSingleRunChargePenalty(int penaltyCharges)
        {
            int previousMaxCharges = MaxCharges;
            m_SingleRunChargePenalty = Mathf.Max(0, penaltyCharges);
            AdjustRemainingCharges(previousMaxCharges, MaxCharges);
        }

        public void SetPersistentCooldownReductionPercent(int reductionPercent)
        {
            m_PersistentCooldownReductionPercent = Mathf.Clamp(reductionPercent, 0, 90);
        }

        public void SetSingleRunRefillMultiplierPercent(int multiplierPercent)
        {
            m_SingleRunRefillMultiplierPercent = Mathf.Clamp(multiplierPercent, 1, 1000);
        }

        void Reset()
        {
            ApplySharedDefaults();
        }

        void OnValidate()
        {
            ApplySharedDefaults();
        }

        void Awake()
        {
            ApplySharedDefaults();
            ConfigureInteractionOnlyLayerCollision();
            ConfigureInteractionOnlyCasterMasks();

            m_Trigger = GetComponent<Collider>();
            m_Trigger.isTrigger = true;
            m_Interactable = GetComponent<XRSimpleInteractable>();

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_PlayerEnergy == null)
                m_PlayerEnergy = FindObjectOfType<PlayerEnergy>();

            ResolveVisualReferences();

            m_ChargesRemaining = MaxCharges;
            m_PropertyBlock = new MaterialPropertyBlock();
            ResolvePromptFaceTarget();
            UpdateVisualState();
        }

        void OnEnable()
        {
            if (m_Interactable == null)
                m_Interactable = GetComponent<XRSimpleInteractable>();

            if (m_Interactable != null)
            {
                m_Interactable.hoverEntered.AddListener(OnHoverEntered);
                m_Interactable.hoverExited.AddListener(OnHoverExited);
            }

            UpdateVisualState();
        }

        void OnDisable()
        {
            if (m_Interactable != null)
            {
                m_Interactable.hoverEntered.RemoveListener(OnHoverEntered);
                m_Interactable.hoverExited.RemoveListener(OnHoverExited);
            }

            m_HoveringInteractors.Clear();
            SetPromptVisible(false);
        }

        void OnDestroy()
        {
            if (m_HoverPromptObject == null)
                return;

            if (Application.isPlaying)
                Destroy(m_HoverPromptObject);
            else
                DestroyImmediate(m_HoverPromptObject);
        }

        void Update()
        {
            UpdateVisualState();
            UpdateHoverPrompt();
        }

        void OnTriggerEnter(Collider other)
        {
            TryRefill(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryRefill(other);
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (!ShouldShowPromptForInteractor(args.interactorObject))
                return;

            m_HoveringInteractors.Add(args.interactorObject);
            UpdateHoverPrompt();
        }

        void OnHoverExited(HoverExitEventArgs args)
        {
            if (args.interactorObject != null)
                m_HoveringInteractors.Remove(args.interactorObject);

            UpdateHoverPrompt();
        }

        bool CanUse(Collider other)
        {
            if (!enabled || !IsReady)
                return false;

            if (other == null)
                return false;

            var rig = other.GetComponentInParent<XROrigin>();
            if (rig == null)
                return false;

            return m_PlayerRig == null || rig == m_PlayerRig;
        }

        void TryRefill(Collider other)
        {
            if (!CanUse(other))
                return;

            bool refilledAny = false;
            float effectiveRefillAmount = GetEffectiveRefillAmount();
            for (int i = 0; i < m_TargetSlots.Length; i++)
            {
                BallHolsterSlot targetSlot = m_TargetSlots[i];
                if (targetSlot == null)
                    continue;

                refilledAny |= targetSlot.RefillAmount(effectiveRefillAmount);
            }

            if (!refilledAny && (m_TargetSlots == null || m_TargetSlots.Length == 0) && m_PlayerEnergy != null)
                refilledAny = m_PlayerEnergy.RestoreAmount(effectiveRefillAmount);

            if (refilledAny)
            {
                m_ChargesRemaining = m_UnlimitedCharges
                    ? MaxCharges
                    : Mathf.Max(0, m_ChargesRemaining - 1);

                if (!IsDepleted)
                    m_CooldownEndsAt = Time.time + GetEffectiveCooldown();
                else
                    m_CooldownEndsAt = -999f;

                PulseAudioService.PlayResourceRestored();
                UpdateVisualState();
            }
        }

        float GetEffectiveRefillAmount()
        {
            if (m_IgnoreProgressionModifiers)
                return Mathf.Max(1f, m_BaseRefillAmount);

            float persistentMultiplier = 1f + Mathf.Max(0, m_PersistentRefillBonusPercent) / 100f;
            float singleRunMultiplier = Mathf.Clamp(m_SingleRunRefillMultiplierPercent, 1, 1000) / 100f;
            float multiplier = persistentMultiplier * singleRunMultiplier;
            return Mathf.Max(1f, m_BaseRefillAmount * multiplier);
        }

        float GetEffectiveCooldown()
        {
            if (m_IgnoreProgressionModifiers)
                return Mathf.Max(0.1f, m_Cooldown);

            float multiplier = 1f - (Mathf.Clamp(m_PersistentCooldownReductionPercent, 0, 90) / 100f);
            return Mathf.Max(0.1f, m_Cooldown * multiplier);
        }

        void UpdateVisualState()
        {
            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            if ((m_StateLampRenderers == null || m_StateLampRenderers.Length == 0)
                && (m_VisitedStateRenderers == null || m_VisitedStateRenderers.Length == 0))
            {
                return;
            }

            if (IsDepleted)
            {
                ApplyVisualState(m_DepletedLampColor, m_VisitedRimColor, m_DepletedEmissionStrength, m_DepletedLightIntensity, 0.94f);
                return;
            }

            if (!IsReady)
            {
                float flashWave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, m_RechargingFlashFrequency) * Mathf.PI * 2f);
                Color flashColor = Color.Lerp(m_RechargingColorA, m_RechargingColorB, flashWave);
                float emission = m_RechargingEmissionStrength * Mathf.Lerp(0.35f, 1.4f, flashWave);
                float lightIntensity = m_RechargingLightIntensity * Mathf.Lerp(0.28f, 1.35f, flashWave);
                ApplyVisualState(flashColor, m_RechargingRimColor, emission, lightIntensity, 1f + flashWave * 0.04f);
                return;
            }

            float readyWave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, m_RechargingFlashFrequency * 0.35f) * Mathf.PI * 2f);
            float readyEmission = m_ReadyEmissionStrength * (1f + readyWave * m_ReadyPulseAmplitude);
            float readyLightIntensity = m_ReadyLightIntensity * (1f + readyWave * m_ReadyPulseAmplitude);
            ApplyVisualState(m_ReadyLampColor, m_VisitedRimColor, readyEmission, readyLightIntensity, 1f + readyWave * 0.025f);
        }

        void ApplyVisualState(Color baseColor, Color rimColor, float emissionStrength, float lightIntensity, float coreScaleMultiplier)
        {
            Renderer[] targetRenderers = m_StateLampRenderers != null && m_StateLampRenderers.Length > 0
                ? m_StateLampRenderers
                : m_VisitedStateRenderers;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer targetRenderer = targetRenderers[i];
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(k_BaseColorId, baseColor);
                m_PropertyBlock.SetColor(k_ColorId, baseColor);
                m_PropertyBlock.SetColor(k_EmissionColorId, baseColor * Mathf.Max(0f, emissionStrength));
                m_PropertyBlock.SetColor(k_RimColorId, rimColor);
                targetRenderer.SetPropertyBlock(m_PropertyBlock);
            }

            if (m_StateLights != null)
            {
                for (int i = 0; i < m_StateLights.Length; i++)
                {
                    Light stateLight = m_StateLights[i];
                    if (stateLight == null)
                        continue;

                    stateLight.color = baseColor;
                    stateLight.intensity = Mathf.Max(0f, lightIntensity);
                }
            }

            if (m_CoreVisualRoot != null)
                m_CoreVisualRoot.localScale = m_CoreBaseLocalScale * Mathf.Max(0.01f, coreScaleMultiplier);
        }

        bool ShouldShowPromptForInteractor(IXRHoverInteractor interactor)
        {
            if (interactor == null || interactor is XRSocketInteractor)
                return false;

            XROrigin rig = interactor.transform.GetComponentInParent<XROrigin>();
            return rig != null && (m_PlayerRig == null || rig == m_PlayerRig);
        }

        void UpdateHoverPrompt()
        {
            if (!Application.isPlaying)
                return;

            if (m_HoveringInteractors.Count == 0)
            {
                SetPromptVisible(false);
                return;
            }

            EnsureHoverPrompt();
            if (m_HoverPromptText == null)
                return;

            ResolvePromptFaceTarget();

            float remaining = Mathf.Max(0f, m_CooldownEndsAt - Time.time);
            float cooldownFlash = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, m_RechargingFlashFrequency) * Mathf.PI * 2f);
            if (IsDepleted)
            {
                m_HoverPromptText.text = m_DepletedPromptText;
                m_HoverPromptText.color = m_HoverPromptDepletedColor;
            }
            else
            {
                m_HoverPromptText.text = IsReady
                    ? m_ReadyPromptText
                    : string.Format(m_CooldownPromptFormat, remaining);
                m_HoverPromptText.color = IsReady
                    ? m_HoverPromptReadyColor
                    : Color.Lerp(m_HoverPromptCooldownColor * 0.72f, m_HoverPromptCooldownColor, cooldownFlash);
            }

            Transform promptTransform = m_HoverPromptRect;
            promptTransform.position = transform.TransformPoint(m_HoverPromptLocalOffset);

            if (m_PromptFaceTarget != null)
            {
                Vector3 toViewer = m_PromptFaceTarget.position - promptTransform.position;
                if (toViewer.sqrMagnitude > 0.0001f)
                    promptTransform.rotation = Quaternion.LookRotation(-toViewer.normalized, Vector3.up);
            }

            SetPromptVisible(true);
        }

        void ResolvePromptFaceTarget()
        {
            if (m_PlayerRig != null && m_PlayerRig.Camera != null)
            {
                m_PromptFaceTarget = m_PlayerRig.Camera.transform;
                return;
            }

            if (Camera.main != null)
                m_PromptFaceTarget = Camera.main.transform;
        }

        void EnsureHoverPrompt()
        {
            if (m_HoverPromptObject != null && m_HoverPromptText != null && m_HoverPromptRect != null)
                return;

            m_HoverPromptObject = new GameObject($"{name}_HoverPrompt");
            m_HoverPromptObject.hideFlags = HideFlags.DontSave;
            Canvas canvas = m_HoverPromptObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 500;
            canvas.overrideSorting = true;

            m_HoverPromptRect = canvas.GetComponent<RectTransform>();
            m_HoverPromptRect.sizeDelta = m_HoverPromptPanelSize;

            CanvasScaler scaler = m_HoverPromptObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 24f;
            scaler.referencePixelsPerUnit = 100f;

            GameObject panelObject = new("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panelObject.transform.SetParent(m_HoverPromptObject.transform, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = m_HoverPromptPanelColor;

            Outline panelOutline = panelObject.GetComponent<Outline>();
            panelOutline.effectColor = m_HoverPromptPanelBorderColor;
            panelOutline.effectDistance = new Vector2(3f, -3f);

            GameObject textObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(28f, 18f);
            textRect.offsetMax = new Vector2(-28f, -18f);

            m_HoverPromptText = textObject.GetComponent<TextMeshProUGUI>();
            m_HoverPromptText.fontSize = Mathf.Max(8f, m_HoverPromptFontSize);
            m_HoverPromptText.enableAutoSizing = true;
            m_HoverPromptText.fontSizeMin = 28f;
            m_HoverPromptText.fontSizeMax = Mathf.Max(8f, m_HoverPromptFontSize);
            m_HoverPromptText.alignment = TextAlignmentOptions.Center;
            m_HoverPromptText.enableWordWrapping = false;
            m_HoverPromptText.color = m_HoverPromptReadyColor;
            m_HoverPromptText.outlineWidth = 0.18f;
            m_HoverPromptText.outlineColor = m_HoverPromptOutlineColor;
            m_HoverPromptText.text = string.Empty;
            m_HoverPromptText.raycastTarget = false;
            m_HoverPromptObject.transform.localScale = Vector3.one * Mathf.Max(0.0001f, m_HoverPromptCanvasScale);
            SetPromptVisible(false);
        }

        void SetPromptVisible(bool visible)
        {
            if (m_HoverPromptObject != null && m_HoverPromptObject.activeSelf != visible)
                m_HoverPromptObject.SetActive(visible);
        }

        void ResolveVisualReferences()
        {
            if (m_StateLampRenderers == null || m_StateLampRenderers.Length == 0)
                m_StateLampRenderers = ResolveStateLampRenderers();

            if (m_VisitedStateRenderers == null || m_VisitedStateRenderers.Length == 0)
                m_VisitedStateRenderers = m_StateLampRenderers;

            if (m_StateLights == null || m_StateLights.Length == 0)
                m_StateLights = GetComponentsInChildren<Light>(true);

            if (m_CoreVisualRoot == null)
            {
                for (int i = 0; i < m_StateLampRenderers.Length; i++)
                {
                    Renderer renderer = m_StateLampRenderers[i];
                    if (renderer == null)
                        continue;

                    string rendererName = renderer.name.ToLowerInvariant();
                    if (rendererName.Contains("core"))
                    {
                        m_CoreVisualRoot = renderer.transform;
                        break;
                    }
                }
            }

            if (m_CoreVisualRoot != null)
                m_CoreBaseLocalScale = m_CoreVisualRoot.localScale;
        }

        Renderer[] ResolveStateLampRenderers()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            List<Renderer> lamps = new(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                string rendererName = renderer.name.ToLowerInvariant();
                if (rendererName.Contains("locator") || rendererName.Contains("proxy") || rendererName.Contains("black")
                    || rendererName.Contains("base") || rendererName.Contains("housing") || rendererName.Contains("glass"))
                {
                    continue;
                }

                if (rendererName.Contains("emissive") || rendererName.Contains("status") || rendererName.Contains("glyph")
                    || rendererName.Contains("charge") || rendererName.Contains("core") || rendererName.Contains("tick"))
                {
                    lamps.Add(renderer);
                }
            }

            return lamps.Count > 0 ? lamps.ToArray() : renderers;
        }

        void ApplySharedDefaults()
        {
            if (m_OverrideSharedDefaults)
                return;

            m_IgnoreProgressionModifiers = false;
            m_UnlimitedCharges = false;
            m_Charges = SharedDefaults.Charges;
            m_Cooldown = SharedDefaults.Cooldown;
            m_BaseRefillAmount = SharedDefaults.BaseRefillAmount;
        }

        static void ConfigureInteractionOnlyLayerCollision()
        {
            if (s_InteractionOnlyLayerConfigured)
                return;

            int interactionLayer = LayerMask.NameToLayer(k_InteractionOnlyLayerName);
            if (interactionLayer < 0)
                return;

            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(interactionLayer, i, true);

            s_InteractionOnlyLayerConfigured = true;
        }

        static void ConfigureInteractionOnlyCasterMasks()
        {
            int interactionLayer = LayerMask.NameToLayer(k_InteractionOnlyLayerName);
            if (interactionLayer < 0)
                return;

            int interactionLayerMask = 1 << interactionLayer;

            CurveInteractionCaster[] curveCasters = FindObjectsOfType<CurveInteractionCaster>(true);
            for (int i = 0; i < curveCasters.Length; i++)
            {
                CurveInteractionCaster caster = curveCasters[i];
                if (caster == null)
                    continue;

                LayerMask mask = caster.raycastMask;
                mask.value |= interactionLayerMask;
                caster.raycastMask = mask;
            }

            SphereInteractionCaster[] sphereCasters = FindObjectsOfType<SphereInteractionCaster>(true);
            for (int i = 0; i < sphereCasters.Length; i++)
            {
                SphereInteractionCaster caster = sphereCasters[i];
                if (caster == null)
                    continue;

                LayerMask mask = caster.physicsLayerMask;
                mask.value |= interactionLayerMask;
                caster.physicsLayerMask = mask;
            }

            XRRayInteractor[] rayInteractors = FindObjectsOfType<XRRayInteractor>(true);
            for (int i = 0; i < rayInteractors.Length; i++)
            {
                XRRayInteractor rayInteractor = rayInteractors[i];
                if (rayInteractor == null)
                    continue;

                LayerMask mask = rayInteractor.raycastMask;
                mask.value |= interactionLayerMask;
                rayInteractor.raycastMask = mask;
            }
        }

        void AdjustRemainingCharges(int previousMaxCharges, int newMaxCharges)
        {
            if (m_UnlimitedCharges)
            {
                m_ChargesRemaining = MaxCharges;
                return;
            }

            if (m_ChargesRemaining <= 0)
                return;

            if (m_ChargesRemaining >= previousMaxCharges)
            {
                m_ChargesRemaining = newMaxCharges;
                return;
            }

            int chargesSpent = Mathf.Max(0, previousMaxCharges - m_ChargesRemaining);
            m_ChargesRemaining = Mathf.Clamp(newMaxCharges - chargesSpent, 0, newMaxCharges);
        }
    }
}
