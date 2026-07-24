using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.LHS.SoundManager
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
    }

    [CreateAssetMenu(fileName = "SoundDataBaseSO", menuName = "Scriptable Objects/SoundDataBaseSO")]
    public class SoundDataBaseSo : ScriptableObject
    {
        public List<SoundInfo> sounds;

        private Dictionary<string, SoundInfo> _lookup;

        private void OnEnable()
        {
            _lookup = new Dictionary<string, SoundInfo>();
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