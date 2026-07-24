using System;
using _Scripts.LSO;
using _Scripts.LSO.Data;
using UnityEngine;

namespace _Scripts.Suxghui.Mining
{
    public enum MiningFailureReason
    {
        None,
        Depleted,
        WrongResourceType,
        CoveredMineral,
        DeepMineral,
        MissingOreData,
        InventoryUnavailable,
        StorageFull
    }

    public enum MiningResourceType
    {
        Stone,
        LooseMineral
    }

    public readonly struct MiningResult
    {
        public MiningResult(
            LSO_MineralSO mineral,
            int mineralAmount,
            int stoneAmount,
            float purity,
            bool scorched,
            MiningFailureReason failure)
        {
            Mineral = mineral;
            MineralAmount = mineralAmount;
            StoneAmount = stoneAmount;
            Purity = purity;
            Scorched = scorched;
            Failure = failure;
        }

        public LSO_MineralSO Mineral { get; }
        public int MineralAmount { get; }
        public int StoneAmount { get; }
        public float Purity { get; }
        public bool Scorched { get; }
        public MiningFailureReason Failure { get; }
        public bool ProducedItems => MineralAmount > 0 || StoneAmount > 0;
    }

    public sealed class MineableAsteroid : MonoBehaviour
    {
        private const string StoneTag = "Stone";
        private const string LooseMineralTag = "One";
        private const string LegacyLooseMineralTag = "Ore";

        [Header("LSO Ore")]
        [SerializeField] private LSO_Ore oreSource;
        [SerializeField] private LSO_MineralSO scorchedMineralOverride;
        [SerializeField] private LSO_MineralSO stoneMineral;

        [Header("Deposit")]
        [SerializeField, Min(1)] private int mineralReserve = 4;
        [SerializeField, Min(0.01f)] private float durabilityPerDrop = 100f;
        [SerializeField, Range(0f, 1f)] private float basePurity = 0.55f;
        [SerializeField] private MiningResourceType untaggedResourceType = MiningResourceType.Stone;

        [Header("Extractor Rules")]
        [SerializeField] private bool stoneCovered;
        [SerializeField] private bool exposedMineral = true;
        [SerializeField] private bool deepMineral;

        [Header("Space Motion")]
        [SerializeField, Min(0f)] private float linearDamping = 1.25f;
        [SerializeField, Min(0f)] private float angularSpeed = 1.5f;

        private float _miningProgress;
        private int _pendingLooseMinerals;
        private float _pendingPurity;
        private int _pendingPuritySamples;
        private bool _pendingScorched;
        private bool _hasRuntimeResourceType;
        private MiningResourceType _runtimeResourceType;
        private bool _isBreaking;
        private bool _isBeingExtracted;
        private bool _motionActive;
        private Vector3 _motionOrigin;
        private float _maximumMotionDistance;
        private float _loosePurity = -1f;
        private bool _looseScorched;
        private Rigidbody _rigidbody;
        private LSO_PlayerInventory _inventory;
        private LSO_Weight _cargoWeight;
        private GameObject _breakExplosionPrefab;
        private float _breakExplosionLifetime = 2.5f;
        private float _breakExplosionScale = 1f;
        private int _breakMaximumChunks = 5;
        private float _breakLooseChunkScale = 0.18f;
        private float _breakMinimumScatterDistance = 0.05f;
        private float _breakMaximumScatterDistance = 0.2f;
        private float _breakScatterDuration = 0.45f;

        public int MineralReserve => mineralReserve;
        public int PendingLooseMinerals => _pendingLooseMinerals;
        public bool IsDepleted => mineralReserve <= 0;
        public bool IsLooseMineral => ResourceType == MiningResourceType.LooseMineral;
        public MiningResourceType ResourceType => ResolveResourceType();
        public LSO_Ore OreSource => oreSource;
        public Vector3 WorldCenter => GetWorldCenter();
        public event Action<MiningResult> Mined;

        private void Awake()
        {
            CacheReferences();
        }

