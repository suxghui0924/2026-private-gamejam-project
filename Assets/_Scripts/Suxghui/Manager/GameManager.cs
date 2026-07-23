using System;
using System.IO;
using _Scripts.Suxghui.CoreLib;
using _Scripts.Suxghui.Manager.Module;
using UnityEngine;

namespace _Scripts.Suxghui.Manager
{
    public class GameManager : MonoSingleton<GameManager>
    {
        private const int CurrentSaveVersion = 1;
        private const string SaveFileName = "save.json";

        public GameSaveData SaveData { get; private set; }
        public WalletModule Wallet { get; private set; }
        public InventoryModule Inventory { get; private set; }
        public ShopModule Shop { get; private set; }
        public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
                return;

            DontDestroyOnLoad(gameObject);
            Load();
            InitializeModules();
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
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                GameSaveData loadedData = JsonUtility.FromJson<GameSaveData>(json);

                if (loadedData.saveVersion <= 0)
                    loadedData.saveVersion = CurrentSaveVersion;

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

        private void OnApplicationQuit()
        {
            Save();
        }

        private void InitializeModules()
        {
            Wallet = new WalletModule(SaveData.money);
            Inventory = new InventoryModule(SaveData.inventoryItems);
            Shop = new ShopModule(Wallet, Inventory);
        }
    }
}
