using System;
using UnityEngine;

namespace _Scripts.Suxghui.Manager.Upgrade
{
    [Serializable]
    public struct ShipStatUpgradeLevel
    {
        [Min(0f)] public float value;
        [Min(0)] public int upgradeCost;
    }

    [CreateAssetMenu(fileName = "Ship Stat Upgrade", menuName = "Suxghui/Upgrade/Ship Stat")]
    public sealed class ShipStatUpgradeSO : ScriptableObject
    {
        [SerializeField] private string statId = "health";
        [SerializeField] private ShipStatUpgradeLevel[] levels = Array.Empty<ShipStatUpgradeLevel>();

        public string StatId => statId;
        public int MaxLevel => Mathf.Max(0, levels.Length - 1);

        public float GetValue(int level)
        {
            if (levels == null || levels.Length == 0)
                return 0f;

            return levels[Mathf.Clamp(level, 0, MaxLevel)].value;
        }

        public int GetUpgradeCost(int targetLevel)
        {
            if (levels == null || levels.Length == 0 || targetLevel <= 0 || targetLevel > MaxLevel)
                return 0;

            return Mathf.Max(0, levels[targetLevel].upgradeCost);
        }

        private void OnValidate()
        {
            if (levels == null)
                levels = Array.Empty<ShipStatUpgradeLevel>();

            for (int i = 0; i < levels.Length; i++)
            {
                levels[i].value = Mathf.Max(0f, levels[i].value);
                levels[i].upgradeCost = Mathf.Max(0, levels[i].upgradeCost);
            }
        }
    }
}
