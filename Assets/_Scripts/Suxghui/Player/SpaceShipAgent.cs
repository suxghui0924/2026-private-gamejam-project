using System;
using System.Collections.Generic;
using _Scripts.Suxghui.Agent;
using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Player.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace _Scripts.Suxghui.Player
{
    public class SpaceShipAgent : AgentAbstract
    {
        [field: SerializeField] public PlayerInputSO PlayerInput { get; private set; }

        /// <summary>플레이어가 설정한 기본 쓰로틀 (0~1). 부스터 오버플로우는 제외.</summary>
        public float Throttle01 => Mathf.Clamp01(_throttle01);
        public float ThrottleAmount => Mathf.Max(0f, _forwardSpeedFactor);

        /// <summary>실제 반영되는 쓰로틀 퍼센트. 부스터 오버플로우 포함(예: 120%). UI 텍스트용.</summary>
        public float ThrottlePercent => _forwardSpeedFactor * 100f;

        [Header("Look")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform visualRoot;
        [SerializeField, Min(0f)] private float rotationSharpness = 8f;
        [SerializeField, Min(0f)] private float bankAngle = 35f;
        [SerializeField, Min(0f)] private float turnSpeed = 90f;
        [SerializeField, Min(0f)] private float pitchAngle = 30f;
        [SerializeField, Min(0f)] private float mousePitchSensitivity = 0.08f;
        [SerializeField, Min(0f)] private float steerResponse = 6f;
        [SerializeField, Min(0f)] private float forwardAcceleration = 1.8f;
        [SerializeField, Min(0f)] private float forwardDeceleration = 1.2f;
        [Tooltip("W/S로 쓰로틀이 초당 얼마나 변하는지(0~1 기준). 0.5면 0%→100%까지 약 2초.")]
        [SerializeField, Min(0.01f)] private float throttleChangeRate = 0.5f;
        [SerializeField] private bool hideCursorOnEnable = true;

        [Header("Fuel")]
        [Tooltip("Fuel consumed per second while flying at 100% throttle.")]
        [SerializeField, Min(0f)] private float fuelConsumptionPerSecondAtFullThrottle = 0.8f;
        [Tooltip("Additional fuel multiplier while the booster is active.")]
        [SerializeField, Min(1f)] private float boosterFuelMultiplier = 1.8f;

        [Header("Camera Feel")]
        [SerializeField] private CinemachineCamera cinemaCamera;
        [SerializeField] private BoosterSettingsSO boosterSettings;
        [SerializeField, Min(1f)] private float defaultFov = 65f;
        [SerializeField, Min(1f)] private float movingFov = 75f;
        [SerializeField, Min(0f)] private float fovSharpness = 6f;
        [SerializeField] private bool driveCameraDirectly = false;
        [SerializeField] private Vector3 cameraFollowOffset = new Vector3(0f, 4.5f, -11f);
        [SerializeField, Min(0f)] private float cameraFollowSharpness = 8f;

        [Header("Jet Engine VFX")]
        [Tooltip("일반 이동 시 재생되는 제트엔진 불꽃. 비워두면 자식 중 이름이 JetEngineVFX 로 시작하는 오브젝트에서 자동으로 찾는다.")]
        [SerializeField] private ParticleSystem[] jetEngineVfx = Array.Empty<ParticleSystem>();
        [Tooltip("부스터 사용 시 재생되는 파란색 제트엔진 불꽃(BlueVer).")]
        [SerializeField] private ParticleSystem[] boosterJetEngineVfx = Array.Empty<ParticleSystem>();
        [Tooltip("이 값보다 전진 입력이 크면 제트엔진 불꽃을 재생한다.")]
        [SerializeField, Range(0f, 1f)] private float jetThrottleThreshold = 0.05f;

        private bool _jetPlaying;
        private bool _boosterJetPlaying;

        private Vector2 _moveInput;
        private Vector2 _flyInput;
        private bool _cursorLocked;
        private float _yaw;
        private float _pitch;
        private float _roll;
        private float _steerInput;
        private float _forwardSpeedFactor;
        private float _throttle01;
        private float _fovMoveAmount;
        private float _boosterAmount;
        private bool _boosterInput;
        private Quaternion _initialShipRotation;

        protected override void Awake()
        {
            base.Awake();
            if (GetComponent<ShipUpgradeRuntime>() == null)
                gameObject.AddComponent<ShipUpgradeRuntime>();
            TryCacheCamera();
            TryCacheVisualRoot();
            _initialShipRotation = transform.localRotation;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            TryCacheCamera();
            TryCacheVisualRoot();
            TryCacheJetEngineVfx();
            SetCursorLocked(hideCursorOnEnable);

            // Some jet particles have "Play On Awake" enabled, so force them off first and let the
            // movement/booster state drive them from here on.
            _jetPlaying = true;
            _boosterJetPlaying = true;
            StopAllJetEngineVfx();

            if (PlayerInput == null)
                return;
            
            PlayerInput.OnMoveKeyPress += HandleMoveKeyPress;
            PlayerInput.OnFlyKeyPress += HandleFlyKeyPress;
            PlayerInput.OnBoosterPress += HandleBoosterPress;
        }

        private void Update()
        {
            if (!HealthComponent.currentHeartbeat)
            {
                if (_cursorLocked)
                    SetCursorLocked(false);
                StopAllJetEngineVfx();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);

            if (!_cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);

            UpdateRotation();
            UpdateMovement(Time.deltaTime);
            UpdateJetEngineVfx();
            UpdateCameraTransform();
            UpdateCameraFov();
        }

        private void UpdateMovement(float deltaTime)
        {
            if (MovementComponent == null)
                return;

            Transform ship = visualRoot != null ? visualRoot : transform;

            // W/S adjust a persistent throttle (0~1) like a real fighter, instead of pushing the ship
            // forward/back directly. W raises it, S lowers it, and it holds its value with no key held.
            _throttle01 = Mathf.Clamp01(_throttle01 + _moveInput.y * throttleChangeRate * deltaTime);

            float targetSpeedMultiplier = boosterSettings != null
                ? Mathf.Lerp(1f, boosterSettings.SpeedMultiplier, _boosterAmount)
                : 1f;
            float targetSpeedFactor = _throttle01 * targetSpeedMultiplier;
            float throttleResponse = Mathf.Abs(targetSpeedFactor) > Mathf.Abs(_forwardSpeedFactor)
                ? forwardAcceleration
                : forwardDeceleration;
            _forwardSpeedFactor = Mathf.MoveTowards(
                _forwardSpeedFactor,
                targetSpeedFactor,
                throttleResponse * deltaTime);

            if (!ConsumeMovementFuel(deltaTime))
            {
                _throttle01 = 0f;
                _forwardSpeedFactor = 0f;
                MovementComponent.Stop();
                return;
            }

            // This prefab's nose points along local -Y. Keep the direction a unit vector and pass the
            // throttle factor (can exceed 1 during a booster overflow, e.g. 1.2) as the speed
            // multiplier: MovmentComponent clamps each direction component to 0~1, so routing the
            // overflow through the direction would cap the booster at 100%. The multiplier path
            // is applied after that clamp and stays uncapped.
            Vector3 direction = -ship.up;
            MovementComponent.Move(direction, _forwardSpeedFactor, deltaTime);
        }

        private bool ConsumeMovementFuel(float deltaTime)
        {
            float throttle = Mathf.Abs(_forwardSpeedFactor);
            if (throttle <= 0.0001f || fuelConsumptionPerSecondAtFullThrottle <= 0f)
                return true;

            GameManager manager = GameManager.Instance;
            if (manager == null)
                return true;

            float boosterMultiplier = _boosterInput ? boosterFuelMultiplier : 1f;
            float requestedFuel = fuelConsumptionPerSecondAtFullThrottle * throttle *
                                  boosterMultiplier * Mathf.Max(0f, deltaTime);
            float consumedFuel = manager.ConsumeFuel(requestedFuel);
            return consumedFuel + 0.0001f >= requestedFuel;
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
            _forwardSpeedFactor = 0f;
            _throttle01 = 0f;
            _boosterInput = false;
            _boosterAmount = 0f;
            StopAllJetEngineVfx();
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

            // Kicking in the booster snaps the throttle to full (e.g. 20% -> 100%). The booster's
            // speed multiplier then pushes the effective throttle past 100% (overflow, e.g. 120%).
            // Releasing leaves the throttle at 100%, so it drops from 120% back down to 100%.
            if (isPressed)
                _throttle01 = 1f;
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
            _fovMoveAmount = Mathf.Lerp(
                _fovMoveAmount,
                moveAmount,
                1f - Mathf.Exp(-fovSharpness * Time.deltaTime));
            float targetFov = Mathf.Lerp(defaultFov, movingFov, _fovMoveAmount);
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

        private void TryCacheJetEngineVfx()
        {
            bool alreadyBound = (jetEngineVfx != null && jetEngineVfx.Length > 0) ||
                                (boosterJetEngineVfx != null && boosterJetEngineVfx.Length > 0);
            if (alreadyBound)
                return;

            List<ParticleSystem> normal = new List<ParticleSystem>();
            List<ParticleSystem> booster = new List<ParticleSystem>();
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("JetEngineVFX", StringComparison.OrdinalIgnoreCase))
                    continue;

                ParticleSystem ps = child.GetComponent<ParticleSystem>();
                if (ps == null)
                    ps = child.GetComponentInChildren<ParticleSystem>(true);
                if (ps == null)
                    continue;

                if (child.name.IndexOf("BlueVer", StringComparison.OrdinalIgnoreCase) >= 0)
                    booster.Add(ps);
                else
                    normal.Add(ps);
            }

            jetEngineVfx = normal.ToArray();
            boosterJetEngineVfx = booster.ToArray();
        }

        private void UpdateJetEngineVfx()
        {
            // Flames follow the throttle: whenever the engine is actually thrusting (throttle > 0)
            // the flame shows, and it cuts out the instant the throttle drops to zero.
            bool moving = _throttle01 > jetThrottleThreshold;
            bool boosting = moving && _boosterInput;

            // While boosting, the blue booster flames replace the normal ones.
            SetParticlesPlaying(boosterJetEngineVfx, boosting, ref _boosterJetPlaying);
            SetParticlesPlaying(jetEngineVfx, moving && !boosting, ref _jetPlaying);
        }

        private void StopAllJetEngineVfx()
        {
            SetParticlesPlaying(boosterJetEngineVfx, false, ref _boosterJetPlaying);
            SetParticlesPlaying(jetEngineVfx, false, ref _jetPlaying);
        }

        private static void SetParticlesPlaying(ParticleSystem[] systems, bool shouldPlay, ref bool state)
        {
            if (systems == null || systems.Length == 0 || state == shouldPlay)
                return;

            foreach (ParticleSystem ps in systems)
            {
                if (ps == null)
                    continue;

                if (shouldPlay)
                    ps.Play(true);
                else
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            state = shouldPlay;
        }

        private void SetCursorLocked(bool isLocked)
        {
            _cursorLocked = isLocked;
            Cursor.visible = !_cursorLocked;
            Cursor.lockState = _cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}
