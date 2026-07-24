using System;
using System.IO;
using _Scripts.Suxghui.CoreLib;
using _Scripts.Suxghui.Manager.Module;
using _Scripts.Suxghui.Manager.Upgrade;
using _Scripts.Suxghui.Mining;
using UnityEngine;

namespace _Scripts.Suxghui.Manager
{
    public class GameManager : MonoSingleton<GameManager>
    {
        private const int CurrentSaveVersion = 2;
        private const string SaveFileName = "save.json";
        private const string HealthUpgradePath = "Suxghui/Upgrades/HealthUpgrade";
        private const string CargoUpgradePath = "Suxghui/Upgrades/CargoUpgrade";
        private const string SpeedUpgradePath = "Suxghui/Upgrades/SpeedUpgrade";

        public GameSaveData SaveData { get; private set; }
        public WalletModule Wallet { get; private set; }
        public InventoryModule Inventory { get; private set; }
        public ShopModule Shop { get; private set; }
        public HealthUpgradeModule HealthUpgrade { get; private set; }
        public CargoUpgradeModule CargoUpgrade { get; private set; }
        public SpeedUpgradeModule SpeedUpgrade { get; private set; }
        public DrillUpgradeModule DrillUpgrade { get; private set; }
        public LaserUpgradeModule LaserUpgrade { get; private set; }
        public ExtractorUpgradeModule ExtractorUpgrade { get; private set; }
        public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
                return;

            DontDestroyOnLoad(gameObject);
            Load();
        }

        public void Save()
        {
            GameSaveData dataToSave = SaveData;
            if (Wallet != null)
                dataToSave.money = Wallet.Money;
            if (Inventory != null)
                dataToSave.inventoryItems = Inventory.ToSaveData();

            dataToSave.saveVersion = CurrentSaveVersion;
            SaveData = dataToSave;

            string json = JsonUtility.ToJson(dataToSave, true);
            File.WriteAllText(SavePath, json);
        }

        public void Load()
        {
            if (!File.Exists(SavePath))
            {
                SaveData = GameSaveData.CreateDefault();
                InitializeModules();
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                GameSaveData loadedData = JsonUtility.FromJson<GameSaveData>(json);

                MigrateSaveData(ref loadedData);

                if (loadedData.inventoryItems == null)
                    loadedData.inventoryItems = Array.Empty<InventoryItemSaveData>();

                SaveData = loadedData;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to load save data: {exception.Message}");
                SaveData = GameSaveData.CreateDefault();
            }

            InitializeModules();
        }

        public void ResetSave()
        {
            SaveData = GameSaveData.CreateDefault();
            InitializeModules();

            if (File.Exists(SavePath))
                File.Delete(SavePath);
        }

        public void SetSelectedMiningTech(int selectedIndex, string techId)
        {
            GameSaveData data = SaveData;
            data.selectedMiningTool = Mathf.Clamp(selectedIndex, 0, 2);
            data.currentTechId = techId ?? string.Empty;
            data.currentMiningToolId = techId ?? string.Empty;
            SaveData = data;
        }

        public int GetMiningTechLevel(string techId)
        {
            return techId switch
            {
                "drill" => SaveData.drillLevel,
                "laser" => SaveData.laserLevel,
                "extractor" => SaveData.extractorLevel,
                _ => 0
            };
        }

        public void SetMiningTechLevel(string techId, int level)
        {
            GameSaveData data = SaveData;
            int maxLevel = techId switch
            {
                "drill" => DrillUpgrade?.MaxLevel ?? 5,
                "laser" => LaserUpgrade?.MaxLevel ?? 5,
                "extractor" => ExtractorUpgrade?.MaxLevel ?? 5,
                _ => 5
            };
            level = Mathf.Clamp(level, 0, maxLevel);

            switch (techId)
            {
                case "drill":
                    data.drillLevel = level;
                    break;
                case "laser":
                    data.laserLevel = level;
                    break;
                case "extractor":
                    data.extractorLevel = level;
                    break;
                default:
                    return;
            }

            SaveData = data;
        }

        public void ConfigureMiningTechUpgrades(
            MiningTechDefinitionSO drillSettings,
            MiningTechDefinitionSO laserSettings,
            MiningTechDefinitionSO extractorSettings)
        {
            if (DrillUpgrade?.Settings == drillSettings &&
                LaserUpgrade?.Settings == laserSettings &&
                ExtractorUpgrade?.Settings == extractorSettings)
                return;

            DrillUpgrade = new DrillUpgradeModule(
                Wallet,
                drillSettings,
                SaveData.drillLevel,
                level => SaveMiningTechLevel("drill", level));
            LaserUpgrade = new LaserUpgradeModule(
                Wallet,
                laserSettings,
                SaveData.laserLevel,
                level => SaveMiningTechLevel("laser", level));
            ExtractorUpgrade = new ExtractorUpgradeModule(
                Wallet,
                extractorSettings,
                SaveData.extractorLevel,
                level => SaveMiningTechLevel("extractor", level));
        }

