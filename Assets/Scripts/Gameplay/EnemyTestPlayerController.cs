using UnityEngine;
using UnityEngine.InputSystem;

namespace CIS5680VRGame.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerHealth))]
    public class EnemyTestPlayerController : MonoBehaviour
    {
        [SerializeField] Transform m_CameraPivot;
        [SerializeField] Camera m_PlayerCamera;
        [SerializeField] float m_MoveSpeed = 3.5f;
        [SerializeField] float m_MouseSensitivity = 0.12f;
        [SerializeField] float m_Gravity = -20f;
        [SerializeField] float m_JumpHeight = 1.15f;
        [SerializeField] bool m_ShowHud = true;

        CharacterController m_CharacterController;
        PlayerHealth m_PlayerHealth;
        Vector3 m_Velocity;
        float m_Pitch;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            ResolveCursorState();
            HandleLook();
            HandleMovement();
        }

        void OnGUI()
        {
            if (!m_ShowHud || m_PlayerHealth == null)
                return;

            GUILayout.BeginArea(new Rect(16f, 16f, 330f, 150f), GUI.skin.box);
            GUILayout.Label("Enemy Prototype Test");
            GUILayout.Label("WASD Move | Mouse Look | Space Jump | Esc Toggle Cursor");
            GUILayout.Space(6f);
            GUILayout.Label($"Health: {m_PlayerHealth.CurrentHealth} / {m_PlayerHealth.MaxHealth}");
            GUILayout.Label(m_PlayerHealth.IsDead ? "Status: Dead" : "Status: Alive");
            GUILayout.Label("Walk into the scout's path to verify contact damage.");
            GUILayout.EndArea();
        }

        void ResolveReferences()
        {
            if (m_CharacterController == null)
                m_CharacterController = GetComponent<CharacterController>();

            if (m_PlayerHealth == null)
                m_PlayerHealth = GetComponent<PlayerHealth>();

            if (m_CameraPivot == null)
            {
                Transform pivot = transform.Find("CameraPivot");
                if (pivot != null)
                    m_CameraPivot = pivot;
            }

            if (m_PlayerCamera == null)
                m_PlayerCamera = GetComponentInChildren<Camera>(true);
        }

        void ResolveCursorState()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                bool lockCursor = Cursor.lockState != CursorLockMode.Locked;
                Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !lockCursor;
            }
        }

        void HandleLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 mouseDelta = mouse.delta.ReadValue();
            float mouseX = mouseDelta.x * m_MouseSensitivity;
            float mouseY = mouseDelta.y * m_MouseSensitivity;

            transform.Rotate(0f, mouseX, 0f);

            if (m_CameraPivot == null)
                return;

            m_Pitch = Mathf.Clamp(m_Pitch - mouseY, -80f, 80f);
            m_CameraPivot.localRotation = Quaternion.Euler(m_Pitch, 0f, 0f);
        }

        void HandleMovement()
        {
            if (m_CharacterController == null)
                return;

            bool grounded = m_CharacterController.isGrounded;
            if (grounded && m_Velocity.y < 0f)
                m_Velocity.y = -2f;

            Keyboard keyboard = Keyboard.current;
            float horizontal = 0f;
            float vertical = 0f;
            bool jumpPressed = false;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed)
                    horizontal -= 1f;
                if (keyboard.dKey.isPressed)
                    horizontal += 1f;
                if (keyboard.sKey.isPressed)
                    vertical -= 1f;
                if (keyboard.wKey.isPressed)
                    vertical += 1f;

                jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
            }

            Vector3 input = new Vector3(horizontal, 0f, vertical);
            input = Vector3.ClampMagnitude(input, 1f);

            Vector3 move = transform.TransformDirection(input) * Mathf.Max(0f, m_MoveSpeed);
            m_CharacterController.Move(move * Time.deltaTime);

            if (grounded && jumpPressed)
                m_Velocity.y = Mathf.Sqrt(Mathf.Max(0.01f, m_JumpHeight) * -2f * Mathf.Min(-0.01f, m_Gravity));

            m_Velocity.y += m_Gravity * Time.deltaTime;
            m_CharacterController.Move(m_Velocity * Time.deltaTime);
        }
    }
}
