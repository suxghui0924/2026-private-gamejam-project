using System;
using System.Collections.Generic;
using System.Text;
using _Scripts.LSO.Data;
using UnityEngine;

namespace _Scripts.Suxghui.Mining
{
    [DisallowMultipleComponent]
    public class OreContents : MonoBehaviour
    {
        private const string StoneTag = "Stone";
        private const string OreTag = "Ore";

        [Serializable]
        public class ExternalRawOre
        {
            public LSO_MineralSO mineral;
            public Transform anchor;
            public bool isExtracted;
        }

        [Header("Internal Ore")]
        [SerializeField] private LSO_OreSO internalOreSO;

        [Header("External Ores")]
        [SerializeField] private List<ExternalRawOre> externalOres = new List<ExternalRawOre>();

        private LSO_Ore _lsoOre;

        public event Action ExternalOresChanged;

        public LSO_MineralSO InternalMineral
        {
            get
            {
                LSO_OreSO ore = ResolveOreSO();
                return ore != null ? ore.mineral : null;
            }
        }

        public LSO_OreSO OreSO => ResolveOreSO();
        public IReadOnlyList<ExternalRawOre> ExternalOres => externalOres;

        public int RemainingExternalCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < externalOres.Count; i++)
                {
                    ExternalRawOre item = externalOres[i];
                    if (item != null && !item.isExtracted && item.mineral != null)
                        count++;
                }

                return count;
            }
        }

        private void Awake()
        {
            CacheOreComponent();
            ApplyResourceTags();
        }

        public void SetInternalOreSO(LSO_OreSO oreSO)
        {
            internalOreSO = oreSO;
            CacheOreComponent();
            if (_lsoOre != null)
                _lsoOre.oreSO = oreSO;
            ApplyResourceTags();
        }

        public ExternalRawOre RegisterExternalOre(LSO_MineralSO mineral, Transform anchor = null)
        {
            if (mineral == null)
                return null;

            var entry = new ExternalRawOre
            {
                mineral = mineral,
                anchor = anchor,
                isExtracted = false
            };
            externalOres.Add(entry);
            ApplyOreTag(anchor);
            ExternalOresChanged?.Invoke();
            return entry;
        }

        public LSO_MineralSO ExtractExternalOre(int index)
        {
            if (index < 0 || index >= externalOres.Count)
                return null;

            ExternalRawOre item = externalOres[index];
            if (item == null || item.isExtracted || item.mineral == null)
                return null;

            item.isExtracted = true;
            ExternalOresChanged?.Invoke();
            return item.mineral;
        }

        public bool MarkExternalOreExtracted(Transform anchor)
        {
            if (anchor == null)
                return false;

            for (int i = 0; i < externalOres.Count; i++)
            {
                ExternalRawOre item = externalOres[i];
                if (item == null || item.isExtracted || item.anchor != anchor)
                    continue;

                item.isExtracted = true;
                ExternalOresChanged?.Invoke();
                return true;
            }

            return false;
        }

        public void RemoveRemainingExternalOres()
        {
            bool changed = false;
            for (int i = 0; i < externalOres.Count; i++)
            {
                ExternalRawOre item = externalOres[i];
                if (item == null || item.isExtracted)
                    continue;

                item.isExtracted = true;
                if (item.anchor != null)
                    Destroy(item.anchor.gameObject);
                changed = true;
            }

            if (changed)
                ExternalOresChanged?.Invoke();
        }

        public string GetContentSummary()
        {
            var summary = new StringBuilder();
            LSO_OreSO definition = ResolveOreSO();
            summary.Append("Ore: ")
                .Append(definition != null ? definition.oreName : "(missing OreSO)")
                .AppendLine();
            summary.Append("  Internal: ")
                .Append(InternalMineral != null ? InternalMineral.mineralName : "(none)")
                .AppendLine();
            summary.Append("  External remaining: ").Append(RemainingExternalCount);
            return summary.ToString();
        }

        private LSO_OreSO ResolveOreSO()
        {
            if (internalOreSO != null)
                return internalOreSO;

            CacheOreComponent();
            return _lsoOre != null ? _lsoOre.oreSO : null;
        }

        private void CacheOreComponent()
        {
            if (_lsoOre == null)
                _lsoOre = GetComponent<LSO_Ore>() ?? GetComponentInParent<LSO_Ore>();
        }

        private void ApplyResourceTags()
        {
            CacheOreComponent();
            GameObject stoneObject = _lsoOre != null ? _lsoOre.gameObject : gameObject;
            stoneObject.tag = StoneTag;

            for (int i = 0; i < externalOres.Count; i++)
            {
                ExternalRawOre externalOre = externalOres[i];
                if (externalOre != null &&
                    externalOre.anchor != null &&
                    externalOre.anchor.gameObject != stoneObject)
                    ApplyOreTag(externalOre.anchor);
            }
        }

        private static void ApplyOreTag(Transform anchor)
        {
            if (anchor != null)
                anchor.gameObject.tag = OreTag;
        }

        [ContextMenu("Log Ore Contents")]
        private void LogContentsInfo()
        {
            Debug.Log(GetContentSummary(), this);
        }
    }
}
