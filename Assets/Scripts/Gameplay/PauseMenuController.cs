using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
#if UNITY_EDITOR
using UnityEditor;
#endif
using CIS5680VRGame.Generation;
using CIS5680VRGame.UI;

namespace CIS5680VRGame.Gameplay
{
    public class PauseMenuController : MonoBehaviour
    {
        enum ControllerButton
        {
            PrimaryButton,
            SecondaryButton,
            MenuButton,
        }

        [Header("Scene References")]
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] MovementModeManager m_MovementModeManager;
        [SerializeField] string m_MainMenuSceneName = "MainMenu";

        [Header("Menu Layout")]
        [SerializeField] Vector2 m_MenuSize = new(960f, 620f);
        [SerializeField] Vector2 m_ButtonSize = new(340f, 84f);
        [SerializeField] Vector2 m_PanelOffset = new(0f, -80f);

        [Header("Pause Input")]
        [SerializeField] XRNode m_ControllerNode = XRNode.LeftHand;
        [SerializeField] ControllerButton m_ControllerButton = ControllerButton.SecondaryButton;
        [SerializeField] Key m_KeyboardToggleKey = Key.Escape;
        [SerializeField] bool m_EnableKeyboardFallback = true;
        [SerializeField] bool m_OpenOnStart;

        GameObject m_MenuRoot;
        RectTransform m_PanelRect;
        Coroutine m_LayoutRefreshCoroutine;
        bool m_PreviousControllerPressed;
        bool m_PreviousKeyboardPressed;
        bool m_IsPauseMenuOpen;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        void Start()
        {
            if (!m_OpenOnStart || ModalMenuPauseUtility.IsPausedForMenu)
                return;

            ModalMenuPauseUtility.PauseGameplayForMenu(m_PlayerRig, m_MovementModeManager);
            ShowPauseMenu();
        }

        void Update()
        {
            ResolveReferences();
            HandleControllerInput();
            HandleKeyboardInput();
        }

        void OnDestroy()
        {
            if (m_IsPauseMenuOpen)
                ModalMenuPauseUtility.ResumeGameplayAfterMenu();

            if (m_MenuRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(m_MenuRoot);
            else
                DestroyImmediate(m_MenuRoot);
        }

        void HandleControllerInput()
        {
            bool controllerPressed = IsControllerButtonPressed();
            if (controllerPressed && !m_PreviousControllerPressed)
                TogglePauseMenu();

            m_PreviousControllerPressed = controllerPressed;
        }

        void HandleKeyboardInput()
        {
            if (!m_EnableKeyboardFallback || Keyboard.current == null)
                return;

            bool keyboardPressed = Keyboard.current[m_KeyboardToggleKey].isPressed;
            if (keyboardPressed && !m_PreviousKeyboardPressed)
                TogglePauseMenu();

            m_PreviousKeyboardPressed = keyboardPressed;
        }

        void TogglePauseMenu()
        {
            if (m_IsPauseMenuOpen)
            {
                ResumeGameplay();
                return;
            }

            if (ModalMenuPauseUtility.IsPausedForMenu)
                return;

            ModalMenuPauseUtility.PauseGameplayForMenu(m_PlayerRig, m_MovementModeManager);
            ShowPauseMenu();
            UIAudioService.PlayPauseOpen();
        }

        void ShowPauseMenu()
        {
            EnsureMenuCreated();
            if (m_MenuRoot == null)
                return;

            m_MenuRoot.SetActive(true);
            RefreshMenuLayout();
            m_IsPauseMenuOpen = true;
        }

        void ResumeGameplay()
        {
            StopLayoutRefreshRoutine();

            if (m_MenuRoot != null)
                m_MenuRoot.SetActive(false);

            m_IsPauseMenuOpen = false;
            ModalMenuPauseUtility.ResumeGameplayAfterMenu();
        }

        void RestartLevel()
        {
            ResumeGameplay();
            Scene activeScene = SceneManager.GetActiveScene();
            RandomMazeRestartUtility.TryPrepareSameMapRestart(activeScene);
            SceneTransitionService.LoadScene(activeScene.name);
        }

        void ReturnToMainMenu()
        {
            ResumeGameplay();
            SceneTransitionService.LoadScene(m_MainMenuSceneName);
        }

        void QuitApplication()
        {
            ResumeGameplay();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ResolveReferences()
        {
            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_MovementModeManager == null)
                m_MovementModeManager = FindObjectOfType<MovementModeManager>();
        }

        bool IsControllerButtonPressed()
        {
            UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(m_ControllerNode);
            if (!device.isValid)
                return false;

            return m_ControllerButton switch
            {
                ControllerButton.PrimaryButton => device.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool primaryPressed) && primaryPressed,
                ControllerButton.SecondaryButton => device.TryGetFeatureValue(XRCommonUsages.secondaryButton, out bool secondaryPressed) && secondaryPressed,
                ControllerButton.MenuButton => device.TryGetFeatureValue(XRCommonUsages.menuButton, out bool menuPressed) && menuPressed,
                _ => false,
            };
        }

