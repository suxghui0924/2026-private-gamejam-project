using System;
using UnityEngine.Serialization;

namespace _Scripts.Suxghui.Manager
{
    [Serializable]
    public struct InventoryItemSaveData
    {
        public string itemId;
        public int amount;

        public InventoryItemSaveData(string itemId, int amount)
        {
            this.itemId = itemId;
            this.amount = amount;
        }
    }

    [Serializable]
    public struct GameSaveData
    {
        public int saveVersion;
        public int money;
        [FormerlySerializedAs("fuel")] public float health;
        [FormerlySerializedAs("maxFuel")] public float maxHealth;
        public float fuel;
        public float maxFuel;
        public float cargoWeight;
        public float maxCargoWeight;
        public int selectedMiningTool;
        public string currentTechId;
        public int drillLevel;
        public int laserLevel;
        public int extractorLevel;
        public int analysisRadarLevel;
        public int shipSpeedLevel;
        public int cargoLevel;
        [FormerlySerializedAs("fuelLevel")] public int healthLevel;
        public int fuelLevel;
        public int boosterLevel;
        public string currentMiningToolId;
        public InventoryItemSaveData[] inventoryItems;

        public static GameSaveData CreateDefault()
        {
            return new GameSaveData
            {
                saveVersion = 1,
                money = 0,
                health = 100f,
                maxHealth = 100f,
                fuel = 100f,
                maxFuel = 100f,
                cargoWeight = 0f,
                maxCargoWeight = 100f,
                selectedMiningTool = 0,
                currentTechId = "drill",
                drillLevel = 0,
                laserLevel = 0,
                extractorLevel = 0,
                analysisRadarLevel = 0,
                shipSpeedLevel = 0,
                cargoLevel = 0,
                healthLevel = 0,
                fuelLevel = 0,
                boosterLevel = 0,
                currentMiningToolId = string.Empty,
                inventoryItems = Array.Empty<InventoryItemSaveData>()
            };
        }
    }
}