        public void SetCurrentHealth(float health)
        {
            GameSaveData data = SaveData;
            data.health = Mathf.Clamp(health, 0f, Mathf.Max(0f, data.maxHealth));
            SaveData = data;
        }

        public void SetCargoWeight(float cargoWeight)
        {
            GameSaveData data = SaveData;
            data.cargoWeight = Mathf.Clamp(cargoWeight, 0f, Mathf.Max(0f, data.maxCargoWeight));
            SaveData = data;
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void InitializeModules()
        {
            Wallet = new WalletModule(SaveData.money);
            Inventory = new InventoryModule(SaveData.inventoryItems);
            Shop = new ShopModule(Wallet, Inventory);

            ShipStatUpgradeSO healthSettings = Resources.Load<ShipStatUpgradeSO>(HealthUpgradePath);
            ShipStatUpgradeSO cargoSettings = Resources.Load<ShipStatUpgradeSO>(CargoUpgradePath);
            ShipStatUpgradeSO speedSettings = Resources.Load<ShipStatUpgradeSO>(SpeedUpgradePath);

            HealthUpgrade = new HealthUpgradeModule(
                Wallet,
                healthSettings,
                SaveData.healthLevel,
                HandleHealthUpgrade);
            CargoUpgrade = new CargoUpgradeModule(
                Wallet,
                cargoSettings,
                SaveData.cargoLevel,
                HandleCargoUpgrade);
            SpeedUpgrade = new SpeedUpgradeModule(
                Wallet,
                speedSettings,
                SaveData.shipSpeedLevel,
                HandleSpeedUpgrade);

            DrillUpgrade = null;
            LaserUpgrade = null;
            ExtractorUpgrade = null;

            SynchronizeUpgradeValues();
        }

        private void SaveMiningTechLevel(string techId, int level)
        {
            SetMiningTechLevel(techId, level);
            Save();
        }

        private void HandleHealthUpgrade(int level, float value)
        {
            GameSaveData data = SaveData;
            float previousMaximum = Mathf.Max(0f, data.maxHealth);
            data.healthLevel = level;
            data.maxHealth = value;
            data.health = Mathf.Clamp(data.health + Mathf.Max(0f, value - previousMaximum), 0f, value);
            SaveData = data;
            Save();
        }

        private void HandleCargoUpgrade(int level, float value)
        {
            GameSaveData data = SaveData;
            data.cargoLevel = level;
            data.maxCargoWeight = value;
            data.cargoWeight = Mathf.Clamp(data.cargoWeight, 0f, value);
            SaveData = data;
            Save();
        }

        private void HandleSpeedUpgrade(int level, float value)
        {
            GameSaveData data = SaveData;
            data.shipSpeedLevel = level;
            data.shipSpeed = value;
            SaveData = data;
            Save();
        }

        private void SynchronizeUpgradeValues()
        {
            GameSaveData data = SaveData;

            if (HealthUpgrade?.Settings != null)
            {
                data.healthLevel = HealthUpgrade.Level;
                data.maxHealth = HealthUpgrade.CurrentValue;
                data.health = Mathf.Clamp(data.health, 0f, data.maxHealth);
            }
            if (CargoUpgrade?.Settings != null)
            {
                data.cargoLevel = CargoUpgrade.Level;
                data.maxCargoWeight = CargoUpgrade.CurrentValue;
                data.cargoWeight = Mathf.Clamp(data.cargoWeight, 0f, data.maxCargoWeight);
            }
            if (SpeedUpgrade?.Settings != null)
            {
                data.shipSpeedLevel = SpeedUpgrade.Level;
                data.shipSpeed = SpeedUpgrade.CurrentValue;
            }

            SaveData = data;
        }

        private static void MigrateSaveData(ref GameSaveData data)
        {
            if (data.saveVersion < 2)
            {
                data.maxHealth = data.maxHealth > 0f ? data.maxHealth : 100f;
                data.health = data.health > 0f ? Mathf.Min(data.health, data.maxHealth) : data.maxHealth;
                data.maxCargoWeight = 20f;
                data.cargoWeight = Mathf.Clamp(data.cargoWeight, 0f, data.maxCargoWeight);
                data.shipSpeed = 10f;
            }

            data.maxFuel = data.maxFuel > 0f ? data.maxFuel : 100f;
            data.fuel = Mathf.Clamp(data.fuel > 0f ? data.fuel : data.maxFuel, 0f, data.maxFuel);
            data.saveVersion = CurrentSaveVersion;
        }
    }
}
