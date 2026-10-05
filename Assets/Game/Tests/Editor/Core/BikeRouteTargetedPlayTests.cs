using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed partial class BikeRouteChasePlayTests
{
    [UnityTest, Timeout(240000)]
    public IEnumerator EveryRoadSignRejectsSlowTouchAndBreaksOnPhysicalImpact()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return AllSignsBody(); }

    private static IEnumerator AllSignsBody()
    {
        Director.enabled = false; SuppressLasers();
        Object.FindAnyObjectByType<BikeRouteContainment>().enabled = false;
        yield return Mount();
        var signs = Object.FindObjectsByType<BikeRouteBreakableSign>().OrderBy(s => s.GetComponent<RunWorldObject>().Id).ToArray();
        Directory.CreateDirectory("Logs/BikeRouteTargeted");
        using var log = new StreamWriter("Logs/BikeRouteTargeted/sign-contacts.txt");
        // Keep each authored sign's scale, body and colliders. Isolate contact above
        // scenery, using the real mounted Rigidbody and trigger callbacks.
        Bike.Body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        var touch=Bike.gameObject.AddComponent<ChaseContactProbe>();
        foreach (var sign in signs)
        {
            string intact = sign.CaptureRunState();
            Bike.Body.linearVelocity = Vector3.forward * Bike.maxSpeed * .59f;
            Assert.That(sign.TryBreak(Bike), Is.False, sign.name + " low-speed threshold");
            float thresholdSpeed=Bike.maxSpeed*.61f;
            Bike.Body.linearVelocity=Vector3.forward*thresholdSpeed;
            Assert.That(sign.TryBreak(Bike),Is.True,sign.name+" threshold");
            Assert.That(Bike.Body.linearVelocity.magnitude,Is.EqualTo(thresholdSpeed*.92f).Within(.01f),sign.name+" modest speed loss");
            sign.RestoreRunState(intact);
            sign.transform.SetPositionAndRotation(new Vector3(200,250,155),Quaternion.identity);
            Bike.Body.position=sign.transform.position+Vector3.up*.85f-Vector3.forward*3.2f;
            Bike.Body.rotation=Quaternion.identity;Bike.Body.angularVelocity=Vector3.zero;
            Bike.Body.linearVelocity=Vector3.forward*Bike.maxSpeed*.4f;Input.MoveInput(Vector2.zero);
            Physics.SyncTransforms();touch.Surfaces.Clear();
            yield return Seconds(.45f);
            Assert.That(sign.Broken,Is.False,sign.name+" low-speed physical touch");
            Assert.That(touch.Surfaces.Contains(sign.signBody.name),Is.True,sign.name+" actual low-speed post contact");
            Bike.Body.position=sign.transform.position+Vector3.up*.85f+Vector3.right*2.6f-Vector3.forward*8;
            Bike.Body.rotation=Quaternion.identity;Bike.Body.angularVelocity=Vector3.zero;
            Bike.Body.linearVelocity=Vector3.forward*Bike.maxSpeed;Input.MoveInput(Vector2.up);
            Physics.SyncTransforms();yield return Seconds(.6f);
            Assert.That(sign.Broken,Is.False,sign.name+" parallel near miss must not break");
            log.WriteLine(sign.name+" slowContact=True nearMissIntact=True");log.Flush();
            foreach (var approach in new[] { Vector3.forward, Vector3.right, Vector3.back })
            {
                sign.RestoreRunState(intact);
                sign.transform.SetPositionAndRotation(new Vector3(200,250,155), Quaternion.identity);
                Bike.Body.position = sign.transform.position + Vector3.up * .85f - approach * 8;
                Bike.Body.rotation = Quaternion.LookRotation(approach);
                Bike.Body.linearVelocity = Vector3.zero; Bike.Body.angularVelocity = Vector3.zero;
                Input.MoveInput(Vector2.zero); Physics.SyncTransforms(); yield return Seconds(.1f);
                float speed = Bike.turboMaxSpeed;
                Bike.Body.linearVelocity = approach * speed;
                Input.MoveInput(Vector2.up);
                yield return Until(() => sign.Broken, 1.5f, sign.name + " physical approach " + approach);
                float launch = Vector3.Dot(sign.signBody.linearVelocity,approach);
                log.WriteLine($"{sign.name} direction={approach} speed={speed:F2} broken={sign.Broken} launch={launch:F2}"); log.Flush();
                Assert.That(launch, Is.GreaterThan(speed*.35f), "Launch follows actual bike travel");
                Assert.That(sign.signBody.isKinematic, Is.False);
                Assert.That(sign.approach.enabled, Is.False);
                Assert.That(Bike.Body.linearVelocity.magnitude, Is.LessThan(speed+2), "No bike speed gain from sign");
                sign.RestoreRunState("{\"broken\":true}");
            }
        }
        Input.MoveInput(Vector2.zero); Complete();
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator GreenEntranceDrivesForwardAndBackward()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return GreenEntranceBody(); }

    private static IEnumerator GreenEntranceBody()
    {
        Director.enabled=false;SuppressLasers();yield return Mount();
        Directory.CreateDirectory("Logs/BikeRouteTargeted");
        using var log=new StreamWriter("Logs/BikeRouteTargeted/entrance-driving.txt");
        foreach(int direction in new[]{1,-1})
        {
            var start=direction>0 ? Director.Guide.At(2,Director.Guide.paths[2].Length-30) : Director.Guide.At(3,125);
            Bike.Body.position=start.position+Vector3.up*.85f;
            Bike.Body.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(start.forward*direction,Vector3.up));
            Bike.Body.linearVelocity=Vector3.zero;Bike.Body.angularVelocity=Vector3.zero;Input.MoveInput(Vector2.zero);
            Physics.SyncTransforms();yield return Seconds(.4f);
            bool finished=false;float next=direction>0 ? 0 : 110;float maxOffset=0;
            yield return Until(()=>finished,25,"Drive green entrance "+direction,()=>
            {
                var a=ProjectCleanupPath(Bike.Body.position,2);var b=ProjectCleanupPath(Bike.Body.position,3);
                var at=(a.position-Bike.Body.position).sqrMagnitude<(b.position-Bike.Body.position).sqrMagnitude?a:b;
                var ahead=Director.Guide.Ahead(at,direction*(8+Bike.Speed*.3f),direction>0?3:2);
                float turn=Vector3.SignedAngle(Vector3.ProjectOnPlane(Bike.transform.forward,Vector3.up),Vector3.ProjectOnPlane(ahead.position-Bike.Body.position,Vector3.up),Vector3.up);
                Input.MoveInput(new Vector2(Mathf.Clamp(turn/30,-1,1),Bike.Speed<24?1:0));
                maxOffset=Mathf.Max(maxOffset,Mathf.Abs(Vector3.Dot(Bike.Body.position-at.position,at.Right)));
                finished=direction>0 ? at.path==3 && at.distance>125 : at.path==2 && at.distance<Director.Guide.paths[2].Length-25;
                if(at.path==3 && (at.distance-next)*direction>=0)
                {
                    ProceduralUIReview.Capture($"green-entrance-{direction}-{next}",1280,720);next+=direction*22;
                }
            });
            log.WriteLine($"direction={direction} complete={finished} maxOffset={maxOffset:F2}");log.Flush();
            Assert.That(maxOffset,Is.LessThan(4));
        }
        Input.MoveInput(Vector2.zero);Complete();
    }
}
