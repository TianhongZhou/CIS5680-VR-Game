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
        [SerializeField] PlayerHealth m_PlayerHealth;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] PulseRevealVisual m_PulseVisual;
        [SerializeField] Light m_GlowLight;
        [SerializeField] float m_Cooldown = 5f;
        [SerializeField] Color m_ReadyBackgroundColor = new(0.02f, 0.14f, 0.05f, 1f);
        [SerializeField] Color m_ReadyPulseColor = new(0.38f, 1f, 0.46f, 1f);
        [SerializeField] Color m_CooldownBackgroundColor = new(0.05f, 0.05f, 0.05f, 1f);
        [SerializeField] Color m_CooldownPulseColor = new(0.26f, 0.28f, 0.28f, 1f);
        [SerializeField] float m_ReadyEmissionStrength = 3.2f;
        [SerializeField] float m_CooldownEmissionStrength = 1.2f;
        [SerializeField] float m_ReadyLightIntensity = 1.45f;
        [SerializeField] float m_CooldownLightIntensity = 0.25f;
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
        [SerializeField] Color m_HoverPromptOutlineColor = new(0f, 0f, 0f, 0.88f);
        [SerializeField] Color m_HoverPromptPanelColor = new(0.03f, 0.04f, 0.04f, 0.88f);
        [SerializeField] Color m_HoverPromptPanelBorderColor = new(0.86f, 0.96f, 0.9f, 0.92f);
        [SerializeField] string m_ReadyPromptText = "Press Grip to Restore Health";
        [SerializeField] string m_CooldownPromptFormat = "Recharging... {0:0.0}s";
        [SerializeField] Transform m_OrbVisualRoot;
        [SerializeField] Color m_CooldownWarningBackgroundColor = new(0.12f, 0.02f, 0.02f, 1f);
        [SerializeField] Color m_CooldownWarningPulseColorA = new(0.62f, 0.1f, 0.08f, 1f);
        [SerializeField] Color m_CooldownWarningPulseColorB = new(1f, 0.48f, 0.16f, 1f);
        [SerializeField] float m_CooldownWarningFlashFrequency = 3.6f;
        [SerializeField] float m_CooldownWarningFlashEmission = 0.8f;
        [SerializeField] float m_CooldownWarningLightBoost = 0.5f;
        [SerializeField] float m_CooldownOrbPulseScale = 0.12f;
        [SerializeField] float m_ReadyOrbPulseScale = 0.03f;

        XRSimpleInteractable m_Interactable;
        float m_CooldownEndsAt = -999f;
        readonly HashSet<IXRHoverInteractor> m_HoveringInteractors = new();
        GameObject m_HoverPromptObject;
        RectTransform m_HoverPromptRect;
        TextMeshProUGUI m_HoverPromptText;
        Transform m_PromptFaceTarget;
        Vector3 m_OrbBaseLocalScale = Vector3.one;

        public bool IsReady => Time.time >= m_CooldownEndsAt;

        void Awake()
        {
            m_Interactable = GetComponent<XRSimpleInteractable>();

            if (m_PlayerHealth == null)
                m_PlayerHealth = FindObjectOfType<PlayerHealth>();

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_PulseVisual == null)
                m_PulseVisual = GetComponent<PulseRevealVisual>();

            if (m_GlowLight == null)
                m_GlowLight = GetComponentInChildren<Light>(true);

            if (m_OrbVisualRoot == null && m_GlowLight != null)
                m_OrbVisualRoot = m_GlowLight.transform;

            if (m_OrbVisualRoot != null)
                m_OrbBaseLocalScale = m_OrbVisualRoot.localScale;

            ResolvePromptFaceTarget();
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

            if (!m_PlayerHealth.RestoreToMax())
                return;

            m_CooldownEndsAt = Time.time + Mathf.Max(0.1f, m_Cooldown);

            if (args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor inputInteractor)
                inputInteractor.SendHapticImpulse(Mathf.Clamp01(m_UseHapticsAmplitude), Mathf.Max(0.01f, m_UseHapticsDuration));

            UpdateVisualState();
            UpdateHoverPrompt();
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
            float readiness = Mathf.Clamp01((Mathf.Max(0f, m_Cooldown - Mathf.Max(0f, m_CooldownEndsAt - Time.time))) / Mathf.Max(0.01f, m_Cooldown));
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

                if (m_GlowLight != null)
                {
                    float pulseBoost = readyWave * m_LightPulseAmplitude;
                    m_GlowLight.color = m_ReadyPulseColor;
                    m_GlowLight.intensity = m_ReadyLightIntensity + pulseBoost;
                }
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

                if (m_GlowLight != null)
                {
                    m_GlowLight.color = pulseColor;
                    m_GlowLight.intensity = Mathf.Lerp(m_CooldownLightIntensity, m_ReadyLightIntensity * 0.72f, readinessTint * 0.55f)
                        + flashWave * m_CooldownWarningLightBoost;
                }
            }

            m_PulseVisual?.SetVisual(backgroundColor, pulseColor, emissionStrength);
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
            m_HoverPromptText.text = IsReady
                ? m_ReadyPromptText
                : string.Format(m_CooldownPromptFormat, remaining);
            m_HoverPromptText.color = IsReady
                ? m_HoverPromptReadyColor
                : Color.Lerp(m_HoverPromptCooldownColor * 0.72f, m_HoverPromptCooldownColor, cooldownFlash);

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
    }
}
