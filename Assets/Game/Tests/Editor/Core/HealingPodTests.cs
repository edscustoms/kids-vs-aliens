using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class HealingPodTests
{
    private const string PrefabPath = "Assets/Game/Prefabs/Environment/PF_HealingPod.prefab";
    private const string SaveKey = "HealingPodTests.Saves";

    [UnitySetUp]
    public IEnumerator Setup()
    {
        SessionState.SetString(SaveKey, Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY",
            System.IO.Path.GetFullPath("Logs/HealingPodTests-" + Guid.NewGuid().ToString("N")));
        SessionState.SetFloat(SaveKey + ".TimeScale", Time.timeScale);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield break;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying)
        {
            ActiveRunController.Instance?.PrepareToLeave();
            yield return new ExitPlayMode();
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(SaveKey, ""));
        Time.timeScale = SessionState.GetFloat(SaveKey + ".TimeScale", 1);
        SessionState.EraseString(SaveKey);
        SessionState.EraseFloat(SaveKey + ".TimeScale");
    }

    [Test]
    public void ReservoirRestoreIsAbsoluteAndKeepsAuthoredTuning()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        try
        {
            var pod = root.GetComponent<HealingPodController>();
            var data = new SerializedObject(pod);
            string[] tuning = {"leftDoorOpenOffset", "rightDoorOpenOffset", "roofHingeAxis", "roofOpenAngle",
                "openCloseDuration", "alignDuration", "riseDuration", "healDuration", "lowerDuration", "exitDuration"};
            var before = tuning.Select(field => data.FindProperty(field).propertyType == SerializedPropertyType.Vector3
                ? data.FindProperty(field).vector3Value.ToString("R") : data.FindProperty(field).floatValue.ToString("R")).ToArray();
            pod.RestoreRunState("{\"remainingCapacity\":0.4}");
            pod.RestoreRunState("{\"remainingCapacity\":0.4}");
            Assert.That(pod.RemainingCapacity, Is.EqualTo(.4f));
            pod.RestoreRunState("{\"remainingCapacity\":0}");
            Assert.That(pod.IsDepleted, Is.True);
            Assert.That(pod.Openness, Is.Zero);
            pod.RestoreRunState("{\"depleted\":false}");
            Assert.That(pod.RemainingCapacity, Is.EqualTo(1));
            pod.RestoreRunState("{\"depleted\":true}");
            Assert.That(pod.IsDepleted, Is.True);
            data.Update();
            CollectionAssert.AreEqual(before, tuning.Select(field => data.FindProperty(field).propertyType == SerializedPropertyType.Vector3
                ? data.FindProperty(field).vector3Value.ToString("R") : data.FindProperty(field).floatValue.ToString("R")).ToArray());
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(100)]
    [TestCase(250)]
    public void HealthOwnerClampsActualGrantWithoutTouchingArmor(float maximum)
    {
        var root = new GameObject("Health capacity fixture");
        try
        {
            var health = root.AddComponent<PlayerHealth>();
            var data = new SerializedObject(health);
            data.FindProperty("maxHealth").floatValue = maximum; data.ApplyModifiedPropertiesWithoutUndo();
            health.RestoreRunHealth(maximum * .4f, 17);
            Assert.That(health.Heal(maximum), Is.EqualTo(maximum * .6f).Within(.001));
            Assert.That(health.HealthNormalized, Is.EqualTo(1));
            Assert.That(health.Heal(maximum), Is.Zero);
            Assert.That(health.CurrentArmor, Is.EqualTo(17));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void FullHealthRestorePreservesArmorAndCannotResurrect()
    {
        var actor = new GameObject("Health owner fixture");
        try
        {
            var health = actor.AddComponent<PlayerHealth>();
            health.RestoreRunHealth(20, 13);
            int changes = 0;
            health.OnHealthChanged += () => changes++;
            Assert.That(health.HealToFull(), Is.True);
            Assert.That(health.HealToFull(), Is.True, "The health owner accepts a no-op full restore; the pod rejects full-health entry");
            Assert.That(health.HealthNormalized, Is.EqualTo(1));
            Assert.That(health.CurrentArmor, Is.EqualTo(13));
            Assert.That(changes, Is.EqualTo(1));
            health.RestoreRunHealth(0, 13);
            Assert.That(health.HealToFull(), Is.False);
            Assert.That(health.IsDead, Is.True);
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [Test]
    public void PrefabKeepsModelMeshesPivotsAndIndicatorWithSeparatePrimitiveCollision()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        var controller = prefab.GetComponentInChildren<HealingPodController>(true);
        Assert.That(controller, Is.Not.Null);
        var data = new SerializedObject(controller);
        var left = Reference(data, "leftDoor");
        var right = Reference(data, "rightDoor");
        var roof = Reference(data, "roof");
        Assert.That(left, Is.Not.SameAs(right));
        Assert.That(left.parent, Is.SameAs(right.parent));
        Assert.That(prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "HealPod_Indicator").parent,
            Is.SameAs(roof));
        Assert.That(controller.transform.IsChildOf(prefab.transform.Find("Visual")), Is.False);
        var trigger = (SphereCollider)data.FindProperty("proximityTrigger").objectReferenceValue;
        Assert.That(trigger.isTrigger, Is.True);
        Assert.That(trigger.GetComponent<Rigidbody>().isKinematic, Is.True);
        Assert.That(controller.GetComponent<RunWorldObject>(), Is.Not.Null);
        foreach (var door in new[] {left, right})
        {
            var box = door.GetComponentInChildren<BoxCollider>();
            Assert.That(box, Is.Not.Null);
            Assert.That(box.isTrigger, Is.False);
            Assert.That(box.GetComponent<Rigidbody>().isKinematic, Is.True);
            Assert.That(door.GetComponent<Renderer>().sharedMaterials.Any(m =>
                m.GetFloat("_Surface") == 1 && m.GetColor("_BaseColor").a < .2f), Is.True,
                "Closed prototype doors need a transparent panel so healing is visible");
        }
        var core = prefab.GetComponentsInChildren<Renderer>().Single(r => r.name == "HealPod_Chamber");
        Assert.That(core.sharedMaterial.GetFloat("_Surface"), Is.EqualTo(1));
        Assert.That(core.sharedMaterial.GetColor("_BaseColor").a, Is.LessThan(.1f));
        var energy = prefab.GetComponentInChildren<BeamEnergyField>(true);
        Assert.That(energy, Is.Not.Null);
        Assert.That(energy.gameObject.activeSelf, Is.False);
        Assert.That(new SerializedObject(energy).FindProperty("spiralCount").intValue, Is.EqualTo(1));
        Assert.That(energy.GetComponentInChildren<LineRenderer>(true).sharedMaterial.shader.name, Is.EqualTo("Game/Beam Energy"));
        Assert.That(prefab.GetComponentsInChildren<BeamTransportController>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<MeshCollider>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty);
        Assert.That(prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "MCP_Test"), Is.False);
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Does.EndWith("HealPod_Proxy.fbx"));
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            foreach (var material in renderer.sharedMaterials)
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
    }

    [UnityTest]
    public IEnumerator PhysicsProximityReversesWithoutDriftAndHandlesMultipleOrDisabledColliders()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        Time.timeScale = 1;
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        var controller = root.GetComponentInChildren<HealingPodController>();
        var data = new SerializedObject(controller);
        var left = Reference(data, "leftDoor");
        var right = Reference(data, "rightDoor");
        var roof = Reference(data, "roof");
        var leftClosed = left.localPosition;
        var rightClosed = right.localPosition;
        var roofClosed = roof.localRotation;
        var roofPivot = roof.position;
        var roofCenter = roof.GetComponent<Renderer>().bounds.center;
        var indicator = root.GetComponentsInChildren<Transform>().Single(t => t.name == "HealPod_Indicator");
        var indicatorLocal = indicator.localPosition;
        float duration = data.FindProperty("openCloseDuration").floatValue;
        Assert.That(controller.Openness, Is.Zero);
        Physics.SyncTransforms();
        Assert.That(EntranceBlocked(), Is.True, "Closed primitive doors must block the chamber entrance");
        var leftBox = left.GetComponentInChildren<BoxCollider>();
        var rightBox = right.GetComponentInChildren<BoxCollider>();
        var boxCenter = leftBox.center;
        var boxSize = leftBox.size;
        var leftBoxLocal = leftBox.transform.localPosition;
        var rightBoxLocal = rightBox.transform.localPosition;

        var actor = new GameObject("Proximity actor");
        actor.transform.position = new Vector3(0, 1, 2.5f);
        var first = actor.AddComponent<SphereCollider>();
        first.radius = .2f;
        // An unmarked collider must never operate the pod.
        Physics.SyncTransforms();
        yield return Seconds(.15f);
        Assert.That(controller.Openness, Is.Zero);
        actor.SetActive(false);
        var character = actor.AddComponent<PlayerCharacter>();
        var characterData = new SerializedObject(character);
        characterData.FindProperty("visualRoot").objectReferenceValue = actor.transform;
        characterData.FindProperty("startingCharacterPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/Amy.prefab");
        characterData.ApplyModifiedPropertiesWithoutUndo();
        actor.SetActive(true);
        var child = new GameObject("Second player collider");
        child.transform.SetParent(actor.transform, false);
        var second = child.AddComponent<SphereCollider>();
        second.radius = .15f;
        yield return Seconds(duration + .15f);
        Assert.That(controller.Openness, Is.EqualTo(1));
        Physics.SyncTransforms();
        Assert.That(EntranceBlocked(), Is.False, "Open doors must leave capsule-width clearance");
        Assert.That(leftBox.center, Is.EqualTo(boxCenter));
        Assert.That(leftBox.size, Is.EqualTo(boxSize));
        Assert.That(leftBox.transform.localPosition, Is.EqualTo(leftBoxLocal));
        Assert.That(rightBox.transform.localPosition, Is.EqualTo(rightBoxLocal));
        Assert.That(Vector3.Distance(left.localPosition, leftClosed + data.FindProperty("leftDoorOpenOffset").vector3Value), Is.LessThan(.0001));
        Assert.That(Vector3.Distance(right.localPosition, rightClosed + data.FindProperty("rightDoorOpenOffset").vector3Value), Is.LessThan(.0001));
        Assert.That(Quaternion.Angle(roofClosed, roof.localRotation), Is.EqualTo(Mathf.Abs(data.FindProperty("roofOpenAngle").floatValue)).Within(.01));
        Assert.That(Vector3.Distance(roof.position, roofPivot), Is.LessThan(.0001), "The rear hinge stays fixed");
        Assert.That(roof.GetComponent<Renderer>().bounds.center.y, Is.GreaterThan(roofCenter.y + .5f), "The roof must swing up, not into the player");
        Assert.That(roof.GetComponent<Renderer>().bounds.center.z, Is.LessThan(roofCenter.z - .4f), "The roof opens backward");
        Assert.That(indicator.localPosition, Is.EqualTo(indicatorLocal));
        first.enabled = false;
        yield return Seconds(.15f);
        Assert.That(controller.Openness, Is.EqualTo(1), "One remaining collider keeps the pod open");
        second.enabled = false;
        yield return Seconds(duration + .15f);
        Assert.That(controller.Openness, Is.Zero, "Disabled colliders must not leave the pod stuck open");
        first.enabled = true;
        yield return Seconds(duration + .15f);
        Assert.That(controller.Openness, Is.EqualTo(1));

        for (int cycle = 0; cycle < 3; cycle++)
        {
            actor.transform.position = new Vector3(0, 1, 8);
            Physics.SyncTransforms();
            yield return Seconds(duration * .35f);
            float partial = controller.Openness;
            Assert.That(partial, Is.InRange(.05f, .95f));
            var beforeReverse = left.localPosition;
            actor.transform.position = new Vector3(0, 1, 2.5f);
            Physics.SyncTransforms();
            Assert.That(left.localPosition, Is.EqualTo(beforeReverse), "Changing proximity must not snap a door pose");
            yield return Seconds(duration * .35f);
            Assert.That(controller.Openness, Is.GreaterThan(partial));
            yield return Seconds(duration);
            Assert.That(controller.Openness, Is.EqualTo(1));
        }

        Time.timeScale = 0;
        float paused = controller.Openness;
        yield return EditorTestFrame.Next();
        Assert.That(controller.Openness, Is.EqualTo(paused));
        Time.timeScale = 1;
        actor.transform.position = new Vector3(0, 1, 8);
        Physics.SyncTransforms();
        yield return Seconds(duration + .15f);
        Assert.That(left.localPosition, Is.EqualTo(leftClosed));
        Assert.That(right.localPosition, Is.EqualTo(rightClosed));
        Assert.That(Quaternion.Angle(roof.localRotation, roofClosed), Is.LessThan(.001));

        actor.transform.position = new Vector3(0, 1, 2.5f);
        Physics.SyncTransforms();
        yield return Seconds(duration * .4f);
        controller.enabled = false;
        Assert.That(controller.Openness, Is.EqualTo(1), "Disabled interaction leaves a safe open entrance");
        controller.enabled = true;
        yield return Seconds(duration + .15f);
        Assert.That(controller.Openness, Is.EqualTo(1), "Stay recovers an already overlapping player on reenable");
        Object.Destroy(actor);
        yield return Seconds(duration + .15f);
        Assert.That(controller.Openness, Is.Zero);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ConstructionSiteArrivalApproachChamberAndDepartureRemainAccessible()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        yield return Seconds(4);
        var podArrival = Object.FindAnyObjectByType<AuthoredBeamArrival>();
        Assert.That(podArrival.TryBegin(Object.FindAnyObjectByType<PlayerCharacter>()), Is.True);
        yield return Seconds(2f);
        Assert.That(podArrival.HasArrived, Is.True);
        var pod = Object.FindAnyObjectByType<HealingPodController>();
        // Retain the original route/proximity regression independently of the one-use sequence tests.
        ((BoxCollider)new SerializedObject(pod).FindProperty("chamberTrigger").objectReferenceValue).enabled = false;
        var arrival = Object.FindAnyObjectByType<PlayerBeamInSequence>();
        var player = Object.FindAnyObjectByType<PlayerCharacter>();
        Assert.That(pod, Is.Not.Null);
        Assert.That(player.isActiveAndEnabled, Is.True);
        Assert.That(player.GetComponent<BeamTransportController>().IsTransporting, Is.False);
        Assert.That(Vector3.Distance(player.transform.position, arrival.transform.position), Is.LessThan(.65f));
        Assert.That(pod.Openness, Is.Zero, "Spawn must be outside the pod proximity");
        var capsule = player.GetComponent<CharacterController>();
        var movement = player.GetComponent<StarterAssets.ThirdPersonController>();
        movement.enabled = false;
        var root = pod.transform;
        var approach = root.position + root.forward * 2.5f;
        // Follow the existing clear aisle beside the excavator, then its north side.
        yield return Walk(capsule, new Vector3(-22,0,-23));
        yield return Walk(capsule, new Vector3(-22,0,-9.5f));
        yield return Walk(capsule, approach);
        yield return Seconds(1);
        Assert.That(pod.Openness, Is.EqualTo(1));
        yield return Walk(capsule, root.position);
        Assert.That(Vector3.Distance(new Vector3(player.transform.position.x, 0, player.transform.position.z),
            new Vector3(root.position.x, 0, root.position.z)), Is.LessThan(.15f), "Player can enter the chamber");
        yield return Walk(capsule, approach);
        yield return Walk(capsule, new Vector3(-22,0,-9.5f));
        yield return Walk(capsule, new Vector3(-22,0,-23));
        yield return Walk(capsule, arrival.transform.position);
        yield return Seconds(1);
        Assert.That(pod.Openness, Is.Zero);
        // The original forward route and vertical arrival segment must remain clear of the new pod.
        var route = arrival.transform.position + arrival.transform.forward * 2;
        yield return Walk(capsule, route);
        Assert.That(player.GetComponent<BeamTransportController>().IsSegmentClear(
            arrival.transform.position + Vector3.up * 3.5f, arrival.transform.position), Is.True);
        LogAssert.NoUnexpectedReceived();
    }

    private static Transform Reference(SerializedObject data, string field) =>
        (Transform)data.FindProperty(field).objectReferenceValue;

    private static bool EntranceBlocked() => Physics.CapsuleCast(
        new Vector3(0,.7f,1.6f), new Vector3(0,1.8f,1.6f), .25f,
        Vector3.back, 1.6f, ~0, QueryTriggerInteraction.Ignore);

    private static IEnumerator Walk(CharacterController capsule, Vector3 destination)
    {
        double deadline = EditorApplication.timeSinceStartup + 12;
        while (EditorApplication.timeSinceStartup < deadline)
        {
            Foreground();
            Vector3 delta = destination - capsule.transform.position;
            delta.y = 0;
            if (delta.magnitude < .1f) yield break;
            capsule.Move(Vector3.ClampMagnitude(delta, 3 * Time.deltaTime) + Vector3.down * (3 * Time.deltaTime));
            yield return EditorTestFrame.Next();
        }
        Assert.Fail($"Player route blocked: {capsule.transform.position} -> {destination}");
    }

    private static IEnumerator Seconds(float seconds)
    {
        float elapsed = 0;
        double deadline = EditorApplication.timeSinceStartup + seconds + 15;
        while (elapsed < seconds && EditorApplication.timeSinceStartup < deadline)
        {
            Foreground();
            yield return EditorTestFrame.Next();
            elapsed += Time.deltaTime;
        }
        Assert.That(elapsed, Is.GreaterThanOrEqualTo(seconds), "Player-loop clock did not advance");
    }

    private static void Foreground()
    {
        if (Time.timeScale != 0) return;
        var run = ActiveRunController.Instance;
        if (run != null)
        {
            run.SendMessage("OnApplicationPause", false);
            run.SendMessage("OnApplicationFocus", true);
        }
        var menu = Object.FindAnyObjectByType<InGameMenuController>();
        if (menu != null && menu.IsOpen) menu.ResumeGame();
    }
}
