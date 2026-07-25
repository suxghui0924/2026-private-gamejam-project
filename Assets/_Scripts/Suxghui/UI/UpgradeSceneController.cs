using System;
using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Manager.Module;
using _Scripts.Suxghui.Mining;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _Scripts.Suxghui.UI
{
    public sealed class UpgradeSceneController : MonoBehaviour
    {
        // Runtime relay keeps the upgrade panel clickable even when its art is an Image-only hierarchy.
        private GameManager _manager;
        private TMP_Text _moneyText;
        private TMP_Text _techNameText;
        private TMP_Text _techLevelText;
        private TMP_Text _techCostText;
        private TechUpgradeClickRelay[] _techRelays = Array.Empty<TechUpgradeClickRelay>();
        private Color _normalTechCostColor = Color.white;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, "LSO_Upgrade", StringComparison.Ordinal))
                return;

            if (FindFirstObjectByType<UpgradeSceneController>() != null)
                return;

            GameObject host = new GameObject(nameof(UpgradeSceneController));
            host.AddComponent<UpgradeSceneController>();
            SceneManager.MoveGameObjectToScene(host, scene);
        }

        private void Start()
        {
            _manager = GameManager.Instance;
            Transform window = FindUpgradeWindow();
            if (_manager == null || window == null)
            {
                Debug.LogError("[UpgradeSceneController] 업그레이드 UI 또는 GameManager를 찾지 못했습니다.", this);
                return;
            }

            BindFixedTechDisplay(window);
            BindTechUpgradeButtons(window);

            _moneyText = FindText(window, "Coin/Text (TMP)");
            _techLevelText = FindText(window, "Upgrade/Button/Text (TMP)");
            _techCostText = FindText(window, "Upgrade/Button/UpgradeCost/Text (TMP)");
            if (_techCostText != null)
                _normalTechCostColor = _techCostText.color;

            Subscribe();
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (_manager?.Wallet != null)
                _manager.Wallet.MoneyChanged -= HandleMoneyChanged;
            if (_manager?.TechSelection != null)
                _manager.TechSelection.SelectionChanged -= HandleSelectionChanged;
            if (_manager?.DrillUpgrade != null)
                _manager.DrillUpgrade.Upgraded -= HandleTechUpgraded;
            if (_manager?.LaserUpgrade != null)
                _manager.LaserUpgrade.Upgraded -= HandleTechUpgraded;
            if (_manager?.ExtractorUpgrade != null)
                _manager.ExtractorUpgrade.Upgraded -= HandleTechUpgraded;

            for (int i = 0; i < _techRelays.Length; i++)
                if (_techRelays[i] != null)
                    _techRelays[i].Bind(null);
        }

        private void BindFixedTechDisplay(Transform window)
        {
            Transform display = window.Find("Module/Dropdown");
            if (display == null)
                return;

            TMP_Dropdown dropdown = display.GetComponent<TMP_Dropdown>();
            if (dropdown != null)
            {
                dropdown.Hide();
                dropdown.interactable = false;
                dropdown.enabled = false;
            }

            Graphic background = display.GetComponent<Graphic>();
            if (background != null)
                background.raycastTarget = false;

            Transform arrow = display.Find("Arrow");
            if (arrow != null)
                arrow.gameObject.SetActive(false);
            Transform template = display.Find("Template");
            if (template != null)
                template.gameObject.SetActive(false);

            _techNameText = FindText(window, "Module/Dropdown/Label");
        }

        private void BindTechUpgradeButtons(Transform window)
        {
            Transform primaryRoot = window.Find("Upgrade/Button");
            Transform overlayRoot = window.Find("Upgrade/Button (1)");
            _techRelays = new[]
            {
                EnsureRelay(primaryRoot),
                EnsureRelay(overlayRoot)
            };

            for (int i = 0; i < _techRelays.Length; i++)
                if (_techRelays[i] != null)
                    _techRelays[i].Bind(UpgradeCurrentTech);
        }

        private static TechUpgradeClickRelay EnsureRelay(Transform target)
        {
            if (target == null)
                return null;

            Graphic graphic = target.GetComponent<Graphic>();
            if (graphic != null)
                graphic.raycastTarget = true;

            TechUpgradeClickRelay relay = target.GetComponent<TechUpgradeClickRelay>();
            return relay != null ? relay : target.gameObject.AddComponent<TechUpgradeClickRelay>();
        }

        private void Subscribe()
        {
            _manager.Wallet.MoneyChanged += HandleMoneyChanged;
            _manager.TechSelection.SelectionChanged += HandleSelectionChanged;
            if (_manager.DrillUpgrade != null)
                _manager.DrillUpgrade.Upgraded += HandleTechUpgraded;
            if (_manager.LaserUpgrade != null)
                _manager.LaserUpgrade.Upgraded += HandleTechUpgraded;
            if (_manager.ExtractorUpgrade != null)
                _manager.ExtractorUpgrade.Upgraded += HandleTechUpgraded;
        }

        private void UpgradeCurrentTech()
        {
            MiningTechUpgradeModule module = GetCurrentTechModule();
            if (module == null)
            {
                Debug.LogWarning("[Upgrade] Current mining tech module is not configured.", this);
                return;
            }

            bool upgraded = module.TryUpgrade();
            Debug.Log(upgraded
                ? $"[Upgrade] {module.Settings.DisplayName} upgraded to level {module.Level}."
                : $"[Upgrade] Cannot upgrade {module.Settings.DisplayName}: insufficient coins or max level.", this);
            RefreshAll();
        }

        private MiningTechUpgradeModule GetCurrentTechModule()
        {
            return _manager.TechSelection.CurrentType switch
            {
                MiningTechType.Drill => _manager.DrillUpgrade,
                MiningTechType.Laser => _manager.LaserUpgrade,
                MiningTechType.Extractor => _manager.ExtractorUpgrade,
                _ => null
            };
        }

        private void RefreshAll()
        {
            MiningTechUpgradeModule module = GetCurrentTechModule();

            if (_moneyText != null)
                _moneyText.text = _manager.Wallet.Money.ToString();
            if (_techNameText != null)
                _techNameText.text = module?.Settings != null
                    ? module.Settings.DisplayName
                    : _manager.TechSelection.CurrentType.ToString();

            bool canUpgrade = module?.CanUpgrade == true;
            bool canAfford = canUpgrade && _manager.Wallet.Money >= module.NextUpgradeCost;
            if (_techLevelText != null)
                _techLevelText.text = module == null
                    ? "-"
                    : module.Level >= module.MaxLevel ? "MAX" : $"Level {module.Level}";
            if (_techCostText != null)
            {
                _techCostText.text = canUpgrade ? module.NextUpgradeCost.ToString() : string.Empty;
                _techCostText.color = canUpgrade && !canAfford
                    ? new Color(1f, 0.3f, 0.3f, 1f)
                    : _normalTechCostColor;
            }

            for (int i = 0; i < _techRelays.Length; i++)
                if (_techRelays[i] != null)
                    _techRelays[i].SetInteractable(canUpgrade && canAfford);
        }

        private void HandleMoneyChanged(int money) => RefreshAll();
        private void HandleSelectionChanged(MiningTechType type) => RefreshAll();
        private void HandleTechUpgraded(int level, MiningTechStats stats) => RefreshAll();

        private static Transform FindUpgradeWindow()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Transform window = canvases[i].transform.Find("UpgradeWindow");
                if (window != null)
                    return window;
            }
            return null;
        }

        private static TMP_Text FindText(Transform root, string relativePath)
        {
            Transform target = root != null ? root.Find(relativePath) : null;
            return target != null ? target.GetComponent<TMP_Text>() : null;
        }
    }
}
