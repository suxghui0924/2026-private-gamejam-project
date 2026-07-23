using DG.Tweening;
using UnityEngine;

/// <summary>
/// 버튼을 누를 때마다 지정한 축으로 부드럽게 회전한다.
/// UI Button의 OnClick에 Rotate() 를 연결해서 사용한다.
/// </summary>
public class LSO_SmoothRotator : MonoBehaviour
{
    private enum RotationAxis { X, Y, Z }

    /// <summary>회전 중에 입력이 또 들어왔을 때의 처리 방식.</summary>
    private enum InterruptMode
    {
        /// <summary>회전이 끝날 때까지 추가 입력을 무시한다. (가장 무난)</summary>
        Ignore,

        /// <summary>진행 중인 회전을 즉시 완료시키고 새 회전을 시작한다. (반응이 빠름)</summary>
        Restart,

        /// <summary>회전을 겹쳐서 누적한다. 연타하면 그만큼 계속 돈다.</summary>
        Blend
    }

    [Header("대상 (비워두면 자기 자신)")]
    [SerializeField] private Transform target;

    [Header("회전")]
    [SerializeField] private RotationAxis axis = RotationAxis.Y;
    [Tooltip("360, 720 등 한 바퀴 이상도 정상 동작한다.")]
    [SerializeField] private float angle = 180f;
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private Ease ease = Ease.InOutQuad;
    [Tooltip("끄면 월드 축을 기준으로 회전한다.")]
    [SerializeField] private bool useLocalSpace = true;

    [Header("동작")]
    [SerializeField] private InterruptMode interruptMode = InterruptMode.Ignore;
    [Tooltip("일시정지(Time.timeScale = 0) 중에도 회전시킬지 여부. UI 메뉴라면 켜두는 게 좋다.")]
    [SerializeField] private bool ignoreTimeScale;

    private Tween _currentTween;

    /// <summary>현재 회전 중인지.</summary>
    public bool IsRotating => _currentTween != null && _currentTween.IsActive() && _currentTween.IsPlaying();

    private Vector3 AxisVector => axis switch
    {
        RotationAxis.X => Vector3.right,
        RotationAxis.Y => Vector3.up,
        _              => Vector3.forward
    };

    private void Awake()
    {
        if (target == null) target = transform;
    }

    /// <summary>설정한 각도만큼 정방향으로 회전한다.</summary>
    public void Rotate() => RotateBy(angle);

    /// <summary>설정한 각도만큼 역방향으로 회전한다.</summary>
    public void RotateReverse() => RotateBy(-angle);

    /// <summary>임의의 각도만큼 회전한다.</summary>
    public void RotateBy(float degrees)
    {
        if (target == null) return;
        if (Mathf.Approximately(degrees, 0f)) return;

        Vector3 delta = AxisVector * degrees;

        // ── 인터럽트 처리 ──
        // Blend 모드는 진행 중인 트윈을 그대로 두고 새 트윈을 겹쳐서 누적시킨다.
        if (interruptMode != InterruptMode.Blend && IsRotating)
        {
            if (interruptMode == InterruptMode.Ignore) return;

            // true를 넘겨 즉시 "완료" 처리한다. 중간에 그냥 Kill하면
            // 각도가 어중간한 값에서 멈춰 계속 어긋난다.
            _currentTween.Kill(true);
        }

        // ── 회전 ──
        // Blendable 계열은 시작/목표 쿼터니언을 보간하는 대신
        // 매 프레임의 '변화량'을 누적해서 곱한다. 그래서 중간 각도를 실제로 지나가고,
        // 360도(= Quaternion.identity 와 동일)나 720도처럼 한 바퀴 이상인 값,
        // 그리고 음수 각도의 방향까지 의도대로 동작한다.
        //
        // 반면 RotateMode.LocalAxisAdd 는 목표 쿼터니언을 만들어 최단 경로로 보간하므로
        //   - 360도 → 시작과 목표가 같아져서 아예 움직이지 않고
        //   - 270도 → 반대 방향으로 90도만 돌며
        //   - ±180도 → 방향이 구분되지 않는다.
        _currentTween = useLocalSpace
            ? target.DOBlendableLocalRotateBy(delta, duration)
            : target.DOBlendableRotateBy(delta, duration);

        Configure(_currentTween, delta);
    }

    /// <summary>진행 중인 회전을 즉시 끝낸다.</summary>
    public void CompleteImmediately() => _currentTween?.Kill(true);

    // ───────── 내부 ─────────

    private void Configure(Tween tween, Vector3 delta)
    {
        tween.SetEase(ease)
            .SetUpdate(ignoreTimeScale)
            // 오브젝트가 파괴되면 트윈도 같이 정리된다.
            // 없으면 MissingReferenceException 이 터질 수 있다.
            .SetLink(gameObject);

        // 변화량을 누적하는 방식이라 아주 미세한 오차가 쌓일 수 있다.
        // 회전이 끝나는 순간 정확한 각도로 스냅해서 오차를 끊어준다.
        // (Blend 모드는 여러 트윈이 겹치므로 서로의 목표를 덮어써서 적용하지 않는다.)
        if (useLocalSpace && interruptMode != InterruptMode.Blend)
        {
            Quaternion goal = target.localRotation * Quaternion.Euler(delta);
            tween.OnComplete(() =>
            {
                if (target != null) target.localRotation = goal;
            });
        }
    }

    private void OnDisable()
    {
        // 비활성화 시 각도가 어중간하게 남지 않도록 완료시킨다.
        _currentTween?.Kill(true);
        _currentTween = null;
    }

#if UNITY_EDITOR
    [ContextMenu("회전 테스트")]
    private void TestRotate()
    {
        if (Application.isPlaying) Rotate();
        else Debug.Log("플레이 모드에서만 동작합니다.");
    }
#endif
}