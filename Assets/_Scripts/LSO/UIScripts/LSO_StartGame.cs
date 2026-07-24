using _Scripts.Suxghui.Manager;
using UnityEngine;

public class LSO_StartGame : MonoBehaviour
{
    [SerializeField] private GameManager.SceneType targetScene = GameManager.SceneType.ModuleSelect;

    public void StartGame()
    {
        GameManager.Instance.ChangeSceneState(targetScene);
    }
}
