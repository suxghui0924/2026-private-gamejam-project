using System;
using _Scripts.Suxghui.Mining;

namespace _Scripts.Suxghui.Manager.Module
{
    public abstract class MiningTechUpgradeModule
    {
        private readonly WalletModule _wallet;
        private readonly Action<int> _saveLevel;

        protected MiningTechUpgradeModule(
            WalletModule wallet,
            MiningTechDefinitionSO settings,
            int savedLevel,
            Action<int> saveLevel)
        {
            _wallet = wallet;
            Settings = settings;
            _saveLevel = saveLevel;
            Level = settings == null ? 0 : Math.Clamp(savedLevel, 0, settings.MaxLevel);
        }

        public MiningTechDefinitionSO Settings { get; }
        public int Level { get; private set; }
        public int MaxLevel => Settings?.MaxLevel ?? 0;
        public int NextUpgradeCost => CanUpgrade ? Settings.GetUpgradeCost(Level + 1) : 0;
        public bool CanUpgrade => Settings != null && Level < Settings.MaxLevel;
        public MiningTechStats CurrentStats => Settings?.GetStats(Level) ?? default;

        public event Action<int, MiningTechStats> Upgraded;

        public bool TryUpgrade()
        {
            if (!CanUpgrade || _wallet == null || !_wallet.TrySpendMoney(NextUpgradeCost))
                return false;

            Level++;
            _saveLevel?.Invoke(Level);
            Upgraded?.Invoke(Level, CurrentStats);
            return true;
        }
    }
}
