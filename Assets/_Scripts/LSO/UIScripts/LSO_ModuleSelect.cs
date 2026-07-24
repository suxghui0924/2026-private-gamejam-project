using _Scripts.Suxghui.Manager;
using _Scripts.Suxghui.Mining;
using UnityEngine;

public class LSO_ModuleSelect : MonoBehaviour
{
    public MiningTechType miningTechType;
    private readonly string _startScene = "LSO_StarField";

    public void Select()
    {
        GameManager.Instance.TechSelection.Select(miningTechType);
        LoadingSceneController.LoadScene(_startScene);
    }
}
