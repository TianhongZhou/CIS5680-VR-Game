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

namespace CIS5680VRGame.Gameplay
{
    public class PlayerHealthFeedback : MonoBehaviour
    {
        [SerializeField] PlayerHealth m_PlayerHealth;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] MovementModeManager m_MovementModeManager;
        [SerializeField] float m_DamageFlashDuration = 0.45f;
        [SerializeField] float m_DamageFlashFalloff = 2.3f;
        [SerializeField] float m_MaxVignetteIntensity = 0.42f;
        [SerializeField] float m_VignetteSmoothness = 0.72f;
        [SerializeField] Color m_VignetteColor = new(0.9f, 0.08f, 0.08f, 1f);
        [SerializeField] float m_DamageHapticsAmplitude = 0.32f;
        [SerializeField] float m_DamageHapticsDuration = 0.1f;
        [SerializeField] float m_DamageHapticsCooldown = 0.18f;
        [SerializeField] Vector3 m_MenuLocalOffset = new(0f, -0.05f, 1.1f);
        [SerializeField] Vector2 m_MenuSize = new(900f, 540f);
        [SerializeField] Vector2 m_ButtonSize = new(240f, 84f);

        Volume m_DamageVolume;
        VolumeProfile m_DamageProfile;
        Vignette m_DamageVignette;
        GameObject m_MenuRoot;
        float m_DamageFlashStrength;
        float m_LastDamageHapticsTime = -999f;

        void Awake()
        {
            ResolveReferences();
            EnsureDamageVolume();
        }

        void OnEnable()
        {
            ResolveReferences();
            Subscribe();
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
        }

        void ResolveReferences()
        {
            if (m_PlayerHealth == null)
                m_PlayerHealth = FindObjectOfType<PlayerHealth>();

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_MovementModeManager == null)
                m_MovementModeManager = FindObjectOfType<MovementModeManager>();
        }

        void Subscribe()
        {
            if (m_PlayerHealth == null)
                return;

            m_PlayerHealth.HealthChangedDetailed += OnHealthChanged;
            m_PlayerHealth.Died += OnPlayerDied;
        }

        void Unsubscribe()
        {
            if (m_PlayerHealth == null)
                return;

            m_PlayerHealth.HealthChangedDetailed -= OnHealthChanged;
            m_PlayerHealth.Died -= OnPlayerDied;
        }

        void OnHealthChanged(HealthChangeContext context)
        {
            if (context.Reason != HealthChangeReason.Damage || context.Delta >= 0)
                return;

            float normalizedDamage = Mathf.Clamp01(Mathf.Abs(context.Delta) / 12f);
            m_DamageFlashStrength = Mathf.Max(m_DamageFlashStrength, 0.42f + normalizedDamage * 0.58f);
            TriggerDamageHaptics(normalizedDamage);
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
            if (m_DamageVolume != null && m_DamageVignette != null)
                return;

            var volumeObject = new GameObject("PlayerDamageVolume");
            volumeObject.hideFlags = HideFlags.DontSave;
            volumeObject.transform.SetParent(transform, false);

            m_DamageVolume = volumeObject.AddComponent<Volume>();
            m_DamageVolume.isGlobal = true;
            m_DamageVolume.priority = 100f;
            m_DamageVolume.weight = 1f;

            m_DamageProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            m_DamageProfile.hideFlags = HideFlags.DontSave;
            m_DamageVolume.profile = m_DamageProfile;
            m_DamageVignette = m_DamageProfile.Add<Vignette>(true);

            m_DamageVignette.active = true;
            m_DamageVignette.color.overrideState = true;
            m_DamageVignette.color.value = m_VignetteColor;
            m_DamageVignette.intensity.overrideState = true;
            m_DamageVignette.intensity.value = 0f;
            m_DamageVignette.smoothness.overrideState = true;
            m_DamageVignette.smoothness.value = m_VignetteSmoothness;
            m_DamageVignette.rounded.overrideState = true;
            m_DamageVignette.rounded.value = false;
        }

        void ApplyDamageFlash(float normalizedStrength)
        {
            if (m_DamageVignette == null)
                return;

            m_DamageVignette.intensity.value = Mathf.Clamp01(normalizedStrength) * Mathf.Clamp01(m_MaxVignetteIntensity);
            m_DamageVignette.smoothness.value = Mathf.Clamp(m_VignetteSmoothness, 0f, 1f);
            m_DamageVignette.color.value = m_VignetteColor;
        }

        void ShowFailureMenu()
        {
            if (m_MenuRoot != null)
            {
                m_MenuRoot.SetActive(true);
                return;
            }

            Camera menuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
            if (menuCamera == null)
                return;

            RectTransform panelRect;
            m_MenuRoot = ModalMenuPauseUtility.CreateScreenSpaceMenuRoot(
                "LevelFailureMenu",
                menuCamera,
                m_MenuSize,
                new Color(0f, 0f, 0f, 0.42f),
                out panelRect);

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
                RestartLevel);

            CreateButton(
                "QuitButton",
                buttonRow.transform,
                "Quit",
                fontAsset,
                new Color(0.2f, 0.12f, 0.14f, 0.94f),
                QuitApplication);
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
        }

        void CreateButton(
            string name,
            Transform parent,
            string label,
            TMP_FontAsset fontAsset,
            Color backgroundColor,
            UnityEngine.Events.UnityAction onClick)
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
            buttonLabel.fontSize = 30f;
            buttonLabel.fontStyle = FontStyles.Bold;
            buttonLabel.color = Color.white;
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.enableWordWrapping = false;
        }

        void RestartLevel()
        {
            ModalMenuPauseUtility.ResumeGameplayAfterMenu();
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
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
