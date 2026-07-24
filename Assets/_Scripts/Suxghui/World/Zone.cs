using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Scripts.Suxghui.World
{
    [DisallowMultipleComponent]
    public class Zone : MonoBehaviour
    {
        [Header("Zone Type")]
        [SerializeField] private ZoneType zoneType = ZoneType.Normal;

        [Header("Zone Volumes")]
        [Tooltip("If empty, BoxColliders are collected from this object and its children.")]
        [SerializeField] private List<BoxCollider> zoneBoxes = new List<BoxCollider>();

        [Header("Gizmo")]
        [SerializeField] private bool drawGizmo = true;

        public ZoneType ZoneType => zoneType;

        public IReadOnlyList<BoxCollider> ZoneBoxes
        {
            get
            {
                EnsureZoneBoxes();
                return zoneBoxes;
            }
        }

        private void Reset()
        {
            RefreshZoneBoxes();
        }

        private void Awake()
        {
            EnsureZoneBoxes();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RemoveMissingBoxes();
            if (zoneBoxes.Count == 0)
                RefreshZoneBoxes();
        }
#endif

        public bool Contains(Vector3 worldPoint)
        {
            EnsureZoneBoxes();
            for (int i = 0; i < zoneBoxes.Count; i++)
            {
                BoxCollider box = zoneBoxes[i];
                if (box != null && Contains(box, worldPoint))
                    return true;
            }

            return false;
        }

        public bool TryGetRandomPoint(out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            EnsureZoneBoxes();
            if (zoneBoxes.Count == 0)
                return false;

            float totalVolume = 0f;
            for (int i = 0; i < zoneBoxes.Count; i++)
                totalVolume += GetWorldVolume(zoneBoxes[i]);

            if (totalVolume <= 0f)
                return false;

            float selection = Random.value * totalVolume;
            BoxCollider selected = null;

            for (int i = 0; i < zoneBoxes.Count; i++)
            {
                BoxCollider candidate = zoneBoxes[i];
                float volume = GetWorldVolume(candidate);
                if (volume <= 0f)
                    continue;

                selected = candidate;
                selection -= volume;
                if (selection <= 0f)
                    break;
            }

            if (selected == null)
                return false;

            Vector3 half = selected.size * 0.5f;
            Vector3 localPoint = selected.center + new Vector3(
                Random.Range(-half.x, half.x),
                Random.Range(-half.y, half.y),
                Random.Range(-half.z, half.z));
            worldPoint = selected.transform.TransformPoint(localPoint);
            return true;
        }

        [ContextMenu("Refresh Child Zone Volumes")]
        public void RefreshZoneBoxes()
        {
            zoneBoxes.Clear();
            zoneBoxes.AddRange(GetComponentsInChildren<BoxCollider>(true));
            RemoveMissingBoxes();
        }

        private void EnsureZoneBoxes()
        {
            RemoveMissingBoxes();
            if (zoneBoxes.Count == 0)
                RefreshZoneBoxes();
        }

        private void RemoveMissingBoxes()
        {
            if (zoneBoxes == null)
                zoneBoxes = new List<BoxCollider>();

            for (int i = zoneBoxes.Count - 1; i >= 0; i--)
            {
                if (zoneBoxes[i] == null)
                    zoneBoxes.RemoveAt(i);
            }
        }

        private static bool Contains(BoxCollider box, Vector3 worldPoint)
        {
            Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(localPoint.x) <= half.x &&
                   Mathf.Abs(localPoint.y) <= half.y &&
                   Mathf.Abs(localPoint.z) <= half.z;
        }

        private static float GetWorldVolume(BoxCollider box)
        {
            if (box == null || !box.enabled || !box.gameObject.activeInHierarchy)
                return 0f;

            Vector3 scale = box.transform.lossyScale;
            Vector3 size = box.size;
            return Mathf.Abs(size.x * scale.x) *
                   Mathf.Abs(size.y * scale.y) *
                   Mathf.Abs(size.z * scale.z);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
                return;

            EnsureZoneBoxes();
            Color solidColor = zoneType switch
            {
                ZoneType.Classified => new Color(1f, 0.75f, 0.1f, 1f),
                ZoneType.TopSecret => new Color(1f, 0.25f, 0.2f, 1f),
                _ => new Color(0.3f, 0.85f, 1f, 1f)
            };

            Matrix4x4 previousMatrix = Gizmos.matrix;
            for (int i = 0; i < zoneBoxes.Count; i++)
            {
                BoxCollider box = zoneBoxes[i];
                if (box == null)
                    continue;

                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.color = solidColor;
                Gizmos.DrawWireCube(box.center, box.size);
                Color fillColor = solidColor;
                fillColor.a = 0.06f;
                Gizmos.color = fillColor;
                Gizmos.DrawCube(box.center, box.size);
            }

            Gizmos.matrix = previousMatrix;
        }
    }
}
