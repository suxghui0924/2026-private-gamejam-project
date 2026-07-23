using UnityEngine;

namespace _Scripts.LSO.Data
{
    [CreateAssetMenu(fileName = "New MineralSO",menuName = "SO/LSO_MineralSO")]
    public class LSO_MineralSO : ScriptableObject
    {
        [Header("광물 종류")]
        public LSO_MineralType mineralType;
        [Header("설명")]
        public string mineralDescription;
        [Header("Kg당 가격")]
        public int mineralPrice;
        [Header("광물 희귀도")]
        public LSO_MineralRarity mineralRarity;
    }
}