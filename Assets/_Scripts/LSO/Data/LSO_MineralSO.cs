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
        [Tooltip("광석 파괴 시 월드에 생성할 원석 모델 프리팹입니다.")]
        public GameObject mineralPrefab;
        [ColorUsage(false, true)]
        public Color mineralColor = Color.white;
        [TextArea]
        public string mineralDescription;

        [Header("Economy")]
        [Min(0)]
        public int mineralPrice;
        public LSO_MineralRarity mineralRarity;
    }
}
