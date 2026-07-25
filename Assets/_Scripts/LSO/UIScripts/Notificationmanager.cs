using UnityEngine;
using DG.Tweening;
using TMPro;
using _Scripts.LHS.Sound;
using _Scripts.LHS.SoundManager;

/// <summary>
/// 알림창 매니저.
/// 프리팹을 할당하고 Show()를 호출하면:
///  1) 프리팹을 복사(Instantiate)
///  2) 화면 위쪽(밖)에서 지정 위치로 내려옴
///  3) 일정 시간 머무름
///  4) 다시 위로 올라간 뒤
///  5) 자동 삭제
///
/// 알림 프리팹은 Canvas 안에 들어갈 UI(RectTransform) 오브젝트여야 합니다.
/// 이 스크립트는 Canvas(또는 그 하위) 오브젝트에 붙이세요.
/// </summary>
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }
    private float _nextOreNotificationTime;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Notify(string message)
    {
        if (Instance == null || string.IsNullOrWhiteSpace(message)) return;
        if (message.StartsWith("원석 획득") && Time.unscaledTime < Instance._nextOreNotificationTime)
            return;
        if (message.StartsWith("원석 획득"))
            Instance._nextOreNotificationTime = Time.unscaledTime + 0.2f;
        Instance.Show(message);
        if (SoundManager.Instance != null)
            SoundManager.Instance.Play(SoundType.UI, "Click01");
    }
    [Header("Prefab")]
    [SerializeField] private RectTransform notificationPrefab;  // 복사할 알림 프리팹
    [SerializeField] private RectTransform spawnParent;         // 생성될 부모 (보통 Canvas). 비우면 자기 자신
    [SerializeField] private string desc;
    
    [Header("Position")]
    [SerializeField] private float shownPosY = -80f;    // 머무는 최종 Y 위치 (앵커 기준)
    [SerializeField] private float hiddenPosY = 200f;   // 화면 위 바깥 Y 위치 (시작/퇴장 지점)

    [Header("Timing")]
    [SerializeField] private float slideInDuration = 0.4f;   // 내려오는 시간
    [SerializeField] private float stayDuration = 2f;        // 머무는 시간
    [SerializeField] private float slideOutDuration = 0.35f; // 올라가는 시간

    [Header("Ease")]
    [SerializeField] private Ease slideInEase = Ease.OutBack;
    [SerializeField] private Ease slideOutEase = Ease.InBack;

    [Header("Options")]
    [SerializeField] private bool ignoreTimeScale = true;  // 일시정지(timeScale=0)에서도 동작

    /// <summary>기본 프리팹으로 알림을 띄운다.</summary>
    [ContextMenu("Spawn")]
    public void Show()
    {
        Show(desc);
    }

    /// <summary>외부에서 프리팹을 직접 넘겨 알림을 띄운다.</summary>
    public void Show(string des)
    {
        RectTransform prefab = notificationPrefab;
        
        if (prefab == null)
        {
            Debug.LogWarning("[NotificationManager] 알림 프리팹이 없습니다.");
            return;
        }
        
        RectTransform parent = spawnParent != null ? spawnParent : (RectTransform)transform;

        // 1) 프리팹 복사
        RectTransform notif = Instantiate(prefab, parent);
        
        notif.GetComponentInChildren<TextMeshProUGUI>().text = des;
        
        // 시작 위치를 화면 위 바깥으로
        Vector2 pos = notif.anchoredPosition;
        pos.y = hiddenPosY;
        notif.anchoredPosition = pos;

        // 2~5) 시퀀스: 내려오기 → 대기 → 올라가기 → 삭제
        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(ignoreTimeScale);
        seq.SetLink(notif.gameObject);   // 알림이 먼저 파괴돼도 트윈 에러 방지

        seq.Append(notif.DOAnchorPosY(shownPosY, slideInDuration).SetEase(slideInEase));
        seq.AppendInterval(stayDuration);
        seq.Append(notif.DOAnchorPosY(hiddenPosY, slideOutDuration).SetEase(slideOutEase));
        seq.OnComplete(() =>
        {
            if (notif != null) Destroy(notif.gameObject);
        });
    }
}
