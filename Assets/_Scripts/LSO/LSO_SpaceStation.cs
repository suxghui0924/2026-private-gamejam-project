using _Scripts.Suxghui.Player;
using Unity.Cinemachine;
using UnityEngine;

public class LSO_SpaceStation : MonoBehaviour
{
    public GameObject output;
    public GameObject input;
    public GameObject healSpot;

    [SerializeField] private CinemachineCamera innerCam;
    [SerializeField] private Canvas upgradeUI;
    [SerializeField] private Canvas gameUI;

    private bool _isDocked;

    public void ToInnerCam(GameObject plane)
    {
        if (_isDocked) return;                       // 중복 진입 차단

        if (!plane.TryGetComponent(out SpaceShipAgent spaceAgent))
        {
            Debug.LogWarning("우주선에 헬스 컴포넌트가 없음");
            return;
        }

        _isDocked = true;
        innerCam.Priority = 2;
        spaceAgent.HealthComponent.currentHeartbeat = false;

        SetUI(false);
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

        innerCam.Priority = 0;                      // 원래 카메라로 복귀
        SetUI(true);
        _isDocked = false;
    }

    public void Heal(GameObject plane)
    {
        if (plane.TryGetComponent(out SpaceShipAgent spaceAgent))
            spaceAgent.HealthComponent.HealDamage(spaceAgent.HealthComponent.MAXHEALTH);
    }

    private void SetUI(bool isGame)
    {
        gameUI.gameObject.SetActive(isGame);
        upgradeUI.gameObject.SetActive(!isGame);
    }
}