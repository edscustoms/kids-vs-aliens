using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One authored, forward scene handoff. The live run/player rig remains the owner.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(GameplaySceneEntry))]
public sealed class SceneCrossover : MonoBehaviour
{
    [SerializeField] private string destinationScene;
    [SerializeField] private BoxCollider handoffArea;
    [SerializeField] private Vector3 atmosphereStart, atmosphereEnd;
    public static SceneCrossover Pending { get; private set; }
    public GameplaySceneEntry Source { get; private set; }
    public GameplaySceneEntry Destination { get; private set; }
    public string DestinationScene => destinationScene;
    public bool Ready => Destination != null && Destination.IsPrepared;
    public bool Committed { get; private set; }
    private AsyncOperation loading;
    private Material workingSky;
    private bool cancelled;
    private ThreadPriority previousPriority;
    private bool ownsPriority;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => Pending = null;

    private IEnumerator Start()
    {
        Source = GetComponent<GameplaySceneEntry>();
        while (!Source.Run.IsReady) yield return null;
        if (Pending != null || !Application.CanStreamedLevelBeLoaded(destinationScene))
        { Debug.LogError("Crossover destination is unavailable or another handoff is pending.", this); yield break; }
        Pending = this;
        previousPriority = Application.backgroundLoadingPriority;
        ownsPriority = true; Application.backgroundLoadingPriority = ThreadPriority.Low;
        loading = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Additive);
        while (!loading.isDone) yield return null;
        if (cancelled) yield break;
        if (Destination == null)
        { Debug.LogError("The incoming scene has no configured GameplaySceneEntry.", this); yield break; }
        yield return Destination.Prepare();
        RestorePriority();
        workingSky = new Material(Source.lighting.sky) { name = "Crossover sky (owned)" };
    }

    public void AcceptDestination(GameplaySceneEntry entry) => Destination = entry;

    private void Update()
    {
        if (!Ready || cancelled || Committed || !Source.Run.IsReady || workingSky == null) return;
        var position = Source.player.transform.position;
        Vector3 axis = atmosphereEnd - atmosphereStart;
        float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Vector3.Dot(position - atmosphereStart, axis) / axis.sqrMagnitude));
        if (t > 0)
        {
            SceneLightingState.Blend(Source.lighting, Destination.lighting, t, workingSky, Source.keyLight);
            foreach (var volume in Source.volumes) volume.weight = 1 - t;
            foreach (var volume in Destination.volumes) volume.weight = t;
        }
        if (Source.player.Bike != null || !handoffArea.bounds.Contains(position + Vector3.up * .8f)) return;
        var sourceScene = gameObject.scene;
        Source.Run.HandoffToScene(Destination.gameObject.scene, Source.carriedRoots);
        Destination.Enter(Source);
        Committed = true; Pending = null;
        SceneManager.UnloadSceneAsync(sourceScene);
    }

    public static void CancelPending(ActiveRunController run)
    {
        var pending = Pending;
        if (pending == null || pending.Source == null || pending.Source.Run != run) return;
        pending.cancelled = true;
        // Single-scene loading owns teardown. Never leave Unity's async queue held at .9.
        if (pending.loading != null) pending.loading.allowSceneActivation = true;
        pending.RestorePriority();
        // Keep the startup gate through any forced activation before the Single load.
    }

    private void RestorePriority()
    {
        if (!ownsPriority) return;
        Application.backgroundLoadingPriority = previousPriority; ownsPriority = false;
    }
    private void OnDestroy()
    {
        if (Pending == this) Pending = null;
        RestorePriority();
        if (workingSky != null) Destroy(workingSky);
    }
}
