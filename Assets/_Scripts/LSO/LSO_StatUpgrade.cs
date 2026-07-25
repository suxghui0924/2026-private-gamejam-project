using _Scripts.LSO.UIScripts;
using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Manager.Module;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        private bool _canAfford;
        private ShipStatUpgradeModule _boundModule;
        private Button _button;
        private WalletModule _wallet;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null)
                _button.onClick.AddListener(Upgrade);
        }

        private void Start()
        {
            RefreshState();
        }

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

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(Upgrade);
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
                _wallet = GameManager.Instance?.Wallet;
                _canAfford = _wallet == null || _wallet.Money >= _price;
            }

            if (_button != null)
                _button.interactable = _canUpgrade && _canAfford;
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
            _wallet = GameManager.Instance?.Wallet;
            if (_wallet != null)
                _wallet.MoneyChanged += HandleMoneyChanged;
        }

        private void UnbindModule()
        {
            if (_boundModule != null)
                _boundModule.Upgraded -= HandleUpgraded;
            if (_wallet != null)
                _wallet.MoneyChanged -= HandleMoneyChanged;
            _wallet = null;
            _boundModule = null;
        }

        private ShipStatUpgradeModule ResolveModule(GameManager manager)
        {
            if (manager == null)
                return null;

            return statType switch
            {
                LSO_StatTypes.Speed => manager.SpeedUpgrade,
                LSO_StatTypes.Fuel => manager.FuelUpgrade,
                LSO_StatTypes.Capacity => manager.CargoUpgrade,
                _ => null
            };
        }

        private void HandleUpgraded(int level, float value)
        {
            RefreshState();
            NotificationManager.Notify($"{statType} 업그레이드 성공 (Lv.{level})");
        }

        private void HandleMoneyChanged(int money) => RefreshState();

        private void Refresh()
        {
            if (levelText == null || costText == null)
                return;

            if (_maxLevel <= 0)
            {
                levelText.text = "-";
                costText.text = "";
                if (icon != null)
                    icon.SetActive(false);
                return;
            }

            if (_level >= _maxLevel)
            {
                levelText.text = maxText;
                costText.text = "";
                if (icon != null)
                    icon.SetActive(false);
                return;
            }

            levelText.text = _level.ToString();
            costText.text = _price.ToString();
            if (icon != null)
                icon.SetActive(true);
        }

        public void Upgrade()
        {
            BindModule();
            if (_boundModule == null || !_canUpgrade || !_boundModule.TryUpgrade())
            {
                if (_boundModule != null && _boundModule.CanUpgrade)
                    NotificationManager.Notify($"업그레이드 실패: {Mathf.Max(0, _price - (GameManager.Instance?.Wallet?.Money ?? 0))} 코인 부족");
                RefreshState();
            }
        }
    }
}
