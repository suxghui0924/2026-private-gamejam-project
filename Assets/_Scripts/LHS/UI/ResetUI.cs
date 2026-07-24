using System;
using UnityEngine;

namespace _Scripts.LHS.UI
{
    public class ResetUI : MonoBehaviour
    {
        [SerializeField] private GameObject alert1;
        [SerializeField] private GameObject alert2;
        
        private void Start()
        {
            ResetNo1();
            ResetNo2();
        }

        public void ResetButton()
        {
            if (alert1.activeSelf || alert2.activeSelf) return;
            alert1.SetActive(true);
        }
        public void ResetYes1()
        {
            alert2.SetActive(true);
            alert1.SetActive(false);
        }

        public void ResetNo1()
        {
            alert1.SetActive(false);
        }

        public void ResetYes2()
        {
           LoadingSceneController.LoadScene("ResetScene");
            alert2.SetActive(false);
        }
        public void ResetNo2()
        {
            alert2.SetActive(false);
        }
    }
}
