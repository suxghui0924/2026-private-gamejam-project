using System;
using System.Collections;
using System.Diagnostics;
using _Scripts.LHS.Sound;
using _Scripts.LHS.SoundManager;
using _Scripts.Suxghui.Manager;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class ResetCutScene : MonoBehaviour
{
    [SerializeField] private CinemachineImpulseSource _cinemachineImpulseSource;
    [SerializeField] private ParticleSystem _first;
    [SerializeField] private ParticleSystem _second;
    [SerializeField] private ParticleSystem _third;

    [SerializeField] private Image niga;
    
    
    [SerializeField] private CinemachineCamera _cinemachineCamera1;
    [SerializeField] private CinemachineCamera _cinemachineCamera2;

    private Sequence _seq;
    
    private void Start()
    {
        StartCoroutine(ThirdImpact());
    }

    private IEnumerator ThirdImpact()
    {
        var color = niga.color;
        color.a = 0;
        niga.color = color;
        _seq = DOTween.Sequence();
        _seq.AppendCallback(() =>
            {
                _cinemachineCamera1.Priority = 10;
                _cinemachineCamera2.Priority = 1;
            })
            .AppendInterval(0.5f)
            .AppendCallback(() =>
            {
                SoundManager.Instance.Play(SoundType.SFX,"Alert");
                CameraShake(2, 0.4f);
                CameraShake(2, 0.5f);
                CameraShake(2, 0.7f);
                CameraShake(2, 0.9f);
                _first.Play();
            })
            .AppendInterval(0.6f)
            .AppendCallback(() =>
            {
                SoundManager.Instance.Play(SoundType.SFX,"Explosion");
                _cinemachineCamera2.Priority = 20;
                CameraShake(5, 1.2f);
                CameraShake(5, 1.2f);
                CameraShake(5, 1.2f);
                _second.Play();
            })
            .AppendInterval(0.5f)
            .AppendCallback(() =>
            {
                SoundManager.Instance.Play(SoundType.SFX,"Explosion3");
                CameraShake(30, 0.2f);
                CameraShake(30, 0.2f);
                _third.Play();

            })
            .AppendInterval(0.2f)
            .AppendCallback(() =>
            {
                CameraShake(8, 0.5f);
                niga.DOFade(1, 3);
            }).AppendCallback(() =>
            {
                CameraShake(8, 0.5f);
                 niga.DOFade(1, 3);
            })
            .AppendCallback(() =>
            {
            CameraShake(8, 0.5f);
            niga.DOFade(1, 3);
              })
            .AppendInterval(4f);
        yield return _seq.WaitForCompletion();
        GameManager.Instance.ResetSave();
        RestartGame();
    }

        public void RestartGame()
        {
#if UNITY_EDITOR
            SceneManager.LoadScene(0);
#else
        RestartBuiltApplication();
#endif
        }
    
    private void RestartBuiltApplication()
    {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        string exePath = Process.GetCurrentProcess().MainModule.FileName;
        Process.Start(exePath);
        Application.Quit();
#else
    SceneManager.LoadScene(0);
#endif
    }

    public void CameraShake(float force,float duration)
    {
        if (_cinemachineImpulseSource == null) return;

        _cinemachineImpulseSource.ImpulseDefinition.ImpulseDuration = duration;
        _cinemachineImpulseSource.GenerateImpulse(new Vector3(force, force, force));
    }

}
