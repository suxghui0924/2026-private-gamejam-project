using System;
using _Scripts.Suxghui.CoreLib;
using UnityEngine;

namespace _Scripts.Suxghui.Agent.Component
{
    public class HeatlhComponent : MonoBehaviour
    {
        public NotifyValue<int> health = new NotifyValue<int>(0);
        public event Action<bool> OnDeadInvoke;

        public bool CurrentHeartbeat { get; private set; } = true;
        
        [SerializeField, Min(1)] private int MAXHEALTH = 100;

        public int MaxHealth => MAXHEALTH;

        private void Awake()
        {
            health.Value = MAXHEALTH;
        }

        public void GetDamage(int damage)
        {
            if (damage <= 0)
                return;

            health.Value = Mathf.Clamp(health.Value - damage, 0, MAXHEALTH);
            CheckHeartBeat();
        }

        public void HealDamage(int amount)
        {
            if (amount <= 0)
                return;

            health.Value = Mathf.Clamp(health.Value + amount, 0, MAXHEALTH);
            CheckHeartBeat();
        }

        public void SetHealthState(int currentHealth, int maxHealth)
        {
            MAXHEALTH = Mathf.Max(1, maxHealth);
            health.Value = Mathf.Clamp(currentHealth, 0, MAXHEALTH);
            CheckHeartBeat();
        }

        private void CheckHeartBeat()
        {
            if (health.Value <= 0)
                Dead();
            else
                CurrentHeartbeat = true;
        }

        private void Dead()
        {
            OnDeadInvoke?.Invoke(true);
            CurrentHeartbeat = false;
        }
    }
}
