using _Scripts.Suxghui.Player;
using UnityEngine;

namespace _Scripts.Suxghui.World
{
    [DisallowMultipleComponent]
    public sealed class SpaceMine : MonoBehaviour
    {
        [SerializeField] private GameObject explosionVfxPrefab;
        [SerializeField, Min(0f)] private float knockbackForce = 75f;
        [SerializeField, Min(0.01f)] private float explosionVfxLifetime = 2.5f;
        [SerializeField, Min(0.01f)] private float explosionVfxScale = 1f;

        private bool _detonated;

        public void Configure(
            GameObject explosionPrefab,
            float force,
            float vfxLifetime,
            float vfxScale)
        {
            explosionVfxPrefab = explosionPrefab;
            knockbackForce = Mathf.Max(0f, force);
            explosionVfxLifetime = Mathf.Max(0.01f, vfxLifetime);
            explosionVfxScale = Mathf.Max(0.01f, vfxScale);
        }

        public void Detonate(ShipCollisionResponder ship, Vector3 contactPoint)
        {
            if (_detonated || ship == null)
                return;

            _detonated = true;
            Vector3 awayFromMine = ship.ShipPosition - transform.position;
            if (awayFromMine.sqrMagnitude < 0.0001f)
                awayFromMine = contactPoint - transform.position;
            if (awayFromMine.sqrMagnitude < 0.0001f)
                awayFromMine = -ship.ShipForward;

            ship.ApplyKnockback(awayFromMine, knockbackForce);
            SpawnExplosion(awayFromMine);

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            Destroy(gameObject);
        }

        private void SpawnExplosion(Vector3 direction)
        {
            if (explosionVfxPrefab == null)
                return;

            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized)
                : Quaternion.identity;
            // Mine explosions originate from the mine itself, not from the ship collider.
            GameObject effect = Instantiate(explosionVfxPrefab, transform.position, rotation);
            effect.transform.localScale = Vector3.one * explosionVfxScale;
            effect.SetActive(true);

            ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
                particles[i].Play(true);

            Destroy(effect, explosionVfxLifetime);
        }
    }
}
