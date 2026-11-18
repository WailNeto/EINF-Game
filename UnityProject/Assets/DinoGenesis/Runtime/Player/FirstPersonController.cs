using UnityEngine;
using UnityEngine.InputSystem;

namespace DinoGenesis.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float jumpHeight = 1.15f;
        [SerializeField] private float gravity = -24f;
        [SerializeField] private float mouseSensitivity = 0.12f;

        private CharacterController characterController;
        private ExplorationHud hud;
        private float pitch;
        private float verticalSpeed;
        private bool inputEnabled;

        public Camera PlayerCamera => playerCamera;
        public bool InputEnabled => inputEnabled;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<Camera>();
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (hud != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    hud.TogglePause();
                }
                else if (keyboard.iKey.wasPressedThisFrame)
                {
                    hud.ToggleFieldNotes();
                }
                else if (keyboard.eKey.wasPressedThisFrame &&
                         (inputEnabled || hud.MarketOpen || hud.ResearchOpen || hud.ZooOpen))
                {
                    hud.ToggleInteraction();
                }
            }

            if (!inputEnabled)
            {
                return;
            }

            Look();
            Move(keyboard);
        }

        public void BindHud(ExplorationHud explorationHud)
        {
            hud = explorationHud;
        }

        public void SetCamera(Camera camera)
        {
            playerCamera = camera;
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }

        public void Teleport(Vector3 position)
        {
            var wasEnabled = characterController.enabled;
            characterController.enabled = false;
            transform.position = position;
            verticalSpeed = 0f;
            characterController.enabled = wasEnabled;
        }

        private void Look()
        {
            if (Mouse.current == null || playerCamera == null)
            {
                return;
            }

            var mouseDelta = Mouse.current.delta.ReadValue();
            transform.Rotate(Vector3.up, mouseDelta.x * mouseSensitivity, Space.World);
            pitch = Mathf.Clamp(pitch - mouseDelta.y * mouseSensitivity, -82f, 82f);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Move(Keyboard keyboard)
        {
            var movement = new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
            movement = Vector2.ClampMagnitude(movement, 1f);

            var speed = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed
                ? sprintSpeed
                : walkSpeed;
            var horizontalVelocity = transform.right * movement.x + transform.forward * movement.y;

            if (characterController.isGrounded && verticalSpeed < 0f)
            {
                verticalSpeed = -2f;
            }

            if (characterController.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            verticalSpeed += gravity * Time.deltaTime;
            var velocity = horizontalVelocity * speed + Vector3.up * verticalSpeed;
            characterController.Move(velocity * Time.deltaTime);
        }
    }
}
