using CIS5680VRGame.Balls;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Object = UnityEngine.Object;

namespace CIS5680VRGame.Gameplay
{
    static class ModalMenuPauseUtility
    {
        static bool s_IsPausedForMenu;

        public static void PauseGameplayForMenu(XROrigin playerRig, MovementModeManager movementModeManager)
        {
            if (s_IsPausedForMenu)
                return;

            s_IsPausedForMenu = true;
            ReleaseSelectedObjects(playerRig);
            DisableGameplayInteractions();
            movementModeManager?.LockMovement();
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        public static void ResumeGameplayAfterMenu()
        {
            s_IsPausedForMenu = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public static Camera ResolveMenuCamera(XROrigin playerRig)
        {
            if (playerRig != null && playerRig.Camera != null)
                return playerRig.Camera;

            return Camera.main;
        }

        public static TMP_FontAsset ResolveFontAsset()
        {
            if (TMP_Settings.defaultFontAsset != null)
                return TMP_Settings.defaultFontAsset;

            TMP_FontAsset[] loadedFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            return loadedFonts.Length > 0 ? loadedFonts[0] : null;
        }

        public static GameObject CreateScreenSpaceMenuRoot(
            string rootName,
            Camera menuCamera,
            Vector2 panelSize,
            Color backdropColor,
            out RectTransform panelRect)
        {
            GameObject root = new(rootName);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = menuCamera;
            canvas.planeDistance = menuCamera != null
                ? Mathf.Max(menuCamera.nearClipPlane + 0.2f, 0.35f)
                : 0.5f;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            TryAddTrackedDeviceGraphicRaycaster(root);

            GameObject backdrop = CreateUIObject("Backdrop", root.transform);
            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = backdropColor;

            RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            GameObject panel = CreateUIObject("Panel", root.transform);
            panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = panelSize;
            panelRect.anchoredPosition = new Vector2(0f, -80f);

            return root;
        }

        public static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static void ReleaseSelectedObjects(XROrigin playerRig)
        {
            if (playerRig == null)
                playerRig = Object.FindObjectOfType<XROrigin>();

            if (playerRig == null)
                return;

            XRBaseInteractor[] interactors = playerRig.GetComponentsInChildren<XRBaseInteractor>(true);
            for (int i = 0; i < interactors.Length; i++)
            {
                XRBaseInteractor interactor = interactors[i];
                if (interactor == null || interactor.interactionManager == null)
                    continue;

                var selected = interactor.interactablesSelected;
                for (int j = selected.Count - 1; j >= 0; j--)
                {
                    IXRSelectInteractable interactable = selected[j];
                    if (interactable == null)
                        continue;

                    interactor.interactionManager.SelectExit((IXRSelectInteractor)interactor, interactable);
                }
            }
        }

        static void DisableGameplayInteractions()
        {
            XRBaseInteractable[] interactables = Object.FindObjectsOfType<XRBaseInteractable>(true);
            for (int i = 0; i < interactables.Length; i++)
            {
                if (interactables[i] != null)
                    interactables[i].enabled = false;
            }

            BallHolsterSlot[] holsterSlots = Object.FindObjectsOfType<BallHolsterSlot>(true);
            for (int i = 0; i < holsterSlots.Length; i++)
            {
                if (holsterSlots[i] != null)
                    holsterSlots[i].enabled = false;
            }

            RefillStationLocatorGuidance[] locatorGuidance = Object.FindObjectsOfType<RefillStationLocatorGuidance>(true);
            for (int i = 0; i < locatorGuidance.Length; i++)
            {
                if (locatorGuidance[i] != null)
                    locatorGuidance[i].enabled = false;
            }
        }

        static void TryAddTrackedDeviceGraphicRaycaster(GameObject target)
        {
            if (target == null)
                return;

            const string trackedRaycasterTypeName =
                "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit";

            System.Type trackedRaycasterType = System.Type.GetType(trackedRaycasterTypeName);
            if (trackedRaycasterType != null && target.GetComponent(trackedRaycasterType) == null)
                target.AddComponent(trackedRaycasterType);
        }
    }
}
