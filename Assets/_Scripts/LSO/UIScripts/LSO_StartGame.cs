using UnityEngine;
using UnityEngine.SceneManagement;

public class LSO_StartGame : MonoBehaviour
{
    [Header("넘어 가는 씬")]
    [SerializeField] private int sceneIndex = 1;
    
    public void StartGame()
    {
        SceneManager.LoadScene(sceneIndex);
    }
}
