using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace CIS5680VRGame.UI
{
    public sealed class TeleportViewEffectService : MonoBehaviour
    {
        static readonly List<InputDevice> s_HapticDevices = new();
        static TeleportViewEffectService s_Instance;

        [SerializeField] float m_MinCameraPlaneDistance = 0.08f;

        Canvas m_Canvas;
        Image m_FadeImage;
        Image m_VignetteImage;
        Image m_CoreGlowImage;
        Sprite m_VignetteSprite;
        Sprite m_CoreGlowSprite;
        Coroutine m_BlinkRoutine;

        public struct BlinkSettings
        {
            public float Intensity;
            public float PeakOpacity;
            public float FadeOutDuration;
            public float HoldDuration;
            public float FadeInDuration;
            public Color Tint;
            public Color CoreColor;
            public float HapticsAmplitude;
            public float HapticsDuration;
            public bool HoldDarkAfterPeak;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        public static void PlayBlink(BlinkSettings settings, Action onBlinkPeak)
        {
            float intensity = Mathf.Clamp01(settings.Intensity);
            if (intensity <= 0.001f)
            {
                onBlinkPeak?.Invoke();
                return;
            }

            EnsureCreated();
            s_Instance.BeginBlink(settings, onBlinkPeak);
        }

        static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            GameObject root = new("TeleportViewEffectService");
            DontDestroyOnLoad(root);
            s_Instance = root.AddComponent<TeleportViewEffectService>();
        }

        void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureOverlay();
            ApplyOverlay(0f, Color.black, Color.cyan);
        }

        void OnDestroy()
        {
            if (m_VignetteSprite != null)
                Destroy(m_VignetteSprite);

            if (m_CoreGlowSprite != null)
                Destroy(m_CoreGlowSprite);
        }

        void BeginBlink(BlinkSettings settings, Action onBlinkPeak)
        {
            EnsureOverlay();
            if (m_BlinkRoutine != null)
                StopCoroutine(m_BlinkRoutine);

            m_BlinkRoutine = StartCoroutine(BlinkRoutine(settings, onBlinkPeak));
        }

        IEnumerator BlinkRoutine(BlinkSettings settings, Action onBlinkPeak)
        {
            RefreshCanvasCamera();

            float intensity = Mathf.Clamp01(settings.Intensity);
            float peakOpacity = Mathf.Clamp01(settings.PeakOpacity) * intensity;
            float fadeOutDuration = Mathf.Max(0.01f, settings.FadeOutDuration);
            float holdDuration = Mathf.Max(0f, settings.HoldDuration);
            float fadeInDuration = Mathf.Max(0.01f, settings.FadeInDuration);
            Color tint = settings.Tint.a > 0.001f ? settings.Tint : new Color(0.018f, 0.012f, 0.055f, 1f);
            Color core = settings.CoreColor.a > 0.001f ? settings.CoreColor : new Color(0.48f, 0.44f, 1f, 1f);

            yield return AnimateOverlay(0f, peakOpacity, fadeOutDuration, tint, core, true);

            TriggerHaptics(
                Mathf.Clamp01(settings.HapticsAmplitude * intensity),
                Mathf.Max(0f, settings.HapticsDuration));
            onBlinkPeak?.Invoke();

            if (holdDuration > 0f)
                yield return WaitUnscaled(holdDuration);

            if (settings.HoldDarkAfterPeak)
            {
                ApplyOverlay(1f, Color.black, Color.clear, false);
                m_BlinkRoutine = null;
                yield break;
            }

            yield return AnimateOverlay(peakOpacity, 0f, fadeInDuration, tint, core, false);
            ApplyOverlay(0f, tint, core);
            m_BlinkRoutine = null;
        }

        IEnumerator AnimateOverlay(float from, float to, float duration, Color tint, Color core, bool closing)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = closing ? Mathf.SmoothStep(0f, 1f, t) : 1f - Mathf.Pow(1f - t, 3f);
                RefreshCanvasCamera();
                ApplyOverlay(Mathf.Lerp(from, to, eased), tint, core);
                yield return null;
            }

            ApplyOverlay(to, tint, core);
        }

        public static void ClearHeldOverlay()
        {
            if (s_Instance == null)
                return;

            s_Instance.ClearOverlayInternal();
        }

        void ClearOverlayInternal()
        {
            EnsureOverlay();
            if (m_BlinkRoutine != null)
            {
                StopCoroutine(m_BlinkRoutine);
                m_BlinkRoutine = null;
            }

            ApplyOverlay(0f, Color.black, Color.cyan);
        }

        IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void ApplyOverlay(float strength, Color tint, Color core, bool showCoreGlow = true)
        {
            float clamped = Mathf.Clamp01(strength);
            if (m_FadeImage != null)
            {
                Color fade = Color.Lerp(Color.black, tint, 0.72f);
                fade.a = clamped;
                m_FadeImage.color = fade;
                m_FadeImage.enabled = clamped > 0.001f;
            }

            if (m_VignetteImage != null)
            {
                Color vignette = Color.Lerp(Color.black, tint, 0.55f);
                vignette.a = Mathf.Clamp01(clamped * 1.18f);
                m_VignetteImage.color = vignette;
                m_VignetteImage.enabled = clamped > 0.001f;
            }

            if (m_CoreGlowImage != null)
            {
                if (!showCoreGlow)
                {
                    m_CoreGlowImage.enabled = false;
                    return;
                }

                Color glow = Color.Lerp(core, Color.white, 0.22f);
                glow.a = Mathf.Clamp01(clamped * 0.42f);
                m_CoreGlowImage.color = glow;
                m_CoreGlowImage.enabled = clamped > 0.001f;
                m_CoreGlowImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.08f, clamped);
            }
        }

        void TriggerHaptics(float amplitude, float duration)
        {
            if (amplitude <= 0.001f || duration <= 0.001f)
                return;

            SendHaptics(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand, amplitude, duration);
            SendHaptics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand, amplitude, duration);
        }

        static void SendHaptics(InputDeviceCharacteristics characteristics, float amplitude, float duration)
        {
            s_HapticDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(characteristics, s_HapticDevices);

            for (int i = 0; i < s_HapticDevices.Count; i++)
            {
                InputDevice device = s_HapticDevices[i];
                if (!device.isValid || !device.TryGetHapticCapabilities(out HapticCapabilities capabilities) || !capabilities.supportsImpulse)
                    continue;

                device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Max(0.01f, duration));
            }
        }

        void EnsureOverlay()
        {
            if (m_Canvas != null && m_FadeImage != null && m_VignetteImage != null && m_CoreGlowImage != null)
                return;

            GameObject canvasObject = new("TeleportViewEffectCanvas");
            canvasObject.transform.SetParent(transform, false);

            m_Canvas = canvasObject.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = 9995;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            m_FadeImage = CreateImage("TeleportDeepBlueFade", canvasObject.transform, null, true);
            m_VignetteImage = CreateImage("TeleportEdgeVignette", canvasObject.transform, ResolveVignetteSprite(), true);
            m_CoreGlowImage = CreateImage("TeleportCoreGlow", canvasObject.transform, ResolveCoreGlowSprite(), false);
            m_CoreGlowImage.rectTransform.sizeDelta = new Vector2(520f, 520f);
        }

        Image CreateImage(string name, Transform parent, Sprite sprite, bool fullScreen)
        {
            GameObject imageObject = new(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            if (fullScreen)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }

            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        Sprite ResolveVignetteSprite()
        {
            if (m_VignetteSprite != null)
                return m_VignetteSprite;

            m_VignetteSprite = CreateRadialSprite("Runtime_TeleportVignette", 128, 0.38f, 0.98f, invert: true);
            return m_VignetteSprite;
        }

        Sprite ResolveCoreGlowSprite()
        {
            if (m_CoreGlowSprite != null)
                return m_CoreGlowSprite;

            m_CoreGlowSprite = CreateRadialSprite("Runtime_TeleportCoreGlow", 96, 0.02f, 0.96f, invert: false);
            return m_CoreGlowSprite;
        }

        static Sprite CreateRadialSprite(string name, int size, float innerRadius, float outerRadius, bool invert)
        {
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            float center = (size - 1) * 0.5f;
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.InverseLerp(innerRadius, outerRadius, radius);
                    alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(alpha));
                    if (!invert)
                        alpha = 1f - alpha;

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        void RefreshCanvasCamera()
        {
            if (m_Canvas == null)
                return;

            Camera targetCamera = ResolveCamera();
            if (targetCamera == null)
            {
                m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                m_Canvas.worldCamera = null;
                return;
            }

            m_Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            m_Canvas.worldCamera = targetCamera;
            m_Canvas.planeDistance = Mathf.Max(targetCamera.nearClipPlane + 0.03f, m_MinCameraPlaneDistance);
        }

        static Camera ResolveCamera()
        {
            XROrigin playerRig = FindObjectOfType<XROrigin>();
            if (playerRig != null && playerRig.Camera != null)
                return playerRig.Camera;

            return Camera.main;
        }
    }
}
