using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Player;
using UnityEngine;

public class LSO_SpaceStation : MonoBehaviour
{
    public GameObject output;
    public GameObject input;
    public GameObject healSpot;
    

    private bool _isDocked;

    public void ToInnerCam(GameObject plane)
    {
        if (_isDocked) return;                       // 중복 진입 차단

        if (!plane.TryGetComponent(out SpaceShipAgent spaceAgent))
        {
            Debug.LogWarning("우주선에 헬스 컴포넌트가 없음");
            return;
        } 
        Refuel();

        _isDocked = true;
        spaceAgent.HealthComponent.currentHeartbeat = false;

        GameManager manager = GameManager.Instance;
        manager.ChangeSceneState(manager.UpgradeState);
    }

    public void ToGameUI(GameObject plane)
    {
        plane.transform.SetPositionAndRotation(
            output.transform.position, output.transform.rotation);

        // 관성 제거 — 안 하면 이전 속도로 튕겨나감
        if (plane.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        _isDocked = false;
    }

    public void Refuel()
    {
        GameManager manager = GameManager.Instance;
        manager.RestoreFuel(manager.SaveData.maxFuel);
        manager.Save();
    }
}
