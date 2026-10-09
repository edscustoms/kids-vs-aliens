using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StarterAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-owned persistence. Suspension is always delegated to the existing lease owner.
[DefaultExecutionOrder(-180), DisallowMultipleComponent]
public sealed class ActiveRunController : MonoBehaviour
{
    [SerializeField, Min(1)] private float snapshotInterval = 10;
    [SerializeField] private string mainMenuScene = "Menu";
    public static ActiveRunController Instance { get; private set; }
    private readonly Dictionary<string, RunWorldObject> objects = new();
    private readonly Dictionary<string, SavedWorldObject> snapshots = new();
    private List<SavedRunScene> previousScenes = new();
    private GameplaySuspensionController suspension;
    private GameplaySuspensionController.Lease restoreLease, lifecycleLease;
    private PlayerInventory inventory;
    private PlayerHealth health;
    private string runId, startingCharacter, startingWeapon;
    private double elapsed;
    private float nextSave;
    private bool ready, leaving, dead, restoring, unfocused, backgrounded;
    private MonoBehaviour playerSavePointOwner;
    private Transform playerSavePoint;
    public string RestoreError { get; private set; }
    public void ReturnToMenuPreservingSnapshot() { PrepareToLeave(); SceneManager.LoadScene(mainMenuScene); }
    public bool IsReady => ready && !leaving && !dead;
    public double ElapsedSeconds => elapsed;
    private void Awake()
    {
        Instance = this;
        suspension = GetComponent<GameplaySuspensionController>();
        inventory = GetComponent<PlayerInventory>(); health = GetComponent<PlayerHealth>();
        restoring = RunSaveService.IsRestoringScene(gameObject.scene.name);
        if (restoring) restoreLease = suspension.Acquire(SuspensionReason.Modal);
        runId = Guid.NewGuid().ToString("N");
    }
    private IEnumerator Start()
    {
        // All authored objects and normal equipment have completed Start before restore.
        yield return null;
        foreach (var entity in FindObjectsByType<RunWorldObject>(FindObjectsInactive.Include))
            if (entity.gameObject.scene == gameObject.scene) Register(entity);
        if (!restoring)
        {
            startingCharacter = RunContentCatalog.Instance.Id(GetComponent<PlayerCharacter>().CurrentCharacterPrefab);
            startingWeapon = RunContentCatalog.Instance.Id(GetComponent<PlayerEquipment>().EquippedWeapon);
            inventory.InitializeFreshAttemptCombat();
            ObjectiveController.Instance?.BeginFreshAttempt();
        }
        if (restoring)
        {
            Exception failure = null;
            try { Restore(RunSaveService.PendingRestore); } catch (Exception error) { failure = error; }
            if (failure != null) { RestoreError = failure.Message; RunSaveService.ReportError(failure); yield break; }
            // Spawned enemies also need their regular Start before applying their state.
            yield return null;
            try {
                foreach (var snapshot in RunSaveService.PendingRestore.world)
                    if (objects.TryGetValue(snapshot.id, out var entity) && entity != null) entity.Restore(snapshot);
            } catch (Exception error) { RestoreError = error.Message; RunSaveService.ReportError(error); yield break; }
            RunSaveService.RestoreFinished(); restoring = false;
            restoreLease?.Dispose(); restoreLease = null;
            FindAnyObjectByType<InGameMenuController>()?.OpenMenu();
            RunSaveService.Notify("Active run restored");
        }
        inventory.OnInventoryChanged += MarkDirty;
        if (health != null) { health.OnHealthChanged += MarkDirty; health.OnDied += OnDeath; }
        ready = true; nextSave = Time.unscaledTime + 1;
    }
    public void Register(RunWorldObject entity)
    {
        // An additively preloaded scene is not part of this attempt's live world yet.
        if (entity == null || entity.gameObject.scene != gameObject.scene) return;
        if (string.IsNullOrEmpty(entity.Id)) throw new InvalidOperationException("Missing run identity on " + entity.name + ". Run scene setup/repair.");
        if (objects.TryGetValue(entity.Id, out var existing) && existing != null && existing != entity)
            throw new InvalidOperationException("Duplicate run identity: " + entity.Id);
        objects[entity.Id] = entity;
    }
    public void Record(SavedWorldObject snapshot) => snapshots[snapshot.id] = snapshot;
    public RunWorldObject FindWorldObject(string id) => objects.TryGetValue(id, out var entity) ? entity : null;
    public void MarkDirty() { if (ready) nextSave = Mathf.Min(nextSave, Time.unscaledTime + .35f); }

