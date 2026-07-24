using UnityEngine;
using _Scripts.Suxghui.Mining;

namespace _Scripts.LSO.Data
{
    public class LSO_Ore : MonoBehaviour, LSO_IMinerable
    {
        private const float PlaceholderCubeScale = 0.12f;

        [Header("Loose Ore Size")]
        [SerializeField] private Vector2 looseOreRandomScaleRange = new Vector2(0.7f, 1.2f);

        public LSO_OreSO oreSO;

        private MeshRenderer _meshRenderer;
        private bool _breakFeedbackPending;
        private GameObject _pendingExplosionPrefab;
        private Vector3 _pendingOrigin;
        private Vector3 _pendingChunkScale;
        private float _pendingExplosionLifetime;
        private float _pendingExplosionScale;
        private float _pendingPurity;
        private bool _pendingScorched;
        private int _pendingMineralAmount;
        private int _pendingMaximumChunks;
        private float _pendingMinimumScatterDistance;
        private float _pendingMaximumScatterDistance;
        private float _pendingScatterDuration;
        private int _pendingLayer;
        private GameObject[] _worldOreTemplates;

        public bool BreakFeedbackPlayedLastMine { get; private set; }

        public void SetWorldOreTemplates(GameObject[] templates)
        {
            _worldOreTemplates = templates;
        }

        private void Awake()
        {
            // 메시가 자식 오브젝트에 있는 경우까지 대응한다.
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void Start()
        {
            Init();
        }

        private void Init()
        {
            if (oreSO == null)
            {
                Debug.LogError($"[LSO_Ore] '{name}' 에 OreSO가 지정되지 않았습니다.", this);
                return;
            }

            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            if (_meshRenderer == null)
            {
                Debug.LogWarning($"[LSO_Ore] '{name}' 및 자식에서 MeshRenderer를 찾지 못했습니다.", this);
                return;
            }

            if (oreSO.oreMaterial == null) return;

            // materials / sharedMaterials 는 배열의 '복사본'을 반환한다.
            // 반환값의 원소를 직접 바꾸면 그 복사본만 바뀌고 버려진다.
            // 반드시 지역 변수로 받아 수정한 뒤 다시 대입해야 한다.
            Material[] mats = _meshRenderer.sharedMaterials;
            if (mats.Length == 0) return;

            mats[0] = oreSO.oreMaterial;
            _meshRenderer.sharedMaterials = mats;
        }

        [ContextMenu("Mine")]
        public LSO_MineralSO Mine()
        {
            BreakFeedbackPlayedLastMine = false;
            if (oreSO == null || oreSO.mineral == null)
            {
                Debug.LogWarning($"[LSO_Ore] '{name}' 에서 채굴할 광물 정보가 없습니다.", this);
                return null;
            }

            LSO_MineralSO mineral = oreSO.mineral;
            Debug.Log($"{mineral.mineralType}을(를) 채굴하여 {mineral.mineralPrice}를 얻었습니다!", this);
            PlayConfiguredBreakFeedback();

            return mineral;
        }

        public void ConfigureBreakFeedback(
            GameObject explosionPrefab,
            Vector3 origin,
            float explosionLifetime,
            float explosionScale,
            int mineralAmount,
            int maximumChunks,
            Vector3 chunkScale,
            float purity,
            bool scorched,
            float minimumScatterDistance,
            float maximumScatterDistance,
            float scatterDuration,
            int targetLayer)
        {
            _breakFeedbackPending = true;
            _pendingExplosionPrefab = explosionPrefab;
            _pendingOrigin = origin;
            _pendingExplosionLifetime = Mathf.Max(0.1f, explosionLifetime);
            _pendingExplosionScale = Mathf.Max(0.01f, explosionScale);
            _pendingMineralAmount = Mathf.Max(1, mineralAmount);
            _pendingMaximumChunks = Mathf.Max(1, maximumChunks);
            _pendingChunkScale = new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(chunkScale.x)),
                Mathf.Max(0.01f, Mathf.Abs(chunkScale.y)),
                Mathf.Max(0.01f, Mathf.Abs(chunkScale.z)));
            _pendingPurity = Mathf.Clamp01(purity);
            _pendingScorched = scorched;
            _pendingMinimumScatterDistance = Mathf.Max(0f, minimumScatterDistance);
            _pendingMaximumScatterDistance = Mathf.Max(
                _pendingMinimumScatterDistance,
                maximumScatterDistance);
            _pendingScatterDuration = Mathf.Max(0.05f, scatterDuration);
            _pendingLayer = targetLayer;
        }

