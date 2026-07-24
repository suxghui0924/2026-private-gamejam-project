using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SphereCollider 표면에 프리팹을 랜덤으로 배치하는 에디터 툴.
/// 반드시 Editor 폴더 안에 넣어야 합니다. (예: Assets/Editor/SphereSurfaceScatter.cs)
/// 메뉴: Tools > Sphere Surface Scatter
/// </summary>
public class SphereSurfaceScatter : EditorWindow
{
    [SerializeField] private SphereCollider targetSphere;
    [SerializeField] private List<GameObject> prefabs = new List<GameObject>();

    [SerializeField] private int minCount = 1;
    [SerializeField] private int maxCount = 5;

    [SerializeField] private bool alignToSurface = true;   // 표면 법선 방향으로 세우기
    [SerializeField] private bool randomSpin = true;       // 법선 축 기준 랜덤 회전
    [SerializeField] private float surfaceOffset = 0f;     // 표면에서 띄우는 거리
    [SerializeField] private float minAngleBetween = 15f;  // 오브젝트 간 최소 각도(겹침 방지)
    [SerializeField] private bool parentToSphere = true;

    [SerializeField] private bool randomScale = false;
    [SerializeField] private float scaleMin = 0.8f;     // 프리팹 원본 크기 대비 배율
    [SerializeField] private float scaleMax = 1.2f;
    [SerializeField] private bool uniformScale = true;  // 끄면 축마다 따로 랜덤

    [SerializeField] private bool useSeed = false;
    [SerializeField] private int seed = 0;

    private SerializedObject _so;
    private SerializedProperty _prefabsProp;
    private Vector2 _scroll;

    [MenuItem("Tools/Sphere Surface Scatter")]
    private static void Open()
    {
        GetWindow<SphereSurfaceScatter>("Sphere Scatter").minSize = new Vector2(320f, 380f);
    }

    private void OnEnable()
    {
        _so = new SerializedObject(this);
        _prefabsProp = _so.FindProperty("prefabs");

        // 창을 열 때 선택 중인 오브젝트에 SphereCollider가 있으면 자동으로 채워줌
        if (targetSphere == null && Selection.activeGameObject != null)
            targetSphere = Selection.activeGameObject.GetComponent<SphereCollider>();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject == null) return;

