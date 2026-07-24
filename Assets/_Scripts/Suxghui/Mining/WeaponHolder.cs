using System;
using System.Collections.Generic;
using _Scripts.LSO.Data;
using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Manager.Module;
using _Scripts.Suxghui.Player;
using _Scripts.Suxghui.Player.Agent;
using Unity.Cinemachine;
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
            private Vector3 _baseAnchorLocalPosition;
            private Quaternion _baseAnchorLocalRotation;
            private bool _hasBaseAnchorPose;

            public float ResponseMultiplier => responseMultiplier;
            public bool IsValid
            {
                get
                {
                    if (bones == null || bones.Length < 2)
                        return false;

                    for (int i = 0; i < bones.Length; i++)
                    {
                        if (bones[i] == null || i > 0 && bones[i].parent != bones[i - 1])
                            return false;
                    }

                    return true;
                }
            }
            public Vector3 RootPosition => IsValid ? bones[0].position : Vector3.zero;
            public Vector3 EndPosition => IsValid ? bones[^1].position : Vector3.zero;
            public Transform EndTransform => IsValid ? bones[^1] : null;
            private Transform Anchor => IsValid ? bones[0].parent : null;

            /// <summary>Total world-space length of the chain in its captured rest pose.</summary>
            public float RestLength
            {
                get
                {
                    if (!IsValid || _baseLocalPositions == null ||
                        _baseLocalPositions.Length != bones.Length)
                        return 0f;

                    float length = 0f;
                    for (int i = 1; i < bones.Length; i++)
                    {
                        Transform parent = bones[i - 1];
                        if (parent != null)
                            length += parent.TransformVector(_baseLocalPositions[i]).magnitude;
                    }

                    return length;
                }
            }

            public bool TryAutoBind(Transform searchRoot)
            {
                if (searchRoot == null)
                    return false;
                if (IsValid && bones[0].IsChildOf(searchRoot))
                    return true;

                SkinnedMeshRenderer[] renderers =
                    searchRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    Transform rootBone = renderer.rootBone;
                    if (rootBone == null || !rootBone.IsChildOf(searchRoot))
                        continue;

                    List<Transform> chain = BuildBoneChain(rootBone);
                    if (chain.Count < 2)
                        continue;

                    bones = chain.ToArray();
                    return true;
                }

                Transform namedRoot = FindNamedBone(searchRoot);
                if (namedRoot == null)
                    return false;

                List<Transform> namedChain = BuildBoneChain(namedRoot);
                if (namedChain.Count < 2)
                    return false;

                bones = namedChain.ToArray();
                return true;
            }

            private static List<Transform> BuildBoneChain(Transform rootBone)
            {
                List<Transform> chain = new List<Transform>();
                Transform current = rootBone;
                while (current != null)
                {
                    chain.Add(current);
                    Transform next = null;
                    for (int i = 0; i < current.childCount; i++)
                    {
                        Transform child = current.GetChild(i);
                        if (child.name.StartsWith("Bone", StringComparison.OrdinalIgnoreCase))
                        {
                            next = child;
                            break;
                        }
                    }

                    current = next;
                }

                return chain;
            }

            private static Transform FindNamedBone(Transform root)
            {
                if (root.name.Equals("Bone", StringComparison.OrdinalIgnoreCase))
                    return root;

                for (int i = 0; i < root.childCount; i++)
                {
                    Transform match = FindNamedBone(root.GetChild(i));
                    if (match != null)
                        return match;
                }

                return null;
            }

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

                Transform anchor = Anchor;
                if (anchor != null)
                {
                    _baseAnchorLocalPosition = anchor.localPosition;
                    _baseAnchorLocalRotation = anchor.localRotation;
                    _hasBaseAnchorPose = true;
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

            public void RestoreAnchor(float blend)
            {
                Transform anchor = Anchor;
                if (anchor == null)
                    return;
                if (!_hasBaseAnchorPose)
                    CapturePose();

                anchor.localPosition = Vector3.Lerp(anchor.localPosition, _baseAnchorLocalPosition, blend);
                anchor.localRotation = Quaternion.Slerp(anchor.localRotation, _baseAnchorLocalRotation, blend);
            }

            public void AimRootAt(Vector3 targetPoint, float weight, float maximumAngle)
            {
                if (!IsValid || _baseLocalRotations == null ||
                    _baseLocalRotations.Length != bones.Length || weight <= 0f)
                    return;

                Transform root = bones[0];
                Vector3 currentDirection = EndPosition - root.position;
                Vector3 targetDirection = targetPoint - root.position;
                if (currentDirection.sqrMagnitude < 0.000001f || targetDirection.sqrMagnitude < 0.000001f)
                    return;

                Quaternion solvedWorldRotation =
                    Quaternion.FromToRotation(currentDirection, targetDirection) * root.rotation;
                Quaternion parentRotation = root.parent != null ? root.parent.rotation : Quaternion.identity;
                Quaternion solvedLocalRotation = Quaternion.Inverse(parentRotation) * solvedWorldRotation;
                Quaternion localDelta = Quaternion.Inverse(_baseLocalRotations[0]) * solvedLocalRotation;
                localDelta.ToAngleAxis(out float angle, out Vector3 axis);
                if (angle > 180f)
                {
                    angle = 360f - angle;
                    axis = -axis;
                }

                Quaternion targetLocalRotation = _baseLocalRotations[0] * Quaternion.AngleAxis(
                    Mathf.Min(angle, Mathf.Max(0f, maximumAngle)),
                    axis);
                root.localPosition = _baseLocalPositions[0];
                root.localRotation = Quaternion.Slerp(
                    root.localRotation,
                    targetLocalRotation,
                    Mathf.Clamp01(weight));
            }

            public void StretchToTarget(
                Vector3 targetPoint,
                float minimumStretch,
                float maximumStretch,
                float weight)
            {
                if (!IsValid || _baseLocalPositions == null ||
                    _baseLocalPositions.Length != bones.Length || weight <= 0f)
                    return;

                float restLength = 0f;
                for (int i = 1; i < bones.Length; i++)
                {
                    Transform child = bones[i];
                    Transform parent = bones[i - 1];
                    if (child == null || parent == null)
                        continue;

                    restLength += parent.TransformVector(_baseLocalPositions[i]).magnitude;
                }

                if (restLength < 0.0001f)
                    return;

                float targetDistance = Vector3.Distance(RootPosition, targetPoint);
                float desiredStretch = Mathf.Clamp(
                    targetDistance / restLength,
                    Mathf.Max(0.1f, minimumStretch),
                    Mathf.Max(minimumStretch, maximumStretch));
                float appliedStretch = Mathf.Lerp(1f, desiredStretch, Mathf.Clamp01(weight));

                // Stretch every child segment equally so the skinned boom stays connected.
                for (int i = 1; i < bones.Length; i++)
                {
                    if (bones[i] != null)
                        bones[i].localPosition = _baseLocalPositions[i] * appliedStretch;
                }
            }

            public Quaternion AnchorRotation => Anchor != null ? Anchor.rotation : Quaternion.identity;

            public void FollowRootWorld(Vector3 targetRootPosition, Quaternion targetAnchorRotation, float weight)
            {
                Transform anchor = Anchor;
                if (anchor == null || weight <= 0f)
                    return;

                weight = Mathf.Clamp01(weight);
                anchor.rotation = Quaternion.Slerp(anchor.rotation, targetAnchorRotation, weight);
                anchor.position += (targetRootPosition - RootPosition) * weight;
            }

            public void StraightenToward(Vector3 targetPoint, float weight)
            {
                if (!IsValid || weight <= 0f)
                    return;

                weight = Mathf.Clamp01(weight);
                for (int i = 0; i < bones.Length - 1; i++)
                {
                    Transform bone = bones[i];
                    Transform child = bones[i + 1];
                    Vector3 segmentDirection = child.position - bone.position;
                    Vector3 targetDirection = targetPoint - bone.position;
                    if (segmentDirection.sqrMagnitude < 0.000001f || targetDirection.sqrMagnitude < 0.000001f)
                        continue;

                    Quaternion targetRotation =
                        Quaternion.FromToRotation(segmentDirection, targetDirection) * bone.rotation;
                    bone.rotation = Quaternion.Slerp(bone.rotation, targetRotation, weight);
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
                        Mathf.Clamp01(responseMultiplier * weight));
                }
            }
        }

        [Header("State")]
        [SerializeField] private WeaponHolderSO holderState;
        [SerializeField] private CrossHairComponent crossHair;
        [SerializeField] private MovmentComponent movementComponent;
        [SerializeField] private bool autoCreateMineableTargets = true;
        [SerializeField] private LSO_OreSO defaultOreDefinition;
        [SerializeField] private LSO_MineralSO defaultStoneMineral;
        [SerializeField] private LSO_MineralSO defaultScorchedMineral;

        [Header("Drill")]
        [SerializeField] private Transform drillRoot;
        [SerializeField] private ProceduralIkChain drillChain = new ProceduralIkChain();
        [SerializeField] private Transform[] drillEffectRoots = Array.Empty<Transform>();
        [SerializeField] private ParticleSystem[] drillEffects = Array.Empty<ParticleSystem>();
        [SerializeField, Range(1f, 180f)] private float drillRootAimMaximumAngle = 180f;
        [SerializeField, Range(0.5f, 1f)] private float drillMinimumStretch = 1f;
        [SerializeField, Range(1f, 20f)] private float drillMaximumStretch = 12f;
        [SerializeField, Min(0f)] private float drillSurfaceOffset;
        [SerializeField, Min(0.01f)] private float drillContactTolerance = 0.35f;
        [SerializeField, Range(0.01f, 1f)] private float drillEffectScale = 0.16f;
        [SerializeField, Min(0f)] private float drillEffectSurfaceOffset = 0.01f;
        [SerializeField, Range(0f, 1f)] private float drillEffectBrightness = 0.2f;

        [Header("Laser")]
        [SerializeField] private Transform laserRoot;
        [SerializeField] private Transform laserMuzzle;
        [SerializeField] private Transform laserEffectStart;
        [SerializeField] private Transform laserEndPosition;
        [SerializeField] private LineRenderer[] laserLines = Array.Empty<LineRenderer>();
        [SerializeField] private ParticleSystem[] laserEffects = Array.Empty<ParticleSystem>();
        [SerializeField, Min(0.01f)] private float beamTickInterval = 0.1f;
        [SerializeField, Min(0.001f)] private float beamWidth = 0.08f;

        [Header("Extractor Tongs")]
        [SerializeField] private Transform extractorRoot;
        [SerializeField] private ProceduralIkChain[] extractorChains = Array.Empty<ProceduralIkChain>();
        [SerializeField, Min(0)] private int extractorBoomChainIndex = 1;
        [SerializeField, Range(1f, 120f)] private float extractorRootAimMaximumAngle = 85f;
        [SerializeField, Range(0.25f, 1f)] private float extractorMinimumStretch = 0.65f;
        [SerializeField, Range(1f, 12f)] private float extractorMaximumStretch = 3.25f;
        [SerializeField, Min(0f)] private float extractorSurfaceOffset = 0.15f;

        [Tooltip("집게 머리(head)를 팔(leg) 축을 따라 이동시킨다. 팔 끝과 머리 사이 간격을 없애려면 음수로 당긴다.")]
        [SerializeField, Range(-5f, 5f)] private float extractorHeadAttachOffset;
        [Tooltip("집게가 표적과 상호작용(크로스헤어 초록) 가능한 최대 거리. 0보다 크면 툴 기본 사거리 대신 이 값을 쓴다.")]
        [SerializeField, Min(0f)] private float extractorInteractionRangeOverride;

        [Header("Feedback")]
        [SerializeField] private Color readyColor = new Color(0.2f, 1f, 0.72f, 1f);
        [SerializeField] private Color outOfRangeColor = new Color(1f, 0.2f, 0.18f, 1f);
        [SerializeField] private Color laserColor = new Color(0.25f, 0.9f, 1f, 1f);
        [SerializeField, Min(0f)] private float animationSharpness = 10f;

        [Header("World Resource Feedback")]
        [SerializeField] private GameObject asteroidExplosionVfxPrefab;
        [SerializeField, Min(0.1f)] private float explosionVfxLifetime = 2.5f;
        [SerializeField, Min(0.01f)] private float explosionVfxScale = 1f;
        [SerializeField, Min(1)] private int maximumLooseMineralChunks = 5;
        [SerializeField, Min(0.01f)] private float looseMineralScale = 0.18f;
        [SerializeField, Min(0f)] private float looseScatterMinimumDistance = 0.15f;
        [SerializeField, Min(0f)] private float looseScatterMaximumDistance = 0.65f;
        [SerializeField, Min(0.05f)] private float looseScatterDuration = 0.35f;
        [SerializeField, Min(0f)] private float extractorPullMinimumDistance = 0.05f;
        [SerializeField, Min(0f)] private float extractorPullMaximumDistance = 0.2f;
        [SerializeField, Min(0.05f)] private float extractorPullDuration = 0.3f;
        [SerializeField] private GameObject laserImpactVfxPrefab;
        [SerializeField] private GameObject[] oreReleaseVfxPrefabs = Array.Empty<GameObject>();
        [SerializeField, Min(0.1f)] private float oreReleaseVfxLifetime = 2.5f;
        [SerializeField, Min(0.01f)] private float oreReleaseVfxScale = 1f;

        [Header("Camera Impulse")]
        [SerializeField] private CinemachineImpulseSource laserImpulseSource;
        [SerializeField] private CinemachineImpulseSource drillImpulseSource;
        [SerializeField] private CinemachineImpulseSource extractorImpulseSource;
        [SerializeField, Min(0f)] private float laserImpulseStrength = 0.045f;
        [SerializeField, Min(0f)] private float drillImpulseStrength = 0.14f;
        [SerializeField, Min(0f)] private float extractorImpulseStrength = 0.16f;
        [SerializeField, Min(0.01f)] private float laserImpulseDuration = 0.08f;
        [SerializeField, Min(0.01f)] private float drillImpulseDuration = 0.18f;
        [SerializeField, Min(0.01f)] private float extractorImpulseDuration = 0.12f;
        [SerializeField, Range(1f, 2f)] private float depletedDrillImpulseMultiplier = 1.35f;
        [SerializeField] private int impulseChannel = 1;
        [SerializeField] private bool configureImpulseSources = true;

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
        private bool _targetCanMine;
        private bool _drillEffectsPlaying;
        private bool _laserEffectsPlaying;
        private bool _laserImpactEffectsPlaying;
        private bool _upgradeModulesSubscribed;
        private GameManager _upgradeManager;
        private MiningTechSelectionModule _techSelectionModule;
        private Transform _laserVfxRoot;
        private GameObject _laserImpactVfxInstance;
        private ParticleSystem[] _laserImpactEffects = Array.Empty<ParticleSystem>();
        private Vector3[] _extractorJawRootOffsetsAtBoomEnd = Array.Empty<Vector3>();
        private Quaternion[] _extractorJawAnchorRotationOffsets = Array.Empty<Quaternion>();
        private bool _extractorRigCaptured;
        private readonly Dictionary<ParticleSystem, Color> _drillEffectBaseColors =
            new Dictionary<ParticleSystem, Color>();
        private readonly Dictionary<Light, float> _drillEffectBaseLightIntensities =
            new Dictionary<Light, float>();

        public MiningTechDefinitionSO CurrentWeapon => _currentDefinition;
        public MiningTechType CurrentType => _currentType;
        public int CurrentLevel { get; private set; }
        public event Action<MiningTechDefinitionSO> WeaponChanged;

        private void Awake()
        {
            CacheReferences();
            EnsureWeaponEffectInstances();
            CacheReferences();
            EnsureImpulseSources();
            CaptureBasePose();
            EnsureBeam();
            SetDrillEffectsPlaying(false, true);

            MiningTechType initialType = holderState != null && holderState.CurrentWeapon != null
                ? holderState.CurrentWeapon.TechType
                : MiningTechType.Drill;
            SetRootActive(drillRoot, initialType == MiningTechType.Drill);
            SetRootActive(laserRoot, initialType == MiningTechType.Laser);
            SetRootActive(extractorRoot, initialType == MiningTechType.Extractor);
        }

        private void EnsureWeaponEffectInstances()
        {
            if (holderState == null)
                return;

            Transform drillEffectParent = drillChain.EndTransform != null
                ? drillChain.EndTransform
                : drillRoot;
            GameObject[] drillPrefabs = holderState.DrillEffectPrefabs;
            if (drillEffectParent != null && drillPrefabs != null)
            {
                List<Transform> effectRoots = new List<Transform>();
                foreach (GameObject prefab in drillPrefabs)
                {
                    if (prefab == null)
                        continue;

                    Transform effectRoot = FindDescendant(drillRoot, prefab.name);
                    if (effectRoot == null)
                    {
                        GameObject instance = Instantiate(prefab, drillEffectParent, false);
                        instance.name = prefab.name;
                        effectRoot = instance.transform;
                    }

                    effectRoot.SetParent(drillEffectParent, false);
                    ResetLocalTransform(effectRoot, drillEffectScale);
                    effectRoots.Add(effectRoot);
                }

                drillEffectRoots = effectRoots.ToArray();
                drillEffects = CollectParticleSystems(drillEffectRoots, drillRoot);
                ApplyDrillEffectBrightness();
            }

            GameObject laserPrefab = holderState.LaserEffectPrefab;
            if (laserRoot != null && laserPrefab != null && FindDescendant(laserRoot, laserPrefab.name) == null)
            {
                GameObject instance = Instantiate(laserPrefab, laserRoot, false);
                instance.name = laserPrefab.name;
                ResetLocalTransform(instance.transform);
            }
        }

        private static void ResetLocalTransform(Transform target, float uniformScale = 1f)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one * Mathf.Max(0.001f, uniformScale);
        }

        private void Start()
        {
            GameManager manager = GameManager.Instance;
            SubscribeTechSelection(manager.TechSelection);
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

            ApplyTechSelection(
                manager.TechSelection != null
                    ? manager.TechSelection.CurrentType
                    : (MiningTechType)Mathf.Clamp(saveData.selectedMiningTool, 0, 2));
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                if (_techSelectionModule != null)
                    _techSelectionModule.SelectNext();
                else
                    SelectTech((MiningTechType)(((int)_currentType + 1) % 3));
            }

            _fireHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            _actionTimer -= Time.deltaTime;
            RefreshStatsAndTargetState();
            UpdateFeedback();
            UpdateWeaponAction();
        }

        private void LateUpdate()
        {
            AnimateDrill();
            UpdateDrillEffects();
            AnimateExtractor();
            UpdateBeamPositions();
        }

        private void OnDisable()
        {
            crossHair?.ClearStatusColor();
            crossHair?.ClearTargetingDistance();
            movementComponent?.SetExternalSpeedMultiplier(1f);
            SetBeamVisible(false);
            SetDrillEffectsPlaying(false);
            if (drillRoot != null)
                drillRoot.localPosition = _drillBaseLocalPosition;
            if (extractorRoot != null)
                extractorRoot.localPosition = _extractorBaseLocalPosition;
        }

        private void OnDestroy()
        {
            UnsubscribeTechSelection();
            UnsubscribeUpgradeModules();
            if (_laserImpactVfxInstance != null)
                Destroy(_laserImpactVfxInstance);
        }

        public void SelectTech(MiningTechType type)
        {
            MiningTechSelectionModule selectionModule = GameManager.Instance.TechSelection;
            if (selectionModule == null)
            {
                ApplyTechSelection(type);
                return;
            }

            SubscribeTechSelection(selectionModule);
            bool changed = selectionModule.Select(type);
            if (!changed && (_currentDefinition == null || _currentType != type))
                ApplyTechSelection(type);
        }

        private void ApplyTechSelection(MiningTechType type)
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
                crossHair?.SetTargetingDistance(_currentStats.TargetingRange);
            }

            SetRootActive(drillRoot, type == MiningTechType.Drill);
            SetRootActive(laserRoot, type == MiningTechType.Laser);
            SetRootActive(extractorRoot, type == MiningTechType.Extractor);
            SetBeamVisible(false);
            SetDrillEffectsPlaying(false);
            _actionTimer = 0f;
            ApplyMovementPenalty();
            WeaponChanged?.Invoke(_currentDefinition);
        }

        private void SubscribeTechSelection(MiningTechSelectionModule selectionModule)
        {
            if (_techSelectionModule == selectionModule)
                return;

            UnsubscribeTechSelection();
            _techSelectionModule = selectionModule;
            if (_techSelectionModule != null)
                _techSelectionModule.SelectionChanged += ApplyTechSelection;
        }

        private void UnsubscribeTechSelection()
        {
            if (_techSelectionModule != null)
                _techSelectionModule.SelectionChanged -= ApplyTechSelection;
            _techSelectionModule = null;
        }

        public bool UpgradeCurrent()
        {
            MiningTechUpgradeModule upgradeModule = GetUpgradeModule(_currentType);
            return upgradeModule != null && upgradeModule.TryUpgrade();
        }

        public void HandleLaserImpact(Collider targetCollider, MiningTechStats stats)
        {
            MiningResult result = ApplyMining(
                targetCollider,
                MiningTechType.Laser,
                stats,
                1f,
                out MineableAsteroid target,
                out bool depleted);
            if (result.Failure != MiningFailureReason.None)
                return;

            GenerateToolImpulse(laserImpulseSource, laserImpulseStrength, MiningTechType.Laser);
            if (depleted)
                BreakStone(target);
        }

        private void RefreshStatsAndTargetState()
        {
            if (_currentDefinition != null)
            {
                _currentStats = _currentDefinition.GetStats(CurrentLevel);
                crossHair?.SetTargetingDistance(_currentStats.TargetingRange);
            }

            _targetCanMine = false;
            if (crossHair == null || !crossHair.HasTarget || crossHair.TargetCollider == null)
            {
                _targetInRange = false;
                return;
            }

            // The extractor can use an inspector override for the interaction range (the distance
            // at which the crosshair turns green). Set it to 0 to fall back to the tool's stat range.
            float interactionRange = _currentType == MiningTechType.Extractor &&
                                     extractorInteractionRangeOverride > 0f
                ? extractorInteractionRangeOverride
                : _currentStats.Range;

            // Never let the extractor turn green past the arm's physical reach, otherwise the
            // boom stops short while the head snaps to a target it can't actually touch. Tie the
            // interaction range to how far the boom can stretch so the tong always stays connected.
            if (_currentType == MiningTechType.Extractor && TryGetExtractorBoom(out ProceduralIkChain boomReach))
            {
                float armReach = boomReach.RestLength * extractorMaximumStretch;
                if (armReach > 0.01f)
                    interactionRange = Mathf.Min(interactionRange, armReach);
            }

            _targetInRange = Vector3.Distance(transform.position, crossHair.TargetSurfacePoint) <= interactionRange;
            if (!_targetInRange)
                return;

            MineableAsteroid target = ResolveMineableTarget(crossHair.TargetCollider);
            _targetCanMine = target != null &&
                             target.ValidateMining(_currentType, _currentStats) == MiningFailureReason.None;
        }

        private void UpdateFeedback()
        {
            if (crossHair == null)
                return;

            // No target -> normal crosshair. Locked target that the current tool can mine -> green
            // (readyColor). Locked but not interactable (out of range / wrong resource) -> red.
            if (!crossHair.HasTarget)
            {
                crossHair.ClearStatusColor();
                return;
            }

            crossHair.SetStatusColor(_targetCanMine ? readyColor : outOfRangeColor);
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
            bool canUse = _fireHeld && _targetCanMine && crossHair != null && crossHair.TargetCollider != null;
            if (type == MiningTechType.Drill && canUse && !IsDrillTipTouchingTarget())
                return;
            if (!canUse || _actionTimer > 0f)
                return;

            MiningResult result = ApplyMining(
                crossHair.TargetCollider,
                type,
                _currentStats,
                1f,
                out MineableAsteroid target,
                out bool depleted);
            if (result.Failure == MiningFailureReason.None)
            {
                if (type == MiningTechType.Drill)
                {
                    GenerateMiningImpulse(type, depleted);
                    if (depleted)
                        BreakStone(target);
                }
                else if (result.ProducedItems && depleted)
                {
                    Vector3 pullDirection = target != null
                        ? transform.position - target.WorldCenter
                        : -crossHair.CorrectedAimDirection;
                    if (target != null && target.ExtractLooseMineral(
                            pullDirection,
                            extractorPullMinimumDistance,
                            extractorPullMaximumDistance,
                            extractorPullDuration))
                    {
                        SpawnOreReleaseVfx(target.WorldCenter);
                        GenerateMiningImpulse(type, true);
                    }
                }
            }
            _actionTimer = _currentStats.ActionInterval;
        }

        private void UpdateLaser()
        {
            bool beamActive = _fireHeld && crossHair != null;
            if (beamActive)
                UpdateBeamPositions();
            SetBeamVisible(beamActive);

            if (!beamActive || !_targetCanMine || !crossHair.HasTarget ||
                crossHair.TargetCollider == null || _actionTimer > 0f)
                return;

            float tickInterval = _currentStats.BeamTickInterval > 0f
                ? _currentStats.BeamTickInterval
                : beamTickInterval;
            MiningTechStats tickStats = _currentStats;
            tickStats.DamagePerAction = tickStats.BeamDamagePerTick > 0f
                ? tickStats.BeamDamagePerTick
                : tickStats.DamagePerAction * tickStats.ActionsPerSecond * tickInterval;

            MiningResult result = ApplyMining(
                crossHair.TargetCollider,
                MiningTechType.Laser,
                tickStats,
                1f,
                out MineableAsteroid target,
                out bool depleted);
            if (result.Failure == MiningFailureReason.None)
            {
                GenerateToolImpulse(laserImpulseSource, laserImpulseStrength, MiningTechType.Laser);
                if (depleted)
                    BreakStone(target);
            }
            _actionTimer = tickInterval;
        }

        private void AnimateDrill()
        {
            if (drillRoot == null)
                return;

            bool hasCompatibleTarget = _currentType == MiningTechType.Drill && _targetCanMine &&
                                       crossHair != null && crossHair.HasTarget;
            float targetWeight = hasCompatibleTarget && _fireHeld ? 1f : 0f;
            _drillUseWeight = Damp(_drillUseWeight, targetWeight);
            drillRoot.localPosition = _drillBaseLocalPosition;
            drillRoot.localScale = _drillBaseLocalScale;

            float poseBlend = FrameBlend();
            drillChain.RestorePose(poseBlend);
            drillChain.RestoreAnchor(1f);
            if (_currentType == MiningTechType.Drill && crossHair != null &&
                _targetCanMine && _drillUseWeight > 0.001f && drillChain.IsValid)
            {
                Vector3 targetPoint = GetToolSurfacePoint(drillChain.RootPosition, drillSurfaceOffset);
                float reachWeight = Mathf.Clamp01(_drillUseWeight * 1.35f);
                drillChain.AimRootAt(targetPoint, reachWeight, drillRootAimMaximumAngle);
                drillChain.StraightenToward(targetPoint, reachWeight);
                drillChain.StretchToTarget(
                    targetPoint,
                    drillMinimumStretch,
                    drillMaximumStretch,
                    reachWeight);
                drillChain.StraightenToward(targetPoint, reachWeight);
                drillChain.Solve(targetPoint, reachWeight);
            }
        }

        private bool IsDrillTipTouchingTarget()
        {
            if (!drillChain.IsValid || crossHair == null || crossHair.TargetCollider == null)
                return false;

            Vector3 tipPosition = drillChain.EndPosition;
            Vector3 closestSurfacePoint = crossHair.TargetCollider.ClosestPoint(tipPosition);
            float scaleAwareTolerance = drillContactTolerance * Mathf.Max(
                1f,
                drillRoot != null ? drillRoot.lossyScale.magnitude : 1f);
            return Vector3.SqrMagnitude(tipPosition - closestSurfacePoint) <=
                   scaleAwareTolerance * scaleAwareTolerance;
        }

        private void UpdateDrillEffects()
        {
            ApplyDrillEffectBrightness();
            Transform drillTip = drillChain.EndTransform;
            if (drillTip != null && drillEffectRoots != null)
            {
                bool hasImpactPoint = _currentType == MiningTechType.Drill && _targetCanMine &&
                                      crossHair != null && crossHair.HasTarget;
                Vector3 effectPosition = drillTip.position;
                if (hasImpactPoint)
                {
                    effectPosition = crossHair.TargetSurfacePoint;
                    Vector3 towardTool = drillChain.RootPosition - effectPosition;
                    if (towardTool.sqrMagnitude > 0.0001f)
                        effectPosition += towardTool.normalized * drillEffectSurfaceOffset;
                }

                for (int i = 0; i < drillEffectRoots.Length; i++)
                {
                    Transform effectRoot = drillEffectRoots[i];
                    if (effectRoot == null)
                        continue;

                    effectRoot.SetPositionAndRotation(effectPosition, drillTip.rotation);
                }
            }

            bool shouldPlay = _currentType == MiningTechType.Drill && _fireHeld && _targetCanMine;
            SetDrillEffectsPlaying(shouldPlay);
        }

        private void ApplyDrillEffectBrightness()
        {
            float brightness = Mathf.Clamp01(drillEffectBrightness);
            if (drillEffects != null)
            {
                foreach (ParticleSystem effect in drillEffects)
                {
                    if (effect == null)
                        continue;

                    ParticleSystem.MainModule main = effect.main;
                    if (!_drillEffectBaseColors.TryGetValue(effect, out Color baseColor))
                    {
                        baseColor = main.startColor.color;
                        _drillEffectBaseColors.Add(effect, baseColor);
                    }

                    main.startColor = new Color(
                        baseColor.r * brightness,
                        baseColor.g * brightness,
                        baseColor.b * brightness,
                        baseColor.a);

                    ParticleSystem.LightsModule lightsModule = effect.lights;
                    if (lightsModule.enabled)
                        lightsModule.intensityMultiplier = brightness;
                }
            }

            if (drillEffectRoots == null)
                return;

            foreach (Transform effectRoot in drillEffectRoots)
            {
                if (effectRoot == null)
                    continue;

                foreach (Light effectLight in effectRoot.GetComponentsInChildren<Light>(true))
                {
                    if (!_drillEffectBaseLightIntensities.TryGetValue(effectLight, out float baseIntensity))
                    {
                        baseIntensity = effectLight.intensity;
                        _drillEffectBaseLightIntensities.Add(effectLight, baseIntensity);
                    }

                    effectLight.intensity = baseIntensity * brightness;
                }
            }
        }

        private void AnimateExtractor()
        {
            if (extractorRoot == null)
                return;

            bool hasCompatibleTarget = _currentType == MiningTechType.Extractor && _targetCanMine &&
                                       crossHair != null && crossHair.HasTarget;
            // Reach out to the target only while firing (left mouse button held), not merely when the
            // target is locked (green). The jaws themselves close while firing as well.
            float targetWeight = hasCompatibleTarget && _fireHeld ? 1f : 0f;
            _extractorUseWeight = Damp(_extractorUseWeight, targetWeight);
            extractorRoot.localPosition = _extractorBaseLocalPosition;

            float poseBlend = FrameBlend();
            for (int i = 0; i < extractorChains.Length; i++)
            {
                ProceduralIkChain chain = extractorChains[i];
                chain?.RestorePose(poseBlend);
                chain?.RestoreAnchor(i == extractorBoomChainIndex ? 1f : poseBlend);
            }

            if (_currentType != MiningTechType.Extractor || crossHair == null ||
                _extractorUseWeight <= 0.001f || !TryGetExtractorBoom(out ProceduralIkChain boom))
                return;

            if (!_extractorRigCaptured)
                CaptureExtractorRig();
            if (!_extractorRigCaptured)
                return;

            Vector3 targetPoint = GetExtractorTargetPoint(boom.RootPosition);
            targetPoint = boom.GetBiasedTarget(
                targetPoint,
                extractorRoot.up,
                extractorRoot.right);
            float reachWeight = Mathf.Clamp01(_extractorUseWeight * 1.35f);
            boom.AimRootAt(targetPoint, reachWeight, extractorRootAimMaximumAngle);
            boom.StretchToTarget(
                targetPoint,
                extractorMinimumStretch,
                extractorMaximumStretch,
                reachWeight);
            boom.Solve(targetPoint, 0.85f * reachWeight);

            for (int i = 0; i < extractorChains.Length; i++)
            {
                if (i == extractorBoomChainIndex)
                    continue;

                ProceduralIkChain jaw = extractorChains[i];
                if (jaw == null || !jaw.IsValid)
                    continue;

                // Keep the jaw (head) locked onto the boom (leg) tip. We preserve the sideways
                // spread of each jaw half but drop the along-the-arm component of the rest offset,
                // otherwise the head drifts ahead of the arm tip the farther the boom stretches.
                Vector3 restWorldOffset = boom.EndTransform.TransformPoint(
                    _extractorJawRootOffsetsAtBoomEnd[i]) - boom.EndTransform.position;
                Vector3 boomAxis = boom.EndPosition - boom.RootPosition;
                if (boomAxis.sqrMagnitude > 0.000001f)
                {
                    boomAxis.Normalize();
                    restWorldOffset -= Vector3.Dot(restWorldOffset, boomAxis) * boomAxis;
                    // Inspector-tunable slide along the arm to close the leg/head gap.
                    restWorldOffset += boomAxis * extractorHeadAttachOffset;
                }

                Vector3 targetJawRoot = boom.EndTransform.position + restWorldOffset;
                Quaternion targetJawRotation = boom.EndTransform.rotation *
                                               _extractorJawAnchorRotationOffsets[i];
                jaw.FollowRootWorld(targetJawRoot, targetJawRotation, 1f);
                float closeWeight = _fireHeld && _targetCanMine ? _extractorUseWeight : 0f;
                jaw.ApplyForcedCurl(closeWeight);
            }
        }

        private bool TryGetExtractorBoom(out ProceduralIkChain boom)
        {
            boom = null;
            if (extractorChains == null || extractorChains.Length == 0)
                return false;

            int index = Mathf.Clamp(extractorBoomChainIndex, 0, extractorChains.Length - 1);
            boom = extractorChains[index];
            return boom != null && boom.IsValid;
        }

        private void CaptureExtractorRig()
        {
            _extractorRigCaptured = false;
            if (extractorRoot == null || !TryGetExtractorBoom(out ProceduralIkChain boom))
                return;

            Transform boomEnd = boom.EndTransform;
            if (boomEnd == null)
                return;

            _extractorJawRootOffsetsAtBoomEnd = new Vector3[extractorChains.Length];
            _extractorJawAnchorRotationOffsets = new Quaternion[extractorChains.Length];

            for (int i = 0; i < extractorChains.Length; i++)
            {
                ProceduralIkChain jaw = extractorChains[i];
                if (i == extractorBoomChainIndex || jaw == null || !jaw.IsValid)
                    continue;

                _extractorJawRootOffsetsAtBoomEnd[i] = boomEnd.InverseTransformPoint(jaw.RootPosition);
                _extractorJawAnchorRotationOffsets[i] =
                    Quaternion.Inverse(boomEnd.rotation) * jaw.AnchorRotation;
            }

            _extractorRigCaptured = true;
        }

        private Vector3 GetExtractorTargetPoint(Vector3 boomRootPosition)
        {
            return GetToolSurfacePoint(boomRootPosition, extractorSurfaceOffset);
        }

        private Vector3 GetToolSurfacePoint(Vector3 toolRootPosition, float surfaceOffset)
        {
            Vector3 targetPoint = crossHair.TargetSurfacePoint;

            Vector3 surfaceNormal = toolRootPosition - targetPoint;
            if (surfaceNormal.sqrMagnitude > 0.0001f)
                targetPoint += surfaceNormal.normalized * surfaceOffset;
            return targetPoint;
        }

        private MiningResult ApplyMining(
            Collider targetCollider,
            MiningTechType type,
            MiningTechStats stats,
            float damageMultiplier,
            out MineableAsteroid target,
            out bool depleted)
        {
            depleted = false;
            target = ResolveMineableTarget(targetCollider);
            if (target == null)
                return default;

            GameObject explosionPrefab = asteroidExplosionVfxPrefab != null
                ? asteroidExplosionVfxPrefab
                : holderState != null ? holderState.ExplosionEffectPrefab : null;
            target.ConfigureBreakFeedback(
                explosionPrefab,
                explosionVfxLifetime,
                explosionVfxScale,
                maximumLooseMineralChunks,
                looseMineralScale,
                looseScatterMinimumDistance,
                looseScatterMaximumDistance,
                looseScatterDuration);

            bool wasDepleted = target.IsDepleted;
            MiningResult result = target.ApplyMining(type, stats, damageMultiplier);
            depleted = !wasDepleted && target.IsDepleted;
            return result;
        }

        private void BreakStone(MineableAsteroid target)
        {
            if (target == null || target.ResourceType != MiningResourceType.Stone)
                return;

            Vector3 impactDirection = crossHair != null
                ? crossHair.CorrectedAimDirection
                : transform.forward;
            GameObject explosionPrefab = asteroidExplosionVfxPrefab != null
                ? asteroidExplosionVfxPrefab
                : holderState != null ? holderState.ExplosionEffectPrefab : null;
            SpawnOreReleaseVfx(target.WorldCenter);
            target.BreakIntoLooseMinerals(
                explosionPrefab,
                explosionVfxLifetime,
                explosionVfxScale,
                maximumLooseMineralChunks,
                looseMineralScale,
                looseScatterMinimumDistance,
                looseScatterMaximumDistance,
                looseScatterDuration,
                impactDirection);
        }

        private void EnsureImpulseSources()
        {
            laserImpulseSource = EnsureImpulseSource(
                laserImpulseSource,
                CinemachineImpulseDefinition.ImpulseShapes.Recoil,
                laserImpulseDuration);

            if (drillImpulseSource == laserImpulseSource)
                drillImpulseSource = null;
            drillImpulseSource = EnsureImpulseSource(
                drillImpulseSource,
                CinemachineImpulseDefinition.ImpulseShapes.Explosion,
                drillImpulseDuration);

            if (extractorImpulseSource == laserImpulseSource || extractorImpulseSource == drillImpulseSource)
                extractorImpulseSource = null;
            extractorImpulseSource = EnsureImpulseSource(
                extractorImpulseSource,
                CinemachineImpulseDefinition.ImpulseShapes.Bump,
                extractorImpulseDuration);
        }

        private CinemachineImpulseSource EnsureImpulseSource(
            CinemachineImpulseSource source,
            CinemachineImpulseDefinition.ImpulseShapes shape,
            float duration)
        {
            bool created = source == null;
            if (created)
                source = gameObject.AddComponent<CinemachineImpulseSource>();

            if (created || configureImpulseSources)
            {
                source.ImpulseDefinition ??= new CinemachineImpulseDefinition();
                source.ImpulseDefinition.ImpulseChannel = impulseChannel;
                source.ImpulseDefinition.ImpulseShape = shape;
                source.ImpulseDefinition.ImpulseDuration = Mathf.Max(0.01f, duration);
                source.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            }

            return source;
        }

        private void GenerateMiningImpulse(MiningTechType type, bool depleted)
        {
            switch (type)
            {
                case MiningTechType.Drill:
                    float strength = drillImpulseStrength *
                                     (depleted ? depletedDrillImpulseMultiplier : 1f);
                    GenerateToolImpulse(drillImpulseSource, strength, type);
                    break;
                case MiningTechType.Extractor:
                    GenerateToolImpulse(extractorImpulseSource, extractorImpulseStrength, type);
                    break;
            }
        }

        private void GenerateToolImpulse(
            CinemachineImpulseSource source,
            float strength,
            MiningTechType type)
        {
            if (source == null || strength <= 0f)
                return;

            Vector3 aimDirection = crossHair != null
                ? crossHair.CorrectedAimDirection
                : transform.forward;
            if (aimDirection.sqrMagnitude < 0.0001f)
                aimDirection = transform.forward;

            Vector3 recoilDirection = -aimDirection.normalized;
            if (type == MiningTechType.Drill)
                recoilDirection = (recoilDirection + transform.up * 0.18f).normalized;
            else if (type == MiningTechType.Extractor)
                recoilDirection = (recoilDirection + transform.right * 0.22f).normalized;

            source.GenerateImpulseWithVelocity(recoilDirection * strength);
        }

        private MineableAsteroid ResolveMineableTarget(Collider targetCollider)
        {
            if (targetCollider == null)
                return null;

            Transform resourceRoot = FindResourceRoot(targetCollider.transform);
            MineableAsteroid target = resourceRoot.GetComponent<MineableAsteroid>() ??
                                        targetCollider.GetComponentInParent<MineableAsteroid>();
            LSO_Ore ore = resourceRoot.GetComponent<LSO_Ore>() ??
                          resourceRoot.GetComponentInChildren<LSO_Ore>(true) ??
                          targetCollider.GetComponentInParent<LSO_Ore>();

            // A target only counts as a mineable resource when it is tagged Stone/Ore, or it
            // already carries ore data (LSO_Ore / MineableAsteroid). Plain scenery is never
            // mineable, so the drill/laser can't break random objects the player clicks on.
            bool isResource = ore != null || target != null || IsResourceTagged(resourceRoot);
            if (!isResource)
                return null;

            if (target == null && autoCreateMineableTargets)
            {
                if (ore == null && defaultOreDefinition != null)
                {
                    ore = resourceRoot.gameObject.AddComponent<LSO_Ore>();
                    ore.oreSO = defaultOreDefinition;
                }

                if (ore != null)
                    target = resourceRoot.gameObject.AddComponent<MineableAsteroid>();
            }

            target?.ConfigureOre(ore, defaultStoneMineral, defaultScorchedMineral);
            return target;
        }

        private static bool IsResourceTagged(Transform root)
        {
            for (Transform current = root; current != null; current = current.parent)
            {
                if (current.CompareTag("Stone") || current.CompareTag("Ore"))
                    return true;
            }

            return false;
        }

        private static Transform FindResourceRoot(Transform start)
        {
            Transform meteorRoot = null;
            for (Transform current = start; current != null; current = current.parent)
            {
                string objectTag = current.tag;
                bool isMeteorContainer = current.name.Equals("Meteors", StringComparison.OrdinalIgnoreCase);
                if (!isMeteorContainer && (objectTag == "Stone" || objectTag == "Ore"))
                    return current;

                // Keep the nearest individual Meteor.  The scene container is named "Meteors".
                if (meteorRoot == null &&
                    current.name.StartsWith("Meteor", StringComparison.OrdinalIgnoreCase) &&
                    !isMeteorContainer)
                    meteorRoot = current;
            }

            return meteorRoot != null ? meteorRoot : start;
        }

        private void CacheReferences()
        {
            if (crossHair == null)
                crossHair = GetComponentInParent<CrossHairComponent>();
            if (movementComponent == null)
                movementComponent = GetComponentInParent<MovmentComponent>();

            Transform discoveredDrill = FindDescendant(transform, "SpaceShipDrill");
            if (discoveredDrill != null &&
                (drillRoot == null || !drillRoot.name.Equals("SpaceShipDrill", StringComparison.OrdinalIgnoreCase)))
                drillRoot = discoveredDrill;
            if (!drillChain.TryAutoBind(drillRoot))
                Debug.LogWarning("WeaponHolder could not find the SpaceShipDrill bone chain.", this);

            laserMuzzle = ResolveLaserStart();
            _laserVfxRoot = FindDescendant(laserRoot, "LaserVFX");
            laserEffectStart = FindDescendant(_laserVfxRoot, "Laser");
            laserEndPosition = FindDescendant(_laserVfxRoot, "LaserEndPos");
            laserLines = _laserVfxRoot != null
                ? _laserVfxRoot.GetComponentsInChildren<LineRenderer>(true)
                : Array.Empty<LineRenderer>();
            laserEffects = _laserVfxRoot != null
                ? _laserVfxRoot.GetComponentsInChildren<ParticleSystem>(true)
                : Array.Empty<ParticleSystem>();
            if (drillEffectRoots == null || drillEffectRoots.Length == 0)
                drillEffectRoots = FindNamedTransforms(drillRoot, "DrillVFX", 4);
            if (drillEffects == null || drillEffects.Length == 0)
                drillEffects = CollectParticleSystems(drillEffectRoots, drillRoot);
        }

        private Transform ResolveLaserStart()
        {
            if (laserRoot == null)
                return null;

            Transform configuredStart = FindDescendant(laserRoot, "Start");
            if (configuredStart != null)
                return configuredStart;

            Transform barrel = FindDescendant(laserRoot, "Cylinder");
            Renderer barrelRenderer = barrel != null
                ? barrel.GetComponentInChildren<Renderer>(true)
                : null;
            Vector3 origin = laserRoot.position;
            Vector3 direction = barrelRenderer != null
                ? barrelRenderer.bounds.center - origin
                : barrel != null ? barrel.position - origin : laserRoot.forward;
            if (direction.sqrMagnitude < 0.000001f)
                direction = laserRoot.forward;
            direction.Normalize();

            Vector3 position = barrelRenderer != null
                ? barrelRenderer.bounds.center + direction * ProjectBoundsExtent(barrelRenderer.bounds.extents, direction)
                : (barrel != null ? barrel.position : origin) + direction * 0.05f;

            GameObject startObject = new GameObject("Start");
            Transform start = startObject.transform;
            start.SetParent(laserRoot, true);
            start.SetPositionAndRotation(position, CreateBeamRotation(direction, laserRoot.up));
            return start;
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
            CaptureExtractorRig();
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
            Transform fallbackBeam = FindDescendant(transform, "Continuous Mining Beam");
            if (fallbackBeam != null)
                Destroy(fallbackBeam.gameObject);

            if (laserLines == null)
                laserLines = Array.Empty<LineRenderer>();
            foreach (LineRenderer line in laserLines)
            {
                if (line == null)
                    continue;

                line.positionCount = 2;
                line.useWorldSpace = true;
                line.numCapVertices = Mathf.Max(2, line.numCapVertices);
            }

            EnsureLaserImpactVfx();
            SetBeamVisible(false, true);
        }

        private void EnsureLaserImpactVfx()
        {
            if (_laserImpactVfxInstance != null || laserImpactVfxPrefab == null)
                return;

            Transform parent = laserRoot != null ? laserRoot : transform;
            _laserImpactVfxInstance = Instantiate(laserImpactVfxPrefab, parent);
            _laserImpactVfxInstance.name = "Laser Impact DrillVFX";
            _laserImpactEffects = _laserImpactVfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            _laserImpactVfxInstance.SetActive(false);
        }

        private void UpdateBeamPositions()
        {
            if (laserLines == null || laserLines.Length == 0 || crossHair == null)
                return;

            Transform originTransform = laserMuzzle != null ? laserMuzzle : laserRoot;
            if (originTransform == null)
                return;

            Vector3 origin = originTransform.position;
            Vector3 direction = crossHair.CorrectedAimDirection;
            float beamLength = Mathf.Max(0.01f, _currentStats.Range);
            if (crossHair.HasTarget)
            {
                Vector3 toSurface = crossHair.TargetSurfacePoint - origin;
                if (toSurface.sqrMagnitude > 0.000001f)
                {
                    direction = toSurface.normalized;
                    beamLength = Mathf.Min(beamLength, toSurface.magnitude);
                }
            }

            if (direction.sqrMagnitude < 0.000001f)
                direction = originTransform.forward;
            Vector3 end = origin + direction.normalized * beamLength;
            Vector3 beamDirection = (end - origin).normalized;
            Vector3 up = laserRoot != null ? laserRoot.up : transform.up;
            Quaternion startRotation = CreateBeamRotation(beamDirection, up);

            if (_laserVfxRoot != null)
                _laserVfxRoot.SetPositionAndRotation(origin, startRotation);
            if (laserEffectStart != null)
                laserEffectStart.SetPositionAndRotation(origin, startRotation);

            foreach (LineRenderer line in laserLines)
            {
                if (line == null)
                    continue;

                line.SetPosition(0, origin);
                line.SetPosition(1, end);
            }

            if (laserEndPosition != null)
                laserEndPosition.SetPositionAndRotation(
                    end,
                    CreateBeamRotation(-beamDirection, up));

            bool showImpact = _currentType == MiningTechType.Laser &&
                              _fireHeld && crossHair.HasTarget;
            UpdateLaserImpactVfx(
                showImpact,
                end,
                CreateBeamRotation(-beamDirection, up));
        }

        private void UpdateLaserImpactVfx(bool visible, Vector3 position, Quaternion rotation)
        {
            EnsureLaserImpactVfx();
            if (_laserImpactVfxInstance == null)
                return;

            if (!visible)
            {
                if (_laserImpactEffectsPlaying)
                    SetParticleSystemsPlaying(_laserImpactEffects, false);
                _laserImpactEffectsPlaying = false;
                _laserImpactVfxInstance.SetActive(false);
                return;
            }

            _laserImpactVfxInstance.transform.SetPositionAndRotation(position, rotation);
            if (!_laserImpactVfxInstance.activeSelf)
                _laserImpactVfxInstance.SetActive(true);

            if (_laserImpactEffectsPlaying)
            {
                RestartCompletedEffects(_laserImpactEffects);
                return;
            }

            _laserImpactEffectsPlaying = true;
            SetParticleSystemsPlaying(_laserImpactEffects, true);
        }

        private static float ProjectBoundsExtent(Vector3 extents, Vector3 direction)
        {
            Vector3 absoluteDirection = new Vector3(
                Mathf.Abs(direction.x),
                Mathf.Abs(direction.y),
                Mathf.Abs(direction.z));
            return Vector3.Dot(extents, absoluteDirection);
        }

        private static Quaternion CreateBeamRotation(Vector3 direction, Vector3 preferredUp)
        {
            if (direction.sqrMagnitude < 0.000001f)
                return Quaternion.identity;

            direction.Normalize();
            if (preferredUp.sqrMagnitude < 0.000001f ||
                Mathf.Abs(Vector3.Dot(direction, preferredUp.normalized)) > 0.98f)
            {
                preferredUp = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < 0.98f
                    ? Vector3.up
                    : Vector3.right;
            }

            return Quaternion.LookRotation(direction, preferredUp);
        }

        private void SetBeamVisible(bool visible, bool force = false)
        {
            if (laserLines != null)
            {
                foreach (LineRenderer line in laserLines)
                    if (line != null)
                        line.enabled = visible;
            }

            if (!visible)
            {
                _laserEffectsPlaying = false;
                SetParticleSystemsPlaying(laserEffects, false);
                UpdateLaserImpactVfx(false, Vector3.zero, Quaternion.identity);
                return;
            }

            if (!force && _laserEffectsPlaying)
            {
                RestartCompletedEffects(laserEffects);
                return;
            }

            _laserEffectsPlaying = true;
            SetParticleSystemsPlaying(laserEffects, true);
        }

        private void SpawnOreReleaseVfx(Vector3 position)
        {
            if (oreReleaseVfxPrefabs == null)
                return;

            for (int i = 0; i < oreReleaseVfxPrefabs.Length; i++)
            {
                GameObject prefab = oreReleaseVfxPrefabs[i];
                if (prefab == null)
                    continue;

                GameObject effect = Instantiate(prefab, position, UnityEngine.Random.rotation);
                effect.transform.localScale *= oreReleaseVfxScale;
                ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
                for (int j = 0; j < particles.Length; j++)
                {
                    particles[j].gameObject.SetActive(true);
                    particles[j].Clear(true);
                    particles[j].Play(true);
                }

                Destroy(effect, oreReleaseVfxLifetime);
            }
        }

        private void SetDrillEffectsPlaying(bool visible, bool force = false)
        {
            if (!visible)
            {
                _drillEffectsPlaying = false;
                SetParticleSystemsPlaying(drillEffects, false);
                return;
            }

            if (!force && _drillEffectsPlaying)
            {
                RestartCompletedEffects(drillEffects);
                return;
            }

            _drillEffectsPlaying = true;
            SetParticleSystemsPlaying(drillEffects, true);
        }

        private static void SetParticleSystemsPlaying(ParticleSystem[] effects, bool playing)
        {
            if (effects == null)
                return;

            foreach (ParticleSystem effect in effects)
            {
                if (effect == null)
                    continue;

                if (playing)
                    effect.Play(false);
                else
                    effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void RestartCompletedEffects(ParticleSystem[] effects)
        {
            if (effects == null)
                return;

            foreach (ParticleSystem effect in effects)
                if (effect != null && !effect.IsAlive(false))
                    effect.Play(false);
        }

        private static Transform[] FindNamedTransforms(Transform root, string namePrefix, int expectedRoots)
        {
            if (root == null)
                return Array.Empty<Transform>();

            List<Transform> matches = new List<Transform>();
            for (int i = 1; i <= expectedRoots; i++)
            {
                Transform effectRoot = FindDescendant(root, $"{namePrefix}{i}");
                if (effectRoot != null)
                    matches.Add(effectRoot);
            }

            return matches.ToArray();
        }

        private static ParticleSystem[] CollectParticleSystems(Transform[] roots, Transform fallbackRoot)
        {
            List<ParticleSystem> effects = new List<ParticleSystem>();
            if (roots != null)
            {
                foreach (Transform root in roots)
                    if (root != null)
                        effects.AddRange(root.GetComponentsInChildren<ParticleSystem>(true));
            }

            return effects.Count > 0
                ? effects.ToArray()
                : fallbackRoot != null
                    ? fallbackRoot.GetComponentsInChildren<ParticleSystem>(true)
                    : Array.Empty<ParticleSystem>();
        }

        private static Transform FindDescendant(Transform root, string objectName)
        {
            if (root == null)
                return null;
            if (root.name == objectName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindDescendant(root.GetChild(i), objectName);
                if (match != null)
                    return match;
            }

            return null;
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
