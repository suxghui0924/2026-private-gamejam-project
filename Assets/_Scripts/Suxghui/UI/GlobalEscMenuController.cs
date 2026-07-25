using _Scripts.Suxghui.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _Scripts.Suxghui.UI
{
    /// <summary>
    /// Keeps the main menu's ESCCanvas available in every scene and wires its
    /// Continue / Setting / Exit buttons without scene-specific inspector events.
    /// </summary>
    public sealed class GlobalEscMenuController : MonoBehaviour
    {
        private const string MainMenuScene = "LSO_MainMenu";
        private const string OpenSettingsOnMainMenuKey = "GlobalEscMenu.OpenSettings";

        private static GlobalEscMenuController _instance;
        private GameObject _escCanvas;
        private bool _buttonsBound;
        private float _timeScaleBeforePause = 1f;
        private bool _cursorVisibleBeforePause;
        private CursorLockMode _cursorLockModeBeforePause;
        private bool _isPausedByMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var host = new GameObject(nameof(GlobalEscMenuController));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<GlobalEscMenuController>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start() => Rebind();

        private void OnDestroy()
        {
            if (_instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            Rebind();
            if (_escCanvas == null) return;
            if (_escCanvas.activeSelf) CloseMenu();
            else OpenMenu();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Rebind();

            if (scene.name == MainMenuScene &&
                PlayerPrefs.GetInt(OpenSettingsOnMainMenuKey, 0) == 1)
            {
                PlayerPrefs.DeleteKey(OpenSettingsOnMainMenuKey);
                PlayerPrefs.Save();
                OpenSettingsWindow();
            }
        }

        private void Rebind()
        {
            if (_escCanvas == null)
                _escCanvas = FindCanvas("ESCCanvas");

            if (_escCanvas == null) return;

            // Returning to the main menu creates a new scene copy. Keep only
            // the persistent panel so two ESC overlays cannot stack.
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas.gameObject == _escCanvas ||
                    canvas.gameObject.name != "ESCCanvas" ||
                    !canvas.gameObject.scene.IsValid())
                    continue;
                Destroy(canvas.gameObject);
            }

            // ESCCanvas is a root object in the main menu. Persisting that root
            // makes the same panel usable after changing to StarField/Upgrade.
            if (_escCanvas.transform.parent == null && _escCanvas.scene.IsValid())
                DontDestroyOnLoad(_escCanvas);

            if (_buttonsBound) return;
            // Prefer the actual button labels because Unity's auto-generated
            // names (Start (1), Start (2), ...) can change between scene edits.
            BindButtonByLabel("CONTINUE", CloseMenu);
            BindButtonByLabel("SETTING", OpenSettingsWindow);
            BindButtonByLabel("EXIT", ExitGame);
            _buttonsBound = true;
            CloseMenu();
        }

        private void BindButton(string parentName, UnityEngine.Events.UnityAction action)
        {
            Transform parent = FindDeep(_escCanvas.transform, parentName);
            Button button = parent != null ? FindButton(parent) : null;
            if (button == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void CloseMenu()
        {
            if (_escCanvas != null) _escCanvas.SetActive(false);
            if (_isPausedByMenu)
            {
                Time.timeScale = _timeScaleBeforePause <= 0f ? 1f : _timeScaleBeforePause;
                Cursor.visible = _cursorVisibleBeforePause;
                Cursor.lockState = _cursorLockModeBeforePause;
                _isPausedByMenu = false;
            }
        }

        private void OpenMenu()
        {
            if (_escCanvas == null) return;
            _timeScaleBeforePause = Time.timeScale;
            _cursorVisibleBeforePause = Cursor.visible;
            _cursorLockModeBeforePause = Cursor.lockState;
            Time.timeScale = 0f;
            _escCanvas.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            _isPausedByMenu = true;
        }

        private void OpenSettingsWindow()
        {
            CloseMenu();
            if (SceneManager.GetActiveScene().name != MainMenuScene)
            {
                PlayerPrefs.SetInt(OpenSettingsOnMainMenuKey, 1);
                PlayerPrefs.Save();
                GameManager manager = GameManager.Instance;
                if (manager != null)
                    manager.ChangeSceneState(manager.MainMenuState);
                else
                    SceneManager.LoadScene(MainMenuScene);
                return;
            }

            Transform settings = FindDeep(SceneManager.GetActiveScene().GetRootGameObjects(), "SettingWindow");
            if (settings == null) return;
            OpenWindow opener = settings.GetComponentInParent<OpenWindow>();
            if (opener != null) opener.Open();
            else settings.gameObject.SetActive(true);
        }

        private static void ExitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private static GameObject FindCanvas(string objectName)
        {
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (Canvas canvas in canvases)
                if (canvas.gameObject.name == objectName && canvas.gameObject.scene.IsValid())
                    return canvas.gameObject;
            return null;
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root == null) return null;
            if (root.name == objectName) return root;
            foreach (Transform child in root)
            {
                Transform result = FindDeep(child, objectName);
                if (result != null) return result;
            }
            return null;
        }

        private static Transform FindDeep(GameObject[] roots, string objectName)
        {
            foreach (GameObject root in roots)
            {
                Transform result = FindDeep(root.transform, objectName);
                if (result != null) return result;
            }
            return null;
        }

        private static Button FindButton(Transform root)
        {
            Button button = root.GetComponent<Button>();
            if (button != null) return button;
            foreach (Button child in root.GetComponentsInChildren<Button>(true))
                return child;
            return null;
        }

        private void BindButtonByLabel(string label, UnityEngine.Events.UnityAction action)
        {
            foreach (TMP_Text text in _escCanvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!string.Equals(text.text?.Trim(), label, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                Button button = text.GetComponentInParent<Button>();
                if (button == null) button = FindButton(text.transform.parent);
                if (button == null) continue;
                button.onClick.RemoveListener(action);
                button.onClick.AddListener(action);
                return;
            }
        }
    }
}
