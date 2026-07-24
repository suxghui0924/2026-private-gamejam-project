using UnityEngine;

namespace _Scripts.Suxghui.Manager
{
    public interface ISceneState
    {
        void Enter();
        void Executor();
        void Exit();
    }

    /// <summary>
    /// Attach one derived component to a scene when that scene needs its own state logic.
    /// GameManager automatically discovers it in the active scene.
    /// </summary>
    public abstract class SceneStateBehaviour : MonoBehaviour, ISceneState
    {
        public abstract void Enter();
        public abstract void Executor();
        public abstract void Exit();
    }

    internal sealed class EmptySceneState : ISceneState
    {
        public void Enter() { }
        public void Executor() { }
        public void Exit() { }
    }
}
