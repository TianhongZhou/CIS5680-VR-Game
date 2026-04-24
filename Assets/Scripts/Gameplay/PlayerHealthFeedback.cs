using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using CIS5680VRGame.Generation;
using CIS5680VRGame.UI;

namespace CIS5680VRGame.Gameplay
{
    public class PlayerHealthFeedback : MonoBehaviour
    {
        [SerializeField] PlayerHealth m_PlayerHealth;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] MovementModeManager m_MovementModeManager;
        [SerializeField] Camera m_FeedbackCamera;
        [SerializeField] float m_DamageFlashDuration = 0.62f;
        [SerializeField] float m_DamageFlashFalloff = 1.8f;
        [SerializeField] float m_MaxVignetteIntensity = 0.58f;
        [SerializeField] float m_VignetteSmoothness = 0.72f;
        [SerializeField] Color m_VignetteColor = new(0.96f, 0.08f, 0.1f, 1f);
        [SerializeField] Color m_DamageTintColor = new(0.96f, 0.4f, 0.43f, 1f);
        [SerializeField, Range(0f, 1f)] float m_MaxTintBlend = 0.62f;
        [SerializeField, Range(0f, 2f)] float m_MaxExposureDrop = 0.3f;
        [SerializeField, Range(0f, 100f)] float m_MaxSaturationLoss = 55f;
        [SerializeField, Range(0f, 100f)] float m_MaxContrastBoost = 20f;
        [SerializeField] Color m_OverlayFlashColor = new(0.98f, 0.14f, 0.14f, 1f);
        [SerializeField, Range(0f, 1f)] float m_MaxOverlayAlpha = 0.2f;
        [SerializeField] float m_OverlayPulseFrequency = 14f;
        [SerializeField] float m_OverlayShakePixels = 16f;
        [SerializeField] float m_OverlayShakeRotation = 0.75f;
        [SerializeField] float m_OverlayScalePunch = 0.022f;
        [SerializeField] float m_DamageHapticsAmplitude = 0.32f;
        [SerializeField] float m_DamageHapticsDuration = 0.1f;
        [SerializeField] float m_DamageHapticsCooldown = 0.18f;
        [SerializeField] float m_DamageAudioCooldown = 0.16f;
        [SerializeField] Vector3 m_MenuLocalOffset = new(0f, -0.36f, 2f);
        [SerializeField] Vector2 m_MenuSize = new(900f, 540f);
        [SerializeField] Vector2 m_ButtonSize = new(240f, 84f);
        [SerializeField] string m_MainMenuSceneName = "MainMenu";

        Volume m_DamageVolume;
        VolumeProfile m_DamageProfile;
        Vignette m_DamageVignette;
        ColorAdjustments m_DamageColorAdjustments;
        GameObject m_DamageOverlayRoot;
        RectTransform m_DamageOverlayRect;
        Image m_DamageOverlayImage;
        GameObject m_MenuRoot;
        float m_DamageFlashStrength;
        float m_LastDamageHapticsTime = -999f;
        float m_LastDamageAudioTime = -999f;

        void Awake()
        {
            ResolveReferences();
            EnsureFeedbackCameraPostProcessing();
            EnsureDamageOverlay();
            EnsureDamageVolume();
        }

        void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            EnsureFeedbackCameraPostProcessing();
            EnsureDamageOverlay();
            EnsureDamageVolume();
            ApplyDamageFlash(0f);
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void Update()
        {
            ResolveReferences();
            EnsureFeedbackCameraPostProcessing();
            EnsureDamageOverlay();
            EnsureDamageVolume();

            if (m_DamageFlashStrength > 0f)
            {
                m_DamageFlashStrength = Mathf.MoveTowards(m_DamageFlashStrength, 0f, Time.unscaledDeltaTime / Mathf.Max(0.05f, m_DamageFlashDuration));
                ApplyDamageFlash(Mathf.Pow(m_DamageFlashStrength, Mathf.Max(0.01f, m_DamageFlashFalloff)));
            }
            else if (m_DamageVignette != null && m_DamageVignette.intensity.value > 0.0001f)
            {
                ApplyDamageFlash(0f);
            }
        }

