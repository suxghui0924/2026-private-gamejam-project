using System;
using _Scripts.Suxghui.Manager.Upgrade;

namespace _Scripts.Suxghui.Manager.Module
{
    public sealed class FuelUpgradeModule : ShipStatUpgradeModule
    {
        public FuelUpgradeModule(
            WalletModule wallet,
            ShipStatUpgradeSO settings,
            int savedLevel,
            Action<int, float> saveLevel)
            : base(wallet, settings, savedLevel, saveLevel)
        {
        }
    }
}
