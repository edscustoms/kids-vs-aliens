using System;
using System.Collections.Generic;
using UnityEngine;

// Owns activation only. Each authored enemy keeps its existing health, AI and run state.
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider), typeof(RunWorldObject))]
public sealed class AuthoredEncounter : MonoBehaviour, IRunStateParticipant
{
    [SerializeField] private BoxCollider entryVolume;
    [Tooltip("Disable for encounters activated explicitly by a mission or chest action.")]
    [SerializeField] private bool activateOnEntry = true;
    [Tooltip("Scene-authored, initially inactive enemies with stable RunWorldObject identities.")]
    [SerializeField] private RunWorldObject[] enemies = Array.Empty<RunWorldObject>();
    [Header("Optional activation actions")]
    [SerializeField] private GameplayActions onActivated = new();
    private bool triggered;

    public bool HasTriggered => triggered;
    public IReadOnlyList<RunWorldObject> Enemies => enemies;
    public string RunStateKey => "authored-encounter";
    public bool IsComplete
    {
        get
        {
            if (!triggered || enemies.Length == 0) return false;
            foreach (var enemy in enemies)
                if (enemy != null && !enemy.IsRemoved && !enemy.GetComponent<EnemyHealth>().IsDead)
                    return false;
            return true;
        }
    }

    private void Reset()
    {
        entryVolume = GetComponent<BoxCollider>();
        entryVolume.isTrigger = true;
    }

    private void Awake()
    {
        if (entryVolume == null) entryVolume = GetComponent<BoxCollider>();
    }

    private void OnTriggerEnter(Collider other) => TryActivate(other.GetComponentInParent<PlayerCharacter>());
    private void OnTriggerStay(Collider other) => TryActivate(other.GetComponentInParent<PlayerCharacter>());

    public bool TryActivate(PlayerCharacter player)
    {
        if (!activateOnEntry || triggered || !isActiveAndEnabled || player == null || !player.isActiveAndEnabled) return false;
        var run = ActiveRunController.Instance;
        if (run == null || !run.IsReady || run.gameObject != player.gameObject) return false;
        if (player.GetComponent<PlayerHealth>().IsDead || player.GetComponent<GameplaySuspensionController>().IsSuspended) return false;
        // Capsule overlap at the landing edge is not commitment to the platform.
        Vector3 local = entryVolume.transform.InverseTransformPoint(player.transform.position) - entryVolume.center;
        Vector3 half = entryVolume.size * .5f;
        if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z) return false;

        return ActivateMembers(player, true);
    }

    // Explicit owners activate the authored positions without attracting distant guards to Amy.
    public bool Activate()
    {
        var run = ActiveRunController.Instance;
        if (triggered || !isActiveAndEnabled || run == null || !run.IsReady) return false;
        return ActivateMembers(run.GetComponent<PlayerCharacter>(), false);
    }

    private bool ActivateMembers(PlayerCharacter player, bool investigatePlayer)
    {
        triggered = true;
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsRemoved) continue;
            enemy.gameObject.SetActive(true);
            if (investigatePlayer) enemy.GetComponent<EnemyBrain>().InvestigatePosition(player.transform.position);
        }
        ActiveRunController.Instance.MarkDirty();
        onActivated.Execute(player);
        return true;
    }

    [Serializable] private struct State { public bool triggered; }
    public string CaptureRunState() => JsonUtility.ToJson(new State { triggered = triggered });
    public void RestoreRunState(string json)
    {
        // Both restore passes assign only our state. Peers restore their own active/dead
        // state and awareness; activating them here could resurrect a removed enemy.
        triggered = JsonUtility.FromJson<State>(json).triggered;
    }
}
