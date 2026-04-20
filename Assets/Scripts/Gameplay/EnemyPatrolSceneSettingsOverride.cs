using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public sealed class EnemyPatrolSceneSettingsOverride : MonoBehaviour
    {
        [SerializeField] bool m_EnableSceneOverride = true;
        [SerializeField] EnemyPatrolTuningProfile m_Profile = default;

        void Reset()
        {
            m_Profile = EnemyPatrolTuningProfile.CreateDefault();
        }

        void OnValidate()
        {
            if (m_Profile.DetectionRange <= 0f)
                m_Profile = EnemyPatrolTuningProfile.CreateDefault();
        }

        public bool TryGetTuningProfile(out EnemyPatrolTuningProfile profile)
        {
            profile = m_Profile.GetSanitized();
            return m_EnableSceneOverride;
        }
    }
}
