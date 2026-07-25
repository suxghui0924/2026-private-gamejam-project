using UnityEngine;

namespace _Scripts.LSO.Data
{
    [CreateAssetMenu(fileName = "New MineralSO", menuName = "SO/LSO_MineralSO")]
    public class LSO_MineralSO : ScriptableObject
    {
        [Header("Identity")]
        public LSO_MineralType mineralType;
        public string mineralName;

        [Header("Presentation")]
        [Tooltip("월드에 배치되는 공용 원석 모델에 적용할 머티리얼입니다.")]
        public Material mineralMaterial;
        [TextArea]
        public string mineralDescription;

        [Header("Economy")]
        [InspectorName("Price Per Kg")]
        [Tooltip("Selling price for one kilogram. Inventory amount 1 equals 1 kg.")]
        [Min(0)]
        public int mineralPrice;
        public int PricePerKilogram => Mathf.Max(0, mineralPrice);
        public LSO_MineralRarity mineralRarity;
    }
}
