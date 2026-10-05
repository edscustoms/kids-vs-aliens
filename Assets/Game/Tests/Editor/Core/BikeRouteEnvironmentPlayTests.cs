using System;
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
    public IEnumerator EnvironmentGameplayCameraReview()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return EnvironmentReviewBody(); }
    private static IEnumerator EnvironmentReviewBody()
    { Director.enabled = false; Laser.enabled = false;
        yield return Mount(); Bike.Body.constraints = RigidbodyConstraints.FreezeAll;
        foreach (var spot in new[] { (0, 115f), (1, 50f), (1, 220f), (1, 450f), (1, 650f), (1, 800f), (2, 240f), (3, 80f), (3, 330f), (4, 200f), (4, 340f), (4, 430f), (5, 410f), (6, 420f), (7, 130f) })
        {
            var sample = Director.Guide.At(spot.Item1, spot.Item2);
            Bike.Body.position = sample.position + Vector3.up * .85f;
            Bike.Body.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(sample.forward, Vector3.up)); Physics.SyncTransforms();
            float settledAt = Time.time + 2.5f;
            yield return Until(() => Time.time >= settledAt, 30, "Camera settles after section jump");
            ProceduralUIReview.Capture("environment-" + spot.Item1 + "-" + spot.Item2, 1280, 720);
        }
        Complete();
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator TurboRoadsideContactSlidesWithoutVerticalLaunch()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return TurboWallsBody(); }
    private static IEnumerator TurboWallsBody()
    { Director.enabled = false; Laser.enabled = false;
        yield return Mount(); var probe = Bike.gameObject.AddComponent<ChaseContactProbe>();
        Directory.CreateDirectory("Logs/BikeRouteEnvironment");
        using var log = new StreamWriter("Logs/BikeRouteEnvironment/wall-impacts.txt");
        foreach (var spot in new[] { (0, 420f), (1, 220f), (2, 200f), (5, 200f), (6, 330f) })
        {
            var sample = Director.Guide.At(spot.Item1, spot.Item2);
            float half = 0;
            var walls = GameObject.Find("LevelGeometry/Smooth Corridor Collision").GetComponentsInChildren<MeshCollider>();
            float nearest = float.PositiveInfinity;
            foreach (var wall in walls)
                if (wall.Raycast(new Ray(sample.position + Vector3.up * .86f, sample.Right), out var hit, 30)) nearest = Mathf.Min(nearest, hit.distance);
            Assert.That(nearest, Is.LessThan(30), "Authored roadside contact surface"); half = nearest;
            var forward = Vector3.ProjectOnPlane(sample.forward, Vector3.up).normalized;
            Bike.Body.position = sample.position + sample.Right * (half - 1.3f) + Vector3.up * .86f;
            Bike.Body.rotation = Quaternion.LookRotation(forward); Bike.Body.linearVelocity = Vector3.zero; Bike.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms(); yield return Seconds(.15f);
            Bike.Body.linearVelocity = forward * (Bike.turboMaxSpeed * .97f) + sample.Right * (Bike.turboMaxSpeed * .24f);
            Input.MoveInput(Vector2.up); int contacts = probe.WallContacts; float maximumRise = 0, maximumSpeed = 0;
            float began = Time.time; var start = Bike.Body.position;
            while (Time.time - began < .65f)
            {
                maximumRise = Mathf.Max(maximumRise, Bike.Body.linearVelocity.y - sample.forward.y * Bike.Speed);
                maximumSpeed = Mathf.Max(maximumSpeed, Bike.Body.linearVelocity.magnitude);
                yield return EditorTestFrame.Next(); Foreground();
            }
            log.WriteLine($"path={spot.Item1} contacts={probe.WallContacts - contacts} rise={maximumRise:F2} peak={maximumSpeed:F2} forward={Vector3.Dot(Bike.Body.position-start,forward):F2}"); log.Flush();
            Assert.That(probe.WallContacts, Is.GreaterThan(contacts), "Actual high-speed side collision " + spot.Item1);
            Assert.That(maximumRise, Is.LessThan(8), "No catapult at " + spot.Item1);
            Assert.That(maximumSpeed, Is.LessThan(Bike.turboMaxSpeed * 1.15f));
            Assert.That(Vector3.Dot(Bike.Body.position - start, forward), Is.GreaterThan(20), "Retain useful forward travel");
        }
        // Edge/landing contacts can have vertical normals; the authored face normals are
        // checked separately. This probe asserts actual launch/speed/forward-travel outcomes.
        Complete();
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator CrossoverJumpKeepsMovingThroughItsAirAndLandingCorridor()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return CrossoverFlightBody(); }
    private static IEnumerator CrossoverFlightBody()
    {
        Director.enabled = false; Laser.enabled = false; yield return Mount();
        var hint = Director.Guide.jumps.Single(j => j.path == 5);
        var start = Director.Guide.At(5, hint.releaseDistance + 30);
        // Isolate the reported airborne collision, independently of V1's slope-sensitive jump charge.
        Bike.Body.position = start.position + Vector3.up * 6;
        Bike.Body.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(start.forward, Vector3.up));
        Bike.Body.linearVelocity = Vector3.ProjectOnPlane(start.forward, Vector3.up).normalized * Bike.turboMaxSpeed + Vector3.up * 4;
        Bike.Body.angularVelocity = Vector3.zero; Physics.SyncTransforms();
        float peak = 0, airborneSpeed = float.PositiveInfinity; float began = Time.time;
        while (Time.time - began < 10)
        {
            var sample = Director.Guide.Project(Bike.Body.position, 5);
            DrivePlayerRoute(float.PositiveInfinity);
            float height = Bike.Body.position.y - sample.position.y;
            peak = Mathf.Max(peak, height);
            if (height > 2) airborneSpeed = Mathf.Min(airborneSpeed, Bike.Speed);
            if (sample.path == 5 && sample.distance > hint.releaseDistance + 145 && Bike.IsGrounded) break;
            yield return EditorTestFrame.Next(); Foreground();
        }
        Directory.CreateDirectory("Logs/BikeRouteEnvironment");
        File.WriteAllText("Logs/BikeRouteEnvironment/bridge-flight.txt", $"seededAirborne=True peak={peak:F2} minimumAirSpeed={airborneSpeed:F2} end={Director.Guide.Project(Bike.Body.position,5).distance:F1}");
        Assert.That(peak, Is.GreaterThan(3)); Assert.That(airborneSpeed, Is.GreaterThan(25), "No airborne dead stop");
        Assert.That(Director.Guide.Project(Bike.Body.position,5).distance, Is.GreaterThan(hint.releaseDistance + 140));
        Complete();
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator MovingHeavyLaserHitsStraightRidingAndMissesTimedLateralDodge()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return MovingLaserBody(); }
    private static IEnumerator MovingLaserBody()
    {
        yield return LaserFixture(1, true);
        var enemy = Director.WaveOne[0].rider.GetComponent<AlienBikeLaserWeapon>();
        // This is a controlled moving-target fixture, not a full-chase feel measurement.
        GameObject.Find("LevelGeometry").SetActive(false);
        foreach (var ground in Object.FindObjectsByType<TerrainCollider>()) ground.enabled = false;
        var plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plane.transform.position = Bike.Body.position - Vector3.up * .95f;
        plane.transform.localScale = new Vector3(1600, .2f, 1600);
        var playerMotion = Bike.gameObject.AddComponent<LaserMotionProbe>();
        var enemyMotion = enemy.Bike.gameObject.AddComponent<LaserMotionProbe>();
        var forward = Vector3.ProjectOnPlane(Bike.transform.forward, Vector3.up).normalized;
        playerMotion.Velocity = enemyMotion.Velocity = forward * Bike.maxSpeed;
        Bike.Body.constraints = enemy.Bike.Body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        var health = Player.GetComponent<PlayerHealth>(); float before = health.CurrentHealth + health.CurrentArmor;
        enemy.enabled = true;
        Directory.CreateDirectory("Logs/BikeRouteEnvironment");
        using var log = new StreamWriter("Logs/BikeRouteEnvironment/moving-laser.txt");
        float nextLog = 0;
        yield return Until(() => health.CurrentHealth + health.CurrentArmor < before, 8, "Straight normal-speed rider takes a swept bolt", () =>
        {
            if (Time.time < nextLog) return; nextLog = Time.time + .25f;
            var driver = enemy.GetComponent<EnemyBikeDriver>();
            var bolts = Object.FindObjectsByType<AlienBikeLaserBolt>();
            log.WriteLine($"t={Time.time:F2} progress={enemy.Progress:F2} shots={enemy.ShotsFired} valid={enemy.IsValidTarget(Bike)} contact={driver.PrioritizesContact} state={driver.State} player={Bike.Body.position} enemy={enemy.Bike.Body.position} velocity={Bike.Body.linearVelocity} enemyVelocity={enemy.Bike.Body.linearVelocity} bolts=" + string.Join("|", bolts.Select(b => $"{b.Flying}:{b.LastHit?.name}:{b.transform.position}:{b.Direction}"))); log.Flush();
        });
        Assert.That(before - health.CurrentHealth - health.CurrentArmor, Is.EqualTo(30).Within(.01f));
        yield return Until(() => enemy.IsWindingUp, 14, "Moving target second lock");
        yield return Seconds(.15f); int shots = enemy.ShotsFired;
        before = health.CurrentHealth + health.CurrentArmor;
        playerMotion.Velocity += Vector3.Cross(Vector3.up, forward) * 8;
        yield return Until(() => enemy.ShotsFired > shots, 1, "The dodge escapes an actual released shot");
        yield return Seconds(1.1f);
        Assert.That(health.CurrentHealth + health.CurrentArmor, Is.EqualTo(before));
        Complete();
    }

    [UnityTest]
    public IEnumerator EnemyHeavyLaserHitsNoReactionButCommittedLateralDodgeEscapes()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return HeavyDodgeBody(); }
    private static IEnumerator HeavyDodgeBody()
    {
        yield return LaserFixture(1, true);
        var enemy = Director.WaveOne[0].rider.GetComponent<AlienBikeLaserWeapon>();
        Assert.That(enemy.enemyPredictionRefreshSeconds, Is.EqualTo(.1f));
        var health = Player.GetComponent<PlayerHealth>(); float before = health.CurrentHealth + health.CurrentArmor;
        enemy.enabled = true;
        yield return Until(() => enemy.ShotsFired > 0, 7, "No-reaction enemy discharge");
        yield return Until(() => health.CurrentHealth + health.CurrentArmor < before, 2, "No-reaction player takes a real bolt");
        Assert.That(before - health.CurrentHealth - health.CurrentArmor, Is.EqualTo(30).Within(.01f));
        yield return Until(() => enemy.IsWindingUp, 14, "Second full lock");
        yield return Seconds(.15f); // Past the short prediction refresh, before discharge.
        var sideways = Bike.transform.right; int fired = enemy.ShotsFired;
        Bike.Body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        float dodgeBegan = Time.time;
        before = health.CurrentHealth + health.CurrentArmor;
        while (Time.time - dodgeBegan < .8f)
        {
            Bike.Body.linearVelocity = sideways * 8; // Controlled lateral velocity, never position teleport or projectile correction.
            yield return EditorTestFrame.Next(); Foreground();
        }
        Assert.That(enemy.ShotsFired, Is.GreaterThan(fired), "Dodge avoids a fired shot, not merely a canceled lock");
        Assert.That(health.CurrentHealth + health.CurrentArmor, Is.EqualTo(before)); Complete();
    }
}
