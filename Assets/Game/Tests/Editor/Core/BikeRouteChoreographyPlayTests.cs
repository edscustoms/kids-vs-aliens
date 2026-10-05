using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed partial class BikeRouteChasePlayTests
{
    [UnityTest, Timeout(240000)]
    public IEnumerator ChaseContactRemainsPrimaryAndCreatesForwardWindowsForSixtySeconds()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return ChaseRoles(false); }

    [UnityTest, Timeout(240000)]
    public IEnumerator NormalChaseCanCompleteRealPlayerLocksOnLeadBlockers()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return ChaseRoles(true); }

    [UnityTest, Timeout(180000)]
    public IEnumerator LoneRiderAlternatesContactAndLeadThenReleasesDisabledRole()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return SoloRoles(); }
    private static IEnumerator SoloRoles()
    {
        SuppressLasers(); yield return Mount();
        yield return Until(() => Director.WaveOne.All(s => s.rider.HasActivated), 6, "Opening riders");
        Director.WaveOne[0].rider.GetComponent<EnemyHealth>().TakeDamage(1000);
        var survivor = Director.WaveOne[1].rider;
        Action drive = () => DrivePlayerRoute(20);
        yield return Until(() => survivor.CompletedAttacks > 0, 20, "Lone rider first applies physical pressure", drive);
        yield return Until(() => survivor.HoldingLead, 25, "Then exposes a real forward window", drive);
        int attacks = survivor.AttackAttempts;
        Assert.That(Director.PrimaryAttacker, Is.Not.SameAs(survivor));
        yield return Until(() => survivor.AttackAttempts > attacks, 20, "After the lead window, physical pressure returns", drive);
        yield return Until(() => survivor.IsLeadBlocker, 25, "A subsequent lead can be reserved", drive);
        survivor.enabled = false;
        Assert.That(Director.LeadBlocker, Is.Null); Assert.That(survivor.IsLeadBlocker, Is.False);
        Complete();
    }

    private static IEnumerator ChaseRoles(bool counterfire)
    {
        // No tuning overrides, frozen bodies or positional correction. Drive the
        // normal road line without turbo; sustained 70.1 turbo can outrun these enemies.
        // Counterfire uses normal braking to maintain a following gap when a blocker is ahead.
        var health = Player.GetComponent<PlayerHealth>();
        var serialized = new SerializedObject(health);
        serialized.FindProperty("maxHealth").floatValue = 10000; serialized.ApplyModifiedPropertiesWithoutUndo();
        health.RestoreRunHealth(10000, 50); // Keep real incoming hits enabled for the measurement.
        Laser.enabled = counterfire;
        var riders = Director.WaveOne.Select(s => s.rider).ToArray();
        var lasers = riders.Select(r => r.GetComponent<AlienBikeLaserWeapon>()).ToArray();
        var probes = riders.Select(r => r.gameObject.AddComponent<ChaseContactProbe>()).ToArray();
        yield return Mount();
        var lines = new List<string>();
        Directory.CreateDirectory("Logs/BikeRouteChaseV42");
        string output = "Logs/BikeRouteChaseV42/" + (counterfire ? "counterfire" : "physical") + ".txt";
        float began = Time.time, nextLog = 0, opportunity = 0, longestOpportunity = 0, pile = 0, longestPile = 0;
        AlienBikeController opportunityTarget = null;
        int suppressedFrames = 0, secondaryLockFrames = 0;
        var caught = new bool[2];
        var countedBolts = new HashSet<AlienBikeLaserBolt>();
        var instigatorField = typeof(AlienBikeLaserBolt).GetField("instigator", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        int playerBoltHits = 0, playerBoltKills = 0;
        while (Time.time - began < 60)
        {
            // Hold forward as a player would; the bike owns its normal speed cap.
            // Zero throttle at every tiny overspeed invokes the strong brake and distorts pursuit.
            Foreground();
            var blocker = Director.LeadBlocker;
            if (counterfire && blocker != null && blocker.LongitudinalOffset > 6 && blocker.DistanceToPlayer < 50)
                DrivePlayerRoute(Mathf.Min(Bike.maxSpeed, Mathf.Max(8, blocker.Bike.Speed + (blocker.LongitudinalOffset - 12) * .8f)));
            else DrivePlayerRoute(float.PositiveInfinity);
            Input.SprintInput(false);
            Assert.That(riders.Count(r => r.IsCommittingContact), Is.LessThanOrEqualTo(1));
            Assert.That(riders.Count(r => r.IsLeadBlocker), Is.LessThanOrEqualTo(1));
            for (int i = 0; i < riders.Length; i++)
            {
                caught[i] |= riders[i].CatchUpAdvantage;
                if (riders[i].PrioritizesContact)
                {
                    suppressedFrames++;
                    Assert.That(lasers[i].Progress, Is.Zero, "Committed physical attacker cannot also lock/fire");
                    Assert.That(lasers[i].IsWindingUp, Is.False);
                }
                else if (lasers[i].Progress > 0) secondaryLockFrames++;
            }
            // Attribute accepted swept hits to Amy, not an intercepted enemy bolt.
            foreach (var bolt in UnityEngine.Object.FindObjectsByType<AlienBikeLaserBolt>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (bolt.Flying) countedBolts.Remove(bolt);
                else if (bolt.LastHit != null && (GameObject)instigatorField.GetValue(bolt) == Player.gameObject
                    && bolt.LastHit.GetComponentInParent<EnemyActor>() is EnemyActor victim && countedBolts.Add(bolt))
                { playerBoltHits++; if (!victim.IsAlive) playerBoltKills++; }
            }
            var visible = Laser.SelectTarget();
            if (visible != null && visible == opportunityTarget) opportunity += Time.deltaTime;
            else { opportunity = 0; opportunityTarget = visible; }
            longestOpportunity = Mathf.Max(longestOpportunity, opportunity);
            if (riders.All(r => r.HasActivated && r.DistanceToPlayer < 5 && r.Bike.Speed < 3)) pile += Time.deltaTime;
            else pile = 0;
            longestPile = Mathf.Max(longestPile, pile);
            if (Time.time - began >= nextLog)
            {
                nextLog += 5;
                lines.Add((Time.time - began).ToString("F1") + " player=" + Bike.Speed.ToString("F1")
                    + " playerLocks=" + Laser.LocksCompleted + " playerShots=" + Laser.ShotsFired
                    + " | " + string.Join(" | ", riders.Select((r, i) => r.Band + " long=" + r.LongitudinalOffset.ToString("F1")
                        + " lead=" + r.IsLeadBlocker + "/" + r.HoldingLead + " windows=" + r.LeadWindows
                        + " attacks=" + r.AttackAttempts + " speed=" + (r.Bike != null ? r.Bike.Speed : 0).ToString("F1")
                        + " laser=" + lasers[i].Progress.ToString("F2") + " shots=" + lasers[i].ShotsFired)));
                File.WriteAllLines(output, lines);
            }
            yield return EditorTestFrame.Next();
        }
        string metrics = "attacks=" + string.Join(",", riders.Select(r => r.AttackAttempts))
            + " recoveries=" + string.Join(",", riders.Select(r => r.CompletedAttacks))
            + " contacts=" + probes.Sum(p => p.ContactEpisodes) + " handoffs=" + Director.ContactHandoffs
            + " leadWindows=" + riders.Sum(r => r.LeadWindows) + " forwardOpportunity=" + longestOpportunity
            + " playerLocks=" + Laser.LocksCompleted + " playerShots=" + Laser.ShotsFired
            + " playerBoltHits=" + playerBoltHits + " playerBoltKills=" + playerBoltKills
            + " secondaryLockFrames=" + secondaryLockFrames + " suppressedFrames=" + suppressedFrames
            + " enemyHealth=" + string.Join(",", riders.Select(r => r.GetComponent<EnemyHealth>().CurrentHealth))
            + " impactPulses=" + Bike.GetComponent<BikeRouteImpactFeedback>().AcceptedImpacts
            + " longestPile=" + longestPile + " damage=" + (10050 - health.CurrentHealth - health.CurrentArmor);
        lines.Add(metrics); File.WriteAllLines(output, lines); Debug.Log(metrics);
        ProceduralUIReview.Capture("chase-v42-" + (counterfire ? "counterfire" : "physical"), 1280, 720);
        Assert.That(riders.Sum(r => r.LeadWindows), Is.GreaterThan(0), "Actual driving creates forward blocker windows");
        if (counterfire)
        {
            Assert.That(Laser.LocksCompleted, Is.GreaterThanOrEqualTo(2), "Normal chase offers a genuine completed lock");
            Assert.That(Laser.ShotsFired, Is.GreaterThanOrEqualTo(2), "No fake completion or guaranteed damage");
            Assert.That(playerBoltHits, Is.GreaterThan(0), "Real offensive windows produce Amy's damaging shot, not just a HUD completion or enemy friendly fire");
        }
        else
        {
            Assert.That(riders.Sum(r => r.AttackAttempts), Is.GreaterThanOrEqualTo(4), "Repeated committed attacks coexist with forward blockers");
            Assert.That(riders.All(r => r.AttackAttempts > 0 || r.LeadWindows > 0), Is.True, "Both riders participate in pressure roles");
            Assert.That(riders.Sum(r => r.LeadWindows), Is.GreaterThanOrEqualTo(2), "Repeated forward windows accompany pressure");
            Assert.That(riders.Sum(r => r.CompletedAttacks), Is.GreaterThanOrEqualTo(2));
            Assert.That(probes.Sum(p => p.ContactEpisodes), Is.GreaterThanOrEqualTo(2));
            Assert.That(suppressedFrames, Is.GreaterThan(0));
            Assert.That(secondaryLockFrames, Is.GreaterThan(0));
            Assert.That(longestOpportunity, Is.GreaterThanOrEqualTo(3.8f), "Maintainable forward solution spans lock plus discharge");
            Assert.That(caught.Any(b => b), Is.True, "Existing catch-up remains in use");
        }
        Assert.That(longestPile, Is.LessThan(2), "No sustained stationary pileup");
        Assert.That(Bike.maxSpeed, Is.EqualTo(40.6f)); Assert.That(Bike.turboMaxSpeed, Is.EqualTo(70.1f));
        Assert.That(Bike.acceleration, Is.EqualTo(28)); Assert.That(Bike.turboAcceleration, Is.EqualTo(53));
        Complete();
    }
}
