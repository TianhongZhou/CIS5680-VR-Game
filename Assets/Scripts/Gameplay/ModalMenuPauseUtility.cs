using CIS5680VRGame.Balls;
using CIS5680VRGame.UI;
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
        const float k_MinStableMenuYawSqrMagnitude = 0.16f;

        static bool s_IsPausedForMenu;
        static readonly List<Behaviour> s_DisabledBehaviours = new();
        static MovementModeManager s_LockedMovementModeManager;
        static bool s_IsLockedForTransition;
        static readonly List<Behaviour> s_TransitionDisabledBehaviours = new();
        static MovementModeManager s_TransitionLockedMovementModeManager;
        static bool s_HasLastStableMenuYaw;
        static Quaternion s_LastStableMenuYaw = Quaternion.identity;

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
            TeleportViewEffectService.ClearHeldOverlay();
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

        public static void RefreshWorldMenuPoseSafely(
            GameObject menuRoot,
            Camera menuCamera,
            Vector3? localOffset,
            Vector2 panelSize,
            LayerMask obstacleMask,
            float minimumDistance,
            float wallClearance)
        {
            if (menuRoot == null || menuCamera == null)
                return;

            Vector3 resolvedOffset = ResolveWorldMenuOffset(localOffset ?? WorldMenuLocalOffset);
            float targetDistance = Mathf.Max(resolvedOffset.z, minimumDistance);
            float safeMinimumDistance = Mathf.Clamp(minimumDistance, 0.55f, targetDistance);
            float safeClearance = Mathf.Max(0.05f, wallClearance);
            Quaternion baseYaw = ResolveYawOnlyRotation(menuCamera.transform);
            Vector3 cameraPosition = menuCamera.transform.position;
            Vector3 localOffsetWithoutDepth = new(resolvedOffset.x, resolvedOffset.y, 0f);
            float safeScaleMultiplier = 1f;

            if (!TryResolveSafeWorldMenuPoseWithScale(
                    menuRoot,
                    menuCamera,
                    cameraPosition,
                    baseYaw,
                    localOffsetWithoutDepth,
                    targetDistance,
                    safeMinimumDistance,
                    safeClearance,
                    panelSize,
                    obstacleMask,
                    out Vector3 safePosition,
                    out Quaternion safeRotation,
                    out safeScaleMultiplier))
            {
                safeScaleMultiplier = 0.68f;
                if (!TryResolveEmergencyWorldMenuPose(
                        menuRoot,
                        menuCamera,
                        cameraPosition,
                        baseYaw,
                        localOffsetWithoutDepth,
                        Mathf.Max(0.65f, safeMinimumDistance * safeScaleMultiplier),
                        safeClearance,
                        obstacleMask,
                        out safePosition,
                        out safeRotation))
                {
                    safeRotation = Quaternion.AngleAxis(-90f, Vector3.up) * baseYaw;
                    Vector3 fallbackDirection = safeRotation * Vector3.forward;
                    safePosition = cameraPosition + safeRotation * localOffsetWithoutDepth + fallbackDirection * Mathf.Max(0.65f, safeMinimumDistance * safeScaleMultiplier);
                }
            }

            menuRoot.transform.SetParent(null, false);
            menuRoot.transform.SetPositionAndRotation(safePosition, safeRotation);
            menuRoot.transform.localScale = Vector3.one * (WorldMenuUnitsPerPixel * safeScaleMultiplier);
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

        static Quaternion ResolveYawOnlyRotation(Transform source)
        {
            Vector3 forward = source != null ? source.forward : Vector3.forward;
            if (TryResolveStableYaw(forward, out Quaternion stableYaw))
                return stableYaw;

            if (source != null && TryResolveStableYaw(source.root.forward, out stableYaw))
                return stableYaw;

            if (s_HasLastStableMenuYaw)
                return s_LastStableMenuYaw;

            return Quaternion.identity;
        }

        static bool TryResolveStableYaw(Vector3 forward, out Quaternion yaw)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude < k_MinStableMenuYawSqrMagnitude)
            {
                yaw = Quaternion.identity;
                return false;
            }

            yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            s_LastStableMenuYaw = yaw;
            s_HasLastStableMenuYaw = true;
            return true;
        }

        static bool TryResolveSafeWorldMenuPoseWithScale(
            GameObject menuRoot,
            Camera menuCamera,
            Vector3 cameraPosition,
            Quaternion baseYaw,
            Vector3 localOffsetWithoutDepth,
            float targetDistance,
            float minimumDistance,
            float wallClearance,
            Vector2 panelSize,
            LayerMask obstacleMask,
            out Vector3 safePosition,
            out Quaternion safeRotation,
            out float safeScaleMultiplier)
        {
            float[] yawOffsets = { 0f, -32f, 32f, -64f, 64f, -105f, 105f, -145f, 145f, 180f };
            float[] scaleMultipliers = { 1f, 0.82f, 0.68f };

            for (int yawIndex = 0; yawIndex < yawOffsets.Length; yawIndex++)
            {
                Quaternion candidateYaw = Quaternion.AngleAxis(yawOffsets[yawIndex], Vector3.up) * baseYaw;
                for (int scaleIndex = 0; scaleIndex < scaleMultipliers.Length; scaleIndex++)
                {
                    float scaleMultiplier = scaleMultipliers[scaleIndex];
                    if (TryResolveSafeWorldMenuPose(
                            menuRoot,
                            menuCamera,
                            cameraPosition,
                            candidateYaw,
                            localOffsetWithoutDepth,
                            targetDistance,
                            Mathf.Max(0.65f, minimumDistance * scaleMultiplier),
                            wallClearance,
                            panelSize * scaleMultiplier,
                            obstacleMask,
                            out safePosition,
                            out safeRotation))
                    {
                        safeScaleMultiplier = scaleMultiplier;
                        return true;
                    }
                }
            }

            safePosition = Vector3.zero;
            safeRotation = baseYaw;
            safeScaleMultiplier = 1f;
            return false;
        }

        static bool TryResolveSafeWorldMenuPose(
            GameObject menuRoot,
            Camera menuCamera,
            Vector3 cameraPosition,
            Quaternion baseYaw,
            Vector3 localOffsetWithoutDepth,
            float targetDistance,
            float minimumDistance,
            float wallClearance,
            Vector2 panelSize,
            LayerMask obstacleMask,
            out Vector3 safePosition,
            out Quaternion safeRotation)
        {
            safePosition = Vector3.zero;
            safeRotation = baseYaw;

            float[] distanceMultipliers = { 1f, 0.82f, 0.68f, 0.52f, 0.45f };

            Quaternion candidateRotation = baseYaw;
            Vector3 candidateForward = candidateRotation * Vector3.forward;
            Vector3 candidateBaseOffset = candidateRotation * localOffsetWithoutDepth;

            for (int distanceIndex = 0; distanceIndex < distanceMultipliers.Length; distanceIndex++)
            {
                float requestedDistance = Mathf.Max(minimumDistance, targetDistance * distanceMultipliers[distanceIndex]);
                Vector3 requestedPosition = cameraPosition + candidateBaseOffset + candidateForward * requestedDistance;

                if (!TryClampMenuDistanceForObstacles(
                        menuRoot,
                        menuCamera,
                        cameraPosition,
                        requestedPosition,
                        minimumDistance,
                        wallClearance,
                        obstacleMask,
                        out Vector3 candidatePosition))
                {
                    continue;
                }

                if (MenuPanelOverlapsObstacle(
                        menuRoot,
                        menuCamera,
                        candidatePosition,
                        candidateRotation,
                        panelSize,
                        wallClearance,
                        obstacleMask))
                {
                    continue;
                }

                safePosition = candidatePosition;
                safeRotation = candidateRotation;
                return true;
            }

            return false;
        }

        static bool TryResolveEmergencyWorldMenuPose(
            GameObject menuRoot,
            Camera menuCamera,
            Vector3 cameraPosition,
            Quaternion baseYaw,
            Vector3 localOffsetWithoutDepth,
            float minimumDistance,
            float wallClearance,
            LayerMask obstacleMask,
            out Vector3 safePosition,
            out Quaternion safeRotation)
        {
            safePosition = Vector3.zero;
            safeRotation = baseYaw;

            float[] yawOffsets = { 0f, -32f, 32f, -64f, 64f, -105f, 105f, -145f, 145f, 180f };
            for (int yawIndex = 0; yawIndex < yawOffsets.Length; yawIndex++)
            {
                Quaternion candidateRotation = Quaternion.AngleAxis(yawOffsets[yawIndex], Vector3.up) * baseYaw;
                Vector3 candidateForward = candidateRotation * Vector3.forward;
                Vector3 candidateBaseOffset = candidateRotation * localOffsetWithoutDepth;
                Vector3 requestedPosition = cameraPosition + candidateBaseOffset + candidateForward * minimumDistance;

                if (!TryClampMenuDistanceForObstacles(
                        menuRoot,
                        menuCamera,
                        cameraPosition,
                        requestedPosition,
                        Mathf.Max(0.45f, minimumDistance * 0.72f),
                        wallClearance,
                        obstacleMask,
                        out Vector3 candidatePosition))
                {
                    continue;
                }

                safePosition = candidatePosition;
                safeRotation = candidateRotation;
                return true;
            }

            return false;
        }

        static bool TryClampMenuDistanceForObstacles(
            GameObject menuRoot,
            Camera menuCamera,
            Vector3 cameraPosition,
            Vector3 requestedPosition,
            float minimumDistance,
            float wallClearance,
            LayerMask obstacleMask,
            out Vector3 clampedPosition)
        {
            clampedPosition = requestedPosition;
            Vector3 toMenu = requestedPosition - cameraPosition;
            float castDistance = toMenu.magnitude;
            if (castDistance <= 0.001f)
                return false;

            Vector3 castDirection = toMenu / castDistance;
            float castRadius = Mathf.Clamp(wallClearance * 0.55f, 0.08f, 0.18f);
            if (CandidateStartsBlockedByObstacle(menuRoot, menuCamera, cameraPosition, castDirection, Mathf.Max(castRadius, wallClearance), obstacleMask))
                return false;

            RaycastHit[] hits = Physics.SphereCastAll(
                cameraPosition,
                castRadius,
                castDirection,
                castDistance + wallClearance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            float nearestBlockingDistance = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || ShouldIgnoreMenuPlacementCollider(menuRoot, menuCamera, hit.collider))
                    continue;

                nearestBlockingDistance = Mathf.Min(nearestBlockingDistance, hit.distance);
            }

            RaycastHit[] rayHits = Physics.RaycastAll(
                cameraPosition,
                castDirection,
                castDistance + wallClearance,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < rayHits.Length; i++)
            {
                RaycastHit hit = rayHits[i];
                if (hit.collider == null || ShouldIgnoreMenuPlacementCollider(menuRoot, menuCamera, hit.collider))
                    continue;

                nearestBlockingDistance = Mathf.Min(nearestBlockingDistance, hit.distance);
            }

            if (float.IsPositiveInfinity(nearestBlockingDistance))
                return true;

            float adjustedDistance = nearestBlockingDistance - wallClearance;
            if (adjustedDistance < minimumDistance)
                return false;

            float distanceRatio = Mathf.Clamp01(adjustedDistance / Mathf.Max(0.001f, castDistance));
            clampedPosition = Vector3.Lerp(cameraPosition, requestedPosition, distanceRatio);
            return true;
        }

        static bool CandidateStartsBlockedByObstacle(
            GameObject menuRoot,
            Camera menuCamera,
            Vector3 cameraPosition,
            Vector3 castDirection,
            float startProbeRadius,
            LayerMask obstacleMask)
        {
            Collider[] overlaps = Physics.OverlapSphere(
                cameraPosition,
                Mathf.Max(0.05f, startProbeRadius),
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider overlap = overlaps[i];
                if (overlap == null || ShouldIgnoreMenuPlacementCollider(menuRoot, menuCamera, overlap))
                    continue;

                Vector3 closestPoint = overlap.ClosestPoint(cameraPosition);
                Vector3 toObstacle = closestPoint - cameraPosition;
                float sqrDistance = toObstacle.sqrMagnitude;
                if (sqrDistance <= 0.000001f)
                {
                    Vector3 toBoundsCenter = overlap.bounds.center - cameraPosition;
                    toBoundsCenter = Vector3.ProjectOnPlane(toBoundsCenter, Vector3.up);
                    if (toBoundsCenter.sqrMagnitude <= 0.000001f)
                        continue;

                    if (Vector3.Dot(toBoundsCenter.normalized, castDirection) > 0.12f)
                        return true;

                    continue;
                }

                float alignment = Vector3.Dot(toObstacle / Mathf.Sqrt(sqrDistance), castDirection);
                if (alignment > 0.12f)
                    return true;
            }

            return false;
        }

        static bool MenuPanelOverlapsObstacle(
            GameObject menuRoot,
            Camera menuCamera,
            Vector3 position,
            Quaternion rotation,
            Vector2 panelSize,
            float wallClearance,
            LayerMask obstacleMask)
        {
            Vector3 halfExtents = new(
                Mathf.Max(0.24f, panelSize.x * WorldMenuUnitsPerPixel * 0.5f + 0.04f),
                Mathf.Max(0.2f, panelSize.y * WorldMenuUnitsPerPixel * 0.5f + 0.04f),
                Mathf.Clamp(wallClearance * 0.65f, 0.08f, 0.18f));

            Collider[] overlaps = Physics.OverlapBox(
                position,
                halfExtents,
                rotation,
                obstacleMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider overlap = overlaps[i];
                if (overlap == null || ShouldIgnoreMenuPlacementCollider(menuRoot, menuCamera, overlap))
                    continue;

                return true;
            }

            return false;
        }

        static bool ShouldIgnoreMenuPlacementCollider(GameObject menuRoot, Camera menuCamera, Collider collider)
        {
            if (collider == null)
                return true;

            if (collider.isTrigger)
                return true;

            Transform colliderTransform = collider.transform;
            if (menuRoot != null && (colliderTransform.IsChildOf(menuRoot.transform) || menuRoot.transform.IsChildOf(colliderTransform)))
                return true;

            if (menuCamera != null && colliderTransform.IsChildOf(menuCamera.transform.root))
                return true;

            if (collider.GetComponentInParent<Canvas>() != null)
                return true;

            return false;
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
