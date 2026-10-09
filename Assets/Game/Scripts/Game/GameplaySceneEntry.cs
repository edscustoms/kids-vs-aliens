using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Explicit authored entry wiring. Standalone scenes keep their normal startup.</summary>
[DefaultExecutionOrder(-31000), DisallowMultipleComponent]
public sealed class GameplaySceneEntry : MonoBehaviour
{
    public GameObject[] carriedRoots = Array.Empty<GameObject>();
    public PlayerBikeRider player;
    public Camera gameplayCamera;
    public GameplayCameraController cameraController;
    public CameraOcclusionController occlusion;
    public Light keyLight;
    public Volume[] volumes = Array.Empty<Volume>();
    public SceneLightingState lighting;
    [Tooltip("Active authored groups, prepared over several frames during additive entry.")]
    public GameObject[] warmupRoots = Array.Empty<GameObject>();
    public GameObject connector;
    public GameObject[] entryGameplay = Array.Empty<GameObject>();
    public BikeRouteChaseDirector bikeChase;
    public AlienBikeLaserWeapon[] bikeCameraBindings = Array.Empty<AlienBikeLaserWeapon>();
    public bool IsPrepared { get; private set; }
    public bool IsIncoming { get; private set; }
    public ActiveRunController Run => player.GetComponent<ActiveRunController>();

    private void Awake()
    {
        var crossover = SceneCrossover.Pending;
        if (crossover == null || crossover.DestinationScene != gameObject.scene.name) return;
        IsIncoming = true;
        // Execution order precedes player/input/audio Awake. Never initialize a duplicate
        // rig and then try to undo its subscriptions or its global ownership assignments.
        foreach (var root in carriedRoots) root.SetActive(false);
        foreach (var root in entryGameplay) root.SetActive(false);
        foreach (var root in warmupRoots) root.SetActive(false);
        if (connector != null) connector.SetActive(false);
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
        var budget = System.Diagnostics.Stopwatch.StartNew();
        foreach (var root in warmupRoots)
        {
            root.SetActive(true);
            if (budget.Elapsed.TotalMilliseconds < 2) continue;
            yield return null; budget.Restart();
        }
        yield return occlusion.PrewarmSceneCache();
        yield return null;
        IsPrepared = true;
    }

    public void Enter(GameplaySceneEntry source)
    {
        if (!IsPrepared) throw new InvalidOperationException("Destination preparation is incomplete.");
        source.occlusion.enabled = false;
        occlusion.enabled = true;
        source.keyLight.enabled = false;
        keyLight.enabled = true;
        lighting.Apply();
        RenderSettings.sun = keyLight;
        foreach (var volume in volumes) volume.weight = 1;
        if (source.connector != null) source.connector.SetActive(false);
        if (connector != null) connector.SetActive(true);
        foreach (var root in entryGameplay) root.SetActive(true);
        IsIncoming = false;
    }
}
