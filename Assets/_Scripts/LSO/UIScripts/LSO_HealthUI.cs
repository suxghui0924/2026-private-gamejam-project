using System.Globalization;
using _Scripts.Suxghui.Manager;
using TMPro;
using UnityEngine;

public class LSO_HealthUI : MonoBehaviour
{
   [SerializeField] private TextMeshProUGUI healthText;
   [SerializeField] private TextMeshProUGUI cargoText;
   [SerializeField] private TextMeshProUGUI moneyText;

   private void InitUI()
   {
       healthText.text = GameManager.Instance.SaveData.fuel.ToString(CultureInfo.InvariantCulture) + "/" +
                         GameManager.Instance.SaveData.maxFuel.ToString(CultureInfo.InvariantCulture);
       
       cargoText.text = GameManager.Instance.SaveData.cargoWeight.ToString(CultureInfo.InvariantCulture) + "/" +
                         GameManager.Instance.SaveData.maxCargoWeight.ToString(CultureInfo.InvariantCulture);
       
       moneyText.text = GameManager.Instance.SaveData.money.ToString(CultureInfo.InvariantCulture);
   }

   private void OnValidate()
   {
       InitUI();
   }
}
