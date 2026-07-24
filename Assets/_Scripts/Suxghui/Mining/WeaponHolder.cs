using System;
using System.Collections.Generic;
using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Manager.Module;
using _Scripts.Suxghui.Player;
using _Scripts.Suxghui.Player.Agent;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Suxghui.Mining
{
    [DefaultExecutionOrder(100)]
    public sealed class WeaponHolder : MonoBehaviour
    {
        [Serializable]
        private sealed class ProceduralIkChain
        {
            [SerializeField] private Transform[] bones = Array.Empty<Transform>();
            [SerializeField, Min(0)] private int lockedRootCount = 1;
            [SerializeField, Range(1f, 90f)] private float maxJointAngle = 22f;
            [SerializeField, Range(0.05f, 1f)] private float rootJointShare = 0.35f;
            [SerializeField] private Vector2 aimOffsetDegrees;
            [SerializeField, Range(0.1f, 2f)] private float responseMultiplier = 1f;
            [SerializeField] private Vector3 forcedCurlAxis = Vector3.right;
            [SerializeField, Range(-45f, 45f)] private float forcedCurlDegrees;
            private Quaternion[] _baseLocalRotations;
            private Vector3[] _baseLocalPositions;

            public float ResponseMultiplier => responseMultiplier;

            public void CapturePose()
            {
                _baseLocalRotations = new Quaternion[bones.Length];
                _baseLocalPositions = new Vector3[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null)
                    {
                        _baseLocalRotations[i] = bones[i].localRotation;
                        _baseLocalPositions[i] = bones[i].localPosition;
                    }
                }
            }

            public void RestorePose(float blend)
            {
                if (_baseLocalRotations == null || _baseLocalRotations.Length != bones.Length ||
                    _baseLocalPositions == null || _baseLocalPositions.Length != bones.Length)
                    CapturePose();

                for (int i = 0; i < bones.Length; i++)
                {
                    if (bones[i] != null)
                    {
                        bool isLockedRoot = i < lockedRootCount;
                        // Bone lengths never change. Moving a child bone opens visible gaps in a skinned mesh.
                        bones[i].localPosition = _baseLocalPositions[i];
                        bones[i].localRotation = isLockedRoot
                            ? _baseLocalRotations[i]
                            : Quaternion.Slerp(bones[i].localRotation, _baseLocalRotations[i], blend);
                    }
                }
            }

            public void Solve(Vector3 targetPoint, float weight)
            {
                if (bones == null || bones.Length < 2 || bones[^1] == null || weight <= 0f)
                    return;

                Transform end = bones[^1];
                int firstMovableBone = Mathf.Clamp(lockedRootCount, 0, bones.Length - 1);
                int movableJointCount = Mathf.Max(1, bones.Length - 1 - firstMovableBone);
                for (int i = bones.Length - 2; i >= firstMovableBone; i--)
                {
                    Transform bone = bones[i];
                    if (bone == null)
                        continue;

                    Vector3 toEnd = end.position - bone.position;
                    Vector3 toTarget = targetPoint - bone.position;
                    if (toEnd.sqrMagnitude < 0.000001f || toTarget.sqrMagnitude < 0.000001f)
                        continue;

                    Quaternion solvedWorldRotation = Quaternion.FromToRotation(toEnd, toTarget) * bone.rotation;
                    Quaternion parentRotation = bone.parent != null ? bone.parent.rotation : Quaternion.identity;
                    Quaternion solvedLocalRotation = Quaternion.Inverse(parentRotation) * solvedWorldRotation;
                    Quaternion localDelta = Quaternion.Inverse(_baseLocalRotations[i]) * solvedLocalRotation;
                    localDelta.ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180f)
                    {
                        angle = 360f - angle;
                        axis = -axis;
                    }

                    float jointOrder = movableJointCount <= 1
                        ? 1f
                        : (i - firstMovableBone) / (float)(movableJointCount - 1);
                    float jointShare = Mathf.Lerp(rootJointShare, 1f, jointOrder);
                    float limitedAngle = Mathf.Min(angle, maxJointAngle * jointShare);
                    Quaternion limitedLocalRotation = _baseLocalRotations[i] * Quaternion.AngleAxis(limitedAngle, axis);
                    bone.localRotation = Quaternion.Slerp(
                        bone.localRotation,
                        limitedLocalRotation,
                        Mathf.Clamp01(weight * jointShare));
                }
            }

            public Vector3 GetBiasedTarget(Vector3 targetPoint, Vector3 yawAxis, Vector3 pitchAxis)
            {
                if (bones == null || bones.Length == 0 || bones[0] == null)
                    return targetPoint;

                Vector3 origin = bones[0].position;
                Vector3 direction = targetPoint - origin;
                if (direction.sqrMagnitude < 0.000001f)
                    return targetPoint;

                Quaternion offset = Quaternion.AngleAxis(aimOffsetDegrees.x, yawAxis) *
                                    Quaternion.AngleAxis(aimOffsetDegrees.y, pitchAxis);
                return origin + offset * direction;
            }

            public void ApplyForcedCurl(float weight)
            {
                if (bones == null || bones.Length < 2 || Mathf.Abs(forcedCurlDegrees) < 0.01f ||
                    forcedCurlAxis.sqrMagnitude < 0.0001f || weight <= 0f)
                    return;

                int firstMovableBone = Mathf.Clamp(lockedRootCount, 0, bones.Length - 1);
                int movableJointCount = Mathf.Max(1, bones.Length - 1 - firstMovableBone);
                for (int i = firstMovableBone; i < bones.Length - 1; i++)
                {
                    if (bones[i] == null)
                        continue;

                    float jointOrder = movableJointCount <= 1
                        ? 1f
                        : (i - firstMovableBone) / (float)(movableJointCount - 1);
                    float jointShare = Mathf.Lerp(rootJointShare, 1f, jointOrder);
                    Quaternion curledRotation = _baseLocalRotations[i] * Quaternion.AngleAxis(
                        forcedCurlDegrees * weight * jointShare,
                        forcedCurlAxis.normalized);
                    bones[i].localRotation = Quaternion.Slerp(
                        bones[i].localRotation,
                        curledRotation,
                        Mathf.Clamp01(responseMultiplier * weight * 0.6f));
                }
            }
        }

        [Header("State")]
        [SerializeField] private WeaponHolderSO holderState;
        [SerializeField] private CrossHairComponent crossHair;
        [SerializeField] private MovmentComponent movementComponent;
        [SerializeField] private bool autoCreateMineableTargets = true;

        [Header("Drill")]
        [SerializeField] private Transform drillRoot;
        [SerializeField] private ProceduralIkChain drillChain = new ProceduralIkChain();

        [Header("Laser")]
        [SerializeField] private Transform laserRoot;
        [SerializeField] private Transform laserMuzzle;
        [SerializeField, Min(1f)] private float projectileSpeed = 90f;
        [SerializeField, Min(0.01f)] private float beamTickInterval = 0.1f;
        [SerializeField, Min(0.001f)] private float beamWidth = 0.08f;

        [Header("Extractor Tongs")]
        [SerializeField] private Transform extractorRoot;
        [SerializeField] private ProceduralIkChain[] extractorChains = Array.Empty<ProceduralIkChain>();

        [Header("Feedback")]
        [SerializeField] private Color readyColor = new Color(0.2f, 1f, 0.72f, 1f);
        [SerializeField] private Color outOfRangeColor = new Color(1f, 0.2f, 0.18f, 1f);
        [SerializeField] private Color laserColor = new Color(0.25f, 0.9f, 1f, 1f);
        [SerializeField, Min(0f)] private float animationSharpness = 10f;

        private readonly Dictionary<Renderer, Color> _baseRendererColors = new Dictionary<Renderer, Color>();
        private MaterialPropertyBlock _propertyBlock;
        private MiningTechType _currentType;
        private MiningTechDefinitionSO _currentDefinition;
        private MiningTechStats _currentStats;
        private Vector3 _drillBaseLocalPosition;
        private Vector3 _drillBaseLocalScale;
        private Vector3 _extractorBaseLocalPosition;
        private float _drillUseWeight;
        private float _extractorUseWeight;
        private float _actionTimer;
        private bool _fireHeld;
        private bool _targetInRange;
        private LineRenderer _beam;
        private bool _upgradeModulesSubscribed;
        private GameManager _upgradeManager;

        public MiningTechDefinitionSO CurrentWeapon => _currentDefinition;
        public MiningTechType CurrentType => _currentType;
        public int CurrentLevel { get; private set; }
        public event Action<MiningTechDefinitionSO> WeaponChanged;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            CacheReferences();
            CaptureBasePose();
            EnsureBeam();

            MiningTechType initialType = holderState != null && holderState.CurrentWeapon != null
                ? holderState.CurrentWeapon.TechType
                : MiningTechType.Drill;
            SetRootActive(drillRoot, initialType == MiningTechType.Drill);
            SetRootActive(laserRoot, initialType == MiningTechType.Laser);
            SetRootActive(extractorRoot, initialType == MiningTechType.Extractor);
        }

        private void Start()
        {
            GameManager manager = GameManager.Instance;
            if (holderState != null)
            {
                manager.ConfigureMiningTechUpgrades(
                    holderState.GetDefinition(MiningTechType.Drill),
                    holderState.GetDefinition(MiningTechType.Laser),
                    holderState.GetDefinition(MiningTechType.Extractor));
                SubscribeUpgradeModules(manager);
            }

            GameSaveData saveData = manager.SaveData;
            if (holderState != null)
            {
                holderState.SetLevel(MiningTechType.Drill, saveData.drillLevel);
                holderState.SetLevel(MiningTechType.Laser, saveData.laserLevel);
                holderState.SetLevel(MiningTechType.Extractor, saveData.extractorLevel);
            }

            SelectTech((MiningTechType)Mathf.Clamp(saveData.selectedMiningTool, 0, 2));
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                SelectTech((MiningTechType)(((int)_currentType + 1) % 3));

            _fireHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            _actionTimer -= Time.deltaTime;
            RefreshStatsAndTargetState();
            UpdateFeedback();
            UpdateWeaponAction();
        }

        private void LateUpdate()
        {
            AnimateDrill();
            AnimateExtractor();
            UpdateBeamPositions();
        }

        private void OnDisable()
        {
            crossHair?.ClearStatusColor();
            movementComponent?.SetExternalSpeedMultiplier(1f);
            SetBeamVisible(false);
            ClearWeaponTint();
            if (drillRoot != null)
                drillRoot.localPosition = _drillBaseLocalPosition;
            if (extractorRoot != null)
                extractorRoot.localPosition = _extractorBaseLocalPosition;
        }

        private void OnDestroy()
        {
            UnsubscribeUpgradeModules();
        }

        public void SelectTech(MiningTechType type)
        {
            _currentType = type;
            _currentDefinition = holderState != null ? holderState.GetDefinition(type) : null;
            holderState?.SetCurrentWeapon(type);

            if (_currentDefinition != null)
            {
                MiningTechUpgradeModule upgradeModule = GetUpgradeModule(type);
                int savedLevel = upgradeModule?.Level ?? GameManager.Instance.GetMiningTechLevel(_currentDefinition.TechId);
                CurrentLevel = Mathf.Clamp(savedLevel, 0, _currentDefinition.MaxLevel);
                holderState?.SetLevel(type, CurrentLevel);
                _currentStats = _currentDefinition.GetStats(CurrentLevel);
                GameManager.Instance.SetSelectedMiningTech((int)type, _currentDefinition.TechId);
            }

            SetRootActive(drillRoot, type == MiningTechType.Drill);
            SetRootActive(laserRoot, type == MiningTechType.Laser);
            SetRootActive(extractorRoot, type == MiningTechType.Extractor);
            SetBeamVisible(false);
            ClearWeaponTint();
            _actionTimer = 0f;
            ApplyMovementPenalty();
            WeaponChanged?.Invoke(_currentDefinition);
        }

        public bool UpgradeCurrent()
        {
            MiningTechUpgradeModule upgradeModule = GetUpgradeModule(_currentType);
            return upgradeModule != null && upgradeModule.TryUpgrade();
        }

        public void HandleLaserImpact(Collider targetCollider, MiningTechStats stats)
        {
            ApplyMining(targetCollider, MiningTechType.Laser, stats, 1f);
        }

        private void RefreshStatsAndTargetState()
        {
            if (_currentDefinition != null)
                _currentStats = _currentDefinition.GetStats(CurrentLevel);

            if (crossHair == null || !crossHair.HasTarget || crossHair.TargetCollider == null)
            {
                _targetInRange = false;
                return;
            }

            _targetInRange = Vector3.Distance(transform.position, crossHair.TargetPoint) <= _currentStats.Range;
        }

        private void UpdateFeedback()
        {
            if (crossHair == null)
                return;

            if (!crossHair.HasTarget)
            {
                crossHair.ClearStatusColor();
                ClearWeaponTint();
                return;
            }

            if (_currentType == MiningTechType.Laser)
            {
                crossHair.SetStatusColor(laserColor);
                ClearWeaponTint();
                return;
            }

            bool canMine = _targetInRange;
            if (canMine)
            {
                MineableAsteroid target = ResolveMineableTarget(crossHair.TargetCollider);
                canMine = target != null && target.ValidateMining(_currentType, _currentStats) == MiningFailureReason.None;
            }

            Color statusColor = canMine ? readyColor : outOfRangeColor;
            crossHair.SetStatusColor(statusColor);
            ApplyWeaponTint(_currentType == MiningTechType.Drill ? drillRoot : extractorRoot, statusColor);
        }

        private void UpdateWeaponAction()
        {
            switch (_currentType)
            {
                case MiningTechType.Drill:
                    UpdateCloseRangeMining(MiningTechType.Drill);
                    break;
                case MiningTechType.Laser:
                    UpdateLaser();
                    break;
                case MiningTechType.Extractor:
                    UpdateCloseRangeMining(MiningTechType.Extractor);
                    break;
            }
        }

        private void UpdateCloseRangeMining(MiningTechType type)
        {
            bool canUse = _fireHeld && _targetInRange && crossHair != null && crossHair.TargetCollider != null;
            if (!canUse || _actionTimer > 0f)
                return;

            ApplyMining(crossHair.TargetCollider, type, _currentStats, 1f);
            _actionTimer = _currentStats.ActionInterval;
        }

        private void UpdateLaser()
        {
            if (_currentStats.UsesContinuousBeam)
            {
                bool beamActive = _fireHeld && crossHair != null;
                SetBeamVisible(beamActive);
                if (!beamActive || !_targetInRange || !crossHair.HasTarget || _actionTimer > 0f)
                    return;

                MiningTechStats tickStats = _currentStats;
                if (tickStats.BeamDamagePerTick > 0f)
                    tickStats.DamagePerAction = tickStats.BeamDamagePerTick;
                float tickInterval = tickStats.BeamTickInterval > 0f
                    ? tickStats.BeamTickInterval
                    : beamTickInterval;
                ApplyMining(crossHair.TargetCollider, MiningTechType.Laser, tickStats, 1f);
                _actionTimer = tickInterval;
                return;
            }

            SetBeamVisible(false);
            if (!_fireHeld || _actionTimer > 0f || crossHair == null)
                return;

            Vector3 origin = laserMuzzle != null ? laserMuzzle.position : laserRoot.position;
            Vector3 direction = crossHair.HasTarget
                ? (crossHair.TargetPoint - origin).normalized
                : crossHair.CorrectedAimDirection;
            MiningLaserProjectile.Spawn(
                origin,
                direction,
                projectileSpeed,
                _currentStats.Range,
                this,
                _currentStats);
            _actionTimer = _currentStats.ActionInterval;
        }

        private void AnimateDrill()
        {
            if (drillRoot == null)
                return;

            float targetWeight = _currentType == MiningTechType.Drill && _fireHeld && _targetInRange ? 1f : 0f;
            _drillUseWeight = Damp(_drillUseWeight, targetWeight);
            drillRoot.localPosition = Vector3.Lerp(
                drillRoot.localPosition,
                _drillBaseLocalPosition,
                FrameBlend());
            drillRoot.localScale = _drillBaseLocalScale * _currentStats.VisualScaleMultiplier;

            drillChain.RestorePose(FrameBlend());
            if (_currentType == MiningTechType.Drill && crossHair != null && _drillUseWeight > 0.001f)
                drillChain.Solve(crossHair.TargetPoint, 0.45f * _drillUseWeight);
        }

        private void AnimateExtractor()
        {
            if (extractorRoot == null)
                return;

            float targetWeight = _currentType == MiningTechType.Extractor && _fireHeld && _targetInRange ? 1f : 0f;
            _extractorUseWeight = Damp(_extractorUseWeight, targetWeight);
            extractorRoot.localPosition = Vector3.Lerp(
                extractorRoot.localPosition,
                _extractorBaseLocalPosition,
                FrameBlend());

            for (int i = 0; i < extractorChains.Length; i++)
            {
                ProceduralIkChain chain = extractorChains[i];
                chain?.RestorePose(FrameBlend());
                if (chain == null || _currentType != MiningTechType.Extractor ||
                    crossHair == null || _extractorUseWeight <= 0.001f)
                    continue;

                Vector3 targetPoint = crossHair.TargetCollider != null
                    ? crossHair.TargetCollider.bounds.center
                    : crossHair.TargetPoint;
                Camera gameplayCamera = Camera.main;
                Vector3 yawAxis = gameplayCamera != null ? gameplayCamera.transform.up : transform.up;
                Vector3 pitchAxis = gameplayCamera != null ? gameplayCamera.transform.right : transform.right;
                targetPoint = chain.GetBiasedTarget(targetPoint, yawAxis, pitchAxis);
                chain.Solve(targetPoint, 0.35f * chain.ResponseMultiplier * _extractorUseWeight);
                chain.ApplyForcedCurl(_extractorUseWeight);
            }
        }

        private void ApplyMining(
            Collider targetCollider,
            MiningTechType type,
            MiningTechStats stats,
            float damageMultiplier)
        {
            MineableAsteroid target = ResolveMineableTarget(targetCollider);
            if (target == null)
                return;

            target.ApplyMining(type, stats, damageMultiplier);
        }

        private MineableAsteroid ResolveMineableTarget(Collider targetCollider)
        {
            if (targetCollider == null)
                return null;

            MineableAsteroid target = targetCollider.GetComponentInParent<MineableAsteroid>();
            if (target == null && autoCreateMineableTargets)
                target = targetCollider.gameObject.AddComponent<MineableAsteroid>();
            return target;
        }

        private void CacheReferences()
        {
            if (crossHair == null)
                crossHair = GetComponentInParent<CrossHairComponent>();
            if (movementComponent == null)
                movementComponent = GetComponentInParent<MovmentComponent>();
            if (laserMuzzle == null)
                laserMuzzle = laserRoot;
        }

        private void CaptureBasePose()
        {
            if (drillRoot != null)
            {
                _drillBaseLocalPosition = drillRoot.localPosition;
                _drillBaseLocalScale = drillRoot.localScale;
            }
            if (extractorRoot != null)
                _extractorBaseLocalPosition = extractorRoot.localPosition;

            drillChain.CapturePose();
            foreach (ProceduralIkChain chain in extractorChains)
                chain?.CapturePose();
        }

        private void ApplyMovementPenalty()
        {
            movementComponent?.SetExternalSpeedMultiplier(
                _currentType == MiningTechType.Laser ? _currentStats.MovementMultiplier : 1f);
        }

        private MiningTechUpgradeModule GetUpgradeModule(MiningTechType type)
        {
            GameManager manager = GameManager.Instance;
            return type switch
            {
                MiningTechType.Drill => manager.DrillUpgrade,
                MiningTechType.Laser => manager.LaserUpgrade,
                MiningTechType.Extractor => manager.ExtractorUpgrade,
                _ => null
            };
        }

        private void SubscribeUpgradeModules(GameManager manager)
        {
            if (_upgradeModulesSubscribed)
                return;

            if (manager.DrillUpgrade != null)
                manager.DrillUpgrade.Upgraded += HandleDrillUpgraded;
            if (manager.LaserUpgrade != null)
                manager.LaserUpgrade.Upgraded += HandleLaserUpgraded;
            if (manager.ExtractorUpgrade != null)
                manager.ExtractorUpgrade.Upgraded += HandleExtractorUpgraded;
            _upgradeManager = manager;
            _upgradeModulesSubscribed = true;
        }

        private void UnsubscribeUpgradeModules()
        {
            if (!_upgradeModulesSubscribed)
                return;

            GameManager manager = _upgradeManager;
            if (manager == null)
            {
                _upgradeManager = null;
                _upgradeModulesSubscribed = false;
                return;
            }
            if (manager.DrillUpgrade != null)
                manager.DrillUpgrade.Upgraded -= HandleDrillUpgraded;
            if (manager.LaserUpgrade != null)
                manager.LaserUpgrade.Upgraded -= HandleLaserUpgraded;
            if (manager.ExtractorUpgrade != null)
                manager.ExtractorUpgrade.Upgraded -= HandleExtractorUpgraded;
            _upgradeManager = null;
            _upgradeModulesSubscribed = false;
        }

        private void HandleDrillUpgraded(int level, MiningTechStats stats)
        {
            HandleTechUpgraded(MiningTechType.Drill, level, stats);
        }

        private void HandleLaserUpgraded(int level, MiningTechStats stats)
        {
            HandleTechUpgraded(MiningTechType.Laser, level, stats);
        }

        private void HandleExtractorUpgraded(int level, MiningTechStats stats)
        {
            HandleTechUpgraded(MiningTechType.Extractor, level, stats);
        }

        private void HandleTechUpgraded(MiningTechType type, int level, MiningTechStats stats)
        {
            holderState?.SetLevel(type, level);
            if (_currentType != type)
                return;

            CurrentLevel = level;
            _currentStats = stats;
            ApplyMovementPenalty();
        }

        private void EnsureBeam()
        {
            if (_beam != null)
                return;

            GameObject beamObject = new GameObject("Continuous Mining Beam");
            beamObject.transform.SetParent(transform, false);
            _beam = beamObject.AddComponent<LineRenderer>();
            _beam.positionCount = 2;
            _beam.useWorldSpace = true;
            _beam.startWidth = beamWidth;
            _beam.endWidth = beamWidth * 0.6f;
            _beam.startColor = laserColor;
            _beam.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, 0.25f);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
                _beam.material = new Material(shader) { color = laserColor };
            SetBeamVisible(false);
        }

        private void UpdateBeamPositions()
        {
            if (_beam == null || !_beam.enabled || crossHair == null)
                return;

            Vector3 origin = laserMuzzle != null ? laserMuzzle.position : laserRoot.position;
            Vector3 end = crossHair.HasTarget
                ? crossHair.TargetPoint
                : origin + crossHair.CorrectedAimDirection * _currentStats.Range;
            _beam.SetPosition(0, origin);
            _beam.SetPosition(1, end);
        }

        private void ApplyWeaponTint(Transform weaponRoot, Color statusColor)
        {
            ClearWeaponTint();
            if (weaponRoot == null)
                return;

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            foreach (Renderer renderer in weaponRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (!_baseRendererColors.TryGetValue(renderer, out Color baseColor))
                {
                    Material material = renderer.sharedMaterial;
                    baseColor = material != null && material.HasProperty("_BaseColor")
                        ? material.GetColor("_BaseColor")
                        : Color.white;
                    _baseRendererColors[renderer] = baseColor;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", Color.Lerp(baseColor, statusColor, 0.55f));
                _propertyBlock.SetColor("_EmissionColor", statusColor * 1.5f);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void ClearWeaponTint()
        {
            foreach (Renderer renderer in _baseRendererColors.Keys)
                if (renderer != null)
                    renderer.SetPropertyBlock(null);
        }

        private void SetBeamVisible(bool visible)
        {
            if (_beam != null)
                _beam.enabled = visible;
        }

        private static void SetRootActive(Transform root, bool active)
        {
            if (root != null)
                root.gameObject.SetActive(active);
        }

        private float Damp(float current, float target)
        {
            return Mathf.Lerp(current, target, FrameBlend());
        }

        private float FrameBlend()
        {
            return 1f - Mathf.Exp(-animationSharpness * Time.deltaTime);
        }
    }
}
