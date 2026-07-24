using System;
using System.Collections.Generic;
using _Scripts.LSO.Data;

namespace _Scripts.Suxghui.Manager.Module
{
    public sealed class InventoryModule
    {
        private readonly Dictionary<LSO_MineralSO, int> _items = new Dictionary<LSO_MineralSO, int>();

        public event Action<LSO_MineralSO, int> ItemChanged;

        public InventoryModule(InventoryItemSaveData[] savedItems)
        {
            if (savedItems == null)
                return;

            foreach (InventoryItemSaveData item in savedItems)
                AddItem(item.itemId, item.amount, false);
        }

        public int GetItemAmount(LSO_MineralSO itemId)
        {
            return itemId && _items.TryGetValue(itemId, out int amount)
                ? amount
                : 0;
        }

        public void AddItem(LSO_MineralSO itemId, int amount)
        {
            AddItem(itemId, amount, true);
        }

        public bool TryRemoveItem(LSO_MineralSO itemId, int amount)
        {
            if (!itemId || amount <= 0 || GetItemAmount(itemId) < amount)
                return false;

            _items[itemId] -= amount;
            if (_items[itemId] <= 0)
                _items.Remove(itemId);

            ItemChanged?.Invoke(itemId, GetItemAmount(itemId));
            return true;
        }

        public InventoryItemSaveData[] ToSaveData()
        {
            InventoryItemSaveData[] result = new InventoryItemSaveData[_items.Count];
            int index = 0;

            foreach (KeyValuePair<LSO_MineralSO, int> item in _items)
                result[index++] = new InventoryItemSaveData(item.Key, item.Value);

            return result;
        }

        public void Clear()
        {
            LSO_MineralSO[] itemIds = new LSO_MineralSO[_items.Count];
            _items.Keys.CopyTo(itemIds, 0);

            foreach (LSO_MineralSO itemId in itemIds)
                TryRemoveItem(itemId, GetItemAmount(itemId));
        }

        private void AddItem(LSO_MineralSO itemId, int amount, bool notify)
        {
            if (!itemId || amount <= 0)
                return;

            _items.TryGetValue(itemId, out int currentAmount);
            _items[itemId] = currentAmount + amount;

            if (notify)
                ItemChanged?.Invoke(itemId, _items[itemId]);
        }
    }
}
