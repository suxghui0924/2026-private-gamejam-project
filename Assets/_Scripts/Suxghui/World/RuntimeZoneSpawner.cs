using System;
using System.Collections.Generic;
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
            [FormerlySerializedAs("oreLimit"), Min(0)] public int stoneLimit = 30;
            [FormerlySerializedAs("oreRespawnBatchSize"), Min(1)]
            public int stoneRespawnBatchSize = 1;
            [FormerlySerializedAs("minimumOreScale"), Min(0.01f)]
            public float minimumStoneScale = 200f;
            [FormerlySerializedAs("maximumOreScale"), Min(0.01f)]
            public float maximumStoneScale = 300f;

            [Header("Mine Population")]
            public GameObject[] minePrefabs = Array.Empty<GameObject>();
            [Min(0)] public int mineLimit;
            [Min(1)] public int mineRespawnBatchSize = 1;
            [Min(0.01f)] public float minimumMineScale = 1f;
            [Min(0.01f)] public float maximumMineScale = 1.5f;

            [Header("Zone Respawn")]
            [Tooltip("This range is used independently for ore and mine respawn timers.")]
            public Vector2 respawnCooldownRange = new Vector2(10f, 20f);
            [Min(0f)] public float minimumSpawnDistance = 40f;
            [Min(1)] public int maximumPlacementAttempts = 100;
        }

        private sealed class SpawnMetadata
        {
            public GameObject SourcePrefab;
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
            public GameObject SourcePrefab;
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

        [Header("Zone Rules")]
        [SerializeField] private List<ZoneSpawnRule> zoneRules = new List<ZoneSpawnRule>
        {
            new ZoneSpawnRule
            {
                displayName = "Normal Zone",
                zoneType = ZoneType.Normal,
                stoneLimit = 35,
                minimumStoneScale = 200f,
                maximumStoneScale = 300f,
                mineLimit = 0,
                respawnCooldownRange = new Vector2(10f, 12f),
                minimumSpawnDistance = 50f
            },
            new ZoneSpawnRule
            {
                displayName = "Classified Zone",
                zoneType = ZoneType.Classified,
                stoneLimit = 25,
                minimumStoneScale = 150f,
                maximumStoneScale = 250f,
                mineLimit = 25,
                respawnCooldownRange = new Vector2(13f, 16f),
                minimumSpawnDistance = 45f
            },
            new ZoneSpawnRule
            {
                displayName = "Top Secret Zone",
                zoneType = ZoneType.TopSecret,
                stoneLimit = 20,
                minimumStoneScale = 100f,
                maximumStoneScale = 200f,
                mineLimit = 35,
                respawnCooldownRange = new Vector2(17f, 20f),
                minimumSpawnDistance = 40f
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
            SpawnMissingImmediately(state, SpawnCategory.Ore);
            SpawnMissingImmediately(state, SpawnCategory.Mine);
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
                if (IsTooCloseToExisting(position, rule.minimumSpawnDistance))
                    continue;

                GameObject prefab = PickRandomPrefab(prefabs);
                if (prefab == null)
                    return false;

                LSO_OreSO oreDefinition = category == SpawnCategory.Ore
                    ? PickRandomOreDefinition(rule.internalOreSOs)
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

                if (category == SpawnCategory.Ore)
                    ConfigureStone(instance, oreDefinition);

                state.Spawned[instance] = new SpawnMetadata
                {
                    SourcePrefab = prefab,
                    OreDefinition = oreDefinition,
                    Category = category
                };
                return true;
            }

            return false;
        }

        private bool IsTooCloseToExisting(Vector3 position, float minimumDistance)
        {
            if (minimumDistance <= 0f)
                return false;

            float squaredDistance = minimumDistance * minimumDistance;
            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                RuntimeZoneState state = _runtimeStates[i];
                PruneDestroyedObjects(state);
                foreach (GameObject spawnedObject in state.Spawned.Keys)
                {
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
                    if (instance == null || metadata.SourcePrefab == null)
                        continue;

                    savedZone.Spawns.Add(new SavedSpawnRecord
                    {
                        SourcePrefab = metadata.SourcePrefab,
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
                    if (record.SourcePrefab == null)
                        continue;

                    Transform parent = record.Category == SpawnCategory.Ore
                        ? runtime.OreContainer
                        : runtime.MineContainer;
                    GameObject instance = Instantiate(
                        record.SourcePrefab,
                        record.Position,
                        record.Rotation,
                        parent);
                    instance.transform.localScale = record.Scale;

                    if (record.Category == SpawnCategory.Ore)
                        ConfigureStone(instance, record.OreDefinition);

                    runtime.Spawned[instance] = new SpawnMetadata
                    {
                        SourcePrefab = record.SourcePrefab,
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

        private static void ConfigureStone(GameObject instance, LSO_OreSO oreDefinition)
        {
            instance.tag = "Stone";
            if (oreDefinition == null)
                return;

            LSO_Ore ore = instance.GetComponent<LSO_Ore>() ?? instance.AddComponent<LSO_Ore>();
            ore.oreSO = oreDefinition;

            OreContents contents = instance.GetComponent<OreContents>() ??
                                   instance.AddComponent<OreContents>();
            contents.SetInternalOreSO(oreDefinition);

            MineableAsteroid mineable = instance.GetComponent<MineableAsteroid>();
            if (mineable != null)
                mineable.ConfigureOre(ore);
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
                    return prefabs[i];
            }

            return null;
        }

        private static LSO_OreSO PickRandomOreDefinition(LSO_OreSO[] definitions)
        {
            if (definitions == null)
                return null;

            int validCount = 0;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] != null)
                    validCount++;
            }

            if (validCount == 0)
                return null;

            int selectedIndex = Random.Range(0, validCount);
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i] == null)
                    continue;
                if (selectedIndex-- == 0)
                    return definitions[i];
            }

            return null;
        }

        private static void NormalizeRule(ZoneSpawnRule rule)
        {
            rule.stoneLimit = Mathf.Max(0, rule.stoneLimit);
            rule.mineLimit = Mathf.Max(0, rule.mineLimit);
            rule.stoneRespawnBatchSize = Mathf.Max(1, rule.stoneRespawnBatchSize);
            rule.mineRespawnBatchSize = Mathf.Max(1, rule.mineRespawnBatchSize);
            rule.minimumStoneScale = Mathf.Max(0.01f, rule.minimumStoneScale);
            rule.maximumStoneScale = Mathf.Max(0.01f, rule.maximumStoneScale);
            rule.minimumMineScale = Mathf.Max(0.01f, rule.minimumMineScale);
            rule.maximumMineScale = Mathf.Max(0.01f, rule.maximumMineScale);
            rule.minimumSpawnDistance = Mathf.Max(0f, rule.minimumSpawnDistance);
            rule.maximumPlacementAttempts = Mathf.Max(1, rule.maximumPlacementAttempts);
            rule.respawnCooldownRange.x = Mathf.Max(0.1f, rule.respawnCooldownRange.x);
            rule.respawnCooldownRange.y = Mathf.Max(0.1f, rule.respawnCooldownRange.y);
        }
    }
}
