using System;
using UnityEngine;

namespace _Scripts.LSO.Data
{
    public class LSO_Ore : MonoBehaviour ,LSO_IMinerable
    {
        public LSO_OreSO oreSO;
        
        private void Start()
        {
            Init();
        }

        private void Init()
        {
        }

        public LSO_MineralSO Mine()
        {
            return oreSO.mineral;
        }
    }
}