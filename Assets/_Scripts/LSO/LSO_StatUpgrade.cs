using _Scripts.LSO.UIScripts;
using _Scripts.Suxghui.Manager;
using TMPro;
using UnityEngine;

namespace _Scripts.LSO
{
    public class LSO_StatUpgrade : MonoBehaviour
    {
        [SerializeField] private LSO_StatTypes statType;
        
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI costText;

        [SerializeField] private string maxText = "MAX";
        
        private int _price;
        private int _level;
        private int _maxLevel;
        private bool _canUpgrade;
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            switch (statType)
            {
                case LSO_StatTypes.Speed:
                    _price = GameManager.Instance.SpeedUpgrade.NextUpgradeCost;
                    _level = GameManager.Instance.SaveData.shipSpeedLevel;
                    _canUpgrade = GameManager.Instance.SpeedUpgrade.CanUpgrade;
                    _maxLevel = GameManager.Instance.SpeedUpgrade.MaxLevel;
                    break;
                case LSO_StatTypes.Fuel:
                    _price = GameManager.Instance.HealthUpgrade.NextUpgradeCost;
                    _level = GameManager.Instance.SaveData.healthLevel;
                    _canUpgrade = GameManager.Instance.HealthUpgrade.CanUpgrade;
                    _maxLevel = GameManager.Instance.SaveData.healthLevel;
                    break;
                case LSO_StatTypes.Capacity:
                    _price = GameManager.Instance.CargoUpgrade.NextUpgradeCost;
                    _level = GameManager.Instance.SaveData.cargoLevel;
                    _canUpgrade = GameManager.Instance.CargoUpgrade.CanUpgrade;
                    _maxLevel = GameManager.Instance.SaveData.cargoLevel;
                    break;
                default:
                    Debug.LogWarning("값을 넣어주세요!");
                    break;
            }
        }
#endif

        private void Start() => Refresh();
        
        private void Refresh()
        {
            if (_level >= _maxLevel)
            {
                levelText.text = maxText;
                costText.text = "";
                return;
            }
            levelText.text = _level.ToString();
            costText.text = _price.ToString();
        }

        public void Upgrade()
        {
            if (_canUpgrade)
            {
                switch (statType)
                {
                    case LSO_StatTypes.Speed:
                        GameManager.Instance.CargoUpgrade.TryUpgrade();
                        break;
                    case LSO_StatTypes.Fuel:
                        GameManager.Instance.CargoUpgrade.TryUpgrade();
                        break;
                    case LSO_StatTypes.Capacity:
                        GameManager.Instance.CargoUpgrade.TryUpgrade();
                        break;
                }
            }
        }
    }
}