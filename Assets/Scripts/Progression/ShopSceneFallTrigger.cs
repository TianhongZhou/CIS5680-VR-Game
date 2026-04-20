using Unity.XR.CoreUtils;
using UnityEngine;

namespace CIS5680VRGame.Progression
{
    [RequireComponent(typeof(Collider))]
    public sealed class ShopSceneFallTrigger : MonoBehaviour
    {
        FixedShopSceneController m_Controller;

        public void Initialize(FixedShopSceneController controller)
        {
            m_Controller = controller;

            Collider triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
                triggerCollider.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (m_Controller == null || other == null)
                return;

            XROrigin playerRig = other.GetComponentInParent<XROrigin>();
            if (playerRig == null)
                return;

            m_Controller.RecoverPlayerToSpawnPoint();
        }
    }
}
