using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
    public class BallHoverInfoDisplay : MonoBehaviour
    {
        [SerializeField] TMP_Text m_Text;
        [SerializeField] string m_FallbackName = "Ball";

        UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable m_Interactable;
        BallHolsterSlot m_Provider;

        void Awake()
        {
            m_Interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            SetTextVisible(false);
        }

        void OnEnable()
        {
            m_Interactable.hoverEntered.AddListener(OnHoverEntered);
            m_Interactable.hoverExited.AddListener(OnHoverExited);
            m_Interactable.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDisable()
        {
            m_Interactable.hoverEntered.RemoveListener(OnHoverEntered);
            m_Interactable.hoverExited.RemoveListener(OnHoverExited);
            m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
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

        void OnHoverEntered(HoverEnterEventArgs _)
        {
            if (m_Text == null)
                return;

            if (m_Provider != null)
                m_Text.text = m_Provider.BuildDisplayText();
            else
                m_Text.text = m_FallbackName;

            SetTextVisible(true);
        }

        void OnHoverExited(HoverExitEventArgs _)
        {
            SetTextVisible(false);
        }

        void OnSelectEntered(SelectEnterEventArgs _)
        {
            SetTextVisible(false);
        }

        void SetTextVisible(bool visible)
        {
            if (m_Text != null)
                m_Text.enabled = visible;
        }
    }
}
