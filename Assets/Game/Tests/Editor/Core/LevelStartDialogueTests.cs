using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class LevelStartDialogueTests
{
    private const string Key = "LevelStartDialogueTests.Saves";
    private const string MessagePath = "Assets/Game/Dialogue/ConstructionSite/CS_Intro_WhereAmI.asset";
    private static ActiveRunController Run => ActiveRunController.Instance;
    private static DialoguePlayer Dialogue => DialoguePlayer.Instance;
    private static BeamTransportController Transport => Run.GetComponent<BeamTransportController>();

    [UnitySetUp] public IEnumerator Setup()
    {
        SessionState.SetString(Key,Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY")??"");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",System.IO.Path.GetFullPath("Logs/OpeningCC-"+Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode();
        Application.runInBackground=true;
        yield return Ready(); Resume();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(Application.isPlaying) { Run?.PrepareToLeave(); yield return new ExitPlayMode(); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",SessionState.GetString(Key,""));
        SessionState.EraseString(Key); Time.timeScale=1;
    }

    [UnityTest] public IEnumerator FreshArrivalSpeaksAfterHandoffContinueDoesNotReplayAndRestartSpeaksAgain()
    {
        yield return FinishArrival();
        AssertOpening();
        yield return Continue();
        Assert.That(Dialogue.CurrentMessage,Is.Null);
        Assert.That(Run.GetComponent<GameplaySuspensionController>().IsSuspended,Is.True,"Continue retains the normal paused flow");
        Resume(); yield return Seconds(4);
        Assert.That(Dialogue.CurrentMessage,Is.Null,"Continue never replays opening speech");
        Assert.That(Run.RestartFromBeginning(),Is.True);
        yield return EditorTestFrame.Next(); yield return Ready(); Resume();
        yield return FinishArrival(); AssertOpening();
    }

    [UnityTest] public IEnumerator CancelledOrSavedMidArrivalDoesNotSpeakOnContinue()
    {
        Assert.That(Transport.IsTransporting,Is.True);
        Assert.That(Dialogue.CurrentMessage,Is.Null);
        yield return Continue(); Resume(); yield return Seconds(4);
        Assert.That(Dialogue.CurrentMessage,Is.Null);
        Assert.That(Run.RestartFromBeginning(),Is.True);
        yield return EditorTestFrame.Next(); yield return Ready(); Resume();
        Transport.CancelTransport(); yield return Seconds(.2f);
        Assert.That(Dialogue.CurrentMessage,Is.Null,"Cancellation before reaching LevelStart is not a completed arrival");
    }

    private static void AssertOpening()
    {
        Assert.That(Dialogue.CurrentMessage,Is.SameAs(AssetDatabase.LoadAssetAtPath<DialogueMessage>(MessagePath)));
        Assert.That(Dialogue.CurrentLine.text,Is.EqualTo("Where am I?"));
        Assert.That(Dialogue.CurrentLine.voice,Is.Null);
        Assert.That(Dialogue.Speaker,Is.SameAs(Run.GetComponent<PlayerCharacter>().ActiveVisual.DialogueIdentity));
        Assert.That(Dialogue.Speaker.displayName.ToUpperInvariant(),Is.EqualTo("AMY"));
        Assert.That(Dialogue.Speaker.voiceType,Is.EqualTo(DialogueVoiceType.Girl));
        Assert.That(Run.GetComponent<GameplaySuspensionController>().OwnerCount,Is.Zero);
        Assert.That(Run.GetComponent<StarterAssets.StarterAssetsInputs>().CanProcessGameplayInput,Is.True);
        Assert.That(Run.GetComponent<StarterAssets.ThirdPersonController>().enabled,Is.True);
        Assert.That(ObjectiveController.Instance.ActiveObjective.title.ToUpperInvariant(),Is.EqualTo("FIND A WAY OUT"));
        Assert.That(Object.FindAnyObjectByType<AuthoredBeamArrival>().State,Is.EqualTo(AuthoredArrivalState.Available));
    }
    private static IEnumerator FinishArrival()
    {
        float end=Time.time+8;
        while(Transport.IsTransporting)
        {
            Assert.That(Time.time,Is.LessThan(end));
            Assert.That(Dialogue.CurrentMessage,Is.Null,"Wait for control handoff, not just the landing frame");
            yield return EditorTestFrame.Next();
        }
        // The existing input owner intentionally guards the release frame.
        yield return EditorTestFrame.Next();
    }
    private static void Resume() { Run.SendMessage("OnApplicationPause",false); Run.SendMessage("OnApplicationFocus",true); Object.FindAnyObjectByType<InGameMenuController>().ResumeGame(); }
    private static IEnumerator Ready() { double end=EditorApplication.timeSinceStartup+30;while(Run==null||!Run.IsReady){Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(end),Run?.RestoreError);yield return EditorTestFrame.Next();}yield return EditorTestFrame.Next(); }
    private static IEnumerator Continue() { Assert.That(Run.Save(),Is.True);Run.PrepareToLeave();Assert.That(RunSaveService.Continue(),Is.True);yield return EditorTestFrame.Next();yield return Ready(); }
    private static IEnumerator Seconds(float duration) { float end=Time.time+duration; double deadline=EditorApplication.timeSinceStartup+duration+20;while(Time.time<end){Assert.That(EditorApplication.timeSinceStartup,Is.LessThan(deadline));yield return EditorTestFrame.Next();} }
}
