using UnityEngine;

namespace _Scripts.Suxghui.Player
{
    [CreateAssetMenu(fileName = "Space Ship Booster", menuName = "SO/Space Ship Booster", order = 1)]
    public class BoosterSettingsSO : ScriptableObject
    {
        [field: SerializeField, Min(1f)] public float SpeedMultiplier { get; private set; } = 2f;
        [field: SerializeField, Min(0f)] public float Acceleration { get; private set; } = 12f;
        [field: SerializeField, Min(1f)] public float BoosterFov { get; private set; } = 80f;
    }
}
