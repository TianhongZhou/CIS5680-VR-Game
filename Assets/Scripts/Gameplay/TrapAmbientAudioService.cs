using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public sealed class TrapAmbientAudioService : MonoBehaviour
    {
        const string TrapLoopClipPath = "Audio/Gameplay/Gameplay_MazeTrapLoop";

        static TrapAmbientAudioService s_Instance;
        static float s_ExternalVolumeMultiplier = 1f;

        AudioSource m_AudioSource;
        AudioClip m_TrapLoopClip;
        Transform m_ListenerTransform;
        float m_CurrentVolumeVelocity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            var root = new GameObject("TrapAmbientAudioService");
            DontDestroyOnLoad(root);
            s_Instance = root.AddComponent<TrapAmbientAudioService>();
        }

        public static void SetExternalVolumeMultiplier(float multiplier)
        {
            s_ExternalVolumeMultiplier = Mathf.Clamp01(multiplier);
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                s_Instance = null;
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
            ResolveListener();
            EnsureAudioSource();
        }

        void Update()
        {
            ResolveListener();
            EnsureAudioSource();
            UpdatePlaybackState();
        }

        void ResolveListener()
        {
            XROrigin playerRig = FindObjectOfType<XROrigin>();
            if (playerRig != null && playerRig.Camera != null)
            {
                m_ListenerTransform = playerRig.Camera.transform;
                return;
            }

            if (Camera.main != null)
                m_ListenerTransform = Camera.main.transform;
        }

        void EnsureAudioSource()
        {
            if (m_AudioSource == null)
                m_AudioSource = GetComponent<AudioSource>();

            if (m_AudioSource == null)
                m_AudioSource = gameObject.AddComponent<AudioSource>();

            if (m_TrapLoopClip == null)
                m_TrapLoopClip = Resources.Load<AudioClip>(TrapLoopClipPath);

            m_AudioSource.playOnAwake = false;
            m_AudioSource.loop = true;
            m_AudioSource.spatialBlend = 0f;
            m_AudioSource.dopplerLevel = 0f;
            m_AudioSource.ignoreListenerPause = false;
            m_AudioSource.clip = m_TrapLoopClip;
        }

        void UpdatePlaybackState()
        {
            if (m_AudioSource == null || m_AudioSource.clip == null)
                return;

            bool hasTarget = PlayerDamageTrap.TryGetAmbientTarget(m_ListenerTransform, out float targetVolume, out float smoothTime);
            if (!hasTarget)
                targetVolume = 0f;

            targetVolume *= s_ExternalVolumeMultiplier;

            m_AudioSource.volume = Mathf.SmoothDamp(
                m_AudioSource.volume,
                targetVolume,
                ref m_CurrentVolumeVelocity,
                Mathf.Max(0.01f, smoothTime));

            if (m_AudioSource.volume > 0.001f || targetVolume > 0.001f)
            {
                if (!m_AudioSource.isPlaying)
                    m_AudioSource.Play();
            }
            else if (m_AudioSource.isPlaying)
            {
                m_AudioSource.Stop();
            }
        }
    }
}
