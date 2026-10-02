using System;
using UnityEngine;

public enum ExcavatorRepairState { NotStarted, Collecting, ReturnToExcavator, RunningFinale, Completed }

/// <summary>ConstructionSite's repair requirements; inventory, objectives and machinery retain their own state.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(RunWorldObject))]
public sealed class ExcavatorRepairMission : MonoBehaviour, IRunStateParticipant
{
    [Header("Existing owners")]
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private ObjectiveController objectives;
    [SerializeField] private DialoguePlayer dialogue;
    [SerializeField] private ExcavatorMotionController excavator;
    [Tooltip("Clear ground outside the swing. Saves during the finale use this endpoint without moving the live player.")]
    [SerializeField] private Transform finaleSavePoint;
    [Header("Authored combat (optional)")]
    [Tooltip("The crash-site weapon must be acquired before starting the repair fights.")]
    [SerializeField] private WeaponItemData requiredWeapon;
    [SerializeField] private AuthoredEncounter route4Encounter;
    [SerializeField] private AuthoredEncounter electricalEncounter;
    [SerializeField] private AuthoredEncounter returnEncounter;
    [Header("Three distinct, reusable items")]
    [SerializeField] private ItemData batteryCables;
    [SerializeField] private ItemData industrialFuse;
    [SerializeField] private ItemData hydraulicFluid;
    [SerializeField] private RunWorldObject[] pickups = new RunWorldObject[3];
    [Header("Communication")]
    [SerializeField] private ObjectiveDefinition findParts;
    [SerializeField] private ObjectiveDefinition returnToExcavator;
    [SerializeField] private DialogueMessage startMessage;
    [SerializeField] private DialogueMessage officeClue;

    private bool cluePlayed, reconcile = true;
    public ExcavatorRepairState State { get; private set; }
    public string RunStateKey => "excavator-repair";
    public int OwnedPartCount => (Owns(batteryCables) ? 1 : 0) + (Owns(industrialFuse) ? 1 : 0) + (Owns(hydraulicFluid) ? 1 : 0);

    private bool Owns(ItemData item)
    {
        if (inventory == null || item == null) return false;
        for (int i = 0; i < inventory.Items.Count; i++)
            if (inventory.Items[i] == item && inventory.CountAt(i) > 0) return true;
        return false;
    }
    private void OnEnable()
    {
        if (inventory != null) inventory.OnInventoryChanged += InventoryChanged;
        if (excavator != null) excavator.OnSwingCompleted.AddListener(FinaleCompleted);
        reconcile = true;
    }
    private void OnDisable()
    {
        if (inventory != null) inventory.OnInventoryChanged -= InventoryChanged;
        if (excavator != null) excavator.OnSwingCompleted.RemoveListener(FinaleCompleted);
        ActiveRunController.Instance?.ClearPlayerSavePoint(this);
    }
    private void Update()
    {
        if (!reconcile || ActiveRunController.Instance == null || !ActiveRunController.Instance.IsReady) return;
        reconcile = false;
        // Both world restore passes and inventory restoration finish before peer decisions.
        if (State == ExcavatorRepairState.Completed)
        {
            excavator.RestoreCompletedPose();
            objectives.CompleteObjective(returnToExcavator);
        }
        else if (State == ExcavatorRepairState.RunningFinale && excavator.State == ExcavatorMotionState.Completed)
            FinaleCompleted();
        foreach (var pickup in pickups)
            if (pickup != null && !pickup.IsRemoved)
                pickup.gameObject.SetActive(State != ExcavatorRepairState.NotStarted);
        RefreshCollection(false);
    }

    // Called by the authored repeatable GameplayTrigger at the excavator.
    public bool Visit(PlayerCharacter player)
    {
        var run = ActiveRunController.Instance;
        if (!isActiveAndEnabled || reconcile || run == null || !run.IsReady || player == null
            || inventory == null || player.gameObject != inventory.gameObject || player.gameObject != run.gameObject
            || player.GetComponent<PlayerHealth>().IsDead || player.GetComponent<GameplaySuspensionController>().IsSuspended) return false;
        if (State == ExcavatorRepairState.NotStarted)
        {
            if (requiredWeapon != null && inventory.GetWeaponState(requiredWeapon) == null) return false;
            State = ExcavatorRepairState.Collecting;
            route4Encounter?.Activate();
            electricalEncounter?.Activate();
            foreach (var pickup in pickups) if (pickup != null && !pickup.IsRemoved) pickup.gameObject.SetActive(true);
            objectives.StartObjective(findParts);
            dialogue.Play(startMessage, player.ActiveVisual?.DialogueIdentity);
            RefreshCollection(true);
            run.MarkDirty();
            return true;
        }
        if (State != ExcavatorRepairState.ReturnToExcavator || OwnedPartCount != 3
            || excavator.State != ExcavatorMotionState.Idle || !run.TryReservePlayerSavePoint(this, finaleSavePoint)) return false;
        excavator.PlaySwing();
        if (excavator.State != ExcavatorMotionState.Moving) { run.ClearPlayerSavePoint(this); return false; }
        // Enter the committed state before inventory notifications. These distinct requirements
        // are preflighted together; consume one of each, never whole stacks or unrelated items.
        State = ExcavatorRepairState.RunningFinale;
        inventory.TryConsumeItem(batteryCables);
        inventory.TryConsumeItem(industrialFuse);
        inventory.TryConsumeItem(hydraulicFluid);
        run.Save();
        return true;
    }
    private void InventoryChanged()
    {
        if (!reconcile && ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady) RefreshCollection(true);
    }
    private void RefreshCollection(bool allowDialogue)
    {
        if (State != ExcavatorRepairState.Collecting) return;
        objectives.SetProgress(findParts, OwnedPartCount);
        if (!cluePlayed && Owns(batteryCables) && Owns(industrialFuse))
        {
            cluePlayed = true;
            if (allowDialogue && !Owns(hydraulicFluid)) dialogue.Play(officeClue, inventory.GetComponent<PlayerCharacter>().ActiveVisual?.DialogueIdentity);
        }
        if (OwnedPartCount == 3)
        {
            State = ExcavatorRepairState.ReturnToExcavator;
            objectives.StartObjective(returnToExcavator);
            returnEncounter?.Activate();
        }
        ActiveRunController.Instance?.MarkDirty();
    }
    private void FinaleCompleted()
    {
        if (State != ExcavatorRepairState.RunningFinale) return;
        State = ExcavatorRepairState.Completed;
        objectives.CompleteObjective(returnToExcavator, showCompletion: true);
        ActiveRunController.Instance?.ClearPlayerSavePoint(this);
        ActiveRunController.Instance?.MarkDirty();
    }

    [Serializable] private struct SavedState { public ExcavatorRepairState state; public bool cluePlayed; }
    public string CaptureRunState() => JsonUtility.ToJson(new SavedState { state = State == ExcavatorRepairState.RunningFinale ? ExcavatorRepairState.Completed : State, cluePlayed = cluePlayed });
    public void RestoreRunState(string json)
    {
        var saved = JsonUtility.FromJson<SavedState>(json);
        State = saved.state == ExcavatorRepairState.RunningFinale ? ExcavatorRepairState.Completed : saved.state;
        cluePlayed = saved.cluePlayed;
        reconcile = true; // Absolute assignment only; peers are not restored yet.
    }
}
