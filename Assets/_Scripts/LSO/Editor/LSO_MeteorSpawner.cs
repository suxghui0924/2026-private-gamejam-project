using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace _Scripts.LSO.Editor
{
    /// <summary>
    /// 지정한 범위 안에 메테오 프리팹을 랜덤하게 배치하는 에디터 툴.
    /// 반드시 Assets 하위의 "Editor" 폴더에 두어야 한다.
    /// 메뉴: Tools > LSO > Meteor Spawner
    /// </summary>
    public class LSO_MeteorSpawner : EditorWindow
    {
        private enum SpawnMode
        {
            /// <summary>범위 전체(3차원 부피)에 흩뿌린다. 공중에 떠 있는 운석용.</summary>
            Volume,

            /// <summary>범위 위에서 아래로 레이를 쏴 바닥에 붙인다. 지면에 박힌 운석용.</summary>
            Surface
        }

        private enum CountMode { Density, FixedCount }

        private enum ScaleMode
        {
            /// <summary>랜덤 값을 그대로 스케일로 쓴다. 결과가 항상 균일하다.</summary>
            Absolute,

            /// <summary>프리팹 원본 스케일에 랜덤 값을 곱한다. 원본이 비균일하면 결과도 비균일해진다.</summary>
            MultiplyPrefabScale
        }

        // ───────── 설정 ─────────

        [SerializeField] private GameObject[] meteorPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject range;
        [SerializeField] private Transform parentOverride;

        [SerializeField] private SpawnMode spawnMode = SpawnMode.Volume;
        [SerializeField] private CountMode countMode = CountMode.Density;

        [SerializeField] private float density = 0.05f;
        [SerializeField] private int fixedCount = 50;
        [SerializeField] private int maxCount = 2000;

        [SerializeField] private float minDistance = 1f;

        [SerializeField] private ScaleMode scaleMode = ScaleMode.Absolute;
        [SerializeField] private bool uniformScale = true;
        [SerializeField] private float scaleMin = 0.7f;
        [SerializeField] private float scaleMax = 1.5f;
        [SerializeField] private Vector3 scaleMinAxis = new Vector3(0.7f, 0.7f, 0.7f);
        [SerializeField] private Vector3 scaleMaxAxis = new Vector3(1.5f, 1.5f, 1.5f);

        [SerializeField] private bool randomRotation = true;
        [SerializeField] private bool yAxisOnly;

        [SerializeField] private LayerMask surfaceMask = ~0;
        [SerializeField] private bool alignToSurfaceNormal;

        [SerializeField] private bool useSeed;
        [SerializeField] private int seed = 12345;

        [SerializeField] private string containerName = "Meteors";

        private SerializedObject _so;
        private SerializedProperty _prefabsProp;
        private Vector2 _scroll;

        [MenuItem("Tools/LSO/Meteor Spawner")]
        private static void Open()
        {
            var window = GetWindow<LSO_MeteorSpawner>(false, "Meteor Spawner");
            window.minSize = new Vector2(340f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            _so = new SerializedObject(this);
            _prefabsProp = _so.FindProperty(nameof(meteorPrefabs));
        }

        // ───────── GUI ─────────

        private void OnGUI()
        {
            _so.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("대상", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_prefabsProp, new GUIContent("메테오 프리팹"), true);
            range = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("범위", "BoxCollider / SphereCollider / Renderer 를 기준으로 영역을 잡는다."),
                range, typeof(GameObject), true);
            parentOverride = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("부모 (비우면 씬 루트)",
                    "스케일이 걸린 오브젝트를 부모로 지정하면 메테오가 눌릴 수 있다. 비워두는 편이 안전하다."),
                parentOverride, typeof(Transform), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("배치", EditorStyles.boldLabel);
            spawnMode = (SpawnMode)EditorGUILayout.EnumPopup("방식", spawnMode);
            countMode = (CountMode)EditorGUILayout.EnumPopup("개수 결정", countMode);

            EditorGUI.indentLevel++;
            if (countMode == CountMode.Density)
            {
                string unit = spawnMode == SpawnMode.Volume ? "개/㎥" : "개/㎡";
                density = EditorGUILayout.Slider($"밀도 ({unit})", density, 0.0001f, 2f);
                maxCount = EditorGUILayout.IntField(
                    new GUIContent("최대 개수", "밀도로 계산된 값이 이 수를 넘지 않게 자른다. 실수로 수만 개를 만드는 걸 막는다."),
                    maxCount);
            }
            else
            {
                fixedCount = Mathf.Max(0, EditorGUILayout.IntField("개수", fixedCount));
            }
            EditorGUI.indentLevel--;

            minDistance = Mathf.Max(0f, EditorGUILayout.FloatField(
                new GUIContent("최소 간격", "이 거리보다 가까우면 다시 뽑는다. 0이면 겹침을 허용한다."),
                minDistance));

            if (spawnMode == SpawnMode.Surface)
            {
                EditorGUI.indentLevel++;
                surfaceMask = LSO_LayerMaskField("바닥 레이어", surfaceMask);
                alignToSurfaceNormal = EditorGUILayout.Toggle(
                    new GUIContent("바닥 기울기에 맞춤"), alignToSurfaceNormal);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("크기", EditorStyles.boldLabel);
            scaleMode = (ScaleMode)EditorGUILayout.EnumPopup(
                new GUIContent("적용 방식",
                    "Absolute: 랜덤 값을 그대로 스케일로 사용한다. 결과가 항상 균일하다.\n" +
                    "MultiplyPrefabScale: 프리팹 원본 스케일에 곱한다. 원본 비율이 유지된다."),
                scaleMode);

            uniformScale = EditorGUILayout.Toggle(
                new GUIContent("균일 스케일", "XYZ에 같은 값을 적용해 비율을 유지한다."), uniformScale);

            // 곱하기 모드에서 원본이 비균일하면 균일 스케일을 켜도 결과가 찌그러진다.
            if (uniformScale && scaleMode == ScaleMode.MultiplyPrefabScale)
            {
                GameObject skewed = FindNonUniformPrefab();
                if (skewed != null)
                {
                    Vector3 s = skewed.transform.localScale;
                    EditorGUILayout.HelpBox(
                        $"'{skewed.name}' 프리팹의 원본 Scale이 비균일합니다 ({s.x:0.##}, {s.y:0.##}, {s.z:0.##}).\n" +
                        "곱하기 모드에서는 균일 스케일을 켜도 결과가 찌그러집니다. " +
                        "적용 방식을 Absolute로 바꾸거나 프리팹의 Scale을 (1, 1, 1)로 맞추세요.",
                        MessageType.Warning);
                }
            }
            if (uniformScale)
            {
                scaleMin = EditorGUILayout.FloatField("Min", scaleMin);
                scaleMax = EditorGUILayout.FloatField("Max", scaleMax);
            }
            else
            {
                scaleMinAxis = EditorGUILayout.Vector3Field("Min", scaleMinAxis);
                scaleMaxAxis = EditorGUILayout.Vector3Field("Max", scaleMaxAxis);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("회전", EditorStyles.boldLabel);
            randomRotation = EditorGUILayout.Toggle("랜덤 회전", randomRotation);
            using (new EditorGUI.DisabledScope(!randomRotation))
            {
                EditorGUI.indentLevel++;
                yAxisOnly = EditorGUILayout.Toggle(
                    new GUIContent("Y축만", "바닥에 놓이는 오브젝트라면 켜는 편이 자연스럽다."), yAxisOnly);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("기타", EditorStyles.boldLabel);
            containerName = EditorGUILayout.TextField(
                new GUIContent("컨테이너 이름", "생성된 오브젝트를 담을 부모. 정리와 일괄 삭제에 쓴다."), containerName);
            useSeed = EditorGUILayout.Toggle(
                new GUIContent("시드 고정", "같은 배치를 반복 재현하고 싶을 때."), useSeed);
            using (new EditorGUI.DisabledScope(!useSeed))
            {
                EditorGUI.indentLevel++;
                seed = EditorGUILayout.IntField("Seed", seed);
                EditorGUI.indentLevel--;
            }

            // ── 요약 & 버튼 ──
            EditorGUILayout.Space();
            _so.ApplyModifiedProperties();

            string error = Validate();
            if (error != null)
            {
                EditorGUILayout.HelpBox(error, MessageType.Warning);
            }
            else
            {
                int count = GetSpawnCount();
                float size = spawnMode == SpawnMode.Volume ? GetRangeVolume() : GetRangeArea();
                string unit = spawnMode == SpawnMode.Volume ? "㎥" : "㎡";
                EditorGUILayout.HelpBox($"범위 {size:0.#}{unit} · 생성 예정 {count}개", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(error != null))
            {
                if (GUILayout.Button("생성", GUILayout.Height(32f))) Spawn();
            }

            using (new EditorGUI.DisabledScope(range == null))
            {
                if (GUILayout.Button("생성된 메테오 전부 삭제")) ClearSpawned();
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnSelectionChange()
        {
            // 선택한 오브젝트를 범위로 바로 쓸 수 있게 편의 제공
            if (range == null && Selection.activeGameObject != null)
            {
                range = Selection.activeGameObject;
                Repaint();
            }
        }

        private string Validate()
        {
            if (range == null) return "범위 오브젝트를 지정하세요.";

            bool hasPrefab = false;
            foreach (var p in meteorPrefabs) if (p != null) { hasPrefab = true; break; }
            if (!hasPrefab) return "메테오 프리팹을 1개 이상 지정하세요.";

            return null;
        }

        // ───────── 생성 ─────────

        private void Spawn()
        {
            int targetCount = GetSpawnCount();
            if (targetCount <= 0) return;

            Random.State previousState = default;
            if (useSeed)
            {
                previousState = Random.state;
                Random.InitState(seed);
            }

            Transform container = GetOrCreateContainer();

            var placed = new List<Vector3>(targetCount);
            float sqrMinDistance = minDistance * minDistance;

            // 최소 간격 때문에 자리를 못 찾고 무한 반복하는 걸 막는다.
            int maxAttempts = targetCount * 30;
            int attempts = 0;
            int created = 0;

            while (created < targetCount && attempts < maxAttempts)
            {
                attempts++;

                if (!TryGetSpawnPoint(out Vector3 pos, out Vector3 normal)) continue;

                // 최소 간격 검사
                if (minDistance > 0f)
                {
                    bool tooClose = false;
                    foreach (var t in placed)
                    {
                        if ((t - pos).sqrMagnitude < sqrMinDistance) { tooClose = true; break; }
                    }
                    if (tooClose) continue;
                }

                GameObject prefab = PickPrefab();
                if (prefab == null) continue;

                // Instantiate 가 아니라 PrefabUtility 를 써야 프리팹 연결이 유지된다.
                // 일반 Instantiate 로 만들면 원본을 수정해도 씬의 오브젝트에 반영되지 않는다.
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
                if (instance == null) continue;

                instance.transform.position = pos;
                instance.transform.rotation = GetRandomRotation(normal);

                Vector3 randomScale = GetRandomScale();
                Vector3 worldScale = scaleMode == ScaleMode.MultiplyPrefabScale
                    ? Vector3.Scale(prefab.transform.localScale, randomScale)
                    : randomScale;

                // localScale은 부모 기준이다. 부모에 비균일 스케일이 걸려 있으면
                // 균일한 값을 넣어도 화면에서는 눌려 보이므로 나눠서 상쇄한다.
                instance.transform.localScale = CompensateParentScale(worldScale, container);

                Undo.RegisterCreatedObjectUndo(instance, "Spawn Meteors");

                placed.Add(pos);
                created++;
            }

            if (useSeed) Random.state = previousState;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            if (created < targetCount)
            {
                Debug.LogWarning(
                    $"[MeteorSpawner] {targetCount}개 중 {created}개만 배치했습니다. " +
                    $"최소 간격({minDistance})이 범위에 비해 너무 크거나, " +
                    (spawnMode == SpawnMode.Surface ? "바닥 레이어에 맞는 콜라이더가 없습니다." : "범위가 좁습니다."));
            }
            else
            {
                Debug.Log($"[MeteorSpawner] 메테오 {created}개를 배치했습니다.", container);
            }

            Selection.activeGameObject = container.gameObject;
        }

        private void ClearSpawned()
        {
            Transform container = FindContainer();
            if (container == null)
            {
                Debug.Log($"[MeteorSpawner] '{containerName}' 컨테이너가 없습니다.");
                return;
            }

            Undo.DestroyObjectImmediate(container.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // ───────── 위치 계산 ─────────

        private bool TryGetSpawnPoint(out Vector3 pos, out Vector3 normal)
        {
            normal = Vector3.up;

            if (!TryGetRandomPointInRange(out pos)) return false;
            if (spawnMode == SpawnMode.Volume) return true;

            // Surface: 범위 위쪽에서 아래로 레이를 쏜다.
            Bounds worldBounds = GetWorldBounds();
            Vector3 origin = new Vector3(pos.x, worldBounds.max.y + 1f, pos.z);
            float distance = worldBounds.size.y + 2f;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance, surfaceMask))
                return false;

            pos = hit.point;
            normal = hit.normal;
            return true;
        }

        private bool TryGetRandomPointInRange(out Vector3 point)
        {
            point = Vector3.zero;
            if (range == null) return false;

            Transform t = range.transform;

            // 로컬 공간에서 점을 뽑아 TransformPoint 로 변환해야
            // 회전된 범위도 정확히 처리된다.
            if (range.TryGetComponent(out SphereCollider sphere))
            {
                point = t.TransformPoint(sphere.center + Random.insideUnitSphere * sphere.radius);
                return true;
            }

            if (range.TryGetComponent(out BoxCollider box))
            {
                Vector3 half = box.size * 0.5f;
                Vector3 local = box.center + new Vector3(
                    Random.Range(-half.x, half.x),
                    Random.Range(-half.y, half.y),
                    Random.Range(-half.z, half.z));

                point = t.TransformPoint(local);
                return true;
            }

            if (range.TryGetComponent(out Renderer renderer))
            {
                // Renderer.bounds 는 월드 축 정렬(AABB)이라 회전이 반영되지 않는다.
                Bounds b = renderer.bounds;
                point = new Vector3(
                    Random.Range(b.min.x, b.max.x),
                    Random.Range(b.min.y, b.max.y),
                    Random.Range(b.min.z, b.max.z));
                return true;
            }

            // 콜라이더도 렌더러도 없으면 스케일을 1x1x1 큐브로 간주한다.
            Vector3 fallback = new Vector3(
                Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f));
            point = t.TransformPoint(fallback);
            return true;
        }

        private Bounds GetWorldBounds()
        {
            if (range == null) return new Bounds();

            if (range.TryGetComponent(out Collider col)) return col.bounds;
            if (range.TryGetComponent(out Renderer rend)) return rend.bounds;

            return new Bounds(range.transform.position, range.transform.lossyScale);
        }

        // ───────── 개수 / 부피 ─────────

        private int GetSpawnCount()
        {
            if (countMode == CountMode.FixedCount) return Mathf.Max(0, fixedCount);

            float size = spawnMode == SpawnMode.Volume ? GetRangeVolume() : GetRangeArea();
            return Mathf.Clamp(Mathf.RoundToInt(size * density), 0, Mathf.Max(0, maxCount));
        }

        private float GetRangeVolume()
        {
            if (range == null) return 0f;

            Vector3 scale = range.transform.lossyScale;

            if (range.TryGetComponent(out SphereCollider sphere))
            {
                float r = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                return 4f / 3f * Mathf.PI * r * r * r;
            }

            Vector3 size = GetRangeSize();
            return Mathf.Abs(size.x * size.y * size.z);
        }

        private float GetRangeArea()
        {
            Vector3 size = GetRangeSize();
            return Mathf.Abs(size.x * size.z);
        }

        private Vector3 GetRangeSize()
        {
            if (range == null) return Vector3.zero;

            Vector3 scale = range.transform.lossyScale;

            if (range.TryGetComponent(out SphereCollider sphere))
            {
                float r = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                return Vector3.one * (r * 2f);
            }

            if (range.TryGetComponent(out BoxCollider box)) return Vector3.Scale(box.size, scale);
            if (range.TryGetComponent(out Renderer renderer)) return renderer.bounds.size;

            return scale;
        }

        // ───────── 랜덤 값 ─────────

        private GameObject PickPrefab()
        {
            // null 항목이 섞여 있어도 안전하게 뽑는다.
            int validCount = 0;
            foreach (var p in meteorPrefabs) if (p != null) validCount++;
            if (validCount == 0) return null;

            int index = Random.Range(0, validCount);
            foreach (var p in meteorPrefabs)
            {
                if (p == null) continue;
                if (index-- == 0) return p;
            }
            return null;
        }

        private Vector3 GetRandomScale()
        {
            if (uniformScale)
            {
                float f = RandomRange(scaleMin, scaleMax);
                return new Vector3(f, f, f);
            }

            return new Vector3(
                RandomRange(scaleMinAxis.x, scaleMaxAxis.x),
                RandomRange(scaleMinAxis.y, scaleMaxAxis.y),
                RandomRange(scaleMinAxis.z, scaleMaxAxis.z));
        }

        private Quaternion GetRandomRotation(Vector3 normal)
        {
            if (!randomRotation)
                return alignToSurfaceNormal ? Quaternion.FromToRotation(Vector3.up, normal) : Quaternion.identity;

            if (alignToSurfaceNormal && spawnMode == SpawnMode.Surface)
            {
                // 바닥 기울기에 세운 뒤 그 축을 중심으로만 돌린다.
                return Quaternion.FromToRotation(Vector3.up, normal)
                       * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            return yAxisOnly
                ? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
                : Random.rotation;
        }

        /// <summary>min/max가 뒤바뀌어 입력돼도 안전하게 동작한다.</summary>
        private static float RandomRange(float a, float b)
            => Random.Range(Mathf.Min(a, b), Mathf.Max(a, b));

        /// <summary>부모의 스케일을 나눠서, 원하는 월드 크기가 그대로 나오도록 보정한다.</summary>
        private static Vector3 CompensateParentScale(Vector3 worldScale, Transform parent)
        {
            if (parent == null) return worldScale;

            Vector3 p = parent.lossyScale;

            return new Vector3(
                Mathf.Abs(p.x) < 0.0001f ? worldScale.x : worldScale.x / p.x,
                Mathf.Abs(p.y) < 0.0001f ? worldScale.y : worldScale.y / p.y,
                Mathf.Abs(p.z) < 0.0001f ? worldScale.z : worldScale.z / p.z);
        }

        /// <summary>원본 스케일이 균일하지 않은 프리팹을 하나 찾는다. 경고 표시용.</summary>
        private GameObject FindNonUniformPrefab()
        {
            foreach (var prefab in meteorPrefabs)
            {
                if (prefab == null) continue;

                Vector3 s = prefab.transform.localScale;
                if (!Mathf.Approximately(s.x, s.y) || !Mathf.Approximately(s.y, s.z))
                    return prefab;
            }
            return null;
        }

        // ───────── 컨테이너 ─────────

        private Transform FindContainer()
        {
            if (parentOverride != null) return parentOverride.Find(containerName);

            // 부모를 지정하지 않으면 씬 루트에서 찾는다.
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == containerName) return root.transform;

            return null;
        }

        private Transform GetOrCreateContainer()
        {
            Transform existing = FindContainer();
            if (existing != null) return existing;

            var go = new GameObject(containerName);
            Undo.RegisterCreatedObjectUndo(go, "Create Meteor Container");

            // 기본값은 씬 루트다. 범위 오브젝트 하위에 두면 범위의 스케일과 회전을
            // 그대로 물려받아 메테오가 눌리거나 기울어진다.
            if (parentOverride != null)
                Undo.SetTransformParent(go.transform, parentOverride, "Create Meteor Container");

            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            return go.transform;
        }

        /// <summary>LayerMask를 인스펙터처럼 드롭다운으로 그린다.</summary>
        private static LayerMask LSO_LayerMaskField(string label, LayerMask mask)
        {
            int field = EditorGUILayout.MaskField(label, InternalMaskToConcatenated(mask), InternalLayerNames());
            return ConcatenatedToInternalMask(field);
        }

        private static string[] InternalLayerNames()
        {
            var names = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName)) names.Add(layerName);
            }
            return names.ToArray();
        }

        private static int InternalMaskToConcatenated(LayerMask mask)
        {
            string[] names = InternalLayerNames();
            int result = 0;
            for (int i = 0; i < names.Length; i++)
                if ((mask.value & (1 << LayerMask.NameToLayer(names[i]))) != 0) result |= 1 << i;
            return result;
        }

        private static int ConcatenatedToInternalMask(int concatenated)
        {
            string[] names = InternalLayerNames();
            int result = 0;
            for (int i = 0; i < names.Length; i++)
                if ((concatenated & (1 << i)) != 0) result |= 1 << LayerMask.NameToLayer(names[i]);
            return result;
        }
    }
}