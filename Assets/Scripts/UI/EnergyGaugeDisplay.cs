using CIS5680VRGame.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace CIS5680VRGame.UI
{
    [RequireComponent(typeof(MeshRenderer))]
    public class EnergyGaugeDisplay : MonoBehaviour
    {
        static readonly int s_FillAmountId = Shader.PropertyToID("_FillAmount");
        static readonly int s_FillColorId = Shader.PropertyToID("_FillColor");
        static readonly int s_BackgroundColorId = Shader.PropertyToID("_BackgroundColor");
        static readonly int s_TickValueMaxId = Shader.PropertyToID("_TickValueMax");
        const float k_DefaultTickValueMax = 100f;
        static readonly List<InputDevice> s_LeftControllerDevices = new();
        static readonly List<InputDevice> s_RightControllerDevices = new();

        enum GaugeAnimationMode
        {
            Normal = 0,
            Consume = 1,
            Regen = 2,
            Refill = 3,
        }

        [SerializeField] PlayerEnergy m_PlayerEnergy;
        [SerializeField] MeshRenderer m_Renderer;
        [SerializeField] Color m_NormalFillColor = new(1f, 0.55f, 0f, 0.95f);
        [SerializeField] Color m_NormalBackgroundColor = new(0.12f, 0.12f, 0.12f, 0.45f);
        [SerializeField] float m_WarningFlashDuration = 0.35f;
        [SerializeField] Color m_InsufficientFlashColorA = new(1f, 0.55f, 0f, 0.95f);
        [SerializeField] Color m_InsufficientFlashColorB = new(1f, 0.92f, 0.48f, 1f);
        [SerializeField] Color m_InsufficientBackgroundColor = new(0.2f, 0.14f, 0.04f, 0.58f);
        [SerializeField] float m_InsufficientFlashFrequency = 11f;
        [SerializeField] Color m_RefillFlashColorA = new(0.25f, 1f, 0.42f, 0.98f);
        [SerializeField] Color m_RefillFlashColorB = new(0.62f, 1f, 0.62f, 0.98f);
        [SerializeField] Color m_RefillBackgroundColor = new(0.08f, 0.22f, 0.08f, 0.58f);
        [SerializeField] float m_RefillFlashFrequency = 8f;
        [SerializeField] float m_ConsumeSmoothTime = 0.12f;
        [SerializeField] float m_RegenSmoothTime = 0.22f;
        [SerializeField] float m_RefillAnimationDuration = 0.6f;
        [SerializeField] float m_RefillHapticsAmplitude = 0.18f;
        [SerializeField] float m_RefillHapticsDuration = 0.12f;

        MaterialPropertyBlock m_PropertyBlock;
        ControllerAttachedBillboard m_ControllerAttachedBillboard;
        float m_WarningFlashUntil;
        float m_DisplayedFillAmount;
        float m_TargetFillAmount;
        float m_FillVelocity;
        GaugeAnimationMode m_AnimationMode;

        public void SetEnergySource(PlayerEnergy energy)
        {
            if (m_PlayerEnergy == energy)
                return;

            Unsubscribe();
            m_PlayerEnergy = energy;
            Subscribe();
            SyncDisplayedFillToTarget();
            UpdateVisuals();
        }

        void Awake()
        {
            if (m_Renderer == null)
                m_Renderer = GetComponent<MeshRenderer>();

            m_PropertyBlock = new MaterialPropertyBlock();
            m_ControllerAttachedBillboard = GetComponent<ControllerAttachedBillboard>();
            ResolveEnergy();
            SyncDisplayedFillToTarget();
        }

        void OnEnable()
        {
            ResolveEnergy();
            Subscribe();
            SyncDisplayedFillToTarget();
            UpdateVisuals();
        }

        void OnDisable()
        {
            Unsubscribe();
        }

        void Update()
        {
            bool needsVisualRefresh = StepDisplayedFill();

            if (m_WarningFlashUntil > 0f && Time.unscaledTime > m_WarningFlashUntil)
            {
                m_WarningFlashUntil = 0f;
                needsVisualRefresh = true;
            }

            if (needsVisualRefresh || IsRefillVisualActive() || m_WarningFlashUntil > Time.unscaledTime)
                UpdateVisuals();
        }

        void ResolveEnergy()
        {
            if (m_PlayerEnergy == null)
                m_PlayerEnergy = FindObjectOfType<PlayerEnergy>();
        }

        void Subscribe()
        {
            if (m_PlayerEnergy != null)
            {
                m_PlayerEnergy.RefillStarted += OnRefillStarted;
                m_PlayerEnergy.EnergyChangedDetailed += OnEnergyChanged;
                m_PlayerEnergy.InsufficientEnergySignaled += OnInsufficientEnergySignaled;
            }
        }

        void Unsubscribe()
        {
            if (m_PlayerEnergy != null)
            {
                m_PlayerEnergy.RefillStarted -= OnRefillStarted;
                m_PlayerEnergy.EnergyChangedDetailed -= OnEnergyChanged;
                m_PlayerEnergy.InsufficientEnergySignaled -= OnInsufficientEnergySignaled;
            }
        }

        void OnRefillStarted()
        {
            m_WarningFlashUntil = 0f;
            m_FillVelocity = 0f;
            m_AnimationMode = GaugeAnimationMode.Refill;
            TriggerRefillHaptics();
            UpdateVisuals();
        }

        void OnEnergyChanged(EnergyChangeContext context)
        {
            m_TargetFillAmount = context.MaxEnergy <= 0 ? 0f : Mathf.Clamp01((float)context.CurrentEnergy / context.MaxEnergy);

            switch (context.Reason)
            {
                case EnergyChangeReason.Consume:
                    m_AnimationMode = GaugeAnimationMode.Consume;
                    break;
                case EnergyChangeReason.Regen:
                    m_AnimationMode = GaugeAnimationMode.Regen;
                    break;
                case EnergyChangeReason.RefillStation:
                    if (m_PlayerEnergy != null && m_PlayerEnergy.IsRefillInProgress)
                        m_AnimationMode = GaugeAnimationMode.Refill;
                    break;
                default:
                    m_AnimationMode = m_TargetFillAmount >= m_DisplayedFillAmount
                        ? GaugeAnimationMode.Regen
                        : GaugeAnimationMode.Consume;
                    break;
            }

            UpdateVisuals();
        }

        void OnInsufficientEnergySignaled(int _, int __)
        {
            m_WarningFlashUntil = Time.unscaledTime + Mathf.Max(0.05f, m_WarningFlashDuration);
            m_ControllerAttachedBillboard?.TriggerShake();
            UpdateVisuals();
        }

        bool StepDisplayedFill()
        {
            float previousFill = m_DisplayedFillAmount;
            bool visualModeChanged = false;

            if (Mathf.Abs(m_DisplayedFillAmount - m_TargetFillAmount) <= 0.0005f)
            {
                m_DisplayedFillAmount = m_TargetFillAmount;
                m_FillVelocity = 0f;
                if (m_AnimationMode == GaugeAnimationMode.Refill && (m_PlayerEnergy == null || !m_PlayerEnergy.IsRefillInProgress))
                {
                    m_AnimationMode = GaugeAnimationMode.Normal;
                    visualModeChanged = true;
                }

                return visualModeChanged;
            }

            if (m_AnimationMode == GaugeAnimationMode.Refill)
            {
                float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, m_RefillAnimationDuration);
                m_DisplayedFillAmount = Mathf.MoveTowards(m_DisplayedFillAmount, m_TargetFillAmount, step);
            }
            else
            {
                float smoothTime = m_TargetFillAmount < m_DisplayedFillAmount
                    ? Mathf.Max(0.03f, m_ConsumeSmoothTime)
                    : Mathf.Max(0.04f, m_RegenSmoothTime);

                m_DisplayedFillAmount = Mathf.SmoothDamp(
                    m_DisplayedFillAmount,
                    m_TargetFillAmount,
                    ref m_FillVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }

            if (Mathf.Abs(m_DisplayedFillAmount - m_TargetFillAmount) <= 0.001f)
            {
                m_DisplayedFillAmount = m_TargetFillAmount;
                m_FillVelocity = 0f;
                if (m_AnimationMode == GaugeAnimationMode.Refill && (m_PlayerEnergy == null || !m_PlayerEnergy.IsRefillInProgress))
                {
                    m_AnimationMode = GaugeAnimationMode.Normal;
                    visualModeChanged = true;
                }
            }

            return visualModeChanged || !Mathf.Approximately(previousFill, m_DisplayedFillAmount);
        }

        void UpdateVisuals()
        {
            if (m_Renderer == null)
                return;

            if (m_PropertyBlock == null)
                m_PropertyBlock = new MaterialPropertyBlock();

            ResolveEnergy();

            bool showRefill = IsRefillVisualActive();
            bool showWarning = !showRefill && m_WarningFlashUntil > Time.unscaledTime;
            Color fillColor = m_NormalFillColor;
            Color backgroundColor = m_NormalBackgroundColor;

            if (showRefill)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Max(0.01f, m_RefillFlashFrequency) * Mathf.PI * 2f);
                fillColor = Color.Lerp(m_RefillFlashColorA, m_RefillFlashColorB, wave);
                backgroundColor = m_RefillBackgroundColor;
            }
            else if (showWarning)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Max(0.01f, m_InsufficientFlashFrequency) * Mathf.PI * 2f);
                fillColor = Color.Lerp(m_InsufficientFlashColorA, m_InsufficientFlashColorB, wave);
                backgroundColor = Color.Lerp(m_NormalBackgroundColor, m_InsufficientBackgroundColor, wave);
            }

            m_Renderer.GetPropertyBlock(m_PropertyBlock);
            m_PropertyBlock.SetFloat(s_FillAmountId, m_DisplayedFillAmount);
            m_PropertyBlock.SetColor(s_FillColorId, fillColor);
            m_PropertyBlock.SetColor(s_BackgroundColorId, backgroundColor);
            m_PropertyBlock.SetFloat(s_TickValueMaxId, m_PlayerEnergy != null ? m_PlayerEnergy.MaxEnergy : k_DefaultTickValueMax);
            m_Renderer.SetPropertyBlock(m_PropertyBlock);
        }

        bool IsRefillVisualActive()
        {
            return m_AnimationMode == GaugeAnimationMode.Refill;
        }

        void SyncDisplayedFillToTarget()
        {
            m_TargetFillAmount = m_PlayerEnergy != null ? m_PlayerEnergy.NormalizedEnergy : 0f;
            m_DisplayedFillAmount = m_TargetFillAmount;
            m_FillVelocity = 0f;
            m_AnimationMode = GaugeAnimationMode.Normal;
        }

        void TriggerRefillHaptics()
        {
            SendHapticsToDevices(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand, s_LeftControllerDevices);
            SendHapticsToDevices(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand, s_RightControllerDevices);
        }

        void SendHapticsToDevices(InputDeviceCharacteristics characteristics, List<InputDevice> devices)
        {
            devices.Clear();
            InputDevices.GetDevicesWithCharacteristics(characteristics, devices);

            for (int i = 0; i < devices.Count; i++)
            {
                InputDevice device = devices[i];
                if (!device.isValid || !device.TryGetHapticCapabilities(out var capabilities) || !capabilities.supportsImpulse)
                    continue;

                device.SendHapticImpulse(0u, Mathf.Clamp01(m_RefillHapticsAmplitude), Mathf.Max(0.01f, m_RefillHapticsDuration));
            }
        }
    }
}
