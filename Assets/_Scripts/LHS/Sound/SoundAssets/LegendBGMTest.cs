using System;
using _Scripts.LHS.Sound;
using _Scripts.LHS.SoundManager;
using UnityEngine;

public class LegendBGMTest : MonoBehaviour
{
    private void Start()
    {
        SoundManager.Instance.Play(SoundType.BGM,"MainBGM");
    }
}
