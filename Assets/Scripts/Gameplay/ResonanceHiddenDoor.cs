using System.Collections;
using CIS5680VRGame.Generation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CIS5680VRGame.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class ResonanceHiddenDoor : MonoBehaviour
    {
        static readonly int PulseColorPropertyId = Shader.PropertyToID("_PulseColor");
        static readonly int BackgroundColorPropertyId = Shader.PropertyToID("_BgColor");
        static readonly int EmissionStrengthPropertyId = Shader.PropertyToID("_EmissionStrength");
        static readonly int RevealFillStrengthPropertyId = Shader.PropertyToID("_RevealFillStrength");
        static readonly int GridLineWidthPropertyId = Shader.PropertyToID("_GridLineWidth");
        static readonly int BandWidthPropertyId = Shader.PropertyToID("_BandWidth");
        static readonly int CircuitGlitchStrengthPropertyId = Shader.PropertyToID("_CircuitGlitchStrength");
        static readonly int CircuitGlitchTimePropertyId = Shader.PropertyToID("_CircuitGlitchTime");
        static readonly int CircuitGlitchSeedPropertyId = Shader.PropertyToID("_CircuitGlitchSeed");

        [Header("Progress")]
        [SerializeField, Min(1)] int m_RequiredPulseHits = 3;
        [SerializeField, Min(0f)] float m_ProgressResetDelay = 12f;
        [SerializeField, Min(0f)] float m_PulseReachPadding = 0.35f;

        [Header("Opening")]
        [SerializeField] Transform m_MovingRoot;
        [SerializeField] Collider m_BlockingCollider;
        [SerializeField] XRSimpleInteractable m_Interactable;
        [SerializeField] XROrigin m_PlayerRig;
        [SerializeField] Vector3 m_OpenLocalOffset = new(0f, 4.6f, 0f);
        [SerializeField, Min(0.01f)] float m_OpenDuration = 0.7f;
        [SerializeField] float m_OpenHapticsAmplitude = 0.25f;
        [SerializeField] float m_OpenHapticsDuration = 0.12f;

        [Header("Visuals")]
        [SerializeField] Transform m_InteractionPointRoot;
        [SerializeField] Renderer[] m_UnstableSurfaceRenderers;
        [SerializeField] Renderer[] m_InteractionPointRenderers;
        [SerializeField] Color m_SurfacePulseColor = new(0.08f, 0.78f, 1f, 1f);
        [SerializeField] Color m_SurfaceFaultPulseColor = new(0.35f, 1f, 0.94f, 1f);
        [SerializeField] Color m_TriggeredPulseColor = new(1f, 0.86f, 0.36f, 1f);
        [SerializeField] Color m_DarkBackgroundColor = new(0f, 0f, 0f, 1f);
        [SerializeField, Min(0f)] float m_SurfaceBaseEmission = 2.4f;
        [SerializeField, Min(0f)] float m_SurfaceFaultEmission = 6.2f;
        [SerializeField, Min(0f)] float m_TriggeredEmission = 5.2f;
        [SerializeField, Min(0f)] float m_PulseFlashDuration = 0.85f;
        [SerializeField, Range(0f, 1f)] float m_IdleGlitchStrength = 0f;
        [SerializeField, Range(0f, 1f)] float m_MaxGlitchStrength = 0.88f;
        [SerializeField, Min(0.001f)] float m_BaseGridLineWidth = 0.05f;
        [SerializeField, Min(0.001f)] float m_MaxGridLineWidth = 0.12f;
        [SerializeField, Min(0.01f)] float m_BaseBandWidth = 0.6f;
        [SerializeField, Min(0.01f)] float m_MaxBandWidth = 1.05f;
        [SerializeField, Min(0f)] float m_CircuitFlickerSpeed = 24f;

        [Header("Audio Hooks")]
        [SerializeField] AudioSource m_AudioSource;
        [SerializeField] AudioClip m_PulseHitClip;
        [SerializeField] AudioClip m_TriggeredClip;
        [SerializeField] AudioClip m_OpenedClip;
        [SerializeField] AudioClip m_ProgressResetClip;

        [Header("Navigation State")]
        [SerializeField] MazeRunBootstrap m_Bootstrap;
        [SerializeField] Vector2Int m_GridPosition;
        [SerializeField] MazeCellConnection m_NavigationConnection = MazeCellConnection.None;
        [SerializeField] bool m_HasNavigationConnection;

        [Header("Events")]
        [SerializeField] UnityEvent m_OnPulseProgressed;
        [SerializeField] UnityEvent m_OnTriggered;
        [SerializeField] UnityEvent m_OnOpened;
        [SerializeField] UnityEvent m_OnProgressReset;

        MaterialPropertyBlock m_PropertyBlock;
        Coroutine m_OpenRoutine;
        Vector3 m_ClosedLocalPosition;
        int m_CurrentPulseHits;
        float m_LastPulseHitTime = -999f;
        float m_FlashEndsAt = -999f;
        float m_GlitchSeed;
        bool m_IsTriggered;
        bool m_IsOpened;

        public int CurrentPulseHits => m_CurrentPulseHits;
        public bool IsTriggered => m_IsTriggered;
        public bool IsOpened => m_IsOpened;

        public void ConfigureGeneratedDoor(
            Transform movingRoot,
            Collider blockingCollider,
            Transform interactionPointRoot,
            Renderer[] unstableSurfaceRenderers,
            Renderer[] interactionPointRenderers,
            Vector3 openLocalOffset,
            int requiredPulseHits,
            float progressResetDelay,
            AudioClip pulseHitClip,
            AudioClip triggeredClip,
            AudioClip openedClip,
            AudioClip progressResetClip)
        {
            m_MovingRoot = movingRoot != null ? movingRoot : transform;
            m_BlockingCollider = blockingCollider != null ? blockingCollider : GetComponent<Collider>();
            m_InteractionPointRoot = interactionPointRoot;
            m_UnstableSurfaceRenderers = unstableSurfaceRenderers;
            m_InteractionPointRenderers = interactionPointRenderers;
            m_OpenLocalOffset = openLocalOffset;
            m_RequiredPulseHits = Mathf.Max(1, requiredPulseHits);
            m_ProgressResetDelay = Mathf.Max(0f, progressResetDelay);
            m_PulseHitClip = pulseHitClip;
            m_TriggeredClip = triggeredClip;
            m_OpenedClip = openedClip;
            m_ProgressResetClip = progressResetClip;

            NormalizeDarkVisualDefaults();
            ResolveReferences();
            EnsureGlitchSeed();
            CacheClosedPose();
            ApplyVisualState();
        }

        public void ConfigureNavigationState(MazeRunBootstrap bootstrap, Vector2Int gridPosition, MazeCellConnection connection)
        {
            m_Bootstrap = bootstrap;
            m_GridPosition = gridPosition;
            m_NavigationConnection = connection;
            m_HasNavigationConnection = bootstrap != null && connection != MazeCellConnection.None;

            if (m_IsOpened)
                NotifyNavigationOpened();
        }

        void Reset()
        {
            NormalizeDarkVisualDefaults();
            ResolveReferences();
        }

        void OnValidate()
        {
            NormalizeDarkVisualDefaults();
            ResolveReferences();
            ApplyVisualState();
        }

        void Awake()
        {
            NormalizeDarkVisualDefaults();
            ResolveReferences();
            EnsureGlitchSeed();
            CacheClosedPose();
            ApplyVisualState();
        }

        void OnEnable()
        {
            NormalizeDarkVisualDefaults();
            ResolveReferences();
            EnsureGlitchSeed();
            PulseManager.PulseSpawned += OnPulseSpawned;

            if (m_Interactable != null)
                m_Interactable.selectEntered.AddListener(OnSelectEntered);

            ApplyVisualState();
        }

        void OnDisable()
        {
            PulseManager.PulseSpawned -= OnPulseSpawned;

            if (m_Interactable != null)
                m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        void NormalizeDarkVisualDefaults()
        {
            m_DarkBackgroundColor = Color.black;
            m_IdleGlitchStrength = 0f;
        }

        void Update()
        {
            if (!m_IsTriggered
                && m_CurrentPulseHits > 0
                && m_ProgressResetDelay > 0f
                && Time.time - m_LastPulseHitTime >= m_ProgressResetDelay)
            {
                ResetProgress();
            }

            ApplyVisualState();
        }

        void ResolveReferences()
        {
            if (m_MovingRoot == null)
                m_MovingRoot = transform;

            if (m_BlockingCollider == null)
                m_BlockingCollider = GetComponent<Collider>();

            if (m_Interactable == null)
                m_Interactable = GetComponent<XRSimpleInteractable>();

            if (m_PlayerRig == null)
                m_PlayerRig = FindObjectOfType<XROrigin>();

            if (m_Bootstrap == null)
                m_Bootstrap = FindObjectOfType<MazeRunBootstrap>();

            TryInferGeneratedNavigationState();

            if (m_AudioSource == null)
                m_AudioSource = GetComponent<AudioSource>();

            if (m_UnstableSurfaceRenderers == null || m_UnstableSurfaceRenderers.Length == 0)
            {
                Renderer ownRenderer = GetComponent<Renderer>();
                m_UnstableSurfaceRenderers = ownRenderer != null
                    ? new[] { ownRenderer }
                    : GetComponentsInChildren<Renderer>(true);
            }
        }

        void EnsureGlitchSeed()
        {
            if (m_GlitchSeed > 0f)
                return;

            unchecked
            {
                int hash = transform.GetInstanceID();
                hash = (hash * 397) ^ Mathf.RoundToInt(transform.position.x * 31f);
                hash = (hash * 397) ^ Mathf.RoundToInt(transform.position.y * 37f);
                hash = (hash * 397) ^ Mathf.RoundToInt(transform.position.z * 41f);
                m_GlitchSeed = Mathf.Abs(hash % 10000) * 0.0137f + 1f;
            }
        }

        void CacheClosedPose()
        {
            if (m_MovingRoot == null)
                return;

            m_ClosedLocalPosition = m_MovingRoot.localPosition;
        }

        void OnPulseSpawned(Vector3 pulseOrigin, Vector3 pulseNormal, float pulseRadius, Collider sourceCollider)
        {
            if (m_IsOpened || m_IsTriggered || !DoesPulseReachDoor(pulseOrigin, pulseRadius, sourceCollider))
                return;

            m_CurrentPulseHits = Mathf.Min(m_RequiredPulseHits, m_CurrentPulseHits + 1);
            m_LastPulseHitTime = Time.time;
            m_FlashEndsAt = Time.time + Mathf.Max(0.01f, m_PulseFlashDuration);
            bool shouldTrigger = m_CurrentPulseHits >= m_RequiredPulseHits;
            if (!shouldTrigger)
                PlayClip(m_PulseHitClip, 0.82f);
            m_OnPulseProgressed?.Invoke();

            if (shouldTrigger)
                TriggerDoor();
            else
                ApplyVisualState();
        }

        bool DoesPulseReachDoor(Vector3 pulseOrigin, float pulseRadius, Collider sourceCollider)
        {
            if (pulseRadius <= 0f)
                return false;

            if (m_BlockingCollider == null)
                return Vector3.Distance(transform.position, pulseOrigin) <= pulseRadius + m_PulseReachPadding;

            if (sourceCollider != null && IsSourceColliderPartOfDoor(sourceCollider))
                return true;

            Vector3 closestPoint = m_BlockingCollider.ClosestPoint(pulseOrigin);
            return Vector3.Distance(closestPoint, pulseOrigin) <= pulseRadius + m_PulseReachPadding;
        }

        bool IsSourceColliderPartOfDoor(Collider sourceCollider)
        {
            if (sourceCollider == null)
                return false;

            if (sourceCollider == m_BlockingCollider)
                return true;

            Transform sourceTransform = sourceCollider.transform;
            return sourceTransform == transform || sourceTransform.IsChildOf(transform);
        }

        void TryInferGeneratedNavigationState()
        {
            if (m_HasNavigationConnection || m_Bootstrap == null)
                return;

            if (!TryParseGeneratedDoorName(name, out Vector2Int gridPosition, out MazeCellConnection connection))
                return;

            ConfigureNavigationState(m_Bootstrap, gridPosition, connection);
        }

        static bool TryParseGeneratedDoorName(string doorName, out Vector2Int gridPosition, out MazeCellConnection connection)
        {
            const string Prefix = "ResonanceHiddenDoor_";
            gridPosition = default;
            connection = MazeCellConnection.None;

            if (string.IsNullOrWhiteSpace(doorName) || !doorName.StartsWith(Prefix, System.StringComparison.Ordinal))
                return false;

            string payload = doorName.Substring(Prefix.Length);
            int connectionSeparator = payload.LastIndexOf('_');
            if (connectionSeparator <= 0 || connectionSeparator >= payload.Length - 1)
                return false;

            string coordinatePayload = payload.Substring(0, connectionSeparator);
            string connectionPayload = payload.Substring(connectionSeparator + 1);
            string[] coordinates = coordinatePayload.Split('_');
            if (coordinates.Length != 2)
                return false;

            if (!int.TryParse(coordinates[0], out int x) || !int.TryParse(coordinates[1], out int y))
                return false;

            if (!System.Enum.TryParse(connectionPayload, ignoreCase: true, out connection)
                || connection == MazeCellConnection.None)
            {
                connection = MazeCellConnection.None;
                return false;
            }

            gridPosition = new Vector2Int(x, y);
            return true;
        }

        void TriggerDoor()
        {
            if (m_IsTriggered || m_IsOpened)
                return;

            m_IsTriggered = true;
            m_CurrentPulseHits = m_RequiredPulseHits;
            PlayClip(m_TriggeredClip, 0.95f);
            m_OnTriggered?.Invoke();
            ApplyVisualState();
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!m_IsTriggered || m_IsOpened || !CanUse(args.interactorObject))
                return;

            if (args.interactorObject is XRBaseInputInteractor inputInteractor)
                inputInteractor.SendHapticImpulse(Mathf.Clamp01(m_OpenHapticsAmplitude), Mathf.Max(0.01f, m_OpenHapticsDuration));

            OpenDoor();
        }

        bool CanUse(IXRSelectInteractor interactor)
        {
            if (interactor == null)
                return false;

            XROrigin rig = interactor.transform.GetComponentInParent<XROrigin>();
            return rig != null && (m_PlayerRig == null || rig == m_PlayerRig);
        }

        void OpenDoor()
        {
            if (m_IsOpened)
                return;

            m_IsOpened = true;
            PlayClip(m_OpenedClip, 1f);
            m_OnOpened?.Invoke();

            if (m_Interactable != null)
                m_Interactable.enabled = false;

            if (m_OpenRoutine != null)
                StopCoroutine(m_OpenRoutine);

            if (Application.isPlaying)
                m_OpenRoutine = StartCoroutine(OpenRoutine());
            else
                FinishOpenInstantly();
        }

        IEnumerator OpenRoutine()
        {
            Transform target = m_MovingRoot != null ? m_MovingRoot : transform;
            Vector3 startPosition = target.localPosition;
            Vector3 endPosition = m_ClosedLocalPosition + m_OpenLocalOffset;
            float duration = Mathf.Max(0.01f, m_OpenDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                target.localPosition = Vector3.Lerp(startPosition, endPosition, t);
                yield return null;
            }

            target.localPosition = endPosition;
            if (m_BlockingCollider != null)
                m_BlockingCollider.enabled = false;

            NotifyNavigationOpened();
            ApplyVisualState();
            m_OpenRoutine = null;
        }

        void FinishOpenInstantly()
        {
            Transform target = m_MovingRoot != null ? m_MovingRoot : transform;
            target.localPosition = m_ClosedLocalPosition + m_OpenLocalOffset;

            if (m_BlockingCollider != null)
                m_BlockingCollider.enabled = false;

            NotifyNavigationOpened();
            ApplyVisualState();
        }

        void NotifyNavigationOpened()
        {
            if (!m_HasNavigationConnection || m_Bootstrap == null)
                return;

            if (m_Bootstrap.SetHiddenDoorConnectionOpen(m_GridPosition, m_NavigationConnection))
                EnemyPatrolController.NotifyMazeNavigationTopologyChanged();
        }

        void ResetProgress()
        {
            if (m_IsTriggered || m_IsOpened || m_CurrentPulseHits <= 0)
                return;

            m_CurrentPulseHits = 0;
            m_LastPulseHitTime = -999f;
            PlayClip(m_ProgressResetClip, 0.55f);
            m_OnProgressReset?.Invoke();
            ApplyVisualState();
        }

        void ApplyVisualState()
        {
            ResolveReferences();

            if (m_Interactable != null && m_Interactable.enabled != (m_IsTriggered && !m_IsOpened))
                m_Interactable.enabled = m_IsTriggered && !m_IsOpened;

            if (m_InteractionPointRoot != null)
                m_InteractionPointRoot.gameObject.SetActive(m_IsTriggered && !m_IsOpened);

            float flash = Mathf.Clamp01((m_FlashEndsAt - Time.time) / Mathf.Max(0.01f, m_PulseFlashDuration));
            float progress = ResolveProgress01();
            ApplyUnstableSurfaceVisuals(progress, flash);
            ApplyRendererVisuals(m_InteractionPointRenderers, m_TriggeredPulseColor, m_TriggeredEmission + flash, 0.65f);
            SetRendererGroupVisible(m_InteractionPointRenderers, m_IsTriggered && !m_IsOpened);
        }

        static void SetRendererGroupVisible(Renderer[] renderers, bool visible)
        {
            if (renderers == null)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
            }
        }

        float ResolveProgress01()
        {
            if (m_IsTriggered)
                return 1f;

            return m_RequiredPulseHits > 0
                ? Mathf.Clamp01(m_CurrentPulseHits / (float)m_RequiredPulseHits)
                : 0f;
        }

        void ApplyUnstableSurfaceVisuals(float progress, float flash)
        {
            if (m_UnstableSurfaceRenderers == null || m_UnstableSurfaceRenderers.Length == 0)
                return;

            m_PropertyBlock ??= new MaterialPropertyBlock();
            EnsureGlitchSeed();
            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            float instability = Mathf.Clamp01(m_IdleGlitchStrength + progress * 0.58f + flash * (0.32f + progress * 0.38f));
            if (m_IsTriggered)
                instability = 1f;

            float glitchStrength = Mathf.Lerp(m_IdleGlitchStrength, m_MaxGlitchStrength, instability);
            float primaryFlicker = Mathf.Sin(time * m_CircuitFlickerSpeed + m_GlitchSeed + m_CurrentPulseHits * 1.73f) * 0.5f + 0.5f;
            float breakerFlicker = Mathf.Sin(time * (m_CircuitFlickerSpeed * 0.41f) + m_GlitchSeed * 2.37f) * 0.5f + 0.5f;
            float flicker = Mathf.Lerp(0.68f, 1.55f, Mathf.Max(primaryFlicker, breakerFlicker));
            float emission = Mathf.Lerp(m_SurfaceBaseEmission, m_IsTriggered ? m_TriggeredEmission : m_SurfaceFaultEmission, instability)
                * flicker
                * (1f + flash * 0.9f);
            float revealFillStrength = Mathf.Lerp(0.32f, m_IsTriggered ? 0.72f : 0.62f, instability);
            float gridLineWidth = Mathf.Lerp(m_BaseGridLineWidth, m_MaxGridLineWidth, Mathf.Clamp01(instability + flash * 0.35f));
            float bandWidth = Mathf.Lerp(m_BaseBandWidth, m_MaxBandWidth, Mathf.Clamp01(instability));
            Color pulseColor = Color.Lerp(m_SurfacePulseColor, m_IsTriggered ? m_TriggeredPulseColor : m_SurfaceFaultPulseColor, Mathf.Clamp01(instability * 0.55f));

            for (int i = 0; i < m_UnstableSurfaceRenderers.Length; i++)
            {
                Renderer targetRenderer = m_UnstableSurfaceRenderers[i];
                if (targetRenderer == null)
                    continue;

                targetRenderer.enabled = true;

                targetRenderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(PulseColorPropertyId, pulseColor);
                m_PropertyBlock.SetColor(BackgroundColorPropertyId, m_DarkBackgroundColor);
                m_PropertyBlock.SetFloat(EmissionStrengthPropertyId, Mathf.Max(0f, emission));
                m_PropertyBlock.SetFloat(RevealFillStrengthPropertyId, Mathf.Clamp01(revealFillStrength));
                m_PropertyBlock.SetFloat(GridLineWidthPropertyId, Mathf.Max(0.001f, gridLineWidth));
                m_PropertyBlock.SetFloat(BandWidthPropertyId, Mathf.Max(0.01f, bandWidth));
                m_PropertyBlock.SetFloat(CircuitGlitchStrengthPropertyId, Mathf.Clamp01(glitchStrength));
                m_PropertyBlock.SetFloat(CircuitGlitchTimePropertyId, time);
                m_PropertyBlock.SetFloat(CircuitGlitchSeedPropertyId, m_GlitchSeed + i * 13.17f);
                targetRenderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        void ApplyRendererVisuals(Renderer[] renderers, Color pulseColor, float emissionStrength, float revealFillStrength)
        {
            if (renderers == null || renderers.Length == 0)
                return;

            m_PropertyBlock ??= new MaterialPropertyBlock();
            float flicker = 1f;
            if (!m_IsOpened)
                flicker = 0.86f + Mathf.Sin(Time.time * 18f) * 0.14f;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null)
                    continue;

                targetRenderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(PulseColorPropertyId, pulseColor);
                m_PropertyBlock.SetColor(BackgroundColorPropertyId, m_DarkBackgroundColor);
                m_PropertyBlock.SetFloat(EmissionStrengthPropertyId, Mathf.Max(0f, emissionStrength * flicker));
                m_PropertyBlock.SetFloat(RevealFillStrengthPropertyId, Mathf.Clamp01(revealFillStrength));
                targetRenderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        void PlayClip(AudioClip clip, float volumeScale)
        {
            if (clip == null)
                return;

            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
                m_AudioSource.loop = false;
                m_AudioSource.spatialBlend = 1f;
                m_AudioSource.rolloffMode = AudioRolloffMode.Linear;
                m_AudioSource.minDistance = 0.65f;
                m_AudioSource.maxDistance = 10f;
                m_AudioSource.dopplerLevel = 0f;
            }

            m_AudioSource.PlayOneShot(clip, Mathf.Clamp(volumeScale, 0f, 1.2f));
        }

        static int SafeLength(Renderer[] renderers)
        {
            return renderers != null ? renderers.Length : 0;
        }
    }
}
