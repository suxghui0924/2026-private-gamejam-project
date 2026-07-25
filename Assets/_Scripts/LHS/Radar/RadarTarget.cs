using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.LHS.Radar
{
    public sealed class RadarTarget : MonoBehaviour
    {
        private static readonly HashSet<RadarTarget> ActiveTargets = new();

        public static IEnumerable<RadarTarget> Targets => ActiveTargets;

        public static event Action<RadarTarget> Added;
        public static event Action<RadarTarget> Removed;
        public static event Action<RadarTarget> Changed;

        [field: SerializeField]
        public Sprite Icon { get; private set; }

        [field: SerializeField]
        public Color Color { get; private set; } = Color.white;

        [field: SerializeField]
        public bool IsVisible { get; private set; } = true;

        private void OnEnable()
        {
            if (!ActiveTargets.Add(this))
                return;

            Added?.Invoke(this);
        }

        private void OnDisable()
        {
            if (!ActiveTargets.Remove(this))
                return;

            Removed?.Invoke(this);
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            NotifyChanged();
        }

        public void Configure(Sprite icon, Color color, bool isVisible = true)
        {
            Icon = icon;
            Color = color;
            IsVisible = isVisible;
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            if (isActiveAndEnabled && ActiveTargets.Contains(this))
                Changed?.Invoke(this);
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            ActiveTargets.Clear();
            Added = null;
            Removed = null;
            Changed = null;
        }
    }
}
