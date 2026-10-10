using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class GraphicsPipelineLifetimeTests
{
    [UnitySetUp]
    public IEnumerator Setup()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        SessionState.SetInt("GraphicsLifetime.quality", QualitySettings.GetQualityLevel());
        SessionState.SetInt("GraphicsLifetime.preset", (int)GraphicsPreferences.Quality);
        SessionState.SetBool("GraphicsLifetime.shadows", GraphicsPreferences.Shadows);
        SessionState.SetBool("GraphicsLifetime.optionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt("GraphicsLifetime.options", (int)EditorSettings.enterPlayModeOptions);
        for (int i = 0; i < QualitySettings.names.Length; i++)
            SessionState.SetString("GraphicsLifetime.asset." + i, AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i)));
        CoreBuildData.instance.Dispose();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTest]
    public IEnumerator ScriptReloadWhilePlayingRecreatesOneOwner()
    {
        yield return new EnterPlayMode();
        GraphicsPreferences.Quality = GraphicsQuality.Medium;
        GraphicsPreferences.Shadows = false;
        EditorUtility.RequestScriptReload();
        yield return new ReloadWhilePlaying();
        Assert.That(GraphicsSettingsController.Instance != null, Is.True);
        Assert.That(GraphicsSettingsController.Instance.RuntimePipeline != null, Is.True);
        Assert.That(GraphicsSettingsController.Instance.RuntimePipeline.shadowDistance, Is.Zero);
        Assert.That(Object.FindObjectsByType<GraphicsSettingsController>().Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<DeveloperFpsHud>().Length, Is.EqualTo(1));
        yield return new ExitPlayMode();
        AssertBuildReferences();
    }

    sealed class ReloadWhilePlaying : IEditModeTestYieldInstruction
    {
        public bool ExpectDomainReload => true;
        public bool ExpectedPlaymodeState => true;
        public IEnumerator Perform() => new WaitForDomainReload().Perform();
    }

    [UnityTest]
    public IEnumerator PlayExitRestoresAllSlotsAndBuildAssets()
    {
        yield return new EnterPlayMode();
        ExercisePresets();
        yield return new ExitPlayMode();
        AssertBuildReferences();
    }

    [UnityTest]
    public IEnumerator DifferentActiveQualityDoesNotStrandClone()
    {
        yield return new EnterPlayMode();
        ExercisePresets();
        int assigned = QualitySettings.GetQualityLevel();
        QualitySettings.SetQualityLevel((assigned + 1) % QualitySettings.names.Length, false);
        Object.Destroy(GraphicsSettingsController.Instance.gameObject);
        yield return EditorTestFrame.Next();
        yield return EditorTestFrame.Next();
        AssertBuildReferences();
        yield return new ExitPlayMode();
        AssertBuildReferences();
    }

    [UnityTest]
    public IEnumerator CachedBuildAssetsCannotRetainDestroyedClone()
    {
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        yield return new EnterPlayMode(false);
        ExercisePresets();
        CoreBuildData.instance.Dispose();
        Assert.That(CoreBuildData.instance.renderPipelineAssets, Does.Contain(GraphicsSettingsController.Instance.RuntimePipeline));
        yield return new ExitPlayMode();
        AssertBuildReferences();
        yield return new EnterPlayMode(false);
        ExercisePresets();
        Assert.That(Object.FindObjectsByType<GraphicsSettingsController>().Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<DeveloperFpsHud>().Length, Is.EqualTo(1));
        yield return new ExitPlayMode();
        AssertBuildReferences();
    }

    static void ExercisePresets()
    {
        Assert.That(GraphicsSettingsController.Instance, Is.Not.Null);
        foreach (var quality in new[] { GraphicsQuality.Low, GraphicsQuality.Medium, GraphicsQuality.High })
        {
            GraphicsPreferences.Quality = quality;
            GraphicsPreferences.Shadows = false;
            Assert.That(GraphicsSettingsController.Instance.RuntimePipeline.shadowDistance, Is.Zero);
            GraphicsPreferences.Shadows = true;
            Assert.That(GraphicsSettingsController.Instance.RuntimePipeline.shadowDistance, Is.GreaterThan(0));
        }
    }

    static void AssertBuildReferences()
    {
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            var expected = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString("GraphicsLifetime.asset." + i, ""));
            Assert.That(QualitySettings.GetRenderPipelineAssetAt(i), Is.SameAs(expected), "Quality slot " + i);
        }
        // This is the cached list URPPreprocessBuild consumes, not just the active quality slot.
        foreach (var asset in CoreBuildData.instance.renderPipelineAssets)
        {
            Assert.That(asset != null, Is.True, "Destroyed pipeline in CoreBuildData");
            Assert.DoesNotThrow(() => { var name = asset.name; });
            Assert.That(EditorUtility.IsPersistent(asset), Is.True, "Runtime clone in build inputs");
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString("GraphicsLifetime.asset." + i, ""));
        }
        QualitySettings.SetQualityLevel(SessionState.GetInt("GraphicsLifetime.quality", 0), false);
        GraphicsPreferences.Quality = (GraphicsQuality)SessionState.GetInt("GraphicsLifetime.preset", 2);
        GraphicsPreferences.Shadows = SessionState.GetBool("GraphicsLifetime.shadows", true);
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool("GraphicsLifetime.optionsEnabled", true);
        EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt("GraphicsLifetime.options", 0);
        CoreBuildData.instance.Dispose();
    }
}
