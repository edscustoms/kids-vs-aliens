using System;
using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ExcavatorSwingTests
{
    [Test]
    public void SceneSwingKeepsRigidUpperAssemblyFixedTracksAndAuthoredBlockerUntilContact()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        try
        {
            var motion = Object.FindObjectsByType<ExcavatorMotionController>().Single();
            Assert.That(motion.State, Is.EqualTo(ExcavatorMotionState.Idle));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(motion), Is.Null, "Motion belongs only to this scene instance.");
            var data = new SerializedObject(motion);
            var axis = data.FindProperty("slewAxis").vector3Value.normalized;
            var pivot = motion.UpperPivot;
            // The bearing is horizontal in the model's original Z-up frame. Its world normal
            // inherits the intentionally tilted chassis, rather than correcting that scene pose.
            Assert.That(axis, Is.EqualTo(Vector3.forward));
            Assert.That(Vector3.Dot(pivot.TransformDirection(axis), motion.transform.up), Is.GreaterThan(.99999f));
            var filters = motion.GetComponentsInChildren<MeshFilter>();
            var original = filters.Select(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v)).ToArray()).ToArray();
            var materials = filters.Select(f => f.GetComponent<Renderer>().sharedMaterials).ToArray();
            var colliders = motion.Blocker.GetComponentsInChildren<MeshCollider>();
            Assert.That(colliders, Is.Not.Empty);
            foreach (var renderer in motion.Blocker.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.That(renderer.staticShadowCaster, Is.False, "Moving concrete must update its shadows.");
                Assert.That(GameObjectUtility.GetStaticEditorFlags(renderer.gameObject), Is.EqualTo((StaticEditorFlags)0));
            }
            var meshes = colliders.Select(c => c.sharedMesh).ToArray();
            var blockerPose = motion.Blocker.localToWorldMatrix;
            var pieces = Enumerable.Range(12, 6).Select(i => motion.Blocker.Find("PRE_Concrete Barrier_2 (" + i + ")")).ToArray();
            var piecePoses = pieces.Select(p => p.localToWorldMatrix).ToArray();
            var movingBoxes = pivot.GetComponentsInChildren<BoxCollider>();
            Assert.That(movingBoxes.Length, Is.GreaterThanOrEqualTo(5));
            Assert.That(pivot.GetComponentsInChildren<MeshCollider>(), Is.Empty);
            var boxPoses = movingBoxes.Select(c => pivot.InverseTransformPoint(c.transform.position)).ToArray();
            var boxSizes = movingBoxes.Select(c => c.size).ToArray();
            Assert.That(pivot.parent.GetComponents<BoxCollider>().Count(c => c.enabled), Is.EqualTo(1), "Only tracks retain stationary collision.");
            var pivotPose = pivot.localPosition;
            var startRotation = pivot.rotation;
            foreach (float time in new[] { motion.Duration * .5f, motion.ImpactTime - .001f, motion.ImpactTime, motion.CompletionTime })
            {
                motion.PreviewProgress(time);
                Assert.That(pivot.localPosition, Is.EqualTo(pivotPose), "Pure rotation, no translation compensation.");
                Quaternion change = pivot.rotation * Quaternion.Inverse(startRotation);
                for (int i = 0; i < filters.Length; i++)
                {
                    var f = filters[i]; var local = f.sharedMesh.vertices;
                    for (int v = 0; v < local.Length; v++)
                    {
                        Vector3 expected = f.transform.IsChildOf(pivot) ? pivot.position + change * (original[i][v] - pivot.position) : original[i][v];
                        Assert.That(Vector3.Distance(expected, f.transform.TransformPoint(local[v])), Is.LessThan(.000025f), f.name);
                    }
                    Assert.That(f.GetComponent<Renderer>().sharedMaterials, Is.EqualTo(materials[i]));
                }
                for (int i = 0; i < colliders.Length; i++) Assert.That(colliders[i].sharedMesh, Is.SameAs(meshes[i]));
                for (int i = 0; i < movingBoxes.Length; i++)
                {
                    Assert.That(Vector3.Distance(movingBoxes[i].transform.position, pivot.TransformPoint(boxPoses[i])), Is.LessThan(.00002f));
                    Assert.That(movingBoxes[i].size, Is.EqualTo(boxSizes[i]));
                    Assert.That(movingBoxes[i].enabled, Is.True);
                    Assert.That(movingBoxes[i].attachedRigidbody.isKinematic, Is.True);
                }
                if (time <= motion.ImpactTime)
                {
                    Assert.That(motion.Blocker.localToWorldMatrix, Is.EqualTo(blockerPose));
                    Assert.That(pieces.Select(p => p.localToWorldMatrix), Is.EqualTo(piecePoses));
                    Assert.That(colliders.All(c => c.enabled == (time < motion.ImpactTime)), Is.True);
                }
            }
            Assert.That(motion.IsExitClear, Is.True);
            Assert.That(colliders.All(c => c.enabled), Is.True);
            Assert.That(motion.Blocker.GetComponentsInChildren<Renderer>().All(r => r.enabled), Is.True, "The blocker falls; it is not hidden/deleted.");
            Assert.That(pieces.Select(p => p.rotation).Distinct().Count(), Is.EqualTo(6), "Separate rubble orientations, not a single sheet.");
            foreach (var r in motion.Blocker.GetComponentsInChildren<Renderer>())
                Assert.That(r.bounds.max.x < motion.Blocker.position.x - .65f || r.bounds.min.x > motion.Blocker.position.x + .65f, Is.True, "Visible rubble must also clear the central walking corridor.");
            motion.PreviewProgress(motion.ImpactTime + .035f);
            Assert.That(pieces[0].localToWorldMatrix, Is.Not.EqualTo(piecePoses[0]));
            Assert.That(pieces[2].localToWorldMatrix, Is.EqualTo(piecePoses[2]), "The fall propagates from impact instead of moving all pieces together.");
            motion.ResetToStart();
            Assert.That(pieces.Select(p => p.localToWorldMatrix), Is.EqualTo(piecePoses));
            Assert.That(pivot.rotation, Is.EqualTo(startRotation));
            Assert.That(motion.Blocker.localToWorldMatrix, Is.EqualTo(blockerPose));
            Assert.That(colliders.All(c => c.enabled), Is.True);
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }
    [Test]
    public void ImpactKicksPiecesAlongShortCurvesAndKeepsOneExitBarrierUntilSettled()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        try
        {
            var motion = Object.FindAnyObjectByType<ExcavatorMotionController>();
            var data = new SerializedObject(motion);
            var barrier = (BoxCollider)data.FindProperty("temporaryExitBlocker").objectReferenceValue;
            var records = data.FindProperty("blockerPieces");
            var pieces = Enumerable.Range(0, records.arraySize).Select(i => (Transform)records.GetArrayElementAtIndex(i).FindPropertyRelative("piece").objectReferenceValue).ToArray();
            var start = pieces.Select(t => t.localToWorldMatrix).ToArray();
            var colliders = motion.Blocker.GetComponentsInChildren<MeshCollider>();
            foreach (int i in Enumerable.Range(0, records.arraySize))
            {
                var record = records.GetArrayElementAtIndex(i);
                Assert.That(record.FindPropertyRelative("fallDuration").floatValue, Is.InRange(.5f, .9f));
                Assert.That(record.FindPropertyRelative("delay").floatValue, Is.InRange(0, .1f));
                Assert.That(record.FindPropertyRelative("flightControl").objectReferenceValue, Is.Not.Null);
            }
            motion.PreviewProgress(motion.ImpactTime);
            Assert.That(colliders.All(c => !c.enabled), Is.True, "All visual collision is removed at impact, even delayed pieces.");
            Assert.That(barrier.enabled, Is.True);
            Assert.That(pieces.Select(t => t.localToWorldMatrix), Is.EqualTo(start));
            Vector3 initial = pieces[0].position;
            motion.PreviewProgress(motion.ImpactTime + .08f);
            Assert.That(Vector3.Distance(initial, pieces[0].position), Is.GreaterThan(.45f), "The initial kick must be immediate and readable.");
            motion.PreviewProgress(motion.BlockerClearTime - .001f);
            Assert.That(barrier.enabled, Is.True);
            Assert.That(motion.IsExitClear, Is.False);
            motion.PreviewProgress(motion.BlockerClearTime);
            Assert.That(barrier.enabled, Is.False);
            Assert.That(motion.IsExitClear, Is.True);
            Assert.That(colliders.All(c => c.enabled), Is.True, "Settled concrete regains its original collision before the exit opens.");
            motion.PreviewProgress(motion.CompletionTime);
            var upper = motion.UpperPivot.GetComponentsInChildren<BoxCollider>();
            foreach (var slab in colliders)
                foreach (var box in upper)
                    Assert.That(Physics.ComputePenetration(slab, slab.transform.position, slab.transform.rotation,
                        box, box.transform.position, box.transform.rotation, out _, out float depth) && depth > .02f,
                        Is.False, slab.transform.parent.name + " must settle clear of " + box.name);
            motion.ResetToStart();
            Assert.That(pieces.Select(t => t.localToWorldMatrix), Is.EqualTo(start));
            Assert.That(colliders.All(c => c.enabled), Is.True);
            Assert.That(barrier.enabled, Is.True);
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }

}

