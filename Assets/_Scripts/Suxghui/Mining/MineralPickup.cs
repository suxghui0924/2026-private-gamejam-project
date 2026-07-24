using _Scripts.LSO.Data;
using UnityEngine;

namespace _Scripts.Suxghui.Mining
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MineralPickup : MonoBehaviour
    {
        private const string OreTag = "Ore";

        [SerializeField] private LSO_MineralSO mineral;
        [SerializeField, Min(1)] private int amount = 1;
        [SerializeField] private bool collectible;

        private Rigidbody _rigidbody;

        public LSO_MineralSO Mineral => mineral;
        public int Amount => amount;
        public bool IsCollectible => collectible && mineral != null && amount > 0;
        public Rigidbody Body => EnsureRigidbody();

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            ConfigureBody(collectible);
        }

        public void Initialize(LSO_MineralSO mineralDefinition, int representedAmount, bool canCollect)
        {
            mineral = mineralDefinition;
            amount = Mathf.Max(1, representedAmount);
            collectible = canCollect;
            gameObject.tag = OreTag;
            ConfigureBody(canCollect);
            ApplyMineralMaterial();
        }

        public void MarkCollectible()
        {
            collectible = true;
            ConfigureBody(true);
        }

        public int Take(int maximumAmount)
        {
            if (!IsCollectible || maximumAmount <= 0)
                return 0;

            int taken = Mathf.Min(amount, maximumAmount);
            amount -= taken;
            return taken;
        }

        public void AttractTowards(Vector3 destination, float acceleration, float maximumSpeed)
        {
            if (!IsCollectible)
                return;

            Rigidbody body = EnsureRigidbody();
            if (body == null)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    destination,
                    Mathf.Max(0f, maximumSpeed) * Time.fixedDeltaTime);
                return;
            }

            Vector3 toDestination = destination - body.worldCenterOfMass;
            if (toDestination.sqrMagnitude < 0.000001f)
                return;

            Vector3 desiredVelocity = toDestination.normalized * Mathf.Max(0f, maximumSpeed);
            body.linearVelocity = Vector3.MoveTowards(
                body.linearVelocity,
                desiredVelocity,
                Mathf.Max(0f, acceleration) * Time.fixedDeltaTime);
        }

        private Rigidbody EnsureRigidbody()
        {
            if (_rigidbody == null)
            {
                TryGetComponent(out _rigidbody);
                if (_rigidbody == null && gameObject != null)
                    _rigidbody = gameObject.AddComponent<Rigidbody>();
            }

            return _rigidbody;
        }

        private void ConfigureBody(bool enableMotion)
        {
            Rigidbody body = EnsureRigidbody();
            if (body == null)
                return;

            body.useGravity = false;
            body.isKinematic = !enableMotion;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void ApplyMineralMaterial()
        {
            if (mineral == null || mineral.mineralMaterial == null)
                return;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                if (materials.Length == 0)
                    continue;

                materials[0] = mineral.mineralMaterial;
                renderers[i].sharedMaterials = materials;
            }
        }
    }
}
