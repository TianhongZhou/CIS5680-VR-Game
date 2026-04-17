using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
    public class BallHoverInfoDisplay : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField] TMP_Text m_Text;
        [SerializeField] string m_FallbackName = "Ball";

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable m_Interactable;
        BallHolsterSlot m_Provider;
        int m_ActiveVisibleHoverCount;
        bool m_FilterRegistered;
        bool m_HasConsumedEnergyForTake;
        IXRSelectInteractor m_ReservedSelectInteractor;
        float m_ReservationExpiresAt;
        float m_LastInsufficientHapticsTime = -999f;

        const float k_InsufficientHapticsCooldown = 0.15f;
        const float k_InsufficientHapticsAmplitude = 0.28f;
        const float k_InsufficientHapticsDuration = 0.08f;

        public bool canProcess => isActiveAndEnabled;

        void Awake()
        {
            m_Interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            m_ActiveVisibleHoverCount = 0;
            m_HasConsumedEnergyForTake = false;
            SetTextVisible(false);
        }

        void OnEnable()
        {
            m_Interactable.hoverEntered.AddListener(OnHoverEntered);
            m_Interactable.hoverExited.AddListener(OnHoverExited);
            m_Interactable.selectEntered.AddListener(OnSelectEntered);
            RegisterFilter();
        }

        void OnDisable()
        {
            m_Interactable.hoverEntered.RemoveListener(OnHoverEntered);
            m_Interactable.hoverExited.RemoveListener(OnHoverExited);
            m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
            UnregisterFilter();
            m_ActiveVisibleHoverCount = 0;
            m_ReservedSelectInteractor = null;
            m_ReservationExpiresAt = 0f;
            SetTextVisible(false);
        }

        public void SetProvider(BallHolsterSlot provider)
        {
            m_Provider = provider;
        }

        public void SetTextTarget(TMP_Text textTarget)
        {
            m_Text = textTarget;
            SetTextVisible(false);
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (!ShouldShowForInteractor(args.interactorObject))
                return;

            if (m_Text == null)
                return;

            m_ActiveVisibleHoverCount++;

            if (m_Provider != null)
                m_Text.text = m_Provider.BuildDisplayText();
            else
                m_Text.text = m_FallbackName;

            SetTextVisible(true);
        }

        void OnHoverExited(HoverExitEventArgs args)
        {
            if (!ShouldShowForInteractor(args.interactorObject))
                return;

            m_ActiveVisibleHoverCount = Mathf.Max(0, m_ActiveVisibleHoverCount - 1);
            if (m_ActiveVisibleHoverCount == 0)
                SetTextVisible(false);
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (args.interactorObject is not XRSocketInteractor && !m_HasConsumedEnergyForTake && m_Provider != null)
            {
                if (m_Provider.TryConsumeEnergyForTakenBall())
                {
                    m_HasConsumedEnergyForTake = true;
                }
                else
                {
                    m_Provider.SignalInsufficientEnergy();
                    TrySendInsufficientHaptics(args.interactorObject);
                    if (m_Interactable.interactionManager != null)
                        m_Interactable.interactionManager.SelectExit(args.interactorObject, m_Interactable);
                }
            }

            ClearReservation();
            m_ActiveVisibleHoverCount = 0;
            SetTextVisible(false);
        }

        public bool Process(IXRSelectInteractor interactor, UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable)
        {
            if (interactor is XRSocketInteractor)
                return true;

            if (m_HasConsumedEnergyForTake)
                return true;

            if (HasActiveReservation(interactor))
                return true;

            if (m_Provider == null || m_Provider.CanAffordEnergy())
            {
                ReserveForInteractor(interactor);
                return true;
            }

            m_Provider.SignalInsufficientEnergy();
            TrySendInsufficientHaptics(interactor);
            return false;
        }

        bool ShouldShowForInteractor(IXRHoverInteractor interactor)
        {
            return interactor is not XRSocketInteractor;
        }

        void SetTextVisible(bool visible)
        {
            if (m_Text != null)
                m_Text.enabled = visible;
        }

        bool HasActiveReservation(IXRSelectInteractor interactor)
        {
            if (!ReferenceEquals(m_ReservedSelectInteractor, interactor))
                return false;

            if (Time.unscaledTime <= m_ReservationExpiresAt)
                return true;

            ClearReservation();
            return false;
        }

        void ReserveForInteractor(IXRSelectInteractor interactor)
        {
            m_ReservedSelectInteractor = interactor;
            m_ReservationExpiresAt = Time.unscaledTime + 0.35f;
        }

        void ClearReservation()
        {
            m_ReservedSelectInteractor = null;
            m_ReservationExpiresAt = 0f;
        }

        void TrySendInsufficientHaptics(IXRInteractor interactor)
        {
            if (Time.unscaledTime - m_LastInsufficientHapticsTime < k_InsufficientHapticsCooldown)
                return;

            if (interactor is not XRBaseInputInteractor inputInteractor)
                return;

            if (inputInteractor.SendHapticImpulse(k_InsufficientHapticsAmplitude, k_InsufficientHapticsDuration))
                m_LastInsufficientHapticsTime = Time.unscaledTime;
        }

        void RegisterFilter()
        {
            if (m_FilterRegistered || m_Interactable == null)
                return;

            m_Interactable.selectFilters.Add(this);
            m_FilterRegistered = true;
        }

        void UnregisterFilter()
        {
            if (!m_FilterRegistered || m_Interactable == null)
                return;

            m_Interactable.selectFilters.Remove(this);
            m_FilterRegistered = false;
        }
    }
}
