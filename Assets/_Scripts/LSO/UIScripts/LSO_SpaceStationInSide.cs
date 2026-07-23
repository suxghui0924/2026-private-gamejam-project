using Unity.Cinemachine;
using UnityEngine;

public class LSO_SpaceStationInSide : MonoBehaviour
{
    [SerializeField] private CinemachineCamera stationInnerCam;
    [SerializeField] private Transform output;
    private GameObject _spaceShip;
    
    private void OnTriggerEnter(Collider other)
    {
       if (!other.CompareTag("Player")) return;

       stationInnerCam.Priority = 2;
    }
    
    private void Out()
    {
        stationInnerCam.Priority = -1;
        _spaceShip.transform.position = output.position;
        _spaceShip.transform.rotation = output.rotation;
    }
    
    [ContextMenu("In")]
    private void Test()
    {
        stationInnerCam.Priority = 2;
    }
    
    [ContextMenu("Out")]    
    private void Test1()
    {
        stationInnerCam.Priority = -1;
    }
}