        private void FixedUpdate()
        {
            if (!_motionActive || _rigidbody == null || _maximumMotionDistance <= 0f)
                return;

            Vector3 offset = _rigidbody.position - _motionOrigin;
            if (offset.sqrMagnitude <= _maximumMotionDistance * _maximumMotionDistance)
                return;

            _rigidbody.position = _motionOrigin + offset.normalized * _maximumMotionDistance;
            _rigidbody.linearVelocity = Vector3.zero;
            _motionActive = false;
        }

        public void ConfigureOre(
            LSO_Ore source,
            LSO_MineralSO stoneByproduct = null,
            LSO_MineralSO scorchedOverride = null)
        {
            if (source != null)
                oreSource = source;
            if (stoneByproduct != null)
                stoneMineral = stoneByproduct;
            if (scorchedOverride != null)
                scorchedMineralOverride = scorchedOverride;

            CacheReferences();
        }

        public MiningFailureReason ValidateMining(MiningTechType techType, MiningTechStats stats)
        {
            return GetFailureReason(techType, stats);
        }

        public void ConfigureBreakFeedback(
            GameObject explosionPrefab,
            float explosionLifetime,
            float explosionScale,
            int maximumLooseChunks,
            float looseChunkScale,
            float minimumScatterDistance,
            float maximumScatterDistance,
            float scatterDuration)
        {
            _breakExplosionPrefab = explosionPrefab;
            _breakExplosionLifetime = explosionLifetime;
            _breakExplosionScale = explosionScale;
            _breakMaximumChunks = maximumLooseChunks;
            _breakLooseChunkScale = looseChunkScale;
            _breakMinimumScatterDistance = minimumScatterDistance;
            _breakMaximumScatterDistance = maximumScatterDistance;
            _breakScatterDuration = scatterDuration;
        }

        public MiningResult ApplyMining(MiningTechType techType, MiningTechStats stats, float damageMultiplier = 1f)
        {
            MiningFailureReason failure = ValidateMining(techType, stats);
            if (failure != MiningFailureReason.None)
                return FailedResult(failure);

            _miningProgress += stats.DamagePerAction * Mathf.Max(0f, damageMultiplier);
            if (_miningProgress < durabilityPerDrop)
                return FailedResult(MiningFailureReason.None);

            int completedDrops = Mathf.FloorToInt(_miningProgress / durabilityPerDrop);
            int requestedAmount = ResourceType == MiningResourceType.LooseMineral
                ? mineralReserve
                : Mathf.Max(1, Mathf.RoundToInt(mineralReserve * stats.YieldMultiplier));

            if (techType == MiningTechType.Extractor)
            {
                if (UnityEngine.Random.value < stats.ExtraExtractionChance)
                    requestedAmount++;
                if (deepMineral && UnityEngine.Random.value < stats.DeepExtractionChance)
                    requestedAmount++;
                if (mineralReserve >= 2 && stats.GuaranteedExtractionCount >= 2)
                    requestedAmount = Mathf.Max(requestedAmount, stats.GuaranteedExtractionCount);
            }

            float purity = _loosePurity >= 0f
                ? Mathf.Clamp01(_loosePurity + stats.PurityBonus)
                : Mathf.Clamp01(basePurity + stats.PurityBonus);
            bool scorched = _looseScorched ||
                            (techType == MiningTechType.Laser && UnityEngine.Random.value < stats.ScorchChance);

            if (ResourceType == MiningResourceType.Stone)
            {
                int totalMinerals = Mathf.Max(1, requestedAmount);
                Vector3 chunkScale = Vector3.Scale(
                    Abs(transform.lossyScale),
                    Vector3.one * Mathf.Max(0.01f, _breakLooseChunkScale));
                oreSource.ConfigureBreakFeedback(
                    _breakExplosionPrefab,
                    WorldCenter,
                    _breakExplosionLifetime,
                    _breakExplosionScale,
                    totalMinerals,
                    _breakMaximumChunks,
                    chunkScale,
                    purity,
                    scorched,
                    _breakMinimumScatterDistance,
                    _breakMaximumScatterDistance,
                    _breakScatterDuration,
                    gameObject.layer);
            }

            LSO_MineralSO minedMineral = oreSource.Mine();
            if (minedMineral == null)
                return FailedResult(MiningFailureReason.MissingOreData);

            if (ResourceType == MiningResourceType.Stone)
                return ProcessStoneMining(minedMineral, requestedAmount, completedDrops, purity, scorched);

            return ProcessLooseMineralExtraction(
                minedMineral,
                requestedAmount,
                completedDrops,
                purity,
                scorched);
        }

