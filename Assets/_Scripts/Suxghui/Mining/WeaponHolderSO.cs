using System;
using UnityEngine;

namespace _Scripts.Suxghui.Mining
{
    [CreateAssetMenu(fileName = "Weapon Holder", menuName = "Suxghui/Mining/Weapon Holder")]
    public sealed class WeaponHolderSO : ScriptableObject
    {
        [Header("Definitions")]
        [SerializeField] private MiningTechDefinitionSO drill;
        [SerializeField] private MiningTechDefinitionSO laser;
        [SerializeField] private MiningTechDefinitionSO extractor;

        [Header("Weapon VFX")]
        [SerializeField] private GameObject[] drillEffectPrefabs = Array.Empty<GameObject>();
        [SerializeField] private GameObject laserEffectPrefab;
        [SerializeField] private GameObject explosionEffectPrefab;

        [Header("Runtime State")]
        [SerializeField] private MiningTechDefinitionSO currentWeapon;
        [SerializeField, Min(0)] private int drillLevel;
        [SerializeField, Min(0)] private int laserLevel;
        [SerializeField, Min(0)] private int extractorLevel;

        public MiningTechDefinitionSO CurrentWeapon => currentWeapon;
        public GameObject[] DrillEffectPrefabs => drillEffectPrefabs;
        public GameObject LaserEffectPrefab => laserEffectPrefab;
        public GameObject ExplosionEffectPrefab => explosionEffectPrefab;
        public event Action<MiningTechDefinitionSO> CurrentWeaponChanged;

        public MiningTechDefinitionSO GetDefinition(MiningTechType type)
        {
            return type switch
            {
                MiningTechType.Drill => drill,
                MiningTechType.Laser => laser,
                MiningTechType.Extractor => extractor,
                _ => drill
            };
        }

        public int GetLevel(MiningTechType type)
        {
            return type switch
            {
                MiningTechType.Drill => drillLevel,
                MiningTechType.Laser => laserLevel,
                MiningTechType.Extractor => extractorLevel,
                _ => 0
            };
        }

        public void SetCurrentWeapon(MiningTechType type)
        {
            MiningTechDefinitionSO next = GetDefinition(type);
            if (next == null || next == currentWeapon)
                return;

            currentWeapon = next;
            CurrentWeaponChanged?.Invoke(currentWeapon);
        }

        public void SetLevel(MiningTechType type, int level)
        {
            MiningTechDefinitionSO definition = GetDefinition(type);
            level = Mathf.Clamp(level, 0, definition != null ? definition.MaxLevel : 5);
            switch (type)
            {
                case MiningTechType.Drill:
                    drillLevel = level;
                    break;
                case MiningTechType.Laser:
                    laserLevel = level;
                    break;
                case MiningTechType.Extractor:
                    extractorLevel = level;
                    break;
            }
        }
    }
}