    public void HandoffToScene(Scene destination, GameObject[] carriedRoots, GameObject[] sceneOwnedChildren)
    {
        if (!IsReady || !destination.isLoaded || playerSavePointOwner != null)
            throw new InvalidOperationException("The run is not ready for a scene handoff.");
        var source = gameObject.scene;
        var roots = new HashSet<GameObject>(carriedRoots);
        if (!roots.Contains(transform.root.gameObject)) throw new InvalidOperationException("The player rig must be carried.");
        foreach (var root in roots)
            if (root == null || root.transform.parent != null || root.scene != source)
                throw new InvalidOperationException("Crossover roots must belong to the outgoing scene.");
        foreach (var child in sceneOwnedChildren)
            if (child == null || child.scene != source || child.transform.parent == null
                || !roots.Contains(child.transform.root.gameObject))
                throw new InvalidOperationException("Level-owned exclusions must be children of a carried root.");
        var incoming = new Dictionary<string, RunWorldObject>();
        foreach (var root in destination.GetRootGameObjects().Concat(roots))
            foreach (var entity in root.GetComponentsInChildren<RunWorldObject>(true))
            {
                if (sceneOwnedChildren.Any(child => entity.transform.IsChildOf(child.transform))) continue;
                if (string.IsNullOrEmpty(entity.Id) || !incoming.TryAdd(entity.Id, entity))
                    throw new InvalidOperationException("Invalid or duplicate destination run identity: " + entity.Id);
            }
        foreach (var entity in objects.Values) if (entity != null) Record(entity.Capture());
        var outgoing = snapshots.Values.Where(s => !incoming.ContainsKey(s.id)).ToList();
        previousScenes.RemoveAll(s => s.sceneName == source.name);
        previousScenes.Add(new SavedRunScene { sceneName = source.name, world = outgoing });
        // Preserve world poses and IDs. Only the input owner travels, not its level spawners.
        foreach (var child in sceneOwnedChildren) child.transform.SetParent(null, true);
        foreach (var root in roots) SceneManager.MoveGameObjectToScene(root, destination);
        SceneManager.SetActiveScene(destination);
        objects.Clear(); snapshots.Clear();
        foreach (var entity in incoming.Values) Register(entity);
        // Normal Save/backgrounding observes the new scene atomically. The last durable
        // source snapshot remains valid until the existing autosave writes the destination.
        MarkDirty();
    }

    // Short interactions supply a safe endpoint without moving the live player on Save.
    // The caller validates it and clears only its own reservation on completion/abort.
    public bool TryReservePlayerSavePoint(MonoBehaviour owner, Transform point)
    {
        if (!IsReady || owner == null || point == null || playerSavePointOwner != null) return false;
        playerSavePointOwner = owner;
        playerSavePoint = point;
        return true;
    }

