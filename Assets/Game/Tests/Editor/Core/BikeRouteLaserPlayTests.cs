using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

// Uses the chase fixture's real scene, mount, suspension and isolated save lifecycle.
public sealed partial class BikeRouteChasePlayTests
{
    private static AlienBikeLaserWeapon Laser => Bike.GetComponent<AlienBikeLaserWeapon>();
    private static IEnumerator LaserFixture(int count = 1, bool behind = false)
    {
        Director.enabled = false;
        Director.GetComponent<BikeRouteContainment>().enabled = false; // Isolate gun fixtures from route escape defense.
        Laser.enabled = false;
        yield return Mount();
        Bike.Body.constraints = RigidbodyConstraints.FreezeAll;
        var at = Director.Guide.Project(Bike.transform.position);
        for (int i = 0; i < count; i++)
        {
            var rider = Director.WaveOne[i].rider;
            rider.GetComponent<AlienBikeLaserWeapon>().enabled = false;
            var location = Director.Guide.Ahead(at, (behind ? -1 : 1) * (23 + i * 5), at.path);
            rider.Activate(location.position + location.Right * (i == 0 ? -1.7f : 1.7f) + Vector3.up * .85f,
                Quaternion.LookRotation(location.forward), false);
        }
        yield return Until(() => Director.WaveOne.Take(count).All(s => s.rider.Bike.IsDriven), 3, "Laser fixture physical drivers");
        foreach (var slot in Director.WaveOne.Take(count)) slot.rider.Bike.Body.constraints = RigidbodyConstraints.FreezeAll;
        Input.MoveInput(Vector2.up);
        yield return Seconds(1.8f);
    }
    [UnityTest]
    public IEnumerator VehicleLaserStickyScreenSelectionCancelsForCoverScreenLossAndDeath()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return VehicleLaserStickyScreenSelectionCancelsForCoverScreenLossAndDeathBody(); }
    private static IEnumerator VehicleLaserStickyScreenSelectionCancelsForCoverScreenLossAndDeathBody()
    {
        yield return LaserFixture(2);
        Laser.enabled = true;
        var visible = Director.WaveOne.Select(s => s.rider.Bike).Where(Laser.IsValidTarget).ToArray();
        Assert.That(visible.Length, Is.EqualTo(2), "Both alternatives are visible and unobstructed");
        var expected = visible.OrderBy(b =>
        {
            Vector3 screen = Laser.combatCamera.ForwardBikeViewportPoint(AlienBikeLaserWeapon.AimPoint(b));
            return new Vector2(screen.x - .5f, screen.y - .5f).sqrMagnitude;
        }).ThenBy(b => (b.transform.position - Bike.transform.position).sqrMagnitude).First();
        Assert.That(Laser.SelectTarget(), Is.SameAs(expected), "Acquire nearest screen center before considering world distance");
        yield return Until(() => Laser.Progress > .15f, 2, "Automatic lock starts");
        Assert.That(Laser.Target, Is.SameAs(expected));
        var other = Director.WaveOne.Select(s => s.rider.Bike).Single(b => b != expected);
        Vector3 previousOther = other.Body.position;
        other.Body.position = Bike.Body.position + Bike.transform.right * 3.5f + Bike.transform.forward * .3f; Physics.SyncTransforms();
        Assert.That(Vector3.Angle(Bike.transform.forward, other.Body.position - Bike.Body.position), Is.GreaterThan(70));
        Assert.That(Laser.IsValidTarget(other), Is.False, "Even a visible target must respect the vehicle firing cone");
        other.Body.position = expected.Body.position;
        expected.Body.position = previousOther; Physics.SyncTransforms();
        Assert.That(Laser.IsValidTarget(expected), Is.True);
        yield return Seconds(.1f);
        Assert.That(Laser.Target, Is.SameAs(expected), "Retain valid target even if another becomes central");
        Laser.targets = new[] { expected };
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = Vector3.Lerp(Laser.muzzle.position, AlienBikeLaserWeapon.AimPoint(expected), .5f);
        wall.transform.localScale = new Vector3(8, 5, 2); Physics.SyncTransforms();
        yield return Seconds(.1f);
        Assert.That(Laser.Target, Is.Null); Assert.That(Laser.Progress, Is.Zero);
        Object.Destroy(wall); yield return Until(() => Laser.Progress > .05f, 2, "Reacquire after real cover clears");
        var original = expected.Body.position;
        expected.Body.position = Bike.transform.position - Bike.transform.forward * 20; Physics.SyncTransforms();
        yield return Seconds(.1f); Assert.That(Laser.Target, Is.Null, "Leaving normal forward screen resets lock");
        expected.Body.position = original; Physics.SyncTransforms();
        yield return Until(() => Laser.Progress > .05f, 2, "Reacquire visible target");
        expected.GetComponent<EnemyHealth>().TakeDamage(1000);
        yield return Seconds(.1f); Assert.That(Laser.Target, Is.Null, "Death resets lock");
        Complete();
    }
    [UnityTest]
    public IEnumerator VehicleLaserLocksForThreePointFiveSecondsThenSweptBoltDamagesOnce()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return VehicleLaserLocksForThreePointFiveSecondsThenSweptBoltDamagesOnceBody(); }
    private static IEnumerator VehicleLaserLocksForThreePointFiveSecondsThenSweptBoltDamagesOnceBody()
    {
        yield return LaserFixture();
        var enemy = Director.WaveOne[0].rider; var health = enemy.GetComponent<EnemyHealth>();
        float initialHealth = health.CurrentHealth;
        Assert.That(AudioService.Play2D(Laser.lockBeep), Is.True, "Target beep is preloaded and registered with AudioService");
        Laser.enabled = true;
        yield return Until(() => Laser.Progress > 0, 2, "Player lock starts");
        float began = Time.time - Laser.Progress * Laser.lockSeconds, firstInterval = Laser.BeepInterval;
        yield return Seconds(.04f);
        ProceduralUIReview.Capture("chase-v41-outgoing-00", 1280, 720);
        yield return Until(() => Laser.Progress > .33f, 2, "Early lock");
        yield return Seconds(.04f);
        ProceduralUIReview.Capture("chase-v41-outgoing-33", 1280, 720);
        var overlay = Object.FindObjectsByType<BikeLaserLockView>().Single(v => v.DisplayedProgress > 0);
        float spread = overlay.CornerSpread;
        yield return Until(() => Laser.Progress > .66f, 4, "Late lock");
        yield return Seconds(.04f);
        ProceduralUIReview.Capture("chase-v41-outgoing-66", 1280, 720);
        yield return Until(() => Laser.Progress > .85f, 2, "Late cadence");
        Assert.That(Laser.BeepInterval, Is.LessThan(firstInterval * .35f));
        Assert.That(overlay.CornerSpread, Is.LessThan(spread));
        Assert.That(health.CurrentHealth, Is.EqualTo(initialHealth), "Locking itself is not hitscan damage");
        ProceduralUIReview.Capture("chase-v4-outgoing-tight", 1280, 720);
        yield return Until(() => Laser.IsWindingUp, 2, "Final LOCKED discharge beat");
        Assert.That(Laser.LockedAt - began, Is.EqualTo(3.5f).Within(.09f));
        Assert.That(Laser.ShotsFired, Is.Zero);
        yield return Seconds(.04f);
        ProceduralUIReview.Capture("chase-v41-outgoing-100", 1280, 720);
        yield return Until(() => Laser.ShotsFired == 1, 2, "Exactly one automatic heavy bolt");
        Assert.That(Laser.LastFireTime - Laser.LockedAt, Is.EqualTo(.3f).Within(.05f));
        Assert.That(Laser.boltSpeed, Is.EqualTo(72));
        Assert.That(health.CurrentHealth, Is.EqualTo(initialHealth), "Damage waits for physical travel");
        yield return Seconds(.04f);
        ProceduralUIReview.Capture("chase-v4-heavy-bolt", 1280, 720);
        yield return Until(() => health.CurrentHealth < initialHealth, 2, "Swept bolt reaches actual enemy hull");
        Assert.That(initialHealth - health.CurrentHealth, Is.EqualTo(30).Within(.01f));
        while (Time.time - Laser.LastFireTime < 7.9f)
        {
            Assert.That(Laser.Target, Is.Null); Assert.That(Laser.Progress, Is.Zero);
            Assert.That(Laser.ShotsFired, Is.EqualTo(1));
            Assert.That(overlay.Visible, Is.False, "No brackets during post-shot quiet period");
            yield return EditorTestFrame.Next(); Foreground();
        }
        yield return Until(() => Laser.Progress > 0, .5f, "Reacquire after eight-second cooldown");
        Assert.That(Time.time - Laser.LastFireTime, Is.GreaterThanOrEqualTo(8));
        Complete();
    }
    [UnityTest]
    public IEnumerator IncomingLasersUseIndependentLocksAndOneWarningWithoutHandheldShots()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return IncomingLasersUseIndependentLocksAndOneWarningWithoutHandheldShotsBody(); }
    private static IEnumerator IncomingLasersUseIndependentLocksAndOneWarningWithoutHandheldShotsBody()
    {
        yield return LaserFixture(2, true);
        var weapons = Director.WaveOne.Select(s => s.rider.GetComponent<AlienBikeLaserWeapon>()).ToArray();
        int handheldShots = 0;
        foreach (var slot in Director.WaveOne) slot.rider.GetComponent<EnemyRangedAttack>().ShotFired += (_, __) => handheldShots++;
        foreach (var weapon in weapons) weapon.enabled = true;
        yield return Until(() => weapons.Any(w => w.Progress > 0), 3, "Incoming start");
        yield return Seconds(.04f);
        ProceduralUIReview.Capture("chase-v41-incoming-00", 1280, 720);
        yield return Until(() => weapons.All(w => w.Progress > .1f), 3, "Independent incoming locks");
        Assert.That(weapons[0].Progress, Is.Not.EqualTo(weapons[1].Progress));
        Assert.That(AlienBikeLaserWeapon.IncomingFor(Bike), Is.SameAs(weapons.OrderByDescending(w => w.Progress).First()));
        yield return Until(() => weapons.Any(w => w.Progress > .33f), 2, "Incoming third");
        yield return Seconds(.04f);
        ProceduralUIReview.Capture("chase-v41-incoming-33", 1280, 720);
        yield return Until(() => weapons.Any(w => w.Progress > .66f), 2, "Incoming two thirds");
        yield return Seconds(.04f);
        ProceduralUIReview.Capture("chase-v41-incoming-66", 1280, 720);
        yield return Until(() => weapons.Any(w => w.IsWindingUp), 2, "Incoming full lock");
        yield return Seconds(.04f);
        ProceduralUIReview.Capture("chase-v41-incoming-100", 1280, 720);
        yield return Until(() => weapons.All(w => w.ShotsFired > 0), 3, "Both vehicle weapons fire");
        Assert.That(Mathf.Abs(weapons[0].LastFireTime - weapons[1].LastFireTime), Is.GreaterThan(.01f), "Independent clocks do not fire on the same frame");
        Assert.That(handheldShots, Is.Zero);
        foreach (var slot in Director.WaveOne)
            Assert.That(slot.rider.GetComponent<EnemyCombatPresentation>().Style, Is.EqualTo(WeaponAnimationStyle.Unarmed));
        yield return Until(() => Player.GetComponent<PlayerHealth>().CurrentArmor < 50, 2, "Vehicle bolt hits occupied Amy hull");
        Complete();
    }
    [UnityTest]
    public IEnumerator IncomingBoltCanBeDodgedAndContinuesIntoWorldImpact()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return IncomingBoltCanBeDodgedAndContinuesIntoWorldImpactBody(); }
    private static IEnumerator IncomingBoltCanBeDodgedAndContinuesIntoWorldImpactBody()
    {
        yield return LaserFixture(1, true);
        var weapon = Director.WaveOne[0].rider.GetComponent<AlienBikeLaserWeapon>(); weapon.enabled = true;
        yield return Until(() => weapon.ShotsFired == 1, 6, "Incoming heavy bolt fires");
        var bolt = Object.FindObjectsByType<AlienBikeLaserBolt>().Single(b => b.Flying);
        Vector3 direction = bolt.Direction, start = bolt.transform.position;
        float targetDistance = Vector3.Distance(start, AlienBikeLaserWeapon.AimPoint(Bike));
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Dodge fixture impact wall";
        wall.transform.SetPositionAndRotation(start + direction * 65, Quaternion.LookRotation(direction));
        wall.transform.localScale = new Vector3(12, 12, 1);
        // A line change after launch, with no alteration to the projectile's flight.
        Bike.Body.position += Vector3.Cross(Vector3.up, direction).normalized * 5;
        float health = Player.GetComponent<PlayerHealth>().CurrentHealth + Player.GetComponent<PlayerHealth>().CurrentArmor;
        Physics.SyncTransforms();
        yield return Until(() => bolt.Travelled > targetDistance + 4 || !bolt.Flying, 2, "Bolt passes the old player line");
        Assert.That(bolt.Flying, Is.True, "A miss must continue beyond Amy");
        Assert.That(bolt.Direction, Is.EqualTo(direction), "No homing after firing");
        Assert.That(Player.GetComponent<PlayerHealth>().CurrentHealth + Player.GetComponent<PlayerHealth>().CurrentArmor, Is.EqualTo(health));
        ProceduralUIReview.Capture("chase-v4-dodged-bolt", 1280, 720);
        yield return Until(() => !bolt.Flying, 2, "Missed bolt impacts world");
        Assert.That(bolt.LastHit, Is.Not.Null);
        Assert.That(bolt.LastHit.GetComponentInParent<PlayerHealth>(), Is.Null);
        Assert.That(Object.FindObjectsByType<PlasmaImpactVFX>().Any(v => v.name.Contains("BikeHeavyImpact")), Is.True);
        yield return Seconds(.08f);
        ProceduralUIReview.Capture("chase-v4-heavy-impact", 1280, 720);
        Object.Destroy(wall); Complete();
    }
    [UnityTest]
    public IEnumerator PlayerBoltCanMissMovingAlienWithoutHoming()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return PlayerBoltCanMissMovingAlienWithoutHomingBody(); }
    private static IEnumerator PlayerBoltCanMissMovingAlienWithoutHomingBody()
    {
        yield return LaserFixture();
        var target = Director.WaveOne[0].rider.Bike;
        float health = target.GetComponent<EnemyHealth>().CurrentHealth;
        Laser.enabled = true;
        yield return Until(() => Laser.ShotsFired == 1, 6, "Player vehicle bolt fires");
        var bolt = Object.FindObjectsByType<AlienBikeLaserBolt>().Single(b => b.Flying);
        Vector3 direction = bolt.Direction;
        target.Body.position += Vector3.Cross(Vector3.up, direction).normalized * 5; Physics.SyncTransforms();
        yield return Seconds(.6f);
        Assert.That(target.GetComponent<EnemyHealth>().CurrentHealth, Is.EqualTo(health));
        Assert.That(bolt.Direction, Is.EqualTo(direction));
        Assert.That(bolt.Travelled, Is.GreaterThan(25), "Projectile continues after missing the alien");
        Complete();
    }
    [UnityTest]
    public IEnumerator RearViewHoldIsInstantForwardLockAndControlsSurviveAndDismountClears()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return RearViewHoldIsInstantForwardLockAndControlsSurviveAndDismountClearsBody(); }
    private static IEnumerator RearViewHoldIsInstantForwardLockAndControlsSurviveAndDismountClearsBody()
    {
        yield return LaserFixture();
        var camera = Laser.combatCamera;
        var button = Object.FindObjectsByType<Button>().Single(b => b.name == "BikeRearView");
        var pause = Object.FindAnyObjectByType<ManualPauseButton>().GetComponent<RectTransform>();
        var rear = (RectTransform)button.transform;
        Assert.That(button.GetComponentInChildren<RearViewGlyph>().canvasRenderer, Is.Not.Null);
        Assert.That(rear.anchoredPosition.x, Is.EqualTo(pause.anchoredPosition.x));
        Assert.That(rear.anchoredPosition.y, Is.LessThan(pause.anchoredPosition.y - pause.rect.height));
        var target = Director.WaveOne[0].rider.Bike;
        Vector3 forwardViewport = camera.ForwardBikeViewportPoint(AlienBikeLaserWeapon.AimPoint(target));
        var inputBefore = Player.DrivingInput;
        var pointer = new PointerEventData(EventSystem.current) { pointerId = 4, button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        Assert.That(camera.RearView, Is.True); Assert.That(camera.RearViewYaw, Is.EqualTo(180), "Rear view is immediate");
        yield return EditorTestFrame.Next();
        Assert.That(Player.DrivingInput, Is.EqualTo(inputBefore), "View-only hold never changes steering input");
        Assert.That(Vector3.Distance(camera.ForwardBikeViewportPoint(AlienBikeLaserWeapon.AimPoint(target)), forwardViewport), Is.LessThan(.05f));
        Assert.That(Laser.IsValidTarget(target), Is.True, "Forward combat view survives rear look");
        target.Body.position = Bike.transform.position - Bike.transform.forward * 15; Physics.SyncTransforms();
        yield return Seconds(.1f); Assert.That(Laser.IsValidTarget(target), Is.False, "Rear view cannot acquire a backwards firing solution");
        ProceduralUIReview.Capture("chase-v4-rear-view", 1280, 720);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        Assert.That(camera.RearViewYaw, Is.Zero, "Release returns immediately");
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
        Assert.That(camera.RearViewYaw, Is.Zero, "Exit releases the hold");
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
        Assert.That(camera.RearViewYaw, Is.Zero, "Cancel releases the hold");
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        button.gameObject.SetActive(false); Assert.That(camera.RearViewYaw, Is.Zero, "Disable releases the hold");
        button.gameObject.SetActive(true);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        Input.MoveInput(Vector2.zero); Bike.Secure();
        Assert.That(Player.TryDismount(), Is.True);
        yield return Until(() => !Player.IsBusy, 3, "Dismount restores on-foot owner");
        yield return Seconds(2);
        Assert.That(camera.RearView, Is.False); Assert.That(camera.RearViewYaw, Is.Zero);
        Assert.That(button.gameObject.activeSelf, Is.False);
        Complete();
    }
    [UnityTest]
    public IEnumerator RearViewAndTransientLockResetOnPauseAndDeath()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return RearCleanup(); }
    private static IEnumerator RearCleanup()
    {
        yield return LaserFixture(); Laser.enabled = true;
        var camera = Laser.combatCamera;
        yield return Until(() => Laser.Progress > .1f, 2, "Lock before pause");
        camera.SetRearViewHeld(true); yield return Seconds(.3f);
        Time.timeScale = 0;
        for (int i = 0; i < 5; i++) yield return EditorTestFrame.Next();
        Assert.That(camera.RearView, Is.False); Assert.That(Laser.Progress, Is.Zero);
        Time.timeScale = 1; yield return Seconds(.6f);
        camera.SetRearViewHeld(true); yield return Seconds(.3f);
        Player.GetComponent<PlayerHealth>().TakeDamage(1000);
        for (int i = 0; i < 5; i++) yield return EditorTestFrame.Next();
        Assert.That(camera.RearView, Is.False); Assert.That(camera.RearViewYaw, Is.Zero);
        Assert.That(Laser.Progress, Is.Zero); Complete();
    }

    [UnityTest]
    public IEnumerator VehicleConeRejectsAheadAlienAndCancelsPassingLock()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return DirectionChecks(); }
    private static IEnumerator DirectionChecks()
    {
        yield return LaserFixture(1, true);
        var enemy = Director.WaveOne[0].rider.Bike;
        var weapon = enemy.GetComponent<AlienBikeLaserWeapon>();
        Vector3 forward = Bike.Body.rotation * Vector3.forward, right = Bike.Body.rotation * Vector3.right;
        void Place(float side, float behind)
        {
            enemy.Body.position = Bike.Body.position + right * side - forward * behind;
            enemy.Body.rotation = Bike.Body.rotation; Physics.SyncTransforms();
        }
        Place(0, 20); Assert.That(weapon.IsValidTarget(Bike), Is.True, "Twenty metres behind, facing Amy");
        Place(3.5f, 2.5f); Assert.That(weapon.IsValidTarget(Bike), Is.True, "Beside/rear inside sixty degrees");
        Place(4.6f, 2.5f); Assert.That(weapon.IsValidTarget(Bike), Is.False, "Outside forward cone");
        Place(0, -20); Assert.That(weapon.IsValidTarget(Bike), Is.False, "Ahead and facing forward must never shoot backwards");
        Place(0, 20); weapon.enabled = true;
        yield return Until(() => weapon.Progress > .15f, 2, "Partial incoming lock");
        Place(0, -20); yield return Seconds(.1f);
        Assert.That(weapon.Target, Is.Null); Assert.That(weapon.Progress, Is.Zero);
        Assert.That(weapon.ShotsFired, Is.Zero, "Passing Amy immediately cancels the lock");
        Assert.That(Laser.IsValidTarget(enemy), Is.True, "The leading bike is now Amy's forward target");
        Place(0, 20); Assert.That(Laser.IsValidTarget(enemy), Is.False, "Player weapon has the same physical forward rule");
        Complete();
    }

    [UnityTest]
    public IEnumerator LateSteeringDuringDischargeCanDodgeWithoutMovingTheBikeByScript()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return SteeringDodge(); }
    private static IEnumerator SteeringDodge()
    {
        yield return LaserFixture(1, true);
        Input.MoveInput(Vector2.zero);
        var weapon = Director.WaveOne[0].rider.GetComponent<AlienBikeLaserWeapon>(); weapon.enabled = true;
        yield return Until(() => weapon.IsWindingUp, 6, "Read the final LOCKED warning");
        Vector3 start = Bike.Body.position, right = Bike.Body.rotation * Vector3.right;
        float health = Player.GetComponent<PlayerHealth>().CurrentHealth + Player.GetComponent<PlayerHealth>().CurrentArmor;
        Bike.Body.constraints = RigidbodyConstraints.None;
        Input.MoveInput(new Vector2(.95f, 1));
        yield return Until(() => weapon.ShotsFired == 1, 1, "Discharge after final beat");
        var bolt = Object.FindObjectsByType<AlienBikeLaserBolt>().Single(b => b.Flying);
        Vector3 direction = bolt.Direction;
        yield return Seconds(.8f);
        Assert.That(Vector3.Dot(Bike.Body.position - start, right), Is.GreaterThan(1), "Real steering produces lateral movement");
        Assert.That(Player.GetComponent<PlayerHealth>().CurrentHealth + Player.GetComponent<PlayerHealth>().CurrentArmor, Is.EqualTo(health));
        Assert.That(bolt.Direction, Is.EqualTo(direction));
        Assert.That(bolt.LastHit == null || bolt.LastHit.GetComponentInParent<AlienBikeController>() != Bike, Is.True);
        ProceduralUIReview.Capture("chase-v41-input-dodge", 1280, 720);
        Complete();
    }
}
