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
        [Min(0)]
        public int mineralPrice;
        public LSO_MineralRarity mineralRarity;
    }
}
