using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ChaseContactProbe : MonoBehaviour
{
    public int PlayerContacts;
    public int FrontPassContacts;
    public float ContactImpulse;
    public int ContactEpisodes;
    private float lastContact = float.NegativeInfinity;
    public readonly List<string> ContactEvents = new List<string>();
    public int WallContacts;
    public float MaximumWallNormalY;
    private void OnCollisionStay(Collision collision) => RecordWall(collision);
    private void RecordWall(Collision collision)
    {
        if (collision.collider.transform.parent == null || collision.collider.transform.parent.name != "Smooth Corridor Collision") return;
        WallContacts++;
        foreach (var contact in collision.contacts) MaximumWallNormalY = Mathf.Max(MaximumWallNormalY, Mathf.Abs(contact.normal.y));
    }
    private void OnCollisionEnter(Collision collision)
    {
        RecordWall(collision);
        if (collision.collider.GetComponentInParent<AlienBikeController>()?.Rider == null) return;
        PlayerContacts++; ContactImpulse += collision.impulse.magnitude;
        if (collision.impulse.magnitude > 1)
        {
            if (Time.time - lastContact > .75f) ContactEpisodes++;
            lastContact = Time.time;
        }
        ContactEvents.Add(Time.time.ToString("F2") + " " + GetComponent<EnemyBikeDriver>().Band + " impulse=" + collision.impulse.magnitude.ToString("F1"));
        if (GetComponent<EnemyBikeDriver>().State == EnemyBikeDriver.DriveState.FrontPass) FrontPassContacts++;
    }
}

// Controlled Rigidbody translation isolates projectile prediction from chase decisions.
[DefaultExecutionOrder(10000)]
public sealed class LaserMotionProbe : MonoBehaviour
{
    public Vector3 Velocity;
    private void FixedUpdate()
    {
        var body = GetComponent<Rigidbody>();
        body.linearVelocity = Velocity;
        body.MoveRotation(Quaternion.LookRotation(Vector3.ProjectOnPlane(Velocity, Vector3.up)));
    }
}

public sealed class SlopeBikeDriveProbe : MonoBehaviour
{
    public AlienBikeController Bike;
    public Vector2 Intent = Vector2.up;
    public bool Jump;
    private void Update() { Bike.SetDriveInput(this, Intent); Bike.TickControls(Time.deltaTime, Jump, false); }
}

