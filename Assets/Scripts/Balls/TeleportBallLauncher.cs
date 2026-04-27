using System;
using System.Collections.Generic;
using CIS5680VRGame.Gameplay;
using CIS5680VRGame.Progression;
using CIS5680VRGame.UI;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace CIS5680VRGame.Balls
{
    [DisallowMultipleComponent]
    public sealed class TeleportBallLauncher : MonoBehaviour
    {
        enum LauncherState
        {
            Idle,
            Aiming,
            ProjectileFlying,
            AnchorArming,
            AnchorReady,
        }

        readonly struct TrajectoryHit
        {
            public readonly bool HasHit;
            public readonly bool IsValidLanding;
            public readonly Vector3 Point;
            public readonly Vector3 Normal;

            public TrajectoryHit(bool hasHit, bool isValidLanding, Vector3 point, Vector3 normal)
            {
                HasHit = hasHit;
                IsValidLanding = isValidLanding;
                Point = point;
                Normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
            }
        }

        const string TutorialSceneName = "TutorialLevel";
        const string Maze1SceneName = "Maze1";
        const string RandomMazeSceneName = "random-maze";
        const string EnemyPrototypeSceneName = "EnemyPrototypePlayable";
        const float SpecialSurfaceHitTolerance = 0.08f;

        static bool s_Bootstrapped;
        static readonly Comparison<RaycastHit> s_HitDistanceComparison = (a, b) => a.distance.CompareTo(b.distance);

        [Header("References")]
        [SerializeField] XROrigin m_XROrigin;
        [SerializeField] Transform m_LaunchOrigin;
        [SerializeField] GameObject m_TeleportBallPrefab;

        [Header("Input")]
        [SerializeField] XRNode m_ControllerNode = XRNode.RightHand;

        [Header("Launch")]
        [SerializeField] float m_LaunchSpeed = 8.5f;
        [SerializeField] float m_LaunchForwardOffset = 0.18f;
        [SerializeField] float m_MaxPreviewTime = 2.2f;
        [SerializeField] float m_PreviewTimeStep = 0.045f;
        [SerializeField] float m_FallbackPreviewRadius = 0.12f;
        [SerializeField] LayerMask m_PreviewCollisionLayers = ~0;
        [SerializeField] float m_ProjectileLifetime = 8f;
        [SerializeField, Min(0f)] float m_AnchorArmingDuration = 0.72f;
        [SerializeField] float m_AnchorLifetime = 8f;
        [SerializeField, Min(0f)] float m_PostDisappearLaunchLockout = 0.55f;

        [Header("Preview")]
        [SerializeField] float m_LineWidth = 0.025f;
        [SerializeField] Color m_ValidPreviewColor = new(0.1f, 0.95f, 0.85f, 0.92f);
        [SerializeField] Color m_InvalidPreviewColor = new(1f, 0.22f, 0.16f, 0.82f);
        [SerializeField] Color m_ReadyAnchorColor = new(0.2f, 0.9f, 1f, 0.95f);
        [SerializeField] float m_ReadyAnchorBlinkFrequency = 2.6f;
        [SerializeField] float m_ReadyAnchorScalePulse = 0.07f;
        [SerializeField] Vector3 m_MarkerScale = new(0.48f, 0.012f, 0.48f);

        [Header("Anchor UI")]
        [SerializeField] Vector3 m_AnchorHintLocalOffset = new(0f, -0.72f, 1.65f);
        [SerializeField] Vector2 m_AnchorHintSize = new(760f, 118f);
        [SerializeField] float m_AnchorHintUnitsPerPixel = 0.00135f;
        [SerializeField] Color m_AnchorHintBackdropColor = new(0.02f, 0.05f, 0.07f, 0.34f);
        [SerializeField] Color m_AnchorHintTextColor = new(0.82f, 0.96f, 1f, 0.82f);

        [Header("Holsters")]
        [SerializeField] float m_FallbackHolsterDistance = 0.42f;
        [SerializeField] float m_FallbackHolsterHeightOffset = -0.72f;
        [SerializeField] float m_HolsterSideOffset = 0.24f;

        LauncherState m_State;
        bool m_PreviousPrimaryPressed;
        bool m_PreviousSecondaryPressed;
        bool m_CurrentPreviewValid;
        Vector3 m_CurrentLaunchPosition;
        Vector3 m_CurrentLaunchVelocity;
        GameObject m_ActiveProjectile;
        TeleportImpactEffect m_AnchorEffect;
        BallImpactContext m_AnchorContext;
        float m_ProjectileExpireTime;
        float m_AnchorReadyTime;
        float m_AnchorExpireTime;
        float m_LaunchLockedUntil;
        LineRenderer m_PreviewLine;
        GameObject m_PreviewMarker;
        Renderer m_PreviewMarkerRenderer;
        Material m_RuntimeMaterial;
        GameObject m_AnchorHintRoot;
        GameObject m_AnchorHintActionsRoot;
        TextMeshProUGUI m_AnchorHintLabel;
        Image m_AnchorHintBackdrop;
        Sprite m_ButtonPromptSprite;
        Texture2D m_ButtonPromptTexture;
        int m_HolsterArrangePassesRemaining;
        readonly List<Vector3> m_PreviewPoints = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (s_Bootstrapped)
                return;

            s_Bootstrapped = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            InstallForScene(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
        {
            InstallForScene(scene);
        }

        static void InstallForScene(Scene scene)
        {
            DisableJumpProviders();

            if (!ShouldInstallInScene(scene.name))
                return;

            XROrigin xrOrigin = FindObjectOfType<XROrigin>();
            if (xrOrigin == null || xrOrigin.GetComponent<TeleportBallLauncher>() != null)
                return;

            TeleportBallLauncher launcher = xrOrigin.gameObject.AddComponent<TeleportBallLauncher>();
            launcher.m_XROrigin = xrOrigin;
            launcher.ResolveReferences();
            launcher.ResolveTeleportPrefabFromHolsters();
            launcher.DisableTeleportHolsters();
            launcher.QueueHolsterArrangePasses();
            launcher.ArrangeRemainingHolsters();
        }

        static bool ShouldInstallInScene(string sceneName)
        {
            return string.Equals(sceneName, TutorialSceneName, StringComparison.Ordinal)
                || string.Equals(sceneName, Maze1SceneName, StringComparison.Ordinal)
                || string.Equals(sceneName, RandomMazeSceneName, StringComparison.Ordinal)
                || string.Equals(sceneName, EnemyPrototypeSceneName, StringComparison.Ordinal);
        }

        static void DisableJumpProviders()
        {
            LocomotionProvider[] providers = FindObjectsOfType<LocomotionProvider>(true);
            for (int i = 0; i < providers.Length; i++)
            {
                LocomotionProvider provider = providers[i];
                if (provider == null)
                    continue;

                bool isJumpProvider = string.Equals(provider.gameObject.name, "Jump", StringComparison.OrdinalIgnoreCase)
                    || provider.GetType().Name.IndexOf("Jump", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isJumpProvider)
                    continue;

                provider.enabled = false;
                provider.gameObject.SetActive(false);
            }
        }

        public static bool TryRegisterAnchor(TeleportImpactEffect teleportEffect, in BallImpactContext context)
        {
            if (teleportEffect == null || context.BallObject == null)
                return false;

            TeleportBallLauncher[] launchers = FindObjectsOfType<TeleportBallLauncher>(true);
            for (int i = 0; i < launchers.Length; i++)
            {
                TeleportBallLauncher launcher = launchers[i];
                if (launcher == null || !launcher.enabled || !launcher.gameObject.activeInHierarchy)
                    continue;

                if (launcher.TryRegisterAnchorInternal(teleportEffect, context))
                    return true;
            }

            return false;
        }

        void Awake()
        {
            ResolveReferences();
            ResolveTeleportPrefabFromHolsters();
            DisableJumpProviders();
            DisableTeleportHolsters();
            QueueHolsterArrangePasses();
            ArrangeRemainingHolsters();
        }

        void OnEnable()
        {
            ResolveReferences();
            SetPreviewVisible(false);
            SetAnchorHintVisible(false);
            QueueHolsterArrangePasses();
        }

        void OnDisable()
        {
            SetPreviewVisible(false);
            SetAnchorHintVisible(false);
            m_PreviousPrimaryPressed = false;
            m_PreviousSecondaryPressed = false;
            if (m_State == LauncherState.Aiming)
                m_State = LauncherState.Idle;
        }

        void OnDestroy()
        {
            if (m_RuntimeMaterial != null)
                Destroy(m_RuntimeMaterial);

            if (m_AnchorHintRoot != null)
                Destroy(m_AnchorHintRoot);

            if (m_ButtonPromptSprite != null)
                Destroy(m_ButtonPromptSprite);

            if (m_ButtonPromptTexture != null)
                Destroy(m_ButtonPromptTexture);
        }

        void Update()
        {
            ResolveReferences();
            ResolveTeleportPrefabFromHolsters();
            DisableJumpProviders();
            DisableTeleportHolsters();
            if (m_HolsterArrangePassesRemaining > 0)
            {
                ArrangeRemainingHolsters();
                m_HolsterArrangePassesRemaining--;
            }

            bool primaryPressed = ReadButton(CommonUsages.primaryButton);
            bool secondaryPressed = ReadButton(CommonUsages.secondaryButton);
            bool primaryDown = primaryPressed && !m_PreviousPrimaryPressed;
            bool primaryUp = !primaryPressed && m_PreviousPrimaryPressed;
            bool secondaryDown = secondaryPressed && !m_PreviousSecondaryPressed;

            if (secondaryDown)
                CancelActiveTeleport();

            switch (m_State)
            {
                case LauncherState.Idle:
                    if (primaryDown)
                        BeginAiming();
                    break;

                case LauncherState.Aiming:
                    UpdateAimPreview();
                    if (primaryUp)
                        ReleaseAimedTeleport();
                    break;

                case LauncherState.ProjectileFlying:
                    if (m_ActiveProjectile == null || Time.time >= m_ProjectileExpireTime)
                        CancelActiveTeleport();
                    break;

                case LauncherState.AnchorArming:
                    if (m_ActiveProjectile == null)
                    {
                        CancelActiveTeleport();
                    }
                    else if (Time.time >= m_AnchorReadyTime)
                    {
                        CompleteAnchorArming();
                    }
                    break;

                case LauncherState.AnchorReady:
                    RefreshAnchorHintUI();
                    if (primaryDown)
                        ConfirmAnchor();
                    else if (Time.time >= m_AnchorExpireTime)
                        CancelActiveTeleport();
                    break;
            }

            m_PreviousPrimaryPressed = primaryPressed;
            m_PreviousSecondaryPressed = secondaryPressed;
        }

        void BeginAiming()
        {
            if (m_TeleportBallPrefab == null || m_LaunchOrigin == null)
                return;

            if (IsTeleportLaunchLocked())
                return;

            m_State = LauncherState.Aiming;
            EnsurePreviewObjects();
            SetPreviewVisible(true);
            UpdateAimPreview();
        }

        void UpdateAimPreview()
        {
            if (m_State != LauncherState.Aiming)
                return;

            m_CurrentPreviewValid = BuildPreviewTrajectory(out TrajectoryHit hit);
            Color previewColor = m_CurrentPreviewValid ? m_ValidPreviewColor : m_InvalidPreviewColor;
            ApplyPreviewColor(previewColor);

            if (m_PreviewMarker != null)
            {
                bool showMarker = hit.HasHit;
                m_PreviewMarker.SetActive(showMarker);
                if (showMarker)
                {
                    m_PreviewMarker.transform.position = hit.Point + hit.Normal * 0.015f;
                    m_PreviewMarker.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.Normal);
                    m_PreviewMarker.transform.localScale = m_MarkerScale;
                }
            }
        }

        void ReleaseAimedTeleport()
        {
            if (m_State != LauncherState.Aiming)
                return;

            SetPreviewVisible(false);

            if (!m_CurrentPreviewValid)
            {
                m_State = LauncherState.Idle;
                return;
            }

            LaunchProjectile();
        }

        void LaunchProjectile()
        {
            if (m_TeleportBallPrefab == null)
            {
                m_State = LauncherState.Idle;
                return;
            }

            if (IsTeleportLaunchLocked())
            {
                m_State = LauncherState.Idle;
                return;
            }

            CancelActiveProjectileOnly();

            GameObject projectile = Instantiate(
                m_TeleportBallPrefab,
                m_CurrentLaunchPosition,
                Quaternion.LookRotation(m_CurrentLaunchVelocity.normalized, Vector3.up));
            m_ActiveProjectile = projectile;

            TeleportImpactEffect teleportEffect = projectile.GetComponent<TeleportImpactEffect>();
            if (teleportEffect != null)
                ApplyProfileUpgradeSettings(teleportEffect);

            ThrowableBall throwableBall = projectile.GetComponent<ThrowableBall>();
            if (throwableBall != null)
                throwableBall.LaunchFromAbility(m_CurrentLaunchVelocity, Vector3.zero);
            else if (projectile.TryGetComponent(out Rigidbody rigidbody))
            {
                rigidbody.isKinematic = false;
                rigidbody.useGravity = true;
                rigidbody.detectCollisions = true;
#if UNITY_2023_3_OR_NEWER
                rigidbody.linearVelocity = m_CurrentLaunchVelocity;
#else
                rigidbody.velocity = m_CurrentLaunchVelocity;
#endif
            }

            m_ProjectileExpireTime = Time.time + Mathf.Max(1f, m_ProjectileLifetime);
            m_State = LauncherState.ProjectileFlying;
        }

        bool TryRegisterAnchorInternal(TeleportImpactEffect teleportEffect, in BallImpactContext context)
        {
            if (m_State != LauncherState.ProjectileFlying || m_ActiveProjectile == null)
                return false;

            if (context.BallObject != m_ActiveProjectile)
                return false;

            TeleportBallVisualController visualController = m_ActiveProjectile.GetComponent<TeleportBallVisualController>();
            Vector3 anchorNormal = context.HitNormal.sqrMagnitude > 0.0001f ? context.HitNormal.normalized : Vector3.up;
            Vector3 anchorPoint = visualController != null
                ? visualController.ResolveAlignedAnchorPoint(context.HitPoint, anchorNormal)
                : context.HitPoint;

            m_AnchorEffect = teleportEffect;
            m_AnchorContext = CreateAlignedAnchorContext(context, anchorPoint, anchorNormal);
            float armingDuration = Mathf.Max(0f, m_AnchorArmingDuration);
            m_AnchorReadyTime = Time.time + armingDuration;
            m_AnchorExpireTime = m_AnchorReadyTime + Mathf.Max(1f, m_AnchorLifetime);
            m_State = armingDuration > 0.01f ? LauncherState.AnchorArming : LauncherState.AnchorReady;

            ThrowableBall throwableBall = m_ActiveProjectile.GetComponent<ThrowableBall>();
            if (throwableBall != null)
                throwableBall.FreezeAsAnchor();

            ApplyAnchorVisual(m_ActiveProjectile, anchorPoint, anchorNormal, m_AnchorReadyTime, m_AnchorExpireTime);

            if (m_State == LauncherState.AnchorReady)
            {
                CompleteAnchorArming();
            }
            else
            {
                SetAnchorHintVisible(false);
            }
            return true;
        }

        static BallImpactContext CreateAlignedAnchorContext(in BallImpactContext context, Vector3 anchorPoint, Vector3 anchorNormal)
        {
            return context.Collision != null
                ? new BallImpactContext(anchorPoint, anchorNormal, context.BallObject, context.Collision)
                : new BallImpactContext(anchorPoint, anchorNormal, context.BallObject, context.HitCollider);
        }

        void CompleteAnchorArming()
        {
            if (m_State != LauncherState.AnchorArming && m_State != LauncherState.AnchorReady)
                return;

            if (m_ActiveProjectile != null
                && m_ActiveProjectile.TryGetComponent(out TeleportBallVisualController visualController))
            {
                visualController.CompleteAnchorReady();
            }

            m_State = LauncherState.AnchorReady;
            SetAnchorHintVisible(true);
            RefreshAnchorHintUI();
        }

        void ConfirmAnchor()
        {
            if (m_State != LauncherState.AnchorReady || m_AnchorEffect == null)
                return;

            m_AnchorEffect.ConfirmAnchoredTeleport(m_AnchorContext);
            CancelActiveTeleport();
        }

        void CancelActiveTeleport()
        {
            SetPreviewVisible(false);
            SetAnchorHintVisible(false);
            CancelActiveProjectileOnly();
            m_State = LauncherState.Idle;
            m_AnchorEffect = null;
            m_AnchorContext = default;
        }

        void CancelActiveProjectileOnly()
        {
            if (m_ActiveProjectile != null)
            {
                m_LaunchLockedUntil = Mathf.Max(
                    m_LaunchLockedUntil,
                    Time.time + Mathf.Max(0f, m_PostDisappearLaunchLockout));

                if (m_ActiveProjectile.TryGetComponent(out TeleportBallVisualController visualController))
                {
                    ResolveProjectileDisappearPose(m_ActiveProjectile, out Vector3 effectPoint, out Vector3 effectNormal);
                    visualController.BeginDisappear(false, effectPoint, effectNormal);
                }
                else
                {
                    BallFadeOut.Begin(m_ActiveProjectile, 0f, 0.38f);
                }
            }

            m_ActiveProjectile = null;
        }

        bool IsTeleportLaunchLocked()
        {
            return m_ActiveProjectile != null
                || Time.time < m_LaunchLockedUntil
                || m_State == LauncherState.ProjectileFlying
                || m_State == LauncherState.AnchorArming
                || m_State == LauncherState.AnchorReady;
        }

        void ResolveProjectileDisappearPose(GameObject projectile, out Vector3 effectPoint, out Vector3 effectNormal)
        {
            effectPoint = projectile != null ? projectile.transform.position : Vector3.zero;
            effectNormal = Vector3.up;

            if (m_AnchorContext.BallObject == projectile)
            {
                effectPoint = m_AnchorContext.HitPoint;
                effectNormal = m_AnchorContext.HitNormal.sqrMagnitude > 0.0001f
                    ? m_AnchorContext.HitNormal.normalized
                    : Vector3.up;
            }
        }

        bool BuildPreviewTrajectory(out TrajectoryHit hit)
        {
            hit = default;
            m_PreviewPoints.Clear();

            if (m_LaunchOrigin == null)
                return false;

            m_CurrentLaunchPosition = m_LaunchOrigin.position + m_LaunchOrigin.forward * Mathf.Max(0f, m_LaunchForwardOffset);
            m_CurrentLaunchVelocity = m_LaunchOrigin.forward.normalized * Mathf.Max(0.1f, m_LaunchSpeed);

            Vector3 previousPoint = m_CurrentLaunchPosition;
            m_PreviewPoints.Add(previousPoint);

            float step = Mathf.Max(0.01f, m_PreviewTimeStep);
            int maxSteps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Max(step, m_MaxPreviewTime) / step));
            for (int i = 1; i <= maxSteps; i++)
            {
                float time = i * step;
                Vector3 nextPoint = m_CurrentLaunchPosition
                    + m_CurrentLaunchVelocity * time
                    + 0.5f * Physics.gravity * time * time;

                if (TryResolveSegmentHit(previousPoint, nextPoint, out hit))
                {
                    m_PreviewPoints.Add(hit.Point);
                    RefreshPreviewLine();
                    return hit.IsValidLanding;
                }

                m_PreviewPoints.Add(nextPoint);
                previousPoint = nextPoint;
            }

            RefreshPreviewLine();
            return false;
        }

        bool TryResolveSegmentHit(Vector3 start, Vector3 end, out TrajectoryHit hit)
        {
            hit = default;
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return false;

            RaycastHit[] hits = Physics.SphereCastAll(
                start,
                ResolvePreviewRadius(),
                delta / distance,
                distance,
                m_PreviewCollisionLayers,
                QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return false;

            Array.Sort(hits, s_HitDistanceComparison);
            RaycastHit? firstBlockingHit = null;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit targetHit = hits[i];
                Collider targetCollider = targetHit.collider;
                if (ShouldIgnoreHit(targetCollider))
                    continue;

                if (firstBlockingHit.HasValue && targetHit.distance > firstBlockingHit.Value.distance + SpecialSurfaceHitTolerance)
                    break;

                if (TryResolveValidLanding(targetHit, out Vector3 landingPoint, out Vector3 landingNormal))
                {
                    hit = new TrajectoryHit(true, true, landingPoint, landingNormal);
                    return true;
                }

                if (!targetCollider.isTrigger && !firstBlockingHit.HasValue)
                    firstBlockingHit = targetHit;
            }

            if (firstBlockingHit.HasValue)
            {
                RaycastHit blocked = firstBlockingHit.Value;
                hit = new TrajectoryHit(true, false, blocked.point, blocked.normal);
                return true;
            }

            return false;
        }

        bool TryResolveValidLanding(RaycastHit hit, out Vector3 point, out Vector3 normal)
        {
            point = hit.point;
            normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;

            if (BallLandingSurface.TryGet(hit.collider, out BallLandingSurface landingSurface))
            {
                normal = landingSurface.SurfaceNormal;
                point = landingSurface.ResolveHitPoint(hit.point);
                return true;
            }

            ThrowableBall prototype = ResolveThrowablePrototype();
            LayerMask validLayers = prototype != null ? prototype.ValidGroundLayers : LayerMask.GetMask("Default");
            float minGroundUpDot = prototype != null ? prototype.MinGroundUpDot : 0.6f;
            bool requireGroundContact = prototype == null || prototype.RequireGroundContact;

            bool validLayer = ((1 << hit.collider.gameObject.layer) & validLayers.value) != 0;
            if (!validLayer)
                return false;

            return !requireGroundContact || Vector3.Dot(normal, Vector3.up) >= minGroundUpDot;
        }

        void RefreshPreviewLine()
        {
            EnsurePreviewObjects();
            if (m_PreviewLine == null)
                return;

            m_PreviewLine.positionCount = m_PreviewPoints.Count;
            for (int i = 0; i < m_PreviewPoints.Count; i++)
            {
                m_PreviewLine.SetPosition(i, m_PreviewPoints[i]);
            }
        }

        void EnsurePreviewObjects()
        {
            if (m_PreviewLine == null)
            {
                GameObject lineObject = new("TeleportTrajectoryPreview");
                lineObject.transform.SetParent(transform, false);
                m_PreviewLine = lineObject.AddComponent<LineRenderer>();
                m_PreviewLine.useWorldSpace = true;
                m_PreviewLine.widthMultiplier = Mathf.Max(0.005f, m_LineWidth);
                m_PreviewLine.numCornerVertices = 6;
                m_PreviewLine.numCapVertices = 6;
                m_PreviewLine.material = ResolveRuntimeMaterial();
            }

            if (m_PreviewMarker == null)
            {
                m_PreviewMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                m_PreviewMarker.name = "TeleportLandingPreview";
                m_PreviewMarker.transform.SetParent(transform, false);
                Collider markerCollider = m_PreviewMarker.GetComponent<Collider>();
                if (markerCollider != null)
                    Destroy(markerCollider);

                m_PreviewMarkerRenderer = m_PreviewMarker.GetComponent<Renderer>();
                if (m_PreviewMarkerRenderer != null)
                    m_PreviewMarkerRenderer.material = ResolveRuntimeMaterial();
            }
        }

        void ApplyPreviewColor(Color color)
        {
            if (m_PreviewLine != null)
            {
                m_PreviewLine.startColor = color;
                m_PreviewLine.endColor = color;
            }

            if (m_PreviewMarkerRenderer != null)
                m_PreviewMarkerRenderer.material.color = color;
        }

        void SetPreviewVisible(bool visible)
        {
            if (m_PreviewLine != null)
                m_PreviewLine.gameObject.SetActive(visible);

            if (m_PreviewMarker != null)
                m_PreviewMarker.SetActive(visible && m_CurrentPreviewValid);
        }

        void ApplyAnchorVisual(GameObject anchorObject, Vector3 hitPoint, Vector3 hitNormal, float anchorReadyTime, float anchorExpireTime)
        {
            if (anchorObject == null)
                return;

            TeleportBallVisualController visualController = anchorObject.GetComponent<TeleportBallVisualController>();
            if (visualController != null)
            {
                visualController.EnterAnchorArming(hitPoint, hitNormal, anchorReadyTime, anchorExpireTime);
                return;
            }

            Renderer[] renderers = anchorObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null)
                    continue;

                Material material = targetRenderer.material;
                if (material == null)
                    continue;

                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", m_ReadyAnchorColor);

                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", m_ReadyAnchorColor);
            }

            TeleportAnchorBlinkEffect blinkEffect = anchorObject.GetComponent<TeleportAnchorBlinkEffect>();
            if (blinkEffect == null)
                blinkEffect = anchorObject.AddComponent<TeleportAnchorBlinkEffect>();

            blinkEffect.Configure(
                m_ReadyAnchorColor,
                m_ReadyAnchorBlinkFrequency,
                m_ReadyAnchorScalePulse);
        }

        Material ResolveRuntimeMaterial()
        {
            if (m_RuntimeMaterial != null)
                return m_RuntimeMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Hidden/Internal-Colored");

            m_RuntimeMaterial = new Material(shader);
            m_RuntimeMaterial.color = m_ValidPreviewColor;
            return m_RuntimeMaterial;
        }

        float ResolvePreviewRadius()
        {
            ThrowableBall prototype = ResolveThrowablePrototype();
            if (prototype != null)
                return Mathf.Max(0.02f, prototype.CollisionRadius);

            return Mathf.Max(0.02f, m_FallbackPreviewRadius);
        }

        ThrowableBall ResolveThrowablePrototype()
        {
            if (m_TeleportBallPrefab == null)
                return null;

            return m_TeleportBallPrefab.GetComponent<ThrowableBall>();
        }

        bool ShouldIgnoreHit(Collider targetCollider)
        {
            if (targetCollider == null)
                return true;

            if (m_XROrigin != null && targetCollider.transform.IsChildOf(m_XROrigin.transform))
                return true;

            if (m_ActiveProjectile != null && targetCollider.transform.IsChildOf(m_ActiveProjectile.transform))
                return true;

            return false;
        }

        void ResolveReferences()
        {
            if (m_XROrigin == null)
                m_XROrigin = GetComponent<XROrigin>() != null ? GetComponent<XROrigin>() : FindObjectOfType<XROrigin>();

            if (m_LaunchOrigin == null && m_XROrigin != null)
            {
                Transform rightController = FindChildByName(m_XROrigin.transform, "Right Controller");
                if (rightController != null)
                    m_LaunchOrigin = rightController;
            }

            if (m_LaunchOrigin == null && Camera.main != null)
                m_LaunchOrigin = Camera.main.transform;
        }

        void ResolveTeleportPrefabFromHolsters()
        {
            if (m_TeleportBallPrefab != null)
                return;

            BallHolsterSlot[] holsterSlots = FindObjectsOfType<BallHolsterSlot>(true);
            for (int i = 0; i < holsterSlots.Length; i++)
            {
                BallHolsterSlot holsterSlot = holsterSlots[i];
                if (holsterSlot == null || holsterSlot.BallType != BallType.Teleport)
                    continue;

                if (holsterSlot.ThrowableBallPrefab == null)
                    continue;

                m_TeleportBallPrefab = holsterSlot.ThrowableBallPrefab;
                return;
            }
        }

        void DisableTeleportHolsters()
        {
            BallHolsterSlot[] holsterSlots = FindObjectsOfType<BallHolsterSlot>(true);
            for (int i = 0; i < holsterSlots.Length; i++)
            {
                BallHolsterSlot holsterSlot = holsterSlots[i];
                if (holsterSlot == null || holsterSlot.BallType != BallType.Teleport)
                    continue;

                holsterSlot.gameObject.SetActive(false);
            }
        }

        void QueueHolsterArrangePasses(int passCount = 8)
        {
            m_HolsterArrangePassesRemaining = Mathf.Max(m_HolsterArrangePassesRemaining, passCount);
        }

        void ArrangeRemainingHolsters()
        {
            BallHolsterSlot[] holsterSlots = FindObjectsOfType<BallHolsterSlot>(true);
            List<BallHolsterSlot> fallbackSlots = null;

            for (int i = 0; i < holsterSlots.Length; i++)
            {
                BallHolsterSlot holsterSlot = holsterSlots[i];
                if (holsterSlot == null || holsterSlot.BallType == BallType.Teleport)
                    continue;

                switch (holsterSlot.BallType)
                {
                    case BallType.Sonar:
                        ApplyHolsterOffset(holsterSlot, -Mathf.Abs(m_HolsterSideOffset));
                        break;

                    case BallType.StickyPulse:
                        ApplyHolsterOffset(holsterSlot, Mathf.Abs(m_HolsterSideOffset));
                        break;

                    default:
                        fallbackSlots ??= new List<BallHolsterSlot>();
                        fallbackSlots.Add(holsterSlot);
                        break;
                }
            }

            if (fallbackSlots == null)
                return;

            for (int i = 0; i < fallbackSlots.Count; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                ApplyHolsterOffset(fallbackSlots[i], side * Mathf.Abs(m_HolsterSideOffset));
            }
        }

        void ApplyHolsterOffset(BallHolsterSlot holsterSlot, float rightOffset)
        {
            if (holsterSlot == null)
                return;

            YawOnlyFollow follower = holsterSlot.GetComponent<YawOnlyFollow>();
            if (follower != null)
            {
                follower.ApplyRightOffset(rightOffset);
                return;
            }

            Camera headCamera = ResolvePlayerCamera();
            if (headCamera == null)
                return;

            Vector3 flatForward = Vector3.ProjectOnPlane(headCamera.transform.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;

            Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;
            holsterSlot.transform.position = headCamera.transform.position
                + flatForward * Mathf.Max(0f, m_FallbackHolsterDistance)
                + flatRight * rightOffset
                + Vector3.up * m_FallbackHolsterHeightOffset;
            holsterSlot.transform.rotation = Quaternion.LookRotation(flatForward, Vector3.up);
        }

        void EnsureAnchorHintUI()
        {
            if (m_AnchorHintRoot != null && m_AnchorHintLabel != null && m_AnchorHintActionsRoot != null)
                return;

            Camera hintCamera = ResolvePlayerCamera();
            if (hintCamera == null)
                return;

            m_AnchorHintRoot = new GameObject("TeleportAnchorHint", typeof(RectTransform));
            RectTransform rootRect = m_AnchorHintRoot.GetComponent<RectTransform>();
            rootRect.sizeDelta = m_AnchorHintSize;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            Canvas canvas = m_AnchorHintRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = hintCamera;
            canvas.sortingOrder = 5200;

            CanvasScaler scaler = m_AnchorHintRoot.AddComponent<CanvasScaler>();
            ModalMenuPauseUtility.ConfigureWorldSpaceCanvasScaler(scaler);

            GameObject panel = ModalMenuPauseUtility.CreateUIObject("Panel", m_AnchorHintRoot.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            m_AnchorHintBackdrop = panel.AddComponent<Image>();
            m_AnchorHintBackdrop.color = m_AnchorHintBackdropColor;
            m_AnchorHintBackdrop.raycastTarget = false;

            VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(24, 24, 12, 12);
            panelLayout.spacing = 4f;
            panelLayout.childAlignment = TextAnchor.MiddleCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            GameObject labelObject = ModalMenuPauseUtility.CreateUIObject("CountdownLabel", panel.transform);
            LayoutElement labelLayout = labelObject.AddComponent<LayoutElement>();
            labelLayout.preferredHeight = 42f;

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();

            m_AnchorHintLabel = labelObject.AddComponent<TextMeshProUGUI>();
            m_AnchorHintLabel.font = ModalMenuPauseUtility.ResolveFontAsset();
            m_AnchorHintLabel.fontSize = 32f;
            m_AnchorHintLabel.fontSizeMax = 32f;
            m_AnchorHintLabel.fontSizeMin = 18f;
            m_AnchorHintLabel.enableAutoSizing = true;
            m_AnchorHintLabel.fontStyle = FontStyles.Bold;
            m_AnchorHintLabel.alignment = TextAlignmentOptions.Center;
            m_AnchorHintLabel.color = m_AnchorHintTextColor;
            m_AnchorHintLabel.raycastTarget = false;

            m_AnchorHintActionsRoot = ModalMenuPauseUtility.CreateUIObject("Actions", panel.transform);
            LayoutElement actionsLayout = m_AnchorHintActionsRoot.AddComponent<LayoutElement>();
            actionsLayout.preferredHeight = 44f;

            HorizontalLayoutGroup actionsGroup = m_AnchorHintActionsRoot.AddComponent<HorizontalLayoutGroup>();
            actionsGroup.spacing = 26f;
            actionsGroup.childAlignment = TextAnchor.MiddleCenter;
            actionsGroup.childControlWidth = true;
            actionsGroup.childControlHeight = true;
            actionsGroup.childForceExpandWidth = false;
            actionsGroup.childForceExpandHeight = false;

            CreateAnchorActionPrompt(m_AnchorHintActionsRoot.transform, "A", "Teleport");
            CreateAnchorActionPrompt(m_AnchorHintActionsRoot.transform, "B", "Cancel");

            RefreshAnchorHintPose();
            m_AnchorHintRoot.SetActive(false);
        }

        void RefreshAnchorHintUI()
        {
            if (m_State != LauncherState.AnchorReady)
            {
                SetAnchorHintVisible(false);
                return;
            }

            EnsureAnchorHintUI();
            if (m_AnchorHintRoot == null || m_AnchorHintLabel == null)
                return;

            RefreshAnchorHintPose();
            if (m_AnchorHintBackdrop != null)
                m_AnchorHintBackdrop.color = m_AnchorHintBackdropColor;

            ApplyAnchorHintColors();
            float remainingSeconds = Mathf.Max(0f, m_AnchorExpireTime - Time.time);
            m_AnchorHintLabel.text = $"Teleport ready: {remainingSeconds:0.0}s";
            SetAnchorHintVisible(true);
        }

        void CreateAnchorActionPrompt(Transform parent, string buttonText, string actionText)
        {
            GameObject prompt = ModalMenuPauseUtility.CreateUIObject($"{buttonText}Prompt", parent);
            LayoutElement promptLayout = prompt.AddComponent<LayoutElement>();
            promptLayout.preferredWidth = 210f;
            promptLayout.preferredHeight = 40f;

            HorizontalLayoutGroup promptGroup = prompt.AddComponent<HorizontalLayoutGroup>();
            promptGroup.spacing = 10f;
            promptGroup.childAlignment = TextAnchor.MiddleCenter;
            promptGroup.childControlWidth = true;
            promptGroup.childControlHeight = true;
            promptGroup.childForceExpandWidth = false;
            promptGroup.childForceExpandHeight = false;

            GameObject badge = ModalMenuPauseUtility.CreateUIObject($"{buttonText}Button", prompt.transform);
            LayoutElement badgeLayout = badge.AddComponent<LayoutElement>();
            badgeLayout.preferredWidth = 38f;
            badgeLayout.preferredHeight = 38f;
            badgeLayout.minWidth = 38f;
            badgeLayout.minHeight = 38f;

            Image badgeImage = badge.AddComponent<Image>();
            badgeImage.sprite = ResolveButtonPromptSprite();
            badgeImage.color = m_AnchorHintTextColor;
            badgeImage.raycastTarget = false;
            badgeImage.preserveAspect = true;

            GameObject badgeTextObject = ModalMenuPauseUtility.CreateUIObject("Letter", badge.transform);
            RectTransform badgeTextRect = badgeTextObject.GetComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.offsetMin = Vector2.zero;
            badgeTextRect.offsetMax = Vector2.zero;

            TextMeshProUGUI badgeText = badgeTextObject.AddComponent<TextMeshProUGUI>();
            badgeText.font = ModalMenuPauseUtility.ResolveFontAsset();
            badgeText.text = buttonText;
            badgeText.fontSize = 25f;
            badgeText.fontSizeMax = 25f;
            badgeText.fontSizeMin = 18f;
            badgeText.enableAutoSizing = true;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = m_AnchorHintTextColor;
            badgeText.raycastTarget = false;

            GameObject actionTextObject = ModalMenuPauseUtility.CreateUIObject("Action", prompt.transform);
            LayoutElement actionLayout = actionTextObject.AddComponent<LayoutElement>();
            actionLayout.preferredWidth = 140f;
            actionLayout.preferredHeight = 38f;

            TextMeshProUGUI actionLabel = actionTextObject.AddComponent<TextMeshProUGUI>();
            actionLabel.font = ModalMenuPauseUtility.ResolveFontAsset();
            actionLabel.text = actionText;
            actionLabel.fontSize = 28f;
            actionLabel.fontSizeMax = 28f;
            actionLabel.fontSizeMin = 18f;
            actionLabel.enableAutoSizing = true;
            actionLabel.fontStyle = FontStyles.Bold;
            actionLabel.alignment = TextAlignmentOptions.Left;
            actionLabel.color = m_AnchorHintTextColor;
            actionLabel.raycastTarget = false;
        }

        void ApplyAnchorHintColors()
        {
            if (m_AnchorHintRoot == null)
                return;

            TextMeshProUGUI[] textElements = m_AnchorHintRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < textElements.Length; i++)
            {
                if (textElements[i] != null)
                    textElements[i].color = m_AnchorHintTextColor;
            }

            Image[] images = m_AnchorHintRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || image == m_AnchorHintBackdrop)
                    continue;

                image.color = m_AnchorHintTextColor;
            }
        }

        Sprite ResolveButtonPromptSprite()
        {
            if (m_ButtonPromptSprite != null)
                return m_ButtonPromptSprite;

            const int size = 64;
            const float radius = 27f;
            const float halfThickness = 2.7f;
            Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
            m_ButtonPromptTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float ringDistance = Mathf.Abs(distance - radius);
                    float alpha = Mathf.Clamp01(halfThickness + 1.15f - ringDistance);
                    m_ButtonPromptTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            m_ButtonPromptTexture.Apply(false, true);
            m_ButtonPromptSprite = Sprite.Create(
                m_ButtonPromptTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            m_ButtonPromptSprite.hideFlags = HideFlags.HideAndDontSave;
            return m_ButtonPromptSprite;
        }

        void RefreshAnchorHintPose()
        {
            if (m_AnchorHintRoot == null)
                return;

            Camera hintCamera = ResolvePlayerCamera();
            if (hintCamera == null)
                return;

            Canvas canvas = m_AnchorHintRoot.GetComponent<Canvas>();
            if (canvas != null)
                canvas.worldCamera = hintCamera;

            m_AnchorHintRoot.transform.SetParent(hintCamera.transform, false);
            m_AnchorHintRoot.transform.localPosition = m_AnchorHintLocalOffset;
            m_AnchorHintRoot.transform.localRotation = Quaternion.identity;
            m_AnchorHintRoot.transform.localScale = Vector3.one * Mathf.Max(0.0001f, m_AnchorHintUnitsPerPixel);
        }

        void SetAnchorHintVisible(bool visible)
        {
            if (visible)
                EnsureAnchorHintUI();

            if (m_AnchorHintRoot != null)
                m_AnchorHintRoot.SetActive(visible);
        }

        Camera ResolvePlayerCamera()
        {
            if (m_XROrigin != null && m_XROrigin.Camera != null)
                return m_XROrigin.Camera;

            return Camera.main;
        }

        void ApplyProfileUpgradeSettings(TeleportImpactEffect teleportEffect)
        {
            if (teleportEffect == null)
                return;

            int landingPulseRadiusPercent = 0;
            int landingPulseRevealDurationSeconds = 0;
            if (ProfileService.TryGetCurrentProfile(out ProfileSaveData profile) && profile != null)
            {
                landingPulseRadiusPercent = ShopUpgradeCatalog.GetTotalEffectValue(
                    profile,
                    ShopUpgradeEffectType.TeleportLandingPulseRadiusPercent);
                landingPulseRevealDurationSeconds = ShopUpgradeCatalog.GetTotalEffectValue(
                    profile,
                    ShopUpgradeEffectType.TeleportLandingPulseRevealDurationSeconds);
            }

            int singleRunRevealBonusPercent = ProfileUpgradeRuntimeApplier.GetActiveSingleRunEffectValue(
                ShopUpgradeEffectType.NextRunRevealHoldDurationBonusPercent);

            teleportEffect.ApplyPersistentLandingPulseRadiusPercent(landingPulseRadiusPercent);
            teleportEffect.ApplyPersistentLandingPulseRevealDurationSeconds(landingPulseRevealDurationSeconds);
            teleportEffect.ApplySingleRunLandingPulseRevealDurationBonusPercent(singleRunRevealBonusPercent);
        }

        bool ReadButton(InputFeatureUsage<bool> usage)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(m_ControllerNode);
            return device.isValid && device.TryGetFeatureValue(usage, out bool pressed) && pressed;
        }

        static Transform FindChildByName(Transform root, string targetName)
        {
            if (root == null)
                return null;

            if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildByName(root.GetChild(i), targetName);
                if (result != null)
                    return result;
            }

            return null;
        }
    }

    sealed class TeleportAnchorBlinkEffect : MonoBehaviour
    {
        Renderer[] m_Renderers = Array.Empty<Renderer>();
        Material[][] m_MaterialsByRenderer = Array.Empty<Material[]>();
        Color[][] m_BaseColorsByRenderer = Array.Empty<Color[]>();
        Color[][] m_BaseEmissionsByRenderer = Array.Empty<Color[]>();
        Color m_BlinkColor = Color.cyan;
        Vector3 m_StartScale = Vector3.one;
        float m_Frequency = 2.6f;
        float m_ScalePulse = 0.07f;
        bool m_HasStartScale;
        bool m_Configured;

        public void Configure(Color blinkColor, float frequency, float scalePulse)
        {
            m_BlinkColor = blinkColor;
            m_Frequency = Mathf.Max(0.1f, frequency);
            m_ScalePulse = Mathf.Max(0f, scalePulse);

            if (!m_HasStartScale)
            {
                m_StartScale = transform.localScale;
                m_HasStartScale = true;
            }

            CaptureMaterials();
            m_Configured = true;
            enabled = true;
        }

        void Update()
        {
            if (!m_Configured)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * m_Frequency * Mathf.PI * 2f);
            float blend = Mathf.Lerp(0.25f, 1f, pulse);
            Color dimColor = m_BlinkColor * 0.42f;
            dimColor.a = Mathf.Lerp(0.38f, 0.62f, pulse);
            Color brightColor = m_BlinkColor * 1.18f;
            brightColor.a = m_BlinkColor.a;
            Color targetColor = Color.Lerp(dimColor, brightColor, pulse);

            for (int rendererIndex = 0; rendererIndex < m_MaterialsByRenderer.Length; rendererIndex++)
            {
                Material[] materials = m_MaterialsByRenderer[rendererIndex];
                if (materials == null)
                    continue;

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                        continue;

                    Color baseColor = ResolveStoredColor(m_BaseColorsByRenderer, rendererIndex, materialIndex, Color.white);
                    ApplyMaterialColor(material, Color.Lerp(baseColor, targetColor, blend));

                    if (!material.HasProperty("_EmissionColor"))
                        continue;

                    Color baseEmission = ResolveStoredColor(m_BaseEmissionsByRenderer, rendererIndex, materialIndex, Color.black);
                    Color pulseEmission = m_BlinkColor * Mathf.Lerp(0.65f, 2.1f, pulse);
                    pulseEmission.a = 1f;
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.Lerp(baseEmission, pulseEmission, blend));
                }
            }

            if (m_HasStartScale)
                transform.localScale = m_StartScale * (1f + m_ScalePulse * pulse);
        }

        void OnDisable()
        {
            if (m_HasStartScale)
                transform.localScale = m_StartScale;
        }

        void CaptureMaterials()
        {
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_MaterialsByRenderer = new Material[m_Renderers.Length][];
            m_BaseColorsByRenderer = new Color[m_Renderers.Length][];
            m_BaseEmissionsByRenderer = new Color[m_Renderers.Length][];

            for (int rendererIndex = 0; rendererIndex < m_Renderers.Length; rendererIndex++)
            {
                Renderer targetRenderer = m_Renderers[rendererIndex];
                if (targetRenderer == null)
                    continue;

                Material[] materials = targetRenderer.materials;
                m_MaterialsByRenderer[rendererIndex] = materials;
                m_BaseColorsByRenderer[rendererIndex] = new Color[materials.Length];
                m_BaseEmissionsByRenderer[rendererIndex] = new Color[materials.Length];

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                    {
                        m_BaseColorsByRenderer[rendererIndex][materialIndex] = Color.white;
                        m_BaseEmissionsByRenderer[rendererIndex][materialIndex] = Color.black;
                        continue;
                    }

                    m_BaseColorsByRenderer[rendererIndex][materialIndex] = ResolveMaterialColor(material);
                    m_BaseEmissionsByRenderer[rendererIndex][materialIndex] = material.HasProperty("_EmissionColor")
                        ? material.GetColor("_EmissionColor")
                        : Color.black;
                }
            }
        }

        static Color ResolveStoredColor(Color[][] colors, int rendererIndex, int materialIndex, Color fallback)
        {
            if (colors == null || rendererIndex < 0 || rendererIndex >= colors.Length)
                return fallback;

            Color[] rendererColors = colors[rendererIndex];
            if (rendererColors == null || materialIndex < 0 || materialIndex >= rendererColors.Length)
                return fallback;

            return rendererColors[materialIndex];
        }

        static Color ResolveMaterialColor(Material material)
        {
            if (material == null)
                return Color.white;

            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");

            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");

            return Color.white;
        }

        static void ApplyMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }
    }
}
