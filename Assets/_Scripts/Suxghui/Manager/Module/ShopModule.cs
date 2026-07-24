using System.Collections.Generic;
using _Scripts.LSO.Data;

namespace _Scripts.Suxghui.Manager.Module
{
    public sealed class ShopModule
    {
        private readonly WalletModule _wallet;
        private readonly InventoryModule _inventory;
        private readonly Dictionary<LSO_MineralSO, int> _buyPrices = new Dictionary<LSO_MineralSO, int>();
        private readonly Dictionary<LSO_MineralSO, int> _sellPrices = new Dictionary<LSO_MineralSO, int>();

        public ShopModule(WalletModule wallet, InventoryModule inventory)
        {
            _wallet = wallet;
            _inventory = inventory;
        }

        public void RegisterItem(LSO_MineralSO itemId, int buyPrice, int sellPrice)
        {
            if (!itemId)
                return;

            _buyPrices[itemId] = buyPrice < 0 ? 0 : buyPrice;
            _sellPrices[itemId] = sellPrice < 0 ? 0 : sellPrice;
        }

        public bool TryBuy(LSO_MineralSO itemId, int amount = 1)
        {
            if (amount <= 0 || !_buyPrices.TryGetValue(itemId, out int price))
                return false;

            int totalPrice = price * amount;
            if (!_wallet.TrySpendMoney(totalPrice))
                return false;

            _inventory.AddItem(itemId, amount);
            return true;
        }

        public bool TrySell(LSO_MineralSO itemId, int amount = 1)
        {
            if (amount <= 0 || !_sellPrices.TryGetValue(itemId, out int price))
                return false;

            if (!_inventory.TryRemoveItem(itemId, amount))
                return false;

            _wallet.AddMoney(price * amount);
            return true;
        }

        public int SellAll()
        {
            InventoryItemSaveData[] items = _inventory.ToSaveData();
            int totalPrice = 0;

            foreach (InventoryItemSaveData item in items)
            {
                if (_sellPrices.TryGetValue(item.itemId, out int price))
                    totalPrice += price * item.amount;
            }

            foreach (InventoryItemSaveData item in items)
            {
                if (_sellPrices.ContainsKey(item.itemId))
                    _inventory.TryRemoveItem(item.itemId, item.amount);
            }

            _wallet.AddMoney(totalPrice);
            return totalPrice;
        }
    }
}
