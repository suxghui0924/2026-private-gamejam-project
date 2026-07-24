using System;
using _Scripts.LSO;
using _Scripts.LSO.Data;
using _Scripts.Suxghui.Manager;
using UnityEngine;

namespace _Scripts.Suxghui.Mining
{
    public enum MiningFailureReason
    {
        None,
        Depleted,
        CoveredMineral,
        DeepMineral,
        MissingOreData,
        InventoryUnavailable,
        StorageFull
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
        [Header("LSO Ore")]
        [SerializeField] private LSO_Ore oreSource;
        [SerializeField] private LSO_MineralSO scorchedMineralOverride;
        [SerializeField] private LSO_MineralSO stoneMineral;

        [Header("Deposit")]
        [SerializeField, Min(1)] private int mineralReserve = 12;
        [SerializeField, Min(0.01f)] private float durabilityPerDrop = 100f;
        [SerializeField, Range(0f, 1f)] private float basePurity = 0.55f;

        [Header("Extractor Rules")]
        [SerializeField] private bool stoneCovered;
        [SerializeField] private bool exposedMineral = true;
        [SerializeField] private bool deepMineral;

        private float _miningProgress;
        private LSO_PlayerInventory _inventory;
        private LSO_Weight _cargoWeight;

        public int MineralReserve => mineralReserve;
        public bool IsDepleted => mineralReserve <= 0;
        public LSO_Ore OreSource => oreSource;
        public event Action<MiningResult> Mined;

        private void Awake()
        {
            CacheReferences();
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

        public MiningResult ApplyMining(MiningTechType techType, MiningTechStats stats, float damageMultiplier = 1f)
        {
            MiningFailureReason failure = ValidateMining(techType, stats);
            if (failure != MiningFailureReason.None)
                return FailedResult(failure);

            _miningProgress += stats.DamagePerAction * Mathf.Max(0f, damageMultiplier);
            if (_miningProgress < durabilityPerDrop)
                return FailedResult(MiningFailureReason.None);

            int completedDrops = Mathf.FloorToInt(_miningProgress / durabilityPerDrop);
            int requestedAmount = Mathf.Max(1, Mathf.RoundToInt(completedDrops * stats.YieldMultiplier));
            if (techType == MiningTechType.Extractor)
            {
                if (UnityEngine.Random.value < stats.ExtraExtractionChance)
                    requestedAmount++;

                if (deepMineral && UnityEngine.Random.value < stats.DeepExtractionChance)
                    requestedAmount++;

                if (mineralReserve >= 2 && stats.GuaranteedExtractionCount >= 2)
                    requestedAmount = Mathf.Max(requestedAmount, stats.GuaranteedExtractionCount);
            }

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

            LSO_MineralSO minedMineral = oreSource.Mine();
            if (minedMineral == null)
                return FailedResult(MiningFailureReason.MissingOreData);

            float purity = Mathf.Clamp01(basePurity + stats.PurityBonus);
            bool scorched = techType == MiningTechType.Laser && UnityEngine.Random.value < stats.ScorchChance;
            int stoneAmount = techType == MiningTechType.Drill
                ? RollStoneAmount(mineralAmount, stats.StoneRatio)
                : 0;
            stoneAmount = stoneMineral != null
                ? Mathf.Min(stoneAmount, Mathf.Max(0, availableCapacity - mineralAmount))
                : 0;

            LSO_MineralSO storedMineral = scorched && scorchedMineralOverride != null
                ? scorchedMineralOverride
                : minedMineral;
            _inventory.AddItem(storedMineral, mineralAmount);
            if (stoneAmount > 0)
                _inventory.AddItem(stoneMineral, stoneAmount);
            _cargoWeight?.AddWeight(mineralAmount + stoneAmount);

            mineralReserve -= mineralAmount;
            _miningProgress -= completedDrops * durabilityPerDrop;

            MiningResult result = new MiningResult(
                storedMineral,
                mineralAmount,
                stoneAmount,
                purity,
                scorched,
                MiningFailureReason.None);
            Mined?.Invoke(result);
            return result;
        }

        private MiningFailureReason GetFailureReason(MiningTechType techType, MiningTechStats stats)
        {
            if (IsDepleted)
                return MiningFailureReason.Depleted;
            if (oreSource == null || oreSource.oreSO == null || oreSource.oreSO.mineral == null)
                return MiningFailureReason.MissingOreData;

            if (techType != MiningTechType.Extractor)
                return MiningFailureReason.None;

            if (stoneCovered && !stats.CanMineCoveredMineral)
                return MiningFailureReason.CoveredMineral;
            if (!exposedMineral && !stats.CanMineDeepMineral)
                return MiningFailureReason.DeepMineral;

            return MiningFailureReason.None;
        }

        private static int RollStoneAmount(int mineralAmount, float stoneRatio)
        {
            float expectedStone = mineralAmount * Mathf.Clamp01(stoneRatio);
            int amount = Mathf.FloorToInt(expectedStone);
            if (UnityEngine.Random.value < expectedStone - amount)
                amount++;
            return amount;
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
                _cargoWeight = LSO_Weight.Instance ?? FindFirstObjectByType<LSO_Weight>();
        }

        private bool TryResolveInventory()
        {
            CacheReferences();
            if (_inventory != null)
                return true;

            GameManager manager = GameManager.Instance;
            if (manager == null)
                return false;

            _inventory = manager.GetComponent<LSO_PlayerInventory>();
            if (_inventory == null)
                _inventory = manager.gameObject.AddComponent<LSO_PlayerInventory>();
            return _inventory != null;
        }

        private MiningResult FailedResult(MiningFailureReason failure)
        {
            LSO_MineralSO mineral = oreSource != null && oreSource.oreSO != null
                ? oreSource.oreSO.mineral
                : null;
            return new MiningResult(mineral, 0, 0, basePurity, false, failure);
        }
    }
}
