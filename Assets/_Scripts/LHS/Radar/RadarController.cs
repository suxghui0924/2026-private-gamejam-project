using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.LHS.Radar
{
    public sealed class RadarController : MonoBehaviour
    {
        private sealed class BlipState
        {
            public RadarBlipView View { get; }
            public float Alpha { get; set; }

            public BlipState(RadarBlipView view)
            {
                View = view;
                Alpha = 0f;
            }
        }

        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private RectTransform blipContainer;
        [SerializeField] private RadarBlipView blipPrefab;
        [SerializeField] private RectTransform sweepLine;
        [SerializeField] private RectTransform playerIcon;

        [Header("Radar")]
        [SerializeField, Min(1f)]
        private float detectionRange = 100f;

        [SerializeField, Min(0f)]
        private float edgePadding = 8f;

        [Header("Sweep")]
        [SerializeField, Min(1f)]
        private float sweepSpeed = 120f;

        [SerializeField, Min(0.01f)]
        private float blipFadeDuration = 1.5f;

        private readonly Dictionary<RadarTarget, BlipState> _blips = new();

        private float _sweepAngle;

        public float DetectionRange => detectionRange;

        private void OnEnable()
        {
            RadarTarget.Added += RegisterTarget;
            RadarTarget.Removed += UnregisterTarget;
            RadarTarget.Changed += RefreshTarget;

            foreach (RadarTarget target in RadarTarget.Targets)
            {
                RegisterTarget(target);
            }
        }

        private void OnDisable()
        {
            RadarTarget.Added -= RegisterTarget;
            RadarTarget.Removed -= UnregisterTarget;
            RadarTarget.Changed -= RefreshTarget;

            foreach (BlipState state in _blips.Values)
            {
                if (state.View != null)
                {
                    Destroy(state.View.gameObject);
                }
            }

            _blips.Clear();
        }

        private void LateUpdate()
        {
            if (player == null ||
                blipContainer == null ||
                sweepLine == null)
            {
                return;
            }

            UpdatePlayerIcon();

            float deltaTime = Time.unscaledDeltaTime;
            float previousSweepAngle = _sweepAngle;
            float sweepStep = sweepSpeed * deltaTime;

            _sweepAngle = Mathf.Repeat(
                _sweepAngle + sweepStep,
                360f
            );

            sweepLine.localEulerAngles =
                new Vector3(0f, 0f, -_sweepAngle);

            bool completedFullRotation = sweepStep >= 360f;

            foreach (
                KeyValuePair<RadarTarget, BlipState> pair in _blips)
            {
                RadarTarget target = pair.Key;
                BlipState state = pair.Value;

                if (target == null || state.View == null)
                {
                    continue;
                }

                UpdateBlip(
                    target,
                    state,
                    previousSweepAngle,
                    _sweepAngle,
                    completedFullRotation,
                    deltaTime
                );
            }
        }

        private void UpdatePlayerIcon()
        {
            if (playerIcon == null)
            {
                return;
            }

            playerIcon.localEulerAngles = new Vector3(
                0f,
                0f,
                -player.eulerAngles.y
            );
        }

        private void UpdateBlip(
            RadarTarget target,
            BlipState state,
            float previousSweepAngle,
            float currentSweepAngle,
            bool completedFullRotation,
            float deltaTime)
        {

            Vector3 worldOffset =
                target.transform.position - player.position;

            Vector2 flatOffset = new Vector2(
                worldOffset.x,
                worldOffset.z
            );

            float distance = flatOffset.magnitude;

            if (!target.IsVisible || distance > detectionRange)
            {
                state.Alpha = 0f;
                state.View.SetAlpha(0f);
                return;
            }

            float radarRadius = GetRadarRadius();

            Vector2 radarPosition =
                flatOffset / detectionRange * radarRadius;

            state.View.SetPosition(radarPosition);

            state.Alpha = Mathf.MoveTowards(
                state.Alpha,
                0f,
                deltaTime / blipFadeDuration
            );
            
            
            float targetAngle = Mathf.Repeat(
                Mathf.Atan2(
                    radarPosition.x,
                    radarPosition.y
                ) * Mathf.Rad2Deg,
                360f
            );

            bool sweepPassedTarget =
                completedFullRotation ||
                DidClockwiseSweepPass(
                    previousSweepAngle,
                    currentSweepAngle,
                    targetAngle
                );

            if (sweepPassedTarget)
            {
                state.Alpha = 1f;
            }

            state.View.SetAlpha(state.Alpha);
        }

        private float GetRadarRadius()
        {
            float width = blipContainer.rect.width;
            float height = blipContainer.rect.height;

            float radius = Mathf.Min(width, height) * 0.5f;

            return Mathf.Max(0f, radius - edgePadding);
        }

        private static bool DidClockwiseSweepPass(
            float previousAngle,
            float currentAngle,
            float targetAngle)
        {
            previousAngle = Mathf.Repeat(previousAngle, 360f);
            currentAngle = Mathf.Repeat(currentAngle, 360f);
            targetAngle = Mathf.Repeat(targetAngle, 360f);

            if (currentAngle >= previousAngle)
            {
                return targetAngle > previousAngle &&
                       targetAngle <= currentAngle;
            }

            return targetAngle > previousAngle ||
                   targetAngle <= currentAngle;
        }

        private void RegisterTarget(RadarTarget target)
        {
            if (target == null || _blips.ContainsKey(target))
            {
                return;
            }

            RadarBlipView view =
                Instantiate(blipPrefab, blipContainer);

            view.Initialize(target);

            _blips.Add(target, new BlipState(view));
        }

        private void UnregisterTarget(RadarTarget target)
        {
            if (!_blips.Remove(target, out BlipState state))
            {
                return;
            }

            if (state.View != null)
            {
                Destroy(state.View.gameObject);
            }
        }

        private void RefreshTarget(RadarTarget target)
        {
            if (target == null || !_blips.TryGetValue(target, out BlipState state))
                return;

            if (state.View != null)
                state.View.Initialize(target);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            detectionRange = Mathf.Max(1f, detectionRange);
            edgePadding = Mathf.Max(0f, edgePadding);
            sweepSpeed = Mathf.Max(1f, sweepSpeed);
            blipFadeDuration = Mathf.Max(0.01f, blipFadeDuration);
        }
#endif
    }
}
