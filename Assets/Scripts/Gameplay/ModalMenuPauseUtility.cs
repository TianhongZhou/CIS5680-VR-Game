using CIS5680VRGame.Balls;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using Object = UnityEngine.Object;
using System.Collections.Generic;

namespace CIS5680VRGame.Gameplay
{
    static class ModalMenuPauseUtility
    {
        public static readonly Vector2 ScreenMenuReferenceResolution = new(1600f, 900f);
        public static readonly Vector3 WorldMenuLocalOffset = new(0f, -0.36f, 2f);
        public const float WorldMenuUnitsPerPixel = 0.0022f;
        const float k_MinWorldMenuDistance = 1.65f;
        const float k_HighestComfortableWorldMenuY = -0.28f;

        static bool s_IsPausedForMenu;
        static readonly List<Behaviour> s_DisabledBehaviours = new();
        static MovementModeManager s_LockedMovementModeManager;
        static bool s_IsLockedForTransition;
        static readonly List<Behaviour> s_TransitionDisabledBehaviours = new();
        static MovementModeManager s_TransitionLockedMovementModeManager;

        public static bool IsPausedForMenu => s_IsPausedForMenu;
        public static bool IsLockedForTransition => s_IsLockedForTransition;

        public static void PauseGameplayForMenu(XROrigin playerRig, MovementModeManager movementModeManager)
        {
            if (s_IsPausedForMenu)
                return;

            s_IsPausedForMenu = true;
            s_DisabledBehaviours.Clear();
            ReleaseSelectedObjects(playerRig);
            DisableGameplayInteractions(s_DisabledBehaviours);
            s_LockedMovementModeManager = movementModeManager;
            s_LockedMovementModeManager?.LockMovement();
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        public static void ResumeGameplayAfterMenu()
        {
            if (!s_IsPausedForMenu)
                return;

            s_IsPausedForMenu = false;
            RestoreGameplayInteractions(s_DisabledBehaviours);
            s_LockedMovementModeManager?.UnlockMovement();
            s_LockedMovementModeManager = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public static void LockGameplayInputForTransition(XROrigin playerRig = null, MovementModeManager movementModeManager = null)
        {
            s_IsLockedForTransition = true;
            RefreshGameplayInputLockForTransition(playerRig, movementModeManager);
        }

        public static void RefreshGameplayInputLockForTransition(XROrigin playerRig = null, MovementModeManager movementModeManager = null)
        {
            if (!s_IsLockedForTransition)
                s_IsLockedForTransition = true;

            ReleaseSelectedObjects(playerRig);
            s_TransitionDisabledBehaviours.Clear();
            DisableTransitionGameplayInput(playerRig, s_TransitionDisabledBehaviours);

            s_TransitionLockedMovementModeManager = movementModeManager != null
                ? movementModeManager
                : Object.FindObjectOfType<MovementModeManager>();
            s_TransitionLockedMovementModeManager?.LockMovement();
        }

        public static void UnlockGameplayInputAfterTransition()
        {
            if (!s_IsLockedForTransition)
                return;

            s_IsLockedForTransition = false;
            RestoreGameplayInteractions(s_TransitionDisabledBehaviours);
            s_TransitionLockedMovementModeManager?.UnlockMovement();
            s_TransitionLockedMovementModeManager = null;
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

        public static GameObject CreateWorldSpaceMenuRoot(
            string rootName,
            Camera menuCamera,
            Vector2 panelSize,
            Color backdropColor,
            out RectTransform panelRect,
            Vector3? localOffset = null)
        {
            GameObject root = new(rootName, typeof(RectTransform));

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = menuCamera;
            canvas.sortingOrder = 5000;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            ConfigureWorldSpaceCanvasScaler(scaler);

            root.AddComponent<GraphicRaycaster>();
            TryAddTrackedDeviceGraphicRaycaster(root);

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = ScreenMenuReferenceResolution;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            RefreshWorldMenuPose(root, menuCamera, localOffset);

            GameObject backdrop = CreateUIObject("Backdrop", root.transform);
            Image backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = backdropColor;
            backdropImage.raycastTarget = false;

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
            ConfigureCenteredSafePanel(panelRect, panelSize);

            return root;
        }

        public static void ConfigureWorldSpaceMenuRoot(GameObject menuRoot, Camera menuCamera, Vector3? localOffset = null)
        {
            if (menuRoot == null)
                return;

            Canvas canvas = menuRoot.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = menuCamera;
            }

            RectTransform rootRect = menuRoot.GetComponent<RectTransform>();
            if (rootRect != null)
            {
                rootRect.sizeDelta = ScreenMenuReferenceResolution;
                rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                rootRect.pivot = new Vector2(0.5f, 0.5f);
            }

            CanvasScaler scaler = menuRoot.GetComponent<CanvasScaler>();
            if (scaler != null)
                ConfigureWorldSpaceCanvasScaler(scaler);

            if (menuRoot.GetComponent<GraphicRaycaster>() == null)
                menuRoot.AddComponent<GraphicRaycaster>();

            TryAddTrackedDeviceGraphicRaycaster(menuRoot);
            RefreshWorldMenuPose(menuRoot, menuCamera, localOffset);
        }

        public static void RefreshWorldMenuPose(GameObject menuRoot, Camera menuCamera, Vector3? localOffset = null)
        {
            if (menuRoot == null || menuCamera == null)
                return;

            menuRoot.transform.SetParent(menuCamera.transform, false);
            menuRoot.transform.localPosition = ResolveWorldMenuOffset(localOffset ?? WorldMenuLocalOffset);
            menuRoot.transform.localRotation = Quaternion.identity;
            menuRoot.transform.localScale = Vector3.one * WorldMenuUnitsPerPixel;
        }

        public static void ConfigureWorldSpaceCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null)
                return;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 20f;
        }

