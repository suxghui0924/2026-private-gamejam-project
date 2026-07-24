using System;
using _Scripts.Suxghui.CoreLib;
using UnityEngine;

namespace _Scripts.Suxghui.Agent.Component
{
    public class HeatlhComponent : MonoBehaviour
    {
        public NotifyValue<int> health = new NotifyValue<int>(0);
        public event Action<bool> OnDeadInvoke;

        public bool currentHeartbeat = true;
        public bool CurrentHeartbeat => currentHeartbeat;

        public int MAXHEALTH { get; private set; } = 100;

        private void Awake()
        {
            health.Value = MAXHEALTH;
        }

        public void GetDamage(int damage)
        {
            health.Value -= Mathf.Clamp(health.Value - damage, 0, MAXHEALTH);
            CheckHeartBeat();
        }

        public void HealDamage(int amount)
        {
            health.Value += Mathf.Clamp(health.Value + amount, 0, MAXHEALTH);
            CheckHeartBeat();
        }

        private void CheckHeartBeat()
        {
            if (health.Value <= 0)
                Dead();
            else
                currentHeartbeat = true;
        }
        
        public void SetHealthState(int currentHealth, int maxHealth)
        {
            MAXHEALTH = Mathf.Max(1, maxHealth);
            health.Value = Mathf.Clamp(currentHealth, 0, MAXHEALTH);
            CheckHeartBeat();
        }

        private void Dead()
        {
            OnDeadInvoke?.Invoke(true);
            currentHeartbeat = false;
        }
    }
}
