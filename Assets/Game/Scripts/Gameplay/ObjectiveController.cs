using System;
using System.Collections.Generic;
using UnityEngine;

// One active tracker, with completed/progress records retained for simple authored conditions.
[DisallowMultipleComponent, RequireComponent(typeof(RunWorldObject))]
public sealed class ObjectiveController : MonoBehaviour, IRunStateParticipant
{
    [SerializeField] private ObjectiveDefinition openingObjective;
    private sealed class Entry { public ObjectiveDefinition definition; public ObjectiveState state; public int progress; }
    private readonly List<Entry> entries = new();
    public static ObjectiveController Instance { get; private set; }
    public ObjectiveDefinition ActiveObjective { get; private set; }
    public string RunStateKey => "objectives";
    public event Action Changed;

    private void OnEnable()
    {
        if (Instance != null && Instance != this) { Debug.LogError("Only one ObjectiveController is supported per gameplay scene.", this); enabled = false; return; }
        Instance = this;
    }
    private void OnDisable() { if (Instance == this) Instance = null; }
    private Entry Find(ObjectiveDefinition definition) => entries.Find(e => e.definition == definition);
    public ObjectiveState StateOf(ObjectiveDefinition definition) => definition != null ? Find(definition)?.state ?? ObjectiveState.Inactive : ObjectiveState.Inactive;
    public int ProgressOf(ObjectiveDefinition definition) => definition != null ? Find(definition)?.progress ?? 0 : 0;
    public void BeginFreshAttempt() => StartObjective(openingObjective);
    public void StartObjective(ObjectiveDefinition definition)
    {
        if (definition == null || StateOf(definition) == ObjectiveState.Completed || ActiveObjective == definition) return;
        if (ActiveObjective != null) Find(ActiveObjective).state = ObjectiveState.Inactive;
        var entry = Find(definition);
        if (entry == null) { entry = new Entry { definition = definition }; entries.Add(entry); }
        entry.state = ObjectiveState.Active; ActiveObjective = definition; Notify();
    }
    public void AddProgress(ObjectiveDefinition definition, int amount = 1)
    {
        var entry = Find(definition);
        if (definition == null || definition.progressMode != ObjectiveProgressMode.Count || entry == null || entry.state != ObjectiveState.Active || amount <= 0) return;
        entry.progress = (int)Math.Min((long)Mathf.Max(1, definition.targetCount), (long)entry.progress + amount);
        if (entry.progress >= definition.targetCount) { CompleteObjective(definition); return; }
        Notify();
    }
    public void CompleteObjective(ObjectiveDefinition definition)
    {
        if (definition == null || StateOf(definition) != ObjectiveState.Active) return;
        var entry = Find(definition); entry.state = ObjectiveState.Completed;
        if (definition.progressMode == ObjectiveProgressMode.Count) entry.progress = Mathf.Max(1, definition.targetCount);
        if (ActiveObjective == definition) ActiveObjective = null;
        Notify();
    }
    private void Notify() { Changed?.Invoke(); ActiveRunController.Instance?.MarkDirty(); }
    [Serializable] private sealed class SavedEntry { public string objective; public ObjectiveState state; public int progress; }
    [Serializable] private sealed class State { public List<SavedEntry> entries = new(); }
    public string CaptureRunState()
    {
        var saved = new State();
        foreach (var entry in entries) saved.entries.Add(new SavedEntry { objective = RunContentCatalog.Instance.Id(entry.definition), state = entry.state, progress = entry.progress });
        return JsonUtility.ToJson(saved);
    }
    public void RestoreRunState(string json)
    {
        var saved = JsonUtility.FromJson<State>(json);
        entries.Clear(); ActiveObjective = null;
        foreach (var record in saved.entries)
        {
            var definition = RunContentCatalog.Instance.Resolve<ObjectiveDefinition>(record.objective);
            if (definition == null || Find(definition) != null) throw new InvalidOperationException("Missing or duplicate saved objective.");
            if (record.state == ObjectiveState.Active)
            {
                if (ActiveObjective != null) throw new InvalidOperationException("Multiple active objectives in snapshot.");
                ActiveObjective = definition;
            }
            entries.Add(new Entry { definition = definition, state = record.state, progress = Mathf.Clamp(record.progress, 0, Mathf.Max(1, definition.targetCount)) });
        }
        Changed?.Invoke(); // Presentation only: never invoke incremental actions during restore.
    }
}
