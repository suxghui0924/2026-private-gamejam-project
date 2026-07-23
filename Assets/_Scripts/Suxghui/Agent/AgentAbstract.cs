using _Scripts.Suxghui.Agent.Component;
using _Scripts.Suxghui.Player.Agent;
using UnityEngine;

namespace _Scripts.Suxghui.Agent
{
    public abstract class AgentAbstract : MonoBehaviour
    {
        [field: SerializeField] public MovmentComponent MovementComponent { get; protected set; }
        [field: SerializeField] public HeatlhComponent HealthComponent { get; protected set; }

        protected virtual void Awake()
        {
            TryCacheMovementComponent();
        }

        protected virtual void OnEnable()
        {
            TryCacheMovementComponent();
        }

        protected virtual void OnValidate()
        {
            TryCacheMovementComponent();
        }

        private void TryCacheMovementComponent()
        {
            MovementComponent = GetComponentInChildren<MovmentComponent>();
            HealthComponent = GetComponentInChildren<HeatlhComponent>();
        }
    }
}
