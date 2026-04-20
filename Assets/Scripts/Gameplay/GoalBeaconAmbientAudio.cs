using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GoalBeaconAmbientAudio : MonoBehaviour
    {
        const string BeaconClipPath = "Audio/Gameplay/Gameplay_BeaconLoop";
        static readonly AnimationCurve s_FlatRolloffCurve = new(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 1f));

        static AudioClip s_BeaconClip;
        static float s_ExternalVolumeMultiplier = 1f;

        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField, Range(0f, 1f)] float m_FarVolume = 0.045f;
        [SerializeField, Range(0f, 1f)] float m_NearVolume = 0.16f;
        [SerializeField] float m_VolumeRampDistance = 25f;
        [SerializeField] float m_VolumeSmoothTime = 0.35f;
        [SerializeField] float m_MaxAudibleDistance = 180f;
        [SerializeField] float m_MinDistance = 1.2f;

        AudioSource m_AudioSource;
        Transform m_ListenerTransform;
        float m_CurrentVolumeVelocity;

        public static void EnsureAttached(GameObject target)
        {
            if (target == null || target.GetComponent<GoalBeaconAmbientAudio>() != null)
                return;

            target.AddComponent<GoalBeaconAmbientAudio>();
        }

        public static void SetExternalVolumeMultiplier(float multiplier)
        {
            s_ExternalVolumeMultiplier = Mathf.Clamp01(multiplier);
        }

        void Awake()
        {
            ResolveReferences();
            EnsureAudioSource();
        }

        void OnEnable()
        {
            ResolveReferences();
            EnsureAudioSource();
            UpdatePlaybackState(immediate: true);
        }

        void OnDisable()
        {
            if (m_AudioSource != null)
                m_AudioSource.Stop();
        }

        void Update()
        {
            ResolveReferences();
            EnsureAudioSource();
            UpdatePlaybackState(immediate: false);
        }

        void ResolveReferences()
        {
            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_PlayerRig != null && m_PlayerRig.Camera != null)
            {
                m_ListenerTransform = m_PlayerRig.Camera.transform;
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

            if (s_BeaconClip == null)
                s_BeaconClip = Resources.Load<AudioClip>(BeaconClipPath);

            m_AudioSource.playOnAwake = false;
            m_AudioSource.loop = true;
            m_AudioSource.spatialBlend = 1f;
            m_AudioSource.rolloffMode = AudioRolloffMode.Custom;
            m_AudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, s_FlatRolloffCurve);
            m_AudioSource.minDistance = Mathf.Max(0.1f, m_MinDistance);
            m_AudioSource.maxDistance = Mathf.Max(m_MinDistance + 1f, m_MaxAudibleDistance);
            m_AudioSource.dopplerLevel = 0f;
            m_AudioSource.spread = 0f;
            m_AudioSource.clip = s_BeaconClip;
        }

        void UpdatePlaybackState(bool immediate)
        {
            if (m_AudioSource == null || m_AudioSource.clip == null)
                return;

            if (IsGoalCompleted())
            {
                if (m_AudioSource.isPlaying)
                    m_AudioSource.Stop();

                return;
            }

            float targetVolume = ResolveTargetVolume();
            if (immediate)
            {
                m_AudioSource.volume = targetVolume;
                m_CurrentVolumeVelocity = 0f;
            }
            else
            {
                m_AudioSource.volume = Mathf.SmoothDamp(
                    m_AudioSource.volume,
                    targetVolume,
                    ref m_CurrentVolumeVelocity,
                    Mathf.Max(0.01f, m_VolumeSmoothTime));
            }

            if (!m_AudioSource.isPlaying)
                m_AudioSource.Play();
        }

        float ResolveTargetVolume()
        {
            if (PlayerDamageTrap.HasAudibleTrapNearby(m_ListenerTransform))
                return 0f;

            if (m_ListenerTransform == null)
                return Mathf.Clamp01(m_FarVolume);

            float distance = Vector3.Distance(m_ListenerTransform.position, transform.position);
            float rampDistance = Mathf.Max(0.01f, m_VolumeRampDistance);
            float t = 1f - Mathf.Clamp01(distance / rampDistance);
            t = Mathf.SmoothStep(0f, 1f, t);
            float baseVolume = Mathf.Lerp(
                Mathf.Clamp01(m_FarVolume),
                Mathf.Clamp01(m_NearVolume),
                t);
            return baseVolume * s_ExternalVolumeMultiplier;
        }

        bool IsGoalCompleted()
        {
            TutorialCompletionGoal tutorialGoal = GetComponent<TutorialCompletionGoal>();
            if (tutorialGoal != null && tutorialGoal.enabled && tutorialGoal.HasCompleted)
                return true;

            LevelGoalTrigger levelGoal = GetComponent<LevelGoalTrigger>();
            return levelGoal != null && levelGoal.HasCompleted;
        }
    }
}
