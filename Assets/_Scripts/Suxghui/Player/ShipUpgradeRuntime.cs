using _Scripts.LSO;
using _Scripts.Suxghui.Agent.Component;
using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Player.Agent;
using UnityEngine;

namespace _Scripts.Suxghui.Player
{
    public sealed class ShipUpgradeRuntime : MonoBehaviour
    {
        [SerializeField] private HeatlhComponent healthComponent;
        [SerializeField] private MovmentComponent movementComponent;
        [SerializeField] private LSO_Weight cargoWeightComponent;

        private GameManager _gameManager;
        private bool _subscribed;

        private void Start()
        {
            CacheReferences();
            _gameManager = GameManager.Instance;
            Subscribe();
            ApplyAllStats();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void CacheReferences()
        {
            Transform root = transform.root;
            if (healthComponent == null)
                healthComponent = root.GetComponentInChildren<HeatlhComponent>(true);
            if (movementComponent == null)
                movementComponent = root.GetComponentInChildren<MovmentComponent>(true);
            if (cargoWeightComponent == null)
                cargoWeightComponent = FindFirstObjectByType<LSO_Weight>();
        }

        private void Subscribe()
        {
            if (_subscribed || _gameManager == null)
                return;

            if (_gameManager.HealthUpgrade != null)
                _gameManager.HealthUpgrade.Upgraded += HandleHealthUpgraded;
            if (_gameManager.CargoUpgrade != null)
                _gameManager.CargoUpgrade.Upgraded += HandleCargoUpgraded;
            if (_gameManager.SpeedUpgrade != null)
                _gameManager.SpeedUpgrade.Upgraded += HandleSpeedUpgraded;
            if (healthComponent != null)
                healthComponent.health.OnValueChanged += HandleHealthChanged;
            if (cargoWeightComponent != null)
                cargoWeightComponent.OnWeightChanged += HandleCargoWeightChanged;

            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            if (_gameManager?.HealthUpgrade != null)
                _gameManager.HealthUpgrade.Upgraded -= HandleHealthUpgraded;
            if (_gameManager?.CargoUpgrade != null)
                _gameManager.CargoUpgrade.Upgraded -= HandleCargoUpgraded;
            if (_gameManager?.SpeedUpgrade != null)
                _gameManager.SpeedUpgrade.Upgraded -= HandleSpeedUpgraded;
            if (healthComponent != null)
                healthComponent.health.OnValueChanged -= HandleHealthChanged;
            if (cargoWeightComponent != null)
                cargoWeightComponent.OnWeightChanged -= HandleCargoWeightChanged;

            _subscribed = false;
        }

        private void ApplyAllStats()
        {
            ApplyHealth();
            ApplyCargo();
            ApplySpeed();
        }

        private void ApplyHealth()
        {
            if (healthComponent == null || _gameManager?.HealthUpgrade == null)
                return;

            int maximum = Mathf.Max(1, Mathf.RoundToInt(_gameManager.HealthUpgrade.CurrentValue));
            int current = Mathf.Clamp(Mathf.RoundToInt(_gameManager.SaveData.health), 0, maximum);
            healthComponent.SetHealthState(current, maximum);
        }

        private void ApplyCargo()
        {
            if (cargoWeightComponent == null || _gameManager?.CargoUpgrade == null)
                return;

            int maximum = Mathf.Max(1, Mathf.RoundToInt(_gameManager.CargoUpgrade.CurrentValue));
            int current = Mathf.Clamp(Mathf.RoundToInt(_gameManager.SaveData.cargoWeight), 0, maximum);
            cargoWeightComponent.SetCapacity(maximum, current);
        }

        private void ApplySpeed()
        {
            if (movementComponent == null || _gameManager?.SpeedUpgrade == null)
                return;

            movementComponent.SetBaseMoveSpeed(_gameManager.SpeedUpgrade.CurrentValue);
        }

        private void HandleHealthUpgraded(int level, float value)
        {
            ApplyHealth();
        }

        private void HandleCargoUpgraded(int level, float value)
        {
            ApplyCargo();
        }

        private void HandleSpeedUpgraded(int level, float value)
        {
            ApplySpeed();
        }

        private void HandleHealthChanged(int previous, int current)
        {
            _gameManager?.SetCurrentHealth(current);
        }

        private void HandleCargoWeightChanged(int current, int maximum)
        {
            _gameManager?.SetCargoWeight(current);
        }
    }
}
