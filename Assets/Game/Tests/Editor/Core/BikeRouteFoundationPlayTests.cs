using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;

public sealed partial class BikeRouteChasePlayTests
{
    [UnityTest, Timeout(480000)]
    public IEnumerator FoundationNormalAndTurboChaseViews()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return FoundationDrivingBody(); }

    private static IEnumerator FoundationDrivingBody()
    {
        // Keep the opening opponents alive for the readability review.
        Bike.GetComponent<AlienBikeLaserWeapon>().enabled=false;
        yield return Mount();
        float began=Time.time,nextCapture=10,normalPeak=0,turboPeak=0;
        bool warningCaptured=false;
        yield return Until(()=>Time.time-began>=40,55,"Normal/turbo visual drive",()=>
        {
            float age=Time.time-began;
            DrivePlayerRoute(float.PositiveInfinity);Input.SprintInput(age>=18);HealPlayer();
            if(age<18) normalPeak=Mathf.Max(normalPeak,Bike.Speed);else turboPeak=Mathf.Max(turboPeak,Bike.Speed);
            if(age>=nextCapture)
            {
                ProceduralUIReview.Capture("foundation-driving-"+nextCapture,1440,900,true);nextCapture+=10;
            }
            if(!warningCaptured)
                foreach(var slot in Director.WaveOne)
                    if(slot.rider.GetComponent<AlienBikeLaserWeapon>().Progress>.6f)
                    {ProceduralUIReview.Capture("foundation-driving-warning",1440,900,true);warningCaptured=true;break;}
        });
        Input.MoveInput(Vector2.zero);Input.SprintInput(false);
        Debug.Log($"Foundation driving normalPeak={normalPeak:F2} turboPeak={turboPeak:F2} incomingWarning={warningCaptured}");
        Assert.That(normalPeak,Is.GreaterThan(25));Assert.That(turboPeak,Is.GreaterThan(45));
        Complete();
    }

    [UnityTest, Timeout(240000)]
    public IEnumerator FoundationFixedGameplayViews()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return FoundationViewsBody(); }

    private static IEnumerator FoundationViewsBody()
    {
        Director.enabled=false;SuppressLasers();yield return Mount();
        Bike.Body.constraints=RigidbodyConstraints.FreezeAll;
        Assert.That(Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing,Is.True);
        foreach(var renderer in GameObject.Find("LevelGeometry/Environment Art/Desert roadside settlement").GetComponentsInChildren<Renderer>())
            foreach(var material in renderer.sharedMaterials)
                if(material.name=="Warm Windows")
                    Assert.That(material.IsKeywordEnabled("_EMISSION"),Is.True,"URP import must retain window emission");
        foreach(var spot in new[]{(0,370f),(1,530f),(3,250f),(4,250f),(5,390f),(0,110f)})
        foreach(int direction in new[]{1,-1})
        {
            var at=Director.Guide.At(spot.Item1,spot.Item2);
            Bike.Body.position=at.position+Vector3.up*.85f;
            Bike.Body.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(at.forward*direction,Vector3.up));
            Physics.SyncTransforms();
            // Shader warm-up can stall the first rendered frame in batch mode.
            // Settle the camera over frames instead of the gameplay timeout helper.
            for (int frame=0;frame<120;frame++) { Foreground(); yield return EditorTestFrame.Next(); }
            ProceduralUIReview.Capture($"foundation-{spot.Item1}-{spot.Item2}-{direction}",1440,900,true);
        }
        Complete();
    }
}
