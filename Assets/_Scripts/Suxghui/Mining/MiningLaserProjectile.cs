using UnityEngine;

namespace _Scripts.Suxghui.Mining
{
    public sealed class MiningLaserProjectile : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static Material _sharedMaterial;

        private WeaponHolder _owner;
        private Transform _ownerRoot;
        private Vector3 _direction;
        private float _speed;
        private float _remainingDistance;
        private MiningTechStats _stats;

        public static void Spawn(
            Vector3 origin,
            Vector3 direction,
            float speed,
            float maxDistance,
            WeaponHolder owner,
            MiningTechStats stats)
        {
            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Mining Laser Projectile";
            projectile.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            projectile.transform.localScale = Vector3.one * 0.16f;

            Collider primitiveCollider = projectile.GetComponent<Collider>();
            if (primitiveCollider != null)
                Destroy(primitiveCollider);

            MeshRenderer renderer = projectile.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetSharedMaterial();

            MiningLaserProjectile behaviour = projectile.AddComponent<MiningLaserProjectile>();
            behaviour._owner = owner;
            behaviour._ownerRoot = owner.transform.root;
            behaviour._direction = direction.normalized;
            behaviour._speed = speed;
            behaviour._remainingDistance = maxDistance;
            behaviour._stats = stats;
        }

        private void Update()
        {
            float travelDistance = Mathf.Min(_speed * Time.deltaTime, _remainingDistance);
            if (TryGetHit(travelDistance, out RaycastHit hit))
            {
                transform.position = hit.point;
                _owner?.HandleLaserImpact(hit.collider, _stats);
                Destroy(gameObject);
                return;
            }

            transform.position += _direction * travelDistance;
            _remainingDistance -= travelDistance;
            if (_remainingDistance <= 0f)
                Destroy(gameObject);
        }

        private bool TryGetHit(float distance, out RaycastHit nearestHit)
        {
            nearestHit = default;
            int hitCount = Physics.RaycastNonAlloc(
                transform.position,
                _direction,
                Hits,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);
            float nearestDistance = distance;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider == null || hit.distance >= nearestDistance)
                    continue;
                if (_ownerRoot != null && hit.collider.transform.IsChildOf(_ownerRoot))
                    continue;

                nearestDistance = hit.distance;
                nearestHit = hit;
            }

            return nearestHit.collider != null;
        }

        private static Material GetSharedMaterial()
        {
            if (_sharedMaterial != null)
                return _sharedMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            _sharedMaterial = new Material(shader)
            {
                name = "Runtime Mining Laser",
                color = new Color(0.2f, 1f, 0.95f, 1f),
                hideFlags = HideFlags.HideAndDontSave
            };
            if (_sharedMaterial.HasProperty("_BaseColor"))
                _sharedMaterial.SetColor("_BaseColor", new Color(0.2f, 1f, 0.95f, 1f));
            if (_sharedMaterial.HasProperty("_EmissionColor"))
                _sharedMaterial.SetColor("_EmissionColor", new Color(0.4f, 3f, 2.5f, 1f));
            return _sharedMaterial;
        }
    }
}
