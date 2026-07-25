using System.Collections.Generic;
using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.World;
using UnityEngine;

namespace _Scripts.Suxghui.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ShipCollisionResponder : MonoBehaviour
    {
        [Header("Collision Sensor")]
        [SerializeField] private Transform shipRoot;
        [SerializeField] private SphereCollider collisionSensor;

        [Header("Stone Collision")]
        [SerializeField] private GameObject stoneImpactVfxPrefab;
        [SerializeField, Min(0f)] private float stoneKnockbackForce = 12f;
        [SerializeField, Min(0f)] private float stoneFuelDamage = 8f;
        [SerializeField, Min(0.01f)] private float stoneImpactVfxLifetime = 2.5f;
        [SerializeField, Min(0.01f)] private float stoneImpactVfxScale = 0.35f;

        [Header("Station Collision")]
        [SerializeField, Min(0f)] private float stationKnockbackForce = 18f;
        [SerializeField, Min(0f)] private float stationFuelDamage = 4f;

        [Header("Knockback")]
        [SerializeField, Min(0f)] private float knockbackDamping = 2.5f;
        [SerializeField, Min(0f)] private float maximumKnockbackSpeed = 120f;
        [SerializeField, Min(0f)] private float collisionCooldown = 0.6f;

        private readonly Dictionary<int, float> _nextCollisionTimes = new Dictionary<int, float>();
        private Vector3 _knockbackVelocity;

        public Vector3 ShipPosition => shipRoot != null ? shipRoot.position : transform.position;
        public Vector3 ShipForward => shipRoot != null ? shipRoot.forward : transform.forward;

        private void Awake()
        {
            if (shipRoot == null)
                shipRoot = transform.parent != null ? transform.parent : transform;

            if (collisionSensor == null)
                collisionSensor = GetComponent<SphereCollider>();

            if (collisionSensor != null)
                collisionSensor.isTrigger = true;
        }

        private void LateUpdate()
        {
            if (_knockbackVelocity.sqrMagnitude < 0.0001f)
            {
                _knockbackVelocity = Vector3.zero;
                return;
            }

            // The ship is driven as a kinematic object, so an AddForce call would be ignored.
            // Apply an inertial displacement after normal ship movement and damp it over time.
            shipRoot.position += _knockbackVelocity * Time.deltaTime;
            _knockbackVelocity *= Mathf.Exp(-knockbackDamping * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || other.transform.IsChildOf(shipRoot))
                return;

            SpaceMine mine = other.GetComponentInParent<SpaceMine>();
            if (mine != null)
            {
                Vector3 contactPoint = other.ClosestPoint(ShipPosition);
                mine.Detonate(this, contactPoint);
                return;
            }

            Transform station = FindTaggedAncestor(other.transform, "Station");
            if (station != null && CanRespondTo(station.gameObject))
            {
                Vector3 stationCollisionPoint = other.ClosestPoint(ShipPosition);
                Vector3 awayFromStation = ShipPosition - other.bounds.center;
                if (awayFromStation.sqrMagnitude < 0.0001f)
                    awayFromStation = ShipPosition - station.position;
                if (awayFromStation.sqrMagnitude < 0.0001f)
                    awayFromStation = -ShipForward;

                SpawnVfx(stoneImpactVfxPrefab, stationCollisionPoint, awayFromStation);
                ApplyKnockback(awayFromStation, stationKnockbackForce);
                ConsumeFuel(stationFuelDamage);
                return;
            }

            Transform stone = FindTaggedAncestor(other.transform, "Stone");
            if (stone == null || !CanRespondTo(stone.gameObject))
                return;

            Vector3 collisionPoint = other.ClosestPoint(ShipPosition);
            Vector3 awayFromStone = ShipPosition - other.bounds.center;
            if (awayFromStone.sqrMagnitude < 0.0001f)
                awayFromStone = ShipPosition - stone.position;
            if (awayFromStone.sqrMagnitude < 0.0001f)
                awayFromStone = -ShipForward;

            SpawnVfx(stoneImpactVfxPrefab, collisionPoint, awayFromStone);
            ApplyKnockback(awayFromStone, stoneKnockbackForce);
            ConsumeFuel(stoneFuelDamage);
        }

        public void ApplyKnockback(Vector3 direction, float force)
        {
            if (direction.sqrMagnitude < 0.0001f || force <= 0f)
                return;

            _knockbackVelocity = Vector3.ClampMagnitude(
                _knockbackVelocity + direction.normalized * force,
                maximumKnockbackSpeed);
        }

        public float ConsumeFuel(float amount)
        {
            GameManager manager = GameManager.Instance;
            return manager != null ? manager.ConsumeFuel(Mathf.Max(0f, amount)) : 0f;
        }

        private bool CanRespondTo(GameObject source)
        {
            int id = source.GetInstanceID();
            if (_nextCollisionTimes.TryGetValue(id, out float nextTime) && Time.time < nextTime)
                return false;

            _nextCollisionTimes[id] = Time.time + collisionCooldown;
            return true;
        }

        private void SpawnVfx(GameObject prefab, Vector3 position, Vector3 normal)
        {
            if (prefab == null)
                return;

            Quaternion rotation = normal.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(normal.normalized, shipRoot.up)
                : Quaternion.identity;
            GameObject effect = Instantiate(prefab, position, rotation);
            effect.transform.localScale = Vector3.one * stoneImpactVfxScale;
            effect.SetActive(true);

            ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                particles[i].Play(true);

            Destroy(effect, stoneImpactVfxLifetime);
        }

        private static Transform FindTaggedAncestor(Transform current, string tag)
        {
            while (current != null)
            {
                if (current.CompareTag(tag))
                    return current;
                current = current.parent;
            }

            return null;
        }
    }
}
