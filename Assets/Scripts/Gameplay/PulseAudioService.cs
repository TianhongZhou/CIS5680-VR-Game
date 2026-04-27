using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public sealed class PulseAudioService : MonoBehaviour
    {
        const string PulseClipPath = "Audio/Gameplay/Gameplay_SonarPulse";
        const string LocatorPingClipPath = "Audio/Gameplay/Gameplay_LocatorPing";
        const string ResourceRestoredClipPath = "Audio/Gameplay/Gameplay_ResourceRestored";
        const string DamageTakenClipPath = "Audio/Gameplay/Gameplay_DamageTaken";
        const string TeleportArrivalClipPath = "Audio/Gameplay/Teleport/teleport_new";
        const string LevelCompleteClipPath = "Audio/Gameplay/Gameplay_LevelComplete";

        static PulseAudioService s_Instance;

        AudioClip m_PulseClip;
        AudioClip m_LocatorPingClip;
        AudioClip m_ResourceRestoredClip;
        AudioClip m_DamageTakenClip;
        AudioClip m_TeleportArrivalClip;
        AudioClip m_LevelCompleteClip;
        AudioSource m_InterfaceSource;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        public static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            var root = new GameObject("PulseAudioService");
            DontDestroyOnLoad(root);
            s_Instance = root.AddComponent<PulseAudioService>();
        }

        public static void PlayPulse(Vector3 worldPosition)
        {
            if (s_Instance == null)
                EnsureCreated();

            s_Instance?.PlayPulseInternal(worldPosition);
        }

        public static void PlayLocatorPing()
        {
            if (s_Instance == null)
                EnsureCreated();

            s_Instance?.PlayLocatorPingInternal();
        }

        public static void PlayResourceRestored()
        {
            if (s_Instance == null)
                EnsureCreated();

            s_Instance?.PlayResourceRestoredInternal();
        }

        public static void PlayDamageTaken(float volumeScale = 1f)
        {
            if (s_Instance == null)
                EnsureCreated();

            s_Instance?.PlayDamageTakenInternal(volumeScale);
        }

        public static void PlayTeleportArrival(float volumeScale = 1f)
        {
            if (s_Instance == null)
                EnsureCreated();

            s_Instance?.PlayTeleportArrivalInternal(volumeScale);
        }

        public static void PlayLevelComplete(float volumeScale = 1f)
        {
            if (s_Instance == null)
                EnsureCreated();

            s_Instance?.PlayLevelCompleteInternal(volumeScale);
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
            LoadClipsIfNeeded();
            EnsureInterfaceSource();
        }

        void LoadClipsIfNeeded()
        {
            if (m_PulseClip == null)
                m_PulseClip = Resources.Load<AudioClip>(PulseClipPath);

            if (m_LocatorPingClip == null)
                m_LocatorPingClip = Resources.Load<AudioClip>(LocatorPingClipPath);

            if (m_ResourceRestoredClip == null)
                m_ResourceRestoredClip = Resources.Load<AudioClip>(ResourceRestoredClipPath);

            if (m_DamageTakenClip == null)
                m_DamageTakenClip = Resources.Load<AudioClip>(DamageTakenClipPath);

            if (m_TeleportArrivalClip == null)
                m_TeleportArrivalClip = Resources.Load<AudioClip>(TeleportArrivalClipPath);

            if (m_LevelCompleteClip == null)
                m_LevelCompleteClip = Resources.Load<AudioClip>(LevelCompleteClipPath);
        }

        void EnsureInterfaceSource()
        {
            if (m_InterfaceSource != null)
                return;

            m_InterfaceSource = gameObject.AddComponent<AudioSource>();
            m_InterfaceSource.playOnAwake = false;
            m_InterfaceSource.loop = false;
            m_InterfaceSource.spatialBlend = 0f;
            m_InterfaceSource.dopplerLevel = 0f;
            m_InterfaceSource.ignoreListenerPause = true;
            m_InterfaceSource.volume = 0.82f;
        }

        void PlayPulseInternal(Vector3 worldPosition)
        {
            LoadClipsIfNeeded();
            if (m_PulseClip == null)
                return;

            var oneShot = new GameObject("PulseAudioShot");
            oneShot.transform.position = worldPosition;

            AudioSource source = oneShot.AddComponent<AudioSource>();
            source.clip = m_PulseClip;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.8f;
            source.maxDistance = 14f;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.volume = 0.92f;
            source.Play();

            Destroy(oneShot, m_PulseClip.length + 0.05f);
        }

        void PlayLocatorPingInternal()
        {
            LoadClipsIfNeeded();
            EnsureInterfaceSource();
            if (m_LocatorPingClip == null || m_InterfaceSource == null)
                return;

            m_InterfaceSource.PlayOneShot(m_LocatorPingClip);
        }

        void PlayResourceRestoredInternal()
        {
            LoadClipsIfNeeded();
            EnsureInterfaceSource();
            if (m_ResourceRestoredClip == null || m_InterfaceSource == null)
                return;

            m_InterfaceSource.PlayOneShot(m_ResourceRestoredClip, 0.94f);
        }

        void PlayDamageTakenInternal(float volumeScale)
        {
            LoadClipsIfNeeded();
            EnsureInterfaceSource();
            if (m_DamageTakenClip == null || m_InterfaceSource == null)
                return;

            m_InterfaceSource.PlayOneShot(m_DamageTakenClip, Mathf.Clamp(volumeScale, 0f, 1.2f));
        }

        void PlayTeleportArrivalInternal(float volumeScale)
        {
            LoadClipsIfNeeded();
            EnsureInterfaceSource();
            if (m_TeleportArrivalClip == null || m_InterfaceSource == null)
                return;

            m_InterfaceSource.PlayOneShot(m_TeleportArrivalClip, Mathf.Clamp(volumeScale, 0f, 1.2f));
        }

        void PlayLevelCompleteInternal(float volumeScale)
        {
            LoadClipsIfNeeded();
            EnsureInterfaceSource();
            if (m_LevelCompleteClip == null || m_InterfaceSource == null)
                return;

            m_InterfaceSource.PlayOneShot(m_LevelCompleteClip, Mathf.Clamp(volumeScale, 0f, 1.2f));
        }
    }
}
