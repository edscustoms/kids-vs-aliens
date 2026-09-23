using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;

[InitializeOnLoad]
public static class BeamTransportV2Review
{
    public const string Reports = "Logs/BeamV2";
    static BeamTransportV2Review() => EditorApplication.update += Tick;
    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
            || !File.Exists("Temp/BeamV2.request")) return;
        string request = File.ReadAllText("Temp/BeamV2.request").Trim();
        File.Delete("Temp/BeamV2.request");
        try
        {
            Directory.CreateDirectory(Reports);
            if (request == "snapshot") Snapshot();
            else if (request == "migrate") BeamTransportPrefabMigration.MigrateActiveScene();
            else if (request == "inspect") Inspect();
            else if (request == "author") AuthorSample();
            else if (request == "play") BeamTransportV2PlayReview.Begin();
            else if (request == "tests") { RunTests(); return; }
            File.WriteAllText(Reports + "/result.txt", "OK " + request);
        }
        catch (Exception error) { File.WriteAllText(Reports + "/result.txt", error.ToString()); Debug.LogException(error); }
    }

    private static void AuthorSample()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Game/Scenes/ConstructionSite.unity") throw new Exception("Open ConstructionSite first.");
        var target = UnityEngine.Object.FindObjectsByType<BeamHoistTarget>(FindObjectsInactive.Include)
            .Single(t => t.transform.parent.name == "Shipping Container 20 Foot Closed (2)");
        var prop = target.transform.parent.gameObject;
        var surface = prop.GetComponent<BeamHoistSurface>() ?? Undo.AddComponent<BeamHoistSurface>(prop);
        BeamHoistSurfaceBaker.Bake(surface, true);
        if (!surface.IsBaked) throw new Exception(surface.BakeStatus);
        // Keep the user's old marker placement available as an optional authored fallback, but do not use its radius.
        Undo.RecordObject(target, "Use baked lower-side hoist zones"); target.enabled = false;
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerCharacter>();
        BeamTransportSetup.ConfigureScene(player);
        var report = new StringBuilder(surface.BakeStatus + "\n" + Describe(prop));
        foreach (var collider in prop.GetComponentsInChildren<Collider>(true))
            report.AppendLine($"COLLIDER {collider.name}: {collider.GetType().Name} bounds={collider.bounds}");
        var transport = player.GetComponent<BeamTransportController>();
        var ability = player.GetComponent<BeamHoistAbility>();
        Vector3 original = player.transform.position;
        try
        {
            for (int i = 0; i < surface.CandidateCount; i++)
            {
                var cell = surface.GetBakedApproach(i);
                Vector3 sample = surface.transform.TransformPoint(cell.region.center);
                if (!Physics.Raycast(sample + Vector3.up, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                Vector3 start = hit.point + Vector3.up * (.02f - transport.FeetOffset);
                player.transform.position = start;
                bool candidate = surface.TryGetCandidate(start, i, transport.FeetOffset, out var end, out float release);
                var path = BeamHoistPath.Create(start, end, release, 1.5f, .9f);
                report.AppendLine($"CELL {i} side={cell.side} start={start:R} end={end:R} candidate={candidate} grounded={transport.IsLandingSafe(start)} limits={ability.IsWithinLimits(path)} clear={candidate && transport.CanHoist(path)}");
            }
        }
        finally { player.transform.position = original; Physics.SyncTransforms(); }
        File.WriteAllText(Reports + "/smart-container.txt", report.ToString());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void Inspect()
    {
        var report = new StringBuilder();
        foreach (var target in UnityEngine.Object.FindObjectsByType<BeamHoistTarget>(FindObjectsInactive.Include))
        {
            report.AppendLine("TARGET " + AnimationUtility.CalculateTransformPath(target.transform, null));
            for (var t = target.transform; t != null; t = t.parent)
            {
                report.AppendLine($"  {t.name}: world={t.position:R} rotation={t.eulerAngles:R} scale={t.lossyScale:R}");
                foreach (var c in t.GetComponents<Collider>()) report.AppendLine($"  {c.GetType().Name} enabled={c.enabled} bounds={c.bounds}");
            }
        }
        File.WriteAllText(Reports + "/scene-inspection.txt", report.ToString());
    }

    private static TestRunnerApi testApi;
    private static void RunTests()
    {
        testApi = ScriptableObject.CreateInstance<TestRunnerApi>();
        testApi.RegisterCallbacks(new Results());
        testApi.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "BeamTransportTests", "BeamTransportV2Tests" } }));
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, Reports + "/tests.xml");
            File.WriteAllText(Reports + "/result.txt", $"Tests: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped");
        }
    }

    public static void Snapshot()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Game/Scenes/ConstructionSite.unity") throw new Exception("ConstructionSite must be open.");
        string backup = Reports + "/ConstructionSite-before-v2.unity";
        if (File.Exists(backup)) throw new Exception("Preservation snapshot already exists; refusing to overwrite it.");
        EditorSceneManager.SaveScene(scene, backup, true);
        File.Copy(BeamTransportSetup.VfxPath, Reports + "/BeamTransportVFX-before-v2.prefab", false);
        var level = scene.GetRootGameObjects().Single(r => r.name == "LevelStart");
        File.WriteAllText(Reports + "/authored-before.txt", Describe(level));
    }

    public static string Describe(GameObject root)
    {
        var report = new StringBuilder();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(t, root.transform);
            report.AppendLine($"{path}: local={t.localPosition:R}; rotation={t.localRotation:R}; scale={t.localScale:R}; world={t.position:R}; active={t.gameObject.activeSelf}");
            foreach (var component in t.GetComponents<Component>())
            {
                if (component is Transform) continue;
                report.AppendLine(component.GetType().Name + ": " + EditorJsonUtility.ToJson(component));
                if (component is Renderer renderer)
                    report.AppendLine("materials=" + string.Join(",", renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath)));
            }
        }
        return report.ToString();
    }
}
