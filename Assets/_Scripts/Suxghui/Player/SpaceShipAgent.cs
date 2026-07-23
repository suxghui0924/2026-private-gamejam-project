using _Scripts.Suxghui.Agent;
using _Scripts.Suxghui.Player.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace _Scripts.Suxghui.Player
{
    public class SpaceShipAgent : AgentAbstract
    {
        [field: SerializeField] public PlayerInputSO PlayerInput { get; private set; }

        [Header("Look")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0f)] private float rotationSharpness = 8f;
        [SerializeField, Min(0f)] private float bankAngle = 35f;
        [SerializeField, Min(0f)] private float turnSpeed = 90f;
        [SerializeField, Min(0f)] private float pitchAngle = 30f;
        [SerializeField, Min(0f)] private float mousePitchSensitivity = 0.08f;
        [SerializeField, Min(0f)] private float steerResponse = 6f;
        [SerializeField] private bool hideCursorOnEnable = true;

        [Header("Camera Feel")]
        [SerializeField] private CinemachineCamera cinemaCamera;
        [SerializeField] private BoosterSettingsSO boosterSettings;
        [SerializeField, Min(1f)] private float defaultFov = 65f;
        [SerializeField, Min(1f)] private float movingFov = 75f;
        [SerializeField, Min(0f)] private float fovSharpness = 6f;
        [SerializeField] private bool driveCameraDirectly = false;
        [SerializeField] private Vector3 cameraFollowOffset = new Vector3(0f, 4.5f, -11f);
        [SerializeField, Min(0f)] private float cameraFollowSharpness = 8f;

        private Vector2 _moveInput;
        private Vector2 _flyInput;
        private bool _cursorLocked;
        private float _yaw;
        private float _pitch;
        private float _roll;
        private float _steerInput;
        private float _boosterAmount;
        private bool _boosterInput;
        private Quaternion _initialShipRotation;

        protected override void Awake()
        {
            base.Awake();
            TryCacheCamera();
            TryCacheVisualRoot();
            _initialShipRotation = transform.localRotation;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            TryCacheCamera();
            TryCacheVisualRoot();
            SetCursorLocked(hideCursorOnEnable);

            if (PlayerInput == null)
                return;

            PlayerInput.OnMoveKeyPress += HandleMoveKeyPress;
            PlayerInput.OnFlyKeyPress += HandleFlyKeyPress;
            PlayerInput.OnBoosterPress += HandleBoosterPress;
        }

        private void Update()
        {
            if (!HealthComponent.CurrentHeartbeat) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);

            if (!_cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);

            UpdateRotation();
            UpdateCameraTransform();
            UpdateCameraFov();
        }

        private void FixedUpdate()
        {
            if (!HealthComponent.CurrentHeartbeat) return;
            if (MovementComponent == null)
                return;

            Transform ship = visualRoot != null ? visualRoot : transform;
            // This prefab's nose points along local -Y. W/S move forward/backward.
            Vector3 direction = -ship.up * _moveInput.y;

            float speedMultiplier = boosterSettings != null
                ? Mathf.Lerp(1f, boosterSettings.SpeedMultiplier, _boosterAmount)
                : 1f;
            MovementComponent.Move(Vector3.ClampMagnitude(direction, 1f), speedMultiplier);
        }

        private void OnDisable()
        {
            if (PlayerInput != null)
            {
                PlayerInput.OnMoveKeyPress -= HandleMoveKeyPress;
                PlayerInput.OnFlyKeyPress -= HandleFlyKeyPress;
                PlayerInput.OnBoosterPress -= HandleBoosterPress;
            }

            MovementComponent?.Stop();
            _boosterInput = false;
            _boosterAmount = 0f;
            SetCursorLocked(false);
        }

        private void HandleMoveKeyPress(Vector2 input, bool isPressed)
        {
            _moveInput = isPressed ? input : Vector2.zero;
        }

        private void HandleFlyKeyPress(Vector2 input, bool isPressed)
        {
            _flyInput = isPressed ? input : Vector2.zero;
        }

        private void HandleBoosterPress(bool isPressed)
        {
            _boosterInput = isPressed;
        }

        private void UpdateRotation()
        {
            Transform targetVisual = visualRoot != null ? visualRoot : transform;
            if (targetVisual == null)
                return;

            Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            float boosterResponse = boosterSettings != null ? boosterSettings.Acceleration : 12f;
            _boosterAmount = Mathf.MoveTowards(
                _boosterAmount,
                _boosterInput ? 1f : 0f,
                boosterResponse * Time.deltaTime);
            // Invert vertical mouse input so moving the mouse down raises the nose.
            _pitch = Mathf.Clamp(_pitch - mouseDelta.y * mousePitchSensitivity, -pitchAngle, pitchAngle);
            _steerInput = Mathf.MoveTowards(
                _steerInput,
                Mathf.Clamp(_moveInput.x, -1f, 1f),
                steerResponse * Time.deltaTime);

            float rollInput = _steerInput;
            _yaw += _steerInput * turnSpeed * Time.deltaTime;
            float targetRoll = rollInput * bankAngle;
            _roll = Mathf.MoveTowards(_roll, targetRoll, bankAngle * steerResponse * Time.deltaTime);

            // The model flies along local -Y, so roll must use its local Y axis.
            Quaternion headingRotation = Quaternion.AngleAxis(_yaw, Vector3.up);
            Quaternion rollRotation = Quaternion.AngleAxis(_roll, Vector3.up);
            Quaternion pitchRotation = Quaternion.AngleAxis(_pitch, Vector3.right);
            Quaternion targetRotation = headingRotation * _initialShipRotation * rollRotation * pitchRotation;
            float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            targetVisual.rotation = Quaternion.Slerp(targetVisual.rotation, targetRotation, blend);
        }

        private void UpdateCameraFov()
        {
            if (cinemaCamera == null)
                return;

            float maxSpeed = MovementComponent != null ? MovementComponent.MoveSpeed : 1f;
            float moveAmount = MovementComponent != null
                ? Mathf.Clamp01(MovementComponent.CurrentSpeed / Mathf.Max(0.01f, maxSpeed))
                : Mathf.Abs(_moveInput.y);
            float targetFov = Mathf.Lerp(defaultFov, movingFov, moveAmount);
            if (boosterSettings != null)
                targetFov = Mathf.Lerp(targetFov, boosterSettings.BoosterFov, _boosterAmount);
            LensSettings lens = cinemaCamera.Lens;
            lens.FieldOfView = Mathf.Lerp(
                lens.FieldOfView,
                targetFov,
                1f - Mathf.Exp(-fovSharpness * Time.deltaTime));
            cinemaCamera.Lens = lens;
        }

        private void UpdateCameraTransform()
        {
            if (!driveCameraDirectly || gameplayCamera == null)
                return;

            Vector3 targetPosition = transform.position + transform.rotation * cameraFollowOffset;
            Quaternion targetRotation = Quaternion.LookRotation(
                (transform.position - targetPosition).normalized,
                Vector3.up);

            float blend = 1f - Mathf.Exp(-cameraFollowSharpness * Time.deltaTime);
            gameplayCamera.transform.position = Vector3.Lerp(gameplayCamera.transform.position, targetPosition, blend);
            gameplayCamera.transform.rotation = Quaternion.Slerp(gameplayCamera.transform.rotation, targetRotation, blend);
        }

        private void TryCacheCamera()
        {
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (cinemaCamera == null)
                cinemaCamera = GetComponentInChildren<CinemachineCamera>(true);

        }

        private void TryCacheVisualRoot()
        {
            if (visualRoot == null)
                visualRoot = transform;
        }

        private void SetCursorLocked(bool isLocked)
        {
            _cursorLocked = isLocked;
            Cursor.visible = !_cursorLocked;
            Cursor.lockState = _cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}
