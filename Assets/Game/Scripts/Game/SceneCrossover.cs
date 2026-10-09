using System;
using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum CrossoverPhase { Idle, Departing, Loading, Arriving, Complete }

/// <summary>Authored one-shot action: existing Beam, loading presentation and run handoff.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(GameplaySceneEntry))]
public sealed class SceneCrossover : MonoBehaviour, IGameplayAction
{
    [SerializeField] private string destinationScene;
    [SerializeField] private string loadingScene = "Loading";
    [SerializeField] private ExcavatorRepairMission exitMission;
    [SerializeField] private Transform departureRecoveryPoint;
    [Header("Existing loading screen presentation")]
    [SerializeField] private string destinationTitle = "BIKE ROUTE";
    [SerializeField] private string loadingText = "LOADING BIKE ROUTE";
    [SerializeField] private Sprite loadingArtwork;
    [SerializeField, Min(0f)] private float minimumLoadingDisplaySeconds = 2.5f;
    [SerializeField, Min(0f)] private float loadingFadeOutSeconds = .35f;
    [Header("Existing Beam presentation")]
    [SerializeField, Min(.1f)] private float beamHeight = 3.5f;
    [SerializeField, Min(.1f)] private float departureDuration = 1.5f;
    [SerializeField, Min(.1f)] private float arrivalDuration = 2f;
    [SerializeField] private DialogueMessage arrivalDialogue;
    public static SceneCrossover Pending { get; private set; }
    public GameplaySceneEntry Source { get; private set; }
    public GameplaySceneEntry Destination { get; private set; }
    public string DestinationScene => destinationScene;
    public bool Ready => loading != null && loading.isDone && Destination != null && !cancelled;
    public bool Transitioning => Phase != CrossoverPhase.Idle && Phase != CrossoverPhase.Complete;
    public bool Committed { get; private set; }
    public CrossoverPhase Phase { get; private set; }
    public string Failure { get; private set; }
    private AsyncOperation loading;
    private LoadingSceneController loadingView;
    private BeamTransportController beam;
    private bool cancelled, departed;
    private ThreadPriority previousPriority;
    private bool ownsPriority;
    private GameplaySuspensionController.Lease preparationLease;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => Pending = null;

