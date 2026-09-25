using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using KidsVsAliens.Environment;

[DisallowMultipleComponent]
public sealed class RunWorldObject : MonoBehaviour
{
    [SerializeField] private string stableId;
    private string prefabId, parentId;
    private bool removed;
    public string Id => stableId;
    public void ConfigureIdentity(string id) => stableId = id;
    private void Start()
    {
        ActiveRunController.Instance?.Register(this);
        var health = GetComponent<EnemyHealth>();
        if (health != null) health.OnDied += MarkRemoved;
    }
    private void OnDestroy()
    {
        var health = GetComponent<EnemyHealth>();
        if (health != null) health.OnDied -= MarkRemoved;
    }
    public static RunWorldObject TrackSpawn(GameObject instance, GameObject prefab, string parent = "")
    {
        if (ActiveRunController.Instance == null) return null;
        var entity = instance.GetComponent<RunWorldObject>() ?? instance.AddComponent<RunWorldObject>();
        entity.stableId = Guid.NewGuid().ToString("N");
        entity.prefabId = RunContentCatalog.Instance.Id(prefab);
        entity.parentId = parent;
        ActiveRunController.Instance?.Register(entity);
        ActiveRunController.Instance?.MarkDirty();
        return entity;
    }
    public void MarkRemoved()
    {
        removed = true;
        ActiveRunController.Instance?.Record(Capture());
        ActiveRunController.Instance?.MarkDirty();
    }
    public SavedWorldObject Capture()
    {
        var health = GetComponent<EnemyHealth>();
        var brain = GetComponent<EnemyBrain>();
        var snapshot = new SavedWorldObject {
            id = stableId, prefab = prefabId, parent = parentId,
            position = transform.position, rotation = transform.rotation,
            active = gameObject.activeSelf, removed = removed || (health != null && health.IsDead),
            health = health != null ? health.CurrentHealth : 0,
            chestOpen = GetComponent<LootChest>()?.IsOpen ?? false,
            brainState = brain != null ? (int)brain.State : 0,
            investigation = brain != null ? brain.RunInvestigationAnchor : Vector3.zero,
            spawnDelay = GetComponent<EnemySpawner>()?.RemainingSpawnDelay ?? 0
        };
        var keys = new HashSet<string>();
        foreach (var behaviour in GetComponents<MonoBehaviour>())
            if (behaviour is IRunStateParticipant participant)
            {
                if (!keys.Add(participant.RunStateKey)) throw new InvalidOperationException("Duplicate run state key on " + name);
                snapshot.parts.Add(new SavedRunPart { key = participant.RunStateKey, json = participant.CaptureRunState() });
            }
        return snapshot;
    }
    public void Restore(SavedWorldObject snapshot)
    {
        stableId = snapshot.id; prefabId = snapshot.prefab; parentId = snapshot.parent; removed = snapshot.removed;
        if (removed) { gameObject.SetActive(false); return; }
        gameObject.SetActive(snapshot.active);
        var agent = GetComponent<NavMeshAgent>();
        bool navigating = agent != null && agent.enabled;
        if (navigating) agent.enabled = false;
        transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);
        GetComponent<WorldItemFloat>()?.ResetAnchor();
        if (navigating) agent.enabled = true;
        GetComponent<EnemyHealth>()?.RestoreRunHealth(snapshot.health);
        GetComponent<LootChest>()?.RestoreRunOpen(snapshot.chestOpen);
        GetComponent<EnemySpawner>()?.RestoreSpawnDelay(snapshot.spawnDelay);
        GetComponent<EnemyBrain>()?.RestoreRunAwareness((EnemyBrainState)snapshot.brainState, snapshot.investigation);
        foreach (var part in snapshot.parts)
        {
            bool found = false;
            foreach (var behaviour in GetComponents<MonoBehaviour>())
                if (behaviour is IRunStateParticipant participant && participant.RunStateKey == part.key)
                { participant.RestoreRunState(part.json); found = true; break; }
            if (!found) throw new InvalidOperationException("Missing saved world component: " + part.key);
        }
    }
}
