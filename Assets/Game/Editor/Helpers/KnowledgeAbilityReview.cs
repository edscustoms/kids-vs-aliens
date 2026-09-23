using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Batch Play Mode checks of the actual presenter; fixture-only progression/saves.
[InitializeOnLoad]
public static class KnowledgeAbilityReview
{
    private const string Key = "KnowledgeAbilityReview.Running";
    private static int step;
    private static double next, deadline, phaseStart;
    private static KnowledgeAbilityDemo.DemoPhase lastPhase;
    private static string inventory;
    private static Vector3 position;
    private static readonly System.Collections.Generic.HashSet<string> captures = new();
    private static T Find<T>() where T : Object => Object.FindAnyObjectByType<T>();
    private static KnowledgeAbilityDemo Demo => (KnowledgeAbilityDemo)typeof(SkillDemoPlayer)
        .GetField("abilityDemo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Find<SkillDemoPlayer>());
    static KnowledgeAbilityReview() { EditorApplication.playModeStateChanged += OnMode; }

    public static void Run()
    {
        GameplayPresentationSetup.CreateInitialAssets();
        GameplayPresentationSetup.CreateInitialAssets();
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/AbilityFixture-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
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
            step = 0; next = EditorApplication.timeSinceStartup + 3; deadline = next + 180;
            captures.Clear();
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
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
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static SkillData Skill(string id) => AssetDatabase.LoadAssetAtPath<SkillData>("Assets/Game/Data/Progression/" + id + ".asset");
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Ability review step " + step + ", phase " + Demo?.Phase);
            var presenter = Find<KnowledgeAcquiredPresenter>();
            if (presenter == null) return;
            if (step == 0 || step == 3)
            {
                if (step == 0)
                {
                    var item = AssetDatabase.LoadAssetAtPath<GrenadeItemData>("Assets/Game/Data/Items/Grenades/ElectricGrenade.asset");
                    for (int i = 0; i < 3; i++) Check(Find<PlayerInventory>().TryAddItem(item), "Fixture grenade add failed.");
                }
                Find<PlayerSkillState>().UnlockSkill(Skill(step == 0 ? "BeamHoist" : "GrenadeHandling"));
                step++; next = EditorApplication.timeSinceStartup + .2; return;
            }
            if (step == 1 || step == 4)
            {
                Check(Demo != null, "Ability loop not initialized.");
                inventory = JsonUtility.ToJson(Find<PlayerInventory>());
                position = Find<PlayerCharacter>().transform.position;
                lastPhase = Demo.Phase; phaseStart = EditorApplication.timeSinceStartup;
                step++; return;
            }
            if (step == 2 || step == 5)
            {
                var demo = Demo;
                Check(demo != null && Find<KnowledgePreviewStage>().IsRendering, "Preview stopped unexpectedly.");
                Check(Find<KnowledgePreviewStage>().Actor.name == Find<PlayerCharacter>().CurrentCharacterPrefab.name, "Selected visual mismatch.");
                Check(inventory == JsonUtility.ToJson(Find<PlayerInventory>()), "Real inventory changed during demonstration.");
                Check(position == Find<PlayerCharacter>().transform.position, "Real player moved during demonstration.");
                if (demo.Phase != lastPhase)
                {
                    double duration = EditorApplication.timeSinceStartup - phaseStart;
                    Debug.Log("ABILITY PHASE " + lastPhase + " -> " + demo.Phase + " after " + duration.ToString("F3") + "s");
                    if (lastPhase == KnowledgeAbilityDemo.DemoPhase.UpperPause) Check(duration >= .48, "Upper wait too short.");
                    if (lastPhase == KnowledgeAbilityDemo.DemoPhase.LowerPause) Check(duration >= .98, "Lower wait too short.");
                    if (lastPhase == KnowledgeAbilityDemo.DemoPhase.DistantWait) Check(duration >= 1.48, "Distant rest too short.");
                    if (demo.Phase == KnowledgeAbilityDemo.DemoPhase.LowerPause)
                        Check(Find<KnowledgePreviewStage>().Actor.transform.localPosition.sqrMagnitude < .00001f, "Beam drift.");
                    if (demo.Phase == KnowledgeAbilityDemo.DemoPhase.UpperPause || demo.Phase == KnowledgeAbilityDemo.DemoPhase.LowerPause)
                    {
                        var stage = Find<KnowledgePreviewStage>();
                        PreviewStageContent.TryGetModelBounds(stage.Actor, out var bounds);
                        Check(stage.PreviewCamera.WorldToViewportPoint(bounds.min).y > .02f
                            && stage.PreviewCamera.WorldToViewportPoint(bounds.max).y < .98f, "Beam endpoints clipped.");
                    }
                    lastPhase = demo.Phase; phaseStart = EditorApplication.timeSinceStartup;
                }
                string capture = "ability-" + presenter.CurrentSkill.Id + "-" + demo.Phase;
                bool ready = EditorApplication.timeSinceStartup - phaseStart >
                    (demo.Phase == KnowledgeAbilityDemo.DemoPhase.Flash || demo.Phase == KnowledgeAbilityDemo.DemoPhase.Flight ? .04 : .2);
                if (ready && captures.Add(capture)) ProceduralUIReview.Capture(capture, 1920, 1080);
                if (demo.CompletedCycles < 3) return;
                if (step == 5) Check(demo.ReleaseCount == 3, "Release marker duplicated or skipped.");
                Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b => b.name == "Acknowledge").onClick.Invoke();
                step++; next = EditorApplication.timeSinceStartup + .2; return;
            }
            // Repeated WATCH AGAIN / GOT IT while loops are in flight.
            if (step >= 6 && step < 14)
            {
                if (step % 2 == 0)
                {
                    Check(Demo == null, "Closed loop survived.");
                    Check(!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).name.StartsWith("Knowledge Grenade Physics", StringComparison.Ordinal)), "Physics scene leaked.");
                    Check(presenter.Review(Skill(step % 4 == 2 ? "GrenadeHandling" : "BeamHoist")), "Review failed.");
                    next = EditorApplication.timeSinceStartup + 1;
                }
                else
                {
                    Check(Demo != null && Demo.CompletedCycles == 0, "Reopened loop did not reset.");
                    presenter.Close();
                    next = EditorApplication.timeSinceStartup + .2;
                }
                step++; return;
            }
            if (step == 14)
            {
                Find<PlayerCharacter>().SetCharacter(AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/SportyGranny.prefab"));
                Check(presenter.Review(Skill("BeamHoist")), "Alternate character beam review failed.");
                step++; return;
            }
            if (step == 15 || step == 17)
            {
                Check(Demo != null && Find<KnowledgePreviewStage>().Actor.name == "SportyGranny", "Alternate character was not used.");
                if (Demo.CompletedCycles < 1) return;
                ProceduralUIReview.Capture("ability-granny-" + presenter.CurrentSkill.Id, 1920, 1080);
                presenter.Close(); step++; next = EditorApplication.timeSinceStartup + .2; return;
            }
            if (step == 16 || step == 18)
            {
                Check(presenter.Review(Skill("GrenadeHandling")), "Alternate character grenade review failed.");
                if (step == 18)
                {
                    // Force the existing CharacterChanged restart in the very
                    // same frame, while the previous physics scene is unloading.
                    Find<PlayerCharacter>().SetCharacter(Find<PlayerCharacter>().CurrentCharacterPrefab);
                    Check(Demo != null, "Same-frame character change broke the loop.");
                }
                step++; next = EditorApplication.timeSinceStartup + .9; return;
            }
            if (step == 19)
            {
                Find<SkillDemoPlayer>().enabled = false;
                Check(Demo == null, "Disable did not dispose the active demonstration.");
                presenter.Close();
                Find<SkillDemoPlayer>().enabled = true;
                step++; next = EditorApplication.timeSinceStartup + .2; return;
            }
            Check(Demo == null, "Final loop survived close.");
            Check(Find<KnowledgePreviewStage>().StagingRoot.GetComponentsInChildren<BeamTransportVFX>(true).Length == 0, "Beam leaked.");
            Check(Object.FindObjectsByType<GrenadeInstance>(FindObjectsInactive.Include).Length == 0, "Grenade leaked.");
            Check(!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).name.StartsWith("Knowledge Grenade Physics", StringComparison.Ordinal)), "Final physics scene leaked.");
            Debug.Log("KNOWLEDGE ABILITY REVIEW PASSED: three cycles each, authored release, deterministic beam return, Amy and Granny, unchanged inventory/player, repeated Review/Close, disable and cleanup.");
        }
        catch (Exception error)
        {
            SessionState.SetBool(Key + "Failed", true);
            ProceduralUIReview.Capture("ability-failure", 1920, 1080);
            Debug.LogException(error);
        }
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        EditorApplication.ExitPlaymode();
    }
}
