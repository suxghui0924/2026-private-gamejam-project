using DG.Tweening;
using UnityEngine;

/// <summary>
/// 창을 스케일 애니메이션과 함께 열고 닫는다.
/// UI Button의 OnClick에 Toggle() / Open() / Close() 를 연결해서 사용한다.
/// </summary>
public class OpenWindow : MonoBehaviour
{
    [SerializeField] private GameObject window;

    [Header("열린 상태의 크기")]
    [Tooltip("켜면 Awake 시점의 스케일을 원본으로 저장한다. " +
             "같은 창을 여러 스크립트가 공유한다면 끄고 아래 값을 직접 지정하는 편이 안전하다.")]
    [SerializeField] private bool captureScaleOnAwake = true;
    [SerializeField] private Vector3 openedScale = Vector3.one;

    [Header("열기")]
    [SerializeField] private float openDuration = 0.25f;
    [Tooltip("OutBack은 살짝 튀어나왔다 들어오는 느낌을 준다.")]
    [SerializeField] private Ease openEase = Ease.OutBack;

    [Header("닫기")]
    [SerializeField] private float closeDuration = 0.15f;
    [SerializeField] private Ease closeEase = Ease.InBack;

    [Header("옵션")]
    [SerializeField] private bool startClosed = true;
    [Tooltip("스케일과 함께 알파도 페이드한다. CanvasGroup이 없으면 자동으로 추가된다.")]
    [SerializeField] private bool alsoFade = true;
    [Tooltip("일시정지(Time.timeScale = 0) 중에도 동작할지 여부.")]
    [SerializeField] private bool ignoreTimeScale = true;
    [Tooltip("애니메이션이 재생되는 동안 추가 입력을 무시한다. 더블클릭으로 즉시 닫히는 것을 막는다.")]
    [SerializeField] private bool ignoreInputWhileAnimating = true;

    [Header("디버그")]
    [Tooltip("열기/닫기 호출과 상태를 콘솔에 찍는다. 원인 추적용.")]
    [SerializeField] private bool verboseLog;

    private Transform _windowTransform;
    private CanvasGroup _canvasGroup;
    private Vector3 _defaultScale;
    private Tween _currentTween;

    // activeSelf로 판정하면 닫히는 애니메이션 도중에는 아직 true라서
    // 상태가 뒤집힌다. 논리 상태를 따로 들고 있는다.
    private bool _isOpen;

    public bool IsOpen => _isOpen;

    /// <summary>트윈이 살아있고 재생 중인지. 죽은(재활용된) 참조를 걸러낸다.</summary>
    public bool IsAnimating => _currentTween != null && _currentTween.IsActive() && _currentTween.IsPlaying();

    private void Awake()
    {
        if (window == null)
        {
            Debug.LogError($"[{nameof(OpenWindow)}] '{name}' 에 window가 지정되지 않았습니다.", this);
            enabled = false;
            return;
        }

        _windowTransform = window.transform;
        _defaultScale = captureScaleOnAwake ? _windowTransform.localScale : openedScale;

        // 캡처 시점에 스케일이 이미 0이면 열어도 크기 0으로 커져서 보이지 않는다.
        // 같은 window를 다른 OpenWindow가 먼저 초기화해 0으로 만들었을 때 발생한다.
        if (_defaultScale.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning(
                $"[{nameof(OpenWindow)}] '{window.name}' 의 Scale이 0인 상태로 캡처됐습니다. " +
                $"같은 창을 가리키는 OpenWindow가 두 개 이상이거나 프리팹에 0이 저장돼 있을 수 있습니다. " +
                $"{openedScale} 로 보정합니다.", window);

            _defaultScale = openedScale.sqrMagnitude < 0.0001f ? Vector3.one : openedScale;
        }

        if (alsoFade)
        {
            _canvasGroup = window.GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = window.AddComponent<CanvasGroup>();
        }

        if (startClosed) CloseImmediately();
        else _isOpen = true;
    }

    // ───────── 공개 메서드 ─────────

    /// <summary>열려 있으면 닫고, 닫혀 있으면 연다.</summary>
    public void Toggle()
    {
        if (ignoreInputWhileAnimating && IsAnimating)
        {
            Log("애니메이션 중이라 입력을 무시했습니다.");
            return;
        }

        if (_isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (window == null) return;
        if (ignoreInputWhileAnimating && IsAnimating && _isOpen) return;

        Log($"Open() 호출. activeSelf={window.activeSelf}, scale={_windowTransform.localScale}");

        _isOpen = true;
        KillCurrentTween();

        // 완전히 닫힌 상태에서만 0부터 시작한다.
        // 닫히는 도중이었다면 현재 크기에서 자연스럽게 이어진다.
        if (!window.activeSelf)
        {
            window.SetActive(true);
            _windowTransform.localScale = Vector3.zero;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;

        _currentTween = BuildTween(_defaultScale, 1f, openDuration, openEase)
            .OnComplete(() =>
            {
                if (_canvasGroup != null) _canvasGroup.interactable = true;
                Log($"Open 완료. scale={_windowTransform.localScale}");
            });
    }

    public void Close()
    {
        if (window == null) return;

        _isOpen = false;

        if (!window.activeSelf) return;
        if (ignoreInputWhileAnimating && IsAnimating && !_isOpen) return;

        Log("Close() 호출");

        KillCurrentTween();

        // 닫히는 동안 클릭이 먹지 않도록 막는다.
        if (_canvasGroup != null) _canvasGroup.interactable = false;

        _currentTween = BuildTween(Vector3.zero, 0f, closeDuration, closeEase)
            .OnComplete(() =>
            {
                if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
                window.SetActive(false);
                Log("Close 완료");
            });
    }

    /// <summary>애니메이션 없이 즉시 닫는다.</summary>
    public void CloseImmediately()
    {
        if (window == null) return;

        _isOpen = false;
        KillCurrentTween();

        _windowTransform.localScale = Vector3.zero;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        window.SetActive(false);
    }

    // ───────── 내부 ─────────

    private Tween BuildTween(Vector3 targetScale, float targetAlpha, float dur, Ease e)
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(_windowTransform.DOScale(targetScale, dur).SetEase(e));

        // 알파에까지 Back 이징을 걸면 값이 0 아래나 1 위로 튀므로 선형으로 둔다.
        if (_canvasGroup != null)
            seq.Join(_canvasGroup.DOFade(targetAlpha, dur).SetEase(Ease.Linear));

        return seq.SetUpdate(ignoreTimeScale)
                  // 창이 파괴되면 트윈도 같이 정리된다.
                  .SetLink(window)
                  // 완료/중단으로 트윈이 죽으면 참조를 놓는다.
                  // 이게 없으면 DOTween이 재활용한 객체를 계속 들고 있다가
                  // 나중에 Kill()이 엉뚱한 트윈을 죽인다.
                  .OnKill(() => _currentTween = null);
    }

    private void KillCurrentTween()
    {
        // IsActive() 확인 없이 Kill()을 부르면 이미 풀로 반환돼
        // 다른 용도로 재사용 중인 트윈을 죽일 수 있다.
        if (_currentTween != null && _currentTween.IsActive())
            _currentTween.Kill();

        _currentTween = null;
    }

    private void Log(string message)
    {
        if (verboseLog) Debug.Log($"[{nameof(OpenWindow)}:{name}] {message}", this);
    }

    private void OnDestroy()
    {
        KillCurrentTween();
    }
}