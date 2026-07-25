using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Mining;
using UnityEngine;

public class LSO_ModuleSelect : MonoBehaviour
{
    public MiningTechType miningTechType;
    public void Select()
    {
        GameManager manager = GameManager.Instance;
        manager.TechSelection.Select(miningTechType);
        manager.ChangeSceneState(manager.StarFieldState);
    }
}
