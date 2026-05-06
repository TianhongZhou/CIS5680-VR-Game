using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

[AddComponentMenu("XR/Locomotion/Arm Swing Move Provider")]
[DisallowMultipleComponent]
public class ArmSwingMoveProvider : DynamicMoveProvider
{
    [Header("Arm Swing Detection")]
    [SerializeField]
    [Min(0f)]
    private float activationSwingSpeed = 0.14f;

    [SerializeField]
    [Min(0.01f)]
    private float maxInputSwingSpeed = 0.6f;

    [SerializeField]
    [Min(0f)]
    private float inputSmoothing = 8f;

    [SerializeField]
    [Min(0f)]
    private float supportSwingSpeed = 0.1f;

    [SerializeField]
    [Min(0.01f)]
    private float supportTimingWindow = 0.25f;

    [SerializeField]
    [Min(0f)]
    private float turnSuppressionDegreesPerSecond = 50f;

    [SerializeField]
    [Min(0f)]
    private float turnSuppressionSmoothing = 12f;

    [Header("Movement Defaults")]
    [SerializeField]
    [Min(0f)]
    private float defaultMoveSpeed = 3f;

    private Vector3 previousLeftLocalPosition;
    private Vector3 previousRightLocalPosition;
    private bool hasPreviousSample;
    private float currentForwardInput;
    private float previousHeadYaw;
    private float lastLeftSupportTime = float.NegativeInfinity;
    private float lastRightSupportTime = float.NegativeInfinity;
    private float lastLeftPrimaryTime = float.NegativeInfinity;
    private float lastRightPrimaryTime = float.NegativeInfinity;

    private void Reset()
    {
        ApplyProviderDefaults();
        AutoAssignReferences();
        ConfigureManualInputReaders();
    }

    private void OnValidate()
    {
        if (supportSwingSpeed > activationSwingSpeed)
            supportSwingSpeed = activationSwingSpeed;

        if (maxInputSwingSpeed < activationSwingSpeed + 0.01f)
            maxInputSwingSpeed = activationSwingSpeed + 0.01f;

        ApplyProviderDefaults();
        AutoAssignReferences();
        ConfigureManualInputReaders();
    }

    protected override void Awake()
    {
        ApplyProviderDefaults();
        AutoAssignReferences();
        base.Awake();
        ConfigureManualInputReaders();
    }

    protected new void OnEnable()
    {
        ConfigureManualInputReaders();
        ResetTracking();
        base.OnEnable();
    }

    protected new void OnDisable()
    {
        SetForwardInput(0f);
        ResetTracking();
        base.OnDisable();
    }

    protected new void Update()
    {
        AutoAssignReferences();
        ConfigureManualInputReaders();

        var targetForwardInput = ComputeSwingInput();
        SetForwardInput(targetForwardInput);

        base.Update();
    }

