using System;
using CIS5680VRGame.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CIS5680VRGame.Progression
{
    public readonly struct RandomMazeGoldSettlementSummary
    {
        public RandomMazeGoldSettlementSummary(int rawRunGoldEarned, int depositedRunGold, int totalGoldAfterDeposit, int payoutMultiplierPercent)
        {
            RawRunGoldEarned = Mathf.Max(0, rawRunGoldEarned);
            RunGoldEarned = Mathf.Max(0, depositedRunGold);
            TotalGoldAfterDeposit = Mathf.Max(0, totalGoldAfterDeposit);
            PayoutMultiplierPercent = Mathf.Max(100, payoutMultiplierPercent);
        }

        public int RawRunGoldEarned { get; }
        public int RunGoldEarned { get; }
        public int TotalGoldAfterDeposit { get; }
        public int PayoutMultiplierPercent { get; }
    }

    public sealed class RandomMazeGoldRunService : MonoBehaviour
    {
        const string RandomMazeSceneName = "random-maze";

        static RandomMazeGoldRunService s_Instance;

        bool m_IsRandomMazeRunActive;
        bool m_HasCommittedCurrentRun;
        int m_CurrentRunGold;
        RandomMazeGoldSettlementSummary m_LastSettlementSummary;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        public static int CurrentRunGold
        {
            get
            {
                EnsureCreated();
                return s_Instance.m_CurrentRunGold;
            }
        }

        public static bool IsRandomMazeRunActive
        {
            get
            {
                EnsureCreated();
                return s_Instance.m_IsRandomMazeRunActive;
            }
        }

        public static bool TryCompleteActiveRunAndSave(out RandomMazeGoldSettlementSummary summary)
        {
            EnsureCreated();
            return s_Instance.TryCompleteActiveRunAndSaveInternal(out summary);
        }

        static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            GameObject root = new("RandomMazeGoldRunService");
            s_Instance = root.AddComponent<RandomMazeGoldRunService>();
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
            RewardPickup.RewardCollected += OnRewardCollected;
        }

        void OnDestroy()
        {
            if (s_Instance != this)
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            RewardPickup.RewardCollected -= OnRewardCollected;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            if (string.Equals(scene.name, RandomMazeSceneName, StringComparison.Ordinal))
            {
                BeginRandomMazeRun();
                return;
            }

            EndRandomMazeRun();
        }

        void OnRewardCollected(RewardCollectionContext context)
        {
            if (!m_IsRandomMazeRunActive || m_HasCommittedCurrentRun)
                return;

            if (context.RewardType != RunRewardType.Coin)
                return;

            m_CurrentRunGold = Mathf.Max(0, m_CurrentRunGold) + Mathf.Max(1, context.Amount);
        }

        void BeginRandomMazeRun()
        {
            m_IsRandomMazeRunActive = true;
            m_HasCommittedCurrentRun = false;
            m_CurrentRunGold = 0;
        }

        void EndRandomMazeRun()
        {
            m_IsRandomMazeRunActive = false;
            m_HasCommittedCurrentRun = false;
            m_CurrentRunGold = 0;
        }

        bool TryCompleteActiveRunAndSaveInternal(out RandomMazeGoldSettlementSummary summary)
        {
            if (!m_IsRandomMazeRunActive)
            {
                summary = default;
                return false;
            }

            if (!m_HasCommittedCurrentRun)
            {
                int payoutMultiplierPercent = 100 + Mathf.Max(
                    0,
                    ProfileUpgradeRuntimeApplier.GetActiveSingleRunEffectValue(ShopUpgradeEffectType.NextRunEscapeGoldBonusPercent));
                int depositedGold = Mathf.Max(
                    0,
                    Mathf.RoundToInt(m_CurrentRunGold * (payoutMultiplierPercent / 100f)));
                int totalGoldAfterDeposit = ProfileService.RecordRandomMazeEscapeAndSave(depositedGold);
                m_LastSettlementSummary = new RandomMazeGoldSettlementSummary(
                    m_CurrentRunGold,
                    depositedGold,
                    totalGoldAfterDeposit,
                    payoutMultiplierPercent);
                m_HasCommittedCurrentRun = true;
            }

            summary = m_LastSettlementSummary;
            return true;
        }
    }
}