        void EnsureMenuCreated()
        {
            if (m_MenuRoot != null)
                return;

            Camera menuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
            if (menuCamera == null)
                return;

            RectTransform panelRect;
            m_MenuRoot = ModalMenuPauseUtility.CreateScreenSpaceMenuRoot(
                "PauseMenuCanvas",
                menuCamera,
                m_MenuSize,
                new Color(0f, 0f, 0f, 0.42f),
                out panelRect);
            m_PanelRect = panelRect;

            panelRect.anchoredPosition = m_PanelOffset;

            GameObject panel = panelRect.gameObject;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.03f, 0.05f, 0.08f, 0.94f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 42, 42);
            layout.spacing = 22f;
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
                "Paused",
                fontAsset,
                64f,
                FontStyles.Bold,
                new Color(0.92f, 0.98f, 1f, 1f),
                110f);

            GameObject buttonColumn = ModalMenuPauseUtility.CreateUIObject("Buttons", panel.transform);
            VerticalLayoutGroup buttonLayout = buttonColumn.AddComponent<VerticalLayoutGroup>();
            buttonLayout.spacing = 18f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;

            ContentSizeFitter buttonFitter = buttonColumn.AddComponent<ContentSizeFitter>();
            buttonFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            buttonFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateButton(
                "ResumeButton",
                buttonColumn.transform,
                "Resume",
                fontAsset,
                new Color(0.12f, 0.68f, 0.98f, 0.96f),
                ResumeGameplay,
                UIButtonSoundStyle.Normal);

            CreateButton(
                "RestartButton",
                buttonColumn.transform,
                "Restart Level",
                fontAsset,
                new Color(0.16f, 0.52f, 0.85f, 0.94f),
                RestartLevel,
                UIButtonSoundStyle.Normal);

            CreateButton(
                "MainMenuButton",
                buttonColumn.transform,
                "Return to Main Menu",
                fontAsset,
                new Color(0.14f, 0.18f, 0.26f, 0.94f),
                ReturnToMainMenu,
                UIButtonSoundStyle.Normal);

            CreateButton(
                "QuitButton",
                buttonColumn.transform,
                "Quit",
                fontAsset,
                new Color(0.2f, 0.12f, 0.14f, 0.94f),
                QuitApplication,
                UIButtonSoundStyle.Cancel);

            ModalMenuPauseUtility.RefreshMenuLayout(m_MenuRoot, m_PanelRect);
            m_MenuRoot.SetActive(false);
        }

        void RefreshMenuLayout()
        {
            if (m_MenuRoot == null || m_PanelRect == null)
                return;

            ModalMenuPauseUtility.RefreshMenuLayout(m_MenuRoot, m_PanelRect);

            StopLayoutRefreshRoutine();
            m_LayoutRefreshCoroutine = StartCoroutine(RefreshMenuLayoutDeferred());
        }

        IEnumerator RefreshMenuLayoutDeferred()
        {
            for (int i = 0; i < 3; i++)
            {
                yield return null;

                if (m_MenuRoot == null || !m_MenuRoot.activeInHierarchy)
                    yield break;

                ModalMenuPauseUtility.RefreshMenuLayout(m_MenuRoot, m_PanelRect);
            }

            m_LayoutRefreshCoroutine = null;
        }

        void StopLayoutRefreshRoutine()
        {
            if (m_LayoutRefreshCoroutine == null)
                return;

            StopCoroutine(m_LayoutRefreshCoroutine);
            m_LayoutRefreshCoroutine = null;
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
            GameObject labelObject = ModalMenuPauseUtility.CreateUIObject(name, parent);
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
            UnityEngine.Events.UnityAction onClick,
            UIButtonSoundStyle soundStyle = UIButtonSoundStyle.Normal)
        {
            GameObject buttonObject = ModalMenuPauseUtility.CreateUIObject(name, parent);
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

            GameObject textObject = ModalMenuPauseUtility.CreateUIObject("Label", buttonObject.transform);
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
    }
}
