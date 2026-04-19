using System.Collections;
using CIS5680VRGame.Gameplay;
using CIS5680VRGame.UI;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(SafeXRSocketInteractor))]
    public class BallHolsterSlot : MonoBehaviour
    {
        [SerializeField] string m_DisplayName = "Teleport Ball";
        [SerializeField] int m_StartingCount = -1;
        [SerializeField] int m_EnergyCostOverride = -1;
        [SerializeField] GameObject m_ThrowableBallPrefab;
        [SerializeField] Transform m_SpawnPoint;
        [SerializeField] TMP_Text m_HoverInfoText;
        [SerializeField] bool m_SpawnOnStart = true;
        [SerializeField] GameObject[] m_HideWhenEmpty;

        UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor m_SocketInteractor;
        PlayerEnergy m_PlayerEnergy;
        int m_RemainingCount;
        int m_ResolvedEnergyCost;
        bool m_HasResolvedEnergyCost;
        bool m_HasTemporaryEnergyCostOverride;
        int m_TemporaryEnergyCostOverride;
        Coroutine m_SpawnCoroutine;
        Coroutine m_StartupCleanupCoroutine;

        public string DisplayName => m_DisplayName;
        public bool IsInfinite => m_StartingCount < 0;
        public int RemainingCount => m_RemainingCount;
        public int DefaultEnergyCost => GetResolvedEnergyCost();
        public int EnergyCost => m_HasTemporaryEnergyCostOverride ? m_TemporaryEnergyCostOverride : GetResolvedEnergyCost();
        public bool UsesEnergy => m_PlayerEnergy != null;
        public bool IsFull => UsesEnergy
            ? m_PlayerEnergy.CurrentEnergy >= m_PlayerEnergy.MaxEnergy
            : IsInfinite || m_RemainingCount >= m_StartingCount;

        void Awake()
        {
            m_SocketInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            m_PlayerEnergy = FindObjectOfType<PlayerEnergy>();
            m_RemainingCount = m_StartingCount;
            m_ResolvedEnergyCost = ResolveEnergyCost();
            m_HasResolvedEnergyCost = true;

            if (m_HoverInfoText != null)
                m_HoverInfoText.enabled = false;

            RefreshVisualState();
        }

        void OnEnable()
        {
            m_SocketInteractor.selectExited.AddListener(OnSocketSelectExited);
        }

        void Start()
        {
            if (m_SpawnOnStart && HasStock() && !m_SocketInteractor.hasSelection)
                SpawnBallInSocket();

            if (m_SpawnOnStart)
                RestartStartupCleanup();
        }

        void OnDisable()
        {
            m_SocketInteractor.selectExited.RemoveListener(OnSocketSelectExited);

            if (m_SpawnCoroutine != null)
            {
                StopCoroutine(m_SpawnCoroutine);
                m_SpawnCoroutine = null;
            }

            if (m_StartupCleanupCoroutine != null)
            {
                StopCoroutine(m_StartupCleanupCoroutine);
                m_StartupCleanupCoroutine = null;
            }
        }

        public string BuildDisplayText()
        {
            if (UsesEnergy)
            {
                string costText = EnergyCost <= 0 ? "FREE" : $"{EnergyCost} EN";
                return $"{m_DisplayName}\nCost: {costText}";
            }

            string countText = IsInfinite ? "INF" : m_RemainingCount.ToString();
            return $"{m_DisplayName}\nCount: {countText}";
        }

        public bool RefillToMax()
        {
            if (UsesEnergy)
                return m_PlayerEnergy.RefillToMax();

            if (IsInfinite || m_StartingCount <= 0)
                return false;

            int previousCount = m_RemainingCount;
            m_RemainingCount = m_StartingCount;
            RefreshVisualState();

            if (isActiveAndEnabled && gameObject.activeInHierarchy && m_SpawnCoroutine == null && !m_SocketInteractor.hasSelection && HasStock())
                m_SpawnCoroutine = StartCoroutine(SpawnNextFrame());

            return m_RemainingCount != previousCount;
        }

        public bool CanAffordEnergy()
        {
            return !UsesEnergy || m_PlayerEnergy.CanAfford(EnergyCost);
        }

        public void SignalInsufficientEnergy()
        {
            if (!UsesEnergy)
                return;

            m_PlayerEnergy.SignalInsufficient(EnergyCost);
        }

        public bool TryConsumeEnergyForTakenBall()
        {
            return !UsesEnergy || m_PlayerEnergy.TryConsume(EnergyCost);
        }

        public void SetTemporaryEnergyCostOverride(int? energyCost)
        {
            if (energyCost.HasValue)
            {
                m_HasTemporaryEnergyCostOverride = true;
                m_TemporaryEnergyCostOverride = Mathf.Max(0, energyCost.Value);
                return;
            }

            m_HasTemporaryEnergyCostOverride = false;
            m_TemporaryEnergyCostOverride = 0;
        }

        bool HasStock()
        {
            if (UsesEnergy)
                return true;

            return IsInfinite || m_RemainingCount > 0;
        }

        void OnSocketSelectExited(SelectExitEventArgs args)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy || m_SocketInteractor == null)
                return;

            if (args.isCanceled)
                return;

            if (!WasTransferredToAnotherInteractor(args))
            {
                DestroyUnexpectedReleasedBall(args);
                QueueRespawnIfNeeded();
                return;
            }

            if (UsesEnergy)
            {
                // Energy is consumed when a non-socket interactor actually completes the grab.
            }
            else if (!IsInfinite)
            {
                m_RemainingCount = Mathf.Max(0, m_RemainingCount - 1);
            }

            RefreshVisualState();

            QueueRespawnIfNeeded();
        }

        IEnumerator SpawnNextFrame()
        {
            yield return null;

            m_SpawnCoroutine = null;

            if (!isActiveAndEnabled || !gameObject.activeInHierarchy || !m_SocketInteractor || m_SocketInteractor.hasSelection || !HasStock())
                yield break;

            SpawnBallInSocket();
        }

        void SpawnBallInSocket()
        {
            if (m_ThrowableBallPrefab == null)
                return;

            YawOnlyFollow follower = GetComponent<YawOnlyFollow>();
            if (follower != null)
            {
                follower.SnapToTarget();
                Physics.SyncTransforms();
            }

            Transform spawnTransform = m_SpawnPoint != null ? m_SpawnPoint : transform;
            GameObject spawned = Instantiate(m_ThrowableBallPrefab, spawnTransform.position, spawnTransform.rotation);

            BallHoverInfoDisplay hoverInfo = spawned.GetComponent<BallHoverInfoDisplay>();
            if (hoverInfo != null)
            {
                hoverInfo.SetProvider(this);
                if (m_HoverInfoText != null)
                    hoverInfo.SetTextTarget(m_HoverInfoText);
            }

            if (!spawned.TryGetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(out var grabInteractable))
                return;

            m_SocketInteractor.StartManualInteraction((UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)grabInteractable);
        }

        bool WasTransferredToAnotherInteractor(SelectExitEventArgs args)
        {
            var interactable = args.interactableObject;
            if (interactable == null || !interactable.isSelected)
                return false;

            var selectors = interactable.interactorsSelecting;
            for (int i = 0; i < selectors.Count; i++)
            {
                if (selectors[i] == null || selectors[i] == m_SocketInteractor)
                    continue;

                return true;
            }

            return false;
        }

        void DestroyUnexpectedReleasedBall(SelectExitEventArgs args)
        {
            var interactable = args.interactableObject;
            if (interactable?.transform == null)
                return;

            Destroy(interactable.transform.gameObject);
        }

        void QueueRespawnIfNeeded()
        {
            RefreshVisualState();

            if (!HasStock() || !gameObject.activeInHierarchy)
                return;

            if (m_SpawnCoroutine != null)
                StopCoroutine(m_SpawnCoroutine);

            m_SpawnCoroutine = StartCoroutine(SpawnNextFrame());
        }

        void RestartStartupCleanup()
        {
            if (m_StartupCleanupCoroutine != null)
                StopCoroutine(m_StartupCleanupCoroutine);

            m_StartupCleanupCoroutine = StartCoroutine(CleanupStartupDuplicates());
        }

        IEnumerator CleanupStartupDuplicates()
        {
            const int cleanupPasses = 6;
            string cloneName = m_ThrowableBallPrefab != null ? $"{m_ThrowableBallPrefab.name}(Clone)" : string.Empty;

            for (int pass = 0; pass < cleanupPasses; pass++)
            {
                yield return null;

                if (!isActiveAndEnabled || !gameObject.activeInHierarchy || m_SocketInteractor == null || !m_SocketInteractor.hasSelection)
                    continue;

                if (string.IsNullOrEmpty(cloneName))
                    continue;

                CleanupLooseClones(cloneName);
            }

            m_StartupCleanupCoroutine = null;
        }

        void CleanupLooseClones(string cloneName)
        {
            var selectedInteractables = m_SocketInteractor.interactablesSelected;
            var heldInteractable = selectedInteractables.Count > 0 ? selectedInteractables[0] : null;
            var grabInteractables = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true);

            for (int i = 0; i < grabInteractables.Length; i++)
            {
                var grabInteractable = grabInteractables[i];
                if (grabInteractable == null || grabInteractable.name != cloneName)
                    continue;

                if (ReferenceEquals(grabInteractable, heldInteractable) || grabInteractable.isSelected)
                    continue;

                Destroy(grabInteractable.gameObject);
            }
        }

        void RefreshVisualState()
        {
            bool show = HasStock();
            if (m_HideWhenEmpty == null)
                return;

            for (int i = 0; i < m_HideWhenEmpty.Length; i++)
            {
                if (m_HideWhenEmpty[i] != null)
                    m_HideWhenEmpty[i].SetActive(show);
            }
        }

        int ResolveEnergyCost()
        {
            if (m_EnergyCostOverride >= 0)
                return m_EnergyCostOverride;

            if (m_ThrowableBallPrefab == null || !m_ThrowableBallPrefab.TryGetComponent<BallImpactEffect>(out var effect))
                return 0;

            return effect.BallType switch
            {
                BallType.Sonar => 10,
                BallType.StickyPulse => 25,
                _ => 0,
            };
        }

        int GetResolvedEnergyCost()
        {
            if (!m_HasResolvedEnergyCost)
            {
                m_ResolvedEnergyCost = ResolveEnergyCost();
                m_HasResolvedEnergyCost = true;
            }

            return m_ResolvedEnergyCost;
        }
    }
}
