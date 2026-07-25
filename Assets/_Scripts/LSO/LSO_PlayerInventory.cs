using System.Collections.Generic;
using _Scripts.LSO.Data;
using UnityEngine;

namespace _Scripts.LSO
{
    public class LSO_PlayerInventory : MonoBehaviour
    {
        public static LSO_PlayerInventory Instance;
    
        public Dictionary<LSO_MineralSO, int> PlayerInventory { get; private set; } = new();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    
        public int CalculateInventory(Dictionary<LSO_MineralSO, int> sourceInventory = null)
        {
            var targetInventory = sourceInventory ?? PlayerInventory;
            if (targetInventory == null) return 0;
        
            int result = 0;
            foreach (var slot in targetInventory)
            {
                result += slot.Value * slot.Key.PricePerKilogram;
            }
            return result;
        }

        public void AddItem(LSO_MineralSO item, int amount = 1)
        {
            if (amount <= 0) return;
        
            if (!PlayerInventory.TryAdd(item, amount))
            {
                PlayerInventory[item] += amount;
            }
        }

    
        public void RemoveItem(LSO_MineralSO item, int amount = -1)
        {
            if (item == null) return;

            // amount가 음수이거나 지정되지 않으면 전량 삭제
            if (amount < 0)
            {
                PlayerInventory.Remove(item);
                return;
            }
    
            // TryGetValue 하나로 탐색을 단축 (오버헤드 감소)
            if (PlayerInventory.TryGetValue(item, out int currentAmount))
            {
                int newAmount = currentAmount - amount;

                if (newAmount <= 0)
                {
                    PlayerInventory.Remove(item);
                }
                else
                {
                    PlayerInventory[item] = newAmount;
                }
            }
        }
    }
}
