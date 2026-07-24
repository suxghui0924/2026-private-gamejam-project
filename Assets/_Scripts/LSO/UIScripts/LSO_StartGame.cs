using UnityEngine;
using UnityEngine.SceneManagement;

public class LSO_StartGame : MonoBehaviour
{
    [Header("넘어 가는 씬")]
    [SerializeField] private string startScene = "StarField";
    
    public void StartGame()
    {
        LoadingSceneController.LoadScene(startScene);
    }
}
