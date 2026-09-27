using System;
using UnityEngine;

public enum GameplayTriggerBehavior { OneShot, Repeatable }

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider), typeof(RunWorldObject))]
public sealed class GameplayTrigger : MonoBehaviour, IRunStateParticipant
{
    [SerializeField] private GameplayTriggerBehavior behavior;
    [Header("Optional conditions")]
    [SerializeField] private ObjectiveDefinition requiredObjective;
    [SerializeField] private ObjectiveState requiredState = ObjectiveState.Active;
    [SerializeField] private SkillData requiredKnowledge;
    [Header("Optional actions")]
    [SerializeField] private GameplayActions actions = new();
    [Header("Persistence")]
    [SerializeField] private bool rememberOneShot = true;
    private BoxCollider volume;
    private bool fired, occupied;
    public bool HasFired => fired;
    public string RunStateKey => "gameplay-trigger";
    private void Awake() => volume = GetComponent<BoxCollider>();
    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
    private void OnDisable() => occupied = false;
    private bool Contains(PlayerCharacter player)
    {
        Vector3 p = volume.transform.InverseTransformPoint(player.transform.position) - volume.center;
        Vector3 half = volume.size * .5f;
        return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
    }
    private void OnTriggerEnter(Collider other) => Visit(other.GetComponentInParent<PlayerCharacter>());
    private void OnTriggerStay(Collider other) => Visit(other.GetComponentInParent<PlayerCharacter>());
    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<PlayerCharacter>();
        if (player != null && !Contains(player)) occupied = false;
    }
    public bool Visit(PlayerCharacter player)
    {
        if (!isActiveAndEnabled || player == null) return false;
        if (!Contains(player)) { occupied = false; return false; }
        var run = ActiveRunController.Instance;
        if (occupied || (behavior == GameplayTriggerBehavior.OneShot && fired) || run == null || !run.IsReady || run.gameObject != player.gameObject) return false;
        if (player.GetComponent<PlayerHealth>().IsDead || player.GetComponent<GameplaySuspensionController>().IsSuspended) return false;
        if (requiredKnowledge != null && !player.GetComponent<PlayerSkillState>().HasSkill(requiredKnowledge)) return false;
        if (requiredObjective != null && (ObjectiveController.Instance == null || ObjectiveController.Instance.StateOf(requiredObjective) != requiredState)) return false;
        occupied = fired = true;
        actions.Execute(player);
        run.MarkDirty();
        return true;
    }
    [Serializable] private struct State { public bool fired; }
    public string CaptureRunState() => JsonUtility.ToJson(new State { fired = rememberOneShot && behavior == GameplayTriggerBehavior.OneShot && fired });
    public void RestoreRunState(string json) { fired = JsonUtility.FromJson<State>(json).fired; occupied = false; }
}
