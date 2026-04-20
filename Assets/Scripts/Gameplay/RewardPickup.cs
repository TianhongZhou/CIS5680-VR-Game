using System;
using CIS5680VRGame.UI;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Gameplay
{
    public enum RunRewardType
    {
        Coin = 0,
    }

    public readonly struct RewardCollectionContext
    {
        public RewardCollectionContext(RewardPickup pickup, RunRewardType rewardType, int amount, Vector3 worldPosition)
        {
            Pickup = pickup;
            RewardType = rewardType;
            Amount = amount;
            WorldPosition = worldPosition;
        }

        public RewardPickup Pickup { get; }
        public RunRewardType RewardType { get; }
        public int Amount { get; }
        public Vector3 WorldPosition { get; }
    }

    public interface IRewardCollectionSink
    {
        void HandleRewardCollected(RewardCollectionContext context);
    }

    [RequireComponent(typeof(Collider))]
    public class RewardPickup : MonoBehaviour
    {
        [SerializeField] RunRewardType m_RewardType = RunRewardType.Coin;
        [SerializeField, Min(1)] int m_Amount = 1;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] MonoBehaviour m_CollectionSink;
        [SerializeField] bool m_DestroyOnCollect = true;
        [SerializeField] bool m_LogCollection = true;

        Collider m_Trigger;
        IRewardCollectionSink m_ResolvedSink;

        public static event Action<RewardCollectionContext> RewardCollected;

        public bool HasBeenCollected { get; private set; }
        public RunRewardType RewardType => m_RewardType;
        public int Amount => Mathf.Max(1, m_Amount);

        void Awake()
        {
            m_Trigger = GetComponent<Collider>();
            if (m_Trigger != null)
                m_Trigger.isTrigger = true;

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            ResolveCollectionSink();
        }

        public void Configure(RunRewardType rewardType, int amount)
        {
            m_RewardType = rewardType;
            m_Amount = Mathf.Max(1, amount);
        }

        void OnTriggerEnter(Collider other)
        {
            TryCollect(other);
        }

        bool CanCollect(Collider other)
        {
            if (HasBeenCollected || other == null)
                return false;

            XROrigin rig = other.GetComponentInParent<XROrigin>();
            if (rig == null)
                return false;

            return m_PlayerRig == null || rig == m_PlayerRig;
        }

        void TryCollect(Collider other)
        {
            if (!CanCollect(other))
                return;

            HasBeenCollected = true;
            if (m_Trigger != null)
                m_Trigger.enabled = false;

            var context = new RewardCollectionContext(this, m_RewardType, Amount, transform.position);
            m_ResolvedSink?.HandleRewardCollected(context);
            RewardCollected?.Invoke(context);
            UIAudioService.PlayClick(UIButtonSoundStyle.Confirm);

            if (m_LogCollection)
            {
                Debug.Log(
                    $"Collected reward '{name}' ({m_RewardType}) for {Amount} at {transform.position}.",
                    this);
            }

            if (m_DestroyOnCollect)
                DestroyCollectedObject();
            else
                gameObject.SetActive(false);
        }

        void ResolveCollectionSink()
        {
            if (m_CollectionSink == null)
            {
                m_ResolvedSink = null;
                return;
            }

            m_ResolvedSink = m_CollectionSink as IRewardCollectionSink;
            if (m_ResolvedSink == null)
            {
                Debug.LogWarning(
                    $"RewardPickup on '{name}' has a collection sink that does not implement {nameof(IRewardCollectionSink)}.",
                    this);
            }
        }

        void DestroyCollectedObject()
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }
}
