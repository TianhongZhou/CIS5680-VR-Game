using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using TMPro;

namespace CIS5680VRGame.Gameplay
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class HealthRefillStation : MonoBehaviour
    {
        static class SharedDefaults
        {
            public const int MaxUses = 2;
            public const float BaseRestoreAmount = 50f;
            public const float Cooldown = 20f;
        }

        const int k_UnlimitedUseCount = 999999;
        static readonly int k_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int k_ColorId = Shader.PropertyToID("_Color");
        static readonly int k_EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int k_RimColorId = Shader.PropertyToID("_RimColor");

        [SerializeField] PlayerHealth m_PlayerHealth;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] PulseRevealVisual m_PulseVisual;
        [SerializeField] Light m_GlowLight;
        [SerializeField] Renderer[] m_StateLampRenderers;
        [SerializeField] Light[] m_StateLights;
        [SerializeField] bool m_OverrideSharedDefaults;
        [SerializeField] bool m_IgnoreProgressionModifiers;
        [SerializeField] bool m_UnlimitedUses;
        [SerializeField, Min(1)] int m_MaxUses = SharedDefaults.MaxUses;
        [SerializeField] float m_BaseRestoreAmount = SharedDefaults.BaseRestoreAmount;
        [SerializeField] float m_Cooldown = SharedDefaults.Cooldown;
        [SerializeField] Color m_ReadyBackgroundColor = new(0.02f, 0.14f, 0.05f, 1f);
        [SerializeField] Color m_ReadyPulseColor = new(0.38f, 1f, 0.46f, 1f);
        [SerializeField] Color m_CooldownBackgroundColor = new(0.05f, 0.05f, 0.05f, 1f);
        [SerializeField] Color m_CooldownPulseColor = new(0.26f, 0.28f, 0.28f, 1f);
        [SerializeField] Color m_DepletedBackgroundColor = new(0.12f, 0.03f, 0.03f, 1f);
        [SerializeField] Color m_DepletedPulseColor = new(0.8f, 0.24f, 0.18f, 1f);
        [SerializeField] float m_ReadyEmissionStrength = 3.2f;
        [SerializeField] float m_CooldownEmissionStrength = 1.2f;
        [SerializeField] float m_DepletedEmissionStrength = 0.75f;
        [SerializeField] float m_ReadyLightIntensity = 1.45f;
        [SerializeField] float m_CooldownLightIntensity = 0.25f;
        [SerializeField] float m_DepletedLightIntensity = 0.12f;
        [SerializeField] float m_LightPulseAmplitude = 0.22f;
        [SerializeField] float m_LightPulseFrequency = 2.2f;
        [SerializeField] float m_UseHapticsAmplitude = 0.3f;
        [SerializeField] float m_UseHapticsDuration = 0.12f;
        [SerializeField] Vector3 m_HoverPromptLocalOffset = new(0f, 1.15f, 0f);
        [SerializeField] Vector2 m_HoverPromptPanelSize = new(720f, 180f);
        [SerializeField] float m_HoverPromptCanvasScale = 0.0018f;
        [SerializeField] float m_HoverPromptFontSize = 56f;
        [SerializeField] Color m_HoverPromptReadyColor = new(0.92f, 1f, 0.94f, 1f);
        [SerializeField] Color m_HoverPromptCooldownColor = new(1f, 0.62f, 0.24f, 1f);
        [SerializeField] Color m_HoverPromptDepletedColor = new(1f, 0.44f, 0.4f, 1f);
        [SerializeField] Color m_HoverPromptOutlineColor = new(0f, 0f, 0f, 0.88f);
        [SerializeField] Color m_HoverPromptPanelColor = new(0.03f, 0.04f, 0.04f, 0.88f);
        [SerializeField] Color m_HoverPromptPanelBorderColor = new(0.86f, 0.96f, 0.9f, 0.92f);
        [SerializeField] string m_ReadyPromptText = "Press Grip to Restore Health";
        [SerializeField] string m_CooldownPromptFormat = "Recharging... {0:0.0}s";
        [SerializeField] string m_DepletedPromptText = "Health station depleted";
        [SerializeField] Transform m_OrbVisualRoot;
        [SerializeField] Color m_CooldownWarningBackgroundColor = new(0.12f, 0.02f, 0.02f, 1f);
        [SerializeField] Color m_CooldownWarningPulseColorA = new(0.62f, 0.1f, 0.08f, 1f);
        [SerializeField] Color m_CooldownWarningPulseColorB = new(1f, 0.48f, 0.16f, 1f);
        [SerializeField] float m_CooldownWarningFlashFrequency = 3.6f;
        [SerializeField] float m_CooldownWarningFlashEmission = 0.8f;
        [SerializeField] float m_CooldownWarningLightBoost = 0.5f;
        [SerializeField] float m_CooldownOrbPulseScale = 0.12f;
        [SerializeField] float m_ReadyOrbPulseScale = 0.03f;
        [SerializeField] bool m_IgnoreEnemyCollisions = true;
        [SerializeField, Min(0.1f)] float m_EnemyCollisionRefreshInterval = 1f;

        XRSimpleInteractable m_Interactable;
        float m_CooldownEndsAt = -999f;
        readonly HashSet<IXRHoverInteractor> m_HoveringInteractors = new();
        readonly HashSet<Collider> m_IgnoredEnemyColliders = new();
        GameObject m_HoverPromptObject;
        RectTransform m_HoverPromptRect;
        TextMeshProUGUI m_HoverPromptText;
        Transform m_PromptFaceTarget;
        MaterialPropertyBlock m_LampPropertyBlock;
        Collider[] m_OwnColliders;
        float m_NextEnemyCollisionRefreshTime;
        Vector3 m_OrbBaseLocalScale = Vector3.one;
        int m_PersistentRestoreBonusPercent;
        int m_PersistentUseBonus;
        int m_PersistentCooldownReductionPercent;
        int m_SingleRunUsePenalty;
        int m_SingleRunRestoreMultiplierPercent = 100;
        int m_UsesRemaining;

        public int MaxUses
        {
            get
            {
                if (m_UnlimitedUses)
                    return k_UnlimitedUseCount;

                int useBonus = m_IgnoreProgressionModifiers ? 0 : Mathf.Max(0, m_PersistentUseBonus);
                int usePenalty = m_IgnoreProgressionModifiers ? 0 : Mathf.Max(0, m_SingleRunUsePenalty);
                return Mathf.Max(1, m_MaxUses + useBonus - usePenalty);
            }
        }

        public int UsesRemaining => m_UnlimitedUses ? MaxUses : Mathf.Max(0, m_UsesRemaining);
        public bool IsDepleted => !m_UnlimitedUses && UsesRemaining <= 0;
        public bool IsReady => !IsDepleted && Time.time >= m_CooldownEndsAt;
        public bool IsLocatorAvailable => !IsDepleted;

        public void SetPersistentRestoreBonusPercent(int bonusPercent)
        {
            m_PersistentRestoreBonusPercent = Mathf.Max(0, bonusPercent);
        }

        public void SetPersistentUseBonus(int bonusUses)
        {
            int previousMaxUses = MaxUses;
            m_PersistentUseBonus = Mathf.Max(0, bonusUses);
            AdjustRemainingUses(previousMaxUses, MaxUses);
        }

        public void SetSingleRunUsePenalty(int penaltyUses)
        {
            int previousMaxUses = MaxUses;
            m_SingleRunUsePenalty = Mathf.Max(0, penaltyUses);
            AdjustRemainingUses(previousMaxUses, MaxUses);
        }

        public void SetPersistentCooldownReductionPercent(int reductionPercent)
        {
            m_PersistentCooldownReductionPercent = Mathf.Clamp(reductionPercent, 0, 90);
        }

        public void SetSingleRunRestoreMultiplierPercent(int multiplierPercent)
        {
            m_SingleRunRestoreMultiplierPercent = Mathf.Clamp(multiplierPercent, 1, 1000);
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

            m_Interactable = GetComponent<XRSimpleInteractable>();
            m_OwnColliders = GetComponentsInChildren<Collider>(true);

            if (m_PlayerHealth == null)
                m_PlayerHealth = FindObjectOfType<PlayerHealth>();

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_PulseVisual == null)
                m_PulseVisual = GetComponent<PulseRevealVisual>();

            if (m_GlowLight == null)
                m_GlowLight = GetComponentInChildren<Light>(true);

            if (m_StateLampRenderers == null || m_StateLampRenderers.Length == 0)
                m_StateLampRenderers = ResolveStateLampRenderers();

            if (m_StateLights == null || m_StateLights.Length == 0)
                m_StateLights = GetComponentsInChildren<Light>(true);

            if (m_OrbVisualRoot == null && m_GlowLight != null)
                m_OrbVisualRoot = m_GlowLight.transform;

            if (m_OrbVisualRoot != null)
                m_OrbBaseLocalScale = m_OrbVisualRoot.localScale;

            m_LampPropertyBlock = new MaterialPropertyBlock();
            m_UsesRemaining = MaxUses;
            ResolvePromptFaceTarget();
            RefreshIgnoredEnemyCollisions(force: true);
            UpdateVisualState();
        }

        void OnEnable()
        {
            if (m_Interactable == null)
                m_Interactable = GetComponent<XRSimpleInteractable>();

            m_Interactable.hoverEntered.AddListener(OnHoverEntered);
            m_Interactable.hoverExited.AddListener(OnHoverExited);
            m_Interactable.selectEntered.AddListener(OnSelectEntered);
            UpdateVisualState();
        }

        void OnDisable()
        {
            if (m_Interactable != null)
            {
                m_Interactable.hoverEntered.RemoveListener(OnHoverEntered);
                m_Interactable.hoverExited.RemoveListener(OnHoverExited);
                m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
            }

            m_HoveringInteractors.Clear();
            ClearIgnoredEnemyCollisions();
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
            RefreshIgnoredEnemyCollisions(force: false);
            UpdateVisualState();
            UpdateHoverPrompt();
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

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!CanUse(args.interactorObject) || !IsReady || m_PlayerHealth == null)
                return;

            if (!m_PlayerHealth.RestoreAmount(GetEffectiveRestoreAmount(), HealthChangeReason.RefillStation))
                return;

            m_UsesRemaining = m_UnlimitedUses
                ? MaxUses
                : Mathf.Max(0, m_UsesRemaining - 1);

            if (!IsDepleted)
                m_CooldownEndsAt = Time.time + GetEffectiveCooldown();
            else
                m_CooldownEndsAt = -999f;

            PulseAudioService.PlayResourceRestored();

            if (args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor inputInteractor)
                inputInteractor.SendHapticImpulse(Mathf.Clamp01(m_UseHapticsAmplitude), Mathf.Max(0.01f, m_UseHapticsDuration));

            UpdateVisualState();
            UpdateHoverPrompt();
        }

        float GetEffectiveRestoreAmount()
        {
            if (m_IgnoreProgressionModifiers)
                return Mathf.Max(1f, m_BaseRestoreAmount);

            float persistentMultiplier = 1f + Mathf.Max(0, m_PersistentRestoreBonusPercent) / 100f;
            float singleRunMultiplier = Mathf.Clamp(m_SingleRunRestoreMultiplierPercent, 1, 1000) / 100f;
            float multiplier = persistentMultiplier * singleRunMultiplier;
            return Mathf.Max(1f, m_BaseRestoreAmount * multiplier);
        }

        float GetEffectiveCooldown()
        {
            if (m_IgnoreProgressionModifiers)
                return Mathf.Max(0.1f, m_Cooldown);

            float multiplier = 1f - (Mathf.Clamp(m_PersistentCooldownReductionPercent, 0, 90) / 100f);
            return Mathf.Max(0.1f, m_Cooldown * multiplier);
        }

        bool CanUse(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return false;

            XROrigin rig = interactor.transform.GetComponentInParent<XROrigin>();
            return rig != null && (m_PlayerRig == null || rig == m_PlayerRig);
        }

        bool ShouldShowPromptForInteractor(IXRHoverInteractor interactor)
        {
            if (interactor == null || interactor is XRSocketInteractor)
                return false;

            XROrigin rig = interactor.transform.GetComponentInParent<XROrigin>();
            return rig != null && (m_PlayerRig == null || rig == m_PlayerRig);
        }

        void UpdateVisualState()
        {
            if (m_LampPropertyBlock == null)
                m_LampPropertyBlock = new MaterialPropertyBlock();

            if (IsDepleted)
            {
                ApplyOrbScale(1f);
                ApplyLampState(m_DepletedPulseColor, m_DepletedEmissionStrength, m_DepletedLightIntensity);
                return;
            }

            float effectiveCooldown = GetEffectiveCooldown();
            float readiness = Mathf.Clamp01((Mathf.Max(0f, effectiveCooldown - Mathf.Max(0f, m_CooldownEndsAt - Time.time))) / Mathf.Max(0.01f, effectiveCooldown));
            if (IsReady)
                readiness = 1f;

            Color backgroundColor;
            Color pulseColor;
            float emissionStrength;

            if (IsReady)
            {
                float readyWave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, m_LightPulseFrequency) * Mathf.PI * 2f);
                backgroundColor = m_ReadyBackgroundColor;
                pulseColor = m_ReadyPulseColor;
                emissionStrength = m_ReadyEmissionStrength + readyWave * (m_LightPulseAmplitude * 0.55f);
                ApplyOrbScale(1f + readyWave * m_ReadyOrbPulseScale);
                float pulseBoost = readyWave * m_LightPulseAmplitude;
                ApplyLampState(pulseColor, emissionStrength, m_ReadyLightIntensity + pulseBoost);
            }
            else
            {
                float flashWave = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, m_CooldownWarningFlashFrequency) * Mathf.PI * 2f);
                float readinessTint = Mathf.SmoothStep(0f, 1f, readiness);
                backgroundColor = Color.Lerp(m_CooldownWarningBackgroundColor, m_CooldownBackgroundColor, readinessTint * 0.35f);
                pulseColor = Color.Lerp(m_CooldownWarningPulseColorA, m_CooldownWarningPulseColorB, flashWave);
                emissionStrength = Mathf.Lerp(m_CooldownEmissionStrength, m_ReadyEmissionStrength * 0.7f, readinessTint * 0.45f)
                    + flashWave * m_CooldownWarningFlashEmission;
                ApplyOrbScale(1f + flashWave * m_CooldownOrbPulseScale);
                float lightIntensity = Mathf.Lerp(m_CooldownLightIntensity, m_ReadyLightIntensity * 0.72f, readinessTint * 0.55f)
                    + flashWave * m_CooldownWarningLightBoost;
                ApplyLampState(pulseColor, emissionStrength, lightIntensity);
            }
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
            float cooldownFlash = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, m_CooldownWarningFlashFrequency) * Mathf.PI * 2f);
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

        void ApplyOrbScale(float scaleMultiplier)
        {
            if (m_OrbVisualRoot != null)
            {
                m_OrbVisualRoot.localScale = m_OrbBaseLocalScale * Mathf.Max(0.01f, scaleMultiplier);
            }
        }

        void ApplyLampState(Color color, float emissionStrength, float lightIntensity)
        {
            if (m_StateLampRenderers != null)
            {
                for (int i = 0; i < m_StateLampRenderers.Length; i++)
                {
                    Renderer targetRenderer = m_StateLampRenderers[i];
                    if (targetRenderer == null)
                        continue;

                    targetRenderer.GetPropertyBlock(m_LampPropertyBlock);
                    m_LampPropertyBlock.SetColor(k_BaseColorId, color);
                    m_LampPropertyBlock.SetColor(k_ColorId, color);
                    m_LampPropertyBlock.SetColor(k_EmissionColorId, color * Mathf.Max(0f, emissionStrength));
                    m_LampPropertyBlock.SetColor(k_RimColorId, color);
                    targetRenderer.SetPropertyBlock(m_LampPropertyBlock);
                }
            }

            if (m_StateLights != null)
            {
                for (int i = 0; i < m_StateLights.Length; i++)
                {
                    Light stateLight = m_StateLights[i];
                    if (stateLight == null)
                        continue;

                    stateLight.color = color;
                    stateLight.intensity = Mathf.Max(0f, lightIntensity);
                }
            }
            else if (m_GlowLight != null)
            {
                m_GlowLight.color = color;
                m_GlowLight.intensity = Mathf.Max(0f, lightIntensity);
            }
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
                    || rendererName.Contains("base") || rendererName.Contains("housing") || rendererName.Contains("glass")
                    || rendererName.Contains("transparent") || rendererName.Contains("capsule") || rendererName.Contains("rail"))
                {
                    continue;
                }

                if (rendererName.Contains("emissive") || rendererName.Contains("status") || rendererName.Contains("glyph")
                    || rendererName.Contains("pulse") || rendererName.Contains("core") || rendererName.Contains("tick"))
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
            m_UnlimitedUses = false;
            m_MaxUses = SharedDefaults.MaxUses;
            m_BaseRestoreAmount = SharedDefaults.BaseRestoreAmount;
            m_Cooldown = SharedDefaults.Cooldown;
        }

        void RefreshIgnoredEnemyCollisions(bool force)
        {
            if (!Application.isPlaying || !m_IgnoreEnemyCollisions)
                return;

            if (!force && Time.time < m_NextEnemyCollisionRefreshTime)
                return;

            m_NextEnemyCollisionRefreshTime = Time.time + Mathf.Max(0.1f, m_EnemyCollisionRefreshInterval);

            if (m_OwnColliders == null || m_OwnColliders.Length == 0)
                m_OwnColliders = GetComponentsInChildren<Collider>(true);

            EnemyPatrolController[] enemies = FindObjectsOfType<EnemyPatrolController>(true);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null)
                    continue;

                Collider[] enemyColliders = enemies[i].GetComponentsInChildren<Collider>(true);
                for (int j = 0; j < enemyColliders.Length; j++)
                {
                    Collider enemyCollider = enemyColliders[j];
                    if (enemyCollider == null || !m_IgnoredEnemyColliders.Add(enemyCollider))
                        continue;

                    for (int k = 0; k < m_OwnColliders.Length; k++)
                    {
                        Collider ownCollider = m_OwnColliders[k];
                        if (ownCollider == null || ownCollider == enemyCollider)
                            continue;

                        Physics.IgnoreCollision(ownCollider, enemyCollider, true);
                    }
                }
            }
        }

        void ClearIgnoredEnemyCollisions()
        {
            if (m_OwnColliders == null || m_OwnColliders.Length == 0 || m_IgnoredEnemyColliders.Count == 0)
            {
                m_IgnoredEnemyColliders.Clear();
                return;
            }

            foreach (Collider enemyCollider in m_IgnoredEnemyColliders)
            {
                if (enemyCollider == null)
                    continue;

                for (int i = 0; i < m_OwnColliders.Length; i++)
                {
                    Collider ownCollider = m_OwnColliders[i];
                    if (ownCollider == null || ownCollider == enemyCollider)
                        continue;

                    Physics.IgnoreCollision(ownCollider, enemyCollider, false);
                }
            }

            m_IgnoredEnemyColliders.Clear();
        }

        void AdjustRemainingUses(int previousMaxUses, int newMaxUses)
        {
            if (m_UnlimitedUses)
            {
                m_UsesRemaining = MaxUses;
                return;
            }

            if (m_UsesRemaining <= 0)
                return;

            if (m_UsesRemaining >= previousMaxUses)
            {
                m_UsesRemaining = newMaxUses;
                return;
            }

            int usesSpent = Mathf.Max(0, previousMaxUses - m_UsesRemaining);
            m_UsesRemaining = Mathf.Clamp(newMaxUses - usesSpent, 0, newMaxUses);
        }
    }
}