        public bool PlayConfiguredBreakFeedback()
        {
            if (!_breakFeedbackPending)
                return false;

            _breakFeedbackPending = false;
            BreakFeedbackPlayedLastMine = true;
            SpawnExplosion();
            int spawnedCubeCount = SpawnLooseOreCubes();
            Debug.Log(
                $"[LSO_Ore] 폭발 VFX와 원석 큐브 {spawnedCubeCount}개를 생성했습니다.",
                this);
            return true;
        }

        private void SpawnExplosion()
        {
            if (_pendingExplosionPrefab == null)
            {
                Debug.LogWarning("[LSO_Ore] 폭발 VFX 프리팹이 지정되지 않았습니다.", this);
                return;
            }

            GameObject effect = Instantiate(
                _pendingExplosionPrefab,
                _pendingOrigin,
                Random.rotation);
            effect.transform.localScale *= _pendingExplosionScale;
            foreach (ParticleSystem particle in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.gameObject.SetActive(true);
                particle.Clear(true);
                particle.Play(true);
            }

            Destroy(effect, _pendingExplosionLifetime);
        }

        private int SpawnLooseOreCubes()
        {
            int chunkCount = Mathf.Clamp(
                _pendingMineralAmount,
                1,
                _pendingMaximumChunks);

            for (int i = 0; i < chunkCount; i++)
            {
                int representedAmount = _pendingMineralAmount / chunkCount +
                                        (i < _pendingMineralAmount % chunkCount ? 1 : 0);
                GameObject template = PickWorldOreTemplate();
                GameObject looseObject = template != null
                    ? Instantiate(template)
                    : GameObject.CreatePrimitive(PrimitiveType.Sphere);
                looseObject.SetActive(true);
                looseObject.name = $"{oreSO.mineral.mineralName} Ore {i + 1}";
                looseObject.transform.SetPositionAndRotation(_pendingOrigin, Random.rotation);
                float randomScale = Random.Range(
                    Mathf.Min(looseOreRandomScaleRange.x, looseOreRandomScaleRange.y),
                    Mathf.Max(looseOreRandomScaleRange.x, looseOreRandomScaleRange.y));
                looseObject.transform.localScale = template != null
                    ? _pendingChunkScale * Mathf.Max(0.05f, randomScale)
                    : Vector3.one * PlaceholderCubeScale * Mathf.Max(0.05f, randomScale);
                looseObject.tag = "Ore";
                looseObject.layer = _pendingLayer;

                Renderer looseRenderer = looseObject.GetComponentInChildren<Renderer>(true);
                Material mineralMaterial = oreSO.mineral != null
                    ? oreSO.mineral.mineralMaterial
                    : null;
                if (looseRenderer != null && mineralMaterial != null)
                {
                    Material[] materials = looseRenderer.sharedMaterials;
                    if (materials.Length > 0)
                    {
                        materials[0] = mineralMaterial;
                        looseRenderer.sharedMaterials = materials;
                    }
                }

                if (looseObject.GetComponentInChildren<Collider>(true) == null)
                    looseObject.AddComponent<BoxCollider>();

                LSO_Ore looseOre = looseObject.GetComponent<LSO_Ore>() ??
                                   looseObject.GetComponentInChildren<LSO_Ore>(true) ??
                                   looseObject.AddComponent<LSO_Ore>();
                looseOre.oreSO = oreSO;

                MineableAsteroid looseTarget = looseObject.GetComponent<MineableAsteroid>() ??
                                               looseObject.AddComponent<MineableAsteroid>();
                looseTarget.InitializeAsLooseMineral(
                    oreSO,
                    representedAmount,
                    _pendingPurity,
                    _pendingScorched);
                looseTarget.LaunchInSpace(
                    Random.onUnitSphere,
                    _pendingMinimumScatterDistance,
                    _pendingMaximumScatterDistance,
                    _pendingScatterDuration);
            }

            return chunkCount;
        }

        private GameObject PickWorldOreTemplate()
        {
            if (_worldOreTemplates == null || _worldOreTemplates.Length == 0)
                return null;

            int validCount = 0;
            for (int i = 0; i < _worldOreTemplates.Length; i++)
                if (_worldOreTemplates[i] != null)
                    validCount++;

            if (validCount == 0)
                return null;

            int selected = Random.Range(0, validCount);
            for (int i = 0; i < _worldOreTemplates.Length; i++)
            {
                if (_worldOreTemplates[i] == null)
                    continue;
                if (selected-- == 0)
                    return _worldOreTemplates[i];
            }

            return null;
        }

#if UNITY_EDITOR
        /// <summary>플레이하지 않고도 SO에 지정된 머티리얼을 확인한다.</summary>
        [ContextMenu("머티리얼 적용 미리보기")]
        private void PreviewMaterial()
        {
            if (oreSO == null) return;

            _meshRenderer = GetComponentInChildren<MeshRenderer>();
            ApplyMaterial();
        }
#endif
    }
}
