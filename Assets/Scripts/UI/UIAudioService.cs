using UnityEngine;

namespace CIS5680VRGame.UI
{
    public enum UIButtonSoundStyle
    {
        Normal = 0,
        Confirm = 1,
        Cancel = 2,
    }

    public sealed class UIAudioService : MonoBehaviour
    {
        const string ConfirmClipPath = "Audio/UI/UI_Confirm";
        const string CancelClipPath = "Audio/UI/UI_Cancel";
        const string PauseOpenClipPath = "Audio/UI/UI_PauseOpen";

        static UIAudioService s_Instance;

        AudioSource m_AudioSource;
        AudioClip m_ConfirmClip;
        AudioClip m_CancelClip;
        AudioClip m_PauseOpenClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        public static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            var root = new GameObject("UIAudioService");
            s_Instance = root.AddComponent<UIAudioService>();
        }

        public static void PlayClick(UIButtonSoundStyle soundStyle)
        {
            if (s_Instance == null)
                EnsureCreated();

            s_Instance?.PlayClickInternal(soundStyle);
        }

        public static void PlayPauseOpen()
        {
            if (s_Instance == null)
                EnsureCreated();

            s_Instance?.PlayPauseOpenInternal();
        }

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAudioSource();
            LoadClipsIfNeeded();
        }

        void EnsureAudioSource()
        {
            if (m_AudioSource == null)
                m_AudioSource = GetComponent<AudioSource>();

            if (m_AudioSource == null)
                m_AudioSource = gameObject.AddComponent<AudioSource>();

            m_AudioSource.playOnAwake = false;
            m_AudioSource.loop = false;
            m_AudioSource.spatialBlend = 0f;
            m_AudioSource.ignoreListenerPause = true;
        }

        void LoadClipsIfNeeded()
        {
            if (m_ConfirmClip == null)
                m_ConfirmClip = Resources.Load<AudioClip>(ConfirmClipPath);

            if (m_CancelClip == null)
                m_CancelClip = Resources.Load<AudioClip>(CancelClipPath);

            if (m_PauseOpenClip == null)
                m_PauseOpenClip = Resources.Load<AudioClip>(PauseOpenClipPath);
        }

        void PlayClickInternal(UIButtonSoundStyle soundStyle)
        {
            LoadClipsIfNeeded();

            AudioClip clip = m_ConfirmClip;
            float volume = 0.78f;

            switch (soundStyle)
            {
                case UIButtonSoundStyle.Confirm:
                    clip = m_ConfirmClip;
                    volume = 0.78f;
                    break;
                case UIButtonSoundStyle.Cancel:
                    clip = m_CancelClip != null ? m_CancelClip : m_ConfirmClip;
                    volume = 0.72f;
                    break;
            }

            if (clip == null)
                return;

            m_AudioSource.PlayOneShot(clip, volume);
        }

        void PlayPauseOpenInternal()
        {
            LoadClipsIfNeeded();

            AudioClip clip = m_PauseOpenClip != null ? m_PauseOpenClip : m_ConfirmClip;
            if (clip == null)
                return;

            m_AudioSource.PlayOneShot(clip, 0.82f);
        }
    }
}
