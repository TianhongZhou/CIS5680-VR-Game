using UnityEngine;

public class MovementModeManager : MonoBehaviour
{
    public enum MovementMode
    {
        NormalMove,
        ArmSwingMove,
    }

    [Header("Managed Move Objects")]
    [SerializeField]
    private GameObject normalMoveObject;

    [SerializeField]
    private GameObject armSwingMoveObject;

    [Header("Startup")]
    [SerializeField]
    private MovementMode defaultMode = MovementMode.NormalMove;

    [SerializeField]
    private bool applyDefaultModeOnStart = true;

    [SerializeField]
    private bool lockMovementOnStart;

    public MovementMode CurrentMode => currentMode;
    public bool IsMovementLocked => isMovementLocked;

    private MovementMode currentMode = MovementMode.NormalMove;
    private MovementMode lastUnlockedMode = MovementMode.NormalMove;
    private bool isMovementLocked;
    private static bool s_HasSessionMode;
    private static MovementMode s_SessionMode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSessionMode()
    {
        s_HasSessionMode = false;
        s_SessionMode = MovementMode.NormalMove;
    }

    private void Reset()
    {
        AutoAssignManagedObjects();
    }

    private void OnValidate()
    {
        AutoAssignManagedObjects();

        if (normalMoveObject != null && normalMoveObject == armSwingMoveObject)
        {
            Debug.LogWarning("MovementModeManager requires different GameObjects for normal move and arm-swing move.", this);
        }
    }

    private void Awake()
    {
        AutoAssignManagedObjects();

        MovementMode startupMode = ResolveStartupMode();
        currentMode = startupMode;
        lastUnlockedMode = startupMode;
        StoreSessionMode(startupMode);

        if (lockMovementOnStart)
        {
            LockMovement();
            return;
        }

        if (applyDefaultModeOnStart)
        {
            ApplyMode(startupMode);
        }
    }

    public void UseNormalMove()
    {
        SetMode(MovementMode.NormalMove);
    }

    public void UseArmSwingMove()
    {
        SetMode(MovementMode.ArmSwingMove);
    }

    public void ToggleMode()
    {
        if (currentMode == MovementMode.NormalMove)
        {
            SetMode(MovementMode.ArmSwingMove);
            return;
        }

        SetMode(MovementMode.NormalMove);
    }

    public void SetMode(MovementMode mode)
    {
        currentMode = mode;
        lastUnlockedMode = mode;
        StoreSessionMode(mode);

        if (isMovementLocked)
        {
            return;
        }

        ApplyMode(mode);
    }

    public static void ResetSessionModeToNormalMove()
    {
        StoreSessionMode(MovementMode.NormalMove);
    }

    public void LockMovement()
    {
        isMovementLocked = true;
        SetManagedObjectsActive(normalMoveActive: false, armSwingMoveActive: false);
    }

    public void UnlockMovement()
    {
        isMovementLocked = false;
        ApplyMode(lastUnlockedMode);
    }

    private MovementMode ResolveStartupMode()
    {
        return s_HasSessionMode ? s_SessionMode : defaultMode;
    }

    private static void StoreSessionMode(MovementMode mode)
    {
        s_SessionMode = mode;
        s_HasSessionMode = true;
    }

    private void ApplyMode(MovementMode mode)
    {
        switch (mode)
        {
            case MovementMode.NormalMove:
                SetManagedObjectsActive(normalMoveActive: true, armSwingMoveActive: false);
                break;

            case MovementMode.ArmSwingMove:
                SetManagedObjectsActive(normalMoveActive: false, armSwingMoveActive: true);
                break;
        }
    }

    private void SetManagedObjectsActive(bool normalMoveActive, bool armSwingMoveActive)
    {
        if (normalMoveObject != null)
        {
            normalMoveObject.SetActive(normalMoveActive);
        }

        if (armSwingMoveObject != null)
        {
            armSwingMoveObject.SetActive(armSwingMoveActive);
        }
    }

    private void AutoAssignManagedObjects()
    {
        Transform locomotionRoot = transform.parent;
        if (locomotionRoot == null)
        {
            return;
        }

        if (normalMoveObject == null)
        {
            Transform moveTransform = locomotionRoot.Find("Move");
            if (moveTransform != null)
            {
                normalMoveObject = moveTransform.gameObject;
            }
        }

        if (armSwingMoveObject == null)
        {
            Transform armSwingTransform = locomotionRoot.Find("Body Move");
            if (armSwingTransform != null)
            {
                armSwingMoveObject = armSwingTransform.gameObject;
            }
        }
    }
}
