using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CIS5680VRGame.Generation
{
    public sealed class RandomMazeSceneSeedOverrideService : MonoBehaviour
    {
        static RandomMazeSceneSeedOverrideService s_Instance;
        static string s_PendingSceneName;
        static int? s_PendingSeed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        public static void SetNextLoadSeedOverride(string sceneName, int seed)
        {
            EnsureCreated();
            s_PendingSceneName = NormalizeSceneName(sceneName);
            s_PendingSeed = seed;
        }

        public static int SetNextLoadToRandomSeed(string sceneName)
        {
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            SetNextLoadSeedOverride(sceneName, seed);
            return seed;
        }

        public static bool TryConsumeSeedOverride(string sceneName, out int seed)
        {
            EnsureCreated();
            string normalizedSceneName = NormalizeSceneName(sceneName);
            if (!s_PendingSeed.HasValue || string.IsNullOrEmpty(normalizedSceneName))
            {
                seed = default;
                return false;
            }

            if (!string.Equals(normalizedSceneName, s_PendingSceneName, StringComparison.Ordinal))
            {
                seed = default;
                return false;
            }

            seed = s_PendingSeed.Value;
            ClearPendingOverride();
            return true;
        }

        static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            GameObject root = new("RandomMazeSceneSeedOverrideService");
            s_Instance = root.AddComponent<RandomMazeSceneSeedOverrideService>();
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
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (s_Instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (!s_PendingSeed.HasValue)
                return;

            string loadedSceneName = NormalizeSceneName(scene.name);
            if (string.Equals(loadedSceneName, s_PendingSceneName, StringComparison.Ordinal))
                return;

            Debug.LogWarning(
                $"RandomMazeSceneSeedOverrideService cleared a pending scene seed override for {s_PendingSceneName} " +
                $"because scene {scene.name} loaded instead.",
                this);
            ClearPendingOverride();
        }

        static void ClearPendingOverride()
        {
            s_PendingSceneName = null;
            s_PendingSeed = null;
        }

        static string NormalizeSceneName(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? null : sceneName.Trim();
        }
    }
}
