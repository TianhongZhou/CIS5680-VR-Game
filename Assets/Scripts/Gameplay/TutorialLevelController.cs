using CIS5680VRGame.Balls;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CIS5680VRGame.Gameplay
{
    public class TutorialLevelController : MonoBehaviour
    {
        static readonly Vector2 k_MinMessagePanelSize = new(840f, 320f);
        static readonly Vector2 k_MessagePanelSafePadding = new(96f, 72f);
        const float k_TutorialLocatorRange = 30f;
        const float k_TutorialLocatorHalfAngle = 60f;

        enum TutorialStep
        {
            Move,
            SwitchMode,
            Sonar,
            StickyPulse,
            Teleport,
            Trap,
            EnergyRefill,
            HealthRefill,
            Locator,
            EnemyChase,
            ReachGoal,
            Completed,
        }

        [Header("Scene References")]
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] MovementModeManager m_MovementModeManager;
        [SerializeField] PlayerEnergy m_PlayerEnergy;
        [SerializeField] PlayerHealth m_PlayerHealth;
        [SerializeField] GameObject m_TutorialGoal;

        [Header("Tutorial UI")]
        [SerializeField] Vector2 m_MessagePanelSize = new(780f, 260f);
        [SerializeField] Vector3 m_MessageWorldOffset = new(0f, -0.42f, 2f);
        [SerializeField] Vector2 m_MessagePanelOffset = new(0f, 240f);

        [Header("Tutorial Safety")]
        [SerializeField] bool m_StartSlightlyBelowMaxHealth = true;
        [SerializeField, Min(1)] int m_HealthMissingForRefillLesson = 1;
        [SerializeField, Min(0f)] float m_HealthRefillFallbackCompletionDelay = 30f;

        [Header("Guide Pipeline Hint")]
        [SerializeField] bool m_CreateGuidePipelineHint = true;
        [SerializeField, Min(0f)] float m_GuidePipelineHintFloorSurfaceY = 0.04f;

        GameObject m_MessageRoot;
        RectTransform m_MessagePanelRect;
        TextMeshProUGUI m_MessageTitle;
        TextMeshProUGUI m_MessageBody;
        TutorialStep m_CurrentStep;
        bool m_HasShownCurrentMessage;
        bool m_HasUnlockedNormalEnergyCosts;
        bool m_InitialMovementModeResolved;
        MovementModeManager.MovementMode m_InitialMovementMode;
        Collider m_SonarTargetVolume;
        TeleportBallLauncher m_TeleportLauncher;
        TutorialGuidePipelineHint m_GuidePipelineHint;
        Coroutine m_DelayedStepCompletionRoutine;
        readonly Dictionary<string, GameObject> m_TutorialObjects = new();
        readonly List<BallHolsterSlot> m_TutorialBallSlots = new();

        const string Step01CompleteZone = "Step01_Move_Complete";
        const string Step02MessageZone = "Step02_Mode_Message";
        const string Step03MessageZone = "Step03_Sonar_Message";
        const string Step04MessageZone = "Step04_Jump_Message";
        const string Step04CompleteZone = "Step04_Jump_Complete";
        const string Step05MessageZone = "Step05_Trap_Message";
        const string Step05CompleteZone = "Step05_Trap_Complete";
        const string Step06MessageZone = "Step06_Refill_Message";
        const string Step07MessageZone = "Step07_Locator_Message";
        const string Step08EnemyMessageZone = "Step08_Enemy_Message";
        const string Step08EnemyCompleteZone = "Step08_Enemy_Complete";
        const string TutorialGoalScanBlocker = "TutorialGoalScanBlocker";

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            EnsureMessageUI();
            ResolveTutorialObjects();
            EnsureGuidePipelineHint();
            ApplyInitialSceneState();
        }

        void OnEnable()
        {
            SonarPulseImpactEffect.PulseSpawned += OnSonarPulseSpawned;
            StickyPulseImpactEffect.PulseSpawned += OnStickyPulseSpawned;
            RefillStationLocatorGuidance.GuidancePingTriggered += OnLocatorPingTriggered;

            if (m_PlayerEnergy != null)
                m_PlayerEnergy.RefillStarted += OnEnergyRefillStarted;

            if (m_PlayerHealth != null)
                m_PlayerHealth.HealthChangedDetailed += OnHealthChanged;
        }

        void Start()
        {
            EnsureHealthRefillLessonCanRestore();
            BeginStep(TutorialStep.Move);
        }

        void OnDisable()
        {
            SonarPulseImpactEffect.PulseSpawned -= OnSonarPulseSpawned;
            StickyPulseImpactEffect.PulseSpawned -= OnStickyPulseSpawned;
            RefillStationLocatorGuidance.GuidancePingTriggered -= OnLocatorPingTriggered;

            CancelPendingDelayedStepCompletion();

            if (m_PlayerEnergy != null)
            {
                m_PlayerEnergy.RefillStarted -= OnEnergyRefillStarted;
                m_PlayerEnergy.SetRegenSuppressed(false);
            }

            if (m_PlayerHealth != null)
                m_PlayerHealth.HealthChangedDetailed -= OnHealthChanged;
        }

        void OnDestroy()
        {
            if (m_MessageRoot == null)
                return;

            if (Application.isPlaying)
                Destroy(m_MessageRoot);
            else
                DestroyImmediate(m_MessageRoot);

            CancelPendingDelayedStepCompletion();
        }

        void Update()
        {
            ResolveReferences();
            UpdateTutorialAbilityAvailability();
            SynchronizeGateLocks();

            if (m_CurrentStep == TutorialStep.SwitchMode && m_HasShownCurrentMessage && m_MovementModeManager != null)
            {
                if (!m_InitialMovementModeResolved)
                {
                    m_InitialMovementModeResolved = true;
                    m_InitialMovementMode = m_MovementModeManager.CurrentMode;
                }
                else if (m_MovementModeManager.CurrentMode != m_InitialMovementMode)
                {
                    CompleteCurrentStep();
                }
            }
        }

        public void NotifyTutorialGoalReached()
        {
            if (m_CurrentStep == TutorialStep.ReachGoal)
            {
                CompleteCurrentStep();
                return;
            }

            m_HasShownCurrentMessage = false;
            HideMessage();
        }

        public bool HandleZoneTriggered(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                return false;

            switch (zoneId)
            {
                case Step01CompleteZone when m_CurrentStep == TutorialStep.Move:
                    CompleteCurrentStep();
                    return true;

                case Step02MessageZone when m_CurrentStep == TutorialStep.SwitchMode:
                case Step03MessageZone when m_CurrentStep == TutorialStep.Sonar:
                case Step04MessageZone when m_CurrentStep == TutorialStep.Teleport:
                case Step05MessageZone when m_CurrentStep == TutorialStep.Trap:
                case Step06MessageZone when m_CurrentStep == TutorialStep.EnergyRefill:
                case Step06MessageZone when m_CurrentStep == TutorialStep.HealthRefill:
                case Step07MessageZone when m_CurrentStep == TutorialStep.Locator:
                case Step08EnemyMessageZone when m_CurrentStep == TutorialStep.EnemyChase:
                    ShowCurrentStepMessage();
                    return true;

                case Step04CompleteZone when m_CurrentStep == TutorialStep.Teleport:
                case Step05CompleteZone when m_CurrentStep == TutorialStep.Trap:
                case Step08EnemyCompleteZone when m_CurrentStep == TutorialStep.EnemyChase && m_HasShownCurrentMessage:
                    CompleteCurrentStep();
                    return true;

                case Step08EnemyCompleteZone when m_CurrentStep == TutorialStep.EnemyChase:
                    ShowCurrentStepMessage();
                    return true;

                default:
                    return false;
            }
        }

        void ResolveReferences()
        {
            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_MovementModeManager == null)
                m_MovementModeManager = FindObjectOfType<MovementModeManager>();

            if (m_PlayerEnergy == null)
                m_PlayerEnergy = FindObjectOfType<PlayerEnergy>();

            if (m_PlayerHealth == null)
                m_PlayerHealth = FindObjectOfType<PlayerHealth>();

            if (m_TeleportLauncher == null)
                m_TeleportLauncher = FindObjectOfType<TeleportBallLauncher>();
        }

        void ResolveTutorialObjects()
        {
            m_TutorialObjects.Clear();
            m_TutorialBallSlots.Clear();

            Scene activeScene = SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                Transform[] sceneTransforms = rootObjects[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < sceneTransforms.Length; j++)
                {
                    Transform sceneTransform = sceneTransforms[j];
                    if (sceneTransform == null || m_TutorialObjects.ContainsKey(sceneTransform.name))
                        continue;

                    m_TutorialObjects.Add(sceneTransform.name, sceneTransform.gameObject);
                }

                BallHolsterSlot[] ballSlots = rootObjects[i].GetComponentsInChildren<BallHolsterSlot>(true);
                for (int j = 0; j < ballSlots.Length; j++)
                {
                    BallHolsterSlot ballSlot = ballSlots[j];
                    if (ballSlot != null && !m_TutorialBallSlots.Contains(ballSlot))
                        m_TutorialBallSlots.Add(ballSlot);
                }
            }

            if (m_TutorialGoal == null)
            {
                m_TutorialObjects.TryGetValue("TutorialGoalBeacon", out GameObject tutorialGoal);
                if (tutorialGoal != null)
                    m_TutorialGoal = tutorialGoal;
            }

            if (m_SonarTargetVolume == null)
            {
                m_TutorialObjects.TryGetValue("Step03_Beacon", out GameObject sonarBeacon);
                if (sonarBeacon != null)
                    m_SonarTargetVolume = sonarBeacon.GetComponent<Collider>();

                if (m_SonarTargetVolume == null)
                {
                    m_TutorialObjects.TryGetValue("Step03_SonarTarget", out GameObject sonarTarget);
                    if (sonarTarget != null)
                        m_SonarTargetVolume = sonarTarget.GetComponent<Collider>();
                }
            }
        }

        void ApplyInitialSceneState()
        {
            m_HasUnlockedNormalEnergyCosts = false;

            SetObjectActive("Step01_Beacon", false, immediateMarker: true);
            SetObjectActive("Step02_Beacon", false, immediateMarker: true);
            SetObjectActive("Step03_Beacon", false, immediateMarker: true);
            SetObjectActive("Step03_SonarTarget", false);
            SetObjectActive("Step04_Beacon", false, immediateMarker: true);
            SetObjectActive("Step04_TeleportBlocker", false);
            SetObjectActive("Step05_Beacon", false, immediateMarker: true);
            SetObjectActive("Step06_Beacon", false, immediateMarker: true);
            SetObjectActive("Step07_Beacon", false, immediateMarker: true);
            SetObjectActive("Step08_Beacon", false, immediateMarker: true);
            SetObjectActive("TutorialEnemyScout", false);
            SetObjectActive(TutorialGoalScanBlocker, true);

            SetObjectActive("Step02_Gate", true);
            SetObjectActive("Step03_Gate", true);
            SetObjectActive("Step06_Gate", true);
            SetObjectActive("Step07_Gate", true);
            SetBallSlotAvailability(BallType.Sonar, false);
            SetBallSlotAvailability(BallType.StickyPulse, false);
            SetBallSlotAvailability(BallType.Teleport, false);

            if (m_TutorialGoal != null)
            {
                m_TutorialGoal.SetActive(true);
                SwitchTutorialGoalMode(useTutorialGoal: false);
            }

            UpdateTutorialEnergyRules();
            UpdateTutorialAbilityAvailability();
        }

        void BeginStep(TutorialStep step)
        {
            CancelPendingDelayedStepCompletion();

            m_CurrentStep = step;
            m_HasShownCurrentMessage = false;
            HideMessage();
            UpdateBeaconVisibility(step);
            UpdateTutorialEnergyRules();
            UpdateTutorialAbilityAvailability();
            SynchronizeGateLocks();

            if (step == TutorialStep.Move
                || step == TutorialStep.StickyPulse
                || step == TutorialStep.HealthRefill
                || step == TutorialStep.ReachGoal)
            {
                ShowCurrentStepMessage();
            }

            if (step == TutorialStep.SwitchMode && m_MovementModeManager != null)
            {
                m_InitialMovementModeResolved = true;
                m_InitialMovementMode = m_MovementModeManager.CurrentMode;
            }
        }

        void CompleteCurrentStep()
        {
            switch (m_CurrentStep)
            {
                case TutorialStep.Move:
                    BeginStep(TutorialStep.SwitchMode);
                    break;

                case TutorialStep.SwitchMode:
                    SetObjectActive("Step02_Gate", false);
                    BeginStep(TutorialStep.Sonar);
                    break;

                case TutorialStep.Sonar:
                    BeginStep(TutorialStep.StickyPulse);
                    break;

                case TutorialStep.StickyPulse:
                    SetObjectActive("Step03_Gate", false);
                    SetObjectActive("Step04_TeleportBlocker", true);
                    BeginStep(TutorialStep.Teleport);
                    break;

                case TutorialStep.Teleport:
                    SetObjectActive("Step04_TeleportBlocker", false);
                    BeginStep(TutorialStep.Trap);
                    break;

                case TutorialStep.Trap:
                    BeginStep(TutorialStep.EnergyRefill);
                    break;

                case TutorialStep.EnergyRefill:
                    BeginStep(TutorialStep.HealthRefill);
                    break;

                case TutorialStep.HealthRefill:
                    SetObjectActive("Step06_Gate", false);
                    BeginStep(TutorialStep.Locator);
                    break;

                case TutorialStep.Locator:
                    SetObjectActive("Step07_Gate", false);
                    SetObjectActive(TutorialGoalScanBlocker, false);
                    BeginStep(TutorialStep.EnemyChase);
                    break;

                case TutorialStep.EnemyChase:
                    SetObjectActive("TutorialEnemyScout", false);
                    SwitchTutorialGoalMode(useTutorialGoal: true);
                    BeginStep(TutorialStep.ReachGoal);
                    break;

                case TutorialStep.ReachGoal:
                    m_CurrentStep = TutorialStep.Completed;
                    HideMessage();
                    UpdateBeaconVisibility(TutorialStep.Completed);
                    break;
            }
        }

        void UpdateBeaconVisibility(TutorialStep activeStep)
        {
            SetObjectActive("Step01_Beacon", activeStep == TutorialStep.Move);
            SetObjectActive("Step02_Beacon", activeStep == TutorialStep.SwitchMode);
            SetObjectActive("Step03_Beacon", activeStep == TutorialStep.Sonar || activeStep == TutorialStep.StickyPulse);
            SetObjectActive("Step04_Beacon", activeStep == TutorialStep.Teleport);
            SetObjectActive("Step05_Beacon", activeStep == TutorialStep.Trap);
            SetObjectActive("Step06_Beacon", activeStep == TutorialStep.EnergyRefill || activeStep == TutorialStep.HealthRefill);
            SetObjectActive("Step07_Beacon", activeStep == TutorialStep.Locator);
            SetObjectActive("Step08_Beacon", activeStep == TutorialStep.EnemyChase);
        }

        void ShowCurrentStepMessage()
        {
            if (m_HasShownCurrentMessage && m_CurrentStep != TutorialStep.ReachGoal)
                return;

            var content = GetStepContent(m_CurrentStep);
            ShowMessage(content.title, content.body);
            m_HasShownCurrentMessage = true;

            if (m_CurrentStep == TutorialStep.EnergyRefill && !m_HasUnlockedNormalEnergyCosts)
            {
                m_HasUnlockedNormalEnergyCosts = true;
                UpdateTutorialEnergyRules();
                UpdateTutorialAbilityAvailability();
            }

            if (m_CurrentStep == TutorialStep.HealthRefill
                && m_DelayedStepCompletionRoutine == null
                && m_HealthRefillFallbackCompletionDelay > 0f)
            {
                m_DelayedStepCompletionRoutine = StartCoroutine(CompleteStepAfterDelay(
                    TutorialStep.HealthRefill,
                    m_HealthRefillFallbackCompletionDelay));
            }

            if (m_CurrentStep == TutorialStep.EnemyChase)
                ActivateTutorialEnemyScout();
        }

        (string title, string body) GetStepContent(TutorialStep step)
        {
            return step switch
            {
                TutorialStep.Move => (
                    "Move Forward",
                    "Push the left thumbstick to move.\nFollow the marker."),

                TutorialStep.SwitchMode => (
                    "Switch Movement",
                    "Press X once to swap movement modes.\nTry the alternate locomotion once to continue."),

                TutorialStep.Sonar => (
                    "Use the Sonar Ball",
                    "A Sonar Ball reveals one burst of hidden space.\nGrab one and send its pulse wave into the glowing beacon."),

                TutorialStep.StickyPulse => (
                    "Use the Sticky Pulse Ball",
                    "A Sticky Pulse Ball keeps revealing an area for longer.\nStick it near a place you will pass more than once, then move through while it keeps pulsing."),

                TutorialStep.Teleport => (
                    "Teleport Movement",
                    "Hold A to preview a teleport ball arc.\nRelease A to fire it, then press A again to teleport or press B to cancel. Clear the obstacle ahead."),

                TutorialStep.Trap => (
                    "Avoid the Trap",
                    "Use a Sonar Ball to reveal the trap floor,\nthen cross on the safe side."),

                TutorialStep.EnergyRefill => (
                    "Manage Energy",
                    "From now on, throwing balls uses energy.\nCheck the right side of your right controller for your energy gauge.\nThrow a ball, then step onto the energy refill pad and watch it recover."),

                TutorialStep.HealthRefill => (
                    "Manage Health",
                    "Low health slowly regenerates up to a cap.\nCheck the left side of your left controller for your health gauge.\nPoint your hand at the health station and press Grip to restore more at once."),

                TutorialStep.Locator => (
                    "Find the Exit Direction",
                    "Stop at the fork and listen for the exit ambience.\nFaint blue conduits can hint at the exit direction, but they may pass through walls.\nTurn toward the sound, then push the right thumbstick forward to scan up to 30m."),

                TutorialStep.EnemyChase => (
                    "Enemy Alert",
                    "A scout is chasing you.\nKeep moving or teleport away, then reach the next marker."),

                TutorialStep.ReachGoal => (
                    "Tutorial Complete",
                    "You can collect coins in the maze and spend them in the shop.\nStep into the exit beacon to continue to the shop tutorial."),

                _ => (string.Empty, string.Empty),
            };
        }

        void ShowMessage(string title, string body)
        {
            EnsureMessageUI();
            if (m_MessageRoot == null || m_MessageTitle == null || m_MessageBody == null)
                return;

            m_MessageTitle.text = title;
            m_MessageBody.text = body;
            Camera menuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
            ModalMenuPauseUtility.RefreshWorldMenuPose(m_MessageRoot, menuCamera, m_MessageWorldOffset);
            m_MessageRoot.SetActive(true);
            ModalMenuPauseUtility.RefreshMenuLayout(m_MessageRoot, m_MessagePanelRect);
        }

        void HideMessage()
        {
            if (m_MessageRoot != null)
                m_MessageRoot.SetActive(false);
        }

        void EnsureMessageUI()
        {
            if (m_MessageRoot != null)
                return;

            Camera menuCamera = ModalMenuPauseUtility.ResolveMenuCamera(m_PlayerRig);
            if (menuCamera == null)
                return;

            m_MessageRoot = ModalMenuPauseUtility.CreateWorldSpaceMenuRoot(
                "TutorialMessageCanvas",
                menuCamera,
                ResolveMessagePanelSize(),
                Color.clear,
                out m_MessagePanelRect,
                m_MessageWorldOffset);

            Canvas canvas = m_MessageRoot.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = 4200;

            GameObject panel = m_MessagePanelRect.gameObject;

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.02f, 0.04f, 0.07f, 0.9f);
            panelImage.raycastTarget = false;

            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(40, 40, 28, 28);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            TMP_FontAsset fontAsset = ModalMenuPauseUtility.ResolveFontAsset();

            m_MessageTitle = CreateLabel(
                "Title",
                panel.transform,
                fontAsset,
                42f,
                FontStyles.Bold,
                new Color(0.92f, 0.98f, 1f, 1f),
                58f);

            m_MessageBody = CreateLabel(
                "Body",
                panel.transform,
                fontAsset,
                22f,
                FontStyles.Normal,
                new Color(0.74f, 0.88f, 0.98f, 1f),
                188f);
            m_MessageBody.enableWordWrapping = true;

            m_MessageRoot.SetActive(false);
        }

        Vector2 ResolveMessagePanelSize()
        {
            Vector2 preferredSize = new(
                Mathf.Max(m_MessagePanelSize.x, k_MinMessagePanelSize.x),
                Mathf.Max(m_MessagePanelSize.y, k_MinMessagePanelSize.y));
            return ModalMenuPauseUtility.ClampPanelSize(
                preferredSize,
                k_MessagePanelSafePadding,
                k_MinMessagePanelSize);
        }

        TextMeshProUGUI CreateLabel(
            string name,
            Transform parent,
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
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMax = fontSize;
            label.fontSizeMin = Mathf.Max(16f, fontSize * 0.65f);
            return label;
        }

        void SetObjectActive(string objectName, bool active, bool immediateMarker = false)
        {
            if (!m_TutorialObjects.TryGetValue(objectName, out GameObject targetObject))
                return;

            if (targetObject == null)
                return;

            if (TrySetTutorialWaypointActive(objectName, targetObject, active, immediateMarker))
                return;

            targetObject.SetActive(active);
        }

        bool TrySetTutorialWaypointActive(string objectName, GameObject targetObject, bool active, bool immediateMarker)
        {
            if (!IsTutorialWaypointBeaconName(objectName))
                return false;

            TutorialWaypointMarkerVisual markerVisual = targetObject.GetComponent<TutorialWaypointMarkerVisual>();
            if (markerVisual == null)
                markerVisual = targetObject.AddComponent<TutorialWaypointMarkerVisual>();

            markerVisual.SetVisible(active, immediateMarker);
            return true;
        }

        static bool IsTutorialWaypointBeaconName(string objectName)
        {
            return objectName.StartsWith("Step", StringComparison.Ordinal)
                && objectName.EndsWith("_Beacon", StringComparison.Ordinal);
        }

        void ActivateTutorialEnemyScout()
        {
            if (!m_TutorialObjects.TryGetValue("TutorialEnemyScout", out GameObject enemyObject) || enemyObject == null)
                return;

            Vector3 targetPosition = ResolvePlayerAnchorPosition();
            EnemyPatrolController enemyController = enemyObject.GetComponent<EnemyPatrolController>();

            if (enemyController != null)
                enemyController.FaceToward(targetPosition);
            else
                FaceTransformToward(enemyObject.transform, targetPosition);

            enemyObject.SetActive(true);

            if (enemyController == null)
                enemyController = enemyObject.GetComponent<EnemyPatrolController>();

            if (enemyController == null)
                return;

            enemyController.SetRoamCenter(enemyObject.transform.position);
            enemyController.SetDetectionRangeMultiplier(4f);
            enemyController.SetPursuitLockDistanceMultiplier(4f);
            enemyController.SetFieldOfViewMultiplier(4f);
            enemyController.ForceChaseTarget(targetPosition);
        }

        void EnsureGuidePipelineHint()
        {
            if (!m_CreateGuidePipelineHint)
                return;

            const string guidePipelineHintName = "TutorialGuidePipelineHint";
            GameObject hintObject = GameObject.Find(guidePipelineHintName);
            if (hintObject == null)
            {
                hintObject = new GameObject(guidePipelineHintName);
                hintObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                hintObject.transform.localScale = Vector3.one;
            }

            m_GuidePipelineHint = hintObject.GetComponent<TutorialGuidePipelineHint>();
            if (m_GuidePipelineHint == null)
                m_GuidePipelineHint = hintObject.AddComponent<TutorialGuidePipelineHint>();

            m_GuidePipelineHint.Configure(BuildGuidePipelineHintPath(), m_GuidePipelineHintFloorSurfaceY);
        }

        List<Vector3> BuildGuidePipelineHintPath()
        {
            Vector3 locatorPosition = ResolveTutorialObjectPosition("Step07_Beacon", new Vector3(0.56f, 0f, 24.75f));
            Vector3 enemyMessagePosition = ResolveTutorialObjectPosition("Step08_Enemy_Message", new Vector3(5f, 0f, 25.4f));
            Vector3 goalPosition = m_TutorialGoal != null ? m_TutorialGoal.transform.position : new Vector3(14f, 0f, 25f);

            float startX = locatorPosition.x + 0.75f;
            float bendX = Mathf.Min(goalPosition.x - 6f, Mathf.Max(startX + 2.4f, enemyMessagePosition.x));
            float endX = Mathf.Clamp(goalPosition.x - 2.5f, bendX + 3f, goalPosition.x - 1.25f);
            float nearWallZ = locatorPosition.z - 0.65f;
            float goalSideZ = goalPosition.z - 0.45f;
            float bendZ = Mathf.Lerp(nearWallZ, goalSideZ, 0.7f);

            return new List<Vector3>
            {
                new(startX, 0f, nearWallZ),
                new(bendX, 0f, nearWallZ),
                new(bendX, 0f, bendZ),
                new(endX, 0f, bendZ),
            };
        }

        Vector3 ResolveTutorialObjectPosition(string objectName, Vector3 fallback)
        {
            if (!string.IsNullOrWhiteSpace(objectName)
                && m_TutorialObjects.TryGetValue(objectName, out GameObject targetObject)
                && targetObject != null)
            {
                return targetObject.transform.position;
            }

            return fallback;
        }

        Vector3 ResolvePlayerAnchorPosition()
        {
            if (m_PlayerRig != null)
                return m_PlayerRig.transform.position;

            Transform viewTransform = ResolveViewTransform();
            return viewTransform != null ? viewTransform.position : transform.position;
        }

        void FaceTransformToward(Transform source, Vector3 targetPosition)
        {
            if (source == null)
                return;

            Vector3 lookDirection = Vector3.ProjectOnPlane(targetPosition - source.position, Vector3.up);
            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            source.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        void SynchronizeGateLocks()
        {
            SetObjectActive("Step02_Gate", m_CurrentStep <= TutorialStep.SwitchMode);
            SetObjectActive("Step03_Gate", m_CurrentStep <= TutorialStep.StickyPulse);
            SetObjectActive("Step04_TeleportBlocker", m_CurrentStep == TutorialStep.Teleport);
            SetObjectActive("Step06_Gate", m_CurrentStep <= TutorialStep.HealthRefill);
            SetObjectActive("Step07_Gate", m_CurrentStep <= TutorialStep.Locator);
            SetObjectActive(TutorialGoalScanBlocker, m_CurrentStep <= TutorialStep.Locator);
        }

        void SwitchTutorialGoalMode(bool useTutorialGoal)
        {
            if (m_TutorialGoal == null)
                return;

            TutorialCompletionGoal tutorialGoal = m_TutorialGoal.GetComponent<TutorialCompletionGoal>();
            if (tutorialGoal != null)
                tutorialGoal.enabled = useTutorialGoal;
        }

        void UpdateTutorialEnergyRules()
        {
            bool shouldWaiveEnergyCosts = !m_HasUnlockedNormalEnergyCosts;

            for (int i = 0; i < m_TutorialBallSlots.Count; i++)
            {
                BallHolsterSlot ballSlot = m_TutorialBallSlots[i];
                if (ballSlot == null || ballSlot.DefaultEnergyCost <= 0)
                    continue;

                ballSlot.SetTemporaryEnergyCostOverride(shouldWaiveEnergyCosts ? (int?)0 : null);
            }
        }

        void UpdateTutorialAbilityAvailability()
        {
            SetBallSlotAvailability(BallType.Sonar, IsStepUnlocked(TutorialStep.Sonar));
            SetBallSlotAvailability(BallType.StickyPulse, IsStepUnlocked(TutorialStep.StickyPulse));
            SetBallSlotAvailability(BallType.Teleport, false);

            if (m_TeleportLauncher != null)
                m_TeleportLauncher.enabled = IsStepUnlocked(TutorialStep.Teleport);

            if (m_PlayerEnergy != null)
                m_PlayerEnergy.SetRegenSuppressed(!m_HasUnlockedNormalEnergyCosts);
        }

        void EnsureHealthRefillLessonCanRestore()
        {
            if (!m_StartSlightlyBelowMaxHealth || m_PlayerHealth == null)
                return;

            int missingHealth = Mathf.Max(1, m_HealthMissingForRefillLesson);
            int maxHealth = m_PlayerHealth.MaxHealth;
            if (maxHealth <= missingHealth || m_PlayerHealth.CurrentHealth < maxHealth)
                return;

            m_PlayerHealth.ApplyDamage(missingHealth, HealthChangeReason.DirectSet);
        }

        bool IsStepUnlocked(TutorialStep step)
        {
            return m_CurrentStep != TutorialStep.Completed && m_CurrentStep >= step;
        }

        void SetBallSlotAvailability(BallType ballType, bool available)
        {
            for (int i = 0; i < m_TutorialBallSlots.Count; i++)
            {
                BallHolsterSlot ballSlot = m_TutorialBallSlots[i];
                if (ballSlot == null || ballSlot.BallType != ballType)
                    continue;

                GameObject slotObject = ballSlot.gameObject;
                if (slotObject != null && slotObject.activeSelf != available)
                    slotObject.SetActive(available);
            }
        }

        void OnSonarPulseSpawned(Vector3 pulseOrigin, float pulseRadius, Collider sourceCollider)
        {
            if (!m_HasShownCurrentMessage || m_DelayedStepCompletionRoutine != null)
                return;

            if (m_CurrentStep != TutorialStep.Sonar || m_SonarTargetVolume == null)
                return;

            Vector3 closestPoint = m_SonarTargetVolume.ClosestPoint(pulseOrigin);
            float distanceToTarget = Vector3.Distance(pulseOrigin, closestPoint);
            if (distanceToTarget > pulseRadius + 0.05f)
                return;

            float pulseSpeed = PulseManager.Instance != null ? Mathf.Max(0.01f, PulseManager.Instance.pulseSpeed) : 8f;
            float travelDelay = Mathf.Max(0f, distanceToTarget / pulseSpeed);
            m_DelayedStepCompletionRoutine = StartCoroutine(CompleteStepAfterDelay(TutorialStep.Sonar, travelDelay));
        }

        void OnStickyPulseSpawned(Vector3 pulseOrigin, float pulseRadius, Collider sourceCollider)
        {
            if (m_CurrentStep != TutorialStep.StickyPulse || !m_HasShownCurrentMessage || m_DelayedStepCompletionRoutine != null)
                return;

            m_DelayedStepCompletionRoutine = StartCoroutine(CompleteStepAfterDelay(TutorialStep.StickyPulse, 1f));
        }

        void OnEnergyRefillStarted()
        {
            if (m_CurrentStep == TutorialStep.EnergyRefill && m_HasShownCurrentMessage)
                CompleteCurrentStep();
        }

        void OnHealthChanged(HealthChangeContext context)
        {
            if (m_CurrentStep != TutorialStep.HealthRefill || !m_HasShownCurrentMessage)
                return;

            if (context.Reason == HealthChangeReason.RefillStation && context.Delta > 0)
                CompleteCurrentStep();
        }

        void OnLocatorPingTriggered()
        {
            if (m_CurrentStep == TutorialStep.Locator
                && m_HasShownCurrentMessage
                && IsTutorialGoalInsideInitialLocatorScan())
            {
                CompleteCurrentStep();
            }
        }

        bool IsTutorialGoalInsideInitialLocatorScan()
        {
            if (m_TutorialGoal == null)
                return true;

            Transform viewTransform = ResolveViewTransform();
            if (viewTransform == null)
                return true;

            Vector3 toGoal = Vector3.ProjectOnPlane(m_TutorialGoal.transform.position - viewTransform.position, Vector3.up);
            if (toGoal.sqrMagnitude < 0.0001f)
                return true;

            if (toGoal.sqrMagnitude > k_TutorialLocatorRange * k_TutorialLocatorRange)
                return false;

            Vector3 forward = Vector3.ProjectOnPlane(viewTransform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            return Vector3.Angle(forward.normalized, toGoal.normalized) <= k_TutorialLocatorHalfAngle;
        }

        Transform ResolveViewTransform()
        {
            if (m_PlayerRig != null && m_PlayerRig.Camera != null)
                return m_PlayerRig.Camera.transform;

            if (Camera.main != null)
                return Camera.main.transform;

            return m_PlayerRig != null ? m_PlayerRig.transform : transform;
        }

        IEnumerator CompleteStepAfterDelay(TutorialStep expectedStep, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            m_DelayedStepCompletionRoutine = null;

            if (m_CurrentStep != expectedStep || !m_HasShownCurrentMessage)
                yield break;

            CompleteCurrentStep();
        }

        void CancelPendingDelayedStepCompletion()
        {
            if (m_DelayedStepCompletionRoutine == null)
                return;

            StopCoroutine(m_DelayedStepCompletionRoutine);
            m_DelayedStepCompletionRoutine = null;
        }
    }
}
