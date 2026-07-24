using System;
using _Scripts.Suxghui.Mining;

namespace _Scripts.Suxghui.Manager.Module
{
    public sealed class MiningTechSelectionModule
    {
        private readonly Action<MiningTechType, string> _saveSelection;

        public MiningTechType CurrentType { get; private set; }
        public string CurrentTechId { get; private set; }
        public int CurrentIndex => (int)CurrentType;

        public event Action<MiningTechType> SelectionChanged;

        public MiningTechSelectionModule(
            int savedIndex,
            string savedTechId,
            Action<MiningTechType, string> saveSelection)
        {
            _saveSelection = saveSelection;
            CurrentType = ResolveInitialType(savedIndex, savedTechId);
            CurrentTechId = GetTechId(CurrentType);
        }

        public bool Select(MiningTechType type)
        {
            if (!IsValid(type))
                return false;

            string techId = GetTechId(type);
            if (CurrentType == type && CurrentTechId == techId)
                return false;

            CurrentType = type;
            CurrentTechId = techId;
            _saveSelection?.Invoke(CurrentType, CurrentTechId);
            SelectionChanged?.Invoke(CurrentType);
            return true;
        }

        public bool Select(string techId)
        {
            if (!TryGetType(techId, out MiningTechType type))
                return false;

            return Select(type);
        }

        public bool SelectNext()
        {
            MiningTechType next = (MiningTechType)((CurrentIndex + 1) % 3);
            return Select(next);
        }

        public static string GetTechId(MiningTechType type)
        {
            return type switch
            {
                MiningTechType.Drill => "drill",
                MiningTechType.Laser => "laser",
                MiningTechType.Extractor => "extractor",
                _ => "drill"
            };
        }

        public static bool TryGetType(string techId, out MiningTechType type)
        {
            if (string.Equals(techId, "laser", StringComparison.OrdinalIgnoreCase))
            {
                type = MiningTechType.Laser;
                return true;
            }

            if (string.Equals(techId, "extractor", StringComparison.OrdinalIgnoreCase))
            {
                type = MiningTechType.Extractor;
                return true;
            }

            if (string.Equals(techId, "drill", StringComparison.OrdinalIgnoreCase))
            {
                type = MiningTechType.Drill;
                return true;
            }

            type = MiningTechType.Drill;
            return false;
        }

        private static MiningTechType ResolveInitialType(int savedIndex, string savedTechId)
        {
            if (TryGetType(savedTechId, out MiningTechType savedType))
                return savedType;

            return savedIndex >= 0 && savedIndex <= 2
                ? (MiningTechType)savedIndex
                : MiningTechType.Drill;
        }

        private static bool IsValid(MiningTechType type)
        {
            return type == MiningTechType.Drill ||
                   type == MiningTechType.Laser ||
                   type == MiningTechType.Extractor;
        }
    }
}
