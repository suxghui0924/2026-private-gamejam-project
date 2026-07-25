using System;
using _Scripts.LSO.Data;

namespace _Scripts.Suxghui.Manager
{
    [Serializable]
    public struct InventoryItemSaveData
    {
        public LSO_MineralSO itemId;
        public int amount;

        public InventoryItemSaveData(LSO_MineralSO itemId, int amount)
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
        public float fuel;
        public float maxFuel;
        public float cargoWeight;
        public float maxCargoWeight;
        public float shipSpeed;
        public int selectedMiningTool;
        public string currentTechId;
        public int drillLevel;
        public int laserLevel;
        public int extractorLevel;
        public int analysisRadarLevel;
        public int shipSpeedLevel;
        public int cargoLevel;
        public int fuelLevel;
        public int boosterLevel;
        public bool endingReached;
        public string currentMiningToolId;
        public InventoryItemSaveData[] inventoryItems;

        public static GameSaveData CreateDefault()
        {
            return new GameSaveData
            {
                saveVersion = 3,
                money = 0,
                fuel = 120f,
                maxFuel = 120f,
                cargoWeight = 0f,
                maxCargoWeight = 20f,
                shipSpeed = 10f,
                selectedMiningTool = 0,
                currentTechId = "drill",
                drillLevel = 0,
                laserLevel = 0,
                extractorLevel = 0,
                analysisRadarLevel = 0,
                shipSpeedLevel = 0,
                cargoLevel = 0,
                fuelLevel = 0,
                boosterLevel = 0,
                endingReached = false,
                currentMiningToolId = string.Empty,
                inventoryItems = Array.Empty<InventoryItemSaveData>()
            };
        }
    }
}
