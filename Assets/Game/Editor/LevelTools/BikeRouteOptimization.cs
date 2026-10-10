using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Only measured BikeRoute changes. Bake validates authoring rather than guessing
// which moving objects can safely lose their shadows.
[InitializeOnLoad]
public static class BikeRouteOptimization
{
    public const string SettingsPath = "Assets/Game/Editor/LevelTools/BikeRouteOptimizationSettings.asset";
    public const string ReportDirectory = "Logs/BikeRouteOptimization";
    const string BakeKey = "BikeRouteOptimization.Baking";
    const string BakeErrorKey = "BikeRouteOptimization.BakeError";
    const string BakeSettingsKey = "BikeRouteOptimization.BakeSettings";

    [Serializable]
    public sealed class Report
    {
        public string operation, utc, scene;
        public int changedObjects, renderers, lodGroups, windRenderers, staticBatchRenderers, lightmaps;
        public long lightmapBytes;
        public float terrainPixelError;
        public List<string> changes = new();
        public List<string> lightingBlockers = new();
        public List<string> observations = new();
    }

    static BikeRouteOptimization()
    {
        Lightmapping.bakeCompleted += BakeCompleted;
        Lightmapping.bakeCancelled += BakeCancelled;
        Application.logMessageReceived += ObserveBakeError;
    }

    public static BikeRouteOptimizationSettings LoadSettings() =>
        AssetDatabase.LoadAssetAtPath<BikeRouteOptimizationSettings>(SettingsPath);

