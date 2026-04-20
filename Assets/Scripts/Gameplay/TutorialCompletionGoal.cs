using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CIS5680VRGame.UI;

namespace CIS5680VRGame.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public class TutorialCompletionGoal : MonoBehaviour
    {
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] MovementModeManager m_MovementModeManager;
        [SerializeField] Renderer[] m_TargetRenderers;
        [SerializeField] string m_TutorialSceneName = "TutorialLevel";
        [SerializeField] string m_GameplaySceneName = "Maze1";
        [SerializeField] Color m_CompletedColor = new(0.34f, 0.96f, 1f, 1f);
        [SerializeField] Color m_CompletedEmissionColor = new(0.18f, 0.72f, 1f, 1f);
        [SerializeField] Vector2 m_MenuSize = new(960f, 560f);
        [SerializeField] Vector2 m_ButtonSize = new(300f, 84f);

        static readonly int s_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int s_ColorId = Shader.PropertyToID("_Color");
        static readonly int s_EmissionColorId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock m_PropertyBlock;
        Collider m_Trigger;
        GameObject m_MenuRoot;
        TutorialLevelController m_TutorialLevelController;

        public bool HasCompleted { get; private set; }

        void Awake()
        {
            m_Trigger = GetComponent<Collider>();
            m_Trigger.isTrigger = true;

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_MovementModeManager == null)
                m_MovementModeManager = FindObjectOfType<MovementModeManager>();

            if (m_TargetRenderers == null || m_TargetRenderers.Length == 0)
                m_TargetRenderers = GetComponentsInChildren<Renderer>(true);

            m_TutorialLevelController = FindObjectOfType<TutorialLevelController>();
            m_PropertyBlock = new MaterialPropertyBlock();
            GoalBeaconAmbientAudio.EnsureAttached(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            if (HasCompleted || !CanUse(other))
                return;

            HasCompleted = true;
            if (m_TutorialLevelController == null)
                m_TutorialLevelController = FindObjectOfType<TutorialLevelController>();

            m_TutorialLevelController?.NotifyTutorialGoalReached();
            ApplyCompletedVisualState();
            PulseAudioService.PlayLevelComplete(0.94f);
            ModalMenuPauseUtility.PauseGameplayForMenu(m_PlayerRig, m_MovementModeManager);
            ShowCompletionMenu();
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

        void ShowCompletionMenu()
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
                "TutorialCompleteMenu",
                menuCamera,
                m_MenuSize,
                new Color(0f, 0f, 0f, 0.42f),
                out panelRect);

            GameObject panel = panelRect.gameObject;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.02f, 0.05f, 0.09f, 0.94f);

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
                "Tutorial Complete",
                fontAsset,
                60f,
                FontStyles.Bold,
                new Color(0.9f, 0.98f, 1f, 1f),
                96f);

            CreateLabel(
                "Message",
                panel.transform,
                "You are ready for the maze. \n Start the full game or replay the tutorial.",
                fontAsset,
                28f,
                FontStyles.Normal,
                new Color(0.74f, 0.88f, 0.98f, 1f),
                84f);

            GameObject buttonRow = ModalMenuPauseUtility.CreateUIObject("Buttons", panel.transform);
            HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 24f;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;

            ContentSizeFitter buttonFitter = buttonRow.AddComponent<ContentSizeFitter>();
            buttonFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            buttonFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateButton(
                "StartGameButton",
                buttonRow.transform,
                "Start Game",
                fontAsset,
                new Color(0.12f, 0.68f, 0.98f, 0.96f),
                StartGame,
                UIButtonSoundStyle.Confirm);

            CreateButton(
                "RestartTutorialButton",
                buttonRow.transform,
                "Restart Tutorial",
                fontAsset,
                new Color(0.16f, 0.22f, 0.3f, 0.94f),
                RestartTutorial,
                UIButtonSoundStyle.Normal);

            ModalMenuPauseUtility.RefreshMenuLayout(m_MenuRoot, panelRect);
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
            buttonLabel.fontSize = 28f;
            buttonLabel.fontStyle = FontStyles.Bold;
            buttonLabel.color = Color.white;
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.enableWordWrapping = false;
        }

        void RestartTutorial()
        {
            ModalMenuPauseUtility.ResumeGameplayAfterMenu();
            SceneTransitionService.LoadScene(m_TutorialSceneName);
        }

        void StartGame()
        {
            ModalMenuPauseUtility.ResumeGameplayAfterMenu();
            SceneTransitionService.LoadScene(m_GameplaySceneName);
        }
    }
}
