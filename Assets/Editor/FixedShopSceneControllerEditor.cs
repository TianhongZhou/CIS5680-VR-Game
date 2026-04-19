using CIS5680VRGame.Progression;
using UnityEditor;
using UnityEngine;

namespace CIS5680VRGame.Editor
{
    [CustomEditor(typeof(FixedShopSceneController))]
    public class FixedShopSceneControllerEditor : UnityEditor.Editor
    {
        int m_SelectedForcedOfferIndex;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These actions only exist in the Inspector for debug use. They do not appear in-game.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Set Gold To 999", GUILayout.Height(28f)))
                {
                    ProfileService.DebugSetTotalGold(999);

                    if (target is FixedShopSceneController controller)
                    {
                        controller.EditorRefreshUiForDebug();
                        EditorUtility.SetDirty(controller);
                    }

                    Debug.Log("Shop debug action set saved gold to 999.");
                }
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear All Purchased Items", GUILayout.Height(28f)))
                {
                    ProfileService.DebugClearAllPurchasedUpgrades();

                    if (target is FixedShopSceneController controller)
                    {
                        controller.EditorRebuildUiForDebug(true);
                        EditorUtility.SetDirty(controller);
                    }

                    Debug.Log("Shop debug action cleared all purchased shop items.");
                }

                if (GUILayout.Button("Unlock All Shop Items", GUILayout.Height(28f)))
                {
                    ProfileService.DebugUnlockAllPurchasableUpgrades();

                    if (target is FixedShopSceneController controller)
                    {
                        controller.EditorRebuildUiForDebug(true);
                        EditorUtility.SetDirty(controller);
                    }

                    Debug.Log("Shop debug action unlocked all persistent shop items.");
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Force Offer Refresh", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select a product and force it onto the current shop shelf. This is a debug-only shortcut for testing one specific offer.",
                MessageType.None);

            if (target is FixedShopSceneController shopController)
            {
                var offerDefinitions = ShopUpgradeCatalog.AllShopOffers;
                string[] optionLabels = new string[offerDefinitions.Count];
                for (int i = 0; i < offerDefinitions.Count; i++)
                    optionLabels[i] = $"{offerDefinitions[i].DisplayName} ({offerDefinitions[i].Id})";

                if (offerDefinitions.Count > 0)
                {
                    m_SelectedForcedOfferIndex = Mathf.Clamp(m_SelectedForcedOfferIndex, 0, offerDefinitions.Count - 1);
                    m_SelectedForcedOfferIndex = EditorGUILayout.Popup(
                        "Offer",
                        m_SelectedForcedOfferIndex,
                        optionLabels);

                    if (GUILayout.Button("Force Selected Offer Into Shop", GUILayout.Height(28f)))
                    {
                        string selectedOfferId = offerDefinitions[m_SelectedForcedOfferIndex].Id;
                        bool didForceOffer = ProfileService.DebugForceShopOffer(selectedOfferId, shopController.DebugOfferSlotCount);
                        if (didForceOffer)
                        {
                            shopController.EditorRebuildUiForDebug(false);
                            EditorUtility.SetDirty(shopController);
                            Debug.Log($"Shop debug action forced offer {selectedOfferId} onto the current shelf.");
                        }
                        else
                        {
                            Debug.LogWarning($"Shop debug action could not force offer {selectedOfferId}.");
                        }
                    }
                }
            }
        }
    }
}
