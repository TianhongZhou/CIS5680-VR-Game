using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

public class MovementModeToggleInput : MonoBehaviour
{
    private enum ControllerButton
    {
        PrimaryButton,
        SecondaryButton,
    }

    [Header("Target")]
    [SerializeField]
    private MovementModeManager movementModeManager;

    [Header("VR Test Binding")]
    [SerializeField]
    private XRNode controllerNode = XRNode.LeftHand;

    [SerializeField]
    private ControllerButton controllerButton = ControllerButton.PrimaryButton;

    [Header("Editor Fallback")]
    [SerializeField]
    private Key keyboardToggleKey = Key.M;

    [SerializeField]
    private bool enableKeyboardFallback = true;

    [SerializeField]
    private bool logModeSwitch = true;

    private bool previousControllerPressed;
    private bool previousKeyboardPressed;

    private void Reset()
    {
        AutoAssignManager();
    }

    private void OnValidate()
    {
        AutoAssignManager();
    }

    private void Awake()
    {
        AutoAssignManager();
    }

    private void Update()
    {
        if (movementModeManager == null)
            return;

        var controllerPressed = IsControllerButtonPressed();
        if (controllerPressed && !previousControllerPressed)
        {
            ToggleMode("controller");
        }

        previousControllerPressed = controllerPressed;

        if (!enableKeyboardFallback || Keyboard.current == null)
            return;

        var keyboardPressed = Keyboard.current[keyboardToggleKey].isPressed;
        if (keyboardPressed && !previousKeyboardPressed)
        {
            ToggleMode("keyboard");
        }

        previousKeyboardPressed = keyboardPressed;
    }

    private bool IsControllerButtonPressed()
    {
        var device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (!device.isValid)
            return false;

        return controllerButton switch
        {
            ControllerButton.PrimaryButton => device.TryGetFeatureValue(XRCommonUsages.primaryButton, out var primaryPressed) && primaryPressed,
            ControllerButton.SecondaryButton => device.TryGetFeatureValue(XRCommonUsages.secondaryButton, out var secondaryPressed) && secondaryPressed,
            _ => false,
        };
    }

    private void ToggleMode(string source)
    {
        movementModeManager.ToggleMode();

        if (!logModeSwitch)
            return;

        Debug.Log($"Movement mode switched to {movementModeManager.CurrentMode} via {source}.", this);
    }

    private void AutoAssignManager()
    {
        if (movementModeManager == null)
            movementModeManager = GetComponent<MovementModeManager>();
    }
}
