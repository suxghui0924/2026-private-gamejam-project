using UnityEngine;

namespace _Scripts.Suxghui.Player.Agent
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovmentComponent : MonoBehaviour
    {
        [field: SerializeField] public Rigidbody RigidBody { get; private set; }
        [field: SerializeField] public Transform MoveTarget { get; private set; }
        [field: SerializeField, Min(0f)] public float MoveSpeed { get; private set; } = 10f;
        [field: SerializeField, Min(0f)] public float VerticalSpeed { get; private set; } = 6f;
        [field: SerializeField] public AnimationCurve MovementCurve { get; private set; } =
            AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [field: SerializeField] public bool IsMovementBlock { get; private set; }
        public Vector3 CurrentVelocity { get; private set; }
        public float CurrentSpeed => CurrentVelocity.magnitude;
        public float ExternalSpeedMultiplier { get; private set; } = 1f;

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
            Move(direction, speedMultiplier, Time.fixedDeltaTime);
        }

        public void Move(Vector3 direction, float speedMultiplier, float deltaTime)
        {
            if (IsMovementBlock)
                return;

            Vector3 curvedDirection = new Vector3(
                ApplyCurve(direction.x),
                ApplyCurve(direction.y),
                ApplyCurve(direction.z));
            Vector3 velocity = new Vector3(
                curvedDirection.x * MoveSpeed * speedMultiplier * ExternalSpeedMultiplier,
                curvedDirection.y * VerticalSpeed * speedMultiplier * ExternalSpeedMultiplier,
                curvedDirection.z * MoveSpeed * speedMultiplier * ExternalSpeedMultiplier);
            CurrentVelocity = velocity;

            if (RigidBody != null && RigidBody.transform == MoveTarget && !RigidBody.isKinematic)
            {
                RigidBody.linearVelocity = velocity;
                return;
            }

            if (MoveTarget != null)
                MoveTarget.position += velocity * deltaTime;
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

        public void SetExternalSpeedMultiplier(float multiplier)
        {
            ExternalSpeedMultiplier = Mathf.Clamp(multiplier, 0.1f, 2f);
        }

        public void SetBaseMoveSpeed(float moveSpeed)
        {
            float verticalRatio = MoveSpeed > 0.001f ? VerticalSpeed / MoveSpeed : 0.75f;
            MoveSpeed = Mathf.Max(0f, moveSpeed);
            VerticalSpeed = MoveSpeed * Mathf.Max(0f, verticalRatio);
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
