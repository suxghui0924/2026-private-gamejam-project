using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Scripts.Suxghui.Player.Input
{
    [CreateAssetMenu(fileName = "Player Input SO", menuName = "SO/Input SO", order = 0)]
    public class PlayerInputSO : ScriptableObject, PlayerAction.IPlayerActions
    {
        private PlayerAction _playerAction;

        public event Action<Vector2, bool> OnMoveKeyPress;
        public event Action<Vector2, bool> OnFlyKeyPress;
        public event Action<bool> OnBoosterPress;

        private void OnEnable()
        {
            if (_playerAction == null)
            {
                _playerAction = new PlayerAction();
                _playerAction.Player.SetCallbacks(this);
            }

            _playerAction.Player.Enable();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            OnMoveKeyPress?.Invoke(context.ReadValue<Vector2>(), context.performed);
        }

        public void OnFly(InputAction.CallbackContext context)
        {
            OnFlyKeyPress?.Invoke(context.ReadValue<Vector2>(), context.performed);
        }

        public void OnBoost(InputAction.CallbackContext context)
        {
            OnBoosterPress?.Invoke(context.ReadValueAsButton());
        }

        private void OnDisable()
        {
            if (_playerAction != null)
                _playerAction.Player.Disable();
        }
    }
}
