using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EasyRoads3Dv3;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Explicit BikeRoute content creation. Never runs as part of shared scene repair.</summary>
public static class BikeRouteChaseSetup
{
    public const string ScenePath = "Assets/Game/Scenes/BikeRoute.unity";
    public const string Content = "Assets/Game/Scenes/BikeRoute/Chase";
    public const string EnemyPrefab = Content + "/PF_EnemyBikeRider.prefab";

    [MenuItem("Tools/Level Authoring/Create BikeRoute Chase Content")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath || Application.isPlaying)
            throw new InvalidOperationException("Open the saved BikeRoute scene outside Play Mode.");
        if (Object.FindAnyObjectByType<BikeRouteChaseDirector>(FindObjectsInactive.Include) != null)
            throw new InvalidOperationException("Chase content already exists. Edit its explicit scene references and Inspector tuning.");
        var player = Object.FindAnyObjectByType<PlayerBikeRider>();
        var bike = Object.FindObjectsByType<AlienBikeController>().Single();
        var roads = Object.FindObjectsByType<ERModularRoad>(FindObjectsInactive.Include).OrderBy(r => r.name).ToArray();
        if (player == null || roads.Length != 10 || Camera.main == null)
            throw new InvalidOperationException("Expected the existing player, camera and ten editable source paths.");
        Directory.CreateDirectory(Content);
        AssetDatabase.Refresh();
        var root = new GameObject("BikeRoute Chase");
        root.AddComponent<RunWorldObject>().ConfigureIdentity("bike-route-chase");
        var director = root.AddComponent<BikeRouteChaseDirector>();
        var guide = Child(root.transform, "Route Guide").gameObject.AddComponent<BikeRouteGuide>();
        BuildGuide(guide, roads);
        Physics.SyncTransforms();
        BuildAnchors(guide);
        BuildJumpHints(guide, scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)));
        var start = guide.At(0, 55);
        bike.transform.SetPositionAndRotation(start.position + Vector3.up * bike.hoverHeight,
            Quaternion.LookRotation(Vector3.ProjectOnPlane(start.forward, Vector3.up)));
        player.transform.SetPositionAndRotation(start.position - start.Right * 2.4f + Vector3.up * .08f, bike.transform.rotation);
        AlienBikeSetup.ConfigureRideable(bike);
        PrefabUtility.RecordPrefabInstancePropertyModifications(bike.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
        var prefab = CreateEnemyPrefab(bike);
        var riders = new EnemyBikeDriver[5];
        for (int i = 0; i < riders.Length; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            go.name = i < 2 ? "Wave 1 Rear " + (i + 1) : i < 4 ? "Wave 2 Front " + (i - 1) : "Wave 2 Rear";
            go.transform.SetPositionAndRotation(guide.At(0, 20 + i * 6).position + Vector3.up * bike.hoverHeight, bike.transform.rotation);
            go.GetComponent<RunWorldObject>().ConfigureIdentity("bike-route-rider-" + (i + 1));
            var rider = go.GetComponent<EnemyBikeDriver>();
            riders[i] = rider;
            Set(rider, "guide", guide); Set(rider, "player", player);
            Number(rider, "preferredSide", i % 2 == 0 ? -1 : 1);
            Number(rider, "reactionInterval", .13f + .037f * i);
            Number(rider, "fireDelay", .12f + .23f * i);
            Number(rider, "preferredDistance", 7.5f + i * 1.4f);
            Number(rider, "throttleAggression", .92f + i * .055f);
            Number(rider, "jumpTimingOffset", (i - 2) * .45f);
            Set(go.GetComponent<EnemyEquipment>(), "startingWeapon", Weapon(i % 2 == 0 ? "PlasmaPistolItem" : "PlasmaRifleItem"));
            foreach (var component in go.GetComponents<Component>()) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            go.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        }
        foreach (var rider in riders) ArrayReferences(rider, "peers", riders);
        Set(director, "player", player); Set(director, "playerBike", bike); Set(director, "guide", guide);
        Set(director, "gameplayCamera", Camera.main);
        Set(director, "pistol", Weapon("PlasmaPistolItem")); Set(director, "rifle", Weapon("PlasmaRifleItem"));
        Slots(director, "waveOne", riders.Take(2).ToArray(), new[] { false, false }, new[] { 0f, .85f });
        Slots(director, "waveTwo", riders.Skip(2).ToArray(), new[] { true, true, false }, new[] { .5f, 2.7f, 1.2f });
        BuildFinish(root.transform, director, riders.Select(r => r.GetComponent<EnemyActor>()).ToArray());
        BuildPlasma(root.transform, guide);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("BIKEROUTE CHASE AUTHORED: five dormant physical riders, two waves, four finish rifles, "
            + guide.spawnAnchors.Length + " safe guide anchors. Player tuning preserved: " + bike.maxSpeed + "/" + bike.turboMaxSpeed);
    }

    public static void BuildBatch()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Build();
    }

    [MenuItem("Tools/Level Authoring/Update BikeRoute Chase Guide")]
    public static void UpdateGuide()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath || Application.isPlaying) throw new InvalidOperationException("Open BikeRoute outside Play Mode.");
        var guide = Object.FindAnyObjectByType<BikeRouteGuide>();
        if (guide == null) throw new InvalidOperationException("Create the chase content first.");
        BuildGuide(guide, Object.FindObjectsByType<ERModularRoad>(FindObjectsInactive.Include).OrderBy(r => r.name).ToArray());
        Physics.SyncTransforms(); BuildAnchors(guide);
        BuildJumpHints(guide, scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)));
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void BuildGuide(BikeRouteGuide guide, ERModularRoad[] roads)
    {
        if (roads.Length != 10 || Enumerable.Range(0, 8).Any(i => !roads[i].name.StartsWith((i + 1).ToString("00") + "_"))
            || !roads[8].name.StartsWith("A_") || !roads[9].name.StartsWith("B_"))
            throw new InvalidOperationException("Expected sections 01-08 and shortcuts A/B. Review the explicit join mapping before changing this guide.");
        guide.paths = new BikeRouteGuide.Path[roads.Length];
        float progress = 0;
        for (int i = 0; i < roads.Length; i++)
        {
            var points = new List<Vector3> { roads[i].splinePoints[0] };
            foreach (var point in roads[i].splinePoints.Skip(1))
                if (Vector3.Distance(points[points.Count - 1], point) >= 4) points.Add(point);
            if (Vector3.Distance(points[points.Count - 1], roads[i].splinePoints.Last()) > .05f) points.Add(roads[i].splinePoints.Last());
            var distances = new float[points.Count];
            for (int p = 1; p < points.Count; p++) distances[p] = distances[p - 1] + Vector3.Distance(points[p - 1], points[p]);
            var path = new BikeRouteGuide.Path { label = roads[i].name, points = points.ToArray(), distances = distances,
                halfWidth = roads[i].roadWidth * .5f, shortcut = i >= 8, startProgress = progress, endProgress = progress + distances.Last() };
            if (i >= 8)
            {
                var replaced = guide.paths[i == 8 ? 1 : 5];
                path.startProgress = replaced.startProgress; path.endProgress = replaced.endProgress;
            }
            else progress = path.endProgress;
            guide.paths[i] = path;
        }
    }

    private static void BuildAnchors(BikeRouteGuide guide)
    {
        var anchors = new List<BikeRouteGuide.SpawnAnchor>();
        for (int p = 0; p < guide.paths.Length; p++)
            for (float d = 12; d < guide.paths[p].Length - 10; d += 8)
            {
                var sample = guide.At(p, d);
                if (Mathf.Abs(sample.forward.y) > .22f || sample.halfWidth < 3) continue;
                if (!Physics.Raycast(sample.position + Vector3.up * 3, Vector3.down, out var hit, 5, ~0, QueryTriggerInteraction.Ignore)
                    || hit.normal.y < .92f || Mathf.Abs(hit.point.y - sample.position.y) > .6f) continue;
                bool safe = true;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 lane = hit.point + sample.Right * side * Mathf.Min(1.7f, sample.halfWidth - 1.15f);
                    if (!Physics.Raycast(lane + Vector3.up * 2, Vector3.down, out var support, 3, ~0, QueryTriggerInteraction.Ignore)
                        || support.normal.y < .92f || Mathf.Abs(support.point.y - hit.point.y) > .25f
                        || Physics.CheckBox(lane + Vector3.up * 1.65f, new Vector3(1, .86f, 1.85f),
                            Quaternion.LookRotation(sample.forward), ~0, QueryTriggerInteraction.Ignore)) { safe = false; break; }
                }
                if (safe) anchors.Add(new BikeRouteGuide.SpawnAnchor { path = p, distance = d,
                    position = hit.point, forward = sample.forward, halfWidth = sample.halfWidth });
            }
        guide.spawnAnchors = anchors.ToArray();
        if (anchors.Count < 40) throw new InvalidOperationException("Unexpectedly few clear route anchors; inspect support before saving.");
    }

    private static void BuildJumpHints(BikeRouteGuide guide, IEnumerable<Transform> transforms)
    {
        var jumps = transforms.Single(t => t.name == "JumpTests");
        var hints = new List<BikeRouteGuide.JumpHint>();
        foreach (Transform ramp in jumps)
        {
            var bounds = ramp.GetComponentsInChildren<MeshRenderer>().Select(r => r.bounds)
                .OrderByDescending(b => b.size.sqrMagnitude).First();
            var sample = guide.Project(bounds.center);
            // Charge on the flat approach and release before the authored ramp begins.
            float halfLength = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(sample.forward.x), 0, Mathf.Abs(sample.forward.z)));
            hints.Add(new BikeRouteGuide.JumpHint { path = sample.path,
                releaseDistance = Mathf.Max(0, sample.distance - Mathf.Clamp(halfLength, 8, 15) - 3), chargeLead = 42 });
            Debug.Log("Jump hint " + ramp.name + " path=" + sample.path + " at=" + sample.distance + " halfLength=" + halfLength);
        }
        guide.jumps = hints.ToArray();
    }

    // Explicit chase-only update; never invoked by shared setup/repair.
    [MenuItem("Tools/Level Authoring/Update BikeRoute Chase Riders")]
    public static void UpdateRiders()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Leave Play Mode before updating rider assets.");
        var profile = AssetDatabase.LoadAssetAtPath<EnemyCombatProfile>(Content + "/BikeRiderCombat.asset");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Content + "/BikeRiderEnemy.controller");
        if (profile == null || controller == null) throw new InvalidOperationException("Create BikeRoute chase content first.");
        TuneChaseCombat(profile);
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        ConfigureSeatedController(controller);
        Debug.Log("BikeRoute riders updated: shared seated base, weapon upper body and chase-only combat tuning. Scene/other enemies unchanged.");
    }

    private static void TuneChaseCombat(EnemyCombatProfile profile)
    {
        profile.aimDelay = .4f;
        profile.spreadDegrees = 7;
        profile.automaticBurstCount = 2; profile.automaticBurstVariation = 0;
        profile.burstPause = 3.2f;
        profile.pistolBehavior.burstPauseMultiplier = new Vector2(1, 1.2f);
        profile.rifleBehavior.burstPauseMultiplier = new Vector2(1, 1.2f);
    }

    private static void ConfigureSeatedController(AnimatorController controller)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AlienBikeSetup.Art + "/Bike_RiderSit.anim");
        if (clip == null) throw new InvalidOperationException("Missing the shared Humanoid rider pose.");
        string maskPath = Content + "/RiderUpperBody.mask";
        if (AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath) == null
            && AssetDatabase.LoadAssetAtPath<AvatarMask>(Content + "/SeatedLowerBody.mask") != null)
            AssetDatabase.MoveAsset(Content + "/SeatedLowerBody.mask", maskPath);
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, maskPath); }
        mask.name = "Rider Upper Body";
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.Head })
            mask.SetHumanoidBodyPartActive(part, true);
        EditorUtility.SetDirty(mask); AssetDatabase.SaveAssetIfDirty(mask);
        var baseLayer = controller.layers[0];
        var pistolPose = BakeSeatedWeaponPose(WeaponAnimationStyle.Pistol, "RiderPistolUpperBody");
        var riflePose = BakeSeatedWeaponPose(WeaponAnimationStyle.Rifle, "RiderRifleUpperBody");
        var arms = new AnimatorControllerLayer { name = "Weapon Upper Body", syncedLayerIndex = 0,
            defaultWeight = 1, avatarMask = mask, blendingMode = AnimatorLayerBlendingMode.Override };
        foreach (var entry in baseLayer.stateMachine.states)
        {
            var state = entry.state;
            if (!state.name.EndsWith("Locomotion")) continue;
            var weaponMotion = state.name == "PistolLocomotion" ? pistolPose : state.name == "RifleLocomotion" ? riflePose : clip;
            arms.SetOverrideMotion(state, weaponMotion);
            state.motion = clip;
            EditorUtility.SetDirty(state);
        }
        // Keep layer-zero semantic ready states used by EnemyCombatPresentation.
        // Root and legs come from Amy's actual seated clip. The synchronized
        // weapon torso/arms cannot restore a standing root translation.
        controller.layers = new[] { baseLayer, arms };
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller);
    }

    private static AnimationClip BakeSeatedWeaponPose(WeaponAnimationStyle style, string name)
    {
        // Imported aiming poses include a body-root yaw. Masking away that root
        // while retaining their raw arm muscles turns the muzzle sideways. Bake
        // their authored upper-body orientation against the seated pelvis once,
        // as Humanoid animation data; no runtime bone/weapon correction is needed.
        var root = PrefabUtility.LoadPrefabContents(EnemyCombatantSetup.PrefabPath);
        try
        {
            var animator = root.GetComponent<EnemyCombatPresentation>().Animator;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EnemyCombatantSetup.ControllerPath);
            animator.Rebind(); animator.Update(0);
            animator.SetInteger("WeaponStyle", (int)style); animator.Update(1); animator.Update(1);
            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            int Depth(Transform t) { int depth = 0; while (t.parent != null) { depth++; t = t.parent; } return depth; }
            var upper = Enumerable.Range(0, (int)HumanBodyBones.LastBone)
                .Select(i => animator.GetBoneTransform((HumanBodyBones)i))
                .Where(t => t != null && (t == spine || t.IsChildOf(spine))).OrderBy(Depth).ToArray();
            var orientations = upper.Select(t => t.rotation).ToArray();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AlienBikeSetup.PosePath);
            animator.Rebind(); animator.Update(0); animator.Update(1);
            for (int i = 0; i < upper.Length; i++) upper[i].rotation = orientations[i];
            var pose = new HumanPose();
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform)) handler.GetHumanPose(ref pose);
            string path = Content + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool created = clip == null;
            if (created) clip = new AnimationClip { name = name, frameRate = 30 };
            foreach (var binding in AnimationUtility.GetCurveBindings(clip)) AnimationUtility.SetEditorCurve(clip, binding, null);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string muscle = HumanTrait.MuscleName[i];
                if (muscle.Contains("Leg") || muscle.Contains("Foot") || muscle.Contains("Toes")) continue;
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle),
                    AnimationCurve.Constant(0, 1, pose.muscles[i]));
            }
            if (created) AssetDatabase.CreateAsset(clip, path);
            else { EditorUtility.SetDirty(clip); AssetDatabase.SaveAssetIfDirty(clip); }
            return clip;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static GameObject CreateEnemyPrefab(AlienBikeController playerBike)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefab);
        if (existing != null) return existing;
        var profile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyCombatProfile>(EnemyCombatantSetup.ProfilePath));
        profile.name = "Bike Rider Combat"; profile.meleeEnabled = false; profile.weaponPickupEnabled = false;
        profile.preferredRange = 13; profile.facingTolerance = 28;
        TuneChaseCombat(profile);
        AssetDatabase.CreateAsset(profile, Content + "/BikeRiderCombat.asset");
        AssetDatabase.CopyAsset(EnemyCombatantSetup.ControllerPath, Content + "/BikeRiderEnemy.controller");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Content + "/BikeRiderEnemy.controller");
        ConfigureSeatedController(controller);
        var animation = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyAnimationProfile>(EnemyCombatantSetup.AnimationPath));
        animation.name = "Bike Rider Animation"; animation.controller = controller;
        AssetDatabase.CreateAsset(animation, Content + "/BikeRiderAnimation.asset");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EnemyCombatantSetup.PrefabPath));
        try
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "PF_EnemyBikeRider";
            EnemyCombatantSetup.Configure(root);
            var chassis = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AlienBikeSetup.Prefabs + "/PF_RideableAlienBike.prefab"), root.transform);
            PrefabUtility.UnpackPrefabInstance(chassis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            chassis.name = "Bike Chassis";
            chassis.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var collider in root.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            Copy(chassis.GetComponent<Rigidbody>(), root.AddComponent<Rigidbody>());
            Copy(chassis.GetComponent<BoxCollider>(), root.AddComponent<BoxCollider>());
            var bike = root.AddComponent<AlienBikeController>(); Copy(chassis.GetComponent<AlienBikeController>(), bike);
            bike.acceleration = playerBike.acceleration; bike.maxSpeed = playerBike.maxSpeed; bike.reverseSpeed = playerBike.reverseSpeed;
            bike.turboAcceleration = playerBike.turboAcceleration; bike.turboMaxSpeed = playerBike.turboMaxSpeed;
            bike.mountApproaches = Array.Empty<AlienBikeController.MountApproach>();
            var audio = root.AddComponent<AlienBikeAudio>(); Copy(chassis.GetComponent<AlienBikeAudio>(), audio); Set(bike, "audioPresentation", audio);
            // Enemy riders use the same physical chassis; cosmetic player lean remains player-owned.
            foreach (var component in chassis.GetComponents<MonoBehaviour>().Where(c => !(c is AlienBikeController) && !(c is RunWorldObject))) Object.DestroyImmediate(component);
            Object.DestroyImmediate(chassis.GetComponent<AlienBikeController>());
            Object.DestroyImmediate(chassis.GetComponent<RunWorldObject>());
            Object.DestroyImmediate(chassis.GetComponent<BoxCollider>()); Object.DestroyImmediate(chassis.GetComponent<Rigidbody>());
            root.GetComponent<NavMeshAgent>().enabled = false;
            root.GetComponent<EnemyBrain>().enabled = false;
            root.GetComponent<EnemyMotor>().enabled = false;
            root.GetComponent<EnemyMeleeAttack>().enabled = false;
            root.GetComponent<EnemyLocomotionAnimator>().enabled = false;
            root.GetComponent<EnemyWeaponAwareness>().enabled = false;
            root.GetComponent<EnemyWanderPlanner>().enabled = false;
            var death = new SerializedObject(root.GetComponent<EnemyDeathSequence>());
            var triggers = death.FindProperty("interruptibleTriggers"); triggers.arraySize = 1;
            triggers.GetArrayElementAtIndex(0).stringValue = "MeleeAttack"; death.ApplyModifiedPropertiesWithoutUndo();
            Set(root.GetComponent<EnemyEquipment>(), "profile", profile);
            Set(root.GetComponent<EnemyEquipment>(), "startingWeapon", Weapon("PlasmaPistolItem"));
            var presentation = root.GetComponent<EnemyCombatPresentation>();
            Set(presentation, "animationProfile", animation);
            // CharacterVisual can be nested below an offset presentation wrapper.
            presentation.Visual.transform.position = bike.seatPoint.position;
            presentation.Animator.runtimeAnimatorController = controller;
            presentation.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var perception = root.GetComponent<EnemyPerception>();
            Number(perception, "detectionRange", 45); Number(perception, "loseSightRange", 60);
            Number(perception, "eyeHeight", 1.45f);
            root.GetComponent<RunWorldObject>().ConfigureIdentity("");
            root.AddComponent<EnemyBikeDriver>();
            root.GetComponent<AimTarget>().CacheBodyData();
            return PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefab);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void BuildFinish(Transform parent, BikeRouteChaseDirector director, EnemyActor[] actors)
    {
        var finish = Object.FindObjectsByType<Transform>().Single(t => t.name == "FinishMarker");
        var defense = Child(parent, "Finish Defense"); defense.position = finish.position;
        var concrete = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Defense Concrete" };
        concrete.SetColor("_BaseColor", new Color(.25f, .31f, .34f)); AssetDatabase.CreateAsset(concrete, Content + "/DefenseConcrete.mat");
        var guns = new List<BikeRouteDefenseGun>();
        for (int side = -1; side <= 1; side += 2)
        {
            var tower = Child(defense, side < 0 ? "Left Tower" : "Right Tower");
            tower.localPosition = new Vector3(side * 8.5f, -1.6f, 0);
            Box(tower, "Concrete support", new Vector3(0, 3.5f, 0), new Vector3(3.2f, 7, 4), concrete);
            Box(tower, "Roof", new Vector3(0, 7, 0), new Vector3(4, .35f, 4.8f), concrete);
            for (int n = 0; n < 2; n++)
            {
                int index = guns.Count;
                var yaw = Child(tower, "Rifle " + (n + 1)); yaw.localPosition = new Vector3(0, 7.6f, n == 0 ? -1.25f : 1.25f);
                yaw.localRotation = Quaternion.Euler(0, 0, 0);
                Box(yaw, "Mount", new Vector3(0, -.3f, 0), new Vector3(.5f, .6f, .5f), concrete);
                var pitch = Child(yaw, "Pitch");
                var rifle = Weapon("PlasmaRifleItem");
                var model = (GameObject)PrefabUtility.InstantiatePrefab(rifle.equippedPrefab, pitch);
                var instance = model.GetComponent<WeaponInstance>();
                // Keep its existing authored muzzle/visual. Guns rotate their own pivots, not the rifle asset.
                model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
                var muzzle = instance.Muzzle;
                Quaternion align = Quaternion.Inverse(pitch.rotation) * muzzle.rotation;
                model.transform.localRotation = Quaternion.Inverse(align);
                var gun = yaw.gameObject.AddComponent<BikeRouteDefenseGun>();
                Set(gun, "structureRoot", tower); Set(gun, "yawPivot", yaw); Set(gun, "pitchPivot", pitch); Set(gun, "muzzle", muzzle);
                Set(gun, "rifle", rifle); ArrayReferences(gun, "targets", actors);
                Set(gun, "boltPrefab", Vfx<PlasmaBoltVFX>("PlasmaBolt")); Set(gun, "muzzlePrefab", Vfx<PlasmaMuzzleVFX>("PlasmaMuzzle"));
                Set(gun, "impactPrefab", Vfx<PlasmaImpactVFX>("PlasmaImpact"));
                Number(gun, "initialPhase", index * .137f); Integer(gun, "targetPreference", index);
                guns.Add(gun);
            }
        }
        var trigger = Child(defense, "Actual Finish Trigger").gameObject;
        trigger.transform.localPosition = Vector3.up * 4;
        var box = trigger.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(11, 14, 4);
        Set(trigger.AddComponent<BikeRouteFinishTrigger>(), "director", director);
        ArrayReferences(director, "guns", guns.ToArray());
    }

    private static void BuildPlasma(Transform parent, BikeRouteGuide guide)
    {
        var root = Child(parent, "Drive-over Plasma");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Items/Resources/PlasmaCapsule.prefab");
        for (int p = 0; p < guide.paths.Length; p++)
        {
            var sample = guide.At(p, guide.paths[p].Length * (p % 2 == 0 ? .38f : .64f));
            if (!Physics.Raycast(sample.position + Vector3.up * 6, Vector3.down, out var hit, 12, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No pickup support on " + guide.paths[p].label);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.name = "Plasma " + guide.paths[p].label;
            go.transform.position = hit.point + Vector3.up * .4f;
            (go.GetComponent<RunWorldObject>() ?? go.AddComponent<RunWorldObject>()).ConfigureIdentity("bike-route-plasma-" + p);
            Integer(go.GetComponent<PickupItem>(), "quantity", 3);
            foreach (var component in go.GetComponents<Component>()) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }

    private static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material;
    }
    private static void Copy(Object source, Object target) => EditorUtility.CopySerialized(source, target);
    private static WeaponItemData Weapon(string name) => AssetDatabase.LoadAssetAtPath<WeaponItemData>("Assets/Game/Items/Weapons/" + name + ".asset");
    private static T Vfx<T>(string name) where T : Component => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/VFX/" + name + "/" + name + ".prefab").GetComponent<T>();
    private static void Set(Object owner, string name, Object value) { var s = new SerializedObject(owner); s.FindProperty(name).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Number(Object owner, string name, float value) { var s = new SerializedObject(owner); s.FindProperty(name).floatValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void Integer(Object owner, string name, int value) { var s = new SerializedObject(owner); s.FindProperty(name).intValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void ArrayReferences<T>(Object owner, string name, T[] values) where T : Object
    {
        var s = new SerializedObject(owner); var p = s.FindProperty(name); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        s.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Slots(Object owner, string name, EnemyBikeDriver[] riders, bool[] front, float[] delays)
    {
        var s = new SerializedObject(owner); var p = s.FindProperty(name); p.arraySize = riders.Length;
        for (int i = 0; i < riders.Length; i++)
        {
            var slot = p.GetArrayElementAtIndex(i); slot.FindPropertyRelative("rider").objectReferenceValue = riders[i];
            slot.FindPropertyRelative("frontPass").boolValue = front[i]; slot.FindPropertyRelative("activationDelay").floatValue = delays[i];
        }
        s.ApplyModifiedPropertiesWithoutUndo();
    }
}
