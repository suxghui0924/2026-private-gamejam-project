using System;
using System.Collections.Generic;

namespace _Scripts.Suxghui.Manager.Module
{
    public sealed class InventoryModule
    {
        private readonly Dictionary<string, int> _items = new Dictionary<string, int>();

        public event Action<string, int> ItemChanged;

        public InventoryModule(InventoryItemSaveData[] savedItems)
        {
            if (savedItems == null)
                return;

            foreach (InventoryItemSaveData item in savedItems)
                AddItem(item.itemId, item.amount, false);
        }

        public int GetItemAmount(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) && _items.TryGetValue(itemId, out int amount)
                ? amount
                : 0;
        }

        public void AddItem(string itemId, int amount)
        {
            AddItem(itemId, amount, true);
        }

        public bool TryRemoveItem(string itemId, int amount)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || GetItemAmount(itemId) < amount)
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

            foreach (KeyValuePair<string, int> item in _items)
                result[index++] = new InventoryItemSaveData(item.Key, item.Value);

            return result;
        }

        public void Clear()
        {
            string[] itemIds = new string[_items.Count];
            _items.Keys.CopyTo(itemIds, 0);

            foreach (string itemId in itemIds)
                TryRemoveItem(itemId, GetItemAmount(itemId));
        }

        private void AddItem(string itemId, int amount, bool notify)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                return;

            _items.TryGetValue(itemId, out int currentAmount);
            _items[itemId] = currentAmount + amount;

            if (notify)
                ItemChanged?.Invoke(itemId, _items[itemId]);
        }
    }
}
