using System;
using UnityEngine;

public enum GameplayTriggerBehavior { OneShot, Repeatable }

[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider), typeof(RunWorldObject)), RequireComponent(typeof(Rigidbody))]
public sealed class GameplayTrigger : MonoBehaviour, IRunStateParticipant
{
    [SerializeField] private GameplayTriggerBehavior behavior;
    [Tooltip("Optional compound region. All enabled child BoxColliders are one trigger; children need no scripts. Empty uses this object's BoxCollider.")]
    [SerializeField] private Transform triggerVolumes;
    [Header("Optional conditions")]
    [SerializeField] private ObjectiveDefinition requiredObjective;
    [SerializeField] private ObjectiveState requiredState = ObjectiveState.Active;
    [SerializeField] private SkillData requiredKnowledge;
    [Header("Optional actions")]
    [SerializeField] private GameplayActions actions = new();
    [Header("Persistence")]
    [SerializeField] private bool rememberOneShot = true;
    private BoxCollider[] volumes;
    private bool fired, occupied;
    private PlayerCharacter occupant;
    public bool HasFired => fired && !actions.CanRetry;
    public string RunStateKey => "gameplay-trigger";
    public Transform VolumeRoot => triggerVolumes;
    public BoxCollider[] Volumes { get { if (volumes == null) RefreshVolumes(); return volumes; } }
    // Authoring/initialization only. Runtime occupancy never rebuilds this array.
    public void RefreshVolumes() => volumes = triggerVolumes != null
        ? triggerVolumes.GetComponentsInChildren<BoxCollider>(true) : new[] { GetComponent<BoxCollider>() };
    private void Awake()
    {
        RefreshVolumes();
        foreach (var volume in volumes) if (volume != null) volume.isTrigger = true;
        var body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
    }
    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
    private void OnDisable() { occupied = false; occupant = null; }
    private void LateUpdate()
    {
        // CharacterController disable/transport can omit a physics exit callback.
        // A union membership check also makes callback ordering across boxes irrelevant.
        if (occupied && (occupant == null || !occupant.isActiveAndEnabled || !Contains(occupant)))
        { occupied = false; occupant = null; }
    }
    public bool ContainsPoint(Vector3 position)
    {
        foreach (var volume in Volumes)
        {
            if (volume == null || !volume.enabled || !volume.gameObject.activeInHierarchy) continue;
            Vector3 p = volume.transform.InverseTransformPoint(position) - volume.center;
            Vector3 half = volume.size * .5f;
            if (Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z) return true;
        }
        return false;
    }
    private bool Contains(PlayerCharacter player) => ContainsPoint(player.transform.position);
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
        if (!Contains(player)) { occupied = false; occupant = null; return false; }
        var run = ActiveRunController.Instance;
        if (occupied || (behavior == GameplayTriggerBehavior.OneShot && HasFired) || run == null || !run.IsReady || run.gameObject != player.gameObject) return false;
        if (player.GetComponent<PlayerHealth>().IsDead || player.GetComponent<GameplaySuspensionController>().IsSuspended) return false;
        if (requiredKnowledge != null && !player.GetComponent<PlayerSkillState>().HasSkill(requiredKnowledge)) return false;
        if (requiredObjective != null && (ObjectiveController.Instance == null || ObjectiveController.Instance.StateOf(requiredObjective) != requiredState)) return false;
        // A rejected arrival is one attempted visit, not a placement query every physics tick.
        occupied = true; occupant = player;
        if (!actions.TryExecute(player)) return false;
        fired = true;
        run.MarkDirty();
        return true;
    }
    [Serializable] private struct State { public bool fired; }
    // An interrupted asynchronous arrival restores as available, alongside its owner.
    public string CaptureRunState() => JsonUtility.ToJson(new State { fired = rememberOneShot && behavior == GameplayTriggerBehavior.OneShot && fired && actions.IsCommitted });
    public void RestoreRunState(string json) { fired = JsonUtility.FromJson<State>(json).fired; occupied = false; occupant = null; }
}
