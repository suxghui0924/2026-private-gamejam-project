using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.LSO
{
    /// <summary>
    /// 풀에서 꺼내지거나 돌아갈 때 알림을 받고 싶은 오브젝트가 구현한다.
    /// 풀링된 오브젝트는 Awake가 최초 1회만 실행되므로, 재사용 때마다 필요한
    /// 초기화는 Awake가 아니라 여기(또는 OnEnable)에 넣어야 한다.
    /// </summary>
    public interface LSO_IPoolable
    {
        void OnSpawnFromPool();
        void OnReturnToPool();
    }

    /// <summary>
    /// 프리팹별로 오브젝트를 재사용하는 간단한 풀 매니저.
    /// </summary>
    public class LSO_PoolManager : MonoBehaviour
    {
        public static LSO_PoolManager Instance { get; private set; }

        [System.Serializable]
        public class PrewarmEntry
        {
            public GameObject prefab;
            [Min(0)] public int count = 10;
        }

        [Header("시작할 때 미리 만들어 둘 오브젝트")]
        [Tooltip("첫 생성 시의 순간적인 끊김을 없앤다.")]
        [SerializeField] private List<PrewarmEntry> prewarm = new List<PrewarmEntry>();

        [Header("씬이 바뀌어도 풀을 유지할지")]
        [SerializeField] private bool dontDestroyOnLoad;

        // 프리팹 → 대기 중인 인스턴스들
        private readonly Dictionary<GameObject, Queue<GameObject>> _pools = new();

        // 인스턴스 → 어느 프리팹에서 나왔는지. Release 때 돌려보낼 곳을 찾는 데 쓴다.
        private readonly Dictionary<GameObject, GameObject> _origin = new();

        // 프리팹 → Hierarchy 정리용 부모
        private readonly Dictionary<GameObject, Transform> _containers = new();

        // ───────── 초기화 ─────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (dontDestroyOnLoad)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }

            foreach (var entry in prewarm)
            {
                if (entry.prefab == null) continue;
                Prewarm(entry.prefab, entry.count);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>인스턴스를 미리 만들어 풀에 채워둔다.</summary>
        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            Queue<GameObject> pool = GetPool(prefab);
            Transform container = GetContainer(prefab);

            for (int i = 0; i < count; i++)
            {
                GameObject instance = Instantiate(prefab, container);
                instance.SetActive(false);

                _origin[instance] = prefab;
                pool.Enqueue(instance);
            }
        }

        // ───────── 꺼내기 ─────────

        public GameObject Get(GameObject prefab)
            => Get(prefab, Vector3.zero, Quaternion.identity);

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[PoolManager] prefab이 null입니다.", this);
                return null;
            }

            Queue<GameObject> pool = GetPool(prefab);

            GameObject instance = null;

            // 씬 전환 등으로 파괴된 인스턴스가 큐에 남아있을 수 있으므로 건너뛴다.
            while (pool.Count > 0 && instance == null)
                instance = pool.Dequeue();

            // 남은 게 없으면 새로 만든다. (풀이 자동으로 커진다)
            if (instance == null)
            {
                instance = Instantiate(prefab);
                _origin[instance] = prefab;
            }

            Transform t = instance.transform;
            t.SetParent(parent != null ? parent : GetContainer(prefab), false);
            t.SetPositionAndRotation(position, rotation);

            instance.SetActive(true);

            if (instance.TryGetComponent(out LSO_IPoolable poolable))
                poolable.OnSpawnFromPool();

            return instance;
        }

        /// <summary>컴포넌트를 바로 받아오는 편의 버전.</summary>
        public T Get<T>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            where T : Component
        {
            GameObject instance = Get(prefab, position, rotation, parent);
            return instance != null ? instance.GetComponent<T>() : null;
        }

        // ───────── 돌려주기 ─────────

        public void Release(GameObject instance)
        {
            if (instance == null) return;

            // 이미 반납된 오브젝트를 또 반납하면 큐에 중복으로 들어가
            // 같은 인스턴스가 두 곳에서 동시에 쓰이게 된다.
            if (!instance.activeSelf) return;

            if (!_origin.TryGetValue(instance, out GameObject prefab))
            {
                Debug.LogWarning($"[PoolManager] '{instance.name}' 은(는) 풀에서 나온 오브젝트가 아닙니다. 파괴합니다.", instance);
                Destroy(instance);
                return;
            }

            if (instance.TryGetComponent(out LSO_IPoolable poolable))
                poolable.OnReturnToPool();

            instance.SetActive(false);
            instance.transform.SetParent(GetContainer(prefab), false);

            GetPool(prefab).Enqueue(instance);
        }

        /// <summary>일정 시간 뒤에 돌려준다. 이펙트나 탄환에 유용하다.</summary>
        public void Release(GameObject instance, float delay)
        {
            if (instance == null) return;

            if (delay <= 0f)
            {
                Release(instance);
                return;
            }

            StartCoroutine(ReleaseRoutine(instance, delay));
        }

        private IEnumerator ReleaseRoutine(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            Release(instance);
        }

        // ───────── 정리 ─────────

        /// <summary>특정 프리팹의 대기 인스턴스를 모두 파괴한다.</summary>
        public void Clear(GameObject prefab)
        {
            if (prefab == null || !_pools.TryGetValue(prefab, out var pool)) return;

            while (pool.Count > 0)
            {
                GameObject instance = pool.Dequeue();
                if (instance == null) continue;

                _origin.Remove(instance);
                Destroy(instance);
            }
        }

        public void ClearAll()
        {
            foreach (var prefab in new List<GameObject>(_pools.Keys)) Clear(prefab);

            _pools.Clear();
            _origin.Clear();
        }

        // ───────── 내부 ─────────

        private Queue<GameObject> GetPool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Queue<GameObject>();
                _pools[prefab] = pool;
            }
            return pool;
        }

        private Transform GetContainer(GameObject prefab)
        {
            if (_containers.TryGetValue(prefab, out var container) && container != null)
                return container;

            container = new GameObject($"Pool - {prefab.name}").transform;
            container.SetParent(transform, false);

            _containers[prefab] = container;
            return container;
        }

#if UNITY_EDITOR
        [ContextMenu("풀 상태 출력")]
        private void LogPoolStatus()
        {
            var sb = new System.Text.StringBuilder("[PoolManager] 풀 상태\n");
            foreach (var kv in _pools)
                sb.AppendLine($"  {kv.Key.name}: 대기 {kv.Value.Count} / 총 생성 {CountTotal(kv.Key)}");
            Debug.Log(sb.ToString(), this);
        }

        private int CountTotal(GameObject prefab)
        {
            int count = 0;
            foreach (var kv in _origin) if (kv.Value == prefab) count++;
            return count;
        }
#endif
    }
}