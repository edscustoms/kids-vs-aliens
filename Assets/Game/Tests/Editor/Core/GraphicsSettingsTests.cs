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
    [SetUp]
    public void ResetTerrainComparison()
    {
        SessionState.SetInt("GraphicsTests.terrain", PlayerPrefs.GetInt(GraphicsPreferences.ExperimentalTerrainKey, 0));
        GraphicsPreferences.ExperimentalTerrain = false;
    }
    [TearDown]
    public void RestoreTerrainComparison() => GraphicsPreferences.ExperimentalTerrain = SessionState.GetInt("GraphicsTests.terrain", 0) != 0;

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
        var experiment = config.terrainMaterials.Single(s => s.experimentalMedium != null);
        Assert.That(experiment.experimentalMedium.shader.name, Is.EqualTo("Environment/BikeRoute Terrain Distance V2"));
        Assert.That(experiment.experimentalBasemapVariants.IsKeywordEnabled("_MASKMAP"), Is.True);
        Assert.That(experiment.experimentalBasemapDistance, Is.EqualTo(150));
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
    public IEnumerator MediumOverridesFallbackBloomWithoutChangingAuthoredProfiles()
    {
        SessionState.SetBool("GraphicsTests.active", true);
        SessionState.SetInt("GraphicsTests.quality", PlayerPrefs.GetInt(GraphicsPreferences.QualityKey, 2));
        SessionState.SetInt("GraphicsTests.shadows", PlayerPrefs.GetInt(GraphicsPreferences.ShadowsKey, 1));
        SessionState.SetString("GraphicsTests.saves", Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GraphicsPreferences.Quality = GraphicsQuality.High;
        yield return new EnterPlayMode();
        var fallbackObject = new GameObject("Fallback global volume");
        var fallback = fallbackObject.AddComponent<Volume>();
        fallback.isGlobal = true; fallback.priority = -100;
        var fallbackProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        fallbackProfile.Add<Bloom>().intensity.Override(.25f);
        fallback.sharedProfile = fallbackProfile;
        try
        {
            // A scene can either author Bloom or inherit it entirely from lower profiles.
            foreach (bool authoredBloom in new[] { false, true })
            {
                var target = new GameObject("Scene volume");
                target.SetActive(false);
                var volume = target.AddComponent<Volume>();
                volume.isGlobal = true; volume.priority = 100;
                var original = ScriptableObject.CreateInstance<VolumeProfile>();
                original.Add<ColorAdjustments>().postExposure.Override(.35f);
                if (authoredBloom) original.Add<Bloom>().intensity.Override(.4f);
                volume.sharedProfile = original;
                var before = original.components.Select(component => EditorJsonUtility.ToJson(component)).ToArray();
                var owner = target.AddComponent<SceneGraphicsSettings>();
                owner.Configure(Array.Empty<Terrain>(), new[] { volume });
                target.SetActive(true);
                try
                {
                    foreach (var tier in new[] { GraphicsQuality.Low, GraphicsQuality.Medium, GraphicsQuality.High, GraphicsQuality.Medium })
                    {
                        GraphicsPreferences.Quality = tier;
                        VolumeManager.instance.Update(target.transform, ~0);
                        var stack = VolumeManager.instance.stack;
                        Assert.That(stack.GetComponent<Bloom>().intensity.value,
                            Is.EqualTo(tier == GraphicsQuality.Medium ? 0f : authoredBloom ? .4f : .25f).Within(.0001f));
                        Assert.That(stack.GetComponent<ColorAdjustments>().postExposure.value, Is.EqualTo(.35f).Within(.0001f));
                        Assert.That(volume.sharedProfile, Is.SameAs(original));
                        Assert.That(original.components.Select(component => EditorJsonUtility.ToJson(component)), Is.EqualTo(before));
                        if (tier != GraphicsQuality.Medium) Assert.That(volume.profile, Is.SameAs(original));
                    }
                    owner.enabled = false;
                    Assert.That(volume.profile, Is.SameAs(original), "Disable must restore the original reference.");
                }
                finally
                {
                    Object.DestroyImmediate(target);
                    foreach (var component in original.components) Object.DestroyImmediate(component);
                    Object.DestroyImmediate(original);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(fallbackObject);
            foreach (var component in fallbackProfile.components) Object.DestroyImmediate(component);
            Object.DestroyImmediate(fallbackProfile);
        }
        yield return new ExitPlayMode();
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
        qualityButton.onClick.Invoke();
        var terrainButton = menuOptions.transform.Find("GraphicsOptions/TerrainRendering").GetComponent<Button>();
        Assert.That(terrainButton.interactable, Is.True);
        terrainButton.onClick.Invoke();
        Assert.That(GraphicsPreferences.ExperimentalTerrain, Is.True);
        Canvas.ForceUpdateCanvases();
        var terrainCorners = new Vector3[4];
        ((RectTransform)terrainButton.transform).GetWorldCorners(terrainCorners);
        var terrainRect = new Rect(terrainCorners[0], terrainCorners[2] - terrainCorners[0]);
        foreach (var button in menuOptions.GetComponentsInChildren<Button>())
        {
            if (button == terrainButton) continue;
            var corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            Assert.That(terrainRect.Overlaps(new Rect(corners[0], corners[2] - corners[0])), Is.False,
                "Terrain rendering control overlaps " + button.name);
        }
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
        float basemapDistance = terrain.basemapDistance;
        float error = terrain.heightmapPixelError, scale = owner.RuntimePipeline.renderScale, shadows = owner.RuntimePipeline.shadowDistance;
        float originalBloom = VolumeManager.instance.stack.GetComponent<Bloom>().intensity.value;
        var originalGrading = JsonUtility.ToJson(VolumeManager.instance.stack.GetComponent<ColorAdjustments>());
        var originalTonemapping = JsonUtility.ToJson(VolumeManager.instance.stack.GetComponent<Tonemapping>());
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
                    else
                    {
                        Assert.That(renderedPost, Is.True);
                        Assert.That(VolumeManager.instance.stack.GetComponent<Bloom>().intensity.value,
                            Is.EqualTo(quality == GraphicsQuality.Medium ? 0f : originalBloom).Within(.0001f));
                        Assert.That(JsonUtility.ToJson(VolumeManager.instance.stack.GetComponent<ColorAdjustments>()), Is.EqualTo(originalGrading));
                        Assert.That(JsonUtility.ToJson(VolumeManager.instance.stack.GetComponent<Tonemapping>()), Is.EqualTo(originalTonemapping));
                    }
                    if (!enabled && counter.Valid) Assert.That(counter.LastValue, Is.Zero, "Realtime shadow casters must be absent with Shadows OFF.");
                    File.AppendAllText("Logs/GraphicsSettings/effective.csv", $"{name},{quality},{enabled},{owner.RuntimePipeline.renderScale},{terrain.heightmapPixelError},{renderedFar},{renderedPost},{(counter.Valid ? counter.LastValue : -1)},{terrain.materialTemplate.shader.name}\n");
                    ScreenCapture.CaptureScreenshot($"Logs/GraphicsSettings/{name}-{quality}-{enabled}.png");
                    yield return Frames(3);
                }
            }
            Assert.That(terrain.materialTemplate, Is.SameAs(material));
            Assert.That(terrain.heightmapPixelError, Is.EqualTo(error));
            // The developer switch changes only material representation, never the selected preset.
            foreach (bool experimental in new[] { false, true, false, true })
            {
                GraphicsPreferences.Quality = GraphicsQuality.Medium;
                menu.OpenMenu(); menu.ShowOptions();
                var terrainButton = options.Find("TerrainRendering").GetComponent<Button>();
                Assert.That(terrainButton.gameObject.activeInHierarchy && terrainButton.interactable, Is.True);
                if (GraphicsPreferences.ExperimentalTerrain != experimental)
                    terrainButton.onClick.Invoke();
                Assert.That(GraphicsPreferences.ExperimentalTerrain, Is.EqualTo(experimental));
                menu.ResumeGame();
                foreach (var tier in new[] { GraphicsQuality.Medium, GraphicsQuality.High, GraphicsQuality.Low, GraphicsQuality.Medium })
                {
                    GraphicsPreferences.Quality = tier;
                    foreach (bool shadow in new[] { false, true })
                    {
                        GraphicsPreferences.Shadows = shadow;
                        yield return Frames(6);
                        var slot = owner.Configuration.GetExperimentalTerrain(material);
                        Assert.That(terrain.materialTemplate, Is.SameAs(slot != null ? slot.experimentalMedium : owner.Configuration.GetTerrainMaterial(material, tier)));
                        Assert.That(terrain.basemapDistance, Is.EqualTo(slot != null ? slot.experimentalBasemapDistance : basemapDistance));
                        var preset = owner.Configuration.Get(tier);
                        Assert.That(terrain.heightmapPixelError, Is.EqualTo(error * preset.terrainPixelErrorMultiplier));
                        Assert.That(owner.RuntimePipeline.renderScale, Is.EqualTo(scale * preset.renderScaleMultiplier).Within(.0001));
                        Assert.That(owner.RuntimePipeline.shadowDistance, Is.EqualTo(shadow ? shadows * preset.shadowDistanceMultiplier : 0).Within(.0001));
                        Assert.That(renderedPost, Is.EqualTo(tier != GraphicsQuality.Low));
                        Assert.That(ShaderUtil.ShaderHasError(terrain.materialTemplate.shader), Is.False);
                        if (tier == GraphicsQuality.Medium) Assert.That(VolumeManager.instance.stack.GetComponent<Bloom>().intensity.value, Is.Zero);
                        if (!shadow && counter.Valid) Assert.That(counter.LastValue, Is.Zero);
                    }
                }
                ScreenCapture.CaptureScreenshot($"Logs/GraphicsSettings/{name}-Distance-{experimental}.png");
                yield return Frames(4);
            }
            menu.OpenMenu(); menu.ShowOptions();
            yield return Frames(8);
            ScreenCapture.CaptureScreenshot($"Logs/GraphicsSettings/{name}-Distance-options.png");
            yield return Frames(4);
            menu.ResumeGame();
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
        Assert.That(PlayerPrefs.GetInt(GraphicsPreferences.QualityKey), Is.EqualTo((int)GraphicsQuality.Medium));
        Assert.That(PlayerPrefs.GetInt(GraphicsPreferences.ShadowsKey), Is.Zero);
        yield return Frames(6);
        Assert.That(VolumeManager.instance.stack.GetComponent<Bloom>().intensity.value, Is.Zero, "Continue must restore Medium's effective Bloom override.");
        Assert.That(owner.RuntimePipeline.shadowDistance, Is.Zero);
        Assert.That(GraphicsPreferences.ExperimentalTerrain, Is.True);
        var restoredTerrain = Object.FindAnyObjectByType<Terrain>();
        var restoredSlot = owner.Configuration.GetExperimentalTerrain(material);
        Assert.That(restoredTerrain.materialTemplate, Is.SameAs(restoredSlot != null ? restoredSlot.experimentalMedium : owner.Configuration.GetTerrainMaterial(material, GraphicsQuality.Medium)));
        Assert.That(restoredTerrain.basemapDistance, Is.EqualTo(restoredSlot != null ? restoredSlot.experimentalBasemapDistance : basemapDistance));
        Assert.That(ActiveRunController.Instance.GetComponent<PlayerInventory>().PlasmaCapsules, Is.EqualTo(9));
        Assert.That(ActiveRunController.Instance.GetComponent<PlayerInventory>().ArmorCapsules, Is.EqualTo(3));
        Assert.That(Object.FindObjectsByType<GraphicsSettingsController>().Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<ActiveRunController>().Length, Is.EqualTo(1));
        GraphicsPreferences.Quality = GraphicsQuality.High; GraphicsPreferences.Shadows = true;
        yield return Frames(4);
        Assert.That(restoredTerrain.materialTemplate, Is.SameAs(material));
        Assert.That(restoredTerrain.basemapDistance, Is.EqualTo(basemapDistance));
        GraphicsPreferences.ExperimentalTerrain = false;
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
