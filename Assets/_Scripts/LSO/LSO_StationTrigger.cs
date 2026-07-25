using System;
using _Scripts.Suxghui.Manager;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class LSO_StationTrigger : MonoBehaviour
{
    public enum TriggerAction
    {
        Upgrade = 0,
        Refuel = 1
    }

    [SerializeField] private TriggerAction action;
    [SerializeField] private string targetTag = "Player";

    private static bool _upgradeSceneRequested;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string sceneName = scene.name;
        if (!string.Equals(sceneName, "StarField", StringComparison.Ordinal) &&
            !string.Equals(sceneName, "LSO_StarField", StringComparison.Ordinal))
            return;

        // A new StarField session must accept docking again after returning from
        // the upgrade scene.
        _upgradeSceneRequested = false;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform target = transforms[i];
                if (target == null)
                    continue;

                if (IsNamed(target, "Heal") || IsNamed(target, "Healspot"))
                    EnsureTrigger(target.gameObject, TriggerAction.Refuel);
                else if (IsNamed(target, "Upgrade") || IsNamed(target, "Input"))
                    EnsureTrigger(target.gameObject, TriggerAction.Upgrade);
            }
        }
    }

    private static bool IsNamed(Transform target, string expectedName)
    {
        return string.Equals(target.name, expectedName, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureTrigger(GameObject target, TriggerAction triggerAction)
    {
        Collider triggerCollider = target.GetComponent<Collider>();
        if (triggerCollider == null)
            triggerCollider = target.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;

        LSO_StationTrigger trigger = target.GetComponent<LSO_StationTrigger>();
        if (trigger == null)
            trigger = target.AddComponent<LSO_StationTrigger>();
        trigger.action = triggerAction;
    }

    private void Reset()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Runtime bootstrap can attach this component after the ship has already
        // entered the trigger, in which case OnTriggerEnter is not sent again.
        if (action == TriggerAction.Upgrade)
            HandleTrigger(other);
    }

    private void HandleTrigger(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        GameManager manager = GameManager.Instance;
        if (manager == null)
            return;

        if (action == TriggerAction.Refuel)
        {
            float restored = manager.RestoreFuel(manager.SaveData.maxFuel);
            manager.Save();
            NotificationManager.Notify($"회복 구역: 연료 {restored:0.#} 회복");
            Debug.Log($"[정거장] 연료 {restored:0.#} 충전 완료 " +
                      $"({manager.SaveData.fuel:0.#}/{manager.SaveData.maxFuel:0.#})", this);
            return;
        }

        if (_upgradeSceneRequested)
            return;

        _upgradeSceneRequested = true;
        NotificationManager.Notify("업그레이드 구역으로 이동합니다");
        manager.Save();
        manager.ChangeSceneState(manager.UpgradeState);
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null || string.IsNullOrWhiteSpace(targetTag))
            return false;

        if (HasTagInParents(other.transform, targetTag))
            return true;

        Rigidbody attachedBody = other.attachedRigidbody;
        return attachedBody != null && HasTagInParents(attachedBody.transform, targetTag);
    }

    private static bool HasTagInParents(Transform target, string requiredTag)
    {
        while (target != null)
        {
            if (target.CompareTag(requiredTag))
                return true;
            target = target.parent;
        }

        return false;
    }
}
