using _Scripts.Suxghui.Manager;
using UnityEngine;

public class LSO_StartGame : MonoBehaviour
{
    [SerializeField] private GameManager.SceneType targetScene = GameManager.SceneType.ModuleSelect;

    public void StartGame()
    {
        GameManager manager = GameManager.Instance;
        manager.ChangeSceneState(manager.GetSceneState(targetScene));
    }
}
