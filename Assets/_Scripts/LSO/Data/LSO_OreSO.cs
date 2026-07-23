using _Scripts.LSO.Data;
using UnityEngine;

[CreateAssetMenu(fileName = "New OreSO",menuName = "SO/LSO_OreSO")]
public class LSO_OreSO : ScriptableObject
{
   [Header("이름")]
   public string oreName;
   [Header("광석 색깔")]
   public Color oreColor;
   [Header("채굴시 획득하는 자원")]
   public LSO_MineralSO mineral;
}
