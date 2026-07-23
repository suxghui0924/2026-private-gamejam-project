using UnityEngine;

namespace _Scripts.Suxghui.Player.Agent
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovmentComponent : MonoBehaviour
    {
        [field: SerializeField] public Rigidbody RigidBody { get; private set; }
        [field: SerializeField] public Transform MoveTarget { get; private set; }
        [field: SerializeField, Min(0f)] public float MoveSpeed { get; private set; } = 8f;
        [field: SerializeField, Min(0f)] public float VerticalSpeed { get; private set; } = 6f;
        [field: SerializeField] public AnimationCurve MovementCurve { get; private set; } =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [field: SerializeField] public bool IsMovementBlock { get; private set; }
        public Vector3 CurrentVelocity { get; private set; }
        public float CurrentSpeed => CurrentVelocity.magnitude;

        private void Awake()
        {
            TryCacheRigidbody();
        }

        private void OnEnable()
        {
            TryCacheRigidbody();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            TryCacheRigidbody();
        }

        public void Move(Vector3 direction)
        {
            Move(direction, 1f);
        }

        public void Move(Vector3 direction, float speedMultiplier)
        {
            if (IsMovementBlock)
                return;

            Vector3 curvedDirection = new Vector3(
                ApplyCurve(direction.x),
                ApplyCurve(direction.y),
                ApplyCurve(direction.z));
            Vector3 velocity = new Vector3(
                curvedDirection.x * MoveSpeed * speedMultiplier,
                curvedDirection.y * VerticalSpeed * speedMultiplier,
                curvedDirection.z * MoveSpeed * speedMultiplier);
            CurrentVelocity = velocity;

            if (RigidBody != null && RigidBody.transform == MoveTarget && !RigidBody.isKinematic)
            {
                RigidBody.linearVelocity = velocity;
                return;
            }

            if (MoveTarget != null)
                MoveTarget.position += velocity * Time.fixedDeltaTime;
        }

        public void Stop()
        {
            CurrentVelocity = Vector3.zero;
            Move(Vector3.zero);
        }

        public void SetMovementBlock(bool isBlock)
        {
            IsMovementBlock = isBlock;

            if (IsMovementBlock)
                Stop();
        }

        private void TryCacheRigidbody()
        {
            if (RigidBody != null)
            {
                if (MoveTarget == null)
                    MoveTarget = RigidBody.transform;

                return;
            }

            RigidBody = GetComponentInParent<Rigidbody>();

            if (MoveTarget == null)
                MoveTarget = transform.parent != null ? transform.parent : transform;
        }

        private float ApplyCurve(float value)
        {
            if (MovementCurve == null)
                return value;

            float magnitude = Mathf.Clamp01(Mathf.Abs(value));
            return Mathf.Sign(value) * MovementCurve.Evaluate(magnitude);
        }
    }
}
