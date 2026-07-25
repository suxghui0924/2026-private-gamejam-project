using System;
using System.Collections.Generic;
using _Scripts.LHS.Radar;
using _Scripts.LSO.Data;
using _Scripts.Suxghui.Mining;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace _Scripts.Suxghui.World
{
    public enum SceneSpawnPolicy
    {
        RefillToLimitOnSceneLoad = 0,
        PreserveRemainingPopulation = 1
    }

    [DisallowMultipleComponent]
    public class RuntimeZoneSpawner : MonoBehaviour
    {
        private enum SpawnCategory
        {
            Ore,
            Mine
        }

        [Serializable]
        public sealed class ZoneSpawnRule
        {
            public string displayName = "Zone";
            public ZoneType zoneType = ZoneType.Normal;
            public Zone zone;

            [Header("Stone Population")]
            [FormerlySerializedAs("orePrefabs")]
            public GameObject[] stonePrefabs = Array.Empty<GameObject>();
            public LSO_OreSO[] internalOreSOs = Array.Empty<LSO_OreSO>();
            [Tooltip("0이면 균등 확률입니다. 값이 높을수록 가격이 비싼 내부/외부 원석이 더 자주 선택됩니다.")]
            [Range(0f, 3f)] public float mineralValueBias;
            [FormerlySerializedAs("oreLimit"), Min(0)] public int stoneLimit = 30;
            [FormerlySerializedAs("oreRespawnBatchSize"), Min(1)]
            public int stoneRespawnBatchSize = 1;
            [FormerlySerializedAs("minimumOreScale"), Min(0.01f)]
            public float minimumStoneScale = 200f;
            [FormerlySerializedAs("maximumOreScale"), Min(0.01f)]
            public float maximumStoneScale = 300f;

            [Header("External Ore Surface Scatter")]
            [Tooltip("Ore1/Ore2/Ore3처럼 공용으로 사용할 원석 모양입니다. 광물별 외형은 Material로 바뀝니다.")]
            public GameObject[] externalOreTemplates = Array.Empty<GameObject>();
            public LSO_MineralSO[] externalMinerals = Array.Empty<LSO_MineralSO>();
            [Min(0)] public int minimumExternalOreCount = 1;
            [Min(0)] public int maximumExternalOreCount = 3;
            [Min(0.01f)] public float minimumExternalOreScale = 0.06f;
            [Min(0.01f)] public float maximumExternalOreScale = 0.1f;
            [Min(0f)] public float externalOreSurfaceOffset = 0.02f;
            [Range(0f, 180f)] public float minimumExternalOreAngle = 35f;

            [Header("Mine Population")]
            public GameObject[] minePrefabs = Array.Empty<GameObject>();
            [Min(0)] public int mineLimit;
            [Min(1)] public int mineRespawnBatchSize = 1;
            [Min(0.01f)] public float minimumMineScale = 3f;
            [Min(0.01f)] public float maximumMineScale = 4f;
            [Min(0f)] public float minimumMineSpawnDistance = 8f;

            [Header("Zone Respawn")]
            [Tooltip("This range is used independently for ore and mine respawn timers.")]
            public Vector2 respawnCooldownRange = new Vector2(10f, 20f);
            [Min(0f)] public float minimumSpawnDistance = 40f;
            [Min(1)] public int maximumPlacementAttempts = 100;
        }

        private sealed class SpawnMetadata
        {
            public int SourceIndex;
            public LSO_OreSO OreDefinition;
            public SpawnCategory Category;
        }

        private sealed class RuntimeZoneState
        {
            public ZoneSpawnRule Rule;
            public Transform Root;
            public Transform OreContainer;
            public Transform MineContainer;
            public float NextOreSpawnAt = -1f;
            public float NextMineSpawnAt = -1f;
            public readonly Dictionary<GameObject, SpawnMetadata> Spawned =
                new Dictionary<GameObject, SpawnMetadata>();
        }

        private sealed class SavedSpawnRecord
        {
            public int SourceIndex;
            public LSO_OreSO OreDefinition;
            public SpawnCategory Category;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        private sealed class SavedZoneState
        {
            public float RemainingOreCooldown = -1f;
            public float RemainingMineCooldown = -1f;
            public readonly List<SavedSpawnRecord> Spawns = new List<SavedSpawnRecord>();
        }

        private sealed class SavedSpawnerState
        {
            public readonly Dictionary<ZoneType, SavedZoneState> Zones =
                new Dictionary<ZoneType, SavedZoneState>();
        }

        private static readonly Dictionary<string, SavedSpawnerState> SavedStates =
            new Dictionary<string, SavedSpawnerState>();

        [Header("Scene Transition")]
        [SerializeField] private SceneSpawnPolicy sceneSpawnPolicy =
            SceneSpawnPolicy.RefillToLimitOnSceneLoad;
        [SerializeField] private string persistenceKey = "StarFieldRuntimeZones";

        [Header("Startup")]
        [SerializeField] private bool fillToLimitOnStart = true;
        [SerializeField] private string runtimeContainerName = "RuntimeZoneSpawns";
        [SerializeField] private bool hideSceneTemplatesAtRuntime = true;

        [Header("Stone Radar")]
        [SerializeField] private Sprite stoneRadarIcon;

        [Header("Mine Hazard")]
        [SerializeField] private GameObject mineExplosionVfxPrefab;
        [SerializeField, Min(0f)] private float mineKnockbackForce = 75f;
        [SerializeField, Min(0f)] private float mineFuelDamage = 24f;
        [SerializeField, Min(0.01f)] private float mineExplosionVfxLifetime = 2.5f;
        [SerializeField, Min(0.01f)] private float mineExplosionVfxScale = 1f;

        [Header("Zone Rules")]
        [SerializeField] private List<ZoneSpawnRule> zoneRules = new List<ZoneSpawnRule>
        {
            new ZoneSpawnRule
            {
                displayName = "Normal Zone",
                zoneType = ZoneType.Normal,
                stoneLimit = 120,
                stoneRespawnBatchSize = 4,
                minimumStoneScale = 200f,
                maximumStoneScale = 300f,
                mineLimit = 0,
                minimumExternalOreCount = 1,
                maximumExternalOreCount = 2,
                respawnCooldownRange = new Vector2(10f, 12f),
                minimumSpawnDistance = 28f,
                maximumPlacementAttempts = 300
            },
            new ZoneSpawnRule
            {
                displayName = "Classified Zone",
                zoneType = ZoneType.Classified,
                stoneLimit = 180,
                stoneRespawnBatchSize = 6,
                minimumStoneScale = 280f,
                maximumStoneScale = 380f,
                mineralValueBias = 1f,
                mineLimit = 70,
                mineRespawnBatchSize = 4,
                minimumMineSpawnDistance = 9f,
                minimumExternalOreCount = 2,
                maximumExternalOreCount = 4,
                respawnCooldownRange = new Vector2(13f, 16f),
                minimumSpawnDistance = 24f,
                maximumPlacementAttempts = 300
            },
            new ZoneSpawnRule
            {
                displayName = "Top Secret Zone",
                zoneType = ZoneType.TopSecret,
                stoneLimit = 250,
                stoneRespawnBatchSize = 8,
                minimumStoneScale = 360f,
                maximumStoneScale = 480f,
                mineralValueBias = 1.5f,
                mineLimit = 110,
                mineRespawnBatchSize = 6,
                minimumMineSpawnDistance = 7f,
                minimumExternalOreCount = 3,
                maximumExternalOreCount = 5,
                respawnCooldownRange = new Vector2(17f, 20f),
                minimumSpawnDistance = 20f,
                maximumPlacementAttempts = 300
            }
        };

        private readonly List<RuntimeZoneState> _runtimeStates = new List<RuntimeZoneState>();
        private Transform _runtimeRoot;
        private bool _initialized;

        public SceneSpawnPolicy SceneSpawnPolicy => sceneSpawnPolicy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearStaticState()
        {
            SavedStates.Clear();
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_initialized)
                return;

            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                RuntimeZoneState state = _runtimeStates[i];
                PruneDestroyedObjects(state);
                UpdateRespawn(state, SpawnCategory.Ore);
                UpdateRespawn(state, SpawnCategory.Mine);
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || !_initialized)
                return;

            if (sceneSpawnPolicy == SceneSpawnPolicy.PreserveRemainingPopulation)
                SaveCurrentPopulation();
        }

        public int GetCurrentStoneCount(ZoneType zoneType)
        {
            RuntimeZoneState state = FindRuntimeState(zoneType);
            if (state == null)
                return 0;

            PruneDestroyedObjects(state);
            return CountSpawned(state, SpawnCategory.Ore);
        }

        [Obsolete("Use GetCurrentStoneCount. Ore means the embedded resource, not the large Stone.")]
        public int GetCurrentOreCount(ZoneType zoneType)
        {
            return GetCurrentStoneCount(zoneType);
        }

        public int GetCurrentMineCount(ZoneType zoneType)
        {
            RuntimeZoneState state = FindRuntimeState(zoneType);
            if (state == null)
                return 0;

            PruneDestroyedObjects(state);
            return CountSpawned(state, SpawnCategory.Mine);
        }

        [ContextMenu("Refill All Runtime Zones")]
        public void ForceRefillAll()
        {
            if (!_initialized)
                Initialize();

            for (int i = 0; i < _runtimeStates.Count; i++)
                FillStateToLimits(_runtimeStates[i]);
        }

        [ContextMenu("Clear Runtime Spawns")]
        public void ClearRuntimeSpawns()
        {
            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                RuntimeZoneState state = _runtimeStates[i];
                var objects = new List<GameObject>(state.Spawned.Keys);
                for (int j = 0; j < objects.Count; j++)
                {
                    if (objects[j] != null)
                        Destroy(objects[j]);
                }

                state.Spawned.Clear();
                state.NextOreSpawnAt = -1f;
                state.NextMineSpawnAt = -1f;
            }
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            ResolveMissingZones();
            CreateRuntimeContainers();

            bool restored = sceneSpawnPolicy == SceneSpawnPolicy.PreserveRemainingPopulation &&
                            TryRestoreSavedPopulation();
            if (!restored && fillToLimitOnStart)
            {
                for (int i = 0; i < _runtimeStates.Count; i++)
                    FillStateToLimits(_runtimeStates[i]);
            }

            if (hideSceneTemplatesAtRuntime)
                HideSceneTemplates();

            _initialized = true;
        }

        private void ResolveMissingZones()
        {
            Zone[] sceneZones = FindObjectsByType<Zone>(FindObjectsSortMode.None);
            for (int i = 0; i < zoneRules.Count; i++)
            {
                ZoneSpawnRule rule = zoneRules[i];
                NormalizeRule(rule);
                if (rule.zone != null)
                    continue;

                for (int j = 0; j < sceneZones.Length; j++)
                {
                    if (sceneZones[j].ZoneType != rule.zoneType)
                        continue;

                    rule.zone = sceneZones[j];
                    break;
                }
            }
        }

        private void CreateRuntimeContainers()
        {
            Transform existing = transform.Find(runtimeContainerName);
            if (existing != null)
                Destroy(existing.gameObject);

            var root = new GameObject(runtimeContainerName);
            root.transform.SetParent(transform, false);
            _runtimeRoot = root.transform;
            _runtimeStates.Clear();

            for (int i = 0; i < zoneRules.Count; i++)
            {
                ZoneSpawnRule rule = zoneRules[i];
                var state = new RuntimeZoneState { Rule = rule };
                state.Root = CreateChild(_runtimeRoot, rule.zoneType.ToString());
                state.OreContainer = CreateChild(state.Root, "Stones");
                state.MineContainer = CreateChild(state.Root, "Mines");
                _runtimeStates.Add(state);
            }
        }

        private static Transform CreateChild(Transform parent, string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private void FillStateToLimits(RuntimeZoneState state)
        {
            // Reserve the mine population first. Otherwise the hundreds of stones spawned
            // before it can consume every valid mine placement candidate.
            SpawnMissingImmediately(state, SpawnCategory.Mine);
            SpawnMissingImmediately(state, SpawnCategory.Ore);
        }

        private void SpawnMissingImmediately(RuntimeZoneState state, SpawnCategory category)
        {
            int limit = GetLimit(state.Rule, category);
            int missing = Mathf.Max(0, limit - CountSpawned(state, category));
            for (int i = 0; i < missing; i++)
            {
                if (!TrySpawnOne(state, category))
                    break;
            }
        }

        private void UpdateRespawn(RuntimeZoneState state, SpawnCategory category)
        {
            ZoneSpawnRule rule = state.Rule;
            int limit = GetLimit(rule, category);
            if (limit <= 0 || rule.zone == null || !HasValidPrefab(GetPrefabs(rule, category)))
            {
                SetNextSpawnAt(state, category, -1f);
                return;
            }

            int currentCount = CountSpawned(state, category);
            if (currentCount >= limit)
            {
                SetNextSpawnAt(state, category, -1f);
                return;
            }

            float nextSpawnAt = GetNextSpawnAt(state, category);
            if (nextSpawnAt < 0f)
            {
                ScheduleNextSpawn(state, category);
                return;
            }

            if (Time.time < nextSpawnAt)
                return;

            int batchSize = GetBatchSize(rule, category);
            int spawnCount = Mathf.Min(batchSize, limit - currentCount);
            for (int i = 0; i < spawnCount; i++)
            {
                if (!TrySpawnOne(state, category))
                    break;
            }

            SetNextSpawnAt(state, category, -1f);
            if (CountSpawned(state, category) < limit)
                ScheduleNextSpawn(state, category);
        }

        private void ScheduleNextSpawn(RuntimeZoneState state, SpawnCategory category)
        {
            Vector2 cooldown = state.Rule.respawnCooldownRange;
            float delay = Random.Range(Mathf.Min(cooldown.x, cooldown.y), Mathf.Max(cooldown.x, cooldown.y));
            SetNextSpawnAt(state, category, Time.time + Mathf.Max(0.1f, delay));
        }

        private bool TrySpawnOne(RuntimeZoneState state, SpawnCategory category)
        {
            ZoneSpawnRule rule = state.Rule;
            if (rule.zone == null)
                return false;

            GameObject[] prefabs = GetPrefabs(rule, category);
            if (!HasValidPrefab(prefabs))
                return false;

            int attempts = Mathf.Max(1, rule.maximumPlacementAttempts);
            for (int i = 0; i < attempts; i++)
            {
                if (!rule.zone.TryGetRandomPoint(out Vector3 position))
                    return false;
                float minimumDistance = category == SpawnCategory.Mine
                    ? rule.minimumMineSpawnDistance
                    : rule.minimumSpawnDistance;
                if (IsTooCloseToExisting(position, minimumDistance, category))
                    continue;

                GameObject prefab = PickRandomPrefab(prefabs, out int sourceIndex);
                if (prefab == null)
                    return false;

                LSO_OreSO oreDefinition = category == SpawnCategory.Ore
                    ? PickRandomOreDefinition(rule.internalOreSOs, rule.mineralValueBias)
                    : null;
                float scale = category == SpawnCategory.Ore
                    ? Random.Range(
                        Mathf.Min(rule.minimumStoneScale, rule.maximumStoneScale),
                        Mathf.Max(rule.minimumStoneScale, rule.maximumStoneScale))
                    : Random.Range(
                        Mathf.Min(rule.minimumMineScale, rule.maximumMineScale),
                        Mathf.Max(rule.minimumMineScale, rule.maximumMineScale));

                Transform parent = category == SpawnCategory.Ore
                    ? state.OreContainer
                    : state.MineContainer;
                GameObject instance = Instantiate(prefab, position, Random.rotation, parent);
                instance.transform.localScale = Vector3.one * scale;
                instance.SetActive(true);

                if (category == SpawnCategory.Ore)
                    ConfigureStone(instance, oreDefinition, rule);
                else
                    ConfigureMine(instance);

                state.Spawned[instance] = new SpawnMetadata
                {
                    SourceIndex = sourceIndex,
                    OreDefinition = oreDefinition,
                    Category = category
                };
                return true;
            }

            return false;
        }

        private bool IsTooCloseToExisting(
            Vector3 position,
            float minimumDistance,
            SpawnCategory category)
        {
            if (minimumDistance <= 0f)
                return false;

            float squaredDistance = minimumDistance * minimumDistance;
            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                RuntimeZoneState state = _runtimeStates[i];
                PruneDestroyedObjects(state);
                foreach (KeyValuePair<GameObject, SpawnMetadata> pair in state.Spawned)
                {
                    // Mines use their own spacing. Stones must not prevent the hazard
                    // population from being created in Classified/TopSecret zones.
                    if (category == SpawnCategory.Mine && pair.Value.Category != SpawnCategory.Mine)
                        continue;

                    GameObject spawnedObject = pair.Key;
                    if (spawnedObject != null &&
                        (spawnedObject.transform.position - position).sqrMagnitude < squaredDistance)
                        return true;
                }
            }

            return false;
        }

        private void SaveCurrentPopulation()
        {
            var savedSpawner = new SavedSpawnerState();
            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                RuntimeZoneState runtime = _runtimeStates[i];
                PruneDestroyedObjects(runtime);
                var savedZone = new SavedZoneState
                {
                    RemainingOreCooldown = GetRemainingCooldown(runtime.NextOreSpawnAt),
                    RemainingMineCooldown = GetRemainingCooldown(runtime.NextMineSpawnAt)
                };

                foreach (KeyValuePair<GameObject, SpawnMetadata> pair in runtime.Spawned)
                {
                    GameObject instance = pair.Key;
                    SpawnMetadata metadata = pair.Value;
                    if (instance == null)
                        continue;

                    savedZone.Spawns.Add(new SavedSpawnRecord
                    {
                        SourceIndex = metadata.SourceIndex,
                        OreDefinition = metadata.OreDefinition,
                        Category = metadata.Category,
                        Position = instance.transform.position,
                        Rotation = instance.transform.rotation,
                        Scale = instance.transform.localScale
                    });
                }

                savedSpawner.Zones[runtime.Rule.zoneType] = savedZone;
            }

            SavedStates[GetStateKey()] = savedSpawner;
        }

        private bool TryRestoreSavedPopulation()
        {
            if (!SavedStates.TryGetValue(GetStateKey(), out SavedSpawnerState savedSpawner))
                return false;

            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                RuntimeZoneState runtime = _runtimeStates[i];
                if (!savedSpawner.Zones.TryGetValue(runtime.Rule.zoneType, out SavedZoneState savedZone))
                    continue;

                runtime.NextOreSpawnAt = RestoreCooldown(savedZone.RemainingOreCooldown);
                runtime.NextMineSpawnAt = RestoreCooldown(savedZone.RemainingMineCooldown);

                for (int j = 0; j < savedZone.Spawns.Count; j++)
                {
                    SavedSpawnRecord record = savedZone.Spawns[j];
                    GameObject[] currentPrefabs = GetPrefabs(runtime.Rule, record.Category);
                    GameObject sourcePrefab = GetPrefabAt(currentPrefabs, record.SourceIndex);
                    if (sourcePrefab == null)
                        continue;

                    Transform parent = record.Category == SpawnCategory.Ore
                        ? runtime.OreContainer
                        : runtime.MineContainer;
                    GameObject instance = Instantiate(
                        sourcePrefab,
                        record.Position,
                        record.Rotation,
                        parent);
                    instance.transform.localScale = record.Scale;
                    instance.SetActive(true);

                    if (record.Category == SpawnCategory.Ore)
                        ConfigureStone(instance, record.OreDefinition, runtime.Rule);
                    else
                        ConfigureMine(instance);

                    runtime.Spawned[instance] = new SpawnMetadata
                    {
                        SourceIndex = record.SourceIndex,
                        OreDefinition = record.OreDefinition,
                        Category = record.Category
                    };
                }
            }

            return true;
        }

        private string GetStateKey()
        {
            string scenePath = gameObject.scene.path;
            return scenePath + "::" + persistenceKey;
        }

        private static float GetRemainingCooldown(float nextSpawnAt)
        {
            return nextSpawnAt < 0f ? -1f : Mathf.Max(0f, nextSpawnAt - Time.time);
        }

        private static float RestoreCooldown(float remainingCooldown)
        {
            return remainingCooldown < 0f ? -1f : Time.time + remainingCooldown;
        }

        private void ConfigureStone(
            GameObject instance,
            LSO_OreSO oreDefinition,
            ZoneSpawnRule rule)
        {
            instance.tag = "Stone";
            LSO_Ore ore = instance.GetComponent<LSO_Ore>() ?? instance.AddComponent<LSO_Ore>();
            ore.oreSO = oreDefinition;
            ore.SetWorldOreTemplates(rule.externalOreTemplates);

            OreContents contents = instance.GetComponent<OreContents>() ??
                                   instance.AddComponent<OreContents>();
            contents.SetInternalOreSO(oreDefinition);

            MineableAsteroid mineable = instance.GetComponent<MineableAsteroid>() ??
                                       instance.AddComponent<MineableAsteroid>();
            mineable.ConfigureOre(ore);

            RadarTarget radarTarget = instance.GetComponent<RadarTarget>() ??
                                      instance.AddComponent<RadarTarget>();
            radarTarget.Configure(stoneRadarIcon, GetStoneRadarColor(rule.zoneType));

            ScatterExternalOres(instance, contents, rule, oreDefinition);
        }

        private static Color GetStoneRadarColor(ZoneType zoneType)
        {
            return zoneType switch
            {
                ZoneType.Normal => new Color(0f, 1f, 0f, 1f),
                ZoneType.Classified => new Color(1f, 1f, 0f, 1f),
                ZoneType.TopSecret => new Color(1f, 0f, 0f, 1f),
                _ => Color.white
            };
        }

        private void ConfigureMine(GameObject instance)
        {
            if (instance.GetComponentInChildren<Collider>(true) == null)
            {
                SphereCollider fallbackCollider = instance.AddComponent<SphereCollider>();
                fallbackCollider.radius = 0.98f;
                fallbackCollider.isTrigger = false;
            }

            SpaceMine mine = instance.GetComponent<SpaceMine>() ?? instance.AddComponent<SpaceMine>();
            mine.Configure(
                mineExplosionVfxPrefab,
                mineKnockbackForce,
                mineFuelDamage,
                mineExplosionVfxLifetime,
                mineExplosionVfxScale);
        }

        private static void ScatterExternalOres(
            GameObject stone,
            OreContents contents,
            ZoneSpawnRule rule,
            LSO_OreSO internalOre)
        {
            if (!HasValidPrefab(rule.externalOreTemplates))
                return;

            LSO_MineralSO fallbackMineral = internalOre != null ? internalOre.mineral : null;
            if (!HasValidMineral(rule.externalMinerals) && fallbackMineral == null)
                return;

            SphereCollider sphere = stone.GetComponent<SphereCollider>() ??
                                    stone.GetComponentInChildren<SphereCollider>();
            Collider stoneCollider = sphere != null
                ? sphere
                : stone.GetComponentInChildren<Collider>();
            if (stoneCollider == null)
                return;

            int minimumCount = Mathf.Max(0, rule.minimumExternalOreCount);
            int maximumCount = Mathf.Max(minimumCount, rule.maximumExternalOreCount);
            int count = Random.Range(minimumCount, maximumCount + 1);
            var directions = new List<Vector3>(count);
            float minimumAngle = Mathf.Clamp(rule.minimumExternalOreAngle, 0f, 180f);
            float cosineLimit = Mathf.Cos(minimumAngle * Mathf.Deg2Rad);

            for (int i = 0; i < count; i++)
            {
                if (!TryFindSurfaceDirection(directions, cosineLimit, out Vector3 direction))
                    break;

                GameObject template = PickRandomPrefab(rule.externalOreTemplates);
                LSO_MineralSO mineral = PickRandomMineral(
                    rule.externalMinerals,
                    rule.mineralValueBias) ?? fallbackMineral;
                if (template == null || mineral == null)
                    continue;

                directions.Add(direction);
                GetSurface(stoneCollider, sphere, direction, out Vector3 center, out float radius);
                Vector3 position = center + direction * (radius + rule.externalOreSurfaceOffset);
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, direction) *
                                      Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up);
                GameObject externalOre = Instantiate(template, position, rotation);
                externalOre.SetActive(true);
                float scaleRatio = Random.Range(
                    Mathf.Min(rule.minimumExternalOreScale, rule.maximumExternalOreScale),
                    Mathf.Max(rule.minimumExternalOreScale, rule.maximumExternalOreScale));
                externalOre.transform.localScale = Abs(stone.transform.lossyScale) * scaleRatio;
                externalOre.transform.SetParent(stone.transform, true);
                externalOre.name = $"External {mineral.mineralName} Ore";
                externalOre.tag = "Ore";
                SetLayerRecursively(externalOre.transform, stone.layer);

                MineralPickup pickup = externalOre.GetComponent<MineralPickup>() ??
                                       externalOre.AddComponent<MineralPickup>();
                pickup.Initialize(mineral, 1, false);
                MineableAsteroid mineable = externalOre.GetComponent<MineableAsteroid>() ??
                                           externalOre.AddComponent<MineableAsteroid>();
                mineable.InitializeAsLooseMineral(mineral, 1);
                contents.RegisterExternalOre(mineral, externalOre.transform);
            }
        }

        private static void GetSurface(
            Collider targetCollider,
            SphereCollider sphere,
            Vector3 direction,
            out Vector3 center,
            out float radius)
        {
            if (sphere != null)
            {
                center = sphere.transform.TransformPoint(sphere.center);
                Vector3 scale = Abs(sphere.transform.lossyScale);
                radius = sphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                return;
            }

            Bounds bounds = targetCollider.bounds;
            center = bounds.center;
            Vector3 absoluteDirection = Abs(direction);
            radius = Vector3.Dot(bounds.extents, absoluteDirection);
        }

        private static bool TryFindSurfaceDirection(
            List<Vector3> placedDirections,
            float cosineLimit,
            out Vector3 direction)
        {
            const int maximumAttempts = 100;
            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                Vector3 candidate = Random.onUnitSphere;
                bool tooClose = false;
                for (int i = 0; i < placedDirections.Count; i++)
                {
                    if (Vector3.Dot(candidate, placedDirections[i]) <= cosineLimit)
                        continue;
                    tooClose = true;
                    break;
                }

                if (tooClose)
                    continue;

                direction = candidate;
                return true;
            }

            direction = Vector3.up;
            return false;
        }

        private void HideSceneTemplates()
        {
            var templates = new HashSet<GameObject>();
            for (int i = 0; i < zoneRules.Count; i++)
            {
                AddSceneTemplates(templates, zoneRules[i].stonePrefabs);
                AddSceneTemplates(templates, zoneRules[i].minePrefabs);
                AddSceneTemplates(templates, zoneRules[i].externalOreTemplates);
            }

            foreach (GameObject template in templates)
                template.SetActive(false);
        }

        private static void AddSceneTemplates(HashSet<GameObject> destination, GameObject[] templates)
        {
            if (templates == null)
                return;

            for (int i = 0; i < templates.Length; i++)
            {
                GameObject template = templates[i];
                if (template != null && template.scene.IsValid())
                    destination.Add(template);
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private RuntimeZoneState FindRuntimeState(ZoneType zoneType)
        {
            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                if (_runtimeStates[i].Rule.zoneType == zoneType)
                    return _runtimeStates[i];
            }

            return null;
        }

        private static void PruneDestroyedObjects(RuntimeZoneState state)
        {
            if (state.Spawned.Count == 0)
                return;

            var missingObjects = new List<GameObject>();
            foreach (GameObject spawnedObject in state.Spawned.Keys)
            {
                if (spawnedObject == null)
                    missingObjects.Add(spawnedObject);
            }

            for (int i = 0; i < missingObjects.Count; i++)
                state.Spawned.Remove(missingObjects[i]);
        }

        private static int CountSpawned(RuntimeZoneState state, SpawnCategory category)
        {
            int count = 0;
            foreach (SpawnMetadata metadata in state.Spawned.Values)
            {
                if (metadata.Category == category)
                    count++;
            }

            return count;
        }

        private static int GetLimit(ZoneSpawnRule rule, SpawnCategory category)
        {
            return category == SpawnCategory.Ore
                ? Mathf.Max(0, rule.stoneLimit)
                : Mathf.Max(0, rule.mineLimit);
        }

        private static int GetBatchSize(ZoneSpawnRule rule, SpawnCategory category)
        {
            return category == SpawnCategory.Ore
                ? Mathf.Max(1, rule.stoneRespawnBatchSize)
                : Mathf.Max(1, rule.mineRespawnBatchSize);
        }

        private static GameObject[] GetPrefabs(ZoneSpawnRule rule, SpawnCategory category)
        {
            return category == SpawnCategory.Ore ? rule.stonePrefabs : rule.minePrefabs;
        }

        private static float GetNextSpawnAt(RuntimeZoneState state, SpawnCategory category)
        {
            return category == SpawnCategory.Ore ? state.NextOreSpawnAt : state.NextMineSpawnAt;
        }

        private static void SetNextSpawnAt(
            RuntimeZoneState state,
            SpawnCategory category,
            float nextSpawnAt)
        {
            if (category == SpawnCategory.Ore)
                state.NextOreSpawnAt = nextSpawnAt;
            else
                state.NextMineSpawnAt = nextSpawnAt;
        }

        private static bool HasValidPrefab(GameObject[] prefabs)
        {
            if (prefabs == null)
                return false;

            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null)
                    return true;
            }

            return false;
        }

        private static GameObject PickRandomPrefab(GameObject[] prefabs)
        {
            return PickRandomPrefab(prefabs, out _);
        }

        private static GameObject PickRandomPrefab(GameObject[] prefabs, out int sourceIndex)
        {
            sourceIndex = -1;
            int validCount = 0;
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] != null)
                    validCount++;
            }

            if (validCount == 0)
                return null;

            int selectedIndex = Random.Range(0, validCount);
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null)
                    continue;
                if (selectedIndex-- == 0)
                {
                    sourceIndex = i;
                    return prefabs[i];
                }
            }

            return null;
        }

        private static GameObject GetPrefabAt(GameObject[] prefabs, int index)
        {
            return prefabs != null && index >= 0 && index < prefabs.Length
                ? prefabs[index]
                : null;
        }

        private static LSO_OreSO PickRandomOreDefinition(
            LSO_OreSO[] definitions,
            float valueBias)
        {
            if (definitions == null)
                return null;

            float totalWeight = 0f;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null)
                    totalWeight += GetMineralValueWeight(definitions[i].mineral, valueBias);
            }

            if (totalWeight <= 0f)
                return null;

            float selectedWeight = Random.value * totalWeight;
            LSO_OreSO lastValid = null;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] == null)
                    continue;
                lastValid = definitions[i];
                selectedWeight -= GetMineralValueWeight(definitions[i].mineral, valueBias);
                if (selectedWeight <= 0f)
                    return definitions[i];
            }

            return lastValid;
        }

        private static bool HasValidMineral(LSO_MineralSO[] minerals)
        {
            if (minerals == null)
                return false;
            for (int i = 0; i < minerals.Length; i++)
                if (minerals[i] != null)
                    return true;
            return false;
        }

        private static LSO_MineralSO PickRandomMineral(
            LSO_MineralSO[] minerals,
            float valueBias)
        {
            if (!HasValidMineral(minerals))
                return null;

            float totalWeight = 0f;
            for (int i = 0; i < minerals.Length; i++)
                if (minerals[i] != null)
                    totalWeight += GetMineralValueWeight(minerals[i], valueBias);

            float selectedWeight = Random.value * totalWeight;
            LSO_MineralSO lastValid = null;
            for (int i = 0; i < minerals.Length; i++)
            {
                if (minerals[i] == null)
                    continue;
                lastValid = minerals[i];
                selectedWeight -= GetMineralValueWeight(minerals[i], valueBias);
                if (selectedWeight <= 0f)
                    return minerals[i];
            }

            return lastValid;
        }

        private static float GetMineralValueWeight(LSO_MineralSO mineral, float valueBias)
        {
            if (mineral == null)
                return 0f;

            float clampedBias = Mathf.Clamp(valueBias, 0f, 3f);
            if (clampedBias <= 0f)
                return 1f;

            float normalizedPrice = Mathf.Max(0.1f, mineral.PricePerKilogram / 100f);
            return Mathf.Pow(normalizedPrice, clampedBias);
        }

        private static void NormalizeRule(ZoneSpawnRule rule)
        {
            rule.stoneLimit = Mathf.Max(0, rule.stoneLimit);
            rule.mineLimit = Mathf.Max(0, rule.mineLimit);
            rule.stoneRespawnBatchSize = Mathf.Max(1, rule.stoneRespawnBatchSize);
            rule.mineRespawnBatchSize = Mathf.Max(1, rule.mineRespawnBatchSize);
            rule.minimumStoneScale = Mathf.Max(0.01f, rule.minimumStoneScale);
            rule.maximumStoneScale = Mathf.Max(0.01f, rule.maximumStoneScale);
            rule.minimumExternalOreCount = Mathf.Max(0, rule.minimumExternalOreCount);
            rule.maximumExternalOreCount = Mathf.Max(
                rule.minimumExternalOreCount,
                rule.maximumExternalOreCount);
            rule.minimumExternalOreScale = Mathf.Max(0.01f, rule.minimumExternalOreScale);
            rule.maximumExternalOreScale = Mathf.Max(0.01f, rule.maximumExternalOreScale);
            rule.externalOreSurfaceOffset = Mathf.Max(0f, rule.externalOreSurfaceOffset);
            rule.minimumExternalOreAngle = Mathf.Clamp(rule.minimumExternalOreAngle, 0f, 180f);
            rule.minimumMineScale = Mathf.Max(0.01f, rule.minimumMineScale);
            rule.maximumMineScale = Mathf.Max(0.01f, rule.maximumMineScale);
            rule.minimumMineSpawnDistance = Mathf.Max(0f, rule.minimumMineSpawnDistance);
            rule.minimumSpawnDistance = Mathf.Max(0f, rule.minimumSpawnDistance);
            rule.maximumPlacementAttempts = Mathf.Max(1, rule.maximumPlacementAttempts);
            rule.respawnCooldownRange.x = Mathf.Max(0.1f, rule.respawnCooldownRange.x);
            rule.respawnCooldownRange.y = Mathf.Max(0.1f, rule.respawnCooldownRange.y);
        }
    }
}
