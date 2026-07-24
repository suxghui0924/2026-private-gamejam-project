using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Suxghui.Player
{
    [DefaultExecutionOrder(50)]
    public class CrossHairComponent : MonoBehaviour
    {
        [Header("Aim")]
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private Vector3 localAimDirection = Vector3.down;
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField, Min(1f)] private float maxDistance = 500f;
        [SerializeField, Min(1f)] private float fallbackDistance = 120f;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Aim Assist")]
        [SerializeField] private bool raycastFromCrossHairPosition = true;
        [SerializeField, Min(0f)] private float aimAssistRadius = 1.5f;
        [SerializeField, Min(0f)] private float nearAimAssistRadius = 4f;
        [SerializeField, Min(0f)] private float nearAimAssistDistance = 35f;
        [SerializeField, Min(0f)] private float targetStickDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float centerCorrection = 0.75f;
        [SerializeField, Min(0f)] private float preferredTargetDepthTolerance = 2f;

        [Header("Crosshair UI")]
        [SerializeField] private Sprite crossHairSprite;
        [SerializeField] private Vector2 crossHairSize = new Vector2(32f, 32f);
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color hitColor = new Color(0.4f, 0.95f, 1f, 1f);
        [SerializeField, Min(0f)] private float positionSharpness = 16f;
        [SerializeField, Min(0f)] private float screenPadding = 24f;
        [SerializeField] private bool hideWhenBehindCamera = true;

        private readonly RaycastHit[] _hits = new RaycastHit[64];
        private readonly RaycastHit[] _occlusionHits = new RaycastHit[32];
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _crossHairRect;
        private Image _crossHairImage;
        private Transform _shipRoot;
        private bool _ownsCanvas;
        private bool _positionInitialized;
        private bool _hasStatusColor;
        private Color _statusColor;
        private Collider _stickyTarget;
        private float _stickyTargetUntil;
        private float _targetingDistance = float.PositiveInfinity;
        private string _preferredTargetTag = string.Empty;

        private float EffectiveMaxDistance => Mathf.Min(maxDistance, _targetingDistance);

        public bool HasTarget { get; private set; }
        public Vector3 TargetPoint { get; private set; }
        public Vector3 TargetSurfacePoint { get; private set; }
        public Vector3 TargetSurfaceNormal { get; private set; }
        public Collider TargetCollider { get; private set; }
        public Vector3 CorrectedAimDirection
        {
            get
            {
                Vector3 origin = aimOrigin != null ? aimOrigin.position : transform.position;
                Vector3 direction = TargetPoint - origin;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : GetAimDirection();
            }
        }

        private void Awake()
        {
            CacheReferences();
            EnsureUi();
        }

        private void OnEnable()
        {
            CacheReferences();
            EnsureUi();

            if (_crossHairImage != null)
                _crossHairImage.enabled = true;
        }

        private void LateUpdate()
        {
            if (gameplayCamera == null)
            {
                CacheReferences();
                if (gameplayCamera == null)
                    return;
            }

            if (_crossHairRect == null)
            {
                EnsureUi();
                if (_crossHairRect == null)
                    return;
            }

            UpdateAimTarget();
            UpdateCrossHairPosition();
        }

        private void OnDisable()
        {
            if (_crossHairImage != null)
                _crossHairImage.enabled = false;

            _positionInitialized = false;
        }

        private void OnDestroy()
        {
            if (_ownsCanvas && _canvas != null)
                Destroy(_canvas.gameObject);
        }

        private void CacheReferences()
        {
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (aimOrigin == null)
                aimOrigin = transform;

            _shipRoot = transform.root;
        }

        private void EnsureUi()
        {
            if (_crossHairRect != null || crossHairSprite == null)
                return;

            GameObject canvasObject = new GameObject("CrossHair Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SetUiLayer(canvasObject);

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRect = canvasObject.GetComponent<RectTransform>();

            GameObject crossHairObject = new GameObject("CrossHair", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            SetUiLayer(crossHairObject);
            crossHairObject.transform.SetParent(canvasObject.transform, false);

            _crossHairRect = crossHairObject.GetComponent<RectTransform>();
            _crossHairRect.anchorMin = new Vector2(0.5f, 0.5f);
            _crossHairRect.anchorMax = new Vector2(0.5f, 0.5f);
            _crossHairRect.pivot = new Vector2(0.5f, 0.5f);
            _crossHairRect.sizeDelta = crossHairSize;

            _crossHairImage = crossHairObject.GetComponent<Image>();
            _crossHairImage.sprite = crossHairSprite;
            _crossHairImage.preserveAspect = true;
            _crossHairImage.raycastTarget = false;
            _crossHairImage.color = idleColor;
            _ownsCanvas = true;
        }

        private void UpdateAimTarget()
        {
            Vector3 direction = GetAimDirection();
            Vector3 origin = aimOrigin.position;
            Ray shipRay = new Ray(origin, direction);
            Vector3 fallbackPoint = origin + direction * Mathf.Min(fallbackDistance, EffectiveMaxDistance);

            HasTarget = false;
            TargetCollider = null;
            TargetPoint = fallbackPoint;
            TargetSurfacePoint = fallbackPoint;
            TargetSurfaceNormal = -direction;

            TryGetDirectHit(shipRay, out RaycastHit shipHit);
            RaycastHit nearestHit = default;

            if (raycastFromCrossHairPosition)
            {
                Vector3 screenReferencePoint = shipHit.collider != null ? shipHit.point : fallbackPoint;
                Vector3 screenPoint = gameplayCamera.WorldToScreenPoint(screenReferencePoint);
                if (screenPoint.z > 0f)
                {
                    Ray crossHairRay = gameplayCamera.ScreenPointToRay(screenPoint);
                    TryGetAssistedHit(crossHairRay, out nearestHit);
                }
            }

            if (nearestHit.collider == null)
                nearestHit = shipHit.collider != null ? shipHit : GetAssistedShipHit(shipRay);

            if (nearestHit.collider != null)
            {
                Vector3 surfacePoint = GetTargetSurfacePoint(
                    nearestHit.collider,
                    shipRay,
                    out Vector3 surfaceNormal);

                Vector3 correctedPoint = Vector3.Lerp(
                    surfacePoint,
                    nearestHit.collider.bounds.center,
                    centerCorrection);
                if (!IsPointInFront(correctedPoint))
                    correctedPoint = surfacePoint;

                if (IsPointInFront(correctedPoint))
                {
                    TargetSurfacePoint = surfacePoint;
                    TargetSurfaceNormal = surfaceNormal;
                    TargetPoint = correctedPoint;
                    TargetCollider = nearestHit.collider;
                    HasTarget = true;
                    _stickyTarget = nearestHit.collider;
                    _stickyTargetUntil = Time.unscaledTime + targetStickDuration;
                }
            }
            else if (TryKeepStickyTarget(shipRay, out Vector3 stickyPoint))
            {
                Vector3 correctedPoint = Vector3.Lerp(
                    stickyPoint,
                    _stickyTarget.bounds.center,
                    centerCorrection);
                TargetPoint = IsPointInFront(correctedPoint) ? correctedPoint : stickyPoint;
                if (IsPointInFront(TargetPoint))
                {
                    TargetSurfacePoint = stickyPoint;
                    TargetSurfaceNormal = GetApproximateSurfaceNormal(
                        _stickyTarget,
                        stickyPoint,
                        -shipRay.direction);
                    TargetCollider = _stickyTarget;
                    HasTarget = true;
                }
            }

            UpdateCrossHairColor();
        }

        private RaycastHit GetAssistedShipHit(Ray shipRay)
        {
            TryGetAssistedHit(shipRay, out RaycastHit assistedHit);
            return assistedHit;
        }

        private bool TryGetDirectHit(Ray ray, out RaycastHit nearestHit)
        {
            int hitCount = Physics.RaycastNonAlloc(ray, _hits, EffectiveMaxDistance, hitLayers, triggerInteraction);
            return TryGetNearestValidHit(hitCount, out nearestHit);
        }

        private bool TryGetAssistedHit(Ray ray, out RaycastHit nearestHit)
        {
            bool hasDirectHit = TryGetDirectHit(ray, out RaycastHit directHit);
            if (hasDirectHit && IsPreferredTarget(directHit.collider))
            {
                nearestHit = directHit;
                return true;
            }

            RaycastHit fallbackHit = hasDirectHit ? directHit : default;

            if (nearAimAssistRadius > 0f && nearAimAssistDistance > 0f)
            {
                int nearHitCount = Physics.SphereCastNonAlloc(
                    ray,
                    nearAimAssistRadius,
                    _hits,
                    Mathf.Min(EffectiveMaxDistance, nearAimAssistDistance),
                    hitLayers,
                    triggerInteraction);
                if (TryGetBestAssistedHit(ray, nearHitCount, out RaycastHit nearHit))
                {
                    if (IsPreferredTarget(nearHit.collider))
                    {
                        nearestHit = nearHit;
                        return true;
                    }
                    if (fallbackHit.collider == null)
                        fallbackHit = nearHit;
                }
            }

            if (aimAssistRadius <= 0f)
            {
                nearestHit = fallbackHit;
                return nearestHit.collider != null;
            }

            int hitCount = Physics.SphereCastNonAlloc(
                ray,
                aimAssistRadius,
                _hits,
                EffectiveMaxDistance,
                hitLayers,
                triggerInteraction);
            if (TryGetBestAssistedHit(ray, hitCount, out RaycastHit assistedHit))
            {
                if (IsPreferredTarget(assistedHit.collider))
                {
                    nearestHit = assistedHit;
                    return true;
                }
                if (fallbackHit.collider == null)
                    fallbackHit = assistedHit;
            }

            nearestHit = fallbackHit;
            return nearestHit.collider != null;
        }

        public void GetAimSolution(out Vector3 origin, out Vector3 direction)
        {
            origin = aimOrigin != null ? aimOrigin.position : transform.position;
            direction = CorrectedAimDirection;
        }

        public void SetTargetingDistance(float distance)
        {
            _targetingDistance = Mathf.Max(1f, distance);
        }

        public void ClearTargetingDistance()
        {
            _targetingDistance = float.PositiveInfinity;
        }

        public void SetPreferredTargetTag(string targetTag)
        {
            string normalizedTag = targetTag ?? string.Empty;
            if (string.Equals(_preferredTargetTag, normalizedTag, System.StringComparison.Ordinal))
                return;

            _preferredTargetTag = normalizedTag;
            _stickyTarget = null;
            _stickyTargetUntil = 0f;
        }

        public void SetStatusColor(Color color)
        {
            _hasStatusColor = true;
            _statusColor = color;
            UpdateCrossHairColor();
        }

        public void ClearStatusColor()
        {
            _hasStatusColor = false;
            UpdateCrossHairColor();
        }

        private void UpdateCrossHairColor()
        {
            if (_crossHairImage == null)
                return;

            _crossHairImage.color = _hasStatusColor
                ? _statusColor
                : HasTarget ? hitColor : idleColor;
        }

        private bool TryGetNearestValidHit(int hitCount, out RaycastHit nearestHit)
        {
            nearestHit = default;
            float nearestDistance = EffectiveMaxDistance;
            RaycastHit nearestPreferredHit = default;
            float nearestPreferredDistance = EffectiveMaxDistance;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == null || IsOwnCollider(hit.collider))
                    continue;

                Vector3 hitPoint = hit.point != Vector3.zero
                    ? hit.point
                    : hit.collider.ClosestPoint(hit.transform.position);
                if (!IsPointInFront(hitPoint))
                    continue;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestHit = hit;
                }

                if (IsPreferredTarget(hit.collider) && hit.distance < nearestPreferredDistance)
                {
                    nearestPreferredDistance = hit.distance;
                    nearestPreferredHit = hit;
                }
            }

            if (nearestPreferredHit.collider != null &&
                (nearestHit.collider == null ||
                 nearestPreferredDistance <= nearestDistance + preferredTargetDepthTolerance))
                nearestHit = nearestPreferredHit;

            return nearestHit.collider != null;
        }

        private bool TryGetBestAssistedHit(Ray aimRay, int hitCount, out RaycastHit bestHit)
        {
            bestHit = default;
            float bestScore = float.PositiveInfinity;
            RaycastHit bestPreferredHit = default;
            float bestPreferredScore = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == null || IsOwnCollider(hit.collider))
                    continue;

                Vector3 center = hit.collider.bounds.center;
                float distanceAlongRay = Vector3.Dot(center - aimRay.origin, aimRay.direction);
                if (distanceAlongRay <= 0f || distanceAlongRay > EffectiveMaxDistance)
                    continue;

                Vector3 pointOnRay = aimRay.GetPoint(distanceAlongRay);
                Vector3 surfacePoint = hit.collider.ClosestPoint(pointOnRay);
                if (!IsPointInFront(surfacePoint))
                    continue;

                float angularError = Vector3.Distance(pointOnRay, surfacePoint) /
                                     Mathf.Max(1f, distanceAlongRay);
                float distanceTieBreaker = distanceAlongRay / EffectiveMaxDistance * 0.001f;
                float score = angularError + distanceTieBreaker;
                if (IsOccluded(hit.collider, aimRay.origin))
                    continue;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestHit = hit;
                }

                if (IsPreferredTarget(hit.collider) && score < bestPreferredScore)
                {
                    bestPreferredScore = score;
                    bestPreferredHit = hit;
                }
            }

            if (bestPreferredHit.collider != null)
                bestHit = bestPreferredHit;

            return bestHit.collider != null;
        }

        private bool TryKeepStickyTarget(Ray aimRay, out Vector3 targetPoint)
        {
            targetPoint = default;
            if (_stickyTarget == null || Time.unscaledTime > _stickyTargetUntil || IsOwnCollider(_stickyTarget))
            {
                _stickyTarget = null;
                return false;
            }

            Vector3 center = _stickyTarget.bounds.center;
            float distanceAlongRay = Vector3.Dot(center - aimRay.origin, aimRay.direction);
            if (distanceAlongRay <= 0f || distanceAlongRay > EffectiveMaxDistance)
                return false;
            if (IsOccluded(_stickyTarget, aimRay.origin))
            {
                _stickyTarget = null;
                return false;
            }

            float assistRadius = distanceAlongRay <= nearAimAssistDistance
                ? nearAimAssistRadius
                : aimAssistRadius;
            Vector3 pointOnRay = aimRay.GetPoint(distanceAlongRay);
            Vector3 surfacePoint = _stickyTarget.ClosestPoint(pointOnRay);
            if (Vector3.Distance(pointOnRay, surfacePoint) > assistRadius)
                return false;

            targetPoint = surfacePoint;
            if (!IsPointInFront(targetPoint))
                return false;

            _stickyTargetUntil = Time.unscaledTime + targetStickDuration;
            return true;
        }

        private Vector3 GetTargetSurfacePoint(
            Collider targetCollider,
            Ray aimRay,
            out Vector3 surfaceNormal)
        {
            if (targetCollider.Raycast(aimRay, out RaycastHit exactHit, EffectiveMaxDistance))
            {
                surfaceNormal = exactHit.normal;
                return exactHit.point;
            }

            Vector3 center = targetCollider.bounds.center;
            float distanceAlongRay = Mathf.Clamp(
                Vector3.Dot(center - aimRay.origin, aimRay.direction),
                0f,
                EffectiveMaxDistance);
            Vector3 surfacePoint = targetCollider.ClosestPoint(aimRay.GetPoint(distanceAlongRay));
            surfaceNormal = GetApproximateSurfaceNormal(targetCollider, surfacePoint, -aimRay.direction);
            return surfacePoint;
        }

        private static Vector3 GetApproximateSurfaceNormal(
            Collider targetCollider,
            Vector3 surfacePoint,
            Vector3 fallback)
        {
            Vector3 normal = surfacePoint - targetCollider.bounds.center;
            if (normal.sqrMagnitude < 0.000001f)
                normal = fallback;
            return normal.sqrMagnitude > 0.000001f ? normal.normalized : Vector3.up;
        }

        private bool IsOccluded(Collider targetCollider, Vector3 rayOrigin)
        {
            Vector3 toTarget = targetCollider.bounds.center - rayOrigin;
            float targetDistance = toTarget.magnitude;
            if (targetDistance < 0.0001f)
                return false;

            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                toTarget / targetDistance,
                _occlusionHits,
                Mathf.Min(targetDistance + 0.05f, EffectiveMaxDistance),
                hitLayers,
                triggerInteraction);
            Collider nearestCollider = null;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _occlusionHits[i];
                if (hit.collider == null || IsOwnCollider(hit.collider) || hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                nearestCollider = hit.collider;
            }

            return nearestCollider != null && !IsSameTarget(nearestCollider, targetCollider);
        }

        private static bool IsSameTarget(Collider first, Collider second)
        {
            if (first == second)
                return true;
            if (first.attachedRigidbody != null && first.attachedRigidbody == second.attachedRigidbody)
                return true;

            Transform firstTransform = first.transform;
            Transform secondTransform = second.transform;
            return firstTransform.IsChildOf(secondTransform) || secondTransform.IsChildOf(firstTransform);
        }

        private bool IsPointInFront(Vector3 point)
        {
            Vector3 origin = aimOrigin != null ? aimOrigin.position : transform.position;
            Vector3 toPoint = point - origin;
            if (toPoint.sqrMagnitude < 0.0001f || Vector3.Dot(toPoint.normalized, GetAimDirection()) <= 0f)
                return false;

            return gameplayCamera == null ||
                   Vector3.Dot(point - gameplayCamera.transform.position, gameplayCamera.transform.forward) > 0f;
        }

        private void UpdateCrossHairPosition()
        {
            Vector3 screenPoint = gameplayCamera.WorldToScreenPoint(TargetPoint);
            bool isBehindCamera = screenPoint.z <= 0f;

            if (isBehindCamera && hideWhenBehindCamera)
            {
                _crossHairImage.enabled = false;
                _positionInitialized = false;
                return;
            }

            _crossHairImage.enabled = true;
            Rect pixelRect = gameplayCamera.pixelRect;
            float padding = Mathf.Min(screenPadding, Mathf.Min(pixelRect.width, pixelRect.height) * 0.5f);
            screenPoint.x = Mathf.Clamp(screenPoint.x, pixelRect.xMin + padding, pixelRect.xMax - padding);
            screenPoint.y = Mathf.Clamp(screenPoint.y, pixelRect.yMin + padding, pixelRect.yMax - padding);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screenPoint,
                    null,
                    out Vector2 targetPosition))
                return;

            if (!_positionInitialized || positionSharpness <= 0f)
            {
                _crossHairRect.anchoredPosition = targetPosition;
                _positionInitialized = true;
                return;
            }

            float blend = 1f - Mathf.Exp(-positionSharpness * Time.unscaledDeltaTime);
            _crossHairRect.anchoredPosition = Vector2.Lerp(
                _crossHairRect.anchoredPosition,
                targetPosition,
                blend);
        }

        private Vector3 GetAimDirection()
        {
            Vector3 direction = localAimDirection.sqrMagnitude > 0.0001f
                ? localAimDirection.normalized
                : Vector3.down;
            return aimOrigin.TransformDirection(direction).normalized;
        }

        private bool IsOwnCollider(Collider targetCollider)
        {
            return _shipRoot != null && targetCollider.transform.IsChildOf(_shipRoot);
        }

        private bool IsPreferredTarget(Collider targetCollider)
        {
            if (targetCollider == null || string.IsNullOrEmpty(_preferredTargetTag))
                return false;

            return string.Equals(
                GetNearestResourceTag(targetCollider.transform),
                _preferredTargetTag,
                System.StringComparison.Ordinal);
        }

        private static string GetNearestResourceTag(Transform start)
        {
            for (Transform current = start; current != null; current = current.parent)
            {
                string currentTag = current.tag;
                if (currentTag == "Ore" || currentTag == "Stone")
                    return currentTag;
            }

            return string.Empty;
        }

        private static void SetUiLayer(GameObject target)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                target.layer = uiLayer;
        }

        private void OnDrawGizmosSelected()
        {
            Transform origin = aimOrigin != null ? aimOrigin : transform;
            Vector3 direction = localAimDirection.sqrMagnitude > 0.0001f
                ? origin.TransformDirection(localAimDirection.normalized)
                : -origin.up;

            Gizmos.color = HasTarget ? Color.cyan : Color.white;
            Gizmos.DrawLine(
                origin.position,
                origin.position + direction * Mathf.Min(fallbackDistance, EffectiveMaxDistance));
        }
    }
}
