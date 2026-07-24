using UnityEngine;
using UnityEngine.Audio;

public class AudioSettingsController : MonoBehaviour
{
    [SerializeField] private AudioMixer mixer;

    private const string MasterKey = "Master";
    private const string SFXKey = "SFX";
    private const string BGMKey = "BGM";
    private const string UIKey = "UI";

    public void SetMasterVolume(float sliderValue)
    {
        SetVolume(MasterKey, sliderValue);
    }

    public void SetSFXVolume(float sliderValue)
    {
        SetVolume(SFXKey, sliderValue);
    }

    public void SetBGMVolume(float sliderValue)
    {
        SetVolume(BGMKey, sliderValue);
    }

    public void SetUIVolume(float sliderValue)
    {
        SetVolume(UIKey, sliderValue);
    }

    private void SetVolume(string paramName, float sliderValue)
    {
        float dB = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
        mixer.SetFloat(paramName, dB);
    }
}