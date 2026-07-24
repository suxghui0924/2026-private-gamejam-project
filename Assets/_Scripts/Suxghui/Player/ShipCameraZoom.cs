using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Suxghui.Player
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class ShipCameraZoom : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private CinemachineCamera cinemaCamera;
        [SerializeField] private Camera gameplayCamera;

        [Header("Field Of View")]
        [SerializeField, Range(1f, 179f)] private float defaultFieldOfView = 65f;
        [SerializeField, Range(1f, 179f)] private float minimumFieldOfView = 35f;
        [SerializeField, Range(1f, 179f)] private float maximumFieldOfView = 90f;

        [Header("Mouse Wheel")]
        [SerializeField, Min(0.01f)] private float fieldOfViewPerScrollStep = 5f;
        [SerializeField, Min(0f)] private float zoomSharpness = 12f;
        [SerializeField] private bool invertScroll;

        private float _targetFieldOfView;
        private float _currentFieldOfView;

        public float TargetFieldOfView => _targetFieldOfView;

        private void Awake()
        {
            CacheCamera();
            NormalizeRange();
            _targetFieldOfView = Mathf.Clamp(
                defaultFieldOfView,
                minimumFieldOfView,
                maximumFieldOfView);
            _currentFieldOfView = _targetFieldOfView;
            ApplyFieldOfView(_currentFieldOfView);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;

            float wheelDelta = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheelDelta) < 0.01f)
                return;

            float scrollSteps = wheelDelta / 120f;
            float direction = invertScroll ? 1f : -1f;
            _targetFieldOfView = Mathf.Clamp(
                _targetFieldOfView + scrollSteps * fieldOfViewPerScrollStep * direction,
                minimumFieldOfView,
                maximumFieldOfView);
        }

        private void LateUpdate()
        {
            if (cinemaCamera == null && gameplayCamera == null)
            {
                CacheCamera();
                if (cinemaCamera == null && gameplayCamera == null)
                    return;
            }

            float blend = zoomSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-zoomSharpness * Time.unscaledDeltaTime);
            _currentFieldOfView = Mathf.Lerp(
                _currentFieldOfView,
                _targetFieldOfView,
                blend);
            ApplyFieldOfView(_currentFieldOfView);
        }

        public void ResetZoom()
        {
            _targetFieldOfView = Mathf.Clamp(
                defaultFieldOfView,
                minimumFieldOfView,
                maximumFieldOfView);
        }

        private void CacheCamera()
        {
            if (cinemaCamera == null)
                cinemaCamera = FindFirstObjectByType<CinemachineCamera>();
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
        }

        private void ApplyFieldOfView(float fieldOfView)
        {
            fieldOfView = Mathf.Clamp(fieldOfView, minimumFieldOfView, maximumFieldOfView);
            if (cinemaCamera != null)
            {
                LensSettings lens = cinemaCamera.Lens;
                lens.FieldOfView = fieldOfView;
                cinemaCamera.Lens = lens;
            }
            else if (gameplayCamera != null)
            {
                gameplayCamera.fieldOfView = fieldOfView;
            }
        }

        private void NormalizeRange()
        {
            minimumFieldOfView = Mathf.Clamp(minimumFieldOfView, 1f, 179f);
            maximumFieldOfView = Mathf.Clamp(maximumFieldOfView, minimumFieldOfView, 179f);
            defaultFieldOfView = Mathf.Clamp(
                defaultFieldOfView,
                minimumFieldOfView,
                maximumFieldOfView);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            NormalizeRange();
        }
#endif
    }
}
