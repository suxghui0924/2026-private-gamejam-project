using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Suxghui.World
{
    [DisallowMultipleComponent]
    public class PlayerZoneTracker : MonoBehaviour
    {
        public static PlayerZoneTracker Instance { get; private set; }

        [Header("Player")]
        [SerializeField] private Transform playerTransform;

        [Header("Zones")]
        [SerializeField] private List<Zone> zones = new List<Zone>();

        [Header("Update")]
        [SerializeField, Range(0.05f, 1f)] private float checkInterval = 0.1f;

        private ZoneType _currentZone = ZoneType.Normal;
        private float _nextCheckTime;

        public ZoneType CurrentZone => _currentZone;
        public Transform PlayerTransform => playerTransform;

        public event Action<ZoneType> OnZoneChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            FindPlayerIfMissing();
            RefreshZonesIfEmpty();
        }

        private void Start()
        {
            EvaluatePlayerZone(true);
        }

        private void Update()
        {
            if (Time.time < _nextCheckTime)
                return;

            _nextCheckTime = Time.time + checkInterval;
            EvaluatePlayerZone(false);
        }

        public void SetPlayer(Transform player)
        {
            playerTransform = player;
            EvaluatePlayerZone(false);
        }

        public void RefreshZonesIfEmpty()
        {
            RemoveMissingZones();
            if (zones.Count == 0)
                zones.AddRange(FindObjectsByType<Zone>(FindObjectsSortMode.None));
        }

        public void RegisterZone(Zone zone)
        {
            if (zone == null || zones.Contains(zone))
                return;

            zones.Add(zone);
            EvaluatePlayerZone(false);
        }

        public void UnregisterZone(Zone zone)
        {
            if (zone != null && zones.Remove(zone))
                EvaluatePlayerZone(false);
        }

        private void FindPlayerIfMissing()
        {
            if (playerTransform != null)
                return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                return;
            }

            if (Camera.main != null)
                playerTransform = Camera.main.transform;
        }

        private void EvaluatePlayerZone(bool forceEvent)
        {
            if (playerTransform == null)
            {
                FindPlayerIfMissing();
                if (playerTransform == null)
                    return;
            }

            RemoveMissingZones();
            ZoneType detectedZone = ZoneType.Normal;
            bool foundZone = false;

            for (int i = 0; i < zones.Count; i++)
            {
                Zone zone = zones[i];
                if (!zone.Contains(playerTransform.position))
                    continue;

                if (!foundZone || zone.ZoneType > detectedZone)
                {
                    detectedZone = zone.ZoneType;
                    foundZone = true;
                }
            }

            if (!forceEvent && detectedZone == _currentZone)
                return;

            _currentZone = detectedZone;
            OnZoneChanged?.Invoke(_currentZone);
            NotificationManager.Notify($"구역 이동: {_currentZone}");
        }

        private void RemoveMissingZones()
        {
            if (zones == null)
                zones = new List<Zone>();

            for (int i = zones.Count - 1; i >= 0; i--)
            {
                if (zones[i] == null)
                    zones.RemoveAt(i);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
