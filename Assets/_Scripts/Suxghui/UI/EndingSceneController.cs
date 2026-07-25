using System;
using System.Collections;
using _Scripts.LHS.SoundManager;
using _Scripts.LHS.Sound;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Scripts.Suxghui.UI
{
    public sealed class EndingSceneController : MonoBehaviour
    {
        private const string EndingSceneName = "EndingScene";
        private const string SequenceResourcePath = "Suxghui/Ending/EndingSequence";

        [SerializeField] private EndingSequenceSO sequence;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private SoundDataBaseSO soundDatabase;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCurrentScene()
        {
            if (SceneManager.GetActiveScene().name == EndingSceneName)
                EnsureForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, EndingSceneName, StringComparison.Ordinal))
                return;
            EnsureForScene(scene);
        }

        private static void HandleActiveSceneChanged(Scene previous, Scene current)
        {
            if (current.name == EndingSceneName)
                EnsureForScene(current);
        }

        public static void EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || scene.name != EndingSceneName ||
                FindFirstObjectByType<EndingSceneController>() != null)
                return;

            GameObject host = new GameObject(nameof(EndingSceneController));
            host.AddComponent<EndingSceneController>();
            SceneManager.MoveGameObjectToScene(host, scene);
        }

        private void Start()
        {
            if (sequence == null)
                sequence = Resources.Load<EndingSequenceSO>(SequenceResourcePath);
            if (dialogueText == null)
                dialogueText = FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
            if (soundDatabase == null && sequence != null)
                soundDatabase = sequence.SoundDatabase;

            if (sequence == null || dialogueText == null)
            {
                Debug.LogError("[Ending] EndingSequence or Text (TMP) could not be found.", this);
                return;
            }

            StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            dialogueText.text = string.Empty;

            for (int lineIndex = 0; lineIndex < sequence.Lines.Count; lineIndex++)
            {
                EndingLine line = sequence.Lines[lineIndex];
                if (line == null)
                    continue;

                dialogueText.text = line.Text;
                dialogueText.maxVisibleCharacters = 0;
                dialogueText.ForceMeshUpdate();

                int characterCount = dialogueText.textInfo.characterCount;
                int soundedCharacters = 0;
                for (int visibleCount = 1; visibleCount <= characterCount; visibleCount++)
                {
                    dialogueText.maxVisibleCharacters = visibleCount;

                    TMP_CharacterInfo character = dialogueText.textInfo.characterInfo[visibleCount - 1];
                    if (!char.IsWhiteSpace(character.character))
                    {
                        soundedCharacters++;
                        if (soundedCharacters % sequence.SoundEveryCharacters == 0)
                            PlayTypingSound();
                    }

                    yield return new WaitForSecondsRealtime(sequence.CharacterInterval);
                }

                yield return new WaitForSecondsRealtime(line.HoldDuration);
            }

            // The ending is a terminal scene. SaveData.endingReached remains
            // true, so a later launch will not retrigger it until ResetAll.
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void PlayTypingSound()
        {
            SoundManager soundManager = SoundManager.Instance;
            if (soundManager == null || soundDatabase == null ||
                string.IsNullOrWhiteSpace(sequence.TypingSoundId))
                return;

            soundManager.Play(soundDatabase, SoundType.SFX, sequence.TypingSoundId);
        }
    }
}
