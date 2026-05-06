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
        [SerializeField] Vector3 m_MenuWorldOffset = new(0f, -0.12f, 2f);
        [SerializeField] bool m_UseFixedWorldMenu = true;
        [SerializeField] Transform m_FixedWorldMenuAnchor;
        [SerializeField] string m_FixedWorldMenuAnchorName = "MainMenuAnchor";
        [SerializeField] bool m_ResetPlayerViewOnStart = true;
        [SerializeField] Transform m_MainMenuViewAnchor;
        [SerializeField] string m_MainMenuViewAnchorName = "MainMenuViewAnchor";
        [SerializeField] bool m_RecenterPlayerToViewAnchorOnStart = true;
        [SerializeField, Min(0f)] float m_ViewResetDelaySeconds = 0.12f;
        [SerializeField, Min(0f)] float m_ViewResetRetrySeconds = 0.9f;
        [SerializeField] Vector2 m_PanelAnchor = new(0.34f, 0.52f);
        [SerializeField] Vector2 m_PanelOffset = Vector2.zero;
        [SerializeField] float m_CanvasPlaneDistance = 1.05f;
        [SerializeField, Min(0f)] float m_TrackingStabilizationSeconds = 0.45f;
        [SerializeField, Min(0f)] float m_MenuStartupRepositionWindow = 8f;
        [SerializeField, Min(0f)] float m_MenuRepositionHeightThreshold = 0.22f;
        [SerializeField, Min(0f)] float m_MenuRepositionPlanarThreshold = 0.28f;
        [SerializeField, Min(0f)] float m_CameraStablePositionThreshold = 0.025f;
        [SerializeField, Min(1)] int m_StableCameraFrameRequirement = 6;
        [SerializeField, Range(0f, 1f)] float m_BackgroundMusicVolume = 0.32f;

        GameObject m_MenuRoot;
        GameObject m_ButtonColumn;
        GameObject m_TutorialPromptOverlay;
        GameObject m_ShopLockedPromptOverlay;
        Button m_ContinueButton;
        TextMeshProUGUI m_GoldSummaryLabel;
        AudioSource m_BackgroundMusicSource;
        AudioClip m_BackgroundMusicClip;
        Transform m_RuntimeFixedWorldMenuAnchor;
        float m_MenuPlacementStartedAt;
        float m_MenuPlacementRepositionUntil;
        int m_StableCameraFrameCount;
        bool m_HasLastCameraPosition;
        bool m_HasPinnedCameraPosition;
        bool m_MenuAnchorCalibrated;
        Vector3 m_LastCameraPosition;
        Vector3 m_LastPinnedCameraPosition;

        void Reset()
        {
            ResolvePlayerRig();
        }

        void Awake()
        {
            ResolvePlayerRig();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            BeginMenuPlacementCalibration();
            TryReuseExistingMenuRoot();
            CreateMenuIfNeeded();
            EnsureBackgroundMusic();
            RefreshContinueButtonState();
            RefreshGoldSummaryLabel();
        }

        void Start()
        {
            if (Application.isPlaying && m_ResetPlayerViewOnStart)
                StartCoroutine(ResetPlayerViewAfterTrackingStarts());
        }

        void LateUpdate()
        {
            UpdateRuntimeMenuPlacement();
        }

        void OnDestroy()
        {
            if (m_BackgroundMusicSource != null)
                m_BackgroundMusicSource.Stop();

            if (m_MenuRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(m_MenuRoot);
                else
                    DestroyImmediate(m_MenuRoot);
            }

            DestroyRuntimeFixedWorldMenuAnchor();
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
                SetShopLockedPromptVisible(false);
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

            CreateShopLockedPrompt(contentArea.transform, fontAsset, resolvedMenuSize, contentAreaHeight);
            SetTutorialPromptVisible(false);
            SetShopLockedPromptVisible(false);
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
            m_ShopLockedPromptOverlay = null;
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

            if (m_UseFixedWorldMenu && (!Application.isPlaying || TryResolveAuthoredFixedWorldMenuAnchor(out _)))
                PinMenuToFixedWorldAnchor(menuCamera);
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

        void PinMenuToFixedWorldAnchor(Camera menuCamera)
        {
            if (m_MenuRoot == null)
                return;

            Transform anchor = ResolveFixedWorldMenuAnchor(menuCamera);
            if (anchor == null)
            {
                m_MenuRoot.transform.SetParent(null, true);
                return;
            }

            m_MenuRoot.transform.SetParent(anchor, false);
            m_MenuRoot.transform.localPosition = Vector3.zero;
            m_MenuRoot.transform.localRotation = Quaternion.identity;
            m_MenuRoot.transform.localScale = Vector3.one * ModalMenuPauseUtility.WorldMenuUnitsPerPixel;
        }

        void BeginMenuPlacementCalibration()
        {
            m_MenuPlacementStartedAt = Time.unscaledTime;
            m_MenuPlacementRepositionUntil = m_MenuPlacementStartedAt + Mathf.Max(m_TrackingStabilizationSeconds, m_MenuStartupRepositionWindow);
            m_StableCameraFrameCount = 0;
            m_HasLastCameraPosition = false;
            m_HasPinnedCameraPosition = false;
            m_MenuAnchorCalibrated = false;

            if (Application.isPlaying)
                DestroyRuntimeFixedWorldMenuAnchor();
        }

        void UpdateRuntimeMenuPlacement()
        {
            if (!Application.isPlaying || !m_UseFixedWorldMenu || m_MenuRoot == null)
                return;

            if (TryResolveAuthoredFixedWorldMenuAnchor(out _))
                return;

            ResolvePlayerRig();
            Camera menuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
            if (menuCamera == null)
                return;

            Vector3 cameraPosition = menuCamera.transform.position;
            UpdateCameraStability(cameraPosition);

            bool stabilizationElapsed = Time.unscaledTime - m_MenuPlacementStartedAt >= m_TrackingStabilizationSeconds;
            bool stableEnough = m_StableCameraFrameCount >= m_StableCameraFrameRequirement;
            bool placementTimedOut = Time.unscaledTime >= m_MenuPlacementRepositionUntil;
            if (!stabilizationElapsed || (!stableEnough && !placementTimedOut))
            {
                ModalMenuPauseUtility.ConfigureWorldSpaceMenuRoot(m_MenuRoot, menuCamera, ResolveMenuWorldOffset());
                return;
            }

            bool stillInStartupWindow = Time.unscaledTime <= m_MenuPlacementRepositionUntil;
            bool needsReposition = !m_MenuAnchorCalibrated || (stillInStartupWindow && CameraMovedEnoughSinceLastPin(cameraPosition));
            if (needsReposition)
            {
                RepositionRuntimeFixedWorldMenuAnchor(menuCamera);
                m_MenuAnchorCalibrated = true;
                m_LastPinnedCameraPosition = cameraPosition;
                m_HasPinnedCameraPosition = true;
            }

            PinMenuToFixedWorldAnchor(menuCamera);
        }

        void UpdateCameraStability(Vector3 cameraPosition)
        {
            if (!m_HasLastCameraPosition)
            {
                m_LastCameraPosition = cameraPosition;
                m_HasLastCameraPosition = true;
                m_StableCameraFrameCount = 0;
                return;
            }

            float movement = Vector3.Distance(cameraPosition, m_LastCameraPosition);
            m_StableCameraFrameCount = movement <= m_CameraStablePositionThreshold
                ? m_StableCameraFrameCount + 1
                : 0;
            m_LastCameraPosition = cameraPosition;
        }

        bool CameraMovedEnoughSinceLastPin(Vector3 cameraPosition)
        {
            if (!m_HasPinnedCameraPosition)
                return true;

            Vector3 delta = cameraPosition - m_LastPinnedCameraPosition;
            float heightDelta = Mathf.Abs(delta.y);
            delta.y = 0f;
            return heightDelta >= m_MenuRepositionHeightThreshold || delta.magnitude >= m_MenuRepositionPlanarThreshold;
        }

        void RepositionRuntimeFixedWorldMenuAnchor(Camera menuCamera)
        {
            if (menuCamera == null)
                return;

            Transform anchor = EnsureRuntimeFixedWorldMenuAnchor(menuCamera);
            if (anchor == null)
                return;

            Pose pose = ResolveInitialFixedWorldMenuPose(menuCamera);
            anchor.SetPositionAndRotation(pose.position, pose.rotation);
        }

        Transform ResolveFixedWorldMenuAnchor(Camera menuCamera)
        {
            if (TryResolveAuthoredFixedWorldMenuAnchor(out Transform authoredAnchor))
                return authoredAnchor;

            if (m_RuntimeFixedWorldMenuAnchor != null)
                return m_RuntimeFixedWorldMenuAnchor;

            return EnsureRuntimeFixedWorldMenuAnchor(menuCamera);
        }

        bool TryResolveAuthoredFixedWorldMenuAnchor(out Transform anchor)
        {
            if (m_FixedWorldMenuAnchor != null)
            {
                anchor = m_FixedWorldMenuAnchor;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(m_FixedWorldMenuAnchorName))
            {
                GameObject anchorObject = GameObject.Find(m_FixedWorldMenuAnchorName);
                if (anchorObject != null)
                {
                    m_FixedWorldMenuAnchor = anchorObject.transform;
                    anchor = m_FixedWorldMenuAnchor;
                    return true;
                }
            }

            anchor = null;
            return false;
        }

        Transform EnsureRuntimeFixedWorldMenuAnchor(Camera menuCamera)
        {
            if (m_RuntimeFixedWorldMenuAnchor != null)
                return m_RuntimeFixedWorldMenuAnchor;

            if (menuCamera == null)
                return null;

            GameObject runtimeAnchorObject = new("RuntimeMainMenuAnchor");
            m_RuntimeFixedWorldMenuAnchor = runtimeAnchorObject.transform;
            Pose initialPose = ResolveInitialFixedWorldMenuPose(menuCamera);
            m_RuntimeFixedWorldMenuAnchor.SetPositionAndRotation(initialPose.position, initialPose.rotation);
            return m_RuntimeFixedWorldMenuAnchor;
        }

        Pose ResolveInitialFixedWorldMenuPose(Camera menuCamera)
        {
            Quaternion yawRotation = ResolveYawRotation(menuCamera != null ? menuCamera.transform : null);
            Vector3 origin = menuCamera != null ? menuCamera.transform.position : transform.position;
            Vector3 worldPosition = origin + yawRotation * ResolveMenuWorldOffset();
            return new Pose(worldPosition, yawRotation);
        }

        Quaternion ResolveYawRotation(Transform source)
        {
            Vector3 forward = source != null ? source.forward : Vector3.forward;
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);

            if (forward.sqrMagnitude < 0.0001f && m_PlayerRig != null)
                forward = Vector3.ProjectOnPlane(m_PlayerRig.transform.forward, Vector3.up);

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        System.Collections.IEnumerator ResetPlayerViewAfterTrackingStarts()
        {
            float delay = Mathf.Max(0f, m_ViewResetDelaySeconds);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
            else
                yield return null;

            float retryUntil = Time.unscaledTime + Mathf.Max(0f, m_ViewResetRetrySeconds);
            do
            {
                if (TryResetPlayerViewToMainMenuPose())
                    yield break;

                yield return null;
            }
            while (Time.unscaledTime <= retryUntil);
        }

        bool TryResetPlayerViewToMainMenuPose()
        {
            ResolvePlayerRig();
            if (m_PlayerRig == null || !TryResolveMainMenuViewPose(out Pose targetPose))
                return false;

            Camera menuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
            if (menuCamera == null)
                return false;

            Vector3 currentForward = Vector3.ProjectOnPlane(menuCamera.transform.forward, Vector3.up);
            Vector3 targetForward = Vector3.ProjectOnPlane(targetPose.rotation * Vector3.forward, Vector3.up);
            if (currentForward.sqrMagnitude < 0.0001f || targetForward.sqrMagnitude < 0.0001f)
                return false;

            Transform originTransform = m_PlayerRig.transform;
            float yawDelta = Vector3.SignedAngle(currentForward.normalized, targetForward.normalized, Vector3.up);
            if (Mathf.Abs(yawDelta) > 0.01f)
                originTransform.RotateAround(menuCamera.transform.position, Vector3.up, yawDelta);

            if (m_RecenterPlayerToViewAnchorOnStart)
            {
                Vector3 cameraPosition = menuCamera.transform.position;
                Vector3 positionDelta = targetPose.position - cameraPosition;
                positionDelta.y = 0f;
                if (positionDelta.sqrMagnitude > 0.000001f)
                    originTransform.position += positionDelta;
            }

            return true;
        }

        bool TryResolveMainMenuViewPose(out Pose viewPose)
        {
            if (TryResolveMainMenuViewAnchor(out Transform viewAnchor))
            {
                viewPose = new Pose(viewAnchor.position, ResolveYawRotation(viewAnchor));
                return true;
            }

            if (TryResolveAuthoredFixedWorldMenuAnchor(out Transform menuAnchor))
            {
                Quaternion yawRotation = ResolveYawRotation(menuAnchor);
                viewPose = new Pose(menuAnchor.position - yawRotation * ResolveMenuWorldOffset(), yawRotation);
                return true;
            }

            viewPose = default;
            return false;
        }

        bool TryResolveMainMenuViewAnchor(out Transform anchor)
        {
            if (m_MainMenuViewAnchor != null)
            {
                anchor = m_MainMenuViewAnchor;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(m_MainMenuViewAnchorName))
            {
                GameObject anchorObject = GameObject.Find(m_MainMenuViewAnchorName);
                if (anchorObject != null)
                {
                    m_MainMenuViewAnchor = anchorObject.transform;
                    anchor = m_MainMenuViewAnchor;
                    return true;
                }
            }

            anchor = null;
            return false;
        }

        void DestroyRuntimeFixedWorldMenuAnchor()
        {
            if (m_RuntimeFixedWorldMenuAnchor == null)
                return;

            GameObject anchorObject = m_RuntimeFixedWorldMenuAnchor.gameObject;
            m_RuntimeFixedWorldMenuAnchor = null;

            if (Application.isPlaying)
                Destroy(anchorObject);
            else
                DestroyImmediate(anchorObject);
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

        void CreateShopLockedPrompt(Transform parent, TMP_FontAsset fontAsset, Vector2 resolvedMenuSize, float contentAreaHeight)
        {
            m_ShopLockedPromptOverlay = ModalMenuPauseUtility.CreateUIObject("ShopLockedPromptOverlay", parent);
            RectTransform promptOverlayRect = m_ShopLockedPromptOverlay.GetComponent<RectTransform>();
            promptOverlayRect.anchorMin = Vector2.zero;
            promptOverlayRect.anchorMax = Vector2.one;
            promptOverlayRect.offsetMin = Vector2.zero;
            promptOverlayRect.offsetMax = Vector2.zero;
            promptOverlayRect.anchoredPosition = Vector2.zero;

            LayoutElement promptOverlayLayout = m_ShopLockedPromptOverlay.AddComponent<LayoutElement>();
            promptOverlayLayout.ignoreLayout = true;

            Image promptOverlayImage = m_ShopLockedPromptOverlay.AddComponent<Image>();
            promptOverlayImage.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject promptFrame = ModalMenuPauseUtility.CreateUIObject("PromptFrame", m_ShopLockedPromptOverlay.transform);
            RectTransform promptFrameRect = promptFrame.GetComponent<RectTransform>();
            promptFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptFrameRect.pivot = new Vector2(0.5f, 0.5f);
            promptFrameRect.anchoredPosition = Vector2.zero;
            promptFrameRect.sizeDelta = ResolveTutorialPromptSize(resolvedMenuSize, contentAreaHeight);

            Image promptFrameImage = promptFrame.AddComponent<Image>();
            promptFrameImage.color = new Color(0.02f, 0.03f, 0.05f, 0.96f);

            Outline promptFrameOutline = promptFrame.AddComponent<Outline>();
            promptFrameOutline.effectColor = new Color(0.98f, 0.74f, 0.22f, 0.42f);
            promptFrameOutline.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup promptLayout = promptFrame.AddComponent<VerticalLayoutGroup>();
            promptLayout.spacing = 18f;
            promptLayout.childAlignment = TextAnchor.MiddleCenter;
            promptLayout.childControlWidth = true;
            promptLayout.childControlHeight = true;
            promptLayout.childForceExpandWidth = false;
            promptLayout.childForceExpandHeight = false;
            promptLayout.padding = new RectOffset(24, 24, 18, 18);

            CreateLabel(
                "PromptTitle",
                promptFrame.transform,
                "Shop Locked",
                fontAsset,
                32f,
                FontStyles.Bold,
                new Color(1f, 0.9f, 0.58f, 1f),
                42f,
                false,
                true);

            CreateLabel(
                "PromptBody",
                promptFrame.transform,
                "Complete the tutorial before visiting the shop.",
                fontAsset,
                22f,
                FontStyles.Normal,
                new Color(0.78f, 0.88f, 0.96f, 1f),
                70f,
                true);

            CreateButton(
                "ShopLockedDismissButton",
                promptFrame.transform,
                "OK",
                fontAsset,
                new Color(0.62f, 0.42f, 0.2f, 0.96f),
                CancelShopLockedPrompt,
                UIButtonSoundStyle.Cancel,
                24f,
                new Vector2(240f, 60f));
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
            bool hasShopLockedPromptOverlay = contentAreaTransform != null && contentAreaTransform.Find("ShopLockedPromptOverlay") != null;
            bool hasContinueButton = contentAreaTransform != null && contentAreaTransform.Find("Buttons/ContinueButton") != null;
            bool hasShopButton = contentAreaTransform != null && contentAreaTransform.Find("Buttons/ShopButton") != null;
            return contentAreaTransform == null || !hasButtonColumn || !hasPromptOverlay || !hasShopLockedPromptOverlay || !hasContinueButton || !hasShopButton || !hasGoldSummary || hasLegacyButtons || hasLegacyPromptOverlay || hasLegacyPrompt;
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

            if (m_ShopLockedPromptOverlay == null)
            {
                Transform promptTransform = m_MenuRoot.transform.Find("Panel/ContentArea/ShopLockedPromptOverlay");
                if (promptTransform != null)
                    m_ShopLockedPromptOverlay = promptTransform.gameObject;
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

            if (visible && m_ShopLockedPromptOverlay != null)
                m_ShopLockedPromptOverlay.SetActive(false);

            RectTransform panelRect = ResolvePanelRect();
            if (m_MenuRoot != null && panelRect != null && m_MenuRoot.activeSelf)
                ModalMenuPauseUtility.RefreshMenuLayout(m_MenuRoot, panelRect);
        }

        void SetShopLockedPromptVisible(bool visible)
        {
            ResolveMenuSections();

            if (m_ButtonColumn != null)
                m_ButtonColumn.SetActive(!visible);

            if (visible && m_TutorialPromptOverlay != null)
                m_TutorialPromptOverlay.SetActive(false);

            if (m_ShopLockedPromptOverlay != null)
                m_ShopLockedPromptOverlay.SetActive(visible);

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

        void ShowShopLockedPrompt()
        {
            SetShopLockedPromptVisible(true);
        }

        void CancelShopLockedPrompt()
        {
            SetShopLockedPromptVisible(false);
        }

        void LoadTutorial()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            ResetMovementModeForNewGame();
            ProfileService.BeginNewGameOnSceneLoad(m_TutorialSceneName);
            SceneTransitionService.LoadScene(m_TutorialSceneName, m_BackgroundMusicSource);
        }

        void LoadMainGameDirectly()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            string gameplaySceneName = ResolveGameplaySceneName();
            ResetMovementModeForNewGame();
            ProfileService.BeginNewGameOnSceneLoad(gameplaySceneName);
            SceneTransitionService.LoadScene(gameplaySceneName, m_BackgroundMusicSource);
        }

        static void ResetMovementModeForNewGame()
        {
            MovementModeManager.ResetSessionModeToNormalMove();
        }

        void LoadShop()
        {
            if (ProfileService.IsTutorialRunInProgress())
            {
                ShowShopLockedPrompt();
                return;
            }

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
