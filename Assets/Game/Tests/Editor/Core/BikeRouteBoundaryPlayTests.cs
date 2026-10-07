using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class BikeRouteChasePlayTests
{
    [UnityTest, Timeout(420000)]
    public IEnumerator CrossingBoundaryContactsAtNormalAndTurboSpeed()
    {
        Setup(); yield return new EnterPlayMode(); yield return Initialize();
        Director.enabled=false; SuppressLasers(); yield return Mount();
        var probe=Bike.gameObject.AddComponent<BikeRouteBoundaryContactProbe>();
        var root=GameObject.Find("LevelGeometry/Readable boundaries");
        var walls=root.GetComponentsInChildren<MeshCollider>();
        Directory.CreateDirectory("Logs/BikeRouteBoundaries");
        using var log=new StreamWriter("Logs/BikeRouteBoundaries/contacts.txt");
        var results=new List<(int contacts,float rise,float peak,float travel,float endSpeed,float speed)>();
        foreach(int direction in new[]{1,-1}) foreach(int side in new[]{-1,1}) foreach(bool turbo in new[]{false,true})
        {
            var at=Director.Guide.At(5,direction==1?330:480);
            RaycastHit hit=default;bool found=false;
            foreach(var wall in walls)
                if(wall.Raycast(new Ray(at.position+Vector3.up*.7f,at.Right*side),out hit,12)){found=true;break;}
            Assert.That(found,Is.True);
            var inward=Vector3.ProjectOnPlane(hit.normal,Vector3.up).normalized;
            var forward=Vector3.Cross(Vector3.up,inward).normalized;
            if(Vector3.Dot(forward,at.forward)*direction<0)forward=-forward;
            var approach=(forward*.97f-inward*.24f).normalized;
            Input.MoveInput(Vector2.zero);Input.SprintInput(false);
            Bike.Body.position=hit.point+inward*2.8f;
            Bike.Body.rotation=Quaternion.LookRotation(approach);
            Bike.Body.linearVelocity=Vector3.zero;Bike.Body.angularVelocity=Vector3.zero;
            Physics.SyncTransforms();yield return Seconds(.65f);
            float speed=turbo?Bike.turboMaxSpeed:Bike.maxSpeed;
            int contacts=probe.Contacts;float rise=0,peak=0;
            var start=Bike.Body.position;float began=Time.time;
            Bike.Body.linearVelocity=approach*speed;Input.MoveInput(Vector2.up);Input.SprintInput(turbo);
            while(Time.time-began<.75f)
            {
                rise=Mathf.Max(rise,Bike.Body.linearVelocity.y);
                peak=Mathf.Max(peak,Bike.Body.linearVelocity.magnitude);
                yield return EditorTestFrame.Next();Foreground();
            }
            float travel=Vector3.Dot(Bike.Body.position-start,forward);
            float endSpeed=Vector3.Dot(Bike.Body.linearVelocity,forward);
            log.WriteLine($"direction={direction} side={side} turbo={turbo} contacts={probe.Contacts-contacts} rise={rise:F2} peak={peak:F2} travel={travel:F2} forwardSpeed={endSpeed:F2}");log.Flush();
            results.Add((probe.Contacts-contacts,rise,peak,travel,endSpeed,speed));
            ProceduralUIReview.Capture($"boundary-contact-{direction}-{side}-{turbo}",1440,900,true);
        }
        foreach(var r in results)
        {
            Assert.That(r.contacts,Is.GreaterThan(0),"The bike must actually contact the changed parapet");
            Assert.That(r.rise,Is.LessThan(5),"No vertical catapult");
            Assert.That(r.peak,Is.LessThan(r.speed*1.15f),"No collision energy spike");
            Assert.That(r.travel,Is.GreaterThan(r.speed*.4f),"Retain forward progress");
            Assert.That(r.endSpeed,Is.GreaterThan(r.speed*.5f),"Slide instead of a dead stop");
        }
        // Cross above the visible top with the real occupied bike. No tall collision
        // extension is permitted to catch an airborne rider.
        foreach(int side in new[]{-1,1}) foreach(bool turbo in new[]{false,true})
        {
            var at=Director.Guide.At(5,330);var forward=Vector3.ProjectOnPlane(at.forward,Vector3.up).normalized;
            var approach=(forward*.8f+at.Right*side*.6f).normalized;
            float speed=turbo?Bike.turboMaxSpeed:Bike.maxSpeed;
            Input.MoveInput(Vector2.up);Input.SprintInput(turbo);
            Bike.Body.position=at.position+at.Right*side*4+Vector3.up*5;
            Bike.Body.rotation=Quaternion.LookRotation(approach);Bike.Body.angularVelocity=Vector3.zero;
            Bike.Body.linearVelocity=approach*speed;Physics.SyncTransforms();
            int contacts=probe.Contacts;yield return Seconds(.25f);
            Assert.That(probe.Contacts,Is.EqualTo(contacts),"Airborne rider clears the visible parapet");
            Assert.That(Vector3.Dot(Bike.Body.linearVelocity,approach),Is.GreaterThan(speed*.6f));
            log.WriteLine($"airborne side={side} turbo={turbo} contacts=0 speed={Bike.Speed:F2}");log.Flush();
        }
        Complete();
    }
}