    public void ClearPlayerSavePoint(MonoBehaviour owner)
    {
        if (playerSavePointOwner != owner) return;
        playerSavePointOwner = null;
        playerSavePoint = null;
    }
    private void Update()
    {
        if (!IsReady) return;
        elapsed += Time.deltaTime;
        if (Time.unscaledTime >= nextSave) Save();
    }
    public bool Save(bool feedback = false)
    {
        if (!IsReady) return false;
        nextSave = Time.unscaledTime + snapshotInterval;
        try
        {
            var catalog = RunContentCatalog.Instance;
            foreach (var entity in objects.Values) if (entity != null) Record(entity.Capture());
            var equipment = GetComponent<PlayerEquipment>();
            var movement = GetComponent<ThirdPersonController>();
            var position = transform.position;
            var beam = GetComponent<BeamTransportController>();
            // A scripted transit is transient; preserve a safe endpoint, never mid-beam suspension.
            if (beam != null && beam.IsTransporting)
                position = beam.PresentationIsCurved ? beam.PresentationStart : beam.PresentationDestination;
            bool safeInteraction = playerSavePointOwner != null && playerSavePoint != null;
            if (safeInteraction) position = playerSavePoint.position;
            var save = new ActiveRunSave {
                startingCharacter = startingCharacter, startingWeapon = startingWeapon,
                runId = runId, sceneName = gameObject.scene.name, elapsedSeconds = elapsed, savedUtc = DateTime.UtcNow.ToString("O"),
                player = new SavedPlayer {
                    position = position, rotation = safeInteraction ? playerSavePoint.rotation : transform.rotation,
                    health = health != null ? health.CurrentHealth : 0, armor = health != null ? health.CurrentArmor : 0,
                    verticalVelocity = safeInteraction || (beam != null && beam.IsTransporting) ? 0 : movement.RunVerticalVelocity,
                    character = catalog.Id(GetComponent<PlayerCharacter>().CurrentCharacterPrefab),
                    equipped = catalog.Id(equipment.EquippedWeapon), selected = catalog.Id(inventory.SelectedItem),
                    ammo = GetComponent<PlayerShooter>().CurrentAmmo, weapons = inventory.CaptureWeaponStates(),
                    plasmaCapsules = inventory.PlasmaCapsules, armorCapsules = inventory.ArmorCapsules,
                    items = inventory.Items.Select(item => catalog.Id(item)).ToList(), itemCounts = inventory.CaptureCounts(), quickSlots = inventory.CaptureQuickSlots()
                }, world = snapshots.Values.ToList(), previousScenes = previousScenes
            };
            RunSaveService.ActiveStore.Write(save);
            if (!PermanentProgress.Flush()) return false;
            if (feedback) RunSaveService.Notify("Run saved");
            return true;
        }
        catch (Exception error) { RunSaveService.ReportError(error); return false; }
    }
    private void Restore(ActiveRunSave saved)
    {
        var catalog = RunContentCatalog.Instance;
        runId = saved.runId; elapsed = saved.elapsedSeconds;
        previousScenes = saved.previousScenes ?? new List<SavedRunScene>();
        startingCharacter = saved.startingCharacter; startingWeapon = saved.startingWeapon;
        foreach (var snapshot in saved.world)
        {
            Record(snapshot);
            if (!objects.ContainsKey(snapshot.id))
            {
                if (string.IsNullOrEmpty(snapshot.prefab)) throw new InvalidOperationException("Saved scene object is missing: " + snapshot.id);
                if (snapshot.removed) continue;
                Transform parent = null;
                if (!string.IsNullOrEmpty(snapshot.parent)) {
                    if (!objects.TryGetValue(snapshot.parent, out var owner)) throw new InvalidOperationException("Missing spawn owner.");
                    parent = owner.GetComponent<EnemySpawner>()?.SpawnContainer;
                }
                var spawned = Instantiate(catalog.Resolve<GameObject>(snapshot.prefab), snapshot.position, snapshot.rotation, parent);
                var entity = spawned.GetComponent<RunWorldObject>() ?? spawned.AddComponent<RunWorldObject>();
                entity.ConfigureIdentity(snapshot.id); Register(entity);
            }
        }
        // Allocate all identities before participants resolve cross-object ownership.
        foreach (var snapshot in saved.world)
            if (objects.TryGetValue(snapshot.id, out var entity) && entity != null) entity.Restore(snapshot);
        var player = saved.player;
        inventory.RestoreSavedItems(player.items.Select(id => catalog.Resolve<ItemData>(id)).ToArray(), player.quickSlots, player.itemCounts);
        inventory.RestoreCapsules(player.plasmaCapsules, player.armorCapsules);
        var controller = GetComponent<CharacterController>(); bool enabledController = controller.enabled;
        controller.enabled = false; transform.SetPositionAndRotation(player.position, player.rotation); controller.enabled = enabledController;
        GetComponent<ThirdPersonController>().RestoreRunVerticalVelocity(player.verticalVelocity);
        health?.RestoreRunHealth(player.health, player.armor);
        var equipment = GetComponent<PlayerEquipment>();
        var weapon = catalog.Resolve<WeaponItemData>(player.equipped);
        // Compatibility for pre-ownership snapshots containing an equipped ghost weapon.
        if (!inventory.EnsureOwnedWeapon(weapon)) throw new InvalidOperationException("Saved equipped weapon does not fit the backpack.");
        inventory.RestoreWeaponStates(player.weapons, weapon, player.ammo);
        if (weapon != null) equipment.EquipWeapon(weapon); else equipment.UnequipWeapon();
        var selected = catalog.Resolve<ItemData>(player.selected);
        if (selected is GrenadeItemData grenade) GetComponent<PlayerGrenadeController>().RestoreRunSelection(grenade);
        else if (selected is UnarmedCombatItemData melee) GetComponent<PlayerMeleeController>().RestoreRunSelection(melee);

    }
    public bool RestartFromBeginning()
    {
        var catalog = RunContentCatalog.Instance;
        PlayerLoadoutState.Initialize(catalog.Resolve<CharacterVisual>(startingCharacter), catalog.Resolve<WeaponItemData>(startingWeapon));
        return RunSaveService.StartFresh(gameObject.scene.name, true);
    }
    public void PrepareToLeave() { leaving = true; SceneCrossover.CancelPending(this); PermanentProgress.Flush(); }
    public bool QuitToMenu()
    {
        if (!Save(true)) return false;
        PrepareToLeave(); SceneManager.LoadScene(mainMenuScene); return true;
    }
    private void OnDeath()
    {
        dead = true;
        try { RunSaveService.DiscardActive(); PermanentProgress.Flush(); }
        catch (Exception error) { RunSaveService.ReportError(error); }
    }
    private void OnApplicationPause(bool paused) { backgrounded = paused; HandleLifecycle(); }
    private void OnApplicationFocus(bool focused) { unfocused = !focused; HandleLifecycle(); }
    private void HandleLifecycle()
    {
        if (leaving || suspension == null) return;
        if (backgrounded || unfocused)
        {
            if (lifecycleLease != null) return;
            Save(); PermanentProgress.Flush(); PlayerPrefs.Save();
            lifecycleLease = suspension.Acquire(SuspensionReason.ApplicationLifecycle);
        }
        else if (lifecycleLease != null)
        {
            FindAnyObjectByType<InGameMenuController>()?.OpenMenu();
            lifecycleLease.Dispose(); lifecycleLease = null;
        }
    }
    private void OnApplicationQuit() { Save(); PermanentProgress.Flush(); PlayerPrefs.Save(); }
    private void OnDestroy()
    {
        if (inventory != null) inventory.OnInventoryChanged -= MarkDirty;
        if (health != null) { health.OnHealthChanged -= MarkDirty; health.OnDied -= OnDeath; }
        restoreLease?.Dispose(); lifecycleLease?.Dispose();
        if (Instance == this) Instance = null;
    }
}
