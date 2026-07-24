using _Scripts.LSO.UIScripts;
using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Manager.Module;
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
        [SerializeField] private GameObject icon;
        
        private int _price;
        private int _level;
        private int _maxLevel;
        private bool _canUpgrade;
        private ShipStatUpgradeModule _boundModule;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                RefreshState();
        }
#endif

        private void OnEnable()
        {
            if (Application.isPlaying)
                RefreshState();
        }

        private void OnDisable()
        {
            UnbindModule();
        }

        private void RefreshState()
        {
            BindModule();

            if (_boundModule == null)
            {
                _price = 0;
                _level = 0;
                _maxLevel = 0;
                _canUpgrade = false;
            }
            else
            {
                _price = _boundModule.NextUpgradeCost;
                _level = _boundModule.Level;
                _maxLevel = _boundModule.MaxLevel;
                _canUpgrade = _boundModule.CanUpgrade;
            }

            Refresh();
        }

        private void BindModule()
        {
            ShipStatUpgradeModule nextModule = ResolveModule(GameManager.Instance);
            if (_boundModule == nextModule)
                return;

            UnbindModule();
            _boundModule = nextModule;
            if (_boundModule != null)
                _boundModule.Upgraded += HandleUpgraded;
        }

        private void UnbindModule()
        {
            if (_boundModule != null)
                _boundModule.Upgraded -= HandleUpgraded;
            _boundModule = null;
        }

        private ShipStatUpgradeModule ResolveModule(GameManager manager)
        {
            if (manager == null)
                return null;

            return statType switch
            {
                LSO_StatTypes.Speed => manager.SpeedUpgrade,
                LSO_StatTypes.Fuel => manager.HealthUpgrade,
                LSO_StatTypes.Capacity => manager.CargoUpgrade,
                _ => null
            };
        }

        private void HandleUpgraded(int level, float value)
        {
            RefreshState();
        }

        private void Refresh()
        {
            if (levelText == null || costText == null)
                return;

            if (_maxLevel <= 0)
            {
                levelText.text = "-";
                costText.text = "";
                icon.SetActive(false);
                return;
            }

            if (_level >= _maxLevel)
            {
                levelText.text = maxText;
                costText.text = "";
                icon.SetActive(false);
                return;
            }

            levelText.text = _level.ToString();
            costText.text = _price.ToString();
        }

        public void Upgrade()
        {
            BindModule();
            if (_boundModule == null || !_canUpgrade || !_boundModule.TryUpgrade())
                RefreshState();
        }
    }
}
