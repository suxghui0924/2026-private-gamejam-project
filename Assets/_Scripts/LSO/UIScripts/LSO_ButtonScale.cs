using _Scripts.LHS.SoundManager;
using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// PC 전용: 마우스가 버튼 위에 올라가면 커지고, 누르면 살짝 작아지는 스크립트.
/// Canvas 안의 Button 오브젝트에 붙여서 사용하세요.
/// </summary>
[DisallowMultipleComponent]
public class LSO_ButtonScale : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Hover")]
    [SerializeField] private float hoverScale = 1.1f;      // 마우스 올렸을 때 배율
    [SerializeField] private float hoverDuration = 0.2f;
    [SerializeField] private Ease hoverEase = Ease.OutBack;

    [Header("Press")]
    [SerializeField] private float pressScale = 0.95f;     // 눌렀을 때 배율
    [SerializeField] private float pressDuration = 0.1f;
    [SerializeField] private Ease pressEase = Ease.OutQuad;

    [Header("Options")]
    [SerializeField] private bool ignoreTimeScale = true;  // 일시정지(timeScale=0)에서도 동작

    private Vector3 _originalScale;
    private bool _isHovering;
    private bool _isPressed;

    private void Awake()
    {
        _originalScale = transform.localScale;
    }

    // 마우스가 버튼 위로 들어옴 → 확대
    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovering = true;
        
        if (!_isPressed) AnimateTo(hoverScale, hoverDuration, hoverEase);
    }

    // 마우스가 버튼 밖으로 나감 → 원래 크기
    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        if (!_isPressed) AnimateTo(1f, hoverDuration, Ease.OutQuad);
    }

    // 마우스 버튼 누름 → 축소
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (SoundManager.Instance != null)
            SoundManager.Instance.Play(SoundType.UI,"Click01");
        _isPressed = true;
        AnimateTo(pressScale, pressDuration, pressEase);
    }

    // 마우스 버튼 뗌 → 여전히 위에 있으면 hover 크기, 아니면 원래 크기
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        _isPressed = false;
        AnimateTo(_isHovering ? hoverScale : 1f, hoverDuration, hoverEase);
    }

    private void AnimateTo(float multiplier, float duration, Ease ease)
    {
        transform.DOKill();                                 // 이전 트윈 정리 (겹침 방지)
        transform.DOScale(_originalScale * multiplier, duration)
                 .SetEase(ease)
                 .SetUpdate(ignoreTimeScale)
                 .SetLink(gameObject);                      // 오브젝트 파괴 시 자동 Kill
    }

    private void OnDisable()
    {
        // 호버 중 비활성화되면 커진 채로 남는 문제 방지
        transform.DOKill();
        transform.localScale = _originalScale;
        _isHovering = false;
        _isPressed = false;
    }
}
