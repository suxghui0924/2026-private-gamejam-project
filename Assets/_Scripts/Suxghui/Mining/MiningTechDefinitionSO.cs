using System;
using UnityEngine;

namespace _Scripts.Suxghui.Mining
{
    public enum MiningTechType
    {
        Drill = 0,
        Laser = 1,
        Extractor = 2
    }

    [Serializable]
    public struct MiningTechStats
    {
        public float DamagePerAction;
        public float ActionsPerSecond;
        public float Range;
        public float YieldMultiplier;
        public float PurityBonus;
        public float StoneRatio;
        public float ScorchChance;
        public float MovementMultiplier;
        public float ExtraExtractionChance;
        public float DeepExtractionChance;
        public int GuaranteedExtractionCount;
        public bool UsesContinuousBeam;
        public bool CanMineCoveredMineral;
        public bool CanMineDeepMineral;
        public float VisualScaleMultiplier;
        public float BeamDamagePerTick;
        public float BeamTickInterval;

        public float ActionInterval => 1f / Mathf.Max(0.01f, ActionsPerSecond);
    }

    [Serializable]
    public struct MiningTechLevelSettings
    {
        [Min(0)] public int upgradeCost;
        [Min(0.01f)] public float damagePerAction;
        [Min(0.01f)] public float actionsPerSecond;
        [Min(0.1f)] public float range;
        [Min(0.1f)] public float yieldMultiplier;
        [Range(-1f, 1f)] public float purityBonus;
        [Range(0f, 1f)] public float stoneRatio;
        [Range(0f, 1f)] public float scorchChance;
        [Range(0.1f, 1f)] public float movementMultiplier;
        [Range(0f, 1f)] public float extraExtractionChance;
        [Range(0f, 1f)] public float deepExtractionChance;
        [Min(0)] public int guaranteedExtractionCount;
        public bool usesContinuousBeam;
        public bool canMineCoveredMineral;
        public bool canMineDeepMineral;
        [Min(0.1f)] public float visualScaleMultiplier;
        [Min(0f)] public float beamDamagePerTick;
        [Min(0.01f)] public float beamTickInterval;

        public MiningTechStats ToStats()
        {
            return new MiningTechStats
            {
                DamagePerAction = Mathf.Max(0.01f, damagePerAction),
                ActionsPerSecond = Mathf.Max(0.01f, actionsPerSecond),
                Range = Mathf.Max(0.1f, range),
                YieldMultiplier = Mathf.Max(0.1f, yieldMultiplier),
                PurityBonus = Mathf.Clamp(purityBonus, -1f, 1f),
                StoneRatio = Mathf.Clamp01(stoneRatio),
                ScorchChance = Mathf.Clamp01(scorchChance),
                MovementMultiplier = Mathf.Clamp(movementMultiplier, 0.1f, 1f),
                ExtraExtractionChance = Mathf.Clamp01(extraExtractionChance),
                DeepExtractionChance = Mathf.Clamp01(deepExtractionChance),
                GuaranteedExtractionCount = Mathf.Max(0, guaranteedExtractionCount),
                UsesContinuousBeam = usesContinuousBeam,
                CanMineCoveredMineral = canMineCoveredMineral,
                CanMineDeepMineral = canMineDeepMineral,
                VisualScaleMultiplier = Mathf.Max(0.1f, visualScaleMultiplier),
                BeamDamagePerTick = Mathf.Max(0f, beamDamagePerTick),
                BeamTickInterval = Mathf.Max(0.01f, beamTickInterval)
            };
        }
    }

    [CreateAssetMenu(fileName = "Mining Tech", menuName = "Suxghui/Mining/Tech Definition")]
    public sealed class MiningTechDefinitionSO : ScriptableObject
    {
        [SerializeField] private string techId = "drill";
        [SerializeField] private string displayName = "Drill";
        [SerializeField] private MiningTechType techType;
        [SerializeField] private MiningTechLevelSettings[] levels = Array.Empty<MiningTechLevelSettings>();

        public string TechId => techId;
        public string DisplayName => displayName;
        public MiningTechType TechType => techType;
        public int MaxLevel => Mathf.Max(0, levels.Length - 1);

        public MiningTechStats GetStats(int level)
        {
            if (levels == null || levels.Length == 0)
                return CreateFallbackStats();

            return levels[Mathf.Clamp(level, 0, MaxLevel)].ToStats();
        }

        public int GetUpgradeCost(int targetLevel)
        {
            if (levels == null || levels.Length == 0 || targetLevel <= 0 || targetLevel > MaxLevel)
                return 0;

            return Mathf.Max(0, levels[targetLevel].upgradeCost);
        }

        private MiningTechStats CreateFallbackStats()
        {
            return new MiningTechStats
            {
                DamagePerAction = 1f,
                ActionsPerSecond = 1f,
                Range = 10f,
                YieldMultiplier = 1f,
                MovementMultiplier = 1f,
                CanMineCoveredMineral = techType != MiningTechType.Extractor,
                CanMineDeepMineral = techType != MiningTechType.Extractor,
                VisualScaleMultiplier = 1f
            };
        }
    }
}
