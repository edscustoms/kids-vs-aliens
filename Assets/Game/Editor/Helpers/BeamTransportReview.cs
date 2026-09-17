using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in work-session bridge, matching the repository's existing review helpers.
[InitializeOnLoad]
public static class BeamTransportReview
{
    // Explicit batch migration entry point; never runs merely on opening Unity.
    public static void PrepareConstructionSite()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
        BeamTransportSetup.ConfigureScene(player);
        var sequence = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerBeamInSequence>(true)).Single();
        var data = new SerializedObject(sequence);
        var point = (Transform)data.FindProperty("beamInSpawn").objectReferenceValue;
        var transport = player.GetComponent<BeamTransportController>();
        Physics.SyncTransforms();
        if (!transport.IsLandingSafe(point.position))
            Debug.LogWarning("Authored arrival marker has no safe supported pose. Placement is preserved; adjust it explicitly in the Scene view.", point);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Beam migration complete; arrival at " + point.position);
    }
    private const string PlayReview = "BeamTransportReview.Active";
    private static BeamTransportController reviewedTransport;
    private static Vector3 destination;
    private static float previousY;
    private static double startedAt;
    private static int observations;
    static BeamTransportReview() => EditorApplication.update += Tick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ObserveStartup()
    {
        if (!SessionState.GetBool(PlayReview, false)) return;
        try
        {
            reviewedTransport = UnityEngine.Object.FindAnyObjectByType<BeamTransportController>();
            var sequence = UnityEngine.Object.FindAnyObjectByType<PlayerBeamInSequence>();
            var data = new SerializedObject(sequence);
            destination = ((Transform)data.FindProperty("beamInSpawn").objectReferenceValue).position;
            float height = data.FindProperty("startHeight").floatValue;
            if (reviewedTransport == null || !reviewedTransport.IsTransporting
                || reviewedTransport.GetComponent<CharacterController>().enabled
                || !reviewedTransport.GetComponent<StarterAssets.StarterAssetsInputs>().GameplayInputBlocked
                || Vector3.Distance(reviewedTransport.transform.position, destination + Vector3.up * height) > 0.001f)
                throw new Exception("Arrival did not own and position Amy before the first Update.");
            previousY = reviewedTransport.transform.position.y;
            startedAt = EditorApplication.timeSinceStartup;
            observations = 0;
            File.WriteAllText("Temp/Beam-play.result", "Startup passed before first Update.");
        }
        catch (Exception error) { FinishReview(error.ToString()); }
    }

    private static void FinishReview(string result)
    {
        File.WriteAllText("Temp/Beam-play.result", result);
        SessionState.SetBool(PlayReview, false);
        EditorApplication.isPlaying = false;
    }

    private static void ObservePlay()
    {
        if (reviewedTransport == null) return;
        observations++;
        float y = reviewedTransport.transform.position.y;
        if (y > previousY + 0.001f) { FinishReview("FAIL: Amy moved upward during arrival."); return; }
        previousY = y;
        if (!reviewedTransport.IsTransporting)
        {
            bool passed = Vector3.Distance(reviewedTransport.transform.position, destination) < 0.12f
                && reviewedTransport.GetComponent<CharacterController>().enabled
                && !reviewedTransport.GetComponent<StarterAssets.StarterAssetsInputs>().GameplayInputBlocked;
            FinishReview($"{(passed ? "PASS" : "FAIL")}: first Update ownership, one monotonic descent, restored capsule/input; observations={observations}; landed={reviewedTransport.transform.position}; expected={destination}");
        }
        else if (EditorApplication.timeSinceStartup - startedAt > 20) FinishReview("FAIL: arrival timed out.");
    }
    private static void Tick()
    {
        if (EditorApplication.isPlaying && SessionState.GetBool(PlayReview, false)) { ObservePlay(); return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode
            || !File.Exists("Temp/Beam.request")) return;
        string request = File.ReadAllText("Temp/Beam.request").Trim();
        File.Delete("Temp/Beam.request");
        try
        {
            if (request == "setup")
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.path != "Assets/Game/Scenes/ConstructionSite.unity") throw new Exception("Open ConstructionSite for authored VFX migration.");
                // Preserve the complete live scene before a targeted migration.
                EditorSceneManager.SaveScene(scene, "Temp/ConstructionSite-before-beam.unity", true);
                var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
                BeamTransportSetup.ConfigureScene(player);
                BeamTransportSetup.ConfigureScene(player);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                File.WriteAllText("Temp/Beam.result", "Setup complete: " + scene.path);
            }
            else if (request == "play")
            {
                if (SceneManager.GetActiveScene().path != "Assets/Game/Scenes/ConstructionSite.unity") throw new Exception("Open ConstructionSite first.");
                SessionState.SetBool(PlayReview, true);
                EditorApplication.isPlaying = true;
            }
            else if (request == "inspect")
            {
                var scene = SceneManager.GetActiveScene();
                var player = UnityEngine.Object.FindAnyObjectByType<PlayerCharacter>();
                var sparks = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include).FirstOrDefault(p => p.name == "BeamSparks");
                File.WriteAllText("Temp/Beam.result", $"Scene={scene.path}; dirty={scene.isDirty}; player={player?.transform.position}; sparks Y={sparks?.velocityOverLifetime.y.Evaluate(0.5f)}; rotation={sparks?.transform.rotation.eulerAngles}");
            }
            else if (request == "physics")
            {
                var transport = UnityEngine.Object.FindAnyObjectByType<BeamTransportController>();
                var sequence = UnityEngine.Object.FindAnyObjectByType<PlayerBeamInSequence>();
                var data = new SerializedObject(sequence);
                var point = (Transform)data.FindProperty("beamInSpawn").objectReferenceValue;
                var cc = transport.GetComponent<CharacterController>();
                string report = $"root={transport.transform.position}, scale={transport.transform.lossyScale}, cc enabled={cc.enabled}, center={cc.center}, height={cc.height}, radius={cc.radius}, destination={point.position}, landingSafe={transport.IsLandingSafe(point.position)}, pathClear={transport.IsSegmentClear(point.position + Vector3.up * 3.5f, point.position)}\n";
                object[] args = { point.position, Vector3.zero, Vector3.zero, 0f };
                typeof(BeamTransportController).GetMethod("CapsuleAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(transport, args);
                report += $"bottom={args[1]}, top={args[2]}, radius={args[3]}\n";
                foreach (var collider in Physics.OverlapCapsule((Vector3)args[1], (Vector3)args[2], (float)args[3], ~0, QueryTriggerInteraction.Ignore))
                    report += $"overlap {collider.name} bounds={collider.bounds}\n";
                foreach (var hit in Physics.RaycastAll(point.position + Vector3.up * 5, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
                    report += $"support {hit.collider.name} at {hit.point} normal={hit.normal}\n";
                File.WriteAllText("Temp/Beam.result", report);
            }
            else if (request == "repairLanding")
            {
                File.WriteAllText("Temp/Beam.result", "Automatic landing snapping retired: preserve authored marker and VFX placement. Adjust explicitly in Scene view if needed.");
            }
        }
        catch (Exception error) { File.WriteAllText("Temp/Beam.result", error.ToString()); Debug.LogException(error); }
    }
}