        var col = Selection.activeGameObject.GetComponent<SphereCollider>();
        if (col != null)
        {
            targetSphere = col;
            Repaint();
        }
    }

    private void OnGUI()
    {
        _so.Update();
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
        targetSphere = (SphereCollider)EditorGUILayout.ObjectField(
            "Sphere Collider", targetSphere, typeof(SphereCollider), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_prefabsProp, new GUIContent("Prefab List"), true);
        EditorGUILayout.HelpBox("리스트 중에서 하나씩 랜덤으로 뽑아 배치합니다.", MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Count", EditorStyles.boldLabel);
        minCount = EditorGUILayout.IntSlider("Min", minCount, 1, 50);
        maxCount = EditorGUILayout.IntSlider("Max", maxCount, 1, 50);
        if (maxCount < minCount) maxCount = minCount;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        alignToSurface = EditorGUILayout.Toggle("Align To Surface", alignToSurface);
        randomSpin = EditorGUILayout.Toggle("Random Spin", randomSpin);
        surfaceOffset = EditorGUILayout.FloatField("Surface Offset", surfaceOffset);
        minAngleBetween = EditorGUILayout.Slider("Min Angle Between", minAngleBetween, 0f, 90f);
        parentToSphere = EditorGUILayout.Toggle("Parent To Sphere", parentToSphere);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scale", EditorStyles.boldLabel);
        randomScale = EditorGUILayout.Toggle("Random Scale", randomScale);
        using (new EditorGUI.DisabledScope(!randomScale))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Range");
            scaleMin = EditorGUILayout.FloatField(scaleMin);
            scaleMax = EditorGUILayout.FloatField(scaleMax);
            EditorGUILayout.EndHorizontal();

            scaleMin = Mathf.Max(0.01f, scaleMin);
            if (scaleMax < scaleMin) scaleMax = scaleMin;

            uniformScale = EditorGUILayout.Toggle("Uniform", uniformScale);
            EditorGUILayout.HelpBox("프리팹 원본 크기에 곱해지는 배율입니다.", MessageType.None);
        }

        EditorGUILayout.Space();
        useSeed = EditorGUILayout.Toggle("Use Fixed Seed", useSeed);
        using (new EditorGUI.DisabledScope(!useSeed))
            seed = EditorGUILayout.IntField("Seed", seed);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space();

        bool ready = targetSphere != null && HasAnyPrefab();
        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button("Scatter", GUILayout.Height(32f)))
                Scatter();
        }

        if (targetSphere == null)
            EditorGUILayout.HelpBox("SphereCollider를 할당하세요.", MessageType.Warning);
        else if (!HasAnyPrefab())
            EditorGUILayout.HelpBox("배치할 프리팹을 1개 이상 넣으세요.", MessageType.Warning);

        _so.ApplyModifiedProperties();
    }

    private bool HasAnyPrefab()
    {
        for (int i = 0; i < prefabs.Count; i++)
            if (prefabs[i] != null) return true;
        return false;
    }

    private void Scatter()
    {
        if (useSeed) Random.InitState(seed);

        Transform t = targetSphere.transform;

        // SphereCollider는 lossyScale의 최댓값을 반지름에 곱해서 사용한다
        Vector3 s = t.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        float radius = targetSphere.radius * maxScale;
        Vector3 center = t.TransformPoint(targetSphere.center);

        int count = Random.Range(minCount, maxCount + 1);
        var placedDirs = new List<Vector3>(count);
        float cosLimit = Mathf.Cos(minAngleBetween * Mathf.Deg2Rad);

        int created = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 dir;
            if (!TryFindDirection(placedDirs, cosLimit, out dir))
                break;   // 최소 각도 조건을 만족하는 자리를 못 찾으면 중단

            placedDirs.Add(dir);

            GameObject prefab = PickPrefab();
            if (prefab == null) continue;

            GameObject instance = InstantiateObject(prefab);
            if (instance == null) continue;

            instance.transform.position = center + dir * (radius + surfaceOffset);

            if (alignToSurface)
            {
                // 오브젝트의 위쪽(+Y)이 표면 바깥을 향하도록
                instance.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
                if (randomSpin)
                    instance.transform.Rotate(dir, Random.Range(0f, 360f), Space.World);
            }
            else if (randomSpin)
            {
                instance.transform.rotation = Random.rotation;
            }

            // 부모로 넣기 전에 적용해야 월드 기준 크기가 의도한 값이 된다
            if (randomScale)
                ApplyRandomScale(instance.transform);

            if (parentToSphere)
                instance.transform.SetParent(t, true);

            Undo.RegisterCreatedObjectUndo(instance, "Scatter On Sphere");
            created++;
        }

        if (created > 0)
        {
            EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
            Debug.Log($"[SphereSurfaceScatter] {created}개를 {t.name} 표면에 배치했습니다.", t.gameObject);
        }
        else
        {
            Debug.LogWarning("[SphereSurfaceScatter] 배치된 오브젝트가 없습니다. Min Angle 값을 줄여보세요.");
        }
    }

    /// <summary>프리팹 원본 크기에 랜덤 배율을 곱한다.</summary>
    private void ApplyRandomScale(Transform target)
    {
        Vector3 baseScale = target.localScale;

        if (uniformScale)
        {
            float m = Random.Range(scaleMin, scaleMax);
            target.localScale = baseScale * m;
        }
        else
        {
            target.localScale = new Vector3(
                baseScale.x * Random.Range(scaleMin, scaleMax),
                baseScale.y * Random.Range(scaleMin, scaleMax),
                baseScale.z * Random.Range(scaleMin, scaleMax));
        }
    }

    /// <summary>기존에 배치된 방향들과 최소 각도를 유지하는 새 방향을 찾는다.</summary>
    private bool TryFindDirection(List<Vector3> placed, float cosLimit, out Vector3 result)
    {
        const int maxAttempts = 100;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 dir = Random.onUnitSphere;

            bool tooClose = false;
            for (int i = 0; i < placed.Count; i++)
            {
                if (Vector3.Dot(dir, placed[i]) > cosLimit)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                result = dir;
                return true;
            }
        }

        result = Vector3.up;
        return false;
    }

    private GameObject PickPrefab()
    {
        // null 항목을 건너뛰고 유효한 것 중에서만 뽑는다
        var valid = new List<GameObject>(prefabs.Count);
        for (int i = 0; i < prefabs.Count; i++)
            if (prefabs[i] != null) valid.Add(prefabs[i]);

        return valid.Count == 0 ? null : valid[Random.Range(0, valid.Count)];
    }

    private GameObject InstantiateObject(GameObject source)
    {
        // 프리팹 에셋이면 연결을 유지한 채로 생성
        if (PrefabUtility.GetPrefabAssetType(source) != PrefabAssetType.NotAPrefab &&
            PrefabUtility.IsPartOfPrefabAsset(source))
        {
            return PrefabUtility.InstantiatePrefab(source) as GameObject;
        }

        // 씬 안의 오브젝트라면 단순 복제
        return Instantiate(source);
    }
}