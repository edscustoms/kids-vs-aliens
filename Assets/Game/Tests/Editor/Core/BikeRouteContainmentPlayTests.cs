using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class BikeRouteChasePlayTests
{
    [UnityTest, Timeout(180000)]
    public IEnumerator BridgeSideEscapeRedirectsWithoutSpeedLossAndInLaneFlightStaysFree()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return BridgeContainmentBody(); }
    private static IEnumerator BridgeContainmentBody()
    {
        Director.enabled = false; Laser.enabled = false; yield return Mount();
        var safety = Director.GetComponent<BikeRouteContainment>();
        var sample = Director.Guide.At(5, safety.flightStart + 65);
        Vector3 forward = Vector3.ProjectOnPlane(sample.forward, Vector3.up).normalized;
        Input.MoveInput(Vector2.up); yield return Seconds(.1f);
        var right = sample.Right;
        Bike.Body.position = sample.position + Vector3.up * 12;
        Bike.Body.rotation = Quaternion.LookRotation(forward); Bike.Body.linearVelocity = forward * 60;
        Physics.SyncTransforms(); yield return Seconds(.18f);
        Assert.That(safety.Redirecting, Is.False, "Normal jump lane remains unconstrained");
        Bike.Body.position = sample.position + right * (sample.halfWidth + safety.flightMargin + .7f) + Vector3.up * 12;
        Bike.Body.linearVelocity = forward * 56 + right * 24;
        // Seed a real diagonal launch, with chassis and momentum aligned. A side-slip
        // injected into a straight chassis measures ordinary grip braking as well as safety.
        Bike.Body.rotation = Quaternion.LookRotation(Bike.Body.linearVelocity.normalized);
        Physics.SyncTransforms(); bool redirected = false; float minSpeed = Bike.Body.linearVelocity.magnitude;
        float began = Time.time;
        while (Time.time - began < .75f)
        {
            Input.MoveInput(Vector2.up);
            redirected |= safety.Redirecting; minSpeed = Mathf.Min(minSpeed, Bike.Body.linearVelocity.magnitude);
            yield return EditorTestFrame.Next(); Foreground();
        }
        Assert.That(redirected, Is.True); Assert.That(minSpeed, Is.GreaterThan(50), "Safety turns velocity, never brakes to zero");
        var road = Director.Guide.Project(Bike.Body.position, 5);
        Assert.That(Vector3.Dot(Bike.Body.linearVelocity, road.Right), Is.LessThan(0), "Outward flight redirected toward road");
        Directory.CreateDirectory("Logs/BikeRouteEnvironmentV2");
        File.WriteAllText("Logs/BikeRouteEnvironmentV2/bridge-safety.txt", "minimumSpeed="+minSpeed+" lane="+Vector3.Dot(Bike.Body.position-road.position,road.Right));
        Complete();
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator LargeRouteEscapeWarnsThenDamagesAndReturningCancelsDefense()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return EscapeDefenseBody(); }
    private static IEnumerator EscapeDefenseBody()
    {
        Director.enabled = false; Laser.enabled = false; yield return Mount();
        var safety = Director.GetComponent<BikeRouteContainment>(); var health = Player.GetComponent<PlayerHealth>();
        var sample = Director.Guide.At(0, 160);
        Bike.Body.constraints = RigidbodyConstraints.FreezeAll;
        Bike.Body.position = sample.position + sample.Right * 95 + Vector3.up * 5;
        Physics.SyncTransforms(); string notice = null; Action<string> onNotice = text => notice = text; RunSaveService.Feedback += onNotice;
        try
        {
            float before = health.CurrentHealth + health.CurrentArmor;
            yield return Until(() => safety.OutsideRoute, 3, "Large authored-route escape detected");
            yield return Seconds(1);
            Assert.That(notice, Does.Contain("OFF ROUTE")); Assert.That(health.CurrentHealth + health.CurrentArmor, Is.EqualTo(before));
            yield return Until(() => health.CurrentHealth + health.CurrentArmor < before, 4, "Warned defense starts");
            Bike.Body.position = sample.position + Vector3.up;
            Physics.SyncTransforms(); yield return Seconds(.3f);
            float returnedHealth = health.CurrentHealth + health.CurrentArmor;
            yield return Seconds(1);
            Assert.That(safety.OutsideRoute, Is.False); Assert.That(safety.OutsideSeconds, Is.Zero);
            Assert.That(health.CurrentHealth + health.CurrentArmor, Is.EqualTo(returnedHealth));
            Bike.Body.position = sample.position + sample.Right * 95 + Vector3.up * 5; Physics.SyncTransforms();
            yield return Until(() => health.IsDead, 8, "Sustained escape invokes existing death/restart owner");
        }
        finally { RunSaveService.Feedback -= onNotice; }
        Complete();
    }

    [UnityTest, Timeout(420000)]
    public IEnumerator IgnoringChaseAtFullThrottleProducesRepeatedPressureAndDamage()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return DemandingChaseBody(); }
    private static IEnumerator DemandingChaseBody()
    {
        Laser.enabled = false; yield return Mount();
        var health = Player.GetComponent<PlayerHealth>(); var riders = Director.WaveOne.Select(s => s.rider).ToArray();
        var guns = riders.Select(r => r.GetComponent<AlienBikeLaserWeapon>()).ToArray();
        int ramHits = 0, boltHits = 0; float previousHealth = health.CurrentHealth + health.CurrentArmor;
        Action recordHit = () => {
            float current = health.CurrentHealth + health.CurrentArmor, delta = previousHealth-current;
            if (Mathf.Abs(delta-14) < .01f) ramHits++;
            if (Mathf.Abs(delta-30) < .01f) boltHits++;
            previousHealth=current;
        };
        health.OnHealthChanged += recordHit;
        float began = Time.time, empty = 0, longestEmpty = 0;
        while (Time.time-began < 180 && !health.IsDead && !Director.Finished)
        {
            DrivePlayerRoute(float.PositiveInfinity); Input.SprintInput(false);
            if (riders.Any(r => r.HasActivated && r.DistanceToPlayer < 30)) empty=0; else empty+=Time.deltaTime;
            longestEmpty = Mathf.Max(empty,longestEmpty);
            yield return EditorTestFrame.Next(); Foreground();
        }
        health.OnHealthChanged -= recordHit;
        float damage = 150-health.CurrentHealth-health.CurrentArmor;
        string metrics = "seconds="+(Time.time-began)+" damage="+damage+" dead="+health.IsDead
            +" attacks="+riders.Sum(r=>r.AttackAttempts)+" leadWindows="+riders.Sum(r=>r.LeadWindows)
            +" shots="+guns.Sum(g=>g.ShotsFired)+" ramHits="+ramHits+" boltHits="+boltHits+" longestEmpty="+longestEmpty;
        Directory.CreateDirectory("Logs/BikeRouteEnvironmentV2"); File.WriteAllText("Logs/BikeRouteEnvironmentV2/ignore-chase.txt",metrics); Debug.Log(metrics);
        Assert.That(health.IsDead, Is.True, "Ignoring all combat without turbo should not complete the route");
        Assert.That(ramHits, Is.GreaterThan(0), "Real committed contact carries modest damage");
        Assert.That(riders.Sum(r=>r.AttackAttempts)+riders.Sum(r=>r.LeadWindows), Is.GreaterThanOrEqualTo(3));
        Assert.That(longestEmpty, Is.LessThan(18), "No long free run after losing riders");
        Assert.That(Bike.maxSpeed, Is.EqualTo(40.6f)); Assert.That(Bike.turboMaxSpeed, Is.EqualTo(70.1f));
        Complete();
    }
}
