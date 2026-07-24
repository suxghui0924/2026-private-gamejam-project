using System;
using _Scripts.Suxghui.Manager;
using UnityEngine;

namespace _Scripts.Suxghui.Mining
{
    public enum MiningFailureReason
    {
        None,
        Depleted,
        CoveredMineral,
        DeepMineral
    }

    public readonly struct MiningResult
    {
        public MiningResult(int mineralAmount, int stoneAmount, float purity, bool scorched, MiningFailureReason failure)
        {
            MineralAmount = mineralAmount;
            StoneAmount = stoneAmount;
            Purity = purity;
            Scorched = scorched;
            Failure = failure;
        }

        public int MineralAmount { get; }
        public int StoneAmount { get; }
        public float Purity { get; }
        public bool Scorched { get; }
        public MiningFailureReason Failure { get; }
        public bool ProducedItems => MineralAmount > 0 || StoneAmount > 0;
    }

    public sealed class MineableAsteroid : MonoBehaviour
    {
        [Header("Deposit")]
        [SerializeField] private string mineralItemId = "mineral";
        [SerializeField] private string scorchedMineralItemId = "mineral_scorched";
        [SerializeField] private string stoneItemId = "stone";
        [SerializeField, Min(1)] private int mineralReserve = 12;
        [SerializeField, Min(0.01f)] private float durabilityPerDrop = 100f;
        [SerializeField, Range(0f, 1f)] private float basePurity = 0.55f;

        [Header("Extractor Rules")]
        [SerializeField] private bool stoneCovered;
        [SerializeField] private bool exposedMineral = true;
        [SerializeField] private bool deepMineral;

        private float _miningProgress;

        public int MineralReserve => mineralReserve;
        public bool IsDepleted => mineralReserve <= 0;
        public event Action<MiningResult> Mined;

        public MiningFailureReason ValidateMining(MiningTechType techType, MiningTechStats stats)
        {
            return GetFailureReason(techType, stats);
        }

        public MiningResult ApplyMining(MiningTechType techType, MiningTechStats stats, float damageMultiplier = 1f)
        {
            MiningFailureReason failure = ValidateMining(techType, stats);
            if (failure != MiningFailureReason.None)
                return new MiningResult(0, 0, basePurity, false, failure);

            _miningProgress += stats.DamagePerAction * Mathf.Max(0f, damageMultiplier);
            if (_miningProgress < durabilityPerDrop)
                return new MiningResult(0, 0, basePurity, false, MiningFailureReason.None);

            int completedDrops = Mathf.FloorToInt(_miningProgress / durabilityPerDrop);
            _miningProgress -= completedDrops * durabilityPerDrop;

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

            int mineralAmount = Mathf.Min(mineralReserve, requestedAmount);
            mineralReserve -= mineralAmount;

            float purity = Mathf.Clamp01(basePurity + stats.PurityBonus);
            bool scorched = techType == MiningTechType.Laser && UnityEngine.Random.value < stats.ScorchChance;
            int stoneAmount = techType == MiningTechType.Drill
                ? RollStoneAmount(mineralAmount, stats.StoneRatio)
                : 0;

            AddToInventory(scorched ? scorchedMineralItemId : mineralItemId, mineralAmount);
            AddToInventory(stoneItemId, stoneAmount);

            MiningResult result = new MiningResult(mineralAmount, stoneAmount, purity, scorched, MiningFailureReason.None);
            Mined?.Invoke(result);
            return result;
        }

        private MiningFailureReason GetFailureReason(MiningTechType techType, MiningTechStats stats)
        {
            if (IsDepleted)
                return MiningFailureReason.Depleted;

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

        private static void AddToInventory(string itemId, int amount)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
                return;

            GameManager.Instance.Inventory?.AddItem(itemId, amount);
        }
    }
}
