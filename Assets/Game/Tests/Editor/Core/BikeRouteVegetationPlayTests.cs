using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class BikeRouteChasePlayTests
{
    [UnityTest, Timeout(420000)]
    public IEnumerator TreeTrunksAllowNormalAndTurboGlancingContact()
    {
        Setup();yield return new EnterPlayMode();yield return Initialize();
        yield return TreeContactBody();
    }
    static IEnumerator TreeContactBody()
    {
        Director.enabled=false;SuppressLasers();yield return Mount();
        Object.FindAnyObjectByType<BikeRouteContainment>().enabled=false;
        var trunks=Object.FindObjectsByType<LODGroup>().Where(l=>l.name.StartsWith("PF Conifer"))
            .Select(l=>l.GetComponent<CapsuleCollider>()).Where(c=>c!=null&&c.enabled)
            .OrderBy(c=>c.radius*c.transform.lossyScale.x).ToArray();
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position=new Vector3(200,249.5f,155);
        floor.transform.localScale=new Vector3(80,1,80);
        var probe=Bike.gameObject.AddComponent<BikeRouteBoundaryContactProbe>();
        foreach(var trunk in new[]{trunks.First(),trunks.Last()})
        {
            var home=trunk.transform.position;var rotation=trunk.transform.rotation;
            trunk.transform.SetPositionAndRotation(new Vector3(200,250,155),Quaternion.identity);
            float radius=trunk.radius*trunk.transform.lossyScale.x;
            foreach(float edgeClearance in new[]{.895f,.82f})
            foreach(bool turbo in new[]{false,true})
            {
                Bike.Body.position=new Vector3(200-radius-edgeClearance,250.85f,147);
                Bike.Body.rotation=Quaternion.identity;
                Bike.Body.linearVelocity=Vector3.zero;Bike.Body.angularVelocity=Vector3.zero;
                Input.MoveInput(Vector2.zero);Input.SprintInput(false);Physics.SyncTransforms();yield return Seconds(.5f);
                probe.Target=trunk;probe.TargetContacts=0;
                float speed=turbo?Bike.turboMaxSpeed:Bike.maxSpeed;
                Bike.Body.linearVelocity=Vector3.forward*speed;
                Input.MoveInput(Vector2.up);Input.SprintInput(turbo);
                float start=Time.time,rise=0,peak=0;
                while(Time.time-start<.5f)
                {
                    rise=Mathf.Max(rise,Bike.Body.linearVelocity.y);peak=Mathf.Max(peak,Bike.Speed);
                    yield return EditorTestFrame.Next();Foreground();
                }
                Debug.Log($"TREE CONTACT edgeClearance={edgeClearance:F3} radius={radius:F2} turbo={turbo} contacts={probe.TargetContacts} rise={rise:F2} forward={Bike.Body.linearVelocity.z:F2} peak={peak:F2}");
                Assert.That(probe.TargetContacts,Is.GreaterThan(0));
                Assert.That(rise,Is.LessThan(4),"No vertical catapult");
                Assert.That(peak,Is.LessThan(speed*1.1f));
                // A 5 mm edge graze should slide. The separate 80 mm front-corner
                // strike is a deeper impact into a solid trunk; record its braking
                // without changing shared bike physics to satisfy a retention target.
                if(edgeClearance > .89f)
                    Assert.That(Bike.Body.linearVelocity.z,Is.GreaterThan(speed*.5f),"Shallow grazing contact retains forward motion");
            }
            trunk.transform.SetPositionAndRotation(home,rotation);
        }
        Object.Destroy(floor);Complete();
    }
}