    public static void RequireScene(Scene scene)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Exit Play Mode and wait for any lighting bake first.");
        if (!scene.IsValid() || scene.path != BikeRouteChaseSetup.ScenePath || SceneManager.sceneCount != 1)
            throw new InvalidOperationException("Open BikeRoute alone. Other scenes are never modified by this tool.");
    }

    static T[] Components<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    public static Report Audit(Scene scene, BikeRouteOptimizationSettings settings)
    {
        RequireScene(scene);
        if (settings == null) throw new InvalidOperationException("Select a BikeRoute optimization settings asset.");
        if (float.IsNaN(settings.terrainPixelError) || settings.terrainPixelError < 1 || settings.terrainPixelError > 10)
            throw new InvalidOperationException("Terrain pixel error must be between 1 and 10.");
        if (settings.lightmapMemoryBudgetMiB < 1)
            throw new InvalidOperationException("Lightmap budget must be at least 1 MiB.");
        var terrains = Components<Terrain>(scene);
        if (terrains.Length != 1) throw new InvalidOperationException("Expected exactly one authored BikeRoute terrain; no changes made.");
        var terrain = terrains[0];
        var renderers = Components<Renderer>(scene);
        var report = new Report { operation = "Audit", utc = DateTime.UtcNow.ToString("O"), scene = scene.path,
            terrainPixelError = terrain.heightmapPixelError, renderers = renderers.Length,
            lodGroups = Components<LODGroup>(scene).Length, lightmaps = LightmapSettings.lightmaps.Length };
        if (!Mathf.Approximately(terrain.heightmapPixelError, settings.terrainPixelError))
            report.changes.Add($"{PathOf(terrain.transform)}: heightmapPixelError {terrain.heightmapPixelError} -> {settings.terrainPixelError}; render LOD only, TerrainData/collider unchanged.");
        var shaders = new HashSet<Shader>();
        foreach (var renderer in renderers)
        {
            bool contributes = (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.ContributeGI) != 0;
            bool wind = renderer.sharedMaterials.Any(IsWindMaterial);
            if (wind) report.windRenderers++;
            if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) != 0) report.staticBatchRenderers++;
            foreach (var material in renderer.sharedMaterials)
                if (material != null && material.shader != null) shaders.Add(material.shader);
            if (!contributes) continue;
            if (wind) report.lightingBlockers.Add(PathOf(renderer.transform) + ": wind foliage contributes GI; exclude from static baking before authoring a bake.");
            if (renderer.GetComponentInParent<Rigidbody>() != null || renderer.GetComponentInParent<Animator>() != null)
                report.lightingBlockers.Add(PathOf(renderer.transform) + ": moving/animated hierarchy marked Contribute GI.");
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && !filter.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                report.lightingBlockers.Add(PathOf(renderer.transform) + ": no secondary lightmap UVs; no importer was changed.");
        }
        if (terrain.materialTemplate != null) shaders.Add(terrain.materialTemplate.shader);
        foreach (var shader in shaders.OrderBy(s => s.name))
        {
            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (!shaderPath.StartsWith("Assets/Game/Scenes/BikeRoute/", StringComparison.Ordinal) || !File.Exists(shaderPath)) continue;
            string source = File.ReadAllText(shaderPath);
            if (source.Contains("bakedGI=SampleSH", StringComparison.Ordinal) && !source.Contains("SAMPLE_GI", StringComparison.Ordinal))
                report.lightingBlockers.Add(shader.name + ": authored receiver ignores lightmaps and uses a constant shadow mask. Replacing realtime shadows with baked shadows is unsafe.");
        }
        if (settings.lightingSettings == null)
            report.lightingBlockers.Add("No reviewed BikeRoute-only LightingSettings asset assigned.");
        else if (!AssetDatabase.GetAssetPath(settings.lightingSettings).Contains("BikeRoute", StringComparison.Ordinal))
            report.lightingBlockers.Add("LightingSettings must be owned by BikeRoute, not shared with another level.");
        if (Components<LightProbeGroup>(scene).Length == 0)
            report.lightingBlockers.Add("No authored light-probe group for dynamic actors. Do not remove their realtime lighting.");
        foreach (var light in Components<Light>(scene))
            report.observations.Add($"Light {PathOf(light.transform)}: {light.type}, {light.lightmapBakeType}, shadows {light.shadows}, intensity {light.intensity}.");
        var textures = LightmapSettings.lightmaps.SelectMany(l => new[] { l.lightmapColor, l.lightmapDir, l.shadowMask }).Where(t => t != null).Distinct();
        foreach (var texture in textures) report.lightmapBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
        if (report.lightmapBytes > (long)settings.lightmapMemoryBudgetMiB * 1024 * 1024)
            report.lightingBlockers.Add($"Lightmaps exceed the configured {settings.lightmapMemoryBudgetMiB} MiB budget.");
        report.observations.Add("Mesh complexity is audited in Edit Mode; runtime static-batch sharedMesh counts are not per-object source triangle counts.");
        foreach (var group in Components<MeshFilter>(scene).Where(f => f.sharedMesh != null)
                     .GroupBy(f => f.sharedMesh).OrderByDescending(g => TriangleCount(g.Key)).Take(12))
            report.observations.Add($"Source mesh {AssetDatabase.GetAssetPath(group.Key)} / {group.Key.name}: {TriangleCount(group.Key)} triangles, {group.Count()} references including inactive LODs; not a visible-triangle total.");
        report.observations.Add("No shader, shadow, foliage, reflection probe, quality preset, collider or gameplay changes are implicit in Apply.");
        return report;
    }

    static bool IsWindMaterial(Material material) => material != null && material.shader != null &&
        (material.shader.name.StartsWith("CTI/", StringComparison.Ordinal) || material.shader.name.Contains("SpeedTree", StringComparison.Ordinal));

    static long TriangleCount(Mesh mesh)
    {
        long triangles = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            if (mesh.GetTopology(i) == MeshTopology.Triangles) triangles += mesh.GetIndexCount(i) / 3;
        return triangles;
    }

    public static Report Apply(Scene scene, BikeRouteOptimizationSettings settings)
    {
        var report = Audit(scene, settings); // Fatal checks happen before Undo/mutation.
        report.operation = "Apply";
        var terrain = Components<Terrain>(scene).Single();
        if (report.changes.Count > 0)
        {
            Undo.RecordObject(terrain, "Optimize BikeRoute terrain render LOD");
            terrain.heightmapPixelError = settings.terrainPixelError;
            PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
            EditorUtility.SetDirty(terrain);
            EditorSceneManager.MarkSceneDirty(scene);
            report.changedObjects = 1;
        }
        WriteReport(report, "apply.json");
        return report;
    }

    public static Report Verify(Scene scene, BikeRouteOptimizationSettings settings)
    {
        var report = Audit(scene, settings);
        report.operation = "Verify";
        report.observations.Add(report.changes.Count == 0 ? "PASS: configured optimizations already applied; second Apply changes nothing." : "PENDING: configured terrain LOD differs from scene.");
        WriteReport(report, "verify.json");
        return report;
    }

    public static Report Bake(Scene scene, BikeRouteOptimizationSettings settings)
    {
        var report = Audit(scene, settings);
        report.operation = "Bake preflight";
        WriteReport(report, "bake-preflight.json");
        if (scene.isDirty) throw new InvalidOperationException("Save BikeRoute before baking. No settings or lightmaps changed.");
        if (report.lightingBlockers.Count > 0)
            throw new InvalidOperationException($"Bake blocked by {report.lightingBlockers.Count} authoring issues. See {ReportDirectory}/bake-preflight.json. Realtime shadows were preserved.");
        if (Lightmapping.lightingSettings != settings.lightingSettings)
            throw new InvalidOperationException("Assign the reviewed LightingSettings to this scene and save it before baking; Bake never silently changes settings.");
        SessionState.SetBool(BakeKey, true); SessionState.SetBool(BakeErrorKey, false);
        SessionState.SetString(BakeSettingsKey, AssetDatabase.GetAssetPath(settings));
        try { if (!Lightmapping.BakeAsync()) throw new InvalidOperationException("Unity could not start the bake."); }
        catch { SessionState.SetBool(BakeKey, false); throw; }
        report.observations.Add("Asynchronous bake started. Realtime shadow flags remain unchanged.");
        return report;
    }

    static void ObserveBakeError(string message, string stack, LogType type)
    {
        if (SessionState.GetBool(BakeKey, false) && (type == LogType.Error || type == LogType.Exception)) SessionState.SetBool(BakeErrorKey, true);
    }
    static void BakeCompleted()
    {
        if (!SessionState.GetBool(BakeKey, false)) return;
        SessionState.SetBool(BakeKey, false);
        if (SessionState.GetBool(BakeErrorKey, false) || Lightmapping.lightingDataAsset == null || LightmapSettings.lightmaps.Length == 0)
        { Debug.LogError("BikeRoute bake did not produce verified lighting. Realtime shadow flags were not changed."); return; }
        var scene = SceneManager.GetActiveScene();
        if (scene.path != BikeRouteChaseSetup.ScenePath) { Debug.LogError("Active scene changed during bake; no scene saved."); return; }
        var settings = AssetDatabase.LoadAssetAtPath<BikeRouteOptimizationSettings>(SessionState.GetString(BakeSettingsKey, ""));
        var report = Verify(scene, settings);
        if (report.lightingBlockers.Count > 0)
        { Debug.LogError("BikeRoute bake needs review (including lightmap budget); scene left unsaved. See verify.json."); return; }
        EditorSceneManager.SaveScene(scene);
    }
    static void BakeCancelled() { if (SessionState.GetBool(BakeKey, false)) Debug.LogWarning("BikeRoute lighting bake cancelled."); SessionState.SetBool(BakeKey, false); }
    public static void WriteReport(Report report, string filename)
    {
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(Path.Combine(ReportDirectory, filename), JsonUtility.ToJson(report, true));
    }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
}

