using UnityEngine;
using UnityEngine.SceneManagement;

public class LSO_StartGame : MonoBehaviour
{
    [Header("넘어 가는 씬")]
    [SerializeField] private string startScene = "StarField";
    
    [Header("로드 씬 사용여부")]
    [SerializeField] private bool useLoadScene;
    
    public void StartGame()
    {
        if (useLoadScene)
            LoadingSceneController.LoadScene(startScene);
        else
            SceneManager.LoadScene(startScene);
    }
}