    private IEnumerator Start()
    {
        Source = GetComponent<GameplaySceneEntry>();
        while (!Source.Run.IsReady) yield return null;
        if (Pending != null || !Application.CanStreamedLevelBeLoaded(destinationScene)
            || !Application.CanStreamedLevelBeLoaded(loadingScene))
        { Fail("Transport destination is unavailable. Your current run is unchanged."); yield break; }
        Pending = this;
        previousPriority = Application.backgroundLoadingPriority;
        ownsPriority = true; Application.backgroundLoadingPriority = ThreadPriority.Low;
        LoadingSceneController.PresentationOnlyRequested = true;
        var presentationLoad = SceneManager.LoadSceneAsync(loadingScene, LoadSceneMode.Additive);
        yield return presentationLoad;
        if (cancelled) yield break;
        loadingView = LoadingSceneController.Presentation;
        if (loadingView == null) { Fail("The existing loading presentation is unavailable."); yield break; }
        loadingView.ConfigurePresentation(destinationTitle, loadingText, loadingArtwork);
        try { loading = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Additive); }
        catch (Exception error) { Fail(error.Message); }
        if (loading == null) { RestorePriority(); yield break; }
        yield return loading;
        RestorePriority();
        if (!cancelled && (Destination == null || Destination.arrivalPoint == null))
            Fail("The next level has no configured arrival point. Your current run is unchanged.");
    }

    public void AcceptDestination(GameplaySceneEntry entry) => Destination = entry;
    public bool TryExecute(PlayerCharacter player)
    {
        if (cancelled || Transitioning || Committed || Source == null || !Source.Run.IsReady
            || player != Source.player.GetComponent<PlayerCharacter>() || loading == null || loadingView == null
            || departureRecoveryPoint == null || Source.player.Bike != null) return false;
        if (exitMission != null && exitMission.State != ExcavatorRepairState.Completed) return false;
        beam = player.GetComponent<BeamTransportController>();
        if (beam == null || !beam.CanBeginTransport || !Source.Run.TryReservePlayerSavePoint(this, departureRecoveryPoint)) return false;
        if (!Source.Run.Save()) { Source.Run.ClearPlayerSavePoint(this); Fail("Transport stopped: the current run could not be saved."); return false; }
        if (!beam.TryDeparture(beamHeight, departureDuration, () => { departed = true; loadingView.ShowPresentation(true); }))
        { Source.Run.ClearPlayerSavePoint(this); return false; }
        Phase = CrossoverPhase.Departing;
        StartCoroutine(Transfer());
        return true;
    }
    private void LateUpdate()
    {
        if (Phase == CrossoverPhase.Loading && loadingView != null)
            loadingView.SetActualProgress(Ready ? .5f + .5f * Destination.PreparationProgress
                : loading == null ? 0 : .5f * Mathf.Clamp01(loading.progress / .9f));
    }
    private IEnumerator Transfer()
    {
        while (!departed && beam.IsTransporting && !cancelled) yield return null;
        if (cancelled) yield break;
        if (!departed) { Source.Run.ClearPlayerSavePoint(this); Fail("Beam departure was interrupted; Continue can retry from the safe passage."); yield break; }
        Phase = CrossoverPhase.Loading;
        var suspension = Source.player.GetComponent<GameplaySuspensionController>();
        preparationLease = suspension.Acquire(SuspensionReason.Modal);
        // The existing art/canvas covers all destination preparation and the ownership switch.
        yield return null;
        double loadingShownAt = Time.realtimeSinceStartupAsDouble;
        while (!Ready && !cancelled) yield return null;
        if (cancelled) yield break;
        Exception failure = null;
        var stack = new System.Collections.Generic.Stack<IEnumerator>();
        stack.Push(Destination.Prepare());
        while (stack.Count > 0 && !cancelled && failure == null)
        {
            object current = null; bool more = false;
            try { more = stack.Peek().MoveNext(); if (more) current = stack.Peek().Current; }
            catch (Exception error) { failure = error; }
            if (failure != null) break;
            if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (current is IEnumerator nested) stack.Push(nested); else yield return current;
        }
        // Keep the prepared, overlapping worlds covered during a lifecycle pause.
        // Our Modal lease already prevents opening a new manual menu here.
        // Hold the real completed bar if preparation finishes before the configured minimum.
        while (!cancelled && (suspension.OwnerCount > 2
            || Time.realtimeSinceStartupAsDouble - loadingShownAt < minimumLoadingDisplaySeconds))
            yield return null;
        if (cancelled) yield break;
        loadingView.ShowPresentation(true);
        loadingView.SetActualProgress(.5f + .5f * Destination.PreparationProgress);
        yield return null;
        var sourceScene = gameObject.scene;
        if (failure == null)
        {
            try
            {
                var entry = Destination.arrivalPoint;
                var sample = Destination.bikeChase.Guide.Project(entry.position);
                if (!Destination.IsPrepared || Vector3.ProjectOnPlane(entry.position - sample.position, Vector3.up).magnitude > sample.halfWidth
                    || !beam.IsSegmentClear(entry.position + Vector3.up * beamHeight, entry.position))
                    throw new InvalidOperationException("The authored arrival must have a clear Beam descent onto the road.");
                var rig = Source.cameraController.GetComponent<CinemachineVirtualCamera>();
                Vector3 oldPosition = Source.player.transform.position;
                Source.Run.ClearPlayerSavePoint(this);
                Source.Run.HandoffToScene(Destination.gameObject.scene, Source.carriedRoots, Source.sceneOwnedChildren);
                Committed = true;
                Destination.Enter(Source, false);
                beam.CancelTransport();
                preparationLease.Dispose(); preparationLease = null;
                // Reserve the supported endpoint for saves during the transient descent.
                Source.Run.TryReservePlayerSavePoint(this, entry);
                if (!beam.TryArrival(entry, null, beamHeight, arrivalDuration, .15f, .25f))
                    throw new InvalidOperationException("Beam arrival could not start.");
                rig.OnTargetObjectWarped(rig.Follow, Source.player.transform.position - oldPosition);
                rig.PreviousStateIsValid = false;
                Physics.SyncTransforms();
                Source.Run.Save();
                Phase = CrossoverPhase.Arriving;
            }
            catch (Exception error) { failure = error; }
        }
        if (failure != null)
        {
            if (!Committed)
            {
                foreach (var root in Destination.stagedRoots) root.SetActive(false);
                Source.lighting.Apply();
                beam.CancelTransport();
                var capsule = Source.player.GetComponent<CharacterController>(); capsule.enabled = false;
                Source.player.transform.SetPositionAndRotation(departureRecoveryPoint.position, departureRecoveryPoint.rotation); capsule.enabled = true;
            }
            preparationLease?.Dispose(); preparationLease = null;
            Source.Run.ClearPlayerSavePoint(this);
            loadingView.ShowPresentation(false);
            Fail("Transport stopped: " + failure.Message);
            yield break;
        }
        // First Beam In frame is prepared while the original loading artwork still covers it.
        yield return null;
        yield return loadingView.FadeOutPresentation(loadingFadeOutSeconds);
        while (beam.IsTransporting && !cancelled) yield return null;
        if (cancelled) yield break;
        Source.Run.ClearPlayerSavePoint(this);
        Destination.ActivateEntryGameplay();
        Source.Run.Save();
        DialoguePlayer.Instance?.Play(arrivalDialogue, Source.player.GetComponent<PlayerCharacter>().ActiveVisual?.DialogueIdentity);
        Phase = CrossoverPhase.Complete; Pending = null;
        yield return SceneManager.UnloadSceneAsync(loadingScene);
        SceneManager.UnloadSceneAsync(sourceScene);
    }
    private void Fail(string message)
    {
        Failure = message; cancelled = true; RestorePriority();
        RunSaveService.Notify(message);
    }
    public static void CancelPending(ActiveRunController run)
    {
        var pending = Pending;
        if (pending == null || pending.Source == null || pending.Source.Run != run) return;
        pending.cancelled = true;
        if (pending.loading != null) pending.loading.allowSceneActivation = true;
        pending.RestorePriority();
    }
    private void RestorePriority()
    {
        if (!ownsPriority) return;
        Application.backgroundLoadingPriority = previousPriority; ownsPriority = false;
    }
    private void OnDestroy()
    {
        if (Pending == this) Pending = null;
        LoadingSceneController.PresentationOnlyRequested = false;
        RestorePriority(); preparationLease?.Dispose();
        if (Source != null && Source.Run != null) Source.Run.ClearPlayerSavePoint(this);
    }
}
