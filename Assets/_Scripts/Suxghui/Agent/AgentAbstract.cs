using _Scripts.Suxghui.Player.Agent;
using UnityEngine;

namespace _Scripts.Suxghui.Agent
{
    public abstract class AgentAbstract : MonoBehaviour
    {
        [field: SerializeField] public MovmentComponent MovementComponent { get; protected set; }

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
            if (Application.isPlaying)
                return;

            TryCacheMovementComponent();
        }

        private void TryCacheMovementComponent()
        {
            if (MovementComponent != null)
                return;

            MovementComponent = GetComponentInChildren<MovmentComponent>();
        }
    }
}