public sealed partial class BikeRouteChasePlayTests
{
    private const string Key = "BikeRouteChasePlayTests";
    private static BikeRouteChaseDirector Director => Object.FindAnyObjectByType<BikeRouteChaseDirector>();
    private static PlayerBikeRider Player => Director.Player;
    private static AlienBikeController Bike => Director.PlayerBike;
    private static PlayerInventory Inventory => Player.GetComponent<PlayerInventory>();
    private static StarterAssetsInputs Input => Player.GetComponent<StarterAssetsInputs>();
    private static WeaponItemData Weapon(string name) => AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/" + name + ".asset");
    private static void Setup(bool empty = false)
    {
        SessionState.SetString(Key, Environment.GetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY") ?? "");
        SessionState.SetInt(Key + "Camera", (int)GameplayCameraSettings.Mode);
        SessionState.SetBool(Key + "Complete", false);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", Path.GetFullPath("Logs/BikeRouteChase/Saves-" + Guid.NewGuid().ToString("N")));
        if (empty) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        else EditorSceneManager.OpenScene(BikeRouteChaseSetup.ScenePath);
    }
    private static IEnumerator Initialize()
    {
        Application.runInBackground = true; Time.timeScale = 1;
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 15, "Run ready");
        yield return Seconds(.4f);
        yield return Until(() => Player.NearbyBike == Bike, 5, "Authored mount staging");
    }
    private static IEnumerator Mount()
    {
        Assert.That(Player.TryMount(Bike), Is.True);
        yield return Until(() => Player.IsDriving && Input.CanProcessBikeControls, 6, "Mount");
    }
    [UnityTest]
    public IEnumerator FreshLoadoutPhysicalPickupsAndHardRestart()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return LoadoutAndPickups(); }
    private static IEnumerator LoadoutAndPickups()
    {
        AssertLoadout();
        Director.enabled = false; // This fixture isolates collection from combat damage.
        yield return Mount();
        var pickup = Object.FindObjectsByType<PickupItem>().First();
        int quantity = pickup.Quantity;
        pickup.transform.position = Bike.transform.position + Bike.transform.forward * 7 - Vector3.up * .35f;
        pickup.GetComponent<WorldItemFloat>()?.ResetAnchor(); Physics.SyncTransforms();
        var weapon = Object.Instantiate(Weapon("PlasmaRifleItem").worldPrefab);
        weapon.transform.position = Bike.transform.position + Bike.transform.forward * 12;
        var weaponPickup = weapon.GetComponent<PickupItem>();
        var state = Inventory.GetWeaponState(Weapon("PlasmaPistolItem")); state.Restore(3, Time.time);
        Input.MoveInput(Vector2.up);
        yield return Until(() => Inventory.PlasmaCapsules == 12 + quantity, 4, "Physical drive-over Plasma");
        Assert.That(weaponPickup.TryCollect(Inventory), Is.False, "Mounted resource exception cannot collect a weapon");
        Assert.That(weapon.activeSelf, Is.True);
        Input.MoveInput(Vector2.zero); yield return Seconds(.5f);
        Assert.That(Inventory.GetWeaponState(Weapon("PlasmaPistolItem")).Rounds, Is.EqualTo(3));
        Assert.That(ActiveRunController.Instance.RestartFromBeginning(), Is.True);
        yield return EditorTestFrame.Next(); yield return Initialize(); AssertLoadout();
        Assert.That(Director.Wave, Is.Zero); Assert.That(Director.Finished, Is.False);
        Complete();
    }
    private static void AssertLoadout()
    {
        var pistol = Weapon("PlasmaPistolItem"); var rifle = Weapon("PlasmaRifleItem");
        Assert.That(Inventory.Items.OfType<WeaponItemData>(), Is.EquivalentTo(new[] { pistol, rifle }));
        Assert.That(Inventory.GetWeaponState(pistol).Rounds, Is.EqualTo(pistol.magazineSize));
        Assert.That(Inventory.GetWeaponState(rifle).Rounds, Is.EqualTo(rifle.magazineSize));
        Assert.That(Player.GetComponent<PlayerEquipment>().EquippedWeapon, Is.SameAs(pistol));
        Assert.That(Inventory.PlasmaCapsules, Is.EqualTo(12));
    }

    [UnityTest]
    public IEnumerator WaveOneActivatesPursuesBumpsAndDoesNotDuplicate()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return WaveOne(); }
    private static IEnumerator WaveOne()
    {
        var riders = Director.WaveOne.Select(s => s.rider).ToArray();
        var probes = riders.Select(r => r.gameObject.AddComponent<ChaseContactProbe>()).ToArray();
        var weapons = riders.Select(r => r.GetComponent<AlienBikeLaserWeapon>()).ToArray();
        Bike.GetComponent<AlienBikeLaserWeapon>().enabled = false; // No automatic counterfire in the opening-wave probe.
        int handheldShots = 0;
        foreach (var rider in riders) rider.GetComponent<EnemyRangedAttack>().ShotFired += (_, __) => handheldShots++;
        yield return Mount();
        yield return Until(() => riders[0].HasActivated, 6, "Rear rider 1");
        Assert.That(Director.Guide.Project(Bike.transform.position).progress - Director.Guide.Project(riders[0].transform.position).progress,
            Is.InRange(22, 40));
        Assert.That(riders[1].HasActivated, Is.False, "Second rider has an independent entry delay");
        yield return Until(() => riders[1].HasActivated, 4, "Rear rider 2");
        var starts = riders.Select(r => r.transform.position).ToArray();
        // At this slow player pace V4.2 riders stay inside their physical-pressure band,
        // which deliberately suppresses lasers. Moving and stationary laser fixtures cover firing.
        yield return Until(() => probes.Sum(p => p.PlayerContacts) > 0, 22, "Opening wave applies physical pressure", () => { HealPlayer(); DrivePlayerRoute(16); });
        Assert.That(handheldShots, Is.Zero);
        for (int i = 0; i < 2; i++)
        {
            Assert.That(riders[i].Bike.IsDriven, Is.True);
            Assert.That(riders[i].Bike.Body.isKinematic, Is.False);
        }
        yield return Until(() => probes.Sum(p => p.PlayerContacts) > 0, 22, "Physical side/rear pressure", () => { HealPlayer(); DrivePlayerRoute(16); });
        for (int i = 0; i < 2; i++) Assert.That(Vector3.Distance(starts[i], riders[i].transform.position), Is.GreaterThan(10));
        Assert.That(Director.Wave, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<EnemyBikeDriver>().Length, Is.EqualTo(2));
        Input.MoveInput(Vector2.zero);
        ProceduralUIReview.Capture("bike-route-chase-pressure", 1280, 720);
        Complete();
    }

    [UnityTest]
    public IEnumerator FrontPairWaitsForHiddenAnchorsPassesAndReturnsToPursuit()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return FrontPair(); }
    private static IEnumerator FrontPair()
    {
        SuppressLasers(); // Isolate physical/pistol behavior from the new automatic vehicle weapon.
        yield return Mount();
        yield return Until(() => Director.WaveOne.All(s => s.rider.HasActivated), 6, "Wave one activation");
        foreach (var slot in Director.WaveOne) slot.rider.GetComponent<EnemyHealth>().TakeDamage(1000);
        Assert.That(Director.PrimaryAttacker, Is.Null, "Dead wave-one riders cannot retain the contact slot");
        yield return Until(() => Director.Wave == 2, 3, "Wave two follows both deaths");
        var rear = Director.WaveTwo.Single(s => !s.frontPass).rider;
        rear.enabled = false; // Isolate pass clearance from the rear pursuer pushing Amy into an oncoming line.
        // Place the controlled subject on a bend to expose an authored, off-screen front entry.
        // The AI itself receives no position updates after its single activation.
        var sample = Director.Guide.At(1, 180);
        Bike.Body.position = sample.position + Vector3.up * Bike.hoverHeight;
        Bike.Body.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(sample.forward, Vector3.up));
        Bike.Body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
        var front = Director.WaveTwo.Where(s => s.frontPass).Select(s => s.rider).ToArray();
        var contacts = front.Select(r => r.gameObject.AddComponent<ChaseContactProbe>()).ToArray();
        var times = new Dictionary<EnemyBikeDriver, float>();
        float elapsed = Time.time;
        yield return Until(() => front.All(r => r.HasActivated), 60, "Off-screen front entries", () =>
        {
            HealPlayer();
            DrivePlayerRoute(20);
            foreach (var rider in front)
                if (rider.HasActivated && !times.ContainsKey(rider))
                {
                    var planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
                    Assert.That(GeometryUtility.TestPlanesAABB(planes, new Bounds(rider.transform.position + Vector3.up, new Vector3(3, 3, 3))), Is.False,
                        "Initial front activation must be outside the gameplay frustum");
                    times.Add(rider, Time.time);
                }
        });
        Input.MoveInput(Vector2.zero);
        Assert.That(Mathf.Abs(times[front[0]] - times[front[1]]), Is.GreaterThan(.8f));
        float passSpeed = 0;
        yield return Until(() => front.All(r => r.State == EnemyBikeDriver.DriveState.Pursuit), 40, "Front pair passes, brakes and turns", () =>
        {
            HealPlayer();
            foreach (var rider in front) if (rider.State == EnemyBikeDriver.DriveState.FrontPass) passSpeed = Mathf.Max(passSpeed, rider.Bike.Speed);
        });
        Assert.That(passSpeed, Is.GreaterThan(25), "A visible high-speed first pass");
        Assert.That(contacts.Sum(c => c.FrontPassContacts), Is.Zero, "First pass keeps hull clearance from Amy");
        Assert.That(Director.WaveTwo.Single(s => !s.frontPass).rider.State, Is.EqualTo(EnemyBikeDriver.DriveState.Pursuit));
        rear.enabled = true;
        yield return Until(() => rear.Bike.IsDriven && rear.Bike.Speed > 2, 4, "Rear rider resumes the same pursuit physics");
        yield return Until(() => Director.PrimaryAttacker != null, 15, "Wave two uses the same contact reservation", () => { HealPlayer(); DrivePlayerRoute(16); });
        Assert.That(Director.WaveTwo.Select(s => s.rider), Does.Contain(Director.PrimaryAttacker));
        var waveRiders = Director.WaveTwo.Select(s => s.rider).ToArray();
        var attackers = new HashSet<EnemyBikeDriver>();
        float reviewUntil = Time.time + 15, pileSeconds = 0, longestPile = 0;
        while (Time.time < reviewUntil)
        {
            Foreground(); HealPlayer(); DrivePlayerRoute(16);
            Assert.That(waveRiders.Count(r => r.Band == EnemyBikeDriver.DistanceBand.Attack), Is.LessThanOrEqualTo(1));
            if (Director.PrimaryAttacker != null) attackers.Add(Director.PrimaryAttacker);
            if (waveRiders.All(r => r.DistanceToPlayer < 12 && r.Bike.Speed < 3)) pileSeconds += Time.deltaTime;
            else pileSeconds = 0;
            longestPile = Mathf.Max(longestPile, pileSeconds);
            yield return EditorTestFrame.Next();
        }
        Assert.That(attackers.Count, Is.GreaterThanOrEqualTo(2), "Wave two rotates pressure after wave-one deaths");
        Assert.That(longestPile, Is.LessThan(2), "Three riders must not form a stationary pile around Amy");
        Assert.That(Director.Wave, Is.EqualTo(2));
        Debug.Log("Front pass elapsed=" + (Time.time - elapsed)); Complete();
    }

    [UnityTest]
    public IEnumerator ActualFinishDisengagesAndFourIndependentRiflesKillEnemiesOnly()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return FinishDefense(); }
    private static IEnumerator FinishDefense()
    {
        Director.enabled = false; yield return Mount();
        var trigger = Object.FindAnyObjectByType<BikeRouteFinishTrigger>();
        var center = trigger.transform.position;
        var line = Director.Guide.Project(new Vector3(center.x, center.y - 4, center.z));
        Bike.Body.position = line.position - line.forward * 12 + Vector3.up * Bike.hoverHeight;
        Bike.Body.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(line.forward, Vector3.up));
        Bike.Body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
        yield return Seconds(.2f); Assert.That(Director.Finished, Is.False); Assert.That(Director.Guns.All(g => !g.Active), Is.True);
        var enemies = Director.WaveOne.Concat(Director.WaveTwo).Select(s => s.rider).ToArray();
        for (int i = 0; i < enemies.Length; i++)
        {
            var behind = Director.Guide.Ahead(line, -16 - i * 2.5f, line.path);
            enemies[i].Activate(behind.position + behind.Right * (i % 2 == 0 ? -2 : 2) + Vector3.up * .85f,
                Quaternion.LookRotation(behind.forward), false);
            enemies[i].enabled = false; // Hold test targets in the approach; finish must still assign their disengaged state.
            var healthData = new SerializedObject(enemies[i].GetComponent<EnemyHealth>());
            healthData.FindProperty("maxHealth").floatValue = 400; healthData.ApplyModifiedPropertiesWithoutUndo();
            enemies[i].GetComponent<EnemyHealth>().RestoreRunHealth(400);
        }
        var events = new List<(int gun, float time)>();
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            Director.Guns[i].ShotFired += hit => { Assert.That(hit == null || hit.GetComponentInParent<PlayerHealth>() == null, Is.True); events.Add((index, Time.time)); };
        }
        Input.MoveInput(Vector2.up);
        yield return Until(() => Director.Finished, 4, "Actual crossing of finish trigger");
        Input.MoveInput(Vector2.zero);
        Assert.That(enemies.All(r => r.State == EnemyBikeDriver.DriveState.Disengaged), Is.True);
        Assert.That(Director.Guns.All(g => g.Active), Is.True);
        Assert.That(Player.IsDriving && Input.CanProcessBikeControls, Is.True);
        float health = Player.GetComponent<PlayerHealth>().CurrentHealth;
        yield return Until(() => Director.Guns.All(g => g.ShotsFired >= 3), 10, "Four independent rifles fire");
        Assert.That(events.GroupBy(e => e.gun).Select(g => g.First().time).Distinct().Count(), Is.EqualTo(4));
        Assert.That(Director.Guns.Where(g => g.Target != null).All(g => enemies.Any(r => r.Actor == g.Target)), Is.True);
        yield return Until(() => enemies.Any(r => r.GetComponent<EnemyHealth>().IsDead), 18, "Existing enemy health/death receives tower hits");
        Assert.That(Player.GetComponent<PlayerHealth>().CurrentHealth, Is.EqualTo(health));
        Director.enabled = true; yield return Seconds(.5f); Assert.That(Director.Wave, Is.Zero, "Finish bars even the opening wave");
        ProceduralUIReview.Capture("bike-route-finish-defense", 1280, 720);
        var camera = Camera.main; var previousPosition = camera.transform.position; var previousRotation = camera.transform.rotation;
        try
        {
            camera.transform.position = line.position + new Vector3(0, 13, -20);
            camera.transform.LookAt(line.position + new Vector3(0, 3, 4));
            ProceduralUIReview.Capture("bike-route-defense-inspection", 1280, 720);
        }
        finally { camera.transform.SetPositionAndRotation(previousPosition, previousRotation); }
        Complete();
    }

    [UnityTest]
    public IEnumerator SaveContinueKeepsDeadRidersWavesResourcesAndFinishedState()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return Persistence(); }
    private static IEnumerator Persistence()
    {
        yield return Mount(); yield return Until(() => Director.WaveOne.All(s => s.rider.HasActivated), 6, "Wave one activation");
        SuppressLasers();
        yield return Until(() => Director.LeadBlocker != null, 25, "Save during a real lead reservation", () => DrivePlayerRoute(20));
        int removedIndex = Director.LeadBlocker == Director.WaveOne[0].rider ? 1 : 0;
        var survivor = Director.LeadBlocker;
        Director.WaveOne[removedIndex].rider.GetComponent<EnemyHealth>().TakeDamage(1000);
        Inventory.RestoreCapsules(5, 0); Inventory.GetWeaponState(Weapon("PlasmaPistolItem")).Restore(2, Time.time);
        yield return Until(() => survivor.Bike.IsGrounded, 3, "Enemy grounded checkpoint");
        survivor.Bike.RememberSafePose(survivor.transform.position);
        var grounded = survivor.transform.position;
        survivor.Bike.Body.position += Vector3.up * 8; survivor.Bike.Body.linearVelocity = Vector3.up * 12;
        Assert.That(ActiveRunController.Instance.Save(), Is.True); ActiveRunController.Instance.PrepareToLeave();
        Assert.That(RunSaveService.Continue(), Is.True, RunSaveService.LastError);
        yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 20, "Continue", null, false);
        Assert.That(Time.timeScale, Is.Zero);
        Assert.That(Director.PrimaryAttacker, Is.Null, "Continue cannot revive a transient contact reservation");
        Assert.That(Director.LeadBlocker, Is.Null, "Both restore passes discard lead choreography");
        Assert.That(Director.WaveOne.All(s => !s.rider.IsLeadBlocker), Is.True);
        Assert.That(Director.Wave, Is.EqualTo(1)); Assert.That(Director.WaveOne[removedIndex].rider.GetComponent<RunWorldObject>().IsRemoved, Is.True);
        survivor = Director.WaveOne[1 - removedIndex].rider;
        Assert.That(Vector3.Distance(survivor.transform.position, grounded), Is.LessThan(.15f), "Enemy restores grounded, not airborne");
        Assert.That(Inventory.PlasmaCapsules, Is.EqualTo(5)); Assert.That(Inventory.GetWeaponState(Weapon("PlasmaPistolItem")).Rounds, Is.EqualTo(2));
        Foreground(); yield return Seconds(.3f);
        survivor.GetComponent<EnemyHealth>().TakeDamage(1000);
        yield return Until(() => Director.Wave == 2, 3, "One remaining death starts wave two");
        Director.ReachFinish(Player);
        Assert.That(ActiveRunController.Instance.Save(), Is.True); ActiveRunController.Instance.PrepareToLeave();
        Assert.That(RunSaveService.Continue(), Is.True, RunSaveService.LastError); yield return EditorTestFrame.Next();
        yield return Until(() => ActiveRunController.Instance != null && ActiveRunController.Instance.IsReady, 20, "Finish Continue", null, false);
        Assert.That(Director.Finished, Is.True); Assert.That(Director.Wave, Is.EqualTo(2));
        Foreground(); yield return Seconds(1);
        Assert.That(Director.Guns.All(g => g.Active), Is.True);
        Assert.That(Director.WaveTwo.All(s => !s.rider.HasActivated), Is.True, "Pending wave is cancelled by finish across both restore passes");
        Assert.That(Inventory.PlasmaCapsules, Is.EqualTo(5)); Complete();
    }

    [UnityTest]
    public IEnumerator SharedPhysicsFollowsInclinesRejectsSmallSeamsAndJumpsUphill()
    { Setup(true); yield return new EnterPlayMode(); yield return SlopeDrive(); }
    private static IEnumerator SlopeDrive()
    {
        Application.runInBackground = true; Time.timeScale = 1;
        foreach (float slope in new[] { 0f, 12f, -12f })
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Slope fixture";
            floor.transform.position = new Vector3(7000, -.5f, 7000); floor.transform.localScale = new Vector3(30, 1, 300);
            floor.transform.rotation = Quaternion.Euler(-slope, 0, 0);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs + "/PF_RideableAlienBike.prefab"));
            var bike = root.GetComponent<AlienBikeController>();
            bike.maxSpeed = 29; bike.acceleration = 24;
            root.transform.position = new Vector3(7000, 30, 6930); Physics.SyncTransforms();
            Physics.Raycast(root.transform.position, Vector3.down, out var support, 100);
            root.transform.position = support.point + Vector3.up * bike.hoverHeight; Physics.SyncTransforms();
            var driver = root.AddComponent<SlopeBikeDriveProbe>(); driver.Bike = bike;
            Assert.That(bike.ClaimDriver(driver), Is.True); bike.StartDriving();
            yield return Seconds(3);
            Assert.That(bike.Speed, Is.GreaterThan(bike.maxSpeed * .8f));
            Assert.That(Vector3.Angle(root.transform.up, floor.transform.up), Is.LessThan(2), "Shared chassis follows slope " + slope);
            Assert.That(bike.IsGrounded, Is.True, "Continuous slope remains supported " + slope);
            if (slope > 0)
            {
                driver.Jump = true; yield return Seconds(.8f);
                Assert.That(bike.JumpCharge01, Is.GreaterThan(.5f)); driver.Jump = false; yield return Seconds(.1f);
                Assert.That(bike.IsGrounded, Is.False);
                Assert.That(bike.Body.linearVelocity.y - Mathf.Tan(slope * Mathf.Deg2Rad) * bike.Body.linearVelocity.z,
                    Is.GreaterThan(4), "Charged launch separates from the rising road even at the saved normal speed");
            }
            if (slope == 0)
            {
                var seams = new List<GameObject>();
                for (int i = 0; i < 8; i++)
                {
                    var seam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    seam.transform.position = new Vector3(7000, .045f, root.transform.position.z + 5 + i * 3);
                    seam.transform.localScale = new Vector3(4, .09f, .12f); seams.Add(seam);
                }
                float maxVertical = 0; int airborneSamples = 0;
                float end = Time.time + 2.5f;
                yield return Until(() => Time.time >= end, 4, "Small seam traverse", () =>
                {
                    maxVertical = Mathf.Max(maxVertical, Mathf.Abs(bike.Body.linearVelocity.y));
                    if (!bike.IsGrounded) airborneSamples++;
                });
                Assert.That(maxVertical, Is.LessThan(.8f), "Tiny asphalt seams do not produce repeated bounce impulses");
                Assert.That(airborneSamples, Is.Zero);
                foreach (var seam in seams) Object.Destroy(seam);
            }
            Object.Destroy(root); Object.Destroy(floor); yield return EditorTestFrame.Next();
        }
        Complete();
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator GuidedRiderDrivesEverySectionAndBothShortcutsWithSharedPhysics()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return RouteDriving(); }
    private static IEnumerator RouteDriving()
    {
        Director.enabled = false;
        Player.GetComponent<ThirdPersonController>().enabled = false;
        Player.GetComponent<CharacterController>().enabled = false;
        var template = Director.WaveOne[0].rider;
        var guide = Director.Guide;
        var boundaries = GameObject.Find("LevelGeometry/Smooth Corridor Collision").GetComponentsInChildren<MeshCollider>();
        var lines = new List<string>();
        void PlaceGoal(BikeRouteGuide.Sample goal)
        {
            Player.transform.SetPositionAndRotation(goal.position + Vector3.up * .1f, Quaternion.LookRotation(goal.forward));
            // This fixture moves an on-foot target far down-route. Move its unused,
            // kinematic bike too, rather than leaving an artificial roadblock at
            // the start that an actual mounted chase never has. Face the target
            // down-route too: V3 measures the envelope against Amy's live heading.
            Bike.transform.SetPositionAndRotation(goal.position + goal.Right * 2.4f + Vector3.up * Bike.hoverHeight,
                Quaternion.LookRotation(goal.forward));
            Physics.SyncTransforms();
        }
        foreach (int shortcut in new[] { -1, 8, 9 })
        {
            int first = shortcut < 0 ? 0 : shortcut == 8 ? 0 : 4;
            int last = shortcut < 0 ? 7 : shortcut == 8 ? 2 : 6;
            var start = guide.At(first, shortcut < 0 ? 15 : guide.paths[first].Length - 65);
            var goal = shortcut < 0 ? guide.At(last, guide.paths[last].Length - 4) : guide.At(shortcut, guide.paths[shortcut].Length - 4);
            PlaceGoal(goal);
            var rider = Object.Instantiate(template, template.transform.parent);
            string label = shortcut < 0 ? "Continuous main" : guide.paths[shortcut].label;
            rider.name = "Traversal " + label;
            rider.GetComponent<RunWorldObject>().ConfigureIdentity("traversal-" + shortcut);
            rider.GetComponent<EnemyRangedAttack>().enabled = false;
            rider.Activate(start.position + Vector3.up * .85f, Quaternion.LookRotation(Vector3.ProjectOnPlane(start.forward, Vector3.up)), false);
            float began = Time.time, peak = 0, jumpClearance = 0, maxLateral = 0;
            float nextTrace = began + 20;
            float lastProgress = start.progress;
            bool rejoinTarget = shortcut < 0;
            var visited = new HashSet<int>();
            float endProgress = shortcut < 0 ? guide.paths[last].endProgress - 20 : guide.paths[last].startProgress + 60;
            yield return Until(() => guide.Project(rider.transform.position).progress > endProgress,
                shortcut < 0 ? 330 : 100, "Physical traversal " + label, () =>
                {
                    var location = guide.Project(rider.transform.position);
                    if (Time.time >= nextTrace)
                    {
                        nextTrace += 20;
                        Debug.Log("Traversal " + label + " t=" + (Time.time - began).ToString("F1")
                            + " path=" + location.path + " at=" + location.distance.ToString("F1") + " pos=" + rider.transform.position
                            + " speed=" + rider.Bike.Speed.ToString("F1") + " intent=" + rider.Intent + " band=" + rider.Band);
                        Assert.That(location.progress - lastProgress, Is.GreaterThan(5), "AI remains stuck after repeated physical recovery: "
                            + label + " path=" + location.path + " at=" + location.distance + " pos=" + rider.transform.position);
                        lastProgress = location.progress;
                    }
                    visited.Add(location.path);
                    if (!rejoinTarget && location.path == shortcut && location.distance > guide.paths[shortcut].Length - 70)
                    {
                        PlaceGoal(guide.At(last, 85));
                        rejoinTarget = true;
                    }
                    peak = Mathf.Max(peak, rider.Bike.Speed);
                    jumpClearance = Mathf.Max(jumpClearance, rider.transform.position.y - location.position.y);
                    float lateral = Mathf.Abs(Vector3.Dot(rider.transform.position - location.position, location.Right));
                    maxLateral = Mathf.Max(maxLateral, lateral);
                    Assert.That(rider.transform.position.y - location.position.y, Is.GreaterThan(-3), "Bike stays on authored terrain " + label);
                    float side = Mathf.Sign(Vector3.Dot(rider.transform.position - location.position, location.Right));
                    float physicalLimit = float.PositiveInfinity;
                    var ray = new Ray(location.position + Vector3.up * .6f, location.Right * side);
                    foreach (var wall in boundaries)
                        if (wall.Raycast(ray, out var hit, 30)) physicalLimit = Mathf.Min(physicalLimit, hit.distance);
                    // Authored shoulders and forward joins are playable; measure the physical boundary,
                    // rather than treating the old centerline + 1 m heuristic as the wall.
                    float limit = float.IsPositiveInfinity(physicalLimit) ? location.halfWidth + 3.5f : physicalLimit + .25f;
                    Assert.That(lateral, Is.LessThan(limit), "AI stays in corridor "
                        + guide.paths[location.path].label + "; distance=" + location.distance + "; speed=" + rider.Bike.Speed
                        + "; intent=" + rider.Intent + "; position=" + rider.transform.position);
                });
            if (shortcut < 0) Assert.That(visited, Is.SupersetOf(Enumerable.Range(0, 8)));
            else Assert.That(visited, Does.Contain(shortcut), "AI takes Amy's authored shortcut, including the fork and forward rejoin");
            lines.Add(label + " seconds=" + (Time.time - began).ToString("F1") + " peak=" + peak.ToString("F1")
                + " clearance=" + jumpClearance.ToString("F1") + " lateral=" + maxLateral.ToString("F1"));
            Debug.Log(lines.Last());
            Object.Destroy(rider.gameObject); yield return EditorTestFrame.Next();
        }
        Directory.CreateDirectory("Logs/BikeRouteChase"); File.WriteAllLines("Logs/BikeRouteChase/AI-traversal.txt", lines);
        Complete();
    }
    [UnityTest, Timeout(240000)]
    public IEnumerator SixtySecondChaseUsesCatchUpContactAndSeparateRecovery()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return SustainedPressure(); }
    private static IEnumerator SustainedPressure()
    {
        SuppressLasers(); // Isolate physical/pistol behavior from the new automatic vehicle weapon.
        Director.enabled = false; yield return Mount();
        var guide = Director.Guide;
        var start = guide.At(0, 130);
        Bike.Body.position = start.position + Vector3.up * Bike.hoverHeight;
        Bike.Body.rotation = Quaternion.LookRotation(start.forward); Bike.Body.linearVelocity = Vector3.zero;
        // Durability fixture: preserve all real hits, but keep the driving probe alive
        // for 60 seconds. Laser travel/damage is exercised separately.
        var health = Player.GetComponent<PlayerHealth>();
        var data = new SerializedObject(health); data.FindProperty("maxHealth").floatValue = 10000; data.ApplyModifiedPropertiesWithoutUndo();
        health.RestoreRunHealth(10000, 50);
        var riders = Director.WaveOne.Select(s => s.rider).ToArray();
        var contacts = riders.Select(r => r.gameObject.AddComponent<ChaseContactProbe>()).ToArray();
        for (int i = 0; i < riders.Length; i++)
        {
            var rear = guide.At(0, 35 - i * 6);
            riders[i].Activate(rear.position + rear.Right * (i == 0 ? -1.7f : 1.7f) + Vector3.up * .85f,
                Quaternion.LookRotation(rear.forward), false);
        }
        float began = Time.time, nextLog = 0, nearSeconds = 0;
        var boosted = new bool[2]; var attacks = new int[2]; var recoveries = new int[2];
        var maneuvers = new HashSet<EnemyBikeDriver.VehicleAttack>();
        var lines = new List<string>();
        var envelopeEntries = new int[2]; var wasInside = new bool[2]; var wasOvershoot = new bool[2];
        float emptySeconds = 0, longestEmpty = 0, lastPressure = -1, intervalSum = 0;
        int pressureIntervals = 0, overshoots = 0, overshootReturns = 0, crossingTargets = 0;
        bool sprint = true;
        Directory.CreateDirectory("Logs/BikeRouteChaseV4");
        while (Time.time - began < 60)
        {
            // Full forward input even above the normal cap: do not accidentally
            // help pursuers by braking Amy whenever a turbo sample exceeds her normal cap.
            Foreground(); DrivePlayerRoute(float.MaxValue); if (Bike.Turbo01 < .02f) sprint = false;
            else if (Bike.Turbo01 > .98f) sprint = true;
            Input.SprintInput(sprint);
            if (riders.Any(r => r.InsideEnvelope)) emptySeconds = 0;
            else { emptySeconds += Time.deltaTime; longestEmpty = Mathf.Max(longestEmpty, emptySeconds); }
            Assert.That(riders.Count(r => r.Band == EnemyBikeDriver.DistanceBand.Attack), Is.LessThanOrEqualTo(1), "One primary contact attacker");
            float nearest = riders.Min(r => Vector3.Distance(r.transform.position, Bike.transform.position));
            if (nearest < 25) nearSeconds += Time.deltaTime;
            for (int i = 0; i < riders.Length; i++)
            {
                boosted[i] |= riders[i].CatchUpAdvantage;
                if (riders[i].InsideEnvelope && !wasInside[i]) envelopeEntries[i]++;
                wasInside[i] = riders[i].InsideEnvelope;
                if (riders[i].OvershootRecovering && !wasOvershoot[i]) overshoots++;
                if (!riders[i].OvershootRecovering && wasOvershoot[i]) overshootReturns++;
                wasOvershoot[i] = riders[i].OvershootRecovering;
                if (riders[i].AttackAttempts != attacks[i])
                {
                    if (riders[i].AttackAttempts > attacks[i])
                    {
                        attacks[i] = riders[i].AttackAttempts; maneuvers.Add(riders[i].Maneuver);
                        if (lastPressure >= 0) { intervalSum += Time.time - lastPressure; pressureIntervals++; }
                        lastPressure = Time.time;
                        Vector3 right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(Bike.transform.forward, Vector3.up)).normalized;
                        float side = Vector3.Dot(riders[i].transform.position - Bike.transform.position, right);
                        Vector3 predicted = Player.transform.position + Vector3.ProjectOnPlane(Bike.Body.linearVelocity, Vector3.up) * .6f;
                        if (side * Vector3.Dot(riders[i].ContactTarget - predicted, right) < -.1f) crossingTargets++;
                    }
                }
                recoveries[i] = riders[i].CompletedAttacks;
            }
            if (Time.time - began >= nextLog)
            {
                nextLog += 5;
                lines.Add((Time.time - began).ToString("F1") + "s player=" + Bike.Speed.ToString("F1") + " "
                    + string.Join(" | ", riders.Select(r => "gap=" + Vector3.Distance(r.transform.position, Bike.transform.position).ToString("F1")
                    + " long=" + r.LongitudinalOffset.ToString("F1") + " envelope=" + r.InsideEnvelope + " overshoot=" + r.OvershootRecovering + " speed=" + r.Bike.Speed.ToString("F1") + " pos=" + r.transform.position + " path=" + guide.Project(r.transform.position).path
                    + " intent=" + r.Intent + " turbo=" + r.Bike.Turbo01.ToString("F2") + " " + r.Band + "/" + r.Maneuver)));
                File.WriteAllLines("Logs/BikeRouteChaseV4/pressure.txt", lines);
                if (nextLog == 25 || nextLog == 45) ProceduralUIReview.Capture("chase-v4-pressure-" + (nextLog - 5), 1280, 720);
            }
            yield return EditorTestFrame.Next();
        }
        Input.SprintInput(false); Input.MoveInput(Vector2.zero);
        string summary = "nearSeconds=" + nearSeconds + " attacks=" + string.Join(",", attacks) + " recoveries=" + string.Join(",", recoveries)
            + " rawContactEntries=" + contacts.Sum(c => c.PlayerContacts) + " contactEpisodes=" + contacts.Sum(c => c.ContactEpisodes) + " impulse=" + contacts.Sum(c => c.ContactImpulse)
            + " damage=" + (10050 - health.CurrentHealth - health.CurrentArmor)
            + " envelopeEntries=" + string.Join(",", envelopeEntries) + " longestEmpty=" + longestEmpty
            + " averagePressureInterval=" + (pressureIntervals > 0 ? intervalSum / pressureIntervals : 60)
            + " overshoots=" + overshoots + " overshootReturns=" + overshootReturns + " handoffs=" + Director.ContactHandoffs + " crossingTargets=" + crossingTargets;
        lines.Add(summary); File.WriteAllLines("Logs/BikeRouteChaseV4/pressure.txt", lines); Debug.Log(summary);
        File.WriteAllLines("Logs/BikeRouteChaseV4/contacts.txt", contacts.SelectMany((c, i) => c.ContactEvents.Select(e => "rider=" + i + " " + e)));
        ProceduralUIReview.Capture("chase-v4-pressure", 1280, 720);
        Assert.That(boosted.All(v => v), Is.True, "Distant riders use their temporary catch-up advantage");
        // V4 explicitly raises Amy to 60 while preserving the enemy 57.5 catch-up cap.
        // Record gap/rhythm metrics without imposing V3's 29/50 balance thresholds.
        // Close three-rider coordination is checked separately in the front-pair fixture.
        Assert.That(nearSeconds, Is.GreaterThan(0), "Catch-up reaches actual interaction");
        Assert.That(attacks.Sum(), Is.GreaterThanOrEqualTo(2), "Physical commitments still repeat");
        Assert.That(recoveries.Sum(), Is.GreaterThanOrEqualTo(2), "Committed attacks still release into recovery");
        Assert.That(maneuvers.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(contacts.Sum(c => c.ContactEpisodes), Is.GreaterThanOrEqualTo(1));
        Assert.That(envelopeEntries.All(n => n >= 1), Is.True, "Both riders can reach the engagement envelope");
        Assert.That(crossingTargets, Is.GreaterThanOrEqualTo(1), "A crossing commitment aims beyond Amy's predicted line");
        Assert.That(contacts.Sum(c => c.ContactImpulse), Is.GreaterThan(1), "Actual collision impulses disturb the bikes");
        Assert.That(Bike.maxSpeed, Is.EqualTo(40.6f)); Assert.That(Bike.turboMaxSpeed, Is.EqualTo(70.1f));
        Complete();
    }

    [UnityTest]
    public IEnumerator OvershootingRiderBrakesAndReturnsWithoutTurningBack()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return OvershootRecovery(); }
    private static IEnumerator OvershootRecovery()
    {
        SuppressLasers(); // Isolate physical/pistol behavior from the new automatic vehicle weapon.
        Director.enabled = false; yield return Mount();
        var at = Director.Guide.At(0, 130);
        Bike.Body.position = at.position + Vector3.up * Bike.hoverHeight;
        Bike.Body.rotation = Quaternion.LookRotation(at.forward); Bike.Body.linearVelocity = at.forward * 29;
        var ahead = Director.Guide.Ahead(at, 32, at.path);
        var rider = Director.WaveOne[0].rider;
        rider.Activate(ahead.position + ahead.Right * 1.5f + Vector3.up * .85f, Quaternion.LookRotation(ahead.forward), false);
        yield return Until(() => rider.Bike.IsDriven, 3, "Physical enemy driver");
        rider.Bike.Body.linearVelocity = ahead.forward * 50;
        float maximumAhead = 0;
        yield return Until(() => rider.OvershootRecovering, 3, "Ahead rider enters slowdown", () => DrivePlayerRoute(29));
        yield return Until(() => rider.InsideEnvelope, 15, "Overshoot returns to engagement envelope", () =>
        {
            HealPlayer(); DrivePlayerRoute(29);
            maximumAhead = Mathf.Max(maximumAhead, rider.LongitudinalOffset);
            var road = Director.Guide.Project(rider.transform.position);
            Assert.That(Vector3.Dot(rider.transform.forward, road.forward), Is.GreaterThan(0), "Recovery must not U-turn into Amy");
        });
        Assert.That(maximumAhead, Is.LessThan(55), "Brake before running 50-100 m away");
        Debug.Log("V3 overshoot recovered; maxAhead=" + maximumAhead);
        Complete();
    }

    [UnityTest]
    public IEnumerator ContactSlotReleasesOnDeathDisableRestoreAndFinish()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return ContactLifetime(); }
    private static IEnumerator ContactLifetime()
    {
        SuppressLasers(); // Isolate physical/pistol behavior from the new automatic vehicle weapon.
        Director.enabled = false; yield return Mount();
        var at = Director.Guide.Project(Bike.transform.position);
        for (int i = 0; i < 2; i++)
        {
            var rear = Director.Guide.Ahead(at, -9 - i * 5, at.path);
            Director.WaveOne[i].rider.Activate(rear.position + rear.Right * (i == 0 ? -1.8f : 1.8f) + Vector3.up * .85f,
                Quaternion.LookRotation(rear.forward), false);
        }
        Action drive = () => { HealPlayer(); DrivePlayerRoute(12); };
        yield return Until(() => Director.PrimaryAttacker != null, 12, "First reservation", drive);
        var dead = Director.PrimaryAttacker;
        dead.GetComponent<EnemyHealth>().TakeDamage(1000);
        Assert.That(Director.PrimaryAttacker, Is.Null);
        yield return Until(() => Director.PrimaryAttacker != null, 15, "Survivor takes the released slot", drive);
        var survivor = Director.PrimaryAttacker;
        Assert.That(survivor, Is.Not.SameAs(dead));
        survivor.enabled = false; Assert.That(Director.PrimaryAttacker, Is.Null);
        survivor.enabled = true;
        yield return Until(() => Director.PrimaryAttacker != null, 15, "Reenabled rider can reserve", drive);
        string saved = survivor.CaptureRunState();
        survivor.RestoreRunState(saved); survivor.RestoreRunState(saved);
        Assert.That(Director.PrimaryAttacker, Is.Null, "Idempotent restore clears transient reservation");
        yield return Until(() => Director.PrimaryAttacker != null, 15, "Restored rider can reserve after readiness", drive);
        Director.ReachFinish(Player);
        Assert.That(Director.PrimaryAttacker, Is.Null);
        Assert.That(Director.TryReserveContact(survivor), Is.False);
        Complete();
    }

    [UnityTest]
    public IEnumerator ChaseHandheldPresentationStaysHiddenAcrossPausedEquipmentRestore()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return HandheldRestore(); }
    private static IEnumerator HandheldRestore()
    {
        Director.enabled = false; SuppressLasers(); yield return Mount();
        var rider = Director.WaveOne[0].rider;
        var at = Director.Guide.Ahead(Director.Guide.Project(Bike.transform.position), -20, 0);
        rider.Activate(at.position + Vector3.up * .85f, Quaternion.LookRotation(at.forward), false);
        yield return Seconds(.5f);
        var equipment = rider.GetComponent<EnemyEquipment>();
        Assert.That(equipment.HasWeapon, Is.True, "Existing logical equipment/loot is preserved");
        int ammo = equipment.Ammo; string saved = equipment.CaptureRunState();
        Time.timeScale = 0;
        equipment.RestoreRunState(saved); equipment.RestoreRunState(saved);
        Assert.That(equipment.Ammo, Is.EqualTo(ammo));
        Assert.That(rider.GetComponent<EnemyCombatPresentation>().Style, Is.EqualTo(WeaponAnimationStyle.Unarmed));
        Assert.That(equipment.Muzzle.gameObject.activeInHierarchy, Is.False);
        Assert.That(rider.GetComponent<EnemyRangedAttack>().enabled, Is.False);
        Time.timeScale = 1; Complete();
    }

    [UnityTest]
    public IEnumerator AllFiveRidersKeepSharedSeatedBodyThroughJumpAndFinishFlight()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return RiderPresentationAndFlight(); }
    private static IEnumerator RiderPresentationAndFlight()
    {
        SuppressLasers(); // Isolate physical/pistol behavior from the new automatic vehicle weapon.
        Director.enabled = false; yield return Mount();
        var riders = Director.WaveOne.Concat(Director.WaveTwo).Select(s => s.rider).ToArray();
        var reference = Object.Instantiate(riders[0].gameObject);
        reference.GetComponent<RunWorldObject>().ConfigureIdentity("seated-reference");
        reference.GetComponent<EnemyBikeDriver>().enabled = false;
        reference.SetActive(true);
        var referenceAnimator = reference.GetComponent<EnemyCombatPresentation>().Animator;
        referenceAnimator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AlienBikeSetup.PosePath);
        referenceAnimator.Update(.1f);
        var referencePose = new HumanPose();
        using (var handler = new HumanPoseHandler(referenceAnimator.avatar, referenceAnimator.transform)) handler.GetHumanPose(ref referencePose);
        Object.Destroy(reference);
        for (int i = 0; i < riders.Length; i++)
        {
            var at = Director.Guide.At(0, 100 + i * 7);
            riders[i].Activate(at.position + at.Right * (i % 2 == 0 ? -1.4f : 1.4f) + Vector3.up * .85f,
                Quaternion.LookRotation(at.forward), false);
        }
        yield return Seconds(.8f);
        foreach (var rider in riders) AssertSeatedBody(rider, referencePose);
        for (int i = 0; i < riders.Length; i++) CaptureRider(riders[i], "chase-v2-seated-" + (i + 1));
        foreach (var rider in riders)
        {
            rider.Bike.CheckGrounded(); rider.Bike.TickControls(1, true, false); rider.Bike.TickControls(.02f, false, false);
        }
        yield return Seconds(.2f);
        Assert.That(riders.Any(r => !r.Bike.IsGrounded), Is.True, "Shared physical jump is airborne");
        foreach (var rider in riders) AssertSeatedBody(rider, referencePose);
        CaptureRider(riders[0], "chase-v2-seated-jump");
        var starts = riders.Select(r => r.transform.position).ToArray();
        foreach (var rider in riders) rider.Disengage();
        yield return Seconds(2);
        foreach (var rider in riders)
        {
            AssertSeatedBody(rider, referencePose);
            Assert.That(rider.Band, Is.EqualTo(EnemyBikeDriver.DistanceBand.Flee));
            Assert.That(rider.Actor.CurrentTarget, Is.Null);
            Assert.That(rider.CatchUpAdvantage, Is.False);
            Assert.That(rider.Bike.maxSpeed, Is.EqualTo(29));
        }
        Assert.That(riders.Select((r, i) => Vector3.Distance(r.transform.position, starts[i])).All(d => d > 3), Is.True,
            "Disengaged riders keep physically moving instead of freezing/braking at the finish");
        CaptureRider(riders[1], "chase-v2-seated-flee"); Complete();
    }
    private static void AssertSeatedBody(EnemyBikeDriver rider, HumanPose reference)
    {
        var animator = rider.GetComponent<EnemyCombatPresentation>().Animator;
        var pose = new HumanPose();
        using (var handler = new HumanPoseHandler(animator.avatar, animator.transform)) handler.GetHumanPose(ref pose);
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
            if (HumanTrait.MuscleName[i].Contains("Leg"))
                Assert.That(pose.muscles[i], Is.EqualTo(reference.muscles[i]).Within(.08f), rider.name + " seated " + HumanTrait.MuscleName[i]);
        Assert.That(Vector3.Distance(pose.bodyPosition, reference.bodyPosition), Is.LessThan(.08f), "Standing animation must not replace the seated body/root");
    }
    private static void CaptureRider(EnemyBikeDriver rider, string name)
    {
        var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation;
        var at = Director.Guide.Project(rider.transform.position);
        float side = Mathf.Sign(Vector3.Dot(rider.transform.position - at.position, at.Right));
        try
        {
            camera.transform.position = rider.transform.position - at.Right * side * 3.6f - at.forward * 2.4f + Vector3.up * 1.6f;
            camera.transform.LookAt(rider.transform.position + Vector3.up * .8f);
            ProceduralUIReview.Capture(name, 1280, 720);
        }
        finally { camera.transform.SetPositionAndRotation(position, rotation); }
    }

    [UnityTest]
    public IEnumerator FightingBackUsesMountedPistolAgainstPhysicalChaseRider()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return FightBack(); }
    private static IEnumerator FightBack()
    {
        SuppressLasers(); // Isolate physical/pistol behavior from the new automatic vehicle weapon.
        Director.enabled = false;
        var pistol = Weapon("PlasmaPistolItem");
        if (Player.GetComponent<PlayerSkillState>().UnlockSkill(pistol.requiredSkill))
        {
            var knowledge = Object.FindAnyObjectByType<KnowledgeAcquiredPresenter>();
            yield return Until(() => knowledge.CurrentSkill == pistol.requiredSkill, 3, "Pistol skill acknowledgement");
            knowledge.Close(); yield return Seconds(.2f);
        }
        yield return Mount();
        var line = Director.Guide.Project(Bike.transform.position);
        var ahead = Director.Guide.Ahead(line, 13, line.path);
        var enemy = Director.WaveOne[0].rider;
        enemy.Activate(ahead.position + ahead.Right + Vector3.up * .85f, Quaternion.LookRotation(ahead.forward), false);
        var enemyHealth = enemy.GetComponent<EnemyHealth>();
        float began = Time.time, initialHealth = enemyHealth.CurrentHealth, nextReport = 0;
        yield return Until(() => enemy == null || enemyHealth.CurrentHealth <= initialHealth - 36, 15, "Mounted pistol damages actual moving rider", () =>
        {
            DrivePlayerRoute(18);
            Input.ShootInput(Mathf.Repeat(Time.time - began, .8f) < .35f);
            if (Time.time - began >= nextReport)
            {
                nextReport += 5;
                Debug.Log("Fight back t=" + (Time.time - began) + " enemyHealth=" + enemyHealth.CurrentHealth
                    + " rounds=" + Player.GetComponent<PlayerShooter>().CurrentAmmo + " aim=" + Player.GetComponent<PlayerAim>().CurrentTarget
                    + " target=" + enemy.GetComponent<AimTarget>().BodyCenter + " state=" + enemy.Band);
            }
        });
        Input.ShootInput(false); Input.MoveInput(Vector2.zero);
        Assert.That(initialHealth, Is.GreaterThan(0));
        Assert.That(Player.GetComponent<PlayerShooter>().CurrentAmmo, Is.LessThan(pistol.magazineSize));
        Assert.That(Player.IsDriving, Is.True); Complete();
    }

    [UnityTest]
    public IEnumerator ActualFinishTransfersPressureWhileRidersKeepMoving()
    { Setup(); yield return new EnterPlayMode(); yield return Initialize(); yield return FinishFlight(); }
    private static IEnumerator FinishFlight()
    {
        Director.enabled = false; yield return Mount();
        var trigger = Object.FindAnyObjectByType<BikeRouteFinishTrigger>();
        var line = Director.Guide.Project(trigger.transform.position - Vector3.up * 4);
        Bike.Body.position = line.position - line.forward * 7 + Vector3.up * .85f;
        Bike.Body.rotation = Quaternion.LookRotation(line.forward); Bike.Body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
        var riders = Director.WaveOne.Select(s => s.rider).ToArray();
        for (int i = 0; i < riders.Length; i++)
        {
            var at = Director.Guide.Ahead(line, -16 - i * 5, line.path);
            riders[i].Activate(at.position + at.Right * (i == 0 ? -1.5f : 1.5f) + Vector3.up * .85f,
                Quaternion.LookRotation(at.forward), false);
            // Let the defense exchange continue long enough to observe flight.
            var health = riders[i].GetComponent<EnemyHealth>();
            var serialized = new SerializedObject(health); serialized.FindProperty("maxHealth").floatValue = 600;
            serialized.ApplyModifiedPropertiesWithoutUndo(); health.RestoreRunHealth(600);
        }
        Input.MoveInput(Vector2.up);
        yield return Until(() => Director.Finished, 4, "Actual finish crossing");
        Input.MoveInput(Vector2.zero);
        var positions = riders.Select(r => r.transform.position).ToArray();
        var lasers = riders.Select(r => r.GetComponent<AlienBikeLaserWeapon>()).ToArray();
        int vehicleShotsAtFinish = lasers.Sum(w => w.ShotsFired);
        int shotsAtFinish = 0; foreach (var r in riders) r.GetComponent<EnemyRangedAttack>().ShotFired += (hit, collider) => shotsAtFinish++;
        yield return Seconds(1.5f);
        for (int i = 0; i < riders.Length; i++)
        {
            Assert.That(riders[i].Band, Is.EqualTo(EnemyBikeDriver.DistanceBand.Flee));
            Assert.That(riders[i].Actor.CurrentTarget, Is.Null);
            Assert.That(Vector3.Dot(riders[i].transform.position - positions[i], line.forward), Is.GreaterThan(3));
        }
        Assert.That(Director.PrimaryAttacker, Is.Null, "Finish clears the contact reservation");
        Assert.That(Director.LeadBlocker, Is.Null, "Finish clears the lead reservation");
        Assert.That(riders.All(r => !r.IsLeadBlocker), Is.True);
        Assert.That(shotsAtFinish, Is.Zero, "The finish immediately removes attacks against Amy");
        Assert.That(lasers.All(w => w.Target == null && w.Progress == 0), Is.True, "Finish cancels incoming locks");
        Assert.That(lasers.Sum(w => w.ShotsFired), Is.EqualTo(vehicleShotsAtFinish), "Fleeing vehicles cannot fire at Amy");
        Assert.That(Director.Guns.Sum(g => g.ShotsFired), Is.GreaterThan(0));
        Assert.That(Player.IsDriving && Input.CanProcessBikeControls, Is.True);
        CaptureRider(riders[0], "chase-v2-finish-defense-flight"); Complete();
    }

    private static void SuppressLasers()
    {
        Director.PlayerBike.GetComponent<AlienBikeLaserWeapon>().enabled = false;
        foreach (var slot in Director.WaveOne.Concat(Director.WaveTwo)) slot.rider.GetComponent<AlienBikeLaserWeapon>().enabled = false;
    }
    private static void HealPlayer()
    {
        var health = Player.GetComponent<PlayerHealth>();
        if (health.CurrentHealth < 50 && !health.IsDead) health.RestoreRunHealth(health.MaxHealth, 0);
    }
    private static void DrivePlayerRoute(float speed)
    {
        var at = Director.Guide.Project(Bike.transform.position);
        var ahead = Director.Guide.Ahead(at, 12 + Bike.Speed * .35f, at.path);
        float turn = Vector3.SignedAngle(Vector3.ProjectOnPlane(Bike.transform.forward, Vector3.up),
            Vector3.ProjectOnPlane(ahead.position - Bike.transform.position, Vector3.up), Vector3.up);
        Input.MoveInput(new Vector2(Mathf.Clamp(turn / 30, -1, 1), Bike.Speed < speed ? 1 : 0));
    }
    private static void Complete() => SessionState.SetBool(Key + "Complete", true);
    private static IEnumerator Seconds(float duration)
    { float end = Time.time + duration; yield return Until(() => Time.time >= end, duration + 2, "Wait"); }
    private static IEnumerator Until(Func<bool> predicate, float seconds, string reason, Action tick = null, bool foreground = true)
    {
        float end = Time.unscaledTime + seconds; double deadline = EditorApplication.timeSinceStartup + seconds * 4 + 15;
        while (!predicate() && Time.unscaledTime < end && EditorApplication.timeSinceStartup < deadline)
        { if (foreground) Foreground(); tick?.Invoke(); yield return EditorTestFrame.Next(); }
        tick?.Invoke();
        Assert.That(predicate(), Is.True, reason + "; wave=" + Director?.Wave + "; player=" + Director?.Player.transform.position
            + "; riders=" + (Director == null ? "" : string.Join(" | ", Director.WaveOne.Concat(Director.WaveTwo).Select(s => s.rider == null ? "dead" : s.rider.name + ":" + s.rider.State + " " + s.rider.transform.position))));
    }
    private static void Foreground()
    {
        ActiveRunController.Instance?.SendMessage("OnApplicationPause", false); ActiveRunController.Instance?.SendMessage("OnApplicationFocus", true);
        var menu = Object.FindAnyObjectByType<InGameMenuController>(); if (menu != null && menu.IsOpen) menu.ResumeGame();
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) { ActiveRunController.Instance?.PrepareToLeave(); yield return new ExitPlayMode(); }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Environment.SetEnvironmentVariable("KIDS_TEST_SAVE_DIRECTORY", SessionState.GetString(Key, ""));
        GameplayCameraSettings.Mode = (GameplayCameraMode)SessionState.GetInt(Key + "Camera", 0); Time.timeScale = 1;
        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
            Assert.That(SessionState.GetBool(Key + "Complete", false), Is.True, "Test body must complete across Play Mode reload");
        SessionState.EraseString(Key); SessionState.EraseInt(Key + "Camera"); SessionState.EraseBool(Key + "Complete");
    }
}
