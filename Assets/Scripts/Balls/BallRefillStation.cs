using CIS5680VRGame.Gameplay;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Balls
{
    [RequireComponent(typeof(Collider))]
    public class BallRefillStation : MonoBehaviour
    {
        const float k_RefillRetryInterval = 0.1f;
        static readonly int k_BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int k_ColorId = Shader.PropertyToID("_Color");
        static readonly int k_RimColorId = Shader.PropertyToID("_RimColor");

        [SerializeField] BallHolsterSlot[] m_TargetSlots;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] PlayerEnergy m_PlayerEnergy;
        [SerializeField] int m_Charges = -1;
        [SerializeField] Renderer[] m_VisitedStateRenderers;
        [SerializeField] Color m_VisitedColor = new(0.18f, 0.84f, 0.96f, 1f);
        [SerializeField] Color m_VisitedRimColor = new(0.9f, 1f, 1f, 0.24f);

        Collider m_Trigger;
        MaterialPropertyBlock m_PropertyBlock;
        float m_NextRefillTime;
        bool m_HasBeenVisited;

        public bool IsInfiniteCharges => m_Charges < 0;
        public int ChargesRemaining => m_Charges;
        public bool HasBeenVisited => m_HasBeenVisited;

        void Awake()
        {
            m_Trigger = GetComponent<Collider>();
            m_Trigger.isTrigger = true;

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_PlayerEnergy == null)
                m_PlayerEnergy = FindObjectOfType<PlayerEnergy>();

            if (m_VisitedStateRenderers == null || m_VisitedStateRenderers.Length == 0)
                m_VisitedStateRenderers = GetComponentsInChildren<Renderer>(true);

            m_PropertyBlock = new MaterialPropertyBlock();
        }

        void OnTriggerEnter(Collider other)
        {
            TryRefill(other);
        }

        void OnTriggerStay(Collider other)
        {
            TryRefill(other);
        }

        bool CanUse(Collider other)
        {
            if (!enabled || (!IsInfiniteCharges && m_Charges <= 0))
                return false;

            if (other == null)
                return false;

            var rig = other.GetComponentInParent<XROrigin>();
            if (rig == null)
                return false;

            return m_PlayerRig == null || rig == m_PlayerRig;
        }

        void TryRefill(Collider other)
        {
            if (Time.time < m_NextRefillTime)
                return;

            if (!CanUse(other))
                return;

            bool refilledAny = false;
            for (int i = 0; i < m_TargetSlots.Length; i++)
            {
                BallHolsterSlot targetSlot = m_TargetSlots[i];
                if (targetSlot == null)
                    continue;

                refilledAny |= targetSlot.RefillToMax();
            }

            if (!refilledAny && (m_TargetSlots == null || m_TargetSlots.Length == 0) && m_PlayerEnergy != null)
                refilledAny = m_PlayerEnergy.RefillToMax();

            if (refilledAny && !IsInfiniteCharges)
                m_Charges = Mathf.Max(0, m_Charges - 1);

            if (refilledAny && !m_HasBeenVisited)
                MarkVisited();

            if (refilledAny)
                m_NextRefillTime = Time.time + k_RefillRetryInterval;
        }

        void MarkVisited()
        {
            m_HasBeenVisited = true;

            for (int i = 0; i < m_VisitedStateRenderers.Length; i++)
            {
                Renderer targetRenderer = m_VisitedStateRenderers[i];
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(k_BaseColorId, m_VisitedColor);
                m_PropertyBlock.SetColor(k_ColorId, m_VisitedColor);
                m_PropertyBlock.SetColor(k_RimColorId, m_VisitedRimColor);
                targetRenderer.SetPropertyBlock(m_PropertyBlock);
            }
        }
    }
}
