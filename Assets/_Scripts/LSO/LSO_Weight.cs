using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace _Scripts.LSO
{
    public class LSO_Weight : MonoBehaviour
    {
        public static LSO_Weight Instance { get; private set; }

        [Header("무게")]
        [SerializeField, Min(1)] private int maxWeight = 15;

        [Tooltip("칸 하나가 나타내는 무게. 1이면 무게 1당 칸 하나.")]
        [SerializeField, Min(1)] private int weightPerBar = 1;

        [Header("칸 UI")]
        [SerializeField] private GameObject barPrefab;

        [Tooltip("칸이 들어갈 부모. VerticalLayoutGroup이 배치를 담당한다.")]
        [SerializeField] private RectTransform barContainer;

        [Tooltip("켜면 새 칸이 위쪽에 쌓인다. VerticalLayoutGroup의 Reverse Arrangement로도 조절할 수 있다.")]
        [SerializeField] private bool stackUpward = true;

        [Header("연출")]
        [SerializeField] private bool animateBars = true;
        [SerializeField] private float popDuration = 0.15f;
        
        private readonly List<GameObject> _bars = new List<GameObject>();

        public int Weight { get; private set; }
        public int MaxWeight => maxWeight;
        
        public float Ratio => (float)Weight / maxWeight;

        public bool IsFull => Weight >= maxWeight;
        public bool IsEmpty => Weight <= 0;
        
        public int RemainingCapacity => Mathf.Max(0, maxWeight - Weight);
        
        public int MaxBars => Mathf.CeilToInt(maxWeight / (float)weightPerBar);
        
        public event Action<int, int> OnWeightChanged;
        
        public event Action OnFull;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (barPrefab == null)
                Debug.LogError($"[{nameof(LSO_Weight)}] barPrefab이 지정되지 않았습니다.", this);

            if (barContainer == null)
                Debug.LogError($"[{nameof(LSO_Weight)}] barContainer가 지정되지 않았습니다.", this);
        }

        private void Start()
        {
            SyncBars();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
        
        public void AddWeight(int amount)
        {
            if (amount <= 0) return;

            SetWeight(Weight + amount);
        }
        
        public void RemoveWeight(int amount)
        {
            if (amount <= 0) return;

            SetWeight(Weight - amount);
        }
        
        private void SetWeight(int value)
        {
            int newWeight = Mathf.Clamp(value, 0, maxWeight);
            if (newWeight == Weight) return;

            bool wasFull = IsFull;
            Weight = newWeight;
            
            SyncBars();

            OnWeightChanged?.Invoke(Weight, maxWeight);

            if (IsFull && !wasFull) OnFull?.Invoke();
        }

        public void ResetWeight() => SetWeight(0);
        
        public bool CanAdd(int amount) => amount <= RemainingCapacity;
        
        private void SyncBars()
        {
            if (barPrefab == null || barContainer == null) return;
            
            int targetCount = Mathf.Clamp(
                Mathf.CeilToInt(Weight / (float)weightPerBar), 0, MaxBars);

            while (_bars.Count < targetCount) AddBar();
            while (_bars.Count > targetCount) RemoveBar();
        }

        private void AddBar()
        {
            var bar = LSO_PoolManager.Instance != null ? LSO_PoolManager.Instance.Get(barPrefab, Vector3.zero, Quaternion.identity, barContainer) :
                Instantiate(barPrefab, barContainer);

            if (bar == null) return;

            Transform t = bar.transform;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            
            if (stackUpward) t.SetAsFirstSibling();
            else t.SetAsLastSibling();

            _bars.Add(bar);

            if (animateBars)
            {
                t.localScale = Vector3.zero;
                t.DOScale(Vector3.one, popDuration).SetEase(Ease.OutBack).SetLink(bar);
            }
        }

        private void RemoveBar()
        {
            int last = _bars.Count - 1;
            if (last < 0) return;

            GameObject bar = _bars[last];
            _bars.RemoveAt(last);

            if (bar == null) return;
            
            bar.transform.DOKill();

            if (LSO_PoolManager.Instance != null) LSO_PoolManager.Instance.Release(bar);
            else Destroy(bar);
        }

#if UNITY_EDITOR
        [ContextMenu("무게 +1")]
        private void TestAdd() { if (Application.isPlaying) AddWeight(1); }

        [ContextMenu("무게 -1")]
        private void TestRemove() { if (Application.isPlaying) RemoveWeight(1); }

        [ContextMenu("무게 초기화")]
        private void TestReset() { if (Application.isPlaying) ResetWeight(); }
#endif
    }
}