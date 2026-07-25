using UnityEngine;

namespace _Scripts.Suxghui.Manager
{
    public interface ISceneState
    {
        void Enter();
        void Executor();
        void Exit();
    }

    public sealed class LoadingSceneState : ISceneState
    {
        public LoadingSceneState(GameManager manager) => Manager = manager;
        public GameManager Manager { get; }

        public void Enter() { }
        public void Executor() { }
        public void Exit() { }
    }

    public sealed class MainMenuState : ISceneState
    {
        public MainMenuState(GameManager manager) => Manager = manager;
        public GameManager Manager { get; }

        public void Enter() { }
        public void Executor() { }
        public void Exit() { }
    }

    public sealed class ModuleSelectState : ISceneState
    {
        public ModuleSelectState(GameManager manager) => Manager = manager;
        public GameManager Manager { get; }

        public void Enter() { }
        public void Executor() { }
        public void Exit() { }
    }

    public sealed class UpgradeState : ISceneState
    {
        public UpgradeState(GameManager manager) => Manager = manager;
        public GameManager Manager { get; }

        public void Enter()
        {
            int soldFor = Manager.Shop?.SellAll() ?? 0;
            Manager.SetCargoWeight(0f);
            Manager.RestoreFuel(Manager.SaveData.maxFuel);
            Manager.Save();

            if (soldFor > 0)
                UnityEngine.Debug.Log($"[업그레이드 정거장] 원석 전체 판매 완료: +{soldFor}");
        }
        public void Executor() { }
        public void Exit() { }
    }

    public sealed class StarFieldState : ISceneState
    {
        public StarFieldState(GameManager manager) => Manager = manager;
        public GameManager Manager { get; }

        public void Enter()
        {
            if (_Scripts.LHS.SoundManager.SoundManager.Instance != null)
                _Scripts.LHS.SoundManager.SoundManager.Instance.Play(
                    _Scripts.LHS.Sound.SoundType.BGM, "Space");

            GameObject healing = GameObject.Find("HealingVFX");
            if (healing != null)
            {
                healing.SetActive(true);
                foreach (ParticleSystem particles in healing.GetComponentsInChildren<ParticleSystem>(true))
                {
                    particles.Clear(true);
                    particles.Play(true);
                }
            }
        }
        public void Executor() { }
        public void Exit() => Manager.Save();
    }

    public sealed class EndingSceneState : ISceneState
    {
        public EndingSceneState(GameManager manager) => Manager = manager;
        public GameManager Manager { get; }

        public void Enter() => Manager.Save();
        public void Executor() { }
        public void Exit() { }
    }
}
