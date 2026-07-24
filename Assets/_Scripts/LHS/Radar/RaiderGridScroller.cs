using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.LHS.Radar
{
    [RequireComponent(typeof(RawImage))]
    public sealed class RadarGridScroller : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private RadarController radarController;

        [Header("Grid")]
        [SerializeField, Min(0.01f)]
        private float gridCellWorldSize = 10f;

        private RawImage _gridImage;

        private void Awake()
        {
            _gridImage = GetComponent<RawImage>();
            _gridImage.raycastTarget = false;
        }

        private void LateUpdate()
        {
            if (player == null || radarController == null)
            {
                return;
            }

            float detectionRange =
                radarController.DetectionRange;

            float repeatCount =
                detectionRange * 2f / gridCellWorldSize;

            Vector2 playerWorldPosition = new Vector2(
                player.position.x,
                player.position.z
            );

            Vector2 uvCenter =
                playerWorldPosition / gridCellWorldSize;

            Vector2 uvSize =
                Vector2.one * repeatCount;

            Vector2 uvPosition =
                uvCenter - uvSize * 0.5f;

            _gridImage.uvRect = new Rect(
                uvPosition,
                uvSize
            );
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            gridCellWorldSize =
                Mathf.Max(0.01f, gridCellWorldSize);
        }
#endif
    }
}