        public void BreakIntoLooseMinerals(
            GameObject explosionPrefab,
            float explosionLifetime,
            float explosionScale,
            int maximumLooseChunks,
            float looseChunkScale,
            float minimumScatterDistance,
            float maximumScatterDistance,
            float scatterDuration,
            Vector3 impactDirection)
        {
            if (_isBreaking || ResourceType != MiningResourceType.Stone || !IsDepleted)
                return;

            _isBreaking = true;
            if (oreSource != null && !oreSource.BreakFeedbackPlayedLastMine)
            {
                float averagePurity = _pendingPuritySamples > 0
                    ? _pendingPurity / _pendingPuritySamples
                    : basePurity;
                oreSource.ConfigureBreakFeedback(
                    explosionPrefab,
                    WorldCenter,
                    explosionLifetime,
                    explosionScale,
                    Mathf.Max(1, _pendingLooseMinerals),
                    maximumLooseChunks,
                    Vector3.Scale(
                        Abs(transform.lossyScale),
                        Vector3.one * Mathf.Max(0.01f, looseChunkScale)),
                    averagePurity,
                    _pendingScorched,
                    minimumScatterDistance,
                    maximumScatterDistance,
                    scatterDuration,
                    gameObject.layer);
                oreSource.PlayConfiguredBreakFeedback();
            }

            Destroy(gameObject);
        }

        public bool ExtractLooseMineral(
            Vector3 pullDirection,
            float minimumDistance,
            float maximumDistance,
            float travelDuration)
        {
            if (_isBeingExtracted || !IsLooseMineral || !IsDepleted)
                return false;

            _isBeingExtracted = true;
            foreach (Collider targetCollider in GetComponentsInChildren<Collider>(true))
                targetCollider.enabled = false;

            LaunchInSpace(pullDirection, minimumDistance, maximumDistance, travelDuration);
            Destroy(gameObject, Mathf.Max(0.1f, travelDuration));
            return true;
        }

        private MiningResult ProcessStoneMining(
            LSO_MineralSO minedMineral,
            int requestedAmount,
            int completedDrops,
            float purity,
            bool scorched)
        {
            int mineralAmount = Mathf.Max(1, requestedAmount);
            mineralReserve = 0;
            _pendingLooseMinerals += mineralAmount;
            _pendingPurity += purity;
            _pendingPuritySamples++;
            _pendingScorched |= scorched;
            _miningProgress -= completedDrops * durabilityPerDrop;

            MiningResult result = new MiningResult(
                minedMineral,
                mineralAmount,
                0,
                purity,
                scorched,
                MiningFailureReason.None);
            Mined?.Invoke(result);
            return result;
        }

        private MiningResult ProcessLooseMineralExtraction(
            LSO_MineralSO minedMineral,
            int requestedAmount,
            int completedDrops,
            float purity,
            bool scorched)
        {
            if (!TryResolveInventory())
                return FailedResult(MiningFailureReason.InventoryUnavailable);

            int availableCapacity = _cargoWeight != null
                ? _cargoWeight.RemainingCapacity
                : int.MaxValue;
            int mineralAmount = Mathf.Min(mineralReserve, requestedAmount, availableCapacity);
            if (mineralAmount <= 0)
            {
                _miningProgress = Mathf.Min(_miningProgress, durabilityPerDrop);
                return FailedResult(MiningFailureReason.StorageFull);
            }

            LSO_MineralSO storedMineral = scorched && scorchedMineralOverride != null
                ? scorchedMineralOverride
                : minedMineral;
            _inventory.AddItem(storedMineral, mineralAmount);
            _cargoWeight?.AddWeight(mineralAmount);
            mineralReserve -= mineralAmount;
            _miningProgress -= completedDrops * durabilityPerDrop;

            MiningResult result = new MiningResult(
                storedMineral,
                mineralAmount,
                0,
                purity,
                scorched,
                MiningFailureReason.None);
            Mined?.Invoke(result);
            return result;
        }

