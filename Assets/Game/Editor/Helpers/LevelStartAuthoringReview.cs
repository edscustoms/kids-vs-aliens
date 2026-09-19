using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit, isolated authoring checks; never writes test placements to production scenes.
[InitializeOnLoad]
public static class LevelStartAuthoringReview
{
    private const string Key = "LevelStartAuthoringReview";
    private const string ScenePath = "Assets/Game/Scenes/ConstructionSite.unity";
    private static BeamTransportController transport;
    private static BeamTransportVFX effect;
    private static Pose expected;
    private static bool arrived;
    private static bool reloaded;
    private static int step;
    private static double deadline;
    private static ActiveRunSave snapshot;
    [Serializable] private class SavedPose { public Vector3 position; public Quaternion rotation; }
    static LevelStartAuthoringReview() { EditorApplication.playModeStateChanged += Mode; }
    private static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>();
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("LEVELSTART REVIEW: " + message); }

    public static void Run()
    {
        Directory.CreateDirectory("Logs/LevelStart");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/LevelStart/Save-" + Guid.NewGuid().ToString("N")));
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Failed", false); SessionState.SetInt(Key + "Pass", 0);
        // Old scenes without the beam foundation receive it through the same canonical tool.
        EditorSceneManager.OpenScene("Assets/Game/Scenes/GamePoc.unity");
        GameplaySceneSetup.SetupOrRepairActiveScene();
        var repaired = Find<PlayerBeamInSequence>().transform;
        repaired.SetPositionAndRotation(new Vector3(13, 2, -7), Quaternion.Euler(0, 73, 0));
        GameplaySceneSetup.SetupOrRepairActiveScene(); GameplaySceneSetup.SetupOrRepairActiveScene();
        Check(repaired.position == new Vector3(13, 2, -7) && Quaternion.Angle(repaired.rotation, Quaternion.Euler(0, 73, 0)) < .001f, "GamePoc repair moved root");
        Check(Object.FindObjectsByType<PlayerBeamInSequence>(FindObjectsInactive.Include).Length == 1, "GamePoc repair duplicated root");
        Debug.Log("LEVELSTART PASS: GamePoc canonical repair creates missing root then preserves moved/rotated root on repeat.");
        Prepare();
    }

    private static void Prepare()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var owner = Find<PlayerBeamInSequence>();
        var root = owner.ArrivalTransform;
        var controller = Find<BeamTransportController>();
        int pass = SessionState.GetInt(Key + "Pass", 0);
        float height = new SerializedObject(owner).FindProperty("startHeight").floatValue;
        if (pass > 0)
        {
            Vector3 origin = root.position;
            bool found = false;
            float distance = pass == 1 ? 10 : 30;
            for (int i = 0; i < 24; i++)
            {
                Vector3 candidate = origin + Quaternion.Euler(0, i * 15, 0) * Vector3.forward * distance;
                // Test authoring on real support at the new location, including height changes.
                if (!Physics.Raycast(candidate + Vector3.up * 50, Vector3.down, out var support, 100, ~0, QueryTriggerInteraction.Ignore)) continue;
                candidate.y = support.point.y - controller.FeetOffset + .02f;
                if (!controller.IsLandingSafe(candidate) || !controller.IsSegmentClear(candidate + Vector3.up * height, candidate)) continue;
                root.SetPositionAndRotation(candidate, Quaternion.Euler(0, pass == 1 ? 97 : 271, 0)); found = true; break;
            }
            Check(found, "No supported, unobstructed moved-root fixture");
        }
        var pose = new SavedPose { position = root.position, rotation = root.rotation };
        SessionState.SetString(Key + "Pose", JsonUtility.ToJson(pose));
        if (pass == 1) SessionState.SetString(Key + "SavePose", JsonUtility.ToJson(pose));
        if (pass == 0) SessionState.SetString(Key + "OriginalPose", JsonUtility.ToJson(pose));
        int children = root.GetComponentsInChildren<Transform>(true).Length;
        // Exercise the actual canonical menu command, not just its Beam sub-helper.
        GameplaySceneSetup.SetupOrRepairActiveScene();
        GameplaySceneSetup.SetupOrRepairActiveScene();
        Check(root.position == pose.position && Quaternion.Angle(root.rotation, pose.rotation) < .001f, "Canonical repair overwrote authored root");
        Check(children == root.GetComponentsInChildren<Transform>(true).Length, "Canonical repair duplicated arrival children");
        Check(Object.FindObjectsByType<PlayerBeamInSequence>(FindObjectsInactive.Include).Length == 1, "Duplicate LevelStart");
        Debug.Log($"LEVELSTART PASS: repair twice preserves position {pose.position:F5} / yaw {pose.rotation.eulerAngles.y}");
        EditorApplication.EnterPlaymode();
    }

    private static void Mode(PlayModeStateChange mode)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (mode == PlayModeStateChange.EnteredPlayMode)
        {
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            step = 0; deadline = EditorApplication.timeSinceStartup + 150;
            Application.logMessageReceived += Log; EditorApplication.update += Tick;
            SceneManager.sceneLoaded += Loaded;
            try { ObserveArrival(ReadPose("Pose")); } catch (Exception error) { Fail(error); }
        }
        if (mode == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += () =>
            {
                int pass = SessionState.GetInt(Key + "Pass", 0);
                bool failed = SessionState.GetBool(Key + "Failed", false);
                if (!failed && pass < 2)
                {
                    try { SessionState.SetInt(Key + "Pass", pass + 1); Prepare(); }
                    catch (Exception error) { SessionState.SetBool(Key, false); Debug.LogException(error); EditorApplication.Exit(1); }
                }
                else { SessionState.SetBool(Key, false); EditorSceneManager.OpenScene(ScenePath); EditorApplication.Exit(failed ? 1 : 0); }
            };
    }
    private static Pose ReadPose(string suffix)
    {
        var value = JsonUtility.FromJson<SavedPose>(SessionState.GetString(Key + suffix, ""));
        return new Pose(value.position, value.rotation);
    }
    private static void Loaded(Scene scene, LoadSceneMode mode) { if (scene.name == "ConstructionSite") reloaded = true; }
    private static void ObserveArrival(Pose pose)
    {
        expected = pose; arrived = false;
        transport = Find<BeamTransportController>();
        var owner = Find<PlayerBeamInSequence>();
        effect = (BeamTransportVFX)new SerializedObject(owner).FindProperty("transportVfx").objectReferenceValue;
        Check(transport.IsTransporting, "Fresh start failed to acquire arrival");
        Check(Vector3.Distance(transport.PresentationDestination, expected.position) < .001f, "Old/cached arrival destination");
        Check(Vector3.Distance(effect.transform.position, expected.position) < .001f, "Beam and player destinations differ");
        Check(!transport.GetComponent<CharacterController>().enabled, "Arrival did not acquire player capsule");
        transport.TransportEnded += ArrivalEnded;
    }
    private static void ArrivalEnded()
    {
        try
        {
            Check(Vector3.Distance(transport.transform.position, expected.position) < .001f, "Final player root differs from LevelStart");
            Check(Quaternion.Angle(transport.transform.rotation, expected.rotation) < .001f, "Final player yaw differs from LevelStart");
            Check(transport.GetComponent<CharacterController>().enabled, "Arrival did not restore capsule");
            Check(!effect.GetComponentInChildren<BeamEnergyField>(true).gameObject.activeSelf, "Arrival beam did not hide");
            arrived = true;
            transport.TransportEnded -= ArrivalEnded;
            Debug.Log($"LEVELSTART PASS: exact arrival at {expected.position:F5}, yaw={expected.rotation.eulerAngles.y}, step={step}");
        }
        catch (Exception error) { Fail(error); }
    }
    private static void Foreground()
    {
        if (Time.timeScale != 0) return;
        var run = ActiveRunController.Instance;
        if (run != null) { run.SendMessage("OnApplicationPause", false); run.SendMessage("OnApplicationFocus", true); }
        var menu = Find<InGameMenuController>(); if (menu != null && menu.IsOpen) menu.ResumeGame();
    }
    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Timed out at step " + step);
            if (step == 0)
            {
                Foreground();
                if (!arrived) return;
                if (SessionState.GetInt(Key + "Pass", 0) < 2) { Finish(); return; }
                if (!ActiveRunController.Instance.IsReady) return;
                Find<InGameMenuController>().OpenMenu();
                var pose = ReadPose("SavePose");
                var capsule = transport.GetComponent<CharacterController>();
                capsule.enabled = false; transport.transform.SetPositionAndRotation(pose.position, Quaternion.Euler(0, 183, 0)); capsule.enabled = true;
                var enemy = Find<EnemyHealth>(); enemy.RestoreRunHealth(enemy.CurrentHealth * .5f);
                Check(ActiveRunController.Instance.Save(), "Save failed");
                Check(RunSaveService.TryReadActive(out snapshot) && snapshot != null, "Snapshot missing");
                Check(Vector3.Distance(snapshot.player.position, expected.position) > 8, "Continue fixture too close to LevelStart");
                Check(ActiveRunController.Instance.QuitToMenu(), "Quit failed"); step = 1; return;
            }
            if (step == 1)
            {
                if (SceneManager.GetActiveScene().name != "Menu") return;
                Check(RunSaveService.Continue(), "Continue failed"); step = 2; return;
            }
            if (step == 2)
            {
                if (ActiveRunController.Instance == null || !ActiveRunController.Instance.IsReady) return;
                transport = Find<BeamTransportController>();
                Check(!transport.IsTransporting, "Continue replayed arrival");
                Check(Vector3.Distance(transport.transform.position, snapshot.player.position) < .001f, "Continue lost saved position");
                Check(Quaternion.Angle(transport.transform.rotation, snapshot.player.rotation) < .001f, "Continue lost saved facing");
                Check(Object.FindObjectsByType<BeamEnergyField>(FindObjectsInactive.Include).All(f => !f.gameObject.activeInHierarchy), "Continue displayed arrival VFX");
                foreach (var entity in Object.FindObjectsByType<RunWorldObject>(FindObjectsInactive.Include))
                {
                    var health = entity.GetComponent<EnemyHealth>();
                    var stored = snapshot.world.FirstOrDefault(w => w.id == entity.Id);
                    if (health != null && stored != null) Check(Mathf.Abs(health.CurrentHealth - stored.health) < .001f, "Continue reset enemy health");
                }
                Debug.Log("LEVELSTART PASS: Quit/Menu/Continue restores saved position, yaw and enemy health; no arrival beam.");
                reloaded = false;
                Check(ActiveRunController.Instance.RestartFromBeginning(), "Hard Restart failed");
                Check(RunSaveService.TryReadActive(out var discarded) && discarded == null, "Hard Restart did not discard old snapshot");
                step = 3; return;
            }
            if (step == 3) { if (!reloaded) return; ObserveArrival(ReadPose("Pose")); step = 4; return; }
            if (step == 4)
            {
                Foreground(); if (!arrived || !ActiveRunController.Instance.IsReady) return;
                Debug.Log("LEVELSTART PASS: Hard Restart uses currently authored root.");
                Check(ActiveRunController.Instance.QuitToMenu(), "Second Quit failed"); step = 5; return;
            }
            if (step == 5)
            {
                if (SceneManager.GetActiveScene().name != "Menu") return;
                reloaded = false;
                Check(RunSaveService.StartFresh("ConstructionSite", true), "Confirmed New Game failed");
                Check(RunSaveService.TryReadActive(out var discarded) && discarded == null, "New Game did not discard old snapshot");
                step = 6; return;
            }
            if (step == 6) { if (!reloaded) return; ObserveArrival(ReadPose("Pose")); step = 7; return; }
            if (step == 7)
            {
                Foreground(); if (!arrived) return;
                Debug.Log("LEVELSTART REVIEW PASSED: three authored root positions/yaws, canonical repair twice each, Continue, Hard Restart and confirmed New Game.");
                File.WriteAllText("Logs/LevelStart/play-result.txt", "PASS: A/B/C/D/E/F; see play.log for exact poses and checks.");
                Finish();
            }
        }
        catch (Exception error) { Fail(error); }
    }
    private static void Log(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) SessionState.SetBool(Key + "Failed", true);
    }
    private static void Fail(Exception error) { SessionState.SetBool(Key + "Failed", true); Debug.LogException(error); Finish(); }
    private static void Finish()
    {
        if (transport != null) transport.TransportEnded -= ArrivalEnded;
        EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
        SceneManager.sceneLoaded -= Loaded;
        EditorApplication.ExitPlaymode();
    }

    public static void Audit()
    {
        foreach (var name in new[] { "ConstructionSite", "GamePoc" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/" + name + ".unity");
            var owners = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerBeamInSequence>(true)).ToArray();
            var player = Object.FindAnyObjectByType<BeamTransportController>();
            Debug.Log($"LEVELSTART AUDIT: {name}, owners={owners.Length}, beamPlayer={player != null}");
            if (player == null) continue;
            foreach (var owner in owners)
            {
                var data = new SerializedObject(owner);
                float height = data.FindProperty("startHeight").floatValue;
                foreach (var pose in new[] { owner.ArrivalTransform })
                    Debug.Log($"LEVELSTART AUDIT: {pose.name} world={pose.position:F5}, local={pose.localPosition:F5}, yaw={pose.eulerAngles.y}, support={player.IsLandingSafe(pose.position)}, path={player.IsSegmentClear(pose.position + Vector3.up * height, pose.position)}");
            }
        }
    }

    // One-time coordinate-contract migration. Routine repair deliberately never rebases a root.
    public static void Migrate()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        var owner = Object.FindAnyObjectByType<PlayerBeamInSequence>();
        var oldHelper = owner.transform.Find("BeamInSpawn");
        Vector3 landing = oldHelper != null ? oldHelper.position : owner.transform.position;
        Quaternion facing = oldHelper != null ? oldHelper.rotation : owner.transform.rotation;
        bool migrateScene = oldHelper != null;
        var prefab = PrefabUtility.LoadPrefabContents(BeamTransportSetup.LevelStartPath);
        try
        {
            var helper = prefab.transform.Find("BeamInSpawn");
            if (helper != null)
            {
                Object.DestroyImmediate(helper.gameObject);
                if (prefab.GetComponent<BeamArrivalPoint>() == null) prefab.AddComponent<BeamArrivalPoint>();
                // Only the arrival wrapper moves. The finished shared VFX asset is untouched.
                prefab.GetComponentInChildren<BeamTransportVFX>(true).transform.localPosition = Vector3.zero;
                PrefabUtility.SaveAsPrefabAsset(prefab, BeamTransportSetup.LevelStartPath);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        if (migrateScene)
        {
            owner = Object.FindAnyObjectByType<PlayerBeamInSequence>();
            owner.transform.SetPositionAndRotation(landing, facing);
            PrefabUtility.RecordPrefabInstancePropertyModifications(owner.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log($"LEVELSTART MIGRATION: root now equals existing player landing {landing:F5}, yaw={facing.eulerAngles.y}; helper removed; shared Beam VFX not modified.");
        Audit();
    }
}
