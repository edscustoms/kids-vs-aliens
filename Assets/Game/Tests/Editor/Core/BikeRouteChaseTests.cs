using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

public sealed class BikeRouteChaseTests
{
    [Test]
    public void AuthoredChaseUsesSharedPhysicsExplicitPeersAndDistinctTiming()
    {
        EditorSceneManager.OpenScene(BikeRouteChaseSetup.ScenePath);
        try
        {
            var director = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
            var slots = director.WaveOne.Concat(director.WaveTwo).ToArray();
            Assert.That(director.WaveOne.Length, Is.EqualTo(2));
            Assert.That(director.WaveTwo.Count(s => s.frontPass), Is.EqualTo(2));
            Assert.That(director.WaveTwo.Count(s => !s.frontPass), Is.EqualTo(1));
            Assert.That(slots.Select(s => s.rider).Distinct().Count(), Is.EqualTo(5));
            Assert.That(slots.Select(s => s.rider.FireDelay).Distinct().Count(), Is.EqualTo(5));
            Assert.That(director.WaveTwo[0].activationDelay, Is.Not.EqualTo(director.WaveTwo[1].activationDelay));
            Assert.That(new SerializedObject(director).FindProperty("contactHandoffDelay").floatValue, Is.EqualTo(.7f));
            var seated = AssetDatabase.LoadAssetAtPath<AnimationClip>(AlienBikeSetup.Art + "/Bike_RiderSit.anim");
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(BikeRouteChaseSetup.Content + "/BikeRiderEnemy.controller");
            Assert.That(controller.layers[0].stateMachine.states.Where(s => s.state.name.EndsWith("Locomotion"))
                .All(s => s.state.motion == seated), Is.True, "The shared seated clip owns the full base body, not a leg-only override");
            var upper = controller.layers[1].avatarMask;
            Assert.That(upper.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.False);
            Assert.That(upper.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False);
            Assert.That(upper.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), Is.False);
            var footProfile = AssetDatabase.LoadAssetAtPath<EnemyCombatProfile>(EnemyCombatantSetup.ProfilePath);
            foreach (var slot in slots)
            {
                var rider = slot.rider; var root = rider.gameObject;
                var driverData = new SerializedObject(rider);
                Assert.That(driverData.FindProperty("attackRecoverySeconds").floatValue, Is.EqualTo(1.4f));
                Assert.That(driverData.FindProperty("crossingExit").floatValue, Is.EqualTo(2.7f));
                Assert.That(root.activeSelf, Is.False);
                Assert.That(root.GetComponentsInChildren<Rigidbody>(true).Length, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<AlienBikeController>(true).Length, Is.EqualTo(1));
                Assert.That(root.GetComponent<NavMeshAgent>().enabled, Is.False);
                Assert.That(root.GetComponent<EnemyBrain>().enabled, Is.False);
                Assert.That(root.GetComponent<EnemyMotor>().enabled, Is.False);
                Assert.That(root.GetComponent<EnemyRangedAttack>().enabled, Is.False, "Only chase handheld firing is removed");
                Assert.That(root.GetComponent<EnemyPerception>().enabled, Is.True);
                Assert.That(Vector3.Distance(root.GetComponent<EnemyCombatPresentation>().Visual.transform.position,
                    root.GetComponent<AlienBikeController>().seatPoint.position), Is.LessThan(.001f), "Seated presentation must account for nested visual wrappers");
                Assert.That(root.GetComponent<EnemyEquipment>().Profile.allowedWeapons, Does.Contain(Weapon("PlasmaPistolItem")));
                Assert.That(root.GetComponent<EnemyEquipment>().Profile, Is.Not.SameAs(footProfile), "Chase balance is isolated from normal aliens");
                Assert.That(root.GetComponent<AlienBikeController>().maxSpeed, Is.EqualTo(29));
                Assert.That(root.GetComponent<AlienBikeController>().turboMaxSpeed, Is.EqualTo(50));
                var laser = root.GetComponent<AlienBikeLaserWeapon>();
                Assert.That(laser, Is.Not.Null); Assert.That(laser.targets, Is.EqualTo(new[] { director.PlayerBike }));
                Assert.That(laser.lockSeconds, Is.EqualTo(3.5f)); Assert.That(laser.damage, Is.EqualTo(30));
                AlienBikeSetup.ConfigureRideable(root.GetComponent<AlienBikeController>());
                Assert.That(root.GetComponent<AlienBikeImpact>(), Is.Null, "Bike contact is physical, not on-foot enemy knockback");
            }
            var identities = Object.FindObjectsByType<RunWorldObject>(FindObjectsInactive.Include).Select(r => r.Id).ToArray();
            Assert.That(director.PlayerBike.maxSpeed, Is.EqualTo(40.6f)); Assert.That(director.PlayerBike.turboMaxSpeed, Is.EqualTo(70.1f));
            Assert.That(director.PlayerBike.acceleration, Is.EqualTo(28)); Assert.That(director.PlayerBike.turboAcceleration, Is.EqualTo(53));
            Assert.That(director.PlayerBike.reverseSpeed, Is.EqualTo(6));
            var playerLaser = director.PlayerBike.GetComponent<AlienBikeLaserWeapon>();
            Assert.That(playerLaser.targets, Is.EquivalentTo(slots.Select(s => s.rider.GetComponent<AlienBikeController>())));
            Assert.That(slots.Select(s => s.rider.GetComponent<AlienBikeLaserWeapon>().initialDelay).Distinct().Count(), Is.EqualTo(5));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCombatantSetup.PrefabPath).GetComponent<EnemyRangedAttack>().enabled, Is.True);
            Assert.That(identities, Has.None.Null.And.None.Empty);
            Assert.That(identities.Distinct().Count(), Is.EqualTo(identities.Length));
            Assert.That(director.Guns.Length, Is.EqualTo(4));
            Assert.That(director.Guns.Select(g => g.Phase).Distinct().Count(), Is.EqualTo(4));
            Assert.That(director.Guns.All(g => !g.Active), Is.True);
            Assert.That(Object.FindAnyObjectByType<BikeRouteFinishTrigger>().GetComponent<BoxCollider>().isTrigger, Is.True);
            Assert.That(Object.FindObjectsByType<PickupItem>().All(p => p.Item is CapsuleItemData), Is.True);
            Assert.That(director.Guide.paths.Count(p => p.shortcut), Is.EqualTo(2));
            Assert.That(director.Guide.spawnAnchors.Any(a => a.path == 8), Is.True);
            Assert.That(director.Guide.spawnAnchors.Any(a => a.path == 9), Is.True);
            Assert.That(director.Guide.jumps.Select(j => j.path), Is.EquivalentTo(new[] { 3, 5, 6, 6 }));
            Assert.That(director.Guide.jumps.All(j => j.releaseDistance > 40), Is.True);
        }
        finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
    }

    [Test]
    public void SharedRepairPreservesCollectorAndHandling()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs + "/PF_RideableAlienBike.prefab"));
        try
        {
            var bike = root.GetComponent<AlienBikeController>(); bike.maxSpeed = 31; bike.hoverSpring = 73;
            AlienBikeSetup.ConfigureRideable(bike); AlienBikeSetup.ConfigureRideable(bike);
            Assert.That(root.GetComponents<AlienBikePickupCollector>().Length, Is.EqualTo(1));
            Assert.That(root.GetComponents<MonoBehaviour>().OfType<IDamageable>().Count(), Is.EqualTo(1), "Existing AlienBikeImpact forwards occupied hull hits");
            Assert.That(bike.maxSpeed, Is.EqualTo(31)); Assert.That(bike.hoverSpring, Is.EqualTo(73));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [TestCase(0)] [TestCase(12)] [TestCase(-12)]
    public void SupportRecognizesFlatAndSlopeRelativeVelocity(float slope)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs + "/PF_RideableAlienBike.prefab"));
        try
        {
            floor.transform.position = new Vector3(7000, -.5f, 7000);
            floor.transform.localScale = new Vector3(20, 1, 100);
            floor.transform.rotation = Quaternion.Euler(-slope, 0, 0);
            var bike = root.GetComponent<AlienBikeController>();
            root.transform.position = new Vector3(7000, 4, 7000); Physics.SyncTransforms();
            Assert.That(Physics.Raycast(root.transform.position, Vector3.down, out var hit, 10), Is.True);
            root.transform.position = hit.point + Vector3.up * bike.hoverHeight; Physics.SyncTransforms();
            bike.CheckGrounded(); bike.StartDriving();
            bike.Body.linearVelocity = new Vector3(0, Mathf.Tan(slope * Mathf.Deg2Rad) * 22, 22);
            Assert.That(bike.CheckGrounded(), Is.True, "Support is measured relative to the slope, not world vertical speed");
            bike.TickControls(.7f, true, false); Assert.That(bike.JumpCharge01, Is.GreaterThan(.49f));
            bike.TickControls(.01f, false, false);
            Assert.That(bike.Body.linearVelocity.y - Mathf.Tan(slope * Mathf.Deg2Rad) * 22, Is.EqualTo(6.5f).Within(.01),
                "Launch clears the road at the same relative speed uphill, flat and downhill");
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(floor); }
    }
    private static WeaponItemData Weapon(string name) => AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/" + name + ".asset");
}