        public void InitializeAsLooseMineral(
            LSO_OreSO oreDefinition,
            int representedAmount,
            float purity,
            bool scorched)
        {
            _hasRuntimeResourceType = true;
            _runtimeResourceType = MiningResourceType.LooseMineral;
            _isBreaking = false;
            _isBeingExtracted = false;
            _miningProgress = 0f;
            _pendingLooseMinerals = 0;
            mineralReserve = Mathf.Max(1, representedAmount);
            stoneCovered = false;
            exposedMineral = true;
            deepMineral = false;
            _loosePurity = Mathf.Clamp01(purity);
            _looseScorched = scorched;

            if (oreSource == null)
                oreSource = gameObject.AddComponent<LSO_Ore>();
            oreSource.oreSO = oreDefinition;
            CacheReferences();
        }

        public void LaunchInSpace(
            Vector3 direction,
            float minimumDistance,
            float maximumDistance,
            float travelDuration)
        {
            transform.SetParent(null, true);
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
                _rigidbody = gameObject.AddComponent<Rigidbody>();

            float minimum = Mathf.Max(0f, minimumDistance);
            float maximum = Mathf.Max(minimum, maximumDistance);
            _maximumMotionDistance = UnityEngine.Random.Range(minimum, maximum);
            _motionOrigin = _rigidbody.position;
            _motionActive = _maximumMotionDistance > 0f;

            if (direction.sqrMagnitude < 0.0001f)
                direction = UnityEngine.Random.onUnitSphere;

            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = false;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _rigidbody.linearDamping = linearDamping;
            _rigidbody.angularDamping = linearDamping * 0.5f;
            _rigidbody.linearVelocity = direction.normalized *
                                        (_maximumMotionDistance / Mathf.Max(0.05f, travelDuration));
            _rigidbody.angularVelocity = UnityEngine.Random.insideUnitSphere * angularSpeed;
        }

        private MiningFailureReason GetFailureReason(MiningTechType techType, MiningTechStats stats)
        {
            if (IsDepleted)
                return MiningFailureReason.Depleted;
            if (oreSource == null || oreSource.oreSO == null || oreSource.oreSO.mineral == null)
                return MiningFailureReason.MissingOreData;

            bool extractor = techType == MiningTechType.Extractor;
            if (extractor != (ResourceType == MiningResourceType.LooseMineral))
                return MiningFailureReason.WrongResourceType;
            if (!extractor)
                return MiningFailureReason.None;
            if (stoneCovered && !stats.CanMineCoveredMineral)
                return MiningFailureReason.CoveredMineral;
            if (!exposedMineral && !stats.CanMineDeepMineral)
                return MiningFailureReason.DeepMineral;

            return MiningFailureReason.None;
        }

        private MiningResourceType ResolveResourceType()
        {
            if (_hasRuntimeResourceType)
                return _runtimeResourceType;

            for (Transform current = transform; current != null; current = current.parent)
            {
                string objectTag = current.tag;
                if (objectTag == LooseMineralTag || objectTag == LegacyLooseMineralTag)
                    return MiningResourceType.LooseMineral;
                if (objectTag == StoneTag)
                    return MiningResourceType.Stone;
            }

            return untaggedResourceType;
        }

        private Vector3 GetWorldCenter()
        {
            Collider targetCollider = GetComponentInChildren<Collider>();
            if (targetCollider != null)
                return targetCollider.bounds.center;

            Renderer targetRenderer = GetComponentInChildren<Renderer>();
            return targetRenderer != null ? targetRenderer.bounds.center : transform.position;
        }

        private void CacheReferences()
        {
            if (oreSource == null)
                oreSource = GetComponent<LSO_Ore>() ??
                            GetComponentInParent<LSO_Ore>() ??
                            GetComponentInChildren<LSO_Ore>(true);
            if (_inventory == null)
                _inventory = LSO_PlayerInventory.Instance ?? FindFirstObjectByType<LSO_PlayerInventory>();
            if (_cargoWeight == null)
                _cargoWeight = FindFirstObjectByType<LSO_Weight>();
        }

        private bool TryResolveInventory()
        {
            CacheReferences();
            return _inventory != null;
        }

        private static MiningResult FailedResult(MiningFailureReason failure)
        {
            return new MiningResult(null, 0, 0, 0f, false, failure);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
