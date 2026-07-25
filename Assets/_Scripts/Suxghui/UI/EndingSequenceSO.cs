using System;
using System.Collections.Generic;
using _Scripts.LHS.SoundManager;
using UnityEngine;

namespace _Scripts.Suxghui.UI
{
    [Serializable]
    public sealed class EndingLine
    {
        [TextArea(2, 6)] [SerializeField] private string text;
        [SerializeField, Min(0f)] private float holdDuration = 1.5f;

        public string Text => text ?? string.Empty;
        public float HoldDuration => Mathf.Max(0f, holdDuration);
    }

    [CreateAssetMenu(fileName = "EndingSequence", menuName = "LSO/Ending Sequence")]
    public sealed class EndingSequenceSO : ScriptableObject
    {
        [Header("Ending Condition")]
        [SerializeField, Min(1)] private int requiredCoins = 100000;

        [Header("Script")]
        [SerializeField] private List<EndingLine> lines = new();
        [SerializeField, Min(0.005f)] private float characterInterval = 0.045f;

        [Header("Typing SFX")]
        [SerializeField] private SoundDataBaseSO soundDatabase;
        [SerializeField] private string typingSoundId = "EndingTyping";
        [SerializeField, Min(1)] private int soundEveryCharacters = 2;

        public int RequiredCoins => Mathf.Max(1, requiredCoins);
        public IReadOnlyList<EndingLine> Lines => lines;
        public float CharacterInterval => Mathf.Max(0.005f, characterInterval);
        public SoundDataBaseSO SoundDatabase => soundDatabase;
        public string TypingSoundId => typingSoundId;
        public int SoundEveryCharacters => Mathf.Max(1, soundEveryCharacters);
    }
}
