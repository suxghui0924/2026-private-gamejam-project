using System;
using _Scripts.Suxghui.Manager.Upgrade;

namespace _Scripts.Suxghui.Manager.Module
{
    public abstract class ShipStatUpgradeModule
    {
        private readonly WalletModule _wallet;
        private readonly Action<int, float> _saveLevel;

        protected ShipStatUpgradeModule(
            WalletModule wallet,
            ShipStatUpgradeSO settings,
            int savedLevel,
            Action<int, float> saveLevel)
        {
            _wallet = wallet;
            Settings = settings;
            _saveLevel = saveLevel;
            Level = settings == null ? 0 : Math.Clamp(savedLevel, 0, settings.MaxLevel);
        }

        public ShipStatUpgradeSO Settings { get; }
        public int Level { get; private set; }
        public int MaxLevel => Settings?.MaxLevel ?? 0;
        public float CurrentValue => Settings?.GetValue(Level) ?? 0f;
        public int NextUpgradeCost => CanUpgrade ? Settings.GetUpgradeCost(Level + 1) : 0;
        public bool CanUpgrade => Settings != null && Level < Settings.MaxLevel;

        public event Action<int, float> Upgraded;

        public bool TryUpgrade()
        {
            if (!CanUpgrade || _wallet == null || !_wallet.TrySpendMoney(NextUpgradeCost))
                return false;

            Level++;
            float value = CurrentValue;
            _saveLevel?.Invoke(Level, value);
            Upgraded?.Invoke(Level, value);
            return true;
        }
    }
}
