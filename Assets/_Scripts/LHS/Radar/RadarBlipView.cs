using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.LHS.Radar
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RadarBlipView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();

            if (iconImage == null)
            {
                iconImage = GetComponent<Image>();
            }

            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        public void Initialize(RadarTarget target)
        {
            if (iconImage != null)
            {
                iconImage.sprite = target.Icon;
                iconImage.color = target.Color;
                iconImage.raycastTarget = false;
            }

            SetAlpha(0f);
        }

        public void SetPosition(Vector2 position)
        {
            _rectTransform.anchoredPosition = position;
        }

        public void SetAlpha(float alpha)
        {
            _canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }
}