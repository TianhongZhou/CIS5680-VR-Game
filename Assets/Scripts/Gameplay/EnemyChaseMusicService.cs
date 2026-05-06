using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public sealed class EnemyChaseMusicService : MonoBehaviour
    {
        const string ChaseClipPath = "Audio/Gameplay/Gameplay_EnemyChaseLoop";

        static EnemyChaseMusicService s_Instance;

        [SerializeField] float m_ChaseVolume = 0.85f;
        [SerializeField] float m_ChaseFadeInTime = 0.2f;
        [SerializeField] float m_ChaseFadeOutTime = 1.45f;
        [SerializeField] float m_AmbientDuckFadeOutTime = 0.14f;
        [SerializeField] float m_AmbientRestoreFadeInTime = 1.35f;

        AudioSource m_AudioSource;
        AudioClip m_ChaseClip;
        float m_CurrentMusicVelocity;
        float m_CurrentAmbientVelocity;
        float m_CurrentAmbientMultiplier = 1f;
        bool m_SuppressForSceneTransition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            GameObject root = new("EnemyChaseMusicService");
            DontDestroyOnLoad(root);
            s_Instance = root.AddComponent<EnemyChaseMusicService>();
        }

        public static void BeginSceneTransitionReset()
        {
            if (s_Instance == null)
            {
                ApplyAmbientMultiplier(1f);
                return;
            }

            s_Instance.m_SuppressForSceneTransition = true;
            s_Instance.StopChaseMusicImmediately();
        }

        public static void EndSceneTransitionReset()
        {
            if (s_Instance == null)
            {
                ApplyAmbientMultiplier(1f);
                return;
            }

            s_Instance.m_SuppressForSceneTransition = false;
            s_Instance.ResetAmbientDuckImmediately();
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
            ApplyAmbientMultiplier(1f);
        }

        void OnDisable()
        {
            ApplyAmbientMultiplier(1f);
            if (m_AudioSource != null)
                m_AudioSource.Stop();
        }

        void Update()
        {
            EnsureAudioSource();

            bool canPlayChaseMusic = m_ChaseClip != null;
            bool shouldPlayChaseMusic = canPlayChaseMusic
                && !m_SuppressForSceneTransition
                && EnemyPatrolController.HasChasingEnemy();

            UpdateAmbientDuck(shouldPlayChaseMusic);
            UpdateMusicPlayback(shouldPlayChaseMusic);
        }

        void EnsureAudioSource()
        {
            if (m_AudioSource == null)
                m_AudioSource = GetComponent<AudioSource>();

            if (m_AudioSource == null)
                m_AudioSource = gameObject.AddComponent<AudioSource>();

            if (m_ChaseClip == null)
                m_ChaseClip = Resources.Load<AudioClip>(ChaseClipPath);

            m_AudioSource.playOnAwake = false;
            m_AudioSource.loop = true;
            m_AudioSource.spatialBlend = 0f;
            m_AudioSource.dopplerLevel = 0f;
            m_AudioSource.ignoreListenerPause = false;
            m_AudioSource.clip = m_ChaseClip;
        }

        void UpdateAmbientDuck(bool shouldPlayChaseMusic)
        {
            float targetMultiplier = shouldPlayChaseMusic ? 0f : 1f;
            float smoothTime = shouldPlayChaseMusic ? m_AmbientDuckFadeOutTime : m_AmbientRestoreFadeInTime;
            m_CurrentAmbientMultiplier = Mathf.SmoothDamp(
                m_CurrentAmbientMultiplier,
                targetMultiplier,
                ref m_CurrentAmbientVelocity,
                Mathf.Max(0.01f, smoothTime));

            ApplyAmbientMultiplier(m_CurrentAmbientMultiplier);
        }

        static void ApplyAmbientMultiplier(float multiplier)
        {
            float clampedMultiplier = Mathf.Clamp01(multiplier);
            GoalBeaconAmbientAudio.SetExternalVolumeMultiplier(clampedMultiplier);
            TrapAmbientAudioService.SetExternalVolumeMultiplier(clampedMultiplier);
        }

        void StopChaseMusicImmediately()
        {
            EnsureAudioSource();

            m_CurrentMusicVelocity = 0f;
            if (m_AudioSource != null)
            {
                m_AudioSource.volume = 0f;
                if (m_AudioSource.isPlaying)
                    m_AudioSource.Stop();
            }

            ResetAmbientDuckImmediately();
        }

        void ResetAmbientDuckImmediately()
        {
            m_CurrentAmbientVelocity = 0f;
            m_CurrentAmbientMultiplier = 1f;
            ApplyAmbientMultiplier(1f);
        }

        void UpdateMusicPlayback(bool shouldPlayChaseMusic)
        {
            if (m_AudioSource == null || m_AudioSource.clip == null)
                return;

            float targetVolume = shouldPlayChaseMusic ? Mathf.Clamp01(m_ChaseVolume) : 0f;
            float smoothTime = shouldPlayChaseMusic ? m_ChaseFadeInTime : m_ChaseFadeOutTime;
            m_AudioSource.volume = Mathf.SmoothDamp(
                m_AudioSource.volume,
                targetVolume,
                ref m_CurrentMusicVelocity,
                Mathf.Max(0.01f, smoothTime));

            if (targetVolume > 0.001f)
            {
                if (!m_AudioSource.isPlaying)
                    m_AudioSource.Play();
            }
            else if (m_AudioSource.isPlaying && m_AudioSource.volume <= 0.001f)
            {
                m_AudioSource.Stop();
            }
        }
    }
}
