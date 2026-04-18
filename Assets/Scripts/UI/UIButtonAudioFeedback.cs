using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace CIS5680VRGame.UI
{
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonAudioFeedback : MonoBehaviour, IPointerEnterHandler
    {
        const float HoverHapticsAmplitude = 0.12f;
        const float HoverHapticsDuration = 0.045f;
        const float HoverHapticsCooldown = 0.08f;

        static float s_LastHoverHapticsTime = -999f;

        Button m_Button;
        UIButtonSoundStyle m_ClickSoundStyle = UIButtonSoundStyle.Normal;

        public static void Attach(Button button, UIButtonSoundStyle clickSoundStyle = UIButtonSoundStyle.Normal)
        {
            if (button == null)
                return;

            UIAudioService.EnsureCreated();

            UIButtonAudioFeedback feedback = button.GetComponent<UIButtonAudioFeedback>();
            if (feedback == null)
                feedback = button.gameObject.AddComponent<UIButtonAudioFeedback>();

            feedback.SetClickSoundStyle(clickSoundStyle);
        }

        void Awake()
        {
            m_Button = GetComponent<Button>();
            m_Button.onClick.AddListener(HandleButtonClicked);
        }

        void OnDestroy()
        {
            if (m_Button != null)
                m_Button.onClick.RemoveListener(HandleButtonClicked);
        }

        public void SetClickSoundStyle(UIButtonSoundStyle clickSoundStyle)
        {
            m_ClickSoundStyle = clickSoundStyle;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || m_Button == null || !m_Button.IsInteractable())
                return;

            if (Time.unscaledTime - s_LastHoverHapticsTime < HoverHapticsCooldown)
                return;

            if (eventData is not TrackedDeviceEventData trackedDeviceEventData)
                return;

            if (trackedDeviceEventData.interactor is not XRBaseInputInteractor inputInteractor)
                return;

            if (inputInteractor.SendHapticImpulse(HoverHapticsAmplitude, HoverHapticsDuration))
                s_LastHoverHapticsTime = Time.unscaledTime;
        }

        void HandleButtonClicked()
        {
            if (!isActiveAndEnabled || m_Button == null || !m_Button.IsInteractable())
                return;

            UIAudioService.PlayClick(m_ClickSoundStyle);
        }
    }
}
