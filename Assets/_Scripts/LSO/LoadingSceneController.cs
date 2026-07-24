using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoadingSceneController : MonoBehaviour
{
    private const string DefaultScene = "LSO_MainMenu";
    private static string _nextScene;

    [SerializeField] private Slider progressBar;

    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[LoadingSceneController] 이동할 씬 이름이 비어 있습니다.");
            return;
        }

        _nextScene = sceneName;
        SceneManager.LoadScene("LoadingScene");
    }

    private void Awake()
    {
        // 슬라이더를 진행도 표시 전용으로 세팅
        if (progressBar != null)
        {
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;
            progressBar.interactable = false;   // 유저가 드래그하지 못하게
            progressBar.transition = Selectable.Transition.None;
        }
    }

    private void Start()
    {
        if (string.IsNullOrWhiteSpace(_nextScene))
            _nextScene = DefaultScene;

        StartCoroutine(LoadSceneProgress());
    }

    private IEnumerator LoadSceneProgress()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(_nextScene);
        op.allowSceneActivation = false;

        float timer = 0f;
        while (!op.isDone)
        {
            yield return null;

            if (op.progress < 0.8f)
            {
                progressBar.value = op.progress;
            }
            else
            {
                timer += Time.unscaledDeltaTime;
                progressBar.value = Mathf.Lerp(0.8f, 1f, timer);

                if (progressBar.value >= 1f)
                {
                    op.allowSceneActivation = true;
                    yield break;
                }
            }
        }
    }
}
