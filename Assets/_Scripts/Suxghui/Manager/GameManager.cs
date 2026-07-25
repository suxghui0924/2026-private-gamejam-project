using System;
using System.Collections;
using System.IO;
using _Scripts.Suxghui.CoreLib;
using _Scripts.Suxghui.Manager.Module;
using _Scripts.Suxghui.Manager.Upgrade;
using _Scripts.Suxghui.Mining;
using _Scripts.Suxghui.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Scripts.Suxghui.Manager
{
    public class GameManager : MonoSingleton<GameManager>
    {
        public enum SceneType
        {
            LoadingScene,
            MainMenu,
            ModuleSelect,
            Upgrade,
            StarField,
            Ending
        }

        private sealed class EmptySceneState : ISceneState
        {
            public void Enter() { }
            public void Executor() { }
            public void Exit() { }
        }

        private const int CurrentSaveVersion = 4;
        private const string SaveFileName = "save.json";
        private const string LoadingSceneName = "LoadingScene";
        private const string MainMenuSceneName = "LSO_MainMenu";
        private const string ModuleSelectSceneName = "LSO_ModuleSelect";
        private const string UpgradeSceneName = "LSO_Upgrade";
        private const string StarFieldSceneName = "StarField";
        private const string EndingSceneName = "EndingScene";
        private const string FuelUpgradePath = "Suxghui/Upgrades/FuelUpgrade";
        private const string CargoUpgradePath = "Suxghui/Upgrades/CargoUpgrade";
        private const string SpeedUpgradePath = "Suxghui/Upgrades/SpeedUpgrade";
        private const string DrillTechPath = "Suxghui/Mining/Drill Tech";
        private const string LaserTechPath = "Suxghui/Mining/Laser Tech";
        private const string ExtractorTechPath = "Suxghui/Mining/Extractor Tech";
        private const string EndingSequencePath = "Suxghui/Ending/EndingSequence";
        private const float RescueMoneyLossRatio = 0.12f;
        private const float RescueMineralLossRatio = 0.20f;
        private const int MaximumRescueMoneyLoss = 5000;

        public GameSaveData SaveData { get; private set; }
        public WalletModule Wallet { get; private set; }
        public InventoryModule Inventory { get; private set; }
        public ShopModule Shop { get; private set; }
        public MiningTechSelectionModule TechSelection { get; private set; }
        public FuelUpgradeModule FuelUpgrade { get; private set; }
        public CargoUpgradeModule CargoUpgrade { get; private set; }
        public SpeedUpgradeModule SpeedUpgrade { get; private set; }
        public DrillUpgradeModule DrillUpgrade { get; private set; }
        public LaserUpgradeModule LaserUpgrade { get; private set; }
        public ExtractorUpgradeModule ExtractorUpgrade { get; private set; }
        public ISceneState LoadingSceneState { get; private set; }
        public ISceneState MainMenuState { get; private set; }
        public ISceneState ModuleSelectState { get; private set; }
        public ISceneState UpgradeState { get; private set; }
        public ISceneState StarFieldState { get; private set; }
        public ISceneState EndingState { get; private set; }
        public ISceneState CurrentSceneState { get; private set; }
        public SceneType? CurrentSceneType { get; private set; }
        public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        public event Action<float, float> FuelChanged;
        public EndingSequenceSO EndingSequence { get; private set; }

        private int _sceneStateSceneHandle = -1;
        private bool _sceneStateInitialized;
        private bool _isSwitchingSceneState;
        private bool _isQuitting;
        private bool _endingTransitionRequested;
        private bool _fuelRescueRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            _ = Instance;
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
                return;

            DontDestroyOnLoad(gameObject);
            InitializeSceneStates();
            Load();
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            ActivateSceneState(SceneManager.GetActiveScene());
        }

        private void Start()
        {
            // Also evaluate an existing save. MoneyChanged only fires when the
            // wallet changes, so an already-completed coin goal needs this check.
            if (Wallet != null)
                HandleWalletMoneyChanged(Wallet.Money);
            if (!_endingTransitionRequested && SaveData.fuel <= 0f)
                HandleFuelDepleted();
        }

        private void Update()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!_sceneStateInitialized || activeScene.handle != _sceneStateSceneHandle)
                ActivateSceneState(activeScene);

            if (!IsStateAlive(CurrentSceneState))
                return;

            try
            {
                CurrentSceneState.Executor();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
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

        public float SetFuel(float fuel)
        {
            GameSaveData data = SaveData;
            float previousFuel = data.fuel;
            data.fuel = Mathf.Clamp(fuel, 0f, Mathf.Max(0f, data.maxFuel));
            SaveData = data;
            FuelChanged?.Invoke(data.fuel, data.maxFuel);

            if (previousFuel > 0f && data.fuel <= 0f)
                HandleFuelDepleted();
            return data.fuel;
        }

        public float ConsumeFuel(float amount)
        {
            if (amount <= 0f)
                return 0f;

            float previous = SaveData.fuel;
            SetFuel(previous - amount);
            return previous - SaveData.fuel;
        }

        public float RestoreFuel(float amount)
        {
            if (amount <= 0f)
                return 0f;

            float previous = SaveData.fuel;
            SetFuel(previous + amount);
            return SaveData.fuel - previous;
        }

        public void SetCargoWeight(float cargoWeight)
        {
            GameSaveData data = SaveData;
            data.cargoWeight = Mathf.Clamp(cargoWeight, 0f, Mathf.Max(0f, data.maxCargoWeight));
            SaveData = data;
        }

        public ISceneState GetSceneState(SceneType sceneType)
        {
            return sceneType switch
            {
                SceneType.LoadingScene => LoadingSceneState,
                SceneType.MainMenu => MainMenuState,
                SceneType.ModuleSelect => ModuleSelectState,
                SceneType.Upgrade => UpgradeState,
                SceneType.StarField => StarFieldState,
                SceneType.Ending => EndingState,
                _ => throw new ArgumentOutOfRangeException(nameof(sceneType), sceneType, null)
            };
        }

        public void ChangeSceneState(ISceneState nextState)
        {
            if (!TryGetRegisteredSceneType(nextState, out SceneType targetScene))
            {
                Debug.LogError("GameManager에 등록된 씬 상태만 ChangeSceneState에 전달할 수 있습니다.", this);
                return;
            }

            if (targetScene == SceneType.LoadingScene)
            {
                Debug.LogWarning("LoadingScene은 목적지가 아니라 다른 씬으로 이동할 때 자동으로 사용됩니다.", this);
                return;
            }

            string targetSceneName = GetUnitySceneName(targetScene);
            if (SceneManager.GetActiveScene().name == targetSceneName)
                return;

            if (targetScene == SceneType.ModuleSelect)
            {
                SceneManager.LoadScene(targetSceneName);
                return;
            }

            global::LoadingSceneController.LoadScene(targetSceneName);
        }

        public void ReloadSceneState(ISceneState state)
        {
            if (!TryGetRegisteredSceneType(state, out SceneType targetScene) ||
                targetScene == SceneType.LoadingScene)
            {
                Debug.LogError("ReloadSceneState requires a registered destination scene state.", this);
                return;
            }

            global::LoadingSceneController.LoadScene(GetUnitySceneName(targetScene));
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            ActivateSceneState(nextScene);
        }

        private void ActivateSceneState(Scene scene)
        {
            if (_isSwitchingSceneState || !scene.IsValid() || !scene.isLoaded)
                return;
            if (_sceneStateInitialized && _sceneStateSceneHandle == scene.handle)
                return;

            _isSwitchingSceneState = true;
            try
            {
                ISceneState nextState = FindSceneState(scene.name, out SceneType? sceneType);
                ApplySceneState(nextState, sceneType);
                _sceneStateSceneHandle = scene.handle;
                _sceneStateInitialized = true;
                if (sceneType == SceneType.StarField && SaveData.fuel > 0f)
                    _fuelRescueRequested = false;
            }
            finally
            {
                _isSwitchingSceneState = false;
            }
        }

        private void ApplySceneState(ISceneState nextState, SceneType? sceneType)
        {
            if (IsStateAlive(CurrentSceneState))
            {
                try
                {
                    CurrentSceneState.Exit();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }

            // Clear the reference before Enter. Even when Enter changes scenes, the previous
            // state's Executor can no longer be called by GameManager.Update.
            CurrentSceneState = null;
            CurrentSceneState = nextState;
            CurrentSceneType = sceneType;
            if (!IsStateAlive(CurrentSceneState))
                return;

            try
            {
                CurrentSceneState.Enter();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private ISceneState FindSceneState(string unitySceneName, out SceneType? sceneType)
        {
            switch (unitySceneName)
            {
                case LoadingSceneName:
                    sceneType = SceneType.LoadingScene;
                    return LoadingSceneState;
                case MainMenuSceneName:
                    sceneType = SceneType.MainMenu;
                    return MainMenuState;
                case ModuleSelectSceneName:
                    sceneType = SceneType.ModuleSelect;
                    return ModuleSelectState;
                case UpgradeSceneName:
                    sceneType = SceneType.Upgrade;
                    return UpgradeState;
                case StarFieldSceneName:
                case "LSO_StarField":
                    sceneType = SceneType.StarField;
                    return StarFieldState;
                case EndingSceneName:
                    sceneType = SceneType.Ending;
                    return EndingState;
                default:
                    sceneType = null;
                    return new EmptySceneState();
            }
        }

        private void InitializeSceneStates()
        {
            LoadingSceneState = new LoadingSceneState(this);
            MainMenuState = new MainMenuState(this);
            ModuleSelectState = new ModuleSelectState(this);
            UpgradeState = new UpgradeState(this);
            StarFieldState = new StarFieldState(this);
            EndingState = new EndingSceneState(this);
        }

        private bool TryGetRegisteredSceneType(ISceneState state, out SceneType sceneType)
        {
            if (ReferenceEquals(state, LoadingSceneState))
                sceneType = SceneType.LoadingScene;
            else if (ReferenceEquals(state, MainMenuState))
                sceneType = SceneType.MainMenu;
            else if (ReferenceEquals(state, ModuleSelectState))
                sceneType = SceneType.ModuleSelect;
            else if (ReferenceEquals(state, UpgradeState))
                sceneType = SceneType.Upgrade;
            else if (ReferenceEquals(state, StarFieldState))
                sceneType = SceneType.StarField;
            else if (ReferenceEquals(state, EndingState))
                sceneType = SceneType.Ending;
            else
            {
                sceneType = default;
                return false;
            }

            return true;
        }

        private static string GetUnitySceneName(SceneType sceneType)
        {
            return sceneType switch
            {
                SceneType.LoadingScene => LoadingSceneName,
                SceneType.MainMenu => MainMenuSceneName,
                SceneType.ModuleSelect => ModuleSelectSceneName,
                SceneType.Upgrade => UpgradeSceneName,
                SceneType.StarField => StarFieldSceneName,
                SceneType.Ending => EndingSceneName,
                _ => throw new ArgumentOutOfRangeException(nameof(sceneType), sceneType, null)
            };
        }

        private static bool IsStateAlive(ISceneState state)
        {
            if (state == null)
                return false;
            return !(state is UnityEngine.Object unityObject) || unityObject != null;
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
            if (IsStateAlive(CurrentSceneState))
                CurrentSceneState.Exit();
            CurrentSceneState = null;
            CurrentSceneType = null;
            Save();
        }

        protected override void OnDestroy()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            if (Wallet != null)
                Wallet.MoneyChanged -= HandleWalletMoneyChanged;
            if (!_isQuitting && IsStateAlive(CurrentSceneState))
                CurrentSceneState.Exit();
            CurrentSceneState = null;
            CurrentSceneType = null;
            base.OnDestroy();
        }

        private void InitializeModules()
        {
            if (Wallet != null)
                Wallet.MoneyChanged -= HandleWalletMoneyChanged;

            EndingSequence = Resources.Load<EndingSequenceSO>(EndingSequencePath);
            _endingTransitionRequested = false;
            _fuelRescueRequested = false;
            Wallet = new WalletModule(SaveData.money);
            Wallet.MoneyChanged += HandleWalletMoneyChanged;
            Inventory = new InventoryModule(SaveData.inventoryItems);
            Shop = new ShopModule(Wallet, Inventory);
            TechSelection = new MiningTechSelectionModule(
                SaveData.selectedMiningTool,
                string.IsNullOrWhiteSpace(SaveData.currentMiningToolId)
                    ? SaveData.currentTechId
                    : SaveData.currentMiningToolId,
                HandleMiningTechSelection);

            ShipStatUpgradeSO fuelSettings = Resources.Load<ShipStatUpgradeSO>(FuelUpgradePath);
            ShipStatUpgradeSO cargoSettings = Resources.Load<ShipStatUpgradeSO>(CargoUpgradePath);
            ShipStatUpgradeSO speedSettings = Resources.Load<ShipStatUpgradeSO>(SpeedUpgradePath);

            FuelUpgrade = new FuelUpgradeModule(
                Wallet,
                fuelSettings,
                SaveData.fuelLevel,
                HandleFuelUpgrade);
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

            ConfigureMiningTechUpgrades(
                Resources.Load<MiningTechDefinitionSO>(DrillTechPath),
                Resources.Load<MiningTechDefinitionSO>(LaserTechPath),
                Resources.Load<MiningTechDefinitionSO>(ExtractorTechPath));

            SynchronizeUpgradeValues();
        }

        private void HandleWalletMoneyChanged(int currentMoney)
        {
            int requiredCoins = EndingSequence != null
                ? EndingSequence.RequiredCoins
                : 100000;

            if (_endingTransitionRequested || SaveData.endingReached || currentMoney < requiredCoins)
                return;

            _endingTransitionRequested = true;
            GameSaveData data = SaveData;
            data.endingReached = true;
            SaveData = data;
            Save();
            ChangeSceneState(EndingState);
        }

        private void HandleFuelDepleted()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            bool isStarField = string.Equals(sceneName, StarFieldSceneName, StringComparison.Ordinal) ||
                               string.Equals(sceneName, "LSO_StarField", StringComparison.Ordinal);
            if (_fuelRescueRequested || _endingTransitionRequested || !isStarField)
                return;

            _fuelRescueRequested = true;

            int currentMoney = Wallet?.Money ?? 0;
            int moneyLoss = Mathf.Min(
                currentMoney,
                Mathf.Min(MaximumRescueMoneyLoss,
                    Mathf.CeilToInt(currentMoney * RescueMoneyLossRatio)));
            if (moneyLoss > 0)
                Wallet.TrySpendMoney(moneyLoss);

            int mineralLoss = 0;
            int remainingCargo = 0;
            if (Inventory != null)
            {
                InventoryItemSaveData[] items = Inventory.ToSaveData();
                int totalCargo = 0;
                for (int i = 0; i < items.Length; i++)
                {
                    InventoryItemSaveData item = items[i];
                    if (item.itemId && item.amount > 0)
                        totalCargo += item.amount;
                }

                int targetLoss = Mathf.CeilToInt(totalCargo * RescueMineralLossRatio);
                int[] plannedLosses = new int[items.Length];
                int lossStillNeeded = targetLoss;

                for (int i = 0; i < items.Length && lossStillNeeded > 0; i++)
                {
                    if (!items[i].itemId || items[i].amount <= 0)
                        continue;
                    int proportionalLoss = Mathf.Min(
                        items[i].amount,
                        Mathf.FloorToInt(items[i].amount * RescueMineralLossRatio));
                    plannedLosses[i] = proportionalLoss;
                    lossStillNeeded -= proportionalLoss;
                }

                while (lossStillNeeded > 0)
                {
                    bool assignedAny = false;
                    for (int i = 0; i < items.Length && lossStillNeeded > 0; i++)
                    {
                        if (!items[i].itemId || plannedLosses[i] >= items[i].amount)
                            continue;
                        plannedLosses[i]++;
                        lossStillNeeded--;
                        assignedAny = true;
                    }

                    if (!assignedAny)
                        break;
                }

                for (int i = 0; i < items.Length; i++)
                {
                    int loss = plannedLosses[i];
                    if (loss > 0 && Inventory.TryRemoveItem(items[i].itemId, loss))
                    {
                        mineralLoss += loss;
                    }
                }

                remainingCargo = Mathf.Max(0, totalCargo - mineralLoss);
            }

            SetCargoWeight(remainingCargo);
            RestoreFuel(SaveData.maxFuel);
            Save();

            Debug.Log(
                $"[Fuel Rescue] Fuel restored. Cost: {moneyLoss} coins, {mineralLoss}kg minerals.",
                this);
            StartCoroutine(ReloadStarFieldAfterFuelRescue());
        }

        private IEnumerator ReloadStarFieldAfterFuelRescue()
        {
            // Let the movement/collision callback that consumed the final fuel
            // finish before unloading its scene objects.
            yield return new WaitForEndOfFrame();
            ReloadSceneState(StarFieldState);
        }

        private void SaveMiningTechLevel(string techId, int level)
        {
            SetMiningTechLevel(techId, level);
            Save();
        }

        private void HandleMiningTechSelection(MiningTechType type, string techId)
        {
            SetSelectedMiningTech((int)type, techId);
            Save();
        }

        private void HandleFuelUpgrade(int level, float value)
        {
            GameSaveData data = SaveData;
            float previousMaximum = Mathf.Max(0f, data.maxFuel);
            data.fuelLevel = level;
            data.maxFuel = Mathf.Max(1f, value);
            data.fuel = Mathf.Clamp(
                data.fuel + Mathf.Max(0f, data.maxFuel - previousMaximum),
                0f,
                data.maxFuel);
            SaveData = data;
            FuelChanged?.Invoke(data.fuel, data.maxFuel);
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

            if (FuelUpgrade?.Settings != null)
            {
                data.fuelLevel = FuelUpgrade.Level;
                data.maxFuel = Mathf.Max(1f, FuelUpgrade.CurrentValue);
                data.fuel = Mathf.Clamp(data.fuel, 0f, data.maxFuel);
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
                data.maxCargoWeight = 20f;
                data.cargoWeight = Mathf.Clamp(data.cargoWeight, 0f, data.maxCargoWeight);
                data.shipSpeed = 10f;
            }

            if (data.saveVersion < 3 && data.fuelLevel == 0 &&
                data.maxFuel > 0f && data.maxFuel < 120f)
            {
                float fuelRatio = Mathf.Clamp01(data.fuel / data.maxFuel);
                data.maxFuel = 120f;
                data.fuel = data.maxFuel * fuelRatio;
            }

            if (data.maxFuel <= 0f)
            {
                data.maxFuel = 120f;
                data.fuel = data.maxFuel;
            }
            else
            {
                data.fuel = Mathf.Clamp(data.fuel, 0f, data.maxFuel);
            }
            data.saveVersion = CurrentSaveVersion;
        }
    }
}
