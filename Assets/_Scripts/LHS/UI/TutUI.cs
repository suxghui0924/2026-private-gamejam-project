using UnityEngine;

namespace _Scripts.LHS.UI
{
    public class TutUI : MonoBehaviour
    {
        [SerializeField] private GameObject tutUI;
        [SerializeField] private GameObject tut1;
        [SerializeField] private GameObject tut2;
        [SerializeField] private GameObject tut3;

        private int current = 0;
        private void Start()
        {
            ResetNo1();
            ResetNo2();
            ResetNo3();
            disableUI();
        }

        public void TutButton()
        {
            if (tut1.activeSelf || tut2.activeSelf||tut3.activeSelf||current!=0) return;
            tut1.SetActive(true);
            current = 1;
            enableUI();
        }

        public void nextButton()
        {
            if (current == 1)
            {
                ResetNo1();
                current = 2;
                tut2.SetActive(true);
            }
            else if (current == 2)
            {
                ResetNo2();
                current = 3;
                tut3.SetActive(true);
            }
        }

        public void prevButton()
        {
            if (current == 2)
            {
                ResetNo2();
                current = 1;
                tut1.SetActive(true);
            }
            else if (current == 3)
            {
                ResetNo3();
                current = 2;
                tut2.SetActive(true);
            }
        }

        public void closeBtn()
        {
            ResetNo1();
            ResetNo2();
            ResetNo3();
            current = 0;
            disableUI();
        }

        public void disableUI()
        {
            tutUI.SetActive(false);
        }
        public void enableUI()
        {
            tutUI.SetActive(true);
        }
        public void ResetNo1()
        {
            tut1.SetActive(false);
        }
        public void ResetNo2()
        {
            tut2.SetActive(false);
        }
        public void ResetNo3()
        {
            tut3.SetActive(false);
        }
    }
}