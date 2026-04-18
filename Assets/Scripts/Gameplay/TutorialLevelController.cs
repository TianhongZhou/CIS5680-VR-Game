using CIS5680VRGame.Balls;
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
        enum TutorialStep
        {
            Move,
            SwitchMode,
            Sonar,
            Jump,
            Trap,
            Refill,
            Locator,
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
        [SerializeField] Vector2 m_MessagePanelSize = new(960f, 260f);
        [SerializeField] Vector2 m_MessagePanelOffset = new(0f, 240f);

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
        Coroutine m_SonarPulseCompletionRoutine;
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

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            EnsureMessageUI();
            ResolveTutorialObjects();
            ApplyInitialSceneState();
        }

        void OnEnable()
        {
            SonarPulseImpactEffect.PulseSpawned += OnSonarPulseSpawned;
            RefillStationLocatorGuidance.GuidancePingTriggered += OnLocatorPingTriggered;

            if (m_PlayerEnergy != null)
                m_PlayerEnergy.RefillStarted += OnEnergyRefillStarted;

            if (m_PlayerHealth != null)
                m_PlayerHealth.HealthChangedDetailed += OnHealthChanged;
        }

        void Start()
        {
            BeginStep(TutorialStep.Move);
        }

        void OnDisable()
        {
            SonarPulseImpactEffect.PulseSpawned -= OnSonarPulseSpawned;
            RefillStationLocatorGuidance.GuidancePingTriggered -= OnLocatorPingTriggered;

            CancelPendingSonarPulseCompletion();

            if (m_PlayerEnergy != null)
                m_PlayerEnergy.RefillStarted -= OnEnergyRefillStarted;

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

            CancelPendingSonarPulseCompletion();
        }

        void Update()
        {
            ResolveReferences();

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

        public void HandleZoneTriggered(string zoneId)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                return;

            switch (zoneId)
            {
                case Step01CompleteZone when m_CurrentStep == TutorialStep.Move:
                    CompleteCurrentStep();
                    break;

                case Step02MessageZone when m_CurrentStep == TutorialStep.SwitchMode:
                case Step03MessageZone when m_CurrentStep == TutorialStep.Sonar:
                case Step04MessageZone when m_CurrentStep == TutorialStep.Jump:
                case Step05MessageZone when m_CurrentStep == TutorialStep.Trap:
                case Step06MessageZone when m_CurrentStep == TutorialStep.Refill:
                case Step07MessageZone when m_CurrentStep == TutorialStep.Locator:
                    ShowCurrentStepMessage();
                    break;

                case Step04CompleteZone when m_CurrentStep == TutorialStep.Jump:
                case Step05CompleteZone when m_CurrentStep == TutorialStep.Trap:
                    CompleteCurrentStep();
                    break;
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

            SetObjectActive("Step01_Beacon", false);
            SetObjectActive("Step02_Beacon", false);
            SetObjectActive("Step03_Beacon", false);
            SetObjectActive("Step03_SonarTarget", false);
            SetObjectActive("Step04_Beacon", false);
            SetObjectActive("Step05_Beacon", false);
            SetObjectActive("Step06_Beacon", false);
            SetObjectActive("Step07_Beacon", false);

            SetObjectActive("Step02_Gate", true);
            SetObjectActive("Step03_Gate", true);
            SetObjectActive("Step06_Gate", true);
            SetObjectActive("Step07_Gate", true);

            if (m_TutorialGoal != null)
            {
                m_TutorialGoal.SetActive(true);
                SwitchTutorialGoalMode(useTutorialGoal: false);
            }

            UpdateTutorialEnergyRules();
        }

        void BeginStep(TutorialStep step)
        {
            if (step != TutorialStep.Sonar)
                CancelPendingSonarPulseCompletion();

            m_CurrentStep = step;
            m_HasShownCurrentMessage = false;
            HideMessage();
            UpdateBeaconVisibility(step);
            UpdateTutorialEnergyRules();

            if (step == TutorialStep.Move || step == TutorialStep.ReachGoal)
                ShowCurrentStepMessage();

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
                    SetObjectActive("Step03_Gate", false);
                    BeginStep(TutorialStep.Jump);
                    break;

                case TutorialStep.Jump:
                    BeginStep(TutorialStep.Trap);
                    break;

                case TutorialStep.Trap:
                    BeginStep(TutorialStep.Refill);
                    break;

                case TutorialStep.Refill:
                    SetObjectActive("Step06_Gate", false);
                    BeginStep(TutorialStep.Locator);
                    break;

                case TutorialStep.Locator:
                    SetObjectActive("Step07_Gate", false);
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
            SetObjectActive("Step03_Beacon", activeStep == TutorialStep.Sonar);
            SetObjectActive("Step04_Beacon", activeStep == TutorialStep.Jump);
            SetObjectActive("Step05_Beacon", activeStep == TutorialStep.Trap);
            SetObjectActive("Step06_Beacon", activeStep == TutorialStep.Refill);
            SetObjectActive("Step07_Beacon", activeStep == TutorialStep.Locator);
        }

        void ShowCurrentStepMessage()
        {
            if (m_HasShownCurrentMessage && m_CurrentStep != TutorialStep.ReachGoal)
                return;

            var content = GetStepContent(m_CurrentStep);
            ShowMessage(content.title, content.body);
            m_HasShownCurrentMessage = true;

            if (m_CurrentStep == TutorialStep.Sonar && !m_HasUnlockedNormalEnergyCosts)
            {
                m_HasUnlockedNormalEnergyCosts = true;
                UpdateTutorialEnergyRules();
            }
        }

        (string title, string body) GetStepContent(TutorialStep step)
        {
            return step switch
            {
                TutorialStep.Move => (
                    "Move Forward",
                    "Push the left thumbstick to move to the next marker."),

                TutorialStep.SwitchMode => (
                    "Switch Movement",
                    "Press X to swap movement modes. Try the alternate locomotion once to continue."),

                TutorialStep.Sonar => (
                    "Use the Sonar Ball",
                    "Grab a Sonar Ball and send its pulse wave into the glowing beacon to continue."),

                TutorialStep.Jump => (
                    "Jump Over Obstacles",
                    "Press A to jump. You can jump twice before landing. Clear the obstacle ahead."),

                TutorialStep.Trap => (
                    "Avoid the Trap",
                    "Use a Sonar Ball to reveal the trap floor, then cross on the safe side."),

                TutorialStep.Refill => (
                    "Recover Resources",
                    "Stand on the energy refill pad to restore energy, or use the grab button to pick up the health device and recover health."),

                TutorialStep.Locator => (
                    "Ping the Goal",
                    "Push the right thumbstick forward to scan for the refill station and the exit beacon."),

                TutorialStep.ReachGoal => (
                    "Tutorial Complete",
                    "Step into the exit beacon to begin the real maze."),

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

            m_MessageRoot = new GameObject("TutorialMessageCanvas");

            Canvas canvas = m_MessageRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = menuCamera;
            canvas.planeDistance = Mathf.Max(menuCamera.nearClipPlane + 0.16f, 0.34f);
            canvas.sortingOrder = 4200;

            CanvasScaler scaler = m_MessageRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panel = ModalMenuPauseUtility.CreateUIObject("Panel", m_MessageRoot.transform);
            m_MessagePanelRect = panel.GetComponent<RectTransform>();
            m_MessagePanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            m_MessagePanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            m_MessagePanelRect.pivot = new Vector2(0.5f, 0.5f);
            m_MessagePanelRect.sizeDelta = m_MessagePanelSize;
            m_MessagePanelRect.anchoredPosition = m_MessagePanelOffset;

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
                60f);

            m_MessageBody = CreateLabel(
                "Body",
                panel.transform,
                fontAsset,
                26f,
                FontStyles.Normal,
                new Color(0.74f, 0.88f, 0.98f, 1f),
                110f);
            m_MessageBody.enableWordWrapping = true;

            m_MessageRoot.SetActive(false);
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
            return label;
        }

        void SetObjectActive(string objectName, bool active)
        {
            if (!m_TutorialObjects.TryGetValue(objectName, out GameObject targetObject))
                return;

            if (targetObject != null)
                targetObject.SetActive(active);
        }

        void SwitchTutorialGoalMode(bool useTutorialGoal)
        {
            if (m_TutorialGoal == null)
                return;

            LevelGoalTrigger locatorGoal = m_TutorialGoal.GetComponent<LevelGoalTrigger>();
            if (locatorGoal != null)
                locatorGoal.enabled = false;

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

        void OnSonarPulseSpawned(Vector3 pulseOrigin, float pulseRadius, Collider sourceCollider)
        {
            if (m_CurrentStep != TutorialStep.Sonar || !m_HasShownCurrentMessage || m_SonarTargetVolume == null || m_SonarPulseCompletionRoutine != null)
                return;

            Vector3 closestPoint = m_SonarTargetVolume.ClosestPoint(pulseOrigin);
            float distanceToTarget = Vector3.Distance(pulseOrigin, closestPoint);
            if (distanceToTarget > pulseRadius + 0.05f)
                return;

            float pulseSpeed = PulseManager.Instance != null ? Mathf.Max(0.01f, PulseManager.Instance.pulseSpeed) : 8f;
            float travelDelay = Mathf.Max(0f, distanceToTarget / pulseSpeed);
            m_SonarPulseCompletionRoutine = StartCoroutine(CompleteSonarStepAfterDelay(travelDelay));
        }

        void OnEnergyRefillStarted()
        {
            if (m_CurrentStep == TutorialStep.Refill && m_HasShownCurrentMessage)
                CompleteCurrentStep();
        }

        void OnHealthChanged(HealthChangeContext context)
        {
            if (m_CurrentStep != TutorialStep.Refill || !m_HasShownCurrentMessage)
                return;

            if (context.Reason == HealthChangeReason.RefillStation && context.Delta > 0)
                CompleteCurrentStep();
        }

        void OnLocatorPingTriggered()
        {
            if (m_CurrentStep == TutorialStep.Locator && m_HasShownCurrentMessage)
                CompleteCurrentStep();
        }

        IEnumerator CompleteSonarStepAfterDelay(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            m_SonarPulseCompletionRoutine = null;

            if (m_CurrentStep != TutorialStep.Sonar || !m_HasShownCurrentMessage)
                yield break;

            CompleteCurrentStep();
        }

        void CancelPendingSonarPulseCompletion()
        {
            if (m_SonarPulseCompletionRoutine == null)
                return;

            StopCoroutine(m_SonarPulseCompletionRoutine);
            m_SonarPulseCompletionRoutine = null;
        }
    }
}
