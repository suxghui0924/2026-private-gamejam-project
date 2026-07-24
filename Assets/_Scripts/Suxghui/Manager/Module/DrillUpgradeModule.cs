using System;
using _Scripts.Suxghui.Mining;

namespace _Scripts.Suxghui.Manager.Module
{
    public sealed class DrillUpgradeModule : MiningTechUpgradeModule
    {
        public DrillUpgradeModule(WalletModule wallet, MiningTechDefinitionSO settings, int savedLevel, Action<int> saveLevel)
            : base(wallet, settings, savedLevel, saveLevel)
        {
        }
    }
}
