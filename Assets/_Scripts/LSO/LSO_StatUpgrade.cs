using _Scripts.LSO.UIScripts;
using _Scripts.Suxghui.Manager;
using TMPro;
using UnityEngine;

namespace _Scripts.LSO
{
    public class LSO_StatUpgrade : MonoBehaviour
    {
        [Header("첫번쨰가 코스트, 두번쨰가 스탯")] 
        [SerializeField] private int[] costs;
        [SerializeField] private int[] stats;
        [SerializeField] private LSO_StatTypes statType;
        
        [SerializeField] private int level;
        
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI costText;

        [SerializeField] private string maxText = "MAX";
        public bool IsMax => level >= costs.Length;

        private void Start()
        {
            costText.text = costs[level].ToString();
            levelText.text = level.ToString();
        }

        public void Upgrade()
        {
            if (IsMax)
            {
                costText.text = "";
                levelText.text = maxText;
            }

            if (GameManager.Instance.Wallet.TrySpendMoney(costs[level]))
            {
                ApplyStat(stats[level]);
                level++;
                SetText();
                
                if (IsMax)
                {
                    costText.text = "";
                    levelText.text = maxText;
                }
            }
            else
            {
                Debug.Log("돈이 부족합니다");
            }
        }

        private void ApplyStat(int stat)
        {
            switch (statType)
            {
                case LSO_StatTypes.Speed:
                    //GameManager.Instance.SaveData.shipSpeed
                    break;
                case LSO_StatTypes.Fuel:
                    //GameManager.Instance.SaveData.maxFuel = stat;
                    break;
                case LSO_StatTypes.Capacity:
                   
                    break;
                default:
                    Debug.LogError("스탯 값을 설정하세요!");
                    break;
            }
        }
        
        private void SetText()
        {
            levelText.text = level.ToString();
            costText.text = costs[level].ToString();
        }
        
    }
}