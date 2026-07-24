using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Player;
using Unity.Cinemachine;
using UnityEngine;

public class LSO_SpaceStation : MonoBehaviour
{
   public GameObject output;
   public GameObject input;
   public GameObject healSpot;
   
   [SerializeField] private CinemachineCamera innerCam;
   
   private void ToInnerCam(GameObject plane)
   {
      innerCam.Priority = 2;
      if (plane.TryGetComponent(out SpaceShipAgent spaceAgent))
      {
         spaceAgent.HealthComponent.currentHeartbeat = false;
      }
      else
      {
         Debug.LogWarning("우주선에 헬스 컴포넌트가 없음");
      }
   }
   
   

   private void Heal(GameObject plane)
   {
      if (plane.TryGetComponent(out SpaceShipAgent spaceAgent))
      {
         //풀회복
         spaceAgent.HealthComponent.HealDamage(spaceAgent.HealthComponent.MAXHEALTH);
      }
   }
}
