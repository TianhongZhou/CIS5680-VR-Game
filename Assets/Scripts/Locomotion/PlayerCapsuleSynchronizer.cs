using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class PlayerCapsuleSynchronizer : MonoBehaviour
{
    [SerializeField] XROrigin xrOrigin;
    [SerializeField] CharacterController characterController;
    [SerializeField] Camera playerCamera;
    [SerializeField, Min(0.2f)] float minimumHeight = 0.8f;
    [SerializeField, Min(0.2f)] float maximumHeight = 2.4f;
    [SerializeField, Min(0f)] float positionSmoothing = 18f;

    bool hasAppliedCapsule;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureSynchronizersInScene();
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSynchronizersInScene();
    }

    static void EnsureSynchronizersInScene()
    {
        XROrigin[] origins = FindObjectsOfType<XROrigin>(true);
        for (int i = 0; i < origins.Length; i++)
        {
            XROrigin origin = origins[i];
            if (origin == null || origin.GetComponent<CharacterController>() == null)
                continue;

            if (origin.GetComponent<PlayerCapsuleSynchronizer>() == null)
                origin.gameObject.AddComponent<PlayerCapsuleSynchronizer>();
        }
    }

    void Reset()
    {
        ResolveReferences();
    }

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        if (maximumHeight < minimumHeight)
            maximumHeight = minimumHeight;
    }

    void LateUpdate()
    {
        ResolveReferences();
        SyncCapsuleToHeadset();
    }

    void ResolveReferences()
    {
        if (xrOrigin == null)
            xrOrigin = GetComponent<XROrigin>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (playerCamera == null && xrOrigin != null)
            playerCamera = xrOrigin.Camera != null ? xrOrigin.Camera : xrOrigin.GetComponentInChildren<Camera>(true);
    }

    void SyncCapsuleToHeadset()
    {
        if (xrOrigin == null || characterController == null || playerCamera == null)
            return;

        Vector3 cameraInOriginSpace = xrOrigin.CameraInOriginSpacePos;
        if (cameraInOriginSpace == Vector3.zero && playerCamera.transform != null)
            cameraInOriginSpace = xrOrigin.transform.InverseTransformPoint(playerCamera.transform.position);

        float cameraHeight = Mathf.Max(0f, cameraInOriginSpace.y);
        float minimumAllowedHeight = Mathf.Max(minimumHeight, characterController.radius * 2f + 0.01f);
        float targetHeight = Mathf.Clamp(cameraHeight, minimumAllowedHeight, Mathf.Max(maximumHeight, minimumAllowedHeight));
        float groundClearance = Mathf.Max(0f, characterController.skinWidth);
        Vector3 targetCenter = new(cameraInOriginSpace.x, targetHeight * 0.5f + groundClearance, cameraInOriginSpace.z);

        if (!hasAppliedCapsule || positionSmoothing <= 0f)
        {
            characterController.height = targetHeight;
            characterController.center = targetCenter;
            hasAppliedCapsule = true;
            return;
        }

        float t = 1f - Mathf.Exp(-positionSmoothing * Time.unscaledDeltaTime);
        characterController.height = Mathf.Lerp(characterController.height, targetHeight, t);
        characterController.center = Vector3.Lerp(characterController.center, targetCenter, t);
    }
}