public sealed class BikeRouteOptimizationWindow : EditorWindow
{
    BikeRouteOptimizationSettings settings;
    Editor settingsEditor;
    Vector2 scroll;
    string result = "Dry Run reports proposed changes without touching the scene. Apply is Undoable; save explicitly after review.";
    [MenuItem("Tools/BikeRoute/Optimize Scene")]
    public static void Open() => GetWindow<BikeRouteOptimizationWindow>("Optimize BikeRoute");
    void OnEnable() => settings = BikeRouteOptimization.LoadSettings();
    void OnDisable() { if (settingsEditor != null) DestroyImmediate(settingsEditor); }
    void OnGUI()
    {
        settings = (BikeRouteOptimizationSettings)EditorGUILayout.ObjectField("Configuration", settings, typeof(BikeRouteOptimizationSettings), false);
        if (settings != null)
        {
            Editor.CreateCachedEditor(settings, null, ref settingsEditor);
            settingsEditor.OnInspectorGUI();
        }
        using (new EditorGUI.DisabledScope(settings == null || EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning))
        {
            if (GUILayout.Button("Dry Run / Audit")) Run(() => { var r = BikeRouteOptimization.Audit(SceneManager.GetActiveScene(), settings); BikeRouteOptimization.WriteReport(r, "audit.json"); return r; });
            if (GUILayout.Button("Apply Optimizations")) Run(() => BikeRouteOptimization.Apply(SceneManager.GetActiveScene(), settings));
            if (GUILayout.Button("Bake Lighting")) Run(() => BikeRouteOptimization.Bake(SceneManager.GetActiveScene(), settings));
            if (GUILayout.Button("Verify / Generate Report")) Run(() => BikeRouteOptimization.Verify(SceneManager.GetActiveScene(), settings));
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(result, MessageType.Info);
        EditorGUILayout.EndScrollView();
    }
    void Run(Func<BikeRouteOptimization.Report> action)
    {
        try { var r = action(); result = $"{r.operation}: {r.changedObjects} objects changed.\n" + string.Join("\n", r.changes) + $"\nBake blockers: {r.lightingBlockers.Count}.\nReports: {BikeRouteOptimization.ReportDirectory}"; }
        catch (Exception e) { result = e.Message; Debug.LogWarning(e.Message); }
    }
}

