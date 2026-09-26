using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class LevelStartMenuFlowTests
{
    private const string Folder = "Assets/LevelStartMenuFlowFixture";
    private const string ScenePath = Folder + "/AuditLevelStart.unity";
    [Serializable] private class BuildBackup { public string[] paths; public bool[] enabled; }
    private const string Key = "LevelStartMenuFlowTests";
    [Serializable] private class SceneDescription { public string path; public bool loaded, active; }
    [Serializable] private class SceneBackup { public SceneDescription[] scenes; }

    [UnityTest]
    public IEnumerator SavedRootIsUsedByProductionMenuNewGameAndStaysThereAfterHandoff()
    {
        SessionState.SetString(Key + "Scenes", JsonUtility.ToJson(new SceneBackup {
            scenes = EditorSceneManager.GetSceneManagerSetup().Select(s => new SceneDescription { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray()
        }));
        string folder = Path.GetFullPath("Logs/LevelStartMenu/Regression-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Assert.That(AssetDatabase.IsValidFolder(Folder), Is.False, "Stale fixture folder must be inspected before retry");
        AssetDatabase.CreateFolder("Assets", "LevelStartMenuFlowFixture");
        Assert.That(AssetDatabase.CopyAsset("Assets/Game/Scenes/ConstructionSite.unity", ScenePath), Is.True);
        Assert.That(AssetDatabase.CopyAsset("Assets/Game/Scenes/Menu.unity", Folder + "/AuditMenu.unity"), Is.True);
        SessionState.SetString(Key + "Build", JsonUtility.ToJson(new BuildBackup {
            paths = EditorBuildSettings.scenes.Select(s => s.path).ToArray(), enabled = EditorBuildSettings.scenes.Select(s => s.enabled).ToArray() }));
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(Folder + "/AuditMenu.unity", true) }).ToArray();
        SessionState.SetString(Key + "Saves", Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.Combine(folder, "Saves"));

        var scene = EditorSceneManager.OpenScene(ScenePath);
        var root = Object.FindAnyObjectByType<PlayerBeamInSequence>().transform;
        // Actual failed manual move: above flat terrain, clear capsule/path, beyond
        // Hoist's short support probe. No repair or automatic Y adjustment is run.
        root.SetPositionAndRotation(new Vector3(-26.85f, .42240095f, -34.9f), Quaternion.Euler(0, 97, 0));
        var transport = Object.FindAnyObjectByType<BeamTransportController>();
        Assert.That(transport.IsSegmentClear(root.position + Vector3.up * 10, root.position), Is.True);
        Assert.That(transport.IsLandingSafe(root.position), Is.False, "This regression must cover the short-support-probe rejection");
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        EditorSceneManager.MarkSceneDirty(scene); Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
        var menuScene = EditorSceneManager.OpenScene(Folder + "/AuditMenu.unity");
        var menuData = new SerializedObject(Object.FindAnyObjectByType<MenuController>());
        menuData.FindProperty("gameSceneName").stringValue = "AuditLevelStart";
        menuData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(menuScene);

        yield return new EnterPlayMode();
        // Allocate event closures after the domain reload, not in the serialized outer enumerator.
        yield return VerifyProductionFlow();
    }

    private static IEnumerator VerifyProductionFlow()
    {
        Application.runInBackground = true;
        yield return null; yield return null;
        // Existing-run fixture only triggers the real replacement UI. No saved pose
        // is applied: this test must go through New Game, not direct StartFresh/Continue.
        RunSaveService.ActiveStore.Write(new ActiveRunSave {
            runId = "old-run-must-be-replaced", sceneName = "AuditLevelStart",
            player = new SavedPlayer { position = new Vector3(22, 5, 18), rotation = Quaternion.Euler(0, 12, 0) }
        });
        Click("PlayButton"); yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("AuditMenu"));
        Click("NewGame"); yield return null;

        bool loaded = false, ended = false, fresh = false;
        Vector3 authored = new Vector3(-26.85f, .42240095f, -34.9f), finalPosition = default;
        Quaternion rotation = Quaternion.Euler(0, 97, 0), finalRotation = default;
        PlayerBeamInSequence runtimeOwner = null;
        BeamTransportController runtimePlayer = null;
        BeamTransportVFX runtimeBeam = null;
        void Ended() { ended = true; finalPosition = runtimePlayer.transform.position; finalRotation = runtimePlayer.transform.rotation; }
        void Loaded(Scene current, LoadSceneMode mode)
        {
            if (current.path != ScenePath) return;
            loaded = true; fresh = RunSaveService.EntryMode == RunEntryMode.Fresh && RunSaveService.PendingRestore == null;
            runtimeOwner = Object.FindAnyObjectByType<PlayerBeamInSequence>();
            runtimePlayer = Object.FindAnyObjectByType<BeamTransportController>();
            runtimeBeam = (BeamTransportVFX)new SerializedObject(runtimeOwner).FindProperty("transportVfx").objectReferenceValue;
            runtimePlayer.TransportEnded += Ended;
        }
        SceneManager.sceneLoaded += Loaded;
        try
        {
            Click("Replace");
            double timeout = EditorApplication.timeSinceStartup + 25;
            while (!loaded && EditorApplication.timeSinceStartup < timeout) yield return null;
            Assert.That(loaded && fresh, Is.True, "Production New Game must load the saved scene in Fresh mode");
            Assert.That(runtimeOwner.gameObject.scene.path, Is.EqualTo(ScenePath));
            Assert.That(EditorUtility.IsPersistent(runtimeOwner), Is.False);
            Assert.That(Vector3.Distance(runtimeOwner.transform.position, authored), Is.LessThan(.001));
            Assert.That(Quaternion.Angle(runtimeOwner.transform.rotation, rotation), Is.LessThan(.001));
            Assert.That(runtimePlayer.IsTransporting, Is.True, "Regression: rejected arrival left Amy at the old scene-player pose");
            Assert.That(Vector3.Distance(runtimePlayer.PresentationDestination, authored), Is.LessThan(.001));
            Assert.That(Vector3.Distance(runtimeBeam.transform.position, authored), Is.LessThan(.001));
            while (!ended && EditorApplication.timeSinceStartup < timeout)
            {
                Foreground(); yield return null;
            }
            Assert.That(ended, Is.True);
            Assert.That(Vector3.Distance(finalPosition, authored), Is.LessThan(.001));
            Assert.That(Quaternion.Angle(finalRotation, rotation), Is.LessThan(.001));
            double afterHandoff = EditorApplication.timeSinceStartup + 3;
            while (EditorApplication.timeSinceStartup < afterHandoff) { Foreground(); yield return null; }
            var settled = runtimePlayer.transform.position;
            Assert.That(Vector2.Distance(new Vector2(settled.x, settled.z), new Vector2(authored.x, authored.z)), Is.LessThan(.02), "No later initialization may reset X/Z");
            Assert.That(Quaternion.Angle(runtimePlayer.transform.rotation, rotation), Is.LessThan(.001));
            Assert.That(settled.y, Is.InRange(authored.y - .6f, authored.y + .01f), "Normal gravity may settle the authored airborne spawn onto its floor");
            Assert.That(runtimePlayer.IsTransporting, Is.False);
            Assert.That(runtimePlayer.GetComponent<CharacterController>().enabled, Is.True);
        }
        finally
        {
            SceneManager.sceneLoaded -= Loaded;
            if (runtimePlayer != null) runtimePlayer.TransportEnded -= Ended;
        }
    }

    private static void Click(string name) => Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b => b.name == name).onClick.Invoke();
    private static void Foreground()
    {
        if (Time.timeScale != 0) return;
        var run = ActiveRunController.Instance;
        if (run != null) { run.SendMessage("OnApplicationPause", false); run.SendMessage("OnApplicationFocus", true); }
        var menu = Object.FindAnyObjectByType<InGameMenuController>(); if (menu != null && menu.IsOpen) menu.ResumeGame();
    }
    [UnityTearDown]
    public IEnumerator RestoreAuthoredScene()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.DeleteAsset(Folder);
        var build = JsonUtility.FromJson<BuildBackup>(SessionState.GetString(Key + "Build", "{}"));
        if (build.paths != null) EditorBuildSettings.scenes = build.paths.Select((path, i) => new EditorBuildSettingsScene(path, build.enabled[i])).ToArray();
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(Key + "Saves", ""));
        var previous = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(Key + "Scenes", "{}"));
        if (previous.scenes != null && previous.scenes.Length > 0 && previous.scenes.All(s => !string.IsNullOrEmpty(s.path)))
            EditorSceneManager.RestoreSceneManagerSetup(previous.scenes.Select(s => new SceneSetup { path = s.path, isLoaded = s.loaded, isActive = s.active }).ToArray());
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.EraseString(Key + "Build"); SessionState.EraseString(Key + "Saves"); SessionState.EraseString(Key + "Scenes");
    }
}
