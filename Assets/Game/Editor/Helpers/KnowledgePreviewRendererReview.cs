using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Review the saved production scene, without running setup in the Play Mode fixture.
[InitializeOnLoad]
public static class KnowledgePreviewRendererReview
{
    private const string Key = "KnowledgePreviewRendererReview";
    private static readonly string[] Skills = { "RifleHandling", "UnarmedCombat", "GrenadeHandling", "BeamHoist", "PistolHandling" };
    private static int index, capture;
    private static double next, deadline;
    static KnowledgePreviewRendererReview()
    {
        EditorApplication.playModeStateChanged += Mode;
        EditorApplication.update += FinishAfterReload;
    }
    public static void CreateRenderer() => GameplayPresentationSetup.EnsurePreviewRenderer();
    public static void Run()
    {
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/KnowledgeRenderer/Save-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory("Logs/KnowledgeRenderer");
        SessionState.SetBool(Key, true); SessionState.SetBool(Key + "Failed", false);
        SessionState.SetBool(Key + "Done", false);
        SessionState.SetInt(Key + "Quality", QualitySettings.GetQualityLevel());
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        typeof(Editor).Assembly.GetType("UnityEditor.LogEntries")?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        EditorApplication.EnterPlaymode();
    }
    private static void Mode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false) || state != PlayModeStateChange.EnteredPlayMode) return;
        index = capture = 0; next = EditorApplication.timeSinceStartup + 4; deadline = next + 120;
        Application.runInBackground = true;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }
    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || message.Contains("Knowledge preview needs"))
            SessionState.SetBool(Key + "Failed", true);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            Check(EditorApplication.timeSinceStartup < deadline, "Preview review timed out.");
            var presenter = Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>();
            var stage = Object.FindAnyObjectByType<KnowledgePreviewStage>();
            var player = Object.FindAnyObjectByType<PlayerCharacter>();
            if (presenter == null || player == null || player.CurrentCharacterPrefab == null) return;
            if (capture == 0)
            {
                // Complete the normal arrival and foreground resume before opening the first modal.
                if (index == 0)
                {
                    var run = ActiveRunController.Instance;
                    if (run != null) { run.SendMessage("OnApplicationPause", false); run.SendMessage("OnApplicationFocus", true); }
                    var menu = Object.FindAnyObjectByType<InGameMenuController>();
                    if (menu != null && menu.IsOpen) menu.ResumeGame();
                    if (Object.FindAnyObjectByType<BeamTransportController>().IsTransporting) return;
                }
                QualitySettings.SetQualityLevel(0, true);
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Game/Data/Progression/" + Skills[index] + ".asset");
                var skills = Object.FindAnyObjectByType<PlayerSkillState>();
                if (skills.HasSkill(skill)) Check(presenter.Review(skill), "Review did not open.");
                else skills.UnlockSkill(skill);
                capture = 1; next = EditorApplication.timeSinceStartup + 1; return;
            }
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Check(pipeline != null && pipeline.name == "Mobile_RPAsset", "Wrong active pipeline: " + pipeline?.name);
            Check(presenter.CurrentSkill == AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Game/Data/Progression/" + Skills[index] + ".asset"), "Wrong demonstration opened.");
            Check(stage.IsRendering && stage.Actor != null, "Preview is blank: " + Skills[index]);
            Check(stage.Actor.name == player.CurrentCharacterPrefab.name, "Preview must use selected character.");
            int rendererIndex = new SerializedObject(stage.PreviewCamera.GetUniversalAdditionalCameraData()).FindProperty("m_RendererIndex").intValue;
            Check(pipeline.rendererDataList[rendererIndex].rendererFeatures.Count == 0, "Preview has gameplay features.");
            Check(pipeline.rendererDataList[0].rendererFeatures.OfType<CameraOcclusionSilhouetteFeature>().Any(), "Gameplay silhouette feature missing.");
            var target = stage.PreviewCamera.targetTexture;
            Check(target != null && target.IsCreated(), "Preview texture missing.");
            RenderPipeline.SubmitRenderRequest(stage.PreviewCamera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            string name = "mobile-" + Skills[index] + "-" + capture;
            SaveTexture(target, name);
            ProceduralUIReview.Capture("renderer-" + name, 1280, 720);
            Debug.Log($"KNOWLEDGE RENDERER REVIEW {name}: {stage.Actor.name}, {pipeline.name}, renderer={rendererIndex}, texture={target.width}x{target.height}");
            if (++capture <= 3) { next = EditorApplication.timeSinceStartup + 2; return; }
            presenter.Close(); capture = 0;
            if (++index < Skills.Length) { next = EditorApplication.timeSinceStartup + .3; return; }
            Debug.Log("KNOWLEDGE RENDERER REVIEW PASSED: five Mobile demonstrations, selected character, rendered textures, gameplay silhouettes retained.");
        }
        catch (Exception e) { SessionState.SetBool(Key + "Failed", true); Debug.LogException(e); }
        Application.logMessageReceived -= OnLog; EditorApplication.update -= Tick;
        SessionState.SetBool(Key + "Done", true); EditorApplication.ExitPlaymode();
    }
    private static void SaveTexture(RenderTexture target, string name)
    {
        var previous = RenderTexture.active; var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            File.WriteAllBytes("Logs/KnowledgeRenderer/" + name + ".png", texture.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); }
    }
    private static void FinishAfterReload()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !SessionState.GetBool(Key + "Done", false)) return;
        SessionState.SetBool(Key + "Done", false); SessionState.SetBool(Key, false);
        QualitySettings.SetQualityLevel(SessionState.GetInt(Key + "Quality", 1), true);
        EditorApplication.Exit(SessionState.GetBool(Key + "Failed", false) ? 1 : 0);
    }
}
