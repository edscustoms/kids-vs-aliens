using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RunSaveService
{
    public static string DirectoryPath
    {
        get
        {
#if UNITY_EDITOR
            string isolated = Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY");
            if (!string.IsNullOrEmpty(isolated)) return isolated;
#endif
            return Path.Combine(Application.persistentDataPath, "Saves");
        }
    }
    public static RecoverableJsonStore ActiveStore => new(DirectoryPath, "active-run");
    public static RecoverableJsonStore PermanentStore => new(DirectoryPath, "permanent");
    public static RunEntryMode EntryMode { get; private set; }
    public static ActiveRunSave PendingRestore { get; private set; }
    public static string LastError { get; private set; }
    public static event Action<string> Feedback;
    public static bool TryReadActive(out ActiveRunSave save)
    {
        save = null; LastError = null;
        try
        {
            save = ActiveStore.Read<ActiveRunSave>();
            if (save != null && save.discarded) save = null;
            if (save != null && (save.version != 1 || string.IsNullOrEmpty(save.sceneName) || save.player == null))
                throw new InvalidDataException("This run uses unsupported or invalid save data.");
            return true;
        }
        catch (Exception error) { LastError = error.Message; return false; }
    }
    public static bool Continue()
    {
        if (!TryReadActive(out var saved) || saved == null) return false;
        try
        {
            var catalog = RunContentCatalog.Instance;
            // Validate before unloading the menu or changing the durable snapshot.
            catalog.Resolve<CharacterVisual>(saved.player.character);
            foreach (var item in saved.player.items) catalog.Resolve<ItemData>(item);
            catalog.Resolve<WeaponItemData>(saved.player.equipped);
            catalog.Resolve<ItemData>(saved.player.selected);
            foreach (var entity in saved.world) if (!entity.removed) catalog.Resolve<GameObject>(entity.prefab);
            if (!Application.CanStreamedLevelBeLoaded(saved.sceneName)) throw new InvalidOperationException("Saved level is not included in this build.");
            PendingRestore = saved; EntryMode = RunEntryMode.Resume;
            PlayerLoadoutState.Initialize(catalog.Resolve<CharacterVisual>(saved.player.character), catalog.Resolve<WeaponItemData>(saved.player.equipped));
            SceneManager.LoadScene(saved.sceneName); return true;
        }
        catch (Exception error) { LastError = error.Message; PendingRestore = null; EntryMode = RunEntryMode.Fresh; return false; }
    }
    public static bool StartFresh(string scene, bool replaceExisting)
    {
        try
        {
            if (!Application.CanStreamedLevelBeLoaded(scene)) throw new InvalidOperationException("The requested level is not included in this build.");
            if (!replaceExisting && (!TryReadActive(out var existing) || existing != null)) throw new InvalidOperationException("Confirm replacement of the existing run first.");
            DiscardActive();
            ActiveRunController.Instance?.PrepareToLeave(); PendingRestore = null; EntryMode = RunEntryMode.Fresh;
            SceneManager.LoadScene(scene); return true;
        }
        catch (Exception error) { LastError = error.Message; return false; }
    }
    public static void DiscardActive()
    {
        // Both recoverable generations must agree that a discarded run is gone.
        ActiveStore.Write(new ActiveRunSave { discarded = true });
        ActiveStore.Write(new ActiveRunSave { discarded = true });
    }
    public static void RestoreFinished() { PendingRestore = null; EntryMode = RunEntryMode.Fresh; }
    public static bool IsRestoringScene(string scene) => EntryMode == RunEntryMode.Resume && PendingRestore?.sceneName == scene;
    public static void Notify(string message) => Feedback?.Invoke(message);
    public static void ReportError(Exception error) { LastError = error.Message; Debug.LogException(error); Notify("Run could not be saved. " + error.Message); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { PendingRestore = null; EntryMode = RunEntryMode.Fresh; LastError = null; Feedback = null; }
}