        public static void ConfigureScreenSpaceCanvasScaler(CanvasScaler scaler)
        {
            if (scaler == null)
                return;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ScreenMenuReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        public static void ConfigureCenteredSafePanel(RectTransform panelRect, Vector2 preferredSize)
        {
            ConfigureCenteredSafePanel(panelRect, preferredSize, new Vector2(96f, 72f), new Vector2(320f, 240f));
        }

        public static void ConfigureCenteredSafePanel(
            RectTransform panelRect,
            Vector2 preferredSize,
            Vector2 safePadding,
            Vector2 minimumSize)
        {
            if (panelRect == null)
                return;

            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = ClampPanelSize(preferredSize, safePadding, minimumSize);
        }

        public static Vector2 ClampPanelSize(Vector2 preferredSize, Vector2 safePadding, Vector2 minimumSize)
        {
            Vector2 maxSize = ScreenMenuReferenceResolution - safePadding * 2f;
            float width = Mathf.Clamp(preferredSize.x, minimumSize.x, Mathf.Max(minimumSize.x, maxSize.x));
            float height = Mathf.Clamp(preferredSize.y, minimumSize.y, Mathf.Max(minimumSize.y, maxSize.y));
            return new Vector2(width, height);
        }

        static Vector3 ResolveWorldMenuOffset(Vector3 localOffset)
        {
            if (localOffset == Vector3.zero)
                localOffset = WorldMenuLocalOffset;

            if (localOffset.y > k_HighestComfortableWorldMenuY)
                localOffset.y = WorldMenuLocalOffset.y;

            localOffset.z = Mathf.Max(localOffset.z, k_MinWorldMenuDistance);
            return localOffset;
        }

        public static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static void RefreshMenuLayout(GameObject menuRoot, RectTransform panelRect)
        {
            if (menuRoot == null || panelRect == null || !menuRoot.activeInHierarchy)
                return;

            TextMeshProUGUI[] textElements = menuRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < textElements.Length; i++)
            {
                if (textElements[i] != null)
                    textElements[i].ForceMeshUpdate();
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            Canvas.ForceUpdateCanvases();
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

        static void DisableGameplayInteractions(
            List<Behaviour> disabledBehaviours,
            bool includeInteractors = false,
            bool includeInputManagers = false,
            bool includeLocomotionProviders = false)
        {
            DisableAndRemember(Object.FindObjectsOfType<XRBaseInteractable>(true), disabledBehaviours);
            DisableAndRemember(Object.FindObjectsOfType<BallHolsterSlot>(true), disabledBehaviours);
            DisableAndRemember(Object.FindObjectsOfType<TeleportBallLauncher>(true), disabledBehaviours);
            DisableAndRemember(Object.FindObjectsOfType<RefillStationLocatorGuidance>(true), disabledBehaviours);
            DisableAndRemember(Object.FindObjectsOfType<MovementModeToggleInput>(true), disabledBehaviours);

            if (includeInteractors)
                DisableAndRemember(Object.FindObjectsOfType<XRBaseInteractor>(true), disabledBehaviours);

            if (includeInputManagers)
            {
                DisableAndRemember(Object.FindObjectsOfType<InputActionManager>(true), disabledBehaviours);
                DisableAndRemember(Object.FindObjectsOfType<ControllerInputActionManager>(true), disabledBehaviours);
            }

            if (includeLocomotionProviders)
                DisableAndRemember(Object.FindObjectsOfType<LocomotionProvider>(true), disabledBehaviours);
        }

        static void DisableTransitionGameplayInput(XROrigin playerRig, List<Behaviour> disabledBehaviours)
        {
            if (playerRig == null)
                playerRig = Object.FindObjectOfType<XROrigin>();

            DisableAndRemember(Object.FindObjectsOfType<RefillStationLocatorGuidance>(true), disabledBehaviours);
            DisableAndRemember(Object.FindObjectsOfType<TeleportBallLauncher>(true), disabledBehaviours);
            DisableAndRemember(Object.FindObjectsOfType<MovementModeToggleInput>(true), disabledBehaviours);
            DisableAndRemember(Object.FindObjectsOfType<InputActionManager>(true), disabledBehaviours);
            DisableAndRemember(Object.FindObjectsOfType<ControllerInputActionManager>(true), disabledBehaviours);
            DisableAndRemember(Object.FindObjectsOfType<LocomotionProvider>(true), disabledBehaviours);

            if (playerRig == null)
                return;

            XRBaseInteractor[] interactors = playerRig.GetComponentsInChildren<XRBaseInteractor>(true);
            for (int i = 0; i < interactors.Length; i++)
            {
                XRBaseInteractor interactor = interactors[i];
                if (interactor == null || !interactor.enabled || interactor is XRSocketInteractor)
                    continue;

                disabledBehaviours.Add(interactor);
                interactor.enabled = false;
            }
        }

        static void RestoreGameplayInteractions(List<Behaviour> disabledBehaviours)
        {
            for (int i = 0; i < disabledBehaviours.Count; i++)
            {
                Behaviour behaviour = disabledBehaviours[i];
                if (behaviour != null)
                    behaviour.enabled = true;
            }

            disabledBehaviours.Clear();
        }

        static void DisableAndRemember<T>(T[] behaviours, List<Behaviour> disabledBehaviours) where T : Behaviour
        {
            if (behaviours == null || disabledBehaviours == null)
                return;

            for (int i = 0; i < behaviours.Length; i++)
            {
                T behaviour = behaviours[i];
                if (behaviour == null || !behaviour.enabled)
                    continue;

                disabledBehaviours.Add(behaviour);
                behaviour.enabled = false;
            }
        }

        static void TryAddTrackedDeviceGraphicRaycaster(GameObject target)
        {
            if (target == null)
                return;

            if (target.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                target.AddComponent<TrackedDeviceGraphicRaycaster>();
        }
    }
}
