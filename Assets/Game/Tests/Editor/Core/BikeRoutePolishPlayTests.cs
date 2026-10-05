using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public sealed partial class BikeRouteChasePlayTests
{
    [UnityTest]
    public IEnumerator BreakableSignsUseSpeedThresholdLaunchAndPersistWithoutReplaying()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return SignContract(); }
    private static IEnumerator SignContract()
    {
        Director.enabled = false; SuppressLasers(); yield return Mount();
        var sign = Object.FindObjectsByType<BikeRouteBreakableSign>().First();
        string id = sign.GetComponent<RunWorldObject>().Id;
        string intact = sign.CaptureRunState();
        Bike.Body.linearVelocity = Bike.transform.forward * (Bike.maxSpeed * .59f);
        Assert.That(sign.TryBreak(Bike), Is.False);
        Bike.Body.linearVelocity = Bike.transform.forward * (Bike.maxSpeed * .61f);
        float speed = Bike.Body.linearVelocity.magnitude;
        Assert.That(sign.TryBreak(Bike), Is.True);
        Assert.That(sign.signBody.isKinematic, Is.False);
        Assert.That(Vector3.Dot(sign.signBody.linearVelocity, Bike.transform.forward), Is.GreaterThan(speed * .4f));
        Assert.That(Bike.Body.linearVelocity.magnitude, Is.EqualTo(speed * .92f).Within(.05f));
        Assert.That(sign.TryBreak(Bike), Is.False);
        string broken = sign.CaptureRunState();
        sign.RestoreRunState(broken); sign.RestoreRunState(broken);
        Assert.That(sign.signBody.gameObject.activeSelf, Is.False);
        Assert.That(sign.approach.enabled, Is.False);
        Assert.That(Bike.Body.linearVelocity.magnitude, Is.EqualTo(speed * .92f).Within(.05f));
        sign.RestoreRunState(intact); sign.RestoreRunState(intact);
        Assert.That(sign.signBody.gameObject.activeSelf && sign.approach.enabled && sign.signBody.isKinematic, Is.True);
        sign.RestoreRunState(broken);
        Input.MoveInput(Vector2.zero); yield return Seconds(1);
        var run = ActiveRunController.Instance; Assert.That(run.Save(), Is.True);
        run.PrepareToLeave(); Assert.That(RunSaveService.Continue(), Is.True, RunSaveService.LastError);
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 20, "Sign Continue", foreground: false);
        var restored = Object.FindObjectsByType<BikeRouteBreakableSign>().Single(s => s.GetComponent<RunWorldObject>().Id == id);
        Assert.That(restored.Broken, Is.True); Assert.That(restored.signBody.gameObject.activeSelf, Is.False);
        Assert.That(restored.approach.enabled, Is.False); Assert.That(Time.timeScale, Is.Zero);
        Complete();
    }
    [UnityTest]
    public IEnumerator PhysicalSignImpactLaunchesAndMountedFeedbackIsRenderOnly()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return PhysicalSign(); }
    private static IEnumerator PhysicalSign()
    {
        Director.enabled = false; SuppressLasers(); yield return Mount();
        var sign = Object.FindObjectsByType<BikeRouteBreakableSign>().First();
        sign.transform.SetPositionAndRotation(Bike.transform.position + Bike.transform.forward * 28 - Vector3.up * .8f,
            Quaternion.LookRotation(Bike.transform.forward));
        Physics.SyncTransforms();
        var impact = Bike.GetComponent<BikeRouteImpactFeedback>();
        var feedback = Object.FindAnyObjectByType<CameraFeedbackController>();
        CameraFeedbackSettings.Enabled = true;
        // Batch Play Mode does not own desktop focus. Simulate the same foreground
        // lifecycle as the existing feedback tests; production must still reject background hits.
        feedback.SendMessage("OnApplicationFocus", true);
        feedback.SendMessage("OnApplicationPause", false);
        Assert.That((bool)typeof(CameraFeedbackController).GetProperty("CanPlay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(feedback),
            Is.True, "Mounted foreground eligibility before real collision");
        Input.MoveInput(Vector2.up);
        yield return Until(() => sign.Broken, 5, "Real bike trigger breaks the sign");
        Assert.That(impact.AcceptedImpacts, Is.GreaterThan(0));
        Assert.That(Vector3.Dot(sign.signBody.linearVelocity, Bike.transform.forward), Is.GreaterThan(10));
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        int count = (int)typeof(CameraFeedbackController).GetField("count", flags).GetValue(feedback);
        Assert.That(count, Is.GreaterThan(0), "Mounted player is eligible for existing render feedback");
        var camera = feedback.GetComponent<Camera>();
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        typeof(CameraFeedbackController).GetMethod("BeginRender", flags).Invoke(feedback, new object[] { default(ScriptableRenderContext), camera });
        Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.GreaterThan(.01f));
        typeof(CameraFeedbackController).GetMethod("EndRender", flags).Invoke(feedback, new object[] { default(ScriptableRenderContext), camera });
        Assert.That(Vector3.Distance(camera.transform.position, position), Is.LessThan(.0001f));
        Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(.001f));
        yield return Seconds(.15f);
        ProceduralUIReview.Capture("chase-v42-sign-flight", 1280, 720);
        int accepted = impact.AcceptedImpacts;
        impact.Present(30, Vector3.right); Assert.That(impact.AcceptedImpacts, Is.EqualTo(accepted), "Debounce contact spam");
        yield return Seconds(.4f); impact.Present(.5f, Vector3.right);
        Assert.That(impact.AcceptedImpacts, Is.EqualTo(accepted), "Small impacts stay quiet");
        yield return Seconds(5); Assert.That(sign.signBody.gameObject.activeSelf, Is.False);
        Complete();
    }
}
