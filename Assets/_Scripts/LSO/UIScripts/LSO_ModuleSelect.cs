using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Mining;
using UnityEngine;

public class LSO_ModuleSelect : MonoBehaviour
{
    public MiningTechType miningTechType;
    public void Select()
    {
        GameManager.Instance.TechSelection.Select(miningTechType);
        GameManager.Instance.ChangeSceneState(GameManager.SceneType.StarField);
    }
}