    private float ComputeSwingInput()
    {
        if (headTransform == null || leftControllerTransform == null || rightControllerTransform == null)
        {
            ResetTracking();
            return 0f;
        }

        var headYawRotation = Quaternion.Euler(0f, headTransform.eulerAngles.y, 0f);
        var headYawInverse = Quaternion.Inverse(headYawRotation);
        var headPosition = headTransform.position;

        var leftLocalPosition = headYawInverse * (leftControllerTransform.position - headPosition);
        var rightLocalPosition = headYawInverse * (rightControllerTransform.position - headPosition);
        var currentHeadYaw = headYawRotation.eulerAngles.y;

        if (!hasPreviousSample)
        {
            previousLeftLocalPosition = leftLocalPosition;
            previousRightLocalPosition = rightLocalPosition;
            previousHeadYaw = currentHeadYaw;
            hasPreviousSample = true;
            return 0f;
        }

        var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        var currentTime = Time.time;
        var headYawSpeed = Mathf.Abs(Mathf.DeltaAngle(previousHeadYaw, currentHeadYaw)) / deltaTime;
        previousHeadYaw = currentHeadYaw;

        if (headYawSpeed >= turnSuppressionDegreesPerSecond)
        {
            previousLeftLocalPosition = leftLocalPosition;
            previousRightLocalPosition = rightLocalPosition;
            ClearSwingTiming();

            var turnSuppressionFactor = 1f - Mathf.Exp(-turnSuppressionSmoothing * deltaTime);
            currentForwardInput = Mathf.Lerp(currentForwardInput, 0f, turnSuppressionFactor);

            if (currentForwardInput < 0.01f)
                currentForwardInput = 0f;

            return currentForwardInput;
        }

        var leftForwardSwingSpeed = Mathf.Abs(leftLocalPosition.z - previousLeftLocalPosition.z) / deltaTime;
        var rightForwardSwingSpeed = Mathf.Abs(rightLocalPosition.z - previousRightLocalPosition.z) / deltaTime;

        previousLeftLocalPosition = leftLocalPosition;
        previousRightLocalPosition = rightLocalPosition;

        if (leftForwardSwingSpeed >= supportSwingSpeed)
            lastLeftSupportTime = currentTime;

        if (rightForwardSwingSpeed >= supportSwingSpeed)
            lastRightSupportTime = currentTime;

        var leftPrimaryInput = 0f;
        if (leftForwardSwingSpeed >= activationSwingSpeed)
        {
            lastLeftPrimaryTime = currentTime;
            leftPrimaryInput = Mathf.InverseLerp(activationSwingSpeed, maxInputSwingSpeed, leftForwardSwingSpeed);
        }

        var rightPrimaryInput = 0f;
        if (rightForwardSwingSpeed >= activationSwingSpeed)
        {
            lastRightPrimaryTime = currentTime;
            rightPrimaryInput = Mathf.InverseLerp(activationSwingSpeed, maxInputSwingSpeed, rightForwardSwingSpeed);
        }

        var leftCanDrive = leftPrimaryInput > 0f && currentTime - lastRightSupportTime <= supportTimingWindow;
        var rightCanDrive = rightPrimaryInput > 0f && currentTime - lastLeftSupportTime <= supportTimingWindow;

        var targetInput = 0f;
        if (leftCanDrive)
            targetInput = Mathf.Max(targetInput, leftPrimaryInput);

        if (rightCanDrive)
            targetInput = Mathf.Max(targetInput, rightPrimaryInput);

        var smoothingFactor = 1f - Mathf.Exp(-inputSmoothing * deltaTime);
        currentForwardInput = Mathf.Lerp(currentForwardInput, targetInput, smoothingFactor);

        if (currentForwardInput < 0.01f)
            currentForwardInput = 0f;

        return currentForwardInput;
    }

    private void SetForwardInput(float value)
    {
        leftHandMoveInput.manualValue = new Vector2(0f, value);
        rightHandMoveInput.manualValue = Vector2.zero;
    }

    private void ConfigureManualInputReaders()
    {
        leftHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
        rightHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
    }

    private void ApplyProviderDefaults()
    {
        leftHandMovementDirection = MovementDirection.HeadRelative;
        rightHandMovementDirection = MovementDirection.HeadRelative;
        enableStrafe = false;
        enableFly = false;

        if (moveSpeed <= 0f)
            moveSpeed = defaultMoveSpeed;
    }

    private void ResetTracking()
    {
        hasPreviousSample = false;
        currentForwardInput = 0f;
        previousHeadYaw = 0f;
        ClearSwingTiming();
    }

    private void ClearSwingTiming()
    {
        lastLeftSupportTime = float.NegativeInfinity;
        lastRightSupportTime = float.NegativeInfinity;
        lastLeftPrimaryTime = float.NegativeInfinity;
        lastRightPrimaryTime = float.NegativeInfinity;
    }

    private void AutoAssignReferences()
    {
        var xrOriginRoot = transform.parent != null ? transform.parent.parent : null;
        if (xrOriginRoot == null)
            return;

        if (headTransform == null)
        {
            var head = FindDescendantByName(xrOriginRoot, "Main Camera");
            if (head != null)
                headTransform = head;
        }

        if (leftControllerTransform == null)
        {
            var leftController = FindDescendantByName(xrOriginRoot, "Left Controller");
            if (leftController != null)
                leftControllerTransform = leftController;
        }

        if (rightControllerTransform == null)
        {
            var rightController = FindDescendantByName(xrOriginRoot, "Right Controller");
            if (rightController != null)
                rightControllerTransform = rightController;
        }
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root.name == targetName)
            return root;

        for (var i = 0; i < root.childCount; i++)
        {
            var result = FindDescendantByName(root.GetChild(i), targetName);
            if (result != null)
                return result;
        }

        return null;
    }
}