public sealed class ExcavatorSwingPlayTests
{
    const string Key = "ExcavatorSwingTests.SaveDirectory";
    static ExcavatorMotionController Motion => Object.FindAnyObjectByType<ExcavatorMotionController>();
    static ActiveRunController Run => ActiveRunController.Instance;
    static PlayerCharacter Player => Run.GetComponent<PlayerCharacter>();
    static StarterAssetsInputs Input => Run.GetComponent<StarterAssetsInputs>();

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SessionState.SetString(Key, Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/ExcavatorSwingTest-" + Guid.NewGuid().ToString("N")));
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        double deadline = EditorApplication.timeSinceStartup + 30;
        while (Run == null || !Run.IsReady)
        {
            Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
            yield return EditorTestFrame.Next();
        }
        Run.GetComponent<BeamTransportController>().CancelTransport();
        Resume(); yield return Seconds(.3f);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) { Run?.PrepareToLeave(); yield return new ExitPlayMode(); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(Key, ""));
        SessionState.EraseString(Key); Time.timeScale = 1;
    }

    [UnityTest]
    public IEnumerator ExplicitSwingKnocksBlockerClearAndAmyWalksThroughWithNormalLocomotion()
    {
        var motion = Motion;
        var pivot = motion.UpperPivot;
        var start = pivot.localRotation;
        var blocker = motion.Blocker.localToWorldMatrix;
        var other = Object.FindObjectsByType<CameraOcclusionGroup>().Single(g => g.name == "PF_Excavator_A" && g.gameObject != motion.gameObject);
        var otherPoses = other.GetComponentsInChildren<Transform>().Select(t => t.localToWorldMatrix).ToArray();
        yield return Seconds(.4f);
        Assert.That(motion.State, Is.EqualTo(ExcavatorMotionState.Idle));
        Assert.That(pivot.localRotation, Is.EqualTo(start));

        Vector3 entry = motion.Blocker.position + new Vector3(0, .08f, -2.5f);
        Vector3 exit = motion.Blocker.position + new Vector3(0, .08f, 2.5f);
        Place(entry);
        yield return WalkToward(exit, 3.5f);
        Assert.That(Player.transform.position.z, Is.LessThan(motion.Blocker.position.z + .5f), "The original blocker physically prevents walking through.");
        Input.MoveInput(Vector2.zero); Place(entry);
        int completed = 0; motion.OnSwingCompleted.AddListener(() => completed++);
        float begun = Time.time;
        motion.PlaySwing(); motion.PlaySwing();
        Assert.That(motion.State, Is.EqualTo(ExcavatorMotionState.Moving));
        var temporaryExit = (BoxCollider)new SerializedObject(motion).FindProperty("temporaryExitBlocker").objectReferenceValue;
        while (!motion.HasImpacted)
        {
            Resume();
            DriveToward(exit);
            Assert.That(motion.Blocker.localToWorldMatrix, Is.EqualTo(blocker));
            Assert.That(completed, Is.Zero);
            Assert.That(Time.time - begun, Is.LessThan(motion.CompletionTime + 2));
            yield return EditorTestFrame.Next();
        }
        Assert.That(Time.time - begun, Is.GreaterThanOrEqualTo(motion.ImpactTime - .02f));
        motion.PlaySwing();
        while (motion.State != ExcavatorMotionState.Completed)
        {
            Resume();
            DriveToward(exit);
            Assert.That(motion.Blocker.GetComponentsInChildren<MeshCollider>().All(c => c.enabled == motion.IsExitClear), Is.True);
            if (!motion.IsExitClear)
            {
                Assert.That(temporaryExit.enabled, Is.True);
                Assert.That(Player.transform.position.z, Is.LessThan(temporaryExit.bounds.min.z), "Amy is walking against the temporary barrier during the fall.");
            }
            Assert.That(completed, Is.Zero, "Completion waits for the blocker fall.");
            Assert.That(Time.time - begun, Is.LessThan(motion.CompletionTime + 2));
            yield return EditorTestFrame.Next();
        }
        Input.MoveInput(Vector2.zero);
        Assert.That(temporaryExit.enabled, Is.False);
        Assert.That(motion.IsExitClear, Is.True);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(Time.time - begun, Is.GreaterThanOrEqualTo(motion.CompletionTime - .02f));
        var end = pivot.localRotation;
        motion.PlaySwing(); yield return Seconds(.2f);
        Assert.That(completed, Is.EqualTo(1)); Assert.That(pivot.localRotation, Is.EqualTo(end));
        Assert.That(other.GetComponentsInChildren<Transform>().Select(t => t.localToWorldMatrix), Is.EqualTo(otherPoses));
        Assert.That(Run.GetComponent<GameplaySuspensionController>().IsSuspended, Is.False);
        Assert.That(Run.GetComponent<ThirdPersonController>().enabled, Is.True);
        Assert.That(Input.GameplayInputBlocked, Is.False);
        yield return WalkToward(exit, 5);
        Assert.That(Vector2.Distance(new Vector2(Player.transform.position.x, Player.transform.position.z), new Vector2(exit.x, exit.z)), Is.LessThan(.25f), "Amy must walk through the opened exit using her normal movement owner.");
        CaptureExit("amy-through-exit");
        TestContext.WriteLine("Amy walked from " + entry.ToString("F3") + " to " + Player.transform.position.ToString("F3") + "; exit target " + exit.ToString("F3"));
        // The new separate rubble poses leave a straight visible corridor, not a route
        // that succeeds only because the fallen geometry has disabled collision.
        var rubble = motion.Blocker.GetComponentsInChildren<Renderer>();
        Vector3 outside = new Vector3(exit.x, exit.y, rubble.Max(r => r.bounds.max.z) + 1f);
        yield return WalkToward(outside, 8);
        Assert.That(Vector2.Distance(new Vector2(Player.transform.position.x, Player.transform.position.z), new Vector2(outside.x, outside.z)), Is.LessThan(.25f));
        CaptureExit("amy-beyond-rubble");
        TestContext.WriteLine("Amy continued between the visible rubble to " + Player.transform.position.ToString("F3"));
        Input.MoveInput(Vector2.zero);
        motion.ResetToStart();
        Assert.That(motion.State, Is.EqualTo(ExcavatorMotionState.Idle));
        Assert.That(pivot.localRotation, Is.EqualTo(start));
        Assert.That(motion.Blocker.localToWorldMatrix, Is.EqualTo(blocker));
        Assert.That(motion.Blocker.GetComponentsInChildren<Collider>().All(c => c.enabled), Is.True);
    }

    [UnityTest]
    public IEnumerator AmyCapsuleIsBlockedByUpperAssemblyBeforeDuringAndAfterSwing()
    {
        var motion = Motion;
        var capsule = Player.GetComponent<CharacterController>();
        var locomotion = Player.GetComponent<ThirdPersonController>();
        var body = motion.UpperPivot.Find("MovingCollision/UpperHouse").GetComponent<BoxCollider>();
        var probe = Player.gameObject.AddComponent<ExcavatorCollisionProbe>();
        locomotion.enabled = false;
        for (int phase = 0; phase < 3; phase++)
        {
            if (phase == 1) { motion.PlaySwing(); yield return Seconds(motion.Duration * .5f); }
            if (phase == 2) yield return Seconds(motion.CompletionTime - motion.Duration * .5f + .1f);
            Physics.SyncTransforms();
            // Exercise the real player capsule against the actual moving compound in
            // both horizontal directions. The fixture supplies movement to isolate collision.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 direction = Vector3.ProjectOnPlane(body.transform.up, Vector3.up).normalized * side;
                Vector3 center = body.transform.TransformPoint(body.center);
                Place(center + direction * 3.2f - Vector3.up * capsule.center.y);
                probe.UpperHits = 0; probe.Pivot = motion.UpperPivot;
                for (int step = 0; step < 80; step++) capsule.Move(-direction * .06f);
                Assert.That(probe.UpperHits, Is.GreaterThan(0), "Amy must hit the upper compound at swing phase " + phase);
                Assert.That(Vector3.Dot(Player.transform.position + Vector3.up * capsule.center.y - center, direction), Is.GreaterThan(.5f), "Amy cannot pass into/through the upper house.");
                Assert.That(Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
                    body, body.transform.position, body.transform.rotation, out _, out float penetration) && penetration > capsule.skinWidth + .02f, Is.False);
            }
        }
        Object.Destroy(probe); locomotion.enabled = true;
    }

    [UnityTest]
    public IEnumerator SettledRubbleSupportsAmyAndBlocksWalkingThroughConcrete()
    {
        var motion = Motion;
        motion.PlaySwing();
        yield return Seconds(motion.CompletionTime + .1f);
        Physics.SyncTransforms();
        var capsule = Player.GetComponent<CharacterController>();
        var rubble = motion.Blocker.GetComponentsInChildren<MeshCollider>();
        Assert.That(rubble.All(c => c.enabled), Is.True);
        foreach (var slab in rubble)
        {
            Assert.That(slab.sharedMesh, Is.SameAs(slab.GetComponent<MeshFilter>().sharedMesh), "Collision uses the visible concrete mesh.");
            var bounds = slab.bounds;
            var ray = new Ray(new Vector3(bounds.center.x, bounds.max.y + 2, bounds.center.z), Vector3.down);
            Assert.That(slab.Raycast(ray, out var surface, 5), Is.True, slab.transform.parent.name);
            Place(surface.point + Vector3.up * .12f);
            yield return Seconds(.5f);
            Assert.That(capsule.bounds.min.y, Is.GreaterThan(surface.point.y - .16f), "Amy must be supported by " + slab.transform.parent.name);
            Vector3 direction = Vector3.ProjectOnPlane(slab.transform.right, Vector3.up).normalized;
            Vector3 from = Player.transform.position;
            yield return WalkToward(from + direction * .35f, 1);
            Assert.That(Vector3.Dot(Player.transform.position - from, direction), Is.GreaterThan(.15f), "Amy can walk along the settled concrete.");
            Assert.That(capsule.bounds.min.y, Is.GreaterThan(surface.point.y - .2f), "Walking must not sink Amy through visible concrete.");
        }
        // Approach a clear outer side with normal locomotion: the low rubble may be
        // climbed if its slope allows it, but the capsule must never pass through it.
        var outer = rubble.OrderByDescending(c => c.bounds.center.x).First();
        Vector3 across = Vector3.ProjectOnPlane(outer.transform.forward, Vector3.up).normalized;
        if (across.x < 0) across = -across;
        Vector3 center = outer.bounds.center;
        Vector3 approach = center + across * 2; approach.y = outer.bounds.min.y + .03f;
        Place(approach);
        var probe = Player.gameObject.AddComponent<ExcavatorCollisionProbe>();
        probe.Rubble = outer;
        yield return WalkToward(center - across * 1.5f, 2.5f);
        Assert.That(probe.RubbleHits, Is.GreaterThan(0), "Amy must physically contact the visible rubble.");
        Assert.That(Physics.ComputePenetration(capsule, capsule.transform.position, capsule.transform.rotation,
            outer, outer.transform.position, outer.transform.rotation, out _, out float depth) && depth > capsule.skinWidth + .02f, Is.False);
        CaptureExit("amy-solid-rubble");
        Object.Destroy(probe);
    }

    static void DriveToward(Vector3 destination)
    {
        Vector3 delta = destination - Player.transform.position; delta.y = 0;
        var camera = Camera.main.transform;
        Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(camera.right, Vector3.up).normalized;
        Input.MoveInput(new Vector2(Vector3.Dot(delta.normalized, right), Vector3.Dot(delta.normalized, forward)));
    }

    static IEnumerator WalkToward(Vector3 destination, float seconds)
    {
        float until = Time.time + seconds; double deadline = EditorApplication.timeSinceStartup + 30;
        while (Time.time < until)
        {
            Resume(); Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
            Vector3 delta = destination - Player.transform.position; delta.y = 0;
            if (delta.magnitude < .15f) break;
            var camera = Camera.main.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(camera.right, Vector3.up).normalized;
            Input.MoveInput(new Vector2(Vector3.Dot(delta.normalized, right), Vector3.Dot(delta.normalized, forward)));
            yield return EditorTestFrame.Next();
        }
        Input.MoveInput(Vector2.zero);
    }
    static void Place(Vector3 position)
    {
        var capsule = Run.GetComponent<CharacterController>(); capsule.enabled = false;
        Player.transform.SetPositionAndRotation(position, Quaternion.identity); capsule.enabled = true;
        Run.GetComponent<ThirdPersonController>().ResetMotion(); Physics.SyncTransforms();
    }
    static void Resume()
    {
        Run.SendMessage("OnApplicationPause", false); Run.SendMessage("OnApplicationFocus", true);
        var menu = Object.FindAnyObjectByType<InGameMenuController>(); if (menu != null && menu.IsOpen) menu.ResumeGame();
    }
    static IEnumerator Seconds(float seconds)
    {
        float end = Time.time + seconds; double deadline = EditorApplication.timeSinceStartup + 30;
        while (Time.time < end) { Resume(); Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline)); yield return EditorTestFrame.Next(); }
    }
    static void CaptureExit(string name)
    {
        var rt = new RenderTexture(1280, 720, 24); var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); var old = RenderTexture.active;
        try
        {
            rt.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(Camera.main, new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt });
            RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            Directory.CreateDirectory("Logs/ExcavatorSwing"); File.WriteAllBytes("Logs/ExcavatorSwing/" + name + ".png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = old; Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt); }
    }
}

public sealed class ExcavatorCollisionProbe : MonoBehaviour
{
    public Transform Pivot;
    public int UpperHits;
    public Collider Rubble;
    public int RubbleHits;
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (Pivot != null && hit.collider.transform.IsChildOf(Pivot)) UpperHits++;
        if (hit.collider == Rubble) RubbleHits++;
    }
}
