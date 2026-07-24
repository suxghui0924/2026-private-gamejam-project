using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace _Scripts.LHS.SoundManager
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [SerializeField] private SoundDataBaseSO database;

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

        public void Play(SoundType type, string id)
{
    if (database == null)
    {
        Debug.LogError("[SoundManager] SoundDataBaseSO가 할당되지 않았습니다.", this);
        return;
    }

    if (string.IsNullOrWhiteSpace(id))
    {
        Debug.LogError("[SoundManager] Sound ID가 비어 있습니다.", this);
        return;
    }

    var info = database.GetSound(type, id);

    if (info == null)
    {
        Debug.LogError(
            $"[SoundManager] 사운드를 찾지 못했습니다. Type: {type}, ID: {id}",
            database);
        return;
    }

    if (info.audioSourcePrefab == null)
    {
        Debug.LogError(
            $"[SoundManager] AudioSource 프리팹이 없습니다. Type: {type}, ID: {id}",
            database);
        return;
    }

    if (info.clip == null)
    {
        Debug.LogError(
            $"[SoundManager] AudioClip이 없습니다. Type: {type}, ID: {id}",
            database);
        return;
    }

    string key = MakeKey(type, id);

    if (info.looping)
    {
        if (type == SoundType.BGM &&
            !string.IsNullOrEmpty(_currentBGMKey) &&
            _currentBGMKey != key)
        {
            StopByKey(_currentBGMKey);
        }

        if (_activeSources.ContainsKey(key))
        {
            StopByKey(key);
        }

        if (type == SoundType.BGM)
        {
            _currentBGMKey = key;
        }
    }

    AudioSource source = GetFromPool(info.audioSourcePrefab);

    source.gameObject.SetActive(true);
    source.enabled = true;
    source.clip = info.clip;
    source.volume = info.volume;
    source.loop = info.looping;
    source.pitch = info.randomizePitch
        ? Random.Range(info.pitchRange.x, info.pitchRange.y)
        : info.pitch;

    if (type == SoundType.BGM)
    {
        source.spatialBlend = 0f;
    }
    AudioMixerGroup outputGroup = source.outputAudioMixerGroup;

    Debug.Log(
        outputGroup == null
            ? "[SoundManager] MixerGroup: None"
            : $"[SoundManager] MixerGroup: {outputGroup.name}, " +
              $"Mixer: {outputGroup.audioMixer.name}"
    );
    source.Play();

    _activeSources[key] = source;

    Debug.Log(
        $"[SoundManager] 재생 요청 완료 | " +
        $"Type: {type}, ID: {id}, Clip: {source.clip.name}, " +
        $"Volume: {source.volume}, IsPlaying: {source.isPlaying}");

    if (!info.looping)
    {
        float duration = info.clip.length / Mathf.Max(Mathf.Abs(source.pitch), 0.01f);

        Coroutine coroutine = StartCoroutine(
            ReturnAfterPlay(key, source, duration));

        _returnCoroutines[key] = coroutine;
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