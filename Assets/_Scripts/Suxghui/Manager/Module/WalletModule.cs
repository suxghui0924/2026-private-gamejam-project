using System;

namespace _Scripts.Suxghui.Manager.Module
{
    public sealed class WalletModule
    {
        public int Money { get; private set; }
        public event Action<int> MoneyChanged;

        public WalletModule(int startingMoney)
        {
            Money = Math.Max(0, startingMoney);
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0)
                return;

            Money += amount;
            MoneyChanged?.Invoke(Money);
        }

        public bool TrySpendMoney(int amount)
        {
            if (amount < 0 || Money < amount)
                return false;

            Money -= amount;
            MoneyChanged?.Invoke(Money);
            return true;
        }

        public void SetMoney(int amount)
        {
            Money = Math.Max(0, amount);
            MoneyChanged?.Invoke(Money);
        }
    }
}
