using UnityEngine;

public class LSO_MoneyManager : MonoBehaviour
{
   public static LSO_MoneyManager Instance;
   public int Current {get; private set;}

   private void Awake()
   {
      if (Instance == null)
      {
         Instance = this;
      }
   }

   public void AddMoney(int amount)
   {
      if (amount <= 0) return;
      Current +=  amount;
   }

   public bool UseMoney(int amount)
   {
      if (amount <= 0 || amount > Current ) return false;
      Current -= amount;
      return true;
   }
}
