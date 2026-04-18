using System.Collections;
using CIS5680VRGame.Gameplay;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CIS5680VRGame.UI
{
    public sealed class SceneTransitionService : MonoBehaviour
    {
        static SceneTransitionService s_Instance;

        [SerializeField] float m_FadeOutDuration = 0.65f;
        [SerializeField] float m_FadeInDuration = 0.85f;
        [SerializeField] float m_MinCameraPlaneDistance = 0.08f;

        Canvas m_Canvas;
        Image m_FadeImage;
        bool m_IsTransitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            EnsureCreated();
        }

        public static bool IsTransitioning => s_Instance != null && s_Instance.m_IsTransitioning;

        public static void LoadScene(string sceneName, AudioSource audioSourceToStop = null)
        {
            EnsureCreated();
            s_Instance.BeginTransition(sceneName, audioSourceToStop);
        }

        static void EnsureCreated()
        {
            if (s_Instance != null)
                return;

            var root = new GameObject("SceneTransitionService");
            s_Instance = root.AddComponent<SceneTransitionService>();
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
            SetFadeAlpha(0f);
        }

        void BeginTransition(string sceneName, AudioSource audioSourceToStop)
        {
            if (m_IsTransitioning || string.IsNullOrWhiteSpace(sceneName))
                return;

            ModalMenuPauseUtility.LockGameplayInputForTransition();
            StartCoroutine(LoadSceneRoutine(sceneName, audioSourceToStop));
        }

        IEnumerator LoadSceneRoutine(string sceneName, AudioSource audioSourceToStop)
        {
            m_IsTransitioning = true;
            EnsureOverlay();
            RefreshCanvasCamera();

            float initialMasterVolume = Mathf.Clamp01(AudioListener.volume);
            yield return FadeRoutine(
                startAlpha: 0f,
                endAlpha: 1f,
                duration: Mathf.Max(0.01f, m_FadeOutDuration),
                startMasterVolume: initialMasterVolume,
                endMasterVolume: 0f);

            if (audioSourceToStop != null)
            {
                audioSourceToStop.Stop();
            }

            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
            while (loadOperation != null && !loadOperation.isDone)
                yield return null;

            yield return WaitForCameraReady();
            ModalMenuPauseUtility.RefreshGameplayInputLockForTransition();
            RefreshCanvasCamera();
            SetFadeAlpha(1f);
            AudioListener.volume = 0f;

            yield return FadeRoutine(
                startAlpha: 1f,
                endAlpha: 0f,
                duration: Mathf.Max(0.01f, m_FadeInDuration),
                startMasterVolume: 0f,
                endMasterVolume: initialMasterVolume);

            SetFadeAlpha(0f);
            AudioListener.volume = initialMasterVolume;
            ModalMenuPauseUtility.UnlockGameplayInputAfterTransition();
            m_IsTransitioning = false;
        }

        IEnumerator FadeRoutine(
            float startAlpha,
            float endAlpha,
            float duration,
            float startMasterVolume = 1f,
            float endMasterVolume = 1f)
        {
            SetFadeAlpha(startAlpha);
            AudioListener.volume = Mathf.Clamp01(startMasterVolume);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                RefreshCanvasCamera();
                SetFadeAlpha(Mathf.Lerp(startAlpha, endAlpha, eased));
                AudioListener.volume = Mathf.Lerp(startMasterVolume, endMasterVolume, eased);

                yield return null;
            }

            SetFadeAlpha(endAlpha);
            AudioListener.volume = Mathf.Clamp01(endMasterVolume);
        }

        IEnumerator WaitForCameraReady()
        {
            const float timeout = 2f;
            float waited = 0f;
            while (ResolveCamera() == null && waited < timeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        void EnsureOverlay()
        {
            if (m_Canvas != null && m_FadeImage != null)
                return;

            GameObject canvasObject = new("SceneTransitionCanvas");
            canvasObject.transform.SetParent(transform, false);

            m_Canvas = canvasObject.AddComponent<Canvas>();
            m_Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            m_Canvas.sortingOrder = 10000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject fadeObject = new("Fade", typeof(RectTransform), typeof(Image));
            fadeObject.transform.SetParent(canvasObject.transform, false);
            RectTransform fadeRect = fadeObject.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;

            m_FadeImage = fadeObject.GetComponent<Image>();
            m_FadeImage.color = Color.black;
            m_FadeImage.raycastTarget = true;
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

        Camera ResolveCamera()
        {
            XROrigin playerRig = FindObjectOfType<XROrigin>();
            if (playerRig != null && playerRig.Camera != null)
                return playerRig.Camera;

            return Camera.main;
        }

        void SetFadeAlpha(float alpha)
        {
            if (m_FadeImage == null)
                return;

            Color color = m_FadeImage.color;
            color.a = Mathf.Clamp01(alpha);
            m_FadeImage.color = color;
        }
    }
}
