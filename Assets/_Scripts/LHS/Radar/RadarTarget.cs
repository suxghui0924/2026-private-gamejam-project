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
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticData()
        {
            ActiveTargets.Clear();
            Added = null;
            Removed = null;
        }
    }
}