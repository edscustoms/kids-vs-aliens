using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Floor-only Play Mode review. No scene/asset saves or inventory fixture mutations.
[InitializeOnLoad]
public static class KnowledgeFloorReview
{
    private const string Key = "KnowledgeFloorReview.Running";
    private static readonly string[] Skills = { "BeamHoist", "UnarmedCombat", "PistolHandling", "RifleHandling", "GrenadeHandling" };
    private static int step;
    private static double next, deadline;
    static KnowledgeFloorReview() { EditorApplication.playModeStateChanged += OnMode; }
    public static void Run()
    {
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/FloorFixture-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + "Failed", false);
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        EditorApplication.EnterPlaymode();
    }
    private static void OnMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            step = 0; next = EditorApplication.timeSinceStartup + 2; deadline = next + 100;
            EditorApplication.update += Tick; Application.logMessageReceived += OnLog;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorApplication.delayCall += () => EditorApplication.Exit(SessionState.GetBool(Key + "Failed", false) ? 1 : 0);
        }
    }
    private static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) SessionState.SetBool(Key + "Failed", true);
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 2;
        try
        {
            if (next > deadline) throw new TimeoutException("Knowledge floor review timed out at step " + step);
            // Preview presentation can be reviewed independently of run/death/arrival state.
            if (UnityEngine.Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>() == null) return;
            if (step < Skills.Length * 2)
            {
                string skill = Skills[step / 2];
                if (step % 2 == 0)
                    UnityEngine.Object.FindAnyObjectByType<PlayerSkillState>().UnlockSkill(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Game/Data/Progression/" + skill + ".asset"));
                else
                {
                    var stage = UnityEngine.Object.FindAnyObjectByType<KnowledgePreviewStage>();
                    if (stage == null || !stage.IsRendering) throw new InvalidOperationException("Stage not rendering: " + skill);
                    ProceduralUIReview.Capture("floor-" + skill, 1920, 1080);
                    if (skill == "RifleHandling") ProceduralUIReview.Capture("floor-RifleHandling-720", 1280, 720);
                    UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b => b.name == "Acknowledge").onClick.Invoke();
                }
                step++;
                return;
            }
            foreach (string name in new[] { "UI/Knowledge Stage Backdrop", "Presentation/Tutorial Floor" })
                if (ShaderUtil.GetShaderMessages(Shader.Find(name)).Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                    throw new InvalidOperationException("Shader compilation failed: " + name);
            Debug.Log("KNOWLEDGE FLOOR REVIEW PASSED: five Play Mode demonstrations captured; shaders compiled; acknowledgement callbacks completed.");
        }
        catch (Exception error) { SessionState.SetBool(Key + "Failed", true); Debug.LogException(error); }
        EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
        EditorApplication.ExitPlaymode();
    }
}
