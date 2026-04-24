using CIS5680VRGame.Gameplay;
using CIS5680VRGame.Progression;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CIS5680VRGame.UI
{
    public class MainMenuController : MonoBehaviour
    {
        const string MainMenuMusicClipPath = "Audio/Music/Music_MainMenu";
        const string RandomMazeSceneName = "random-maze";
        static readonly Vector2 k_MinMainMenuSize = new(620f, 560f);
        static readonly Vector2 k_MainMenuSafePadding = new(96f, 72f);
        static readonly Vector2 k_MinTutorialPromptSize = new(520f, 300f);

        [SerializeField] string m_GameplaySceneName = RandomMazeSceneName;
        [SerializeField] string m_TutorialSceneName = "TutorialLevel";
        [SerializeField] string m_ShopSceneName = "ShopScene";
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] string m_Title = "SONAR BOUNCE";
        [SerializeField] Vector2 m_MenuSize = new(620f, 460f);
        [SerializeField] Vector2 m_ButtonSize = new(320f, 84f);
        [SerializeField] Vector2 m_TutorialPromptSize = new(540f, 360f);
        [SerializeField] Vector2 m_TutorialChoiceButtonSize = new(196f, 60f);
        [SerializeField] Vector2 m_TutorialCancelButtonSize = new(240f, 56f);
        [SerializeField] float m_TutorialChoiceButtonFontSize = 26f;
        [SerializeField] float m_TutorialCancelButtonFontSize = 24f;
        [SerializeField] Vector3 m_MenuWorldOffset = new(0f, -0.44f, 2f);
        [SerializeField] Vector2 m_PanelAnchor = new(0.34f, 0.52f);
        [SerializeField] Vector2 m_PanelOffset = Vector2.zero;
        [SerializeField] float m_CanvasPlaneDistance = 1.05f;
        [SerializeField, Range(0f, 1f)] float m_BackgroundMusicVolume = 0.32f;

        GameObject m_MenuRoot;
        GameObject m_ButtonColumn;
        GameObject m_TutorialPromptOverlay;
        Button m_ContinueButton;
        TextMeshProUGUI m_GoldSummaryLabel;
        AudioSource m_BackgroundMusicSource;
        AudioClip m_BackgroundMusicClip;

        void Reset()
        {
            ResolvePlayerRig();
        }

        void Awake()
        {
            ResolvePlayerRig();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            TryReuseExistingMenuRoot();
            CreateMenuIfNeeded();
            EnsureBackgroundMusic();
            RefreshContinueButtonState();
            RefreshGoldSummaryLabel();
        }

        void OnDestroy()
        {
            if (m_BackgroundMusicSource != null)
                m_BackgroundMusicSource.Stop();

            if (m_MenuRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(m_MenuRoot);
            else
                DestroyImmediate(m_MenuRoot);
        }

        void ResolvePlayerRig()
        {
            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();
        }

        void EnsureBackgroundMusic()
        {
            if (!Application.isPlaying)
                return;

            if (m_BackgroundMusicSource == null)
                m_BackgroundMusicSource = GetComponent<AudioSource>();

            if (m_BackgroundMusicSource == null)
                m_BackgroundMusicSource = gameObject.AddComponent<AudioSource>();

            if (m_BackgroundMusicClip == null)
                m_BackgroundMusicClip = Resources.Load<AudioClip>(MainMenuMusicClipPath);

            if (m_BackgroundMusicClip == null)
                return;

            m_BackgroundMusicSource.playOnAwake = false;
            m_BackgroundMusicSource.loop = true;
            m_BackgroundMusicSource.spatialBlend = 0f;
            m_BackgroundMusicSource.dopplerLevel = 0f;
            m_BackgroundMusicSource.ignoreListenerPause = false;
            m_BackgroundMusicSource.volume = Mathf.Clamp01(m_BackgroundMusicVolume);

            if (m_BackgroundMusicSource.clip != m_BackgroundMusicClip)
                m_BackgroundMusicSource.clip = m_BackgroundMusicClip;

            if (!m_BackgroundMusicSource.isPlaying)
                m_BackgroundMusicSource.Play();
        }

        void CreateMenuIfNeeded()
        {
            Camera menuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
            if (menuCamera == null)
                return;

            bool createdNewRoot = m_MenuRoot == null;
            Vector2 resolvedMenuSize = ResolveMenuSize();
            RectTransform panelRect;
            if (createdNewRoot)
            {
                m_MenuRoot = ModalMenuPauseUtility.CreateWorldSpaceMenuRoot(
                    "MainMenuCanvas",
                    menuCamera,
                    resolvedMenuSize,
                    new Color(0f, 0f, 0f, 0.18f),
                    out panelRect,
                    ResolveMenuWorldOffset());
            }
            else
            {
                m_MenuRoot.SetActive(true);
                panelRect = ResolvePanelRect();
            }

            ConfigureMenuCanvas(menuCamera);
            ConfigurePanelTransform(panelRect, resolvedMenuSize);
            ResolveMenuSections();

            if (!createdNewRoot && MenuRootNeedsRefresh())
            {
                DestroyMenuRoot();
                CreateMenuIfNeeded();
                return;
            }

            if (!createdNewRoot)
            {
                SetTutorialPromptVisible(false);
                RefreshContinueButtonState();
                RefreshGoldSummaryLabel();
                return;
            }

            GameObject panel = panelRect.gameObject;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.035f, 0.055f, 0.08f, 0.8f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(52, 52, 46, 46);
            layout.spacing = 20f;
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
                m_Title,
                fontAsset,
                74f,
                FontStyles.Bold,
                new Color(0.92f, 0.98f, 1f, 1f),
                104f,
                false,
                true);

            m_GoldSummaryLabel = CreateLabel(
                "GoldSummary",
                panel.transform,
                string.Empty,
                fontAsset,
                28f,
                FontStyles.Bold,
                new Color(0.98f, 0.84f, 0.32f, 1f),
                46f,
                false,
                true);

            GameObject spacer = ModalMenuPauseUtility.CreateUIObject("Spacer", panel.transform);
            RectTransform spacerRect = spacer.GetComponent<RectTransform>();
            spacerRect.sizeDelta = new Vector2(0f, 10f);
            LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.preferredHeight = 10f;

            GameObject contentArea = ModalMenuPauseUtility.CreateUIObject("ContentArea", panel.transform);
            RectTransform contentAreaRect = contentArea.GetComponent<RectTransform>();
            contentAreaRect.anchorMin = new Vector2(0f, 0f);
            contentAreaRect.anchorMax = new Vector2(1f, 1f);
            contentAreaRect.offsetMin = Vector2.zero;
            contentAreaRect.offsetMax = Vector2.zero;

            LayoutElement contentAreaLayout = contentArea.AddComponent<LayoutElement>();
            float contentAreaHeight = ResolveContentAreaHeight();
            contentAreaRect.sizeDelta = new Vector2(0f, contentAreaHeight);
            contentAreaLayout.preferredHeight = contentAreaHeight;
            contentAreaLayout.flexibleHeight = 1f;

            m_ButtonColumn = ModalMenuPauseUtility.CreateUIObject("Buttons", contentArea.transform);
            RectTransform buttonColumnRect = m_ButtonColumn.GetComponent<RectTransform>();
            buttonColumnRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonColumnRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonColumnRect.pivot = new Vector2(0.5f, 0.5f);
            buttonColumnRect.anchoredPosition = Vector2.zero;
            buttonColumnRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup buttonLayout = m_ButtonColumn.AddComponent<VerticalLayoutGroup>();
            buttonLayout.spacing = 18f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;

            ContentSizeFitter buttonFitter = m_ButtonColumn.AddComponent<ContentSizeFitter>();
            buttonFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            buttonFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            m_ContinueButton = CreateButton(
                "ContinueButton",
                m_ButtonColumn.transform,
                "Continue",
                fontAsset,
                new Color(0.18f, 0.72f, 0.42f, 0.96f),
                ContinueGame,
                UIButtonSoundStyle.Confirm);

            CreateButton(
                "NewGameButton",
                m_ButtonColumn.transform,
                "New Game",
                fontAsset,
                new Color(0.12f, 0.68f, 0.98f, 0.96f),
                ShowTutorialPrompt,
                UIButtonSoundStyle.Confirm);

            CreateButton(
                "ShopButton",
                m_ButtonColumn.transform,
                "Shop",
                fontAsset,
                new Color(0.76f, 0.64f, 0.18f, 0.96f),
                LoadShop,
                UIButtonSoundStyle.Normal);

            CreateButton(
                "QuitButton",
                m_ButtonColumn.transform,
                "Quit",
                fontAsset,
                new Color(0.16f, 0.2f, 0.28f, 0.94f),
                QuitGame,
                UIButtonSoundStyle.Cancel);

            m_TutorialPromptOverlay = ModalMenuPauseUtility.CreateUIObject("TutorialPromptOverlay", contentArea.transform);
            RectTransform promptOverlayRect = m_TutorialPromptOverlay.GetComponent<RectTransform>();
            promptOverlayRect.anchorMin = Vector2.zero;
            promptOverlayRect.anchorMax = Vector2.one;
            promptOverlayRect.offsetMin = Vector2.zero;
            promptOverlayRect.offsetMax = Vector2.zero;
            promptOverlayRect.anchoredPosition = Vector2.zero;

            LayoutElement promptOverlayLayout = m_TutorialPromptOverlay.AddComponent<LayoutElement>();
            promptOverlayLayout.ignoreLayout = true;

            Image promptOverlayImage = m_TutorialPromptOverlay.AddComponent<Image>();
            promptOverlayImage.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject promptFrame = ModalMenuPauseUtility.CreateUIObject("PromptFrame", m_TutorialPromptOverlay.transform);
            RectTransform promptFrameRect = promptFrame.GetComponent<RectTransform>();
            promptFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptFrameRect.pivot = new Vector2(0.5f, 0.5f);
            promptFrameRect.anchoredPosition = Vector2.zero;
            promptFrameRect.sizeDelta = ResolveTutorialPromptSize(resolvedMenuSize, contentAreaHeight);

            Image promptFrameImage = promptFrame.AddComponent<Image>();
            promptFrameImage.color = new Color(0.02f, 0.03f, 0.05f, 0.96f);

            Outline promptFrameOutline = promptFrame.AddComponent<Outline>();
            promptFrameOutline.effectColor = new Color(0.2f, 0.52f, 0.76f, 0.45f);
            promptFrameOutline.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup promptLayout = promptFrame.AddComponent<VerticalLayoutGroup>();
            promptLayout.spacing = 16f;
            promptLayout.childAlignment = TextAnchor.MiddleCenter;
            promptLayout.childControlWidth = true;
            promptLayout.childControlHeight = true;
            promptLayout.childForceExpandWidth = false;
            promptLayout.childForceExpandHeight = false;
            promptLayout.padding = new RectOffset(20, 20, 12, 12);

            CreateLabel(
                "PromptTitle",
                promptFrame.transform,
                "Play the tutorial first?",
                fontAsset,
                30f,
                FontStyles.Bold,
                new Color(0.9f, 0.98f, 1f, 1f),
                38f,
                false,
                true);

            CreateLabel(
                "PromptBody",
                promptFrame.transform,
                "Start in the guided training level before entering the maze.",
                fontAsset,
                20f,
                FontStyles.Normal,
                new Color(0.72f, 0.86f, 0.96f, 1f),
                46f,
                true);

            GameObject promptButtons = ModalMenuPauseUtility.CreateUIObject("PromptButtons", promptFrame.transform);
            HorizontalLayoutGroup promptButtonLayout = promptButtons.AddComponent<HorizontalLayoutGroup>();
            promptButtonLayout.spacing = 12f;
            promptButtonLayout.childAlignment = TextAnchor.MiddleCenter;
            promptButtonLayout.childControlWidth = true;
            promptButtonLayout.childControlHeight = true;
            promptButtonLayout.childForceExpandWidth = false;
            promptButtonLayout.childForceExpandHeight = false;

            ContentSizeFitter promptButtonFitter = promptButtons.AddComponent<ContentSizeFitter>();
            promptButtonFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            promptButtonFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateButton(
                "TutorialYesButton",
                promptButtons.transform,
                "Yes",
                fontAsset,
                new Color(0.12f, 0.68f, 0.98f, 0.96f),
                LoadTutorial,
                UIButtonSoundStyle.Confirm,
                m_TutorialChoiceButtonFontSize,
                m_TutorialChoiceButtonSize);

            CreateButton(
                "TutorialNoButton",
                promptButtons.transform,
                "No",
                fontAsset,
                new Color(0.16f, 0.2f, 0.28f, 0.94f),
                LoadMainGameDirectly,
                UIButtonSoundStyle.Normal,
                m_TutorialChoiceButtonFontSize,
                m_TutorialChoiceButtonSize);

            CreateButton(
                "TutorialCancelButton",
                promptFrame.transform,
                "Cancel",
                fontAsset,
                new Color(0.12f, 0.16f, 0.22f, 0.96f),
                CancelTutorialPrompt,
                UIButtonSoundStyle.Cancel,
                m_TutorialCancelButtonFontSize,
                m_TutorialCancelButtonSize);

            SetTutorialPromptVisible(false);
            RefreshContinueButtonState();
            RefreshGoldSummaryLabel();
            ModalMenuPauseUtility.RefreshMenuLayout(m_MenuRoot, panelRect);
        }

        void TryReuseExistingMenuRoot()
        {
            if (m_MenuRoot != null)
                return;

            GameObject existingRoot = GameObject.Find("MainMenuCanvas");
            if (existingRoot != null)
                m_MenuRoot = existingRoot;
        }

        void DestroyMenuRoot()
        {
            if (m_MenuRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(m_MenuRoot);
            else
                DestroyImmediate(m_MenuRoot);

            m_MenuRoot = null;
            m_ButtonColumn = null;
            m_TutorialPromptOverlay = null;
            m_ContinueButton = null;
            m_GoldSummaryLabel = null;
        }

        void ConfigureMenuCanvas(Camera menuCamera)
        {
            if (m_MenuRoot == null)
                return;

            Canvas canvas = m_MenuRoot.GetComponent<Canvas>();
            if (canvas == null)
                return;

            ModalMenuPauseUtility.ConfigureWorldSpaceMenuRoot(m_MenuRoot, menuCamera, ResolveMenuWorldOffset());
        }

        RectTransform ResolvePanelRect()
        {
            if (m_MenuRoot == null)
                return null;

            Transform panelTransform = m_MenuRoot.transform.Find("Panel");
            return panelTransform as RectTransform;
        }

        Vector3 ResolveMenuWorldOffset()
        {
            Vector3 offset = m_MenuWorldOffset;
            if (offset.z <= 0f)
                offset.z = Mathf.Max(m_CanvasPlaneDistance, ModalMenuPauseUtility.WorldMenuLocalOffset.z);
            return offset;
        }

        Vector2 ResolveMenuSize()
        {
            float contentAreaHeight = ResolveContentAreaHeight();
            Vector2 resolvedMenuSize = new(
                Mathf.Max(m_MenuSize.x, k_MinMainMenuSize.x),
                Mathf.Max(m_MenuSize.y, k_MinMainMenuSize.y));
            resolvedMenuSize.y = Mathf.Max(resolvedMenuSize.y, 312f + contentAreaHeight);
            return ModalMenuPauseUtility.ClampPanelSize(
                resolvedMenuSize,
                k_MainMenuSafePadding,
                k_MinMainMenuSize);
        }

        float ResolveContentAreaHeight()
        {
            return Mathf.Max(m_ButtonSize.y * 4f + 114f, m_MenuSize.y - 276f);
        }

        Vector2 ResolveTutorialPromptSize(Vector2 resolvedMenuSize, float contentAreaHeight)
        {
            Vector2 preferredSize = new(
                Mathf.Max(m_TutorialPromptSize.x, k_MinTutorialPromptSize.x),
                Mathf.Max(m_TutorialPromptSize.y, k_MinTutorialPromptSize.y));
            float maxWidth = Mathf.Max(k_MinTutorialPromptSize.x, resolvedMenuSize.x - 80f);
            float maxHeight = Mathf.Max(k_MinTutorialPromptSize.y, contentAreaHeight - 24f);
            return new Vector2(
                Mathf.Min(preferredSize.x, maxWidth),
                Mathf.Min(preferredSize.y, maxHeight));
        }

        void ConfigurePanelTransform(RectTransform panelRect, Vector2 resolvedMenuSize)
        {
            if (panelRect == null)
                return;

            ModalMenuPauseUtility.ConfigureCenteredSafePanel(
                panelRect,
                resolvedMenuSize,
                k_MainMenuSafePadding,
                k_MinMainMenuSize);
        }

        bool MenuRootNeedsRefresh()
        {
            if (m_MenuRoot == null)
                return false;

            Transform panelTransform = m_MenuRoot.transform.Find("Panel");
            if (panelTransform == null)
                return true;

            Transform contentAreaTransform = panelTransform.Find("ContentArea");
            bool hasLegacyButtons = panelTransform.Find("Buttons") != null;
            bool hasLegacyPromptOverlay = panelTransform.Find("TutorialPromptOverlay") != null;
            bool hasLegacyPrompt = panelTransform.Find("TutorialPrompt") != null;
            bool hasGoldSummary = panelTransform.Find("GoldSummary") != null;
            bool hasButtonColumn = contentAreaTransform != null && contentAreaTransform.Find("Buttons") != null;
            bool hasPromptOverlay = contentAreaTransform != null && contentAreaTransform.Find("TutorialPromptOverlay") != null;
            bool hasContinueButton = contentAreaTransform != null && contentAreaTransform.Find("Buttons/ContinueButton") != null;
            bool hasShopButton = contentAreaTransform != null && contentAreaTransform.Find("Buttons/ShopButton") != null;
            return contentAreaTransform == null || !hasButtonColumn || !hasPromptOverlay || !hasContinueButton || !hasShopButton || !hasGoldSummary || hasLegacyButtons || hasLegacyPromptOverlay || hasLegacyPrompt;
        }

        void ResolveMenuSections()
        {
            if (m_MenuRoot == null)
                return;

            if (m_ButtonColumn == null)
            {
                Transform buttonTransform = m_MenuRoot.transform.Find("Panel/ContentArea/Buttons");
                if (buttonTransform != null)
                    m_ButtonColumn = buttonTransform.gameObject;
            }

            if (m_TutorialPromptOverlay == null)
            {
                Transform promptTransform = m_MenuRoot.transform.Find("Panel/ContentArea/TutorialPromptOverlay");
                if (promptTransform != null)
                    m_TutorialPromptOverlay = promptTransform.gameObject;
            }

            if (m_ContinueButton == null)
            {
                Transform continueTransform = m_MenuRoot.transform.Find("Panel/ContentArea/Buttons/ContinueButton");
                if (continueTransform != null)
                    m_ContinueButton = continueTransform.GetComponent<Button>();
            }

            if (m_GoldSummaryLabel == null)
            {
                Transform goldSummaryTransform = m_MenuRoot.transform.Find("Panel/GoldSummary");
                if (goldSummaryTransform != null)
                    m_GoldSummaryLabel = goldSummaryTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        void SetTutorialPromptVisible(bool visible)
        {
            ResolveMenuSections();

            if (m_ButtonColumn != null)
                m_ButtonColumn.SetActive(!visible);

            if (m_TutorialPromptOverlay != null)
                m_TutorialPromptOverlay.SetActive(visible);

            RectTransform panelRect = ResolvePanelRect();
            if (m_MenuRoot != null && panelRect != null && m_MenuRoot.activeSelf)
                ModalMenuPauseUtility.RefreshMenuLayout(m_MenuRoot, panelRect);
        }

        void RefreshContinueButtonState()
        {
            ResolveMenuSections();
            if (m_ContinueButton == null)
                return;

            m_ContinueButton.interactable = ProfileService.TryGetContinueSceneName(out _);
        }

        void RefreshGoldSummaryLabel()
        {
            ResolveMenuSections();
            if (m_GoldSummaryLabel == null)
                return;

            m_GoldSummaryLabel.text = $"Total Gold: {ProfileService.GetTotalGold()}";
        }

        TextMeshProUGUI CreateLabel(
            string name,
            Transform parent,
            string text,
            TMP_FontAsset fontAsset,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            float preferredHeight,
            bool allowWordWrap = false,
            bool autoSizeToWidth = false)
        {
            GameObject labelObject = ModalMenuPauseUtility.CreateUIObject(name, parent);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(0f, preferredHeight);
            LayoutElement layoutElement = labelObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = preferredHeight;

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.font = fontAsset;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = allowWordWrap;
            label.enableAutoSizing = autoSizeToWidth || allowWordWrap;
            if (label.enableAutoSizing)
            {
                label.fontSizeMax = fontSize;
                label.fontSizeMin = Mathf.Max(18f, fontSize * 0.55f);
                if (autoSizeToWidth && !allowWordWrap)
                    label.enableWordWrapping = false;
            }

            return label;
        }

        Button CreateButton(
            string name,
            Transform parent,
            string label,
            TMP_FontAsset fontAsset,
            Color backgroundColor,
            UnityEngine.Events.UnityAction onClick,
            UIButtonSoundStyle soundStyle = UIButtonSoundStyle.Normal,
            float labelFontSize = 30f,
            Vector2? customSize = null)
        {
            GameObject buttonObject = ModalMenuPauseUtility.CreateUIObject(name, parent);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            Vector2 resolvedButtonSize = customSize ?? m_ButtonSize;
            buttonRect.sizeDelta = resolvedButtonSize;

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = resolvedButtonSize.x;
            layoutElement.preferredHeight = resolvedButtonSize.y;

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

            GameObject textObject = ModalMenuPauseUtility.CreateUIObject("Label", buttonObject.transform);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);

            TextMeshProUGUI buttonLabel = textObject.AddComponent<TextMeshProUGUI>();
            buttonLabel.font = fontAsset;
            buttonLabel.text = label;
            buttonLabel.fontSize = labelFontSize;
            buttonLabel.fontStyle = FontStyles.Bold;
            buttonLabel.color = Color.white;
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.enableWordWrapping = false;
            buttonLabel.enableAutoSizing = true;
            buttonLabel.fontSizeMax = labelFontSize;
            buttonLabel.fontSizeMin = Mathf.Max(18f, labelFontSize * 0.72f);

            return button;
        }

        void ShowTutorialPrompt()
        {
            SetTutorialPromptVisible(true);
        }

        void CancelTutorialPrompt()
        {
            SetTutorialPromptVisible(false);
        }

        void LoadTutorial()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            ProfileService.BeginNewGameOnSceneLoad(m_TutorialSceneName);
            SceneTransitionService.LoadScene(m_TutorialSceneName, m_BackgroundMusicSource);
        }

        void LoadMainGameDirectly()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            string gameplaySceneName = ResolveGameplaySceneName();
            ProfileService.BeginNewGameOnSceneLoad(gameplaySceneName);
            SceneTransitionService.LoadScene(gameplaySceneName, m_BackgroundMusicSource);
        }

        void LoadShop()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneTransitionService.LoadScene(m_ShopSceneName, m_BackgroundMusicSource);
        }

        void ContinueGame()
        {
            if (!ProfileService.TryGetContinueSceneName(out string continueSceneName))
                return;

            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneTransitionService.LoadScene(continueSceneName, m_BackgroundMusicSource);
        }

        string ResolveGameplaySceneName()
        {
            string resolvedSceneName = ProfileService.ResolveFormalGameplaySceneName(m_GameplaySceneName);
            return string.IsNullOrWhiteSpace(resolvedSceneName) ? RandomMazeSceneName : resolvedSceneName;
        }

        void QuitGame()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
