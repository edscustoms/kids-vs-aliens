using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class PermanentSave
{
    public int version = 1;
    public List<SavedSkill> skills = new();
}
[Serializable] public sealed class SavedSkill { public string id; public int xp; public bool acknowledged; }
[Serializable] public sealed class ActiveRunSave
{
    public int version = 1;
    public bool discarded;
    public string runId, sceneName, savedUtc;
    public string startingCharacter, startingWeapon;
    public double elapsedSeconds;
    public SavedPlayer player = new();
    public List<SavedWorldObject> world = new();
}
[Serializable] public sealed class SavedPlayer
{
    public Vector3 position;
    public Quaternion rotation;
    public float health, armor, verticalVelocity;
    public string character, equipped, selected;
    public int ammo; // Legacy selected-magazine fallback.
    public int plasmaCapsules, armorCapsules; // Missing in legacy saves: zero resources.
    public List<SavedWeaponState> weapons; // Null in pre-owned-magazine saves.
    public List<string> items = new();
    // Null in older saves: each item entry then represents one unit.
    public int[] itemCounts;
    public int[] quickSlots;
}
[Serializable] public sealed class SavedWeaponState
{
    public string weapon;
    public int rounds;
    public float reloadRemaining, cooldownRemaining;
}
[Serializable] public sealed class SavedWorldObject
{
    public string id, prefab, parent;
    public Vector3 position;
    public Quaternion rotation;
    public bool active = true, removed, chestOpen;
    public float health, spawnDelay;
    public int brainState;
    public Vector3 investigation;
    public List<SavedRunPart> parts = new();
}
[Serializable] public sealed class SavedRunPart { public string key, json; }

/// <summary>
/// Absolute saved state on a RunWorldObject. Keys must be stable and unique per object.
/// RestoreRunState currently runs twice: before player restore, then after one frame
/// of normal Start. OnEnable can run before either call. Both calls must be idempotent:
/// never award rewards, consume resources or replay completion events during restore.
/// Resolve peers by stable RunWorldObject identity; a peer may still have default state
/// on the first pass. Defer peer-dependent decisions until ActiveRunController.IsReady.
/// See Docs/RunInterface.md for the complete lifecycle and compatibility policy.
/// </summary>
public interface IRunStateParticipant
{
    string RunStateKey { get; }
    string CaptureRunState();
    void RestoreRunState(string json);
}
public enum RunEntryMode { Fresh, Resume }
