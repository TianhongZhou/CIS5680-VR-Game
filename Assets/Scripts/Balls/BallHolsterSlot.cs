using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor))]
    public class BallHolsterSlot : MonoBehaviour
    {
        [SerializeField] string m_DisplayName = "Teleport Ball";
        [SerializeField] int m_StartingCount = -1;
        [SerializeField] GameObject m_ThrowableBallPrefab;
        [SerializeField] Transform m_SpawnPoint;
        [SerializeField] TMP_Text m_HoverInfoText;
        [SerializeField] bool m_SpawnOnStart = true;
        [SerializeField] GameObject[] m_HideWhenEmpty;

        UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor m_SocketInteractor;
        int m_RemainingCount;

        public string DisplayName => m_DisplayName;
        public bool IsInfinite => m_StartingCount < 0;
        public int RemainingCount => m_RemainingCount;

        void Awake()
        {
            m_SocketInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            m_RemainingCount = m_StartingCount;
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
        }

        public string BuildDisplayText()
        {
            var countText = IsInfinite ? "INF" : m_RemainingCount.ToString();
            return $"{m_DisplayName}\nCount: {countText}";
        }

        bool HasStock()
        {
            return IsInfinite || m_RemainingCount > 0;
        }

        void OnSocketSelectExited(SelectExitEventArgs _)
        {
            if (!IsInfinite)
                m_RemainingCount = Mathf.Max(0, m_RemainingCount - 1);

            RefreshVisualState();

            if (HasStock())
                StartCoroutine(SpawnNextFrame());
        }

        IEnumerator SpawnNextFrame()
        {
            yield return null;

            if (!m_SocketInteractor || m_SocketInteractor.hasSelection || !HasStock())
                yield break;

            SpawnBallInSocket();
        }

        void SpawnBallInSocket()
        {
            if (m_ThrowableBallPrefab == null)
                return;

            var spawnTransform = m_SpawnPoint != null ? m_SpawnPoint : transform;
            var spawned = Instantiate(m_ThrowableBallPrefab, spawnTransform.position, spawnTransform.rotation);

            var hoverInfo = spawned.GetComponent<BallHoverInfoDisplay>();
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

            for (var i = 0; i < m_HideWhenEmpty.Length; i++)
            {
                if (m_HideWhenEmpty[i] != null)
                    m_HideWhenEmpty[i].SetActive(show);
            }
        }
    }
}
