using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using CIS5680VRGame.Generation;
using CIS5680VRGame.Progression;
using CIS5680VRGame.UI;

namespace CIS5680VRGame.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class LevelGoalTrigger : MonoBehaviour
    {
        static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int s_ColorId = Shader.PropertyToID("_Color");
        static readonly int s_EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] Renderer[] m_TargetRenderers;
        [SerializeField] GameObject[] m_EnableOnComplete;
        [SerializeField] bool m_ApplyCompletedVisualState = true;
        [SerializeField] Color m_CompletedColor = new(0.22f, 1f, 0.72f, 1f);
        [SerializeField] Color m_CompletedEmissionColor = new(0.08f, 0.9f, 0.55f, 1f);
        [SerializeField] bool m_LogCompletion = true;
        [SerializeField] Vector3 m_MenuLocalOffset = new(0f, -0.36f, 2f);
        [SerializeField] Vector2 m_MenuSize = new(900f, 540f);
        [SerializeField] Vector2 m_ButtonSize = new(240f, 84f);
        [SerializeField] Color m_MenuBackdropColor = new(0f, 0f, 0f, 0.42f);
        [SerializeField] string m_MainMenuSceneName = "MainMenu";
        [Header("Next Level")]
        [SerializeField] bool m_EnableNextLevelButton = true;
        [SerializeField] string m_ShowNextLevelButtonOnSceneName = "Maze1";
        [SerializeField] string m_NextLevelSceneName = "random-maze";
        [SerializeField] string m_NextLevelButtonLabel = "Enter New Level";
        [Header("Random Maze Restart")]
        [SerializeField] bool m_EnableGenerateNewMapButton = true;
        [SerializeField] string m_ShowGenerateNewMapButtonOnSceneName = "random-maze";
        [SerializeField] string m_GenerateNewMapButtonLabel = "Generate New Map";
        [Header("Shop")]
        [SerializeField] bool m_EnableEnterShopButton = true;
        [SerializeField] string m_ShowEnterShopButtonOnSceneName = "random-maze";
        [SerializeField] string m_ShopSceneName = "ShopScene";
        [SerializeField] string m_EnterShopButtonLabel = "Enter Shop";
        [Header("Profile Save")]
        [SerializeField] string m_SaveProfileOnCompletionSceneName = "random-maze";
        [Header("Gold Settlement")]
        [SerializeField] bool m_ShowGoldSettlementOnCompletion = true;
        [SerializeField] Color m_GoldSettlementPrimaryColor = new(0.98f, 0.84f, 0.32f, 1f);
        [SerializeField] Color m_GoldSettlementSecondaryColor = new(0.9f, 0.95f, 1f, 1f);
        [Header("Exit Transition")]
        [SerializeField] bool m_UseExitTransition;
        [SerializeField] MazeExitGateVisualController m_ExitVisualController;
        [SerializeField, Range(0f, 1f)] float m_ExitViewEffectIntensity = 0.86f;
        [SerializeField, Range(0f, 1f)] float m_ExitViewEffectPeakOpacity = 0.98f;
        [SerializeField, Min(0.01f)] float m_ExitViewFadeOutDuration = 0.42f;
        [SerializeField, Min(0f)] float m_ExitViewHoldDuration = 0.18f;
        [SerializeField, Min(0.01f)] float m_ExitViewFadeInDuration = 0.28f;
        [SerializeField] Color m_ExitViewTint = new(0.01f, 0.018f, 0.07f, 1f);
        [SerializeField] Color m_ExitViewCoreColor = new(0.72f, 0.92f, 1f, 1f);
        [SerializeField] bool m_ExitHoldBlackBehindMenu;
        [SerializeField] Color m_ExitMenuBackdropColor = Color.black;
        [SerializeField, Range(0f, 1f)] float m_ExitHapticsAmplitude = 0.18f;
        [SerializeField, Min(0f)] float m_ExitHapticsDuration = 0.12f;
        [SerializeField, Range(0f, 1.2f)] float m_ExitCompletionAudioVolumeScale = 0.84f;

        Collider m_Trigger;
        MaterialPropertyBlock m_PropertyBlock;
        GameObject m_MenuRoot;
        MovementModeManager m_MovementModeManager;
        bool m_HasGoldSettlementSummary;
        RandomMazeGoldSettlementSummary m_GoldSettlementSummary;

        public bool HasCompleted { get; private set; }

        void Awake()
        {
            m_Trigger = GetComponent<Collider>();
            m_Trigger.isTrigger = true;

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_ApplyCompletedVisualState && (m_TargetRenderers == null || m_TargetRenderers.Length == 0))
                m_TargetRenderers = GetComponentsInChildren<Renderer>(true);

            if (m_ExitVisualController == null)
                m_ExitVisualController = GetComponent<MazeExitGateVisualController>();

            m_MovementModeManager = FindObjectOfType<MovementModeManager>();
            m_PropertyBlock = new MaterialPropertyBlock();
            SetCompletionObjectsActive(false);
            GoalBeaconAmbientAudio.EnsureAttached(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            if (HasCompleted || !CanUse(other))
                return;

            HasCompleted = true;
            BeginCompletionSequence();
        }

        void BeginCompletionSequence()
        {
            if (m_ApplyCompletedVisualState)
                ApplyCompletedVisualState();
            SetCompletionObjectsActive(true);

            if (m_UseExitTransition)
            {
                StartCoroutine(ExitTransitionRoutine());
                return;
            }

            CompleteLevelImmediately();
        }

        IEnumerator ExitTransitionRoutine()
        {
            m_ExitVisualController?.PlayExitActivation();
            ModalMenuPauseUtility.PauseGameplayForMenu(m_PlayerRig, m_MovementModeManager);
            PulseAudioService.PlayLevelComplete(m_ExitCompletionAudioVolumeScale);

            bool menuShown = false;
            var settings = new TeleportViewEffectService.BlinkSettings
            {
                Intensity = m_ExitViewEffectIntensity,
                PeakOpacity = m_ExitViewEffectPeakOpacity,
                FadeOutDuration = m_ExitViewFadeOutDuration,
                HoldDuration = m_ExitViewHoldDuration,
                FadeInDuration = m_ExitViewFadeInDuration,
                Tint = m_ExitViewTint,
                CoreColor = m_ExitViewCoreColor,
                HapticsAmplitude = m_ExitHapticsAmplitude,
                HapticsDuration = m_ExitHapticsDuration,
                HoldDarkAfterPeak = m_ExitHoldBlackBehindMenu,
            };

            TeleportViewEffectService.PlayBlink(settings, () =>
            {
                if (menuShown)
                    return;

                menuShown = true;
                SaveProfileIfNeeded();
                ShowCompletionMenu();
            });

            float maxWait = Mathf.Max(0.01f, m_ExitViewFadeOutDuration + m_ExitViewHoldDuration + m_ExitViewFadeInDuration + 0.12f);
            float elapsed = 0f;
            while (!menuShown && elapsed < maxWait)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!menuShown)
            {
                SaveProfileIfNeeded();
                ShowCompletionMenu();
            }

            LogCompletionIfNeeded();
        }

        void CompleteLevelImmediately()
        {
            PulseAudioService.PlayLevelComplete(1f);
            SaveProfileIfNeeded();
            ModalMenuPauseUtility.PauseGameplayForMenu(m_PlayerRig, m_MovementModeManager);
            ShowCompletionMenu();
            LogCompletionIfNeeded();
        }

        void LogCompletionIfNeeded()
        {
            if (m_LogCompletion)
                Debug.Log("Maze goal reached.", this);
        }

        bool CanUse(Collider other)
        {
            if (other == null)
                return false;

            XROrigin rig = other.GetComponentInParent<XROrigin>();
            if (rig == null)
                return false;

            return m_PlayerRig == null || rig == m_PlayerRig;
        }

        void ApplyCompletedVisualState()
        {
            if (m_TargetRenderers == null)
                return;

            for (int i = 0; i < m_TargetRenderers.Length; i++)
            {
                Renderer targetRenderer = m_TargetRenderers[i];
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(s_BaseColorId, m_CompletedColor);
                m_PropertyBlock.SetColor(s_ColorId, m_CompletedColor);
                m_PropertyBlock.SetColor(s_EmissionColorId, m_CompletedEmissionColor);
                targetRenderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        void SetCompletionObjectsActive(bool active)
        {
            if (m_EnableOnComplete == null)
                return;

            for (int i = 0; i < m_EnableOnComplete.Length; i++)
            {
                if (m_EnableOnComplete[i] != null)
                    m_EnableOnComplete[i].SetActive(active);
            }
        }

        void ShowCompletionMenu()
        {
            if (m_MenuRoot != null)
            {
                Camera existingMenuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
                ModalMenuPauseUtility.RefreshWorldMenuPose(m_MenuRoot, existingMenuCamera, m_MenuLocalOffset);
                ApplyMenuBackdropColor();
                m_MenuRoot.SetActive(true);
                return;
            }

            Camera menuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
            if (menuCamera == null)
                return;

            RectTransform panelRect;
            m_MenuRoot = ModalMenuPauseUtility.CreateWorldSpaceMenuRoot(
                "LevelCompleteMenu",
                menuCamera,
                m_MenuSize,
                ResolveMenuBackdropColor(),
                out panelRect,
                m_MenuLocalOffset);

            GameObject panel = panelRect.gameObject;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.03f, 0.05f, 0.07f, 0.94f);

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
                "Goal Reached",
                fontAsset,
                64f,
                FontStyles.Bold,
                new Color(0.9f, 1f, 0.96f, 1f),
                110f);

            CreateLabel(
                "Message",
                panel.transform,
                "You escaped the maze.",
                fontAsset,
                34f,
                FontStyles.Normal,
                new Color(0.72f, 0.88f, 0.98f, 1f),
                84f);

            if (ShouldShowGoldSettlementSummary())
            {
                CreateLabel(
                    "RunGoldSummary",
                    panel.transform,
                    $"Gold Collected This Run: +{m_GoldSettlementSummary.RunGoldEarned}",
                    fontAsset,
                    30f,
                    FontStyles.Bold,
                    m_GoldSettlementPrimaryColor,
                    62f);

                CreateLabel(
                    "TotalGoldSummary",
                    panel.transform,
                    $"Total Gold: {m_GoldSettlementSummary.TotalGoldAfterDeposit}",
                    fontAsset,
                    26f,
                    FontStyles.Normal,
                    m_GoldSettlementSecondaryColor,
                    52f);
            }

            if (ShouldShowNextLevelButton())
            {
                CreateButton(
                    "NextLevelButton",
                    panel.transform,
                    m_NextLevelButtonLabel,
                    fontAsset,
                    new Color(0.18f, 0.72f, 0.42f, 0.94f),
                    LoadNextLevelScene,
                    UIButtonSoundStyle.Normal,
                    28f);
            }

            if (ShouldShowGenerateNewMapButton())
            {
                CreateButton(
                    "GenerateNewMapButton",
                    panel.transform,
                    m_GenerateNewMapButtonLabel,
                    fontAsset,
                    new Color(0.76f, 0.54f, 0.18f, 0.94f),
                    GenerateNewMapAndRestartLevel,
                    UIButtonSoundStyle.Normal,
                    26f);
            }

            if (ShouldShowEnterShopButton())
            {
                CreateButton(
                    "EnterShopButton",
                    panel.transform,
                    m_EnterShopButtonLabel,
                    fontAsset,
                    new Color(0.62f, 0.34f, 0.82f, 0.94f),
                    LoadShopScene,
                    UIButtonSoundStyle.Confirm,
                    26f);
            }

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
                new Color(0.12f, 0.65f, 0.96f, 0.94f),
                RestartLevel,
                UIButtonSoundStyle.Normal);

            CreateButton(
                "MainMenuButton",
                buttonRow.transform,
                "Return to Main Menu",
                fontAsset,
                new Color(0.1f, 0.14f, 0.2f, 0.94f),
                ReturnToMainMenu,
                UIButtonSoundStyle.Normal,
                22f);

            CreateButton(
                "QuitButton",
                buttonRow.transform,
                "Quit",
                fontAsset,
                new Color(0.12f, 0.16f, 0.22f, 0.94f),
                QuitApplication,
                UIButtonSoundStyle.Cancel);

            ModalMenuPauseUtility.RefreshMenuLayout(m_MenuRoot, panelRect);
        }

        Color ResolveMenuBackdropColor()
        {
            return m_UseExitTransition && HasCompleted ? m_ExitMenuBackdropColor : m_MenuBackdropColor;
        }

        void ApplyMenuBackdropColor()
        {
            if (m_MenuRoot == null)
                return;

            Transform backdrop = m_MenuRoot.transform.Find("Backdrop");
            if (backdrop == null || !backdrop.TryGetComponent(out Image backdropImage))
                return;

            backdropImage.color = ResolveMenuBackdropColor();
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

        bool ShouldShowNextLevelButton()
        {
            if (!m_EnableNextLevelButton || string.IsNullOrWhiteSpace(m_NextLevelSceneName))
                return false;

            if (string.IsNullOrWhiteSpace(m_ShowNextLevelButtonOnSceneName))
                return true;

            Scene activeScene = SceneManager.GetActiveScene();
            return string.Equals(activeScene.name, m_ShowNextLevelButtonOnSceneName, System.StringComparison.Ordinal);
        }

        bool ShouldShowGenerateNewMapButton()
        {
            if (!m_EnableGenerateNewMapButton)
                return false;

            if (string.IsNullOrWhiteSpace(m_ShowGenerateNewMapButtonOnSceneName))
                return true;

            Scene activeScene = SceneManager.GetActiveScene();
            return string.Equals(activeScene.name, m_ShowGenerateNewMapButtonOnSceneName, System.StringComparison.Ordinal);
        }

        bool ShouldShowEnterShopButton()
        {
            if (!m_EnableEnterShopButton || string.IsNullOrWhiteSpace(m_ShopSceneName))
                return false;

            if (string.IsNullOrWhiteSpace(m_ShowEnterShopButtonOnSceneName))
                return true;

            Scene activeScene = SceneManager.GetActiveScene();
            return string.Equals(activeScene.name, m_ShowEnterShopButtonOnSceneName, System.StringComparison.Ordinal);
        }

        void SaveProfileIfNeeded()
        {
            m_HasGoldSettlementSummary = false;
            m_GoldSettlementSummary = default;

            if (string.IsNullOrWhiteSpace(m_SaveProfileOnCompletionSceneName))
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!string.Equals(activeScene.name, m_SaveProfileOnCompletionSceneName, System.StringComparison.Ordinal))
                return;

            if (RandomMazeGoldRunService.TryCompleteActiveRunAndSave(out RandomMazeGoldSettlementSummary settlementSummary))
            {
                m_HasGoldSettlementSummary = true;
                m_GoldSettlementSummary = settlementSummary;
                return;
            }

            ProfileService.RecordRandomMazeEscapeAndSave();
        }

        bool ShouldShowGoldSettlementSummary()
        {
            return m_ShowGoldSettlementOnCompletion && m_HasGoldSettlementSummary;
        }

        void LoadNextLevelScene()
        {
            ModalMenuPauseUtility.ResumeGameplayAfterMenu();
            SceneTransitionService.LoadScene(m_NextLevelSceneName);
        }

        void GenerateNewMapAndRestartLevel()
        {
            ModalMenuPauseUtility.ResumeGameplayAfterMenu();
            Scene activeScene = SceneManager.GetActiveScene();
            RandomMazeRestartUtility.TryPrepareNewMapRestart(activeScene, out _);
            SceneTransitionService.LoadScene(activeScene.name);
        }

        void LoadShopScene()
        {
            ModalMenuPauseUtility.ResumeGameplayAfterMenu();
            SceneTransitionService.LoadScene(m_ShopSceneName);
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