        void OnDestroy()
        {
            if (m_MenuRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(m_MenuRoot);
                else
                    DestroyImmediate(m_MenuRoot);
            }

            if (m_DamageProfile != null)
            {
                if (Application.isPlaying)
                    Destroy(m_DamageProfile);
                else
                    DestroyImmediate(m_DamageProfile);
            }

            if (m_DamageVolume != null)
            {
                if (Application.isPlaying)
                    Destroy(m_DamageVolume.gameObject);
                else
                    DestroyImmediate(m_DamageVolume.gameObject);
            }

            if (m_DamageOverlayRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(m_DamageOverlayRoot);
                else
                    DestroyImmediate(m_DamageOverlayRoot);
            }
        }

        void ResolveReferences()
        {
            if (m_PlayerHealth == null)
                m_PlayerHealth = FindObjectOfType<PlayerHealth>();

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_MovementModeManager == null)
                m_MovementModeManager = FindObjectOfType<MovementModeManager>();

            if (m_FeedbackCamera == null)
            {
                if (m_PlayerRig != null && m_PlayerRig.Camera != null)
                    m_FeedbackCamera = m_PlayerRig.Camera;
                else
                    m_FeedbackCamera = Camera.main;
            }
        }

        void Subscribe()
        {
            if (m_PlayerHealth == null)
                return;

            m_PlayerHealth.DamageApplied += OnDamageApplied;
            m_PlayerHealth.Died += OnPlayerDied;
        }

        void Unsubscribe()
        {
            if (m_PlayerHealth == null)
                return;

            m_PlayerHealth.DamageApplied -= OnDamageApplied;
            m_PlayerHealth.Died -= OnPlayerDied;
        }

        void OnDamageApplied(float amount, HealthChangeReason reason)
        {
            if (reason != HealthChangeReason.Damage || amount <= 0f)
                return;

            float normalizedDamage = Mathf.Clamp01(amount / 10f);
            m_DamageFlashStrength = Mathf.Max(m_DamageFlashStrength, 0.72f + normalizedDamage * 0.28f);
            TriggerDamageHaptics(normalizedDamage);
            TriggerDamageAudio(normalizedDamage);
        }

        void OnPlayerDied()
        {
            m_DamageFlashStrength = 1f;
            ApplyDamageFlash(1f);
            ModalMenuPauseUtility.PauseGameplayForMenu(m_PlayerRig, m_MovementModeManager);
            ShowFailureMenu();
        }

        void TriggerDamageHaptics(float normalizedDamage)
        {
            if (Time.unscaledTime - m_LastDamageHapticsTime < m_DamageHapticsCooldown)
                return;

            m_LastDamageHapticsTime = Time.unscaledTime;
            float amplitude = Mathf.Clamp01(m_DamageHapticsAmplitude * Mathf.Lerp(0.8f, 1.3f, normalizedDamage));
            float duration = Mathf.Max(0.01f, m_DamageHapticsDuration);

            SendHaptics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand, amplitude, duration);
            SendHaptics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand, amplitude, duration);
        }

        void TriggerDamageAudio(float normalizedDamage)
        {
            if (Time.unscaledTime - m_LastDamageAudioTime < m_DamageAudioCooldown)
                return;

            m_LastDamageAudioTime = Time.unscaledTime;
            float volumeScale = Mathf.Lerp(0.72f, 0.98f, normalizedDamage);
            PulseAudioService.PlayDamageTaken(volumeScale);
        }

        void SendHaptics(InputDeviceCharacteristics characteristics, float amplitude, float duration)
        {
            var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                if (!device.isValid || !device.TryGetHapticCapabilities(out var capabilities) || !capabilities.supportsImpulse)
                    continue;

                device.SendHapticImpulse(0u, amplitude, duration);
            }
        }

        void EnsureDamageVolume()
        {
            if (m_DamageVolume != null && m_DamageVignette != null && m_DamageColorAdjustments != null)
                return;

            var volumeObject = new GameObject("PlayerDamageVolume");
            volumeObject.hideFlags = HideFlags.DontSave;
            volumeObject.transform.SetParent(transform, false);
            volumeObject.layer = 0;

            m_DamageVolume = volumeObject.AddComponent<Volume>();
            m_DamageVolume.isGlobal = true;
            m_DamageVolume.priority = 100f;
            m_DamageVolume.weight = 1f;

            m_DamageProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            m_DamageProfile.hideFlags = HideFlags.DontSave;
            m_DamageVolume.profile = m_DamageProfile;
            m_DamageVignette = m_DamageProfile.Add<Vignette>(true);
            m_DamageColorAdjustments = m_DamageProfile.Add<ColorAdjustments>(true);

            m_DamageVignette.active = true;
            m_DamageVignette.color.overrideState = true;
            m_DamageVignette.color.value = m_VignetteColor;
            m_DamageVignette.intensity.overrideState = true;
            m_DamageVignette.intensity.value = 0f;
            m_DamageVignette.smoothness.overrideState = true;
            m_DamageVignette.smoothness.value = m_VignetteSmoothness;
            m_DamageVignette.rounded.overrideState = true;
            m_DamageVignette.rounded.value = false;

            m_DamageColorAdjustments.active = true;
            m_DamageColorAdjustments.colorFilter.overrideState = true;
            m_DamageColorAdjustments.colorFilter.value = Color.white;
            m_DamageColorAdjustments.postExposure.overrideState = true;
            m_DamageColorAdjustments.postExposure.value = 0f;
            m_DamageColorAdjustments.saturation.overrideState = true;
            m_DamageColorAdjustments.saturation.value = 0f;
            m_DamageColorAdjustments.contrast.overrideState = true;
            m_DamageColorAdjustments.contrast.value = 0f;
        }

        void EnsureDamageOverlay()
        {
            if (m_DamageOverlayRoot != null && m_DamageOverlayRect != null && m_DamageOverlayImage != null)
                return;

            if (m_FeedbackCamera == null)
                return;

            m_DamageOverlayRoot = new GameObject("PlayerDamageOverlay");
            m_DamageOverlayRoot.hideFlags = HideFlags.DontSave;

            Canvas canvas = m_DamageOverlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = m_FeedbackCamera;
            canvas.planeDistance = Mathf.Max(m_FeedbackCamera.nearClipPlane + 0.18f, 0.32f);
            canvas.sortingOrder = 4500;

            CanvasScaler scaler = m_DamageOverlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject flashObject = ModalMenuPauseUtility.CreateUIObject("Flash", m_DamageOverlayRoot.transform);
            m_DamageOverlayRect = flashObject.GetComponent<RectTransform>();
            m_DamageOverlayRect.anchorMin = Vector2.zero;
            m_DamageOverlayRect.anchorMax = Vector2.one;
            m_DamageOverlayRect.offsetMin = new Vector2(-220f, -220f);
            m_DamageOverlayRect.offsetMax = new Vector2(220f, 220f);
            m_DamageOverlayRect.anchoredPosition = Vector2.zero;
            m_DamageOverlayRect.localRotation = Quaternion.identity;
            m_DamageOverlayRect.localScale = Vector3.one;

            m_DamageOverlayImage = flashObject.AddComponent<Image>();
            m_DamageOverlayImage.raycastTarget = false;
            m_DamageOverlayImage.color = new Color(m_OverlayFlashColor.r, m_OverlayFlashColor.g, m_OverlayFlashColor.b, 0f);
        }

        void EnsureFeedbackCameraPostProcessing()
        {
            if (m_FeedbackCamera == null)
                return;

            UniversalAdditionalCameraData cameraData = m_FeedbackCamera.GetUniversalAdditionalCameraData();
            if (cameraData != null && !cameraData.renderPostProcessing)
                cameraData.renderPostProcessing = true;
        }

        void ApplyDamageFlash(float normalizedStrength)
        {
            if (m_DamageVignette == null)
                return;

            normalizedStrength = Mathf.Clamp01(normalizedStrength);

            m_DamageVignette.intensity.value = normalizedStrength * Mathf.Clamp01(m_MaxVignetteIntensity);
            m_DamageVignette.smoothness.value = Mathf.Clamp(m_VignetteSmoothness, 0f, 1f);
            m_DamageVignette.color.value = m_VignetteColor;

            if (m_DamageColorAdjustments == null)
                return;

            float tintBlend = normalizedStrength * Mathf.Clamp01(m_MaxTintBlend);
            m_DamageColorAdjustments.colorFilter.value = Color.Lerp(Color.white, m_DamageTintColor, tintBlend);
            m_DamageColorAdjustments.postExposure.value = -normalizedStrength * Mathf.Max(0f, m_MaxExposureDrop);
            m_DamageColorAdjustments.saturation.value = -normalizedStrength * Mathf.Max(0f, m_MaxSaturationLoss);
            m_DamageColorAdjustments.contrast.value = normalizedStrength * Mathf.Max(0f, m_MaxContrastBoost);

            ApplyDamageOverlay(normalizedStrength);
        }

        void ApplyDamageOverlay(float normalizedStrength)
        {
            if (m_DamageOverlayImage == null || m_DamageOverlayRect == null)
                return;

            if (normalizedStrength <= 0.0001f)
            {
                m_DamageOverlayImage.color = new Color(m_OverlayFlashColor.r, m_OverlayFlashColor.g, m_OverlayFlashColor.b, 0f);
                m_DamageOverlayRect.anchoredPosition = Vector2.zero;
                m_DamageOverlayRect.localRotation = Quaternion.identity;
                m_DamageOverlayRect.localScale = Vector3.one;
                return;
            }

            float pulse = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * Mathf.Max(0.01f, m_OverlayPulseFrequency) * Mathf.PI * 2f);
            float alpha = normalizedStrength * Mathf.Clamp01(m_MaxOverlayAlpha) * pulse;
            m_DamageOverlayImage.color = new Color(m_OverlayFlashColor.r, m_OverlayFlashColor.g, m_OverlayFlashColor.b, alpha);

            float shakeStrength = normalizedStrength * normalizedStrength;
            float noiseX = (Mathf.PerlinNoise(Time.unscaledTime * 21f, 0.17f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0.83f, Time.unscaledTime * 25f) - 0.5f) * 2f;
            float roll = (Mathf.PerlinNoise(Time.unscaledTime * 19f, 1.37f) - 0.5f) * 2f;

            m_DamageOverlayRect.anchoredPosition = new Vector2(noiseX, noiseY) * (m_OverlayShakePixels * shakeStrength);
            m_DamageOverlayRect.localRotation = Quaternion.Euler(0f, 0f, roll * m_OverlayShakeRotation * shakeStrength);
            m_DamageOverlayRect.localScale = Vector3.one * (1f + m_OverlayScalePunch * normalizedStrength);
        }

        void ShowFailureMenu()
        {
            if (m_MenuRoot != null)
            {
                Camera existingMenuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
                ModalMenuPauseUtility.RefreshWorldMenuPose(m_MenuRoot, existingMenuCamera, m_MenuLocalOffset);
                m_MenuRoot.SetActive(true);
                return;
            }

            Camera menuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
            if (menuCamera == null)
                return;

            RectTransform panelRect;
            m_MenuRoot = ModalMenuPauseUtility.CreateWorldSpaceMenuRoot(
                "LevelFailureMenu",
                menuCamera,
                m_MenuSize,
                new Color(0f, 0f, 0f, 0.42f),
                out panelRect,
                m_MenuLocalOffset);

            GameObject panel = panelRect.gameObject;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.02f, 0.03f, 0.94f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 42, 42);
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            TMP_FontAsset fontAsset = ModalMenuPauseUtility.ResolveFontAsset();

            CreateLabel(
                "Title",
                panel.transform,
                "Health Depleted",
                fontAsset,
                62f,
                FontStyles.Bold,
                new Color(1f, 0.84f, 0.86f, 1f),
                110f);

            CreateLabel(
                "Message",
                panel.transform,
                "You died in the maze.",
                fontAsset,
                34f,
                FontStyles.Normal,
                new Color(0.96f, 0.74f, 0.78f, 1f),
                84f);

            GameObject buttonRow = CreateUIObject("Buttons", panel.transform);
            HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 28f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;

            ContentSizeFitter buttonFitter = buttonRow.AddComponent<ContentSizeFitter>();
            buttonFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            buttonFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateButton(
                "RestartButton",
                buttonRow.transform,
                "Restart",
                fontAsset,
                new Color(0.76f, 0.14f, 0.18f, 0.94f),
                RestartLevel,
                UIButtonSoundStyle.Confirm);

            CreateButton(
                "MainMenuButton",
                buttonRow.transform,
                "Return to Main Menu",
                fontAsset,
                new Color(0.18f, 0.1f, 0.12f, 0.94f),
                ReturnToMainMenu,
                UIButtonSoundStyle.Normal,
                22f);

            CreateButton(
                "QuitButton",
                buttonRow.transform,
                "Quit",
                fontAsset,
                new Color(0.2f, 0.12f, 0.14f, 0.94f),
                QuitApplication,
                UIButtonSoundStyle.Cancel);

            ModalMenuPauseUtility.RefreshMenuLayout(m_MenuRoot, panelRect);
        }

        GameObject CreateUIObject(string name, Transform parent)
        {
            return ModalMenuPauseUtility.CreateUIObject(name, parent);
        }

        void CreateLabel(
            string name,
            Transform parent,
            string text,
            TMP_FontAsset fontAsset,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            float preferredHeight)
        {
            GameObject labelObject = CreateUIObject(name, parent);
            LayoutElement layoutElement = labelObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = fontAsset;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = Mathf.Max(18f, fontSize * 0.65f);
        }

        void CreateButton(
            string name,
            Transform parent,
            string label,
            TMP_FontAsset fontAsset,
            Color backgroundColor,
            UnityEngine.Events.UnityAction onClick,
            UIButtonSoundStyle soundStyle = UIButtonSoundStyle.Normal,
            float fontSize = 30f)
        {
            GameObject buttonObject = CreateUIObject(name, parent);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = m_ButtonSize;

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = m_ButtonSize.x;
            layoutElement.preferredHeight = m_ButtonSize.y;

            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = backgroundColor;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = backgroundColor * 1.12f;
            colors.pressedColor = backgroundColor * 0.88f;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0.45f);
            button.colors = colors;
            UIButtonAudioFeedback.Attach(button, soundStyle);
            button.onClick.AddListener(onClick);

            GameObject textObject = CreateUIObject("Label", buttonObject.transform);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);

            TextMeshProUGUI buttonLabel = textObject.AddComponent<TextMeshProUGUI>();
            buttonLabel.font = fontAsset;
            buttonLabel.text = label;
            buttonLabel.fontSize = fontSize;
            buttonLabel.fontStyle = FontStyles.Bold;
            buttonLabel.color = Color.white;
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.enableWordWrapping = false;
            buttonLabel.enableAutoSizing = true;
            buttonLabel.fontSizeMax = fontSize;
            buttonLabel.fontSizeMin = 18f;
        }

        void RestartLevel()
        {
            ModalMenuPauseUtility.ResumeGameplayAfterMenu();
            Scene activeScene = SceneManager.GetActiveScene();
            RandomMazeRestartUtility.TryPrepareSameMapRestart(activeScene);
            SceneTransitionService.LoadScene(activeScene.name);
        }

        void ReturnToMainMenu()
        {
            ModalMenuPauseUtility.ResumeGameplayAfterMenu();
            SceneTransitionService.LoadScene(m_MainMenuSceneName);
        }

        void QuitApplication()
        {
            ModalMenuPauseUtility.ResumeGameplayAfterMenu();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
