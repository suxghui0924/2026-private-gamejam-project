using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.LHS.SoundManager
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [SerializeField] private SoundDataBaseSo database;

        private readonly Dictionary<AudioSource, Queue<AudioSource>> _pools = new();
        private readonly Dictionary<string, AudioSource> _activeSources = new();
        private readonly Dictionary<string, Coroutine> _returnCoroutines = new();
        private readonly Dictionary<AudioSource, AudioSource> _instanceToPrefab = new();
        private string _currentBGMKey;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private string MakeKey(SoundType type, string ID) => $"{type}_{ID}";

        public void Play(SoundType type, string ID)
        {
            var info = database.GetSound(type, ID);
            if (info == null || info.audioSourcePrefab == null || info.clip == null) return;

            string key = MakeKey(type, ID);

            if (info.looping)
            {
                if (type == SoundType.BGM && _currentBGMKey != null && _currentBGMKey != key)
                    StopByKey(_currentBGMKey);

                if (type == SoundType.BGM)
                    _currentBGMKey = key;

                if (_activeSources.ContainsKey(key))
                    StopByKey(key);
            }

            AudioSource source = GetFromPool(info.audioSourcePrefab);
            source.clip = info.clip;
            source.volume = info.volume;
            source.loop = info.looping;
            source.Play();

            _activeSources[key] = source;

            if (!info.looping)
            {
                var co = StartCoroutine(ReturnAfterPlay(key, source, info.clip.length));
                _returnCoroutines[key] = co;
            }
        }

        public void Stop(SoundType type, string ID)
        {
            string key = MakeKey(type, ID);
            StopByKey(key);
        }

        private void StopByKey(string key)
        {
            if (!_activeSources.TryGetValue(key, out var source)) return;

            if (_returnCoroutines.TryGetValue(key, out var co))
            {
                StopCoroutine(co);
                _returnCoroutines.Remove(key);
            }

            source.Stop();
            source.gameObject.SetActive(false);

            if (_instanceToPrefab.TryGetValue(source, out var prefabKey))
                ReturnToPool(prefabKey, source);

            _activeSources.Remove(key);

            if (key == _currentBGMKey)
                _currentBGMKey = null;
        }

        private AudioSource GetFromPool(AudioSource prefab)
        {
            if (_pools.TryGetValue(prefab, out var queue) && queue.Count > 0)
            {
                var source = queue.Dequeue();
                source.gameObject.SetActive(true);
                return source;
            }

            var newSource = Instantiate(prefab, transform);
            _instanceToPrefab[newSource] = prefab;
            return newSource;
        }

        private void ReturnToPool(AudioSource prefabKey, AudioSource source)
        {
            if (!_pools.ContainsKey(prefabKey))
                _pools[prefabKey] = new Queue<AudioSource>();
            _pools[prefabKey].Enqueue(source);
        }

        private IEnumerator ReturnAfterPlay(string key, AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);

            source.Stop();
            source.gameObject.SetActive(false);

            if (_instanceToPrefab.TryGetValue(source, out var prefabKey))
                ReturnToPool(prefabKey, source);

            _activeSources.Remove(key);
            _returnCoroutines.Remove(key);
        }
    }
}