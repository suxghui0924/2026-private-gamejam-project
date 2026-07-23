using UnityEngine;

namespace _Scripts.Suxghui.CoreLib
{
    public class NotifyValue<T>
    {
        public delegate void ValueChanged(T prev, T next);

        public event ValueChanged OnValueChanged;

        private T _value;

        public T Value
        {
            get => _value;
            set
            {
                T before = _value;
                _value = value;
                if ((before == null && value != null) || !before.Equals(_value))
                    OnValueChanged?.Invoke(before, _value);
            }
        }

        public NotifyValue()
        {
            _value = default;
        }

        public NotifyValue(T initValue)
        {
            _value = initValue;
        }
    }
}