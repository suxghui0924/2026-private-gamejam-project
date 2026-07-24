using System.Collections.Generic;
using _Scripts.LSO;
using _Scripts.LSO.Data;
using _Scripts.Suxghui.Manager;
using UnityEngine;

namespace _Scripts.Suxghui.Mining
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class GetItemComponent : MonoBehaviour
    {
        [Header("Suction")]
        [SerializeField, Min(0.01f)] private float collectDistance = 1.2f;
        [SerializeField, Min(0f)] private float minimumSuctionDuration = 0.4f;
        [SerializeField, Min(0f)] private float suctionAcceleration = 8f;
        [SerializeField, Min(0f)] private float maximumSuctionSpeed = 4f;

        [Header("Inventory Compatibility")]
        [Tooltip("현재 LSO 인벤토리와 저장용 GameManager 인벤토리를 함께 동기화합니다.")]
        [SerializeField] private bool synchronizePersistentInventory = true;

        private readonly HashSet<MineralPickup> _nearbyPickups = new HashSet<MineralPickup>();
        private readonly List<MineralPickup> _iterationBuffer = new List<MineralPickup>();
        private readonly Dictionary<MineralPickup, float> _suctionStartedAt =
            new Dictionary<MineralPickup, float>();
        private LSO_PlayerInventory _playerInventory;
        private LSO_Weight _cargoWeight;
        private SphereCollider _collectionTrigger;

        private void Awake()
        {
            _collectionTrigger = GetComponent<SphereCollider>();
            _collectionTrigger.isTrigger = true;
            CacheInventory();
        }

        private void OnTriggerEnter(Collider other)
        {
            Track(other);
        }

        private void OnTriggerStay(Collider other)
        {
            Track(other);
        }

        private void OnTriggerExit(Collider other)
        {
            MineralPickup pickup = ResolvePickup(other);
            if (pickup != null)
            {
                _nearbyPickups.Remove(pickup);
                _suctionStartedAt.Remove(pickup);
            }
        }

        private void FixedUpdate()
        {
            if (_nearbyPickups.Count == 0)
                return;

            _iterationBuffer.Clear();
            _iterationBuffer.AddRange(_nearbyPickups);
            Vector3 destination = transform.position;
            float effectiveCollectDistance = GetWorldCollectDistance();
            float collectDistanceSquared = effectiveCollectDistance * effectiveCollectDistance;

            for (int i = 0; i < _iterationBuffer.Count; i++)
            {
                MineralPickup pickup = _iterationBuffer[i];
                if (pickup == null || !pickup.IsCollectible)
                {
                    _nearbyPickups.Remove(pickup);
                    _suctionStartedAt.Remove(pickup);
                    continue;
                }

                pickup.AttractTowards(destination, suctionAcceleration, maximumSuctionSpeed);
                bool suctionPlayedLongEnough = _suctionStartedAt.TryGetValue(pickup, out float startedAt) &&
                                               Time.time - startedAt >= minimumSuctionDuration;
                if (suctionPlayedLongEnough &&
                    (pickup.transform.position - destination).sqrMagnitude <= collectDistanceSquared)
                    TryCollect(pickup);
            }
        }

        private void Track(Collider other)
        {
            MineralPickup pickup = ResolvePickup(other);
            if (pickup != null && pickup.IsCollectible && _nearbyPickups.Add(pickup))
                _suctionStartedAt[pickup] = Time.time;
        }

        private void TryCollect(MineralPickup pickup)
        {
            CacheInventory();
            GameManager gameManager = GameManager.Instance;
            if (_playerInventory == null &&
                (!synchronizePersistentInventory || gameManager?.Inventory == null))
                return;

            int capacity = _cargoWeight != null
                ? _cargoWeight.RemainingCapacity
                : int.MaxValue;
            int taken = pickup.Take(capacity);
            if (taken <= 0)
                return;

            LSO_MineralSO mineral = pickup.Mineral;

            if (_playerInventory != null)
                _playerInventory.AddItem(mineral, taken);

            if (synchronizePersistentInventory && gameManager?.Inventory != null)
                gameManager.Inventory.AddItem(mineral, taken);

            _cargoWeight?.AddWeight(taken);

            if (synchronizePersistentInventory && gameManager != null)
            {
                float savedWeight = _cargoWeight != null
                    ? _cargoWeight.Weight
                    : gameManager.SaveData.cargoWeight + taken;
                gameManager.SetCargoWeight(savedWeight);
                gameManager.Save();
            }

            string mineralName = !string.IsNullOrWhiteSpace(mineral?.mineralName)
                ? mineral.mineralName
                : mineral != null ? mineral.name : "알 수 없는 원석";
            int totalAmount = gameManager?.Inventory != null
                ? gameManager.Inventory.GetItemAmount(mineral)
                : _playerInventory != null && _playerInventory.PlayerInventory.TryGetValue(mineral, out int amount)
                    ? amount
                    : taken;
            Debug.Log($"[원석 획득] {mineralName} x{taken} (보유: {totalAmount}) - SaveData 동기화 완료", this);

            if (pickup.Amount > 0)
                return;

            _nearbyPickups.Remove(pickup);
            _suctionStartedAt.Remove(pickup);
            Destroy(pickup.gameObject);
        }

        private void CacheInventory()
        {
            if (_playerInventory == null)
                _playerInventory = LSO_PlayerInventory.Instance ?? FindFirstObjectByType<LSO_PlayerInventory>();
            if (_cargoWeight == null)
                _cargoWeight = LSO_Weight.Instance ?? FindFirstObjectByType<LSO_Weight>();
        }

        private float GetWorldCollectDistance()
        {
            // Collection distance is deliberately world-space. Collectible ore colliders are
            // triggers, so they can visibly pass through the hull before reaching this point.
            return Mathf.Max(0.01f, collectDistance);
        }

        private static MineralPickup ResolvePickup(Collider other)
        {
            return other != null
                ? other.GetComponent<MineralPickup>() ?? other.GetComponentInParent<MineralPickup>()
                : null;
        }
    }
}
