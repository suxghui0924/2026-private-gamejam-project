using _Scripts.Suxghui.Player;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class LSO_StationTrigger : MonoBehaviour
{
    public enum TriggerAction { EnterStation, Heal }

    [SerializeField] private TriggerAction action;
    [SerializeField] private LSO_SpaceStation station;
    [SerializeField] private string targetTag = "Player";

    // 컴포넌트 추가 시 자동 세팅
    private void Reset()
    {
        station = GetComponentInParent<LSO_SpaceStation>();
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        if (station == null) station = GetComponentInParent<LSO_SpaceStation>();
        if (station == null)
            Debug.LogError($"[{name}] SpaceStation 참조를 찾을 수 없습니다.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(targetTag)) return;

        // 자식 콜라이더에 맞을 수도 있으므로 부모까지 탐색
        var agent = other.GetComponentInParent<SpaceShipAgent>();
        if (agent == null) return;

        switch (action)
        {
            case TriggerAction.EnterStation:
                station.ToInnerCam(agent.gameObject);
                break;
            case TriggerAction.Heal:
                station.Heal(agent.gameObject);
                break;
        }
    }
}