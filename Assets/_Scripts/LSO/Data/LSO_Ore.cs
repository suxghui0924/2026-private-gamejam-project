using UnityEngine;
using _Scripts.Suxghui.Mining;

namespace _Scripts.LSO.Data
{
    public class LSO_Ore : MonoBehaviour, LSO_IMinerable
    {
        private const float PlaceholderCubeScale = 0.06f;

        [Header("Loose Ore Size")] [SerializeField]
        private Vector2 looseOreRandomScaleRange = new Vector2(0.7f, 1.2f);

        public LSO_OreSO oreSO;
        
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

        private void Start()
        {
            Init();
        }

        private void Init()
        {
            if (oreSO == null)
            {
                Debug.LogError($"[LSO_Ore] '{name}' 에 OreSO가 지정되지 않았습니다.", this);
            }
        }



        private void ApplyLooseMineralMaterial()
        {
            Material mineralMaterial = oreSO != null && oreSO.mineral != null
                ? oreSO.mineral.mineralMaterial
                : null;
            if (mineralMaterial == null)
            {
                Debug.LogWarning($"[LSO_Ore] '{name}'의 Mineral SO에 원석 Material이 없습니다.", this);
            }
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
            Debug.Log($"{mineral.mineralType} 1kg을 채굴했습니다. kg당 가격: {mineral.PricePerKilogram}", this);
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

                Material mineralMaterial = oreSO.mineral != null
                    ? oreSO.mineral.mineralMaterial
                    : null;
                if (mineralMaterial != null)
                {
                    Renderer[] looseRenderers = looseObject.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < looseRenderers.Length; rendererIndex++)
                    {
                        Material[] materials = looseRenderers[rendererIndex].sharedMaterials;
                        if (materials.Length == 0)
                        {
                            looseRenderers[rendererIndex].sharedMaterial = mineralMaterial;
                            continue;
                        }

                        for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                            materials[materialIndex] = mineralMaterial;
                        looseRenderers[rendererIndex].sharedMaterials = materials;
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
    }
}
