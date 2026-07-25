using _Scripts.LHS.Sound;
using _Scripts.LHS.SoundManager;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _Scripts.Suxghui.UI
{
    [DisallowMultipleComponent]
    public sealed class GlobalButtonSound : MonoBehaviour
    {
        private Button _button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= BindSceneButtons;
            SceneManager.sceneLoaded += BindSceneButtons;
        }

        private static void BindSceneButtons(Scene scene, LoadSceneMode mode)
        {
            foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button == null || button.GetComponent<LSO_ButtonScale>() != null)
                    continue;
                if (button.GetComponent<GlobalButtonSound>() == null)
                    button.gameObject.AddComponent<GlobalButtonSound>();
            }
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null)
                _button.onClick.AddListener(PlayClick);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(PlayClick);
        }

        private static void PlayClick()
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.Play(SoundType.UI, "Click01");
        }
    }
}
