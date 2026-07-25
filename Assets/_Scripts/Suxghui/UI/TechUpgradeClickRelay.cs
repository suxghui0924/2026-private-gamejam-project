using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _Scripts.Suxghui.UI
{
    public sealed class TechUpgradeClickRelay : MonoBehaviour, IPointerClickHandler
    {
        private Action _onClick;
        private bool _interactable;

        public void Bind(Action onClick)
        {
            _onClick = onClick;
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_interactable && eventData.button == PointerEventData.InputButton.Left)
                _onClick?.Invoke();
        }
    }
}
