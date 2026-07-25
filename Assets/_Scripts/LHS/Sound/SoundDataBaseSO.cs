using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.LHS.Sound
{
    public enum SoundType
    {
        SFX, UI, BGM, Other
    }

    [System.Serializable]
    public class SoundInfo
    {
        public string soundID;
        public SoundType type;
        public AudioSource audioSourcePrefab;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool looping;

        [Header("Pitch")]
        [Range(-3f, 3f)]
        public float pitch = 1f;
        public bool randomizePitch;
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

        [Header("Fade")]
        public bool useFade;
        [Range(0f, 5f)] public float fadeInDuration = 0.5f;
        [Range(0f, 5f)] public float fadeOutDuration = 0.5f;
    }

    [CreateAssetMenu(fileName = "SoundDataBaseSO", menuName = "SO/SoundDataBaseSO")]
    public class SoundDataBaseSO : ScriptableObject
    {
        public List<SoundInfo> sounds;

        private Dictionary<string, SoundInfo> _lookup;

        private void OnEnable()
        {
            _lookup = new Dictionary<string, SoundInfo>();

            if (sounds == null)
            {
                sounds = new List<SoundInfo>();
                return;
            }

            foreach (var s in sounds)
            {
                string key = MakeKey(s.type, s.soundID);
                if (!_lookup.ContainsKey(key))
                    _lookup.Add(key, s);
                else
                    Debug.LogWarning($"중복된 사운드 키: {key}");
            }
        }

        private string MakeKey(SoundType type, string soundID) => $"{type}_{soundID}";

        public SoundInfo GetSound(SoundType type, string soundID)
        {
            string key = MakeKey(type, soundID);
            if (_lookup.TryGetValue(key, out var info)) return info;

            Debug.LogWarning($"등록 안 된 사운드: {type}/{soundID}");
            return null;
        }
    }
}