using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    [CreateAssetMenu(fileName = "EnemyPatrolSharedSettings", menuName = "CIS5680 VR Game/Enemy Patrol Shared Settings")]
    public sealed class EnemyPatrolSharedSettings : ScriptableObject
    {
        const string ResourcePath = "Gameplay/EnemyPatrolSharedSettings";

        static EnemyPatrolSharedSettings s_CachedSettings;

        [SerializeField] EnemyPatrolTuningProfile m_Profile = default;

        void OnEnable()
        {
            if (m_Profile.DetectionRange <= 0f)
                m_Profile = EnemyPatrolTuningProfile.CreateDefault();
        }

        public EnemyPatrolTuningProfile GetSanitizedProfile()
        {
            return m_Profile.GetSanitized();
        }

        public static EnemyPatrolTuningProfile ResolveActiveProfile()
        {
            EnemyPatrolSceneSettingsOverride sceneOverride = Object.FindObjectOfType<EnemyPatrolSceneSettingsOverride>(true);
            if (sceneOverride != null && sceneOverride.TryGetTuningProfile(out EnemyPatrolTuningProfile overrideProfile))
                return overrideProfile;

            if (s_CachedSettings == null)
                s_CachedSettings = Resources.Load<EnemyPatrolSharedSettings>(ResourcePath);

            if (s_CachedSettings != null)
                return s_CachedSettings.GetSanitizedProfile();

            return EnemyPatrolTuningProfile.CreateDefault().GetSanitized();
        }
    }
}
