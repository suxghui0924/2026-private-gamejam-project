using UnityEngine;

namespace _Scripts.Suxghui.World
{
    public class StarfieldParallax : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private ParticleSystem nearStars;
        [SerializeField] private ParticleSystem middleStars;
        [SerializeField] private ParticleSystem farStars;

        [Header("Parallax Follow Factors")]
        [SerializeField, Range(0f, 1f)] private float nearFollowFactor = 1f;
        [SerializeField, Range(0f, 1f)] private float middleFollowFactor = 0.65f;
        [SerializeField, Range(0f, 1f)] private float farFollowFactor = 0.3f;

        private readonly Transform[] _starTransforms = new Transform[3];
        private readonly float[] _followFactors = new float[3];
        private Vector3 _lastPlayerPosition;

        private void Awake()
        {
            CacheLayer(0, nearStars, nearFollowFactor);
            CacheLayer(1, middleStars, middleFollowFactor);
            CacheLayer(2, farStars, farFollowFactor);

            if (player != null)
                _lastPlayerPosition = player.position;
        }

        private void LateUpdate()
        {
            if (player == null)
                return;

            Vector3 playerDelta = player.position - _lastPlayerPosition;
            _lastPlayerPosition = player.position;

            if (playerDelta.sqrMagnitude <= 0f)
                return;

            for (int i = 0; i < _starTransforms.Length; i++)
            {
                Transform star = _starTransforms[i];
                if (star != null)
                    star.position += playerDelta * _followFactors[i];
            }
        }

        private void CacheLayer(int index, ParticleSystem particleSystem, float followFactor)
        {
            if (particleSystem == null)
                return;

            ParticleSystem.MainModule main = particleSystem.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            _starTransforms[index] = particleSystem.transform;
            _followFactors[index] = followFactor;
        }
    }
}
