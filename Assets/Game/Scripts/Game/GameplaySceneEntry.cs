using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Explicit authored entry wiring. Standalone scenes keep their normal startup.</summary>
[DefaultExecutionOrder(-31000), DisallowMultipleComponent]
public sealed class GameplaySceneEntry : MonoBehaviour
{
    public GameObject[] carriedRoots = Array.Empty<GameObject>();
    [Tooltip("Level-owned children of carried roots; retain them in the outgoing scene.")]
    public GameObject[] sceneOwnedChildren = Array.Empty<GameObject>();
    public PlayerBikeRider player;
    public Camera gameplayCamera;
    public GameplayCameraController cameraController;
    public CameraOcclusionController occlusion;
    public Light keyLight;
    public Volume[] volumes = Array.Empty<Volume>();
    public SceneLightingState lighting;
    [Tooltip("Active authored groups, prepared over several frames during additive entry.")]
    public GameObject[] warmupRoots = Array.Empty<GameObject>();
    [Tooltip("Incoming environment stays inactive until the loading screen covers preparation.")]
    public GameObject[] stagedRoots = Array.Empty<GameObject>();
    public Transform arrivalPoint;
    public GameObject[] entryGameplay = Array.Empty<GameObject>();
    public BikeRouteChaseDirector bikeChase;
    public AlienBikeLaserWeapon[] bikeCameraBindings = Array.Empty<AlienBikeLaserWeapon>();
    public bool IsPrepared { get; private set; }
    public float PreparationProgress { get; private set; }
    public bool IsIncoming { get; private set; }
    public ActiveRunController Run => player != null ? player.GetComponent<ActiveRunController>() : null;
    private GameObject arrivalGameplayGate;

    private void Awake()
    {
        var crossover = SceneCrossover.Pending;
        if (crossover == null || crossover.DestinationScene != gameObject.scene.name) return;
        IsIncoming = true;
        // Execution order precedes player/input/audio Awake. Never initialize a duplicate
        // rig and then try to undo its subscriptions or its global ownership assignments.
        foreach (var root in carriedRoots) root.SetActive(false);
        // Gate hierarchy activation, not each saved object's activeSelf. A save made
        // during Beam In must restore the authored active bike/chase on Continue.
        arrivalGameplayGate = new GameObject("Arrival gameplay gate");
        arrivalGameplayGate.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(arrivalGameplayGate, gameObject.scene);
        foreach (var root in entryGameplay)
        {
            if (bikeChase != null && root == bikeChase.PlayerBike.gameObject) root.SetActive(false);
            else root.transform.SetParent(arrivalGameplayGate.transform, true);
        }
        foreach (var root in warmupRoots) root.SetActive(false);
        foreach (var root in stagedRoots) root.SetActive(false);
        keyLight.enabled = false;
        foreach (var volume in volumes) volume.weight = 0;
        var source = crossover.Source;
        occlusion.PrepareSceneArrival(source.player.transform, source.gameplayCamera);
        foreach (var root in carriedRoots) Destroy(root);
        carriedRoots = source.carriedRoots;
        player = source.player;
        gameplayCamera = source.gameplayCamera;
        cameraController = source.cameraController;
        bikeChase?.BindSceneArrival(player, gameplayCamera);
        foreach (var weapon in bikeCameraBindings)
        {
            weapon.combatCamera = cameraController;
            if (weapon.GetComponent<EnemyBikeDriver>() == null) weapon.player = player;
        }
        crossover.AcceptDestination(this);
    }

    public IEnumerator Prepare()
    {
        // Inactive duplicate roots must be destroyed before the run registry is assembled.
        yield return null;
        foreach (var root in stagedRoots) root.SetActive(true);
        var budget = System.Diagnostics.Stopwatch.StartNew();
        int completedGroups = 0;
        foreach (var root in warmupRoots)
        {
            root.SetActive(true);
            PreparationProgress = (float)++completedGroups / (warmupRoots.Length + 1);
            if (budget.Elapsed.TotalMilliseconds < 2) continue;
            yield return null; budget.Restart();
        }
        yield return occlusion.PrewarmSceneCache();
        yield return null;
        IsPrepared = true; PreparationProgress = 1;
    }

    public void Enter(GameplaySceneEntry source, bool startGameplay = true)
    {
        if (!IsPrepared) throw new InvalidOperationException("Destination preparation is incomplete.");
        source.occlusion.enabled = false;
        occlusion.enabled = true;
        source.keyLight.enabled = false;
        keyLight.enabled = true;
        lighting.Apply();
        RenderSettings.sun = keyLight;
        foreach (var volume in volumes) volume.weight = 1;

        // Reveal the unoccupied native bike behind the loading artwork, so it is
        // already beside Amy during Beam In. Chase/containment wait for landing.
        if (bikeChase != null) bikeChase.PlayerBike.gameObject.SetActive(true);

        IsIncoming = false;
        if (startGameplay) ActivateEntryGameplay();
    }

    public void ActivateEntryGameplay()
    {
        if (arrivalGameplayGate != null) arrivalGameplayGate.SetActive(true);
        bikeChase?.GetComponent<BikeRouteContainment>()?.ClearArrivalWarning();
        IsIncoming = false;
    }
}
