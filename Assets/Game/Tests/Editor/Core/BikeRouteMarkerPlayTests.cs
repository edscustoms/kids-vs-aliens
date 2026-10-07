using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class BikeRouteChasePlayTests
{
    [UnityTest, Timeout(420000)]
    public IEnumerator ShortcutMarkersBreakOnContactAndContinueKeepsThemBroken()
    {
        Setup(); yield return new EnterPlayMode(); yield return Initialize();
        yield return ShortcutMarkerBody();
    }

    // Create captured locals after the Play Mode domain reload, as other persistence
    // tests do; Unity cannot carry a compiler-generated closure across EnterPlayMode.
    private static IEnumerator ShortcutMarkerBody()
    {
        Director.enabled = false; SuppressLasers(); yield return Mount();
        Object.FindAnyObjectByType<BikeRouteContainment>().enabled = false;
        var signs = GameObject.Find("LevelGeometry/Readable boundaries").GetComponentsInChildren<BikeRouteBreakableSign>();
        const string intact = "{\"broken\":false}";
        foreach (var sign in signs)
        {
            Bike.Body.linearVelocity = Vector3.forward * Bike.maxSpeed * .59f;
            Assert.That(sign.TryBreak(Bike), Is.False);
            float speed = Bike.maxSpeed * .61f;
            Bike.Body.linearVelocity = Vector3.forward * speed;
            Assert.That(sign.TryBreak(Bike), Is.True);
            Assert.That(Bike.Body.linearVelocity.magnitude, Is.EqualTo(speed*.92f).Within(.01f));
            sign.RestoreRunState(intact);
            sign.RestoreRunState(intact);
        }
        // Isolate authored pole contacts on a flat test platform. The mounted bike,
        // sign component, collider and Rigidbody are real and remain unconstrained.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Temporary marker contact floor";
        floor.transform.position = new Vector3(200,249.5f,155);
        floor.transform.localScale = new Vector3(80,1,80);
        foreach (var sign in new[] { signs.First(s=>s.name.StartsWith("Shortcut 8")), signs.First(s=>s.name.StartsWith("Shortcut 9")) })
        {
            var home = sign.transform.position; var rotation = sign.transform.rotation;
            foreach (bool turbo in new[] { false,true })
            {
                sign.RestoreRunState(intact);
                sign.transform.SetPositionAndRotation(new Vector3(200,250,155),Quaternion.identity);
                Bike.Body.position = sign.transform.position - Vector3.forward*8 + Vector3.up*.85f;
                Bike.Body.rotation = Quaternion.identity;
                Bike.Body.linearVelocity = Vector3.zero; Bike.Body.angularVelocity = Vector3.zero;
                Input.MoveInput(Vector2.zero); Input.SprintInput(false);
                Physics.SyncTransforms(); yield return Seconds(.5f);
                float speed = turbo ? Bike.turboMaxSpeed : Bike.maxSpeed;
                Bike.Body.linearVelocity = Vector3.forward*speed;
                Input.MoveInput(Vector2.up); Input.SprintInput(turbo);
                yield return Until(()=>sign.Broken,1.5f,"Physical shortcut marker contact");
                Assert.That(sign.signBody.linearVelocity.z,Is.GreaterThan(speed*.35f));
                Assert.That(Bike.Body.linearVelocity.z,Is.GreaterThan(speed*.7f),"No dead stop");
                Assert.That(Bike.Body.linearVelocity.y,Is.LessThan(4),"No catapult");
                Assert.That(Bike.Speed,Is.LessThan(speed*1.1f),"No energy spike");
                ProceduralUIReview.Capture($"cleanup-marker-impact-{sign.name}-{turbo}",1440,900,true);
                sign.RestoreRunState(intact);
            }
            sign.transform.SetPositionAndRotation(home,rotation);
        }
        Object.Destroy(floor);
        var target = signs.First();
        string id = target.GetComponent<RunWorldObject>().Id;
        // Finish at an authored pose before testing the real save/Continue lifecycle.
        var at = Director.Guide.At(8,54);
        Bike.Body.position = at.position+Vector3.up*.85f;
        Bike.Body.rotation = Quaternion.LookRotation(at.forward);
        Bike.Body.linearVelocity = Vector3.forward*Bike.maxSpeed;
        Assert.That(target.TryBreak(Bike),Is.True);
        target.RestoreRunState("{\"broken\":true}");
        Input.MoveInput(Vector2.zero); Input.SprintInput(false);
        Bike.Body.linearVelocity = Vector3.zero;
        Physics.SyncTransforms(); yield return Seconds(1);
        var run = ActiveRunController.Instance;
        Assert.That(run, Is.Not.Null, "Active run before save");
        Assert.That(run.Save(),Is.True);
        run.PrepareToLeave(); Assert.That(RunSaveService.Continue(),Is.True,RunSaveService.LastError);
        yield return Until(()=>ActiveRunController.Instance!=null&&ActiveRunController.Instance.IsReady,20,"Marker Continue",foreground:false);
        var restored = Object.FindObjectsByType<BikeRouteBreakableSign>().Single(s=>s.GetComponent<RunWorldObject>().Id==id);
        Assert.That(restored.Broken,Is.True);
        Assert.That(restored.signBody.gameObject.activeSelf,Is.False);
        Assert.That(restored.approach.enabled,Is.False);
        Assert.That(Time.timeScale,Is.Zero);
        Complete();
    }
}
