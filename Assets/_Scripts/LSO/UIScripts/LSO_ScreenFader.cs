using System;
using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class LSO_ScreenFader : MonoBehaviour
{

    [SerializeField] private float duration = 1f;
    [SerializeField] private Ease ease = Ease.Linear;

    private CanvasGroup cg;
    private Tween tween;

    private void Awake()
    {
        cg = GetComponent<CanvasGroup>();
    }

    public Tween FadeIn(Action onComplete = null) => Fade(0f, onComplete);   // 밝아짐
    public Tween FadeOut(Action onComplete = null) => Fade(1f, onComplete);  // 어두워짐

    public Tween Fade(float target, Action onComplete = null)
    {
        tween?.Kill();                    // 중복 호출 시 이전 트윈 정리
        cg.blocksRaycasts = true;

        tween = cg.DOFade(target, duration)
            .SetEase(ease)
            .SetUpdate(true)        // Time.timeScale = 0 에서도 동작
            .OnComplete(() =>
            {
                cg.blocksRaycasts = target > 0.5f;
                onComplete?.Invoke();
            });

        return tween;
    }

    private void OnDestroy() => tween?.Kill();
}