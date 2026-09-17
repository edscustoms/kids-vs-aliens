using System;
using System.IO;
using System.Linq;
using System.Text;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit, temporary Play-mode review of the saved scene. Never saves runtime changes.
[InitializeOnLoad]
public static class BeamTransportV2PlayReview
{
    private const string Key = "BeamV2.PlayReview";
    private static BeamTransportController transport;
    private static BeamTransportVFX arrival, hoist;
    private static StarterAssetsInputs input;
    private static Vector3 destination, start;
    private static BeamHoistPath path;
    private static int phase;
    private static bool arrivalCaptured, hoistCaptured, curveCaptured;
    private static float previousY;
    private static double started;
    private static StringBuilder report;
    static BeamTransportV2PlayReview() => EditorApplication.update += Tick;

    public static void Begin()
    {
        if (SceneManager.GetActiveScene().path != "Assets/Game/Scenes/ConstructionSite.unity") throw new Exception("Open ConstructionSite first.");
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ObserveStartup()
    {
        if (!SessionState.GetBool(Key, false)) return;
        report = new StringBuilder(); phase = 0;
        arrivalCaptured = hoistCaptured = curveCaptured = false;
        started = EditorApplication.timeSinceStartup;
        try
        {
            transport = Object.FindAnyObjectByType<BeamTransportController>();
            input = transport.GetComponent<StarterAssetsInputs>();
            var sequence = Object.FindAnyObjectByType<PlayerBeamInSequence>();
            var data = new SerializedObject(sequence);
            destination = ((Transform)data.FindProperty("beamInSpawn").objectReferenceValue).position;
            arrival = (BeamTransportVFX)data.FindProperty("transportVfx").objectReferenceValue;
            Check(transport.IsTransporting && input.GameplayInputBlocked && !transport.GetComponent<CharacterController>().enabled,
                "arrival owns capsule/input before first Update");
            Check(Vector3.Distance(transport.transform.position, destination + Vector3.up * data.FindProperty("startHeight").floatValue) < .001f,
                "arrival starts at authored height");
            previousY = transport.transform.position.y;
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false) || transport == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 60) throw new Exception("Play review timed out.");
            if (phase == 0)
            {
                float y = transport.transform.position.y;
                if (transport.IsTransporting && y > previousY + .002f) throw new Exception("Arrival moved upward.");
                previousY = y;
                if (!arrivalCaptured && y < destination.y + 6 && y > destination.y + 1)
                { Capture(arrival, "arrival"); arrivalCaptured = true; }
                if (transport.IsTransporting || !input.CanProcessGameplayInput) return;
                Check(arrivalCaptured && Vector3.Distance(transport.transform.position, destination) < .15f, "one monotonic arrival and restored input");
                PrepareHoist(); phase = 1;
            }
            else if (phase == 1)
            {
                if (input.CanProcessGameplayInput)
                {
                    input.JumpInput(false); input.JumpInput(true);
                    Check(transport.IsTransporting && !input.jump, "contextual Jump starts hoist on actual container");
                    hoist = Object.FindObjectsByType<BeamTransportVFX>(FindObjectsInactive.Include).Single(v => v != arrival);
                    Check(hoist.Direction == BeamTransportDirection.Up, "hoist particles configured Up");
                    phase = 2;
                }
            }
            else if (phase == 2)
            {
                Vector3 position = transport.transform.position;
                bool beamVisible = hoist.transform.Find("BeamInVFX").gameObject.activeSelf;
                if (beamVisible)
                {
                    if (Vector2.Distance(new Vector2(position.x, position.z), new Vector2(start.x, start.z)) > .001f)
                        throw new Exception("Lateral motion began before beam release.");
                    if (Vector3.Distance(hoist.transform.position, start) > .001f) throw new Exception("Beam moved during lift.");
                    if (!hoistCaptured && position.y > start.y + .8f) { Capture(hoist, "hoist"); hoistCaptured = true; }
                }
                else if (!curveCaptured && Vector3.Distance(position, path.release) > .15f && transport.IsTransporting)
                {
                    CaptureCamera(Camera.main, "curve-game"); curveCaptured = true;
                    report.AppendLine("PASS: beam off before lateral transfer; curve observed.");
                }
                if (transport.IsTransporting || !input.CanProcessGameplayInput) return;
                Check(hoistCaptured && curveCaptured && Vector3.Distance(position, path.landing) < .15f && transport.IsLandingSafe(position),
                    "supported exact hoist landing and restored capsule/input");
                input.JumpInput(false); input.JumpInput(true);
                Check(input.jump && !transport.IsTransporting, "Jump on top falls back to normal jump; no reactivation");
                Finish("PASS");
            }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private static void PrepareHoist()
    {
        var surface = Object.FindAnyObjectByType<BeamHoistSurface>();
        var capsule = transport.GetComponent<CharacterController>();
        var ability = transport.GetComponent<BeamHoistAbility>();
        for (int i = 0; i < surface.CandidateCount; i++)
        {
            var cell = surface.GetBakedApproach(i);
            if (cell.side != 2) continue;
            Vector3 sample = surface.transform.TransformPoint(cell.region.center);
            if (!Physics.Raycast(sample + Vector3.up, Vector3.down, out var hit, 5, ~0, QueryTriggerInteraction.Ignore)) continue;
            start = hit.point + Vector3.up * (.02f - transport.FeetOffset);
            capsule.enabled = false; transport.transform.position = start; capsule.enabled = true;
            transport.GetComponent<ThirdPersonController>().ResetMotion();
            if (!surface.TryGetCandidate(start, i, transport.FeetOffset, out var end, out float release)) continue;
            path = BeamHoistPath.Create(start, end, release, 1.5f, .9f);
            if (!transport.IsLandingSafe(start) || !ability.IsWithinLimits(path) || !transport.CanHoist(path)) continue;
            transport.GetComponent<PlayerSkillState>().UnlockSkill(ability.RequiredSkill);
            report.AppendLine($"Container cell {i}: start={start:R}, release={path.release:R}, landing={path.landing:R}");
            return;
        }
        throw new Exception("No supported lower-side route on the actual container.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        report.AppendLine("PASS: " + message);
    }
    private static void Capture(BeamTransportVFX effect, string name)
    {
        CaptureCamera(Camera.main, name + "-game");
        var cone = effect.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "BeamOuterCone");
        Check(cone.enabled && cone.GetComponent<MeshFilter>().sharedMesh.vertexCount > 20, name + " full cone mesh active");
        var go = new GameObject("Temporary beam review camera");
        var camera = go.AddComponent<Camera>(); camera.enabled = false;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.orthographic = true; camera.orthographicSize = cone.bounds.extents.y + 1f;
        camera.nearClipPlane = .1f; camera.farClipPlane = 150f;
        camera.transform.position = cone.bounds.center + new Vector3(14, 3, -18);
        camera.transform.LookAt(cone.bounds.center);
        try
        {
            CaptureCamera(camera, name + "-full-beam");
            cone.enabled = false;
            CaptureCamera(camera, name + "-cone-disabled-comparison");
        }
        finally { cone.enabled = true; Object.Destroy(go); }
    }
    private static void CaptureCamera(Camera camera, string name)
    {
        if (camera == null) throw new Exception("Missing capture camera.");
        var target = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(1024, 768, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0); pixels.Apply();
            File.WriteAllBytes(BeamTransportV2Review.Reports + "/" + name + ".png", pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; target.Release(); Object.Destroy(target); Object.Destroy(pixels); }
    }
    private static void Finish(string result)
    {
        File.WriteAllText(BeamTransportV2Review.Reports + "/play-review.txt", report + "\n" + result);
        SessionState.SetBool(Key, false);
        EditorApplication.isPlaying = false;
    }
}
