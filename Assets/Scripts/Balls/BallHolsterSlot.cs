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
        Coroutine m_SpawnCoroutine;

        public string DisplayName => m_DisplayName;
        public bool IsInfinite => m_StartingCount < 0;
        public int RemainingCount => m_RemainingCount;
        public int EnergyCost => m_ResolvedEnergyCost;
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
        }

        void OnDisable()
        {
            m_SocketInteractor.selectExited.RemoveListener(OnSocketSelectExited);

            if (m_SpawnCoroutine != null)
            {
                StopCoroutine(m_SpawnCoroutine);
                m_SpawnCoroutine = null;
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

            if (UsesEnergy)
            {
                // Energy is consumed when a non-socket interactor actually completes the grab.
            }
            else if (!IsInfinite)
            {
                m_RemainingCount = Mathf.Max(0, m_RemainingCount - 1);
            }

            RefreshVisualState();

            if (HasStock() && gameObject.activeInHierarchy)
                m_SpawnCoroutine = StartCoroutine(SpawnNextFrame());
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
                BallType.StickyPulse => 20,
                _ => 0,
            };
        }
    }
}
