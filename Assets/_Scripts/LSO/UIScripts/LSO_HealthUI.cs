using System.Globalization;
using _Scripts.LSO;
using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class LSO_HealthUI : MonoBehaviour
{
    [Header("Throttle")]
    [SerializeField] private Slider throttleSlider;
    [SerializeField] private SpaceShipAgent spaceShip;
    [FormerlySerializedAs("maximumThrottlePercent")]
    [SerializeField, Min(0.01f)] private float maximumThrottleValue = 1.5f;

    [Header("Status Text")]
    [FormerlySerializedAs("healthText")]
    [SerializeField] private TextMeshProUGUI fuelText;
    [SerializeField] private TextMeshProUGUI cargoText;
    [SerializeField] private TextMeshProUGUI moneyText;

    private LSO_Weight _cargoWeight;

    private void Awake()
    {
        ConfigureSlider();
        CacheRuntimeReferences();
    }

    private void OnEnable()
    {
        ConfigureSlider();
        if (!Application.isPlaying)
            return;

        CacheRuntimeReferences();
        RefreshUI();
    }

    private void Update()
    {
        if (spaceShip == null || _cargoWeight == null)
            CacheRuntimeReferences();

        RefreshUI();
    }

    private void ConfigureSlider()
    {
        if (throttleSlider == null)
            return;

        throttleSlider.minValue = 0f;
        throttleSlider.maxValue = maximumThrottleValue;
        throttleSlider.wholeNumbers = false;
        throttleSlider.interactable = false;
    }

    private void CacheRuntimeReferences()
    {
        if (throttleSlider == null)
            throttleSlider = GetComponentInChildren<Slider>(true);
        if (spaceShip == null)
            spaceShip = FindFirstObjectByType<SpaceShipAgent>();
        if (_cargoWeight == null)
            _cargoWeight = LSO_Weight.Instance ?? FindFirstObjectByType<LSO_Weight>();
    }

    private void RefreshUI()
    {
        if (throttleSlider != null)
        {
            float throttle = spaceShip != null ? spaceShip.ThrottleAmount : 0f;
            throttleSlider.SetValueWithoutNotify(
                Mathf.Clamp(throttle, 0f, maximumThrottleValue));
        }

        GameManager manager = GameManager.Instance;
        GameSaveData data = manager.SaveData;

        if (fuelText != null)
            fuelText.text = $"{FormatNumber(data.fuel)}/{FormatNumber(data.maxFuel)}";

        if (cargoText != null)
        {
            float currentWeight = _cargoWeight != null ? _cargoWeight.Weight : data.cargoWeight;
            float maximumWeight = _cargoWeight != null ? _cargoWeight.MaxWeight : data.maxCargoWeight;
            cargoText.text = $"{FormatNumber(currentWeight)}/{FormatNumber(maximumWeight)}";
        }

        if (moneyText != null)
        {
            int currentMoney = manager.Wallet != null ? manager.Wallet.Money : data.money;
            moneyText.text = currentMoney.ToString(CultureInfo.InvariantCulture);
        }
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maximumThrottleValue = Mathf.Max(0.01f, maximumThrottleValue);
        ConfigureSlider();
    }
#endif
}
