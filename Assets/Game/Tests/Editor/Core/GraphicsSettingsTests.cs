using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[Category("GraphicsSettings")]
public sealed class GraphicsSettingsTests
{
    [Test]
    public void DefaultsPreserveHighAndExposeCheaperTiersAndBuildVariants()
    {
        var config = Resources.Load<GraphicsQualityConfiguration>("GraphicsQuality");
        Assert.That(config, Is.Not.Null);
        Assert.That(config.high.renderScaleMultiplier, Is.EqualTo(1));
        Assert.That(config.high.terrainPixelErrorMultiplier, Is.EqualTo(1));
        Assert.That(config.high.lodBiasMultiplier, Is.EqualTo(1));
        Assert.That(config.high.farClipLimit, Is.Zero);
        Assert.That(config.high.postProcessing, Is.EqualTo(GraphicsPostQuality.Original));
        Assert.That(config.low.renderScaleMultiplier, Is.LessThan(config.medium.renderScaleMultiplier));
        Assert.That(config.medium.renderScaleMultiplier, Is.LessThan(1));
        foreach (var slot in config.terrainMaterials)
        {
            Assert.That(slot.original, Is.Not.Null);
            Assert.That(config.GetTerrainMaterial(slot.original, GraphicsQuality.High), Is.SameAs(slot.original));
            Assert.That(config.GetTerrainMaterial(slot.original, GraphicsQuality.Medium), Is.SameAs(slot.medium != null ? slot.medium : slot.original));
            Assert.That(slot.low.shader.name, Is.EqualTo("Universal Render Pipeline/Terrain/Lit"));
            Assert.That(slot.low.shader.isSupported, Is.True);
        }
        GraphicsSettingsBuildCheck.Validate();
        Assert.That(GraphicsSettings.GetRenderPipelineSettings<URPShaderStrippingSetting>().stripUnusedVariants, Is.True,
            "Runtime presets must not retain all unused renderer feature combinations.");
        foreach (var path in new[] { "Assets/Game/Settings/Rendering/Mobile_Renderer.asset", "Assets/Game/Settings/Rendering/PC_Renderer.asset" })
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            Assert.That(renderer.rendererFeatures.OfType<PostProcessingAlphaVariants>().Count(f => f.isActive), Is.EqualTo(1));
            var before = EditorJsonUtility.ToJson(renderer);
            GraphicsSettingsSetup.EnsureAlphaOutputVariants(renderer);
            Assert.That(EditorJsonUtility.ToJson(renderer), Is.EqualTo(before), "Retention setup must be idempotent.");
        }
        Assert.That(DeveloperFpsHud.ColorFor(60), Is.EqualTo(Color.green));
        Assert.That(DeveloperFpsHud.ColorFor(59), Is.EqualTo(Color.yellow));
        Assert.That(DeveloperFpsHud.ColorFor(30), Is.EqualTo(Color.yellow));
        Assert.That(DeveloperFpsHud.ColorFor(29), Is.EqualTo(Color.red));
    }

    [TestCase("ConstructionSite")]
    [TestCase("BikeRoute")]
    public void SceneTargetsAreExplicitCompleteAndRepairDoesNotDuplicate(string name)
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Game/Scenes/" + name + ".unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var owner = roots.SelectMany(r => r.GetComponentsInChildren<SceneGraphicsSettings>(true)).Single();
            var serialized = new SerializedObject(owner);
            Assert.That(serialized.FindProperty("terrains").arraySize, Is.EqualTo(roots.Sum(r => r.GetComponentsInChildren<Terrain>(true).Length)));
            var before = EditorJsonUtility.ToJson(owner);
            Assert.That(GraphicsSettingsSetup.ConfigureScene(scene), Is.SameAs(owner));
            Assert.That(EditorJsonUtility.ToJson(owner), Is.EqualTo(before));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [UnityTest]
    public IEnumerator BothLevelsAllCombinationsContinueAndPersistentOwner()
    {
        SessionState.SetBool("GraphicsTests.active", true);
        SessionState.SetInt("GraphicsTests.quality", PlayerPrefs.GetInt(GraphicsPreferences.QualityKey, 2));
        SessionState.SetInt("GraphicsTests.shadows", PlayerPrefs.GetInt(GraphicsPreferences.ShadowsKey, 1));
        SessionState.SetString("GraphicsTests.saves", Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/GraphicsSettingsTestSaves"));
        RunSaveService.DiscardActive();
        GraphicsPreferences.Quality = GraphicsQuality.High; GraphicsPreferences.Shadows = true;
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        Directory.CreateDirectory("Logs/GraphicsSettings");
        yield return WaitReady();
        var owner = GraphicsSettingsController.Instance;
        Assert.That(owner, Is.Not.Null);
        yield return CheckLevel("ConstructionSite");
        ActiveRunController.Instance.PrepareToLeave(); RunSaveService.DiscardActive();
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("BikeRoute");
        yield return WaitReady();
        Assert.That(GraphicsSettingsController.Instance, Is.SameAs(owner));
        yield return CheckLevel("BikeRoute");
        Assert.That(Object.FindObjectsByType<GraphicsSettingsController>().Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<DeveloperFpsHud>().Length, Is.EqualTo(1));
        ActiveRunController.Instance.PrepareToLeave();
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Menu");
        yield return Frames(10);
        Object.FindAnyObjectByType<UIScreenRouter>().ShowOptions();
        yield return Frames(10);
        var menuOptions = Object.FindAnyObjectByType<OptionsScreenController>();
        var qualityButton = menuOptions.transform.Find("GraphicsOptions/GraphicsQuality").GetComponent<Button>();
        qualityButton.onClick.Invoke();
        Assert.That(GraphicsPreferences.Quality, Is.EqualTo(GraphicsQuality.Low));
        yield return Frames(12);
        ScreenCapture.CaptureScreenshot("Logs/GraphicsSettings/Menu-options.png");
        yield return Frames(4);
        GraphicsPreferences.Quality = GraphicsQuality.High;
        yield return new ExitPlayMode();
    }

    private static IEnumerator CheckLevel(string name)
    {
        var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
        gameView.Show(); gameView.Focus(); gameView.Repaint();
        for (int i = 0; i < 160; i++) yield return EditorTestFrame.Next();
        var owner = GraphicsSettingsController.Instance;
        var terrain = Object.FindObjectsByType<Terrain>().First();
        var material = terrain.materialTemplate;
        float error = terrain.heightmapPixelError, scale = owner.RuntimePipeline.renderScale, shadows = owner.RuntimePipeline.shadowDistance;
        var menu = Object.FindAnyObjectByType<InGameMenuController>();
        menu.OpenMenu(); menu.ShowOptions();
        Assert.That(menu.OptionsScreen.activeInHierarchy, Is.True);
        var options = menu.OptionsScreen.transform.Find("GraphicsOptions");
        Assert.That(options, Is.Not.Null);
        Canvas.ForceUpdateCanvases();
        var cameraCorners = new Vector3[4];
        var graphicsCorners = new Vector3[4];
        ((RectTransform)menu.OptionsScreen.transform.Find("CurrentCameraModeLabel")).GetWorldCorners(cameraCorners);
        ((RectTransform)options.Find("GraphicsQuality")).GetWorldCorners(graphicsCorners);
        Assert.That(cameraCorners[0].y, Is.GreaterThan(graphicsCorners[1].y), "Camera and graphics rows overlap.");
        options.Find("GraphicsQuality").GetComponent<Button>().onClick.Invoke();
        Assert.That(GraphicsPreferences.Quality, Is.EqualTo(GraphicsQuality.Low));
        options.Find("Shadows").GetComponent<Button>().onClick.Invoke();
        Assert.That(GraphicsPreferences.Shadows, Is.False);
        yield return Frames(5);
        ScreenCapture.CaptureScreenshot("Logs/GraphicsSettings/" + name + "-options.png");
        yield return Frames(4);
        menu.ResumeGame();
        var lease = ActiveRunController.Instance.GetComponent<GameplaySuspensionController>().Acquire(SuspensionReason.ManualPause);
        using var counter = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count", 1);
        float renderedFar = 0; bool renderedPost = false;
        void Observe(ScriptableRenderContext context, Camera camera)
        {
            if (camera != Camera.main) return;
            renderedFar = camera.farClipPlane;
            renderedPost = camera.GetUniversalAdditionalCameraData().renderPostProcessing;
        }
        RenderPipelineManager.beginCameraRendering += Observe;
        try
        {
            foreach (GraphicsQuality quality in new[] { GraphicsQuality.High, GraphicsQuality.Medium, GraphicsQuality.Low, GraphicsQuality.High })
            {
                GraphicsPreferences.Quality = quality;
                foreach (bool enabled in new[] { false, true })
                {
                    GraphicsPreferences.Shadows = enabled;
                    var preset = owner.Configuration.Get(quality);
                    yield return Frames(12);
                    Assert.That(renderedFar, Is.GreaterThan(0), "No gameplay camera was rendered; run this fixture in a visible Game view.");
                    Assert.That(owner.RuntimePipeline.renderScale, Is.EqualTo(scale * preset.renderScaleMultiplier).Within(.0001));
                    Assert.That(owner.RuntimePipeline.shadowDistance, Is.EqualTo(enabled ? shadows * preset.shadowDistanceMultiplier : 0).Within(.0001));
                    Assert.That(terrain.heightmapPixelError, Is.EqualTo(error * preset.terrainPixelErrorMultiplier).Within(.001));
                    Assert.That(terrain.materialTemplate, Is.SameAs(owner.Configuration.GetTerrainMaterial(material, quality)));
                    Assert.That(terrain.materialTemplate.shader.isSupported, Is.True);
                    if (preset.farClipLimit > 0) Assert.That(renderedFar, Is.LessThanOrEqualTo(preset.farClipLimit + .01));
                    if (quality == GraphicsQuality.Low) Assert.That(renderedPost, Is.False);
                    if (!enabled && counter.Valid) Assert.That(counter.LastValue, Is.Zero, "Realtime shadow casters must be absent with Shadows OFF.");
                    File.AppendAllText("Logs/GraphicsSettings/effective.csv", $"{name},{quality},{enabled},{owner.RuntimePipeline.renderScale},{terrain.heightmapPixelError},{renderedFar},{renderedPost},{(counter.Valid ? counter.LastValue : -1)},{terrain.materialTemplate.shader.name}\n");
                    ScreenCapture.CaptureScreenshot($"Logs/GraphicsSettings/{name}-{quality}-{enabled}.png");
                    yield return Frames(3);
                }
            }
            Assert.That(terrain.materialTemplate, Is.SameAs(material));
            Assert.That(terrain.heightmapPixelError, Is.EqualTo(error));
        }
        finally { RenderPipelineManager.beginCameraRendering -= Observe; lease.Dispose(); }
        // Graphics preferences must survive real Continue; inventory remains owned by Active Run.
        GraphicsPreferences.Quality = GraphicsQuality.Medium; GraphicsPreferences.Shadows = false;
        var run = ActiveRunController.Instance;
        var inventory = run.GetComponent<PlayerInventory>(); inventory.RestoreCapsules(9, 3);
        Assert.That(run.Save(), Is.True, RunSaveService.LastError);
        run.PrepareToLeave(); Assert.That(RunSaveService.Continue(), Is.True, RunSaveService.LastError);
        yield return Frames(2); yield return WaitReady();
        Assert.That(GraphicsSettingsController.Instance, Is.SameAs(owner));
        Assert.That(GraphicsPreferences.Quality, Is.EqualTo(GraphicsQuality.Medium));
        Assert.That(owner.RuntimePipeline.shadowDistance, Is.Zero);
        Assert.That(ActiveRunController.Instance.GetComponent<PlayerInventory>().PlasmaCapsules, Is.EqualTo(9));
        Assert.That(ActiveRunController.Instance.GetComponent<PlayerInventory>().ArmorCapsules, Is.EqualTo(3));
        Assert.That(Object.FindObjectsByType<GraphicsSettingsController>().Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<ActiveRunController>().Length, Is.EqualTo(1));
        GraphicsPreferences.Quality = GraphicsQuality.High; GraphicsPreferences.Shadows = true;
        yield return Frames(4);
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return EditorTestFrame.Next(); }
    private static IEnumerator WaitReady()
    {
        double timeout = EditorApplication.timeSinceStartup + 40;
        while ((ActiveRunController.Instance == null || !ActiveRunController.Instance.IsReady) && EditorApplication.timeSinceStartup < timeout)
            yield return EditorTestFrame.Next();
        Assert.That(ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, Is.True);
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (!SessionState.GetBool("GraphicsTests.active", false)) yield break;
        if (Application.isPlaying)
        {
            ActiveRunController.Instance?.PrepareToLeave();
            yield return new ExitPlayMode();
        }
        GraphicsPreferences.Quality = (GraphicsQuality)SessionState.GetInt("GraphicsTests.quality", 2);
        GraphicsPreferences.Shadows = SessionState.GetInt("GraphicsTests.shadows", 1) != 0;
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString("GraphicsTests.saves", ""));
        SessionState.SetBool("GraphicsTests.active", false);
    }
}
