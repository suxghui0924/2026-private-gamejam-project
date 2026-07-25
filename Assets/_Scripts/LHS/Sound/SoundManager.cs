using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace _Scripts.LHS.Sound
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [SerializeField] private SoundDataBaseSO database;

        private readonly Dictionary<AudioSource, Queue<AudioSource>> _pools = new();
        private readonly Dictionary<string, AudioSource> _activeSources = new();
        private readonly Dictionary<string, SoundInfo> _activeSoundInfo = new();
        private readonly Dictionary<string, Coroutine> _returnCoroutines = new();
        private readonly Dictionary<string, Coroutine> _fadeCoroutines = new();
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

            // 같은 키로 남아있던 이전 페이드 코루틴이 있으면 정리 (충돌 방지)
            if (_fadeCoroutines.TryGetValue(key, out var leftoverFade))
            {
                StopCoroutine(leftoverFade);
                _fadeCoroutines.Remove(key);
            }

            AudioSource source = GetFromPool(info.audioSourcePrefab);

            source.gameObject.SetActive(true);
            source.enabled = true;
            source.clip = info.clip;
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

            if (info.useFade && info.fadeInDuration > 0f)
            {
                source.volume = 0f;
                source.Play();
                var fadeCo = StartCoroutine(FadeVolume(key, source, info.volume, info.fadeInDuration));
                _fadeCoroutines[key] = fadeCo;
            }
            else
            {
                source.volume = info.volume;
                source.Play();
            }

            _activeSources[key] = source;
            _activeSoundInfo[key] = info;

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

            // 자연 종료 예약(코루틴)이 있으면 취소 - 수동으로 끄는 거니까
            if (_returnCoroutines.TryGetValue(key, out var returnCo))
            {
                StopCoroutine(returnCo);
                _returnCoroutines.Remove(key);
            }

            // 기존 페이드 코루틴(예: 페이드 인 중이었다면)이 있으면 취소하고 페이드 아웃으로 교체
            if (_fadeCoroutines.TryGetValue(key, out var existingFade))
            {
                StopCoroutine(existingFade);
                _fadeCoroutines.Remove(key);
            }

            _activeSoundInfo.TryGetValue(key, out var info);
            bool useFade = info != null && info.useFade && info.fadeOutDuration > 0f;

            if (useFade)
            {
                var fadeCo = StartCoroutine(FadeOutAndStop(key, source, info.fadeOutDuration));
                _fadeCoroutines[key] = fadeCo;
            }
            else
            {
                FinalizeStop(key, source);
            }
        }

        private IEnumerator FadeVolume(string key, AudioSource source, float targetVolume, float duration)
        {
            float startVolume = source.volume;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, targetVolume, t / duration);
                yield return null;
            }

            source.volume = targetVolume;
            _fadeCoroutines.Remove(key);
        }

        private IEnumerator FadeOutAndStop(string key, AudioSource source, float duration)
        {
            float startVolume = source.volume;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, t / duration);
                yield return null;
            }

            source.volume = 0f;
            _fadeCoroutines.Remove(key);
            FinalizeStop(key, source);
        }

        // 실제 정지 + 풀 반납 + 추적 데이터 정리.
        // 같은 키가 그 사이 새 사운드로 재사용됐다면(비동기 페이드 도중 재생 겹침),
        // 새 인스턴스의 추적 데이터를 잘못 지우지 않도록 참조를 비교해서만 정리함.
        private void FinalizeStop(string key, AudioSource source)
        {
            source.Stop();
            source.gameObject.SetActive(false);

            if (_instanceToPrefab.TryGetValue(source, out var prefabKey))
                ReturnToPool(prefabKey, source);

            if (_activeSources.TryGetValue(key, out var currentSource) && currentSource == source)
            {
                _activeSources.Remove(key);
                _activeSoundInfo.Remove(key);

                if (key == _currentBGMKey)
                    _currentBGMKey = null;
            }
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

            // 자연 종료 시점에 페이드 코루틴이 아직 남아있으면(짧은 클립 + 긴 페이드 인 등) 정리
            if (_fadeCoroutines.TryGetValue(key, out var fadeCo))
            {
                StopCoroutine(fadeCo);
                _fadeCoroutines.Remove(key);
            }

            _returnCoroutines.Remove(key);
            FinalizeStop(key, source);
        }
    }
}