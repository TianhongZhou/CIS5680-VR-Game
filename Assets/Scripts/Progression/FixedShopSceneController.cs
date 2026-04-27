using System.Collections.Generic;
using CIS5680VRGame.Gameplay;
using CIS5680VRGame.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CIS5680VRGame.Progression
{
    public class FixedShopSceneController : MonoBehaviour
    {
        const string MainMenuSceneName = "MainMenu";
        const string RandomMazeSceneName = "random-maze";
        const string ShopMusicClipPath = "Audio/Music/Music_ShopScene";
        const string SafetyRootName = "ShopSafetyRuntime";

        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] string m_ShopSpawnPointAnchorName = "ShopSpawnPoint";
        [SerializeField] string m_SafetyBoundsSourceName = "Template Environment";
        [SerializeField] string m_GoldDisplayAnchorName = "GoldDisplayAnchor";
        [SerializeField] string m_RefreshPlaceholderAnchorName = "RefreshPlaceholderAnchor";
        [SerializeField] string m_StartNextRunAnchorName = "StartNextRunAnchor";
        [SerializeField] string m_ReturnToMainMenuAnchorName = "ReturnToMainMenuAnchor";
        [SerializeField] string[] m_UpgradeInfoAnchorNames = { "UpgradeInfoAnchor_A", "UpgradeInfoAnchor_B", "UpgradeInfoAnchor_C" };
        [SerializeField] string[] m_UpgradeMarkerNames = { "UpgradeMarker_A", "UpgradeMarker_B", "UpgradeMarker_C" };
        [SerializeField] string m_RuntimeRootName = "ShopRuntimeUI";
        [SerializeField] Vector2 m_GoldPanelSize = new(540f, 150f);
        [SerializeField] Vector2 m_UpgradePanelSize = new(440f, 360f);
        [SerializeField] Vector2 m_ActionButtonPanelSize = new(320f, 150f);
        [SerializeField] Vector2 m_TutorialPromptPanelSize = new(980f, 520f);
        [SerializeField] Vector3 m_TutorialPromptWorldOffset = new(0f, -0.34f, 1.9f);
        [SerializeField] float m_WorldCanvasScale = 0.0016f;
        [SerializeField, Range(0f, 1f)] float m_BackgroundMusicVolume = 0.18f;
        [Header("Safety")]
        [SerializeField] bool m_EnableSafetyBarriers = true;
        [SerializeField] float m_SafetyBoundsPadding = 0.25f;
        [SerializeField] float m_AirWallThickness = 0.45f;
        [SerializeField] float m_AirWallHeightPadding = 1.2f;
        [SerializeField] float m_CeilingBarrierThickness = 0.45f;
        [SerializeField] float m_FallTriggerDepth = 3f;
        [SerializeField] float m_FallTriggerThickness = 1.5f;

        readonly List<UpgradePanelView> m_UpgradeViews = new();

        GameObject m_RuntimeRoot;
        GameObject m_SafetyRoot;
        TextMeshProUGUI m_GoldSummaryLabel;
        TextMeshProUGUI m_RefreshCostLabel;
        TextMeshProUGUI m_RefreshHintLabel;
        Button m_RefreshPlaceholderButton;
        AudioSource m_BackgroundMusicSource;
        AudioClip m_BackgroundMusicClip;
        GameObject m_ShopTutorialRoot;
        MaterialPropertyBlock m_MarkerPropertyBlock;
        Material m_DefaultMarkerMaterial;
        Material m_MechanicMarkerMaterial;
        Material m_RiskySingleRunMarkerMaterial;

        void Awake()
        {
            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            m_MarkerPropertyBlock ??= new MaterialPropertyBlock();
            EnsureBackgroundMusic();
            BuildSafetyGeometry();
        }

        void Start()
        {
            // ProfileService applies pending tutorial gold and first-shop offers from
            // its sceneLoaded callback, which runs after scene Awake and before Start.
            BuildRuntimeUi();
            RefreshAllUi();
            ShowShopTutorialIfNeeded();
        }

        void BuildRuntimeUi()
        {
            DestroyRuntimeUiIfPresent();
            m_UpgradeViews.Clear();
            m_GoldSummaryLabel = null;
            m_RefreshCostLabel = null;
            m_RefreshHintLabel = null;
            m_RefreshPlaceholderButton = null;

            m_RuntimeRoot = new GameObject(m_RuntimeRootName);
            m_RuntimeRoot.transform.SetParent(transform, false);

            BuildGoldPanel();
            BuildUpgradePanels();
            BuildNavigationButtons();
            BuildRefreshPlaceholder();
        }

        void DestroyRuntimeUiIfPresent()
        {
            Transform existing = transform.Find(m_RuntimeRootName);
            if (existing == null)
                return;

            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        void OnDestroy()
        {
            if (m_BackgroundMusicSource != null)
                m_BackgroundMusicSource.Stop();

            DestroyShopTutorialPrompt();

            if (m_MechanicMarkerMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(m_MechanicMarkerMaterial);
                else
                    DestroyImmediate(m_MechanicMarkerMaterial);
            }

            if (m_RiskySingleRunMarkerMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(m_RiskySingleRunMarkerMaterial);
                else
                    DestroyImmediate(m_RiskySingleRunMarkerMaterial);
            }
        }

        void BuildSafetyGeometry()
        {
            DestroySafetyGeometryIfPresent();
            if (!m_EnableSafetyBarriers)
                return;

            if (!TryResolveSafetyBounds(out Bounds safetyBounds))
                return;

            m_SafetyRoot = new GameObject(SafetyRootName);
            m_SafetyRoot.transform.SetParent(transform, false);

            float halfWidth = safetyBounds.extents.x + Mathf.Max(0f, m_SafetyBoundsPadding);
            float halfDepth = safetyBounds.extents.z + Mathf.Max(0f, m_SafetyBoundsPadding);
            float wallHeight = Mathf.Max(2f, safetyBounds.size.y + Mathf.Max(0.1f, m_AirWallHeightPadding));
            float wallThickness = Mathf.Max(0.05f, m_AirWallThickness);
            float ceilingThickness = Mathf.Max(0.05f, m_CeilingBarrierThickness);
            float fallTriggerThickness = Mathf.Max(0.1f, m_FallTriggerThickness);
            float fallDepth = Mathf.Max(0.5f, m_FallTriggerDepth);
            Vector3 center = safetyBounds.center;

            CreateSafetyBox(
                "AirWall_Left",
                center + new Vector3(-(halfWidth + wallThickness * 0.5f), 0f, 0f),
                new Vector3(wallThickness, wallHeight, halfDepth * 2f + wallThickness * 2f),
                false);
            CreateSafetyBox(
                "AirWall_Right",
                center + new Vector3(halfWidth + wallThickness * 0.5f, 0f, 0f),
                new Vector3(wallThickness, wallHeight, halfDepth * 2f + wallThickness * 2f),
                false);
            CreateSafetyBox(
                "AirWall_Back",
                center + new Vector3(0f, 0f, -(halfDepth + wallThickness * 0.5f)),
                new Vector3(halfWidth * 2f + wallThickness * 2f, wallHeight, wallThickness),
                false);
            CreateSafetyBox(
                "AirWall_Front",
                center + new Vector3(0f, 0f, halfDepth + wallThickness * 0.5f),
                new Vector3(halfWidth * 2f + wallThickness * 2f, wallHeight, wallThickness),
                false);
            CreateSafetyBox(
                "AirWall_Ceiling",
                new Vector3(center.x, safetyBounds.max.y + ceilingThickness * 0.5f, center.z),
                new Vector3(halfWidth * 2f + wallThickness * 2f, ceilingThickness, halfDepth * 2f + wallThickness * 2f),
                false);

            GameObject fallTriggerObject = CreateSafetyBox(
                "FallRecoveryTrigger",
                new Vector3(center.x, safetyBounds.min.y - fallDepth, center.z),
                new Vector3(halfWidth * 2f + wallThickness * 2f, fallTriggerThickness, halfDepth * 2f + wallThickness * 2f),
                true);

            ShopSceneFallTrigger fallTrigger = fallTriggerObject.AddComponent<ShopSceneFallTrigger>();
            fallTrigger.Initialize(this);
        }

        void DestroySafetyGeometryIfPresent()
        {
            Transform existing = transform.Find(SafetyRootName);
            if (existing == null)
                return;

            if (Application.isPlaying)
                Destroy(existing.gameObject);
            else
                DestroyImmediate(existing.gameObject);
        }

        void BuildGoldPanel()
        {
            Transform anchor = FindAnchor(m_GoldDisplayAnchorName);
            if (anchor == null)
                return;

            RectTransform panelRect = CreateWorldPanel("GoldDisplayPanel", anchor, m_GoldPanelSize);
            VerticalLayoutGroup layout = ConfigureVerticalPanel(panelRect.gameObject, 16);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(28, 28, 18, 18);

            TMP_FontAsset fontAsset = ModalMenuPauseUtility.ResolveFontAsset();

            CreateLabel(
                "GoldTitle",
                panelRect.transform,
                "Current Gold",
                fontAsset,
                34f,
                FontStyles.Bold,
                new Color(0.97f, 0.93f, 0.82f, 1f),
                46f);

            m_GoldSummaryLabel = CreateLabel(
                "GoldValue",
                panelRect.transform,
                string.Empty,
                fontAsset,
                30f,
                FontStyles.Bold,
                new Color(0.98f, 0.84f, 0.32f, 1f),
                40f);
        }

        void BuildUpgradePanels()
        {
            TMP_FontAsset fontAsset = ModalMenuPauseUtility.ResolveFontAsset();
            IReadOnlyList<string> offerIds = ProfileService.GetOrCreateCurrentShopOfferIds(m_UpgradeInfoAnchorNames.Length);

            for (int i = 0; i < offerIds.Count && i < m_UpgradeInfoAnchorNames.Length; i++)
            {
                int offerIndex = i;
                Transform anchor = FindAnchor(m_UpgradeInfoAnchorNames[i]);
                if (anchor == null)
                    continue;

                if (!ShopUpgradeCatalog.TryGetDefinition(offerIds[i], out ShopUpgradeDefinition definition))
                    continue;

                ShopUpgradeDefinition capturedDefinition = definition;

                RectTransform panelRect = CreateWorldPanel($"UpgradePanel_{i}", anchor, m_UpgradePanelSize);
                VerticalLayoutGroup layout = ConfigureVerticalPanel(panelRect.gameObject, 10);
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.padding = new RectOffset(24, 24, 22, 24);
                Transform markerTransform = i < m_UpgradeMarkerNames.Length
                    ? FindAnchor(m_UpgradeMarkerNames[i])
                    : null;

                TextMeshProUGUI titleLabel = CreateLabel(
                    "Title",
                    panelRect.transform,
                    definition.DisplayName,
                    fontAsset,
                    30f,
                    FontStyles.Bold,
                    new Color(0.9f, 0.98f, 1f, 1f),
                    42f);

                TextMeshProUGUI descriptionLabel = CreateLabel(
                    "Description",
                    panelRect.transform,
                    definition.Description,
                    fontAsset,
                    22f,
                    FontStyles.Normal,
                    new Color(0.76f, 0.88f, 0.96f, 1f),
                    82f,
                    true);

                TextMeshProUGUI priceLabel = CreateLabel(
                    "Price",
                    panelRect.transform,
                    string.Empty,
                    fontAsset,
                    22f,
                    FontStyles.Bold,
                    new Color(0.98f, 0.84f, 0.32f, 1f),
                    30f);

                TextMeshProUGUI statusLabel = CreateLabel(
                    "Status",
                    panelRect.transform,
                    string.Empty,
                    fontAsset,
                    20f,
                    FontStyles.Normal,
                    new Color(0.84f, 0.93f, 1f, 1f),
                    46f,
                    true);

                Button buyButton = CreateButton(
                    "BuyButton",
                    panelRect.transform,
                    "Purchase",
                    fontAsset,
                    new Color(0.16f, 0.68f, 0.96f, 0.94f),
                    () => TryPurchaseUpgrade(offerIndex, capturedDefinition),
                    UIButtonSoundStyle.Confirm,
                    22f,
                    new Vector2(280f, 58f));

                m_UpgradeViews.Add(new UpgradePanelView(
                    offerIndex,
                    capturedDefinition,
                    titleLabel,
                    descriptionLabel,
                    priceLabel,
                    statusLabel,
                    buyButton,
                    markerTransform));
            }
        }

        void BuildNavigationButtons()
        {
            TMP_FontAsset fontAsset = ModalMenuPauseUtility.ResolveFontAsset();

            Transform startAnchor = FindAnchor(m_StartNextRunAnchorName);
            if (startAnchor != null)
            {
                RectTransform panelRect = CreateWorldPanel("StartRunPanel", startAnchor, m_ActionButtonPanelSize);
                VerticalLayoutGroup layout = ConfigureVerticalPanel(panelRect.gameObject, 12);
                layout.padding = new RectOffset(18, 18, 18, 18);
                layout.childAlignment = TextAnchor.MiddleCenter;

                CreateLabel(
                    "Title",
                    panelRect.transform,
                    "Ready for another run?",
                    fontAsset,
                    24f,
                    FontStyles.Bold,
                    new Color(0.9f, 0.98f, 1f, 1f),
                    42f,
                    true);

                CreateButton(
                    "StartRunButton",
                    panelRect.transform,
                    "Start Next Run",
                    fontAsset,
                    new Color(0.18f, 0.72f, 0.42f, 0.94f),
                    StartNextRun,
                    UIButtonSoundStyle.Confirm,
                    22f,
                    new Vector2(260f, 60f));
            }

            Transform returnAnchor = FindAnchor(m_ReturnToMainMenuAnchorName);
            if (returnAnchor != null)
            {
                RectTransform panelRect = CreateWorldPanel("ReturnMainMenuPanel", returnAnchor, m_ActionButtonPanelSize);
                VerticalLayoutGroup layout = ConfigureVerticalPanel(panelRect.gameObject, 12);
                layout.padding = new RectOffset(18, 18, 18, 18);
                layout.childAlignment = TextAnchor.MiddleCenter;

                CreateLabel(
                    "Title",
                    panelRect.transform,
                    "Take a break",
                    fontAsset,
                    24f,
                    FontStyles.Bold,
                    new Color(0.9f, 0.98f, 1f, 1f),
                    42f,
                    true);

                CreateButton(
                    "ReturnMainMenuButton",
                    panelRect.transform,
                    "Return to Main Menu",
                    fontAsset,
                    new Color(0.1f, 0.14f, 0.2f, 0.94f),
                    ReturnToMainMenu,
                    UIButtonSoundStyle.Normal,
                    20f,
                    new Vector2(260f, 60f));
            }
        }

        void BuildRefreshPlaceholder()
        {
            Transform anchor = FindAnchor(m_RefreshPlaceholderAnchorName);
            if (anchor == null)
                return;

            TMP_FontAsset fontAsset = ModalMenuPauseUtility.ResolveFontAsset();
            RectTransform panelRect = CreateWorldPanel("RefreshPlaceholderPanel", anchor, new Vector2(340f, 260f));
            VerticalLayoutGroup layout = ConfigureVerticalPanel(panelRect.gameObject, 8);
            layout.padding = new RectOffset(18, 18, 20, 24);
            layout.childAlignment = TextAnchor.MiddleCenter;

            CreateLabel(
                "Title",
                panelRect.transform,
                "Refresh Offers",
                fontAsset,
                22f,
                FontStyles.Bold,
                new Color(0.9f, 0.98f, 1f, 1f),
                34f);

            m_RefreshPlaceholderButton = CreateButton(
                "RefreshPlaceholderButton",
                panelRect.transform,
                "Refresh Shop",
                fontAsset,
                new Color(0.62f, 0.42f, 0.2f, 0.92f),
                RefreshShopOffers,
                UIButtonSoundStyle.Normal,
                20f,
                new Vector2(220f, 54f));

            m_RefreshCostLabel = CreateLabel(
                "RefreshCost",
                panelRect.transform,
                string.Empty,
                fontAsset,
                18f,
                FontStyles.Bold,
                new Color(0.98f, 0.84f, 0.32f, 1f),
                28f,
                true);

            m_RefreshHintLabel = CreateLabel(
                "RefreshHint",
                panelRect.transform,
                "Complete the maze to earn a free refresh.",
                fontAsset,
                16f,
                FontStyles.Normal,
                new Color(0.82f, 0.9f, 0.98f, 1f),
                56f,
                true);
        }

        void TryPurchaseUpgrade(int offerIndex, ShopUpgradeDefinition definition)
        {
            if (definition == null)
                return;

            if (ProfileService.IsCurrentShopOfferConsumed(offerIndex))
            {
                RefreshAllUi();
                return;
            }

            if (!ProfileService.CanPurchaseUpgrade(definition.Id))
            {
                RefreshAllUi();
                return;
            }

            if (!ProfileService.TryPurchaseCurrentShopOffer(definition.Id, offerIndex))
            {
                RefreshAllUi();
                return;
            }

            RefreshAllUi();
        }

        void RefreshAllUi()
        {
            if (m_GoldSummaryLabel != null)
                m_GoldSummaryLabel.text = $"{ProfileService.GetTotalGold()} G";

            int refreshCost = ProfileService.GetCurrentShopRefreshCost();
            bool canRefresh = ProfileService.CanRefreshShopOffers();
            if (m_RefreshCostLabel != null)
                m_RefreshCostLabel.text = refreshCost <= 0
                    ? "Refresh Cost: Free"
                    : $"Refresh Cost: {refreshCost} G";

            if (m_RefreshPlaceholderButton != null)
                m_RefreshPlaceholderButton.interactable = canRefresh;

            int currentGold = ProfileService.GetTotalGold();
            for (int i = 0; i < m_UpgradeViews.Count; i++)
            {
                UpgradePanelView upgradeView = m_UpgradeViews[i];
                bool isConsumed = ProfileService.IsCurrentShopOfferConsumed(upgradeView.OfferIndex);
                int purchaseCount = ProfileService.GetUpgradePurchaseCount(upgradeView.Definition.Id);
                bool isPurchased = isConsumed || purchaseCount > 0;
                bool canStillPurchase = !isConsumed && ProfileService.CanPurchaseUpgrade(upgradeView.Definition.Id);
                bool canAfford = ProfileService.CanAfford(upgradeView.Definition.Cost);

                if (upgradeView.PriceLabel != null)
                    upgradeView.PriceLabel.text = $"Cost: {upgradeView.Definition.Cost} G";

                if (upgradeView.StatusLabel != null)
                {
                    if (upgradeView.Definition.IsPlaceholder)
                    {
                        upgradeView.StatusLabel.text = isConsumed
                            ? "Purchased"
                            : canAfford
                                ? "It truly does nothing."
                                : $"Need {Mathf.Max(0, upgradeView.Definition.Cost - currentGold)} more G";
                        upgradeView.StatusLabel.color = isConsumed
                            ? new Color(0.3f, 0.9f, 0.58f, 1f)
                            : canAfford
                                ? new Color(0.9f, 0.82f, 0.68f, 1f)
                                : new Color(1f, 0.72f, 0.44f, 1f);
                    }
                    else if (!canStillPurchase)
                    {
                        upgradeView.StatusLabel.text = isPurchased
                            ? "Purchased"
                            : "Limit Reached";
                        upgradeView.StatusLabel.color = new Color(0.3f, 0.9f, 0.58f, 1f);
                    }
                    else
                    {
                        upgradeView.StatusLabel.text = canAfford
                            ? $"You have {currentGold} G"
                            : $"Need {Mathf.Max(0, upgradeView.Definition.Cost - currentGold)} more G";
                        upgradeView.StatusLabel.color = canAfford
                            ? new Color(0.84f, 0.93f, 1f, 1f)
                            : new Color(1f, 0.72f, 0.44f, 1f);
                    }
                }

                if (upgradeView.BuyButton != null)
                    upgradeView.BuyButton.interactable = canStillPurchase && canAfford;

                if (upgradeView.MarkerTransform != null)
                {
                    bool showMarker = !isConsumed && (upgradeView.Definition.IsPlaceholder || canStillPurchase);
                    upgradeView.MarkerTransform.gameObject.SetActive(showMarker);
                    if (showMarker)
                        ApplyMarkerVisual(upgradeView.MarkerTransform, upgradeView.Definition, canAfford, canStillPurchase);
                }
            }
        }

        void ShowShopTutorialIfNeeded()
        {
            if (!Application.isPlaying || !ProfileService.ShouldShowShopTutorial() || m_ShopTutorialRoot != null)
                return;

            Camera menuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
            if (menuCamera == null)
                return;

            RectTransform panelRect;
            m_ShopTutorialRoot = ModalMenuPauseUtility.CreateWorldSpaceMenuRoot(
                "ShopTutorialPrompt",
                menuCamera,
                m_TutorialPromptPanelSize,
                new Color(0f, 0f, 0f, 0.28f),
                out panelRect,
                m_TutorialPromptWorldOffset);

            Canvas canvas = m_ShopTutorialRoot.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 4400;

            GameObject panel = panelRect.gameObject;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.02f, 0.05f, 0.09f, 0.94f);

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(52, 52, 44, 44);
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
                "Shop Tutorial",
                fontAsset,
                54f,
                FontStyles.Bold,
                new Color(0.9f, 0.98f, 1f, 1f),
                76f);

            string grantLine = ProfileService.HasReceivedShopTutorialGoldGrant()
                ? "You have 20 tutorial gold for this visit. Try buying upgrades or refreshing the offers."
                : "Mazes award coins. Spend those coins here to buy upgrades or refresh the offers.";

            CreateLabel(
                "Body",
                panel.transform,
                $"{grantLine}\nPurchased upgrades shape your next run. When you are ready, press Start Next Run to enter the random maze.",
                fontAsset,
                26f,
                FontStyles.Normal,
                new Color(0.74f, 0.88f, 0.98f, 1f),
                150f,
                true);

            CreateButton(
                "DismissButton",
                panel.transform,
                "Got It",
                fontAsset,
                new Color(0.12f, 0.68f, 0.98f, 0.96f),
                DismissShopTutorialPrompt,
                UIButtonSoundStyle.Confirm,
                24f,
                new Vector2(240f, 64f));

            ModalMenuPauseUtility.RefreshMenuLayout(m_ShopTutorialRoot, panelRect);
        }

        void DismissShopTutorialPrompt()
        {
            ProfileService.MarkShopTutorialSeen();
            DestroyShopTutorialPrompt();
        }

        void DestroyShopTutorialPrompt()
        {
            if (m_ShopTutorialRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(m_ShopTutorialRoot);
            else
                DestroyImmediate(m_ShopTutorialRoot);

            m_ShopTutorialRoot = null;
        }

        public void EditorRefreshUiForDebug()
        {
            RefreshAllUi();
        }

        public int DebugOfferSlotCount => m_UpgradeInfoAnchorNames != null && m_UpgradeInfoAnchorNames.Length > 0
            ? m_UpgradeInfoAnchorNames.Length
            : ShopUpgradeCatalog.DefaultOfferCount;

        public void RecoverPlayerToSpawnPoint()
        {
            Transform spawnPoint = FindAnchor(m_ShopSpawnPointAnchorName);
            if (spawnPoint == null)
                return;

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_PlayerRig != null)
            {
                Vector3 destination = spawnPoint.position + Vector3.up * m_PlayerRig.CameraInOriginSpaceHeight;
                m_PlayerRig.MoveCameraToWorldLocation(destination);
            }
            else
            {
                Transform mainCameraTransform = Camera.main != null ? Camera.main.transform : null;
                if (mainCameraTransform != null)
                    mainCameraTransform.position = spawnPoint.position + Vector3.up * 1.7f;
            }

            PulseAudioService.PlayTeleportArrival(0.9f);
        }

        public void EditorRebuildUiForDebug(bool rerollOffers)
        {
            if (rerollOffers)
                ProfileService.RefreshShopOffers(m_UpgradeInfoAnchorNames.Length);

            BuildRuntimeUi();
            RefreshAllUi();
        }

        void StartNextRun()
        {
            SceneTransitionService.LoadScene(RandomMazeSceneName, m_BackgroundMusicSource);
        }

        void ReturnToMainMenu()
        {
            SceneTransitionService.LoadScene(MainMenuSceneName, m_BackgroundMusicSource);
        }

        void RefreshShopOffers()
        {
            if (!ProfileService.TryRefreshShopOffers(m_UpgradeInfoAnchorNames.Length))
            {
                RefreshAllUi();
                return;
            }

            BuildRuntimeUi();
            RefreshAllUi();
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
                m_BackgroundMusicClip = Resources.Load<AudioClip>(ShopMusicClipPath);

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

        Transform FindAnchor(string anchorName)
        {
            if (string.IsNullOrWhiteSpace(anchorName))
                return null;

            Transform found = transform.Find(anchorName);
            if (found != null)
                return found;

            foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(child.name, anchorName, System.StringComparison.Ordinal))
                    return child;
            }

            return GameObject.Find(anchorName)?.transform;
        }

        bool TryResolveSafetyBounds(out Bounds bounds)
        {
            bounds = default;
            Transform boundsSource = FindAnchor(m_SafetyBoundsSourceName);
            if (boundsSource == null)
                return false;

            Renderer[] renderers = boundsSource.GetComponentsInChildren<Renderer>(true);
            bool foundAny = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!foundAny)
                {
                    bounds = renderer.bounds;
                    foundAny = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return foundAny;
        }

        GameObject CreateSafetyBox(string name, Vector3 worldPosition, Vector3 size, bool isTrigger)
        {
            GameObject boxObject = new(name);
            boxObject.transform.SetParent(m_SafetyRoot.transform, false);
            boxObject.transform.position = worldPosition;
            boxObject.transform.rotation = Quaternion.identity;
            boxObject.layer = 0;

            BoxCollider collider = boxObject.AddComponent<BoxCollider>();
            collider.isTrigger = isTrigger;
            collider.size = size;
            return boxObject;
        }

        RectTransform CreateWorldPanel(string name, Transform anchor, Vector2 size)
        {
            GameObject panelRoot = new(name);
            panelRoot.transform.SetParent(m_RuntimeRoot.transform, false);
            panelRoot.transform.position = anchor.position;
            panelRoot.transform.rotation = Quaternion.LookRotation(anchor.forward == Vector3.zero ? Vector3.back : anchor.forward, Vector3.up);

            Canvas canvas = panelRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = ResolveUiCamera();
            canvas.sortingOrder = 20;

            RectTransform canvasRect = panelRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = size;
            canvasRect.localScale = Vector3.one * Mathf.Max(0.0001f, m_WorldCanvasScale);

            CanvasScaler scaler = panelRoot.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;

            panelRoot.AddComponent<GraphicRaycaster>();
            TryAddTrackedDeviceGraphicRaycaster(panelRoot);

            Image panelImage = panelRoot.AddComponent<Image>();
            panelImage.color = new Color(0.04f, 0.06f, 0.08f, 0.88f);

            Outline outline = panelRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0.18f, 0.56f, 0.84f, 0.36f);
            outline.effectDistance = new Vector2(2f, -2f);

            return canvasRect;
        }

        VerticalLayoutGroup ConfigureVerticalPanel(GameObject panelObject, float spacing)
        {
            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = panelObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            return layout;
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
            bool allowWordWrap = false)
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
            label.enableWordWrapping = allowWordWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = Mathf.Max(16f, fontSize * 0.65f);
            return label;
        }

        Button CreateButton(
            string name,
            Transform parent,
            string label,
            TMP_FontAsset fontAsset,
            Color backgroundColor,
            UnityEngine.Events.UnityAction onClick,
            UIButtonSoundStyle soundStyle,
            float fontSize,
            Vector2 size)
        {
            GameObject buttonObject = ModalMenuPauseUtility.CreateUIObject(name, parent);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = size;

            LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = size.x;
            layoutElement.preferredHeight = size.y;

            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = backgroundColor;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            UIButtonAudioFeedback.Attach(button, soundStyle);
            if (onClick != null)
                button.onClick.AddListener(onClick);

            ColorBlock colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = backgroundColor * 1.1f;
            colors.pressedColor = backgroundColor * 0.9f;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0.35f);
            button.colors = colors;

            GameObject textObject = ModalMenuPauseUtility.CreateUIObject("Label", buttonObject.transform);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 6f);
            textRect.offsetMax = new Vector2(-10f, -6f);

            TextMeshProUGUI buttonLabel = textObject.AddComponent<TextMeshProUGUI>();
            buttonLabel.font = fontAsset;
            buttonLabel.text = label;
            buttonLabel.fontSize = fontSize;
            buttonLabel.fontStyle = FontStyles.Bold;
            buttonLabel.color = Color.white;
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.enableAutoSizing = true;
            buttonLabel.fontSizeMax = fontSize;
            buttonLabel.fontSizeMin = Mathf.Max(16f, fontSize * 0.7f);
            buttonLabel.enableWordWrapping = false;

            return button;
        }

        void ApplyMarkerVisual(Transform markerTransform, ShopUpgradeDefinition definition, bool canAfford, bool canStillPurchase)
        {
            if (markerTransform == null || definition == null)
                return;

            ShopUpgradeDisplayVisualController displayVisual = markerTransform.GetComponentInChildren<ShopUpgradeDisplayVisualController>(true);
            if (displayVisual != null)
            {
                displayVisual.ApplyDefinition(definition, canAfford, canStillPurchase);
                return;
            }

            m_MarkerPropertyBlock ??= new MaterialPropertyBlock();
            Renderer markerRenderer = markerTransform.GetComponent<Renderer>();
            if (markerRenderer == null)
                return;

            Material markerMaterial = ResolveMarkerMaterial(markerRenderer, definition);
            if (markerMaterial != null && markerRenderer.sharedMaterial != markerMaterial)
                markerRenderer.sharedMaterial = markerMaterial;

            Color markerColor = definition.IsPlaceholder
                ? new Color(0.82f, 0.82f, 0.82f, 1f)
                : definition.IsRiskySingleRunTemporary
                    ? new Color(1f, 0.18f, 0.12f, 1f)
                : definition.IsSingleRunTemporary
                    ? new Color(1f, 0.62f, 0.24f, 1f)
                : definition.IsMechanicChanging
                    ? new Color(0.32f, 0.98f, 1f, 1f)
                : definition.EffectType switch
                {
                    ShopUpgradeEffectType.MaxHealthBonus => new Color(0.44f, 0.96f, 0.58f, 1f),
                    ShopUpgradeEffectType.HealthRegenCapBonus => new Color(0.58f, 1f, 0.76f, 1f),
                    ShopUpgradeEffectType.MaxEnergyBonus => new Color(0.36f, 0.84f, 1f, 1f),
                    ShopUpgradeEffectType.StartingEnergyBonus => new Color(0.54f, 0.9f, 1f, 1f),
                    ShopUpgradeEffectType.EnergyRegenIntervalReductionPercent => new Color(0.28f, 0.74f, 1f, 1f),
                    ShopUpgradeEffectType.SonarCostReduction => new Color(1f, 0.82f, 0.34f, 1f),
                    ShopUpgradeEffectType.PulseRevealDurationBonusSeconds => new Color(0.74f, 0.54f, 1f, 1f),
                    ShopUpgradeEffectType.PulseRadiusBonusPercent => new Color(0.98f, 0.58f, 0.28f, 1f),
                    ShopUpgradeEffectType.StickyPulseCostReduction => new Color(0.88f, 0.42f, 1f, 1f),
                    ShopUpgradeEffectType.LocatorCooldownReductionPercent => new Color(0.34f, 1f, 0.9f, 1f),
                    ShopUpgradeEffectType.RefillBoostPercent => new Color(0.94f, 1f, 0.42f, 1f),
                    _ => new Color(0.94f, 0.94f, 0.94f, 1f)
                };

            markerRenderer.GetPropertyBlock(m_MarkerPropertyBlock);
            m_MarkerPropertyBlock.SetColor("_BaseColor", markerColor);
            m_MarkerPropertyBlock.SetColor("_Color", markerColor);
            markerRenderer.SetPropertyBlock(m_MarkerPropertyBlock);
        }

        Material ResolveMarkerMaterial(Renderer markerRenderer, ShopUpgradeDefinition definition)
        {
            if (markerRenderer == null || definition == null)
                return null;

            m_DefaultMarkerMaterial ??= markerRenderer.sharedMaterial;
            if (definition.IsRiskySingleRunTemporary)
            {
                if (m_RiskySingleRunMarkerMaterial == null)
                {
                    Material baseMaterial = m_DefaultMarkerMaterial != null
                        ? m_DefaultMarkerMaterial
                        : markerRenderer.sharedMaterial;

                    if (baseMaterial != null)
                    {
                        m_RiskySingleRunMarkerMaterial = new Material(baseMaterial)
                        {
                            name = "Runtime_ShopRiskySingleRunMarker"
                        };

                        if (m_RiskySingleRunMarkerMaterial.HasProperty("_Smoothness"))
                            m_RiskySingleRunMarkerMaterial.SetFloat("_Smoothness", 0.92f);

                        if (m_RiskySingleRunMarkerMaterial.HasProperty("_Metallic"))
                            m_RiskySingleRunMarkerMaterial.SetFloat("_Metallic", 0.22f);

                        if (m_RiskySingleRunMarkerMaterial.HasProperty("_EmissionColor"))
                        {
                            m_RiskySingleRunMarkerMaterial.EnableKeyword("_EMISSION");
                            m_RiskySingleRunMarkerMaterial.SetColor("_EmissionColor", new Color(0.62f, 0.08f, 0.04f, 1f));
                        }
                    }
                }

                return m_RiskySingleRunMarkerMaterial != null ? m_RiskySingleRunMarkerMaterial : m_DefaultMarkerMaterial;
            }

            if (!definition.IsMechanicChanging)
                return m_DefaultMarkerMaterial;

            if (m_MechanicMarkerMaterial == null)
            {
                Material baseMaterial = m_DefaultMarkerMaterial != null
                    ? m_DefaultMarkerMaterial
                    : markerRenderer.sharedMaterial;

                if (baseMaterial != null)
                {
                    m_MechanicMarkerMaterial = new Material(baseMaterial)
                    {
                        name = "Runtime_ShopMechanicMarker"
                    };

                    if (m_MechanicMarkerMaterial.HasProperty("_Smoothness"))
                        m_MechanicMarkerMaterial.SetFloat("_Smoothness", 0.95f);

                    if (m_MechanicMarkerMaterial.HasProperty("_Metallic"))
                        m_MechanicMarkerMaterial.SetFloat("_Metallic", 0.18f);

                    if (m_MechanicMarkerMaterial.HasProperty("_EmissionColor"))
                    {
                        m_MechanicMarkerMaterial.EnableKeyword("_EMISSION");
                        m_MechanicMarkerMaterial.SetColor("_EmissionColor", new Color(0.06f, 0.44f, 0.52f, 1f));
                    }
                }
            }

            return m_MechanicMarkerMaterial != null ? m_MechanicMarkerMaterial : m_DefaultMarkerMaterial;
        }

        Camera ResolveUiCamera()
        {
            if (m_PlayerRig != null && m_PlayerRig.Camera != null)
                return m_PlayerRig.Camera;

            return Camera.main;
        }

        static void TryAddTrackedDeviceGraphicRaycaster(GameObject target)
        {
            if (target == null)
                return;

            if (target.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                target.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        sealed class UpgradePanelView
        {
            public UpgradePanelView(
                int offerIndex,
                ShopUpgradeDefinition definition,
                TextMeshProUGUI titleLabel,
                TextMeshProUGUI descriptionLabel,
                TextMeshProUGUI priceLabel,
                TextMeshProUGUI statusLabel,
                Button buyButton,
                Transform markerTransform)
            {
                OfferIndex = offerIndex;
                Definition = definition;
                TitleLabel = titleLabel;
                DescriptionLabel = descriptionLabel;
                PriceLabel = priceLabel;
                StatusLabel = statusLabel;
                BuyButton = buyButton;
                MarkerTransform = markerTransform;
            }

            public int OfferIndex { get; }
            public ShopUpgradeDefinition Definition { get; }
            public TextMeshProUGUI TitleLabel { get; }
            public TextMeshProUGUI DescriptionLabel { get; }
            public TextMeshProUGUI PriceLabel { get; }
            public TextMeshProUGUI StatusLabel { get; }
            public Button BuyButton { get; }
            public Transform MarkerTransform { get; }
        }
    }
}
