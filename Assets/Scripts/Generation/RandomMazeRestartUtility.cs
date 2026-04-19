using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CIS5680VRGame.Generation
{
    public static class RandomMazeRestartUtility
    {
        public const string RandomMazeSceneName = "random-maze";

        public static bool TryPrepareSameMapRestart(Scene scene)
        {
            if (!IsRandomMazeScene(scene))
                return false;

            MazeRunBootstrap bootstrap = UnityEngine.Object.FindObjectOfType<MazeRunBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("RandomMazeRestartUtility could not find MazeRunBootstrap for same-map restart.");
                return false;
            }

            RandomMazeSceneSeedOverrideService.SetNextLoadSeedOverride(scene.name, bootstrap.CurrentSeed);
            return true;
        }

        public static bool TryPrepareNewMapRestart(Scene scene, out int seed)
        {
            seed = default;
            if (!IsRandomMazeScene(scene))
                return false;

            seed = RandomMazeSceneSeedOverrideService.SetNextLoadToRandomSeed(scene.name);
            return true;
        }

        public static bool IsRandomMazeScene(Scene scene)
        {
            return string.Equals(scene.name, RandomMazeSceneName, StringComparison.Ordinal);
        }
    }
}
