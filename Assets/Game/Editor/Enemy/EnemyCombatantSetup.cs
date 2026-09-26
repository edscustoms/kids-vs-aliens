using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Idempotent extension of the existing enemy prefab, never assigns encounter loadouts.</summary>
public static class EnemyCombatantSetup
{
    public const string PrefabPath = "Assets/Game/Prefabs/Enemies/PF_Enemy_Melee_POC_V1.prefab";
    private const string PlasmaPickupPath =
        "Assets/Game/Prefabs/Items/Resources/PlasmaCapsule.prefab";
    public const string ProfilePath = "Assets/Game/Data/Enemies/AlienCombatV1.asset";
    public const string AnimationPath = "Assets/Game/Data/Enemies/AlienAnimationV1.asset";
    public const string ControllerPath =
        "Assets/Game/Animations/Enemy/HumanoidMeleeEnemy.controller";

    [MenuItem("Tools/Helpers/Repair Selected Alien Combatant")]
    public static void RepairSelected()
    {
        if (Selection.activeGameObject == null)
            throw new InvalidOperationException("Select an enemy root.");
        EnsureAssets();
        string path = AssetDatabase.GetAssetPath(Selection.activeGameObject);
        if (
            !string.IsNullOrEmpty(path)
            && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
        )
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        else
        {
            Configure(Selection.activeGameObject);
            EditorSceneManager.MarkSceneDirty(Selection.activeGameObject.scene);
        }
        AssetDatabase.SaveAssets();
    }

    public static void Apply()
    {
        EnsureAssets();
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Configure(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs/AlienCombat");
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        var lines = UnityEngine
            .Object.FindObjectsByType<EnemyActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            )
            .Select(x =>
                x.name
                + " | "
                + x.transform.position.ToString("F2")
                + " | "
                + x.GetComponent<RunWorldObject>()?.Id
            )
            .ToList();
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.IndexOf("gate", StringComparison.OrdinalIgnoreCase) >= 0)
                lines.Add("GATE " + t.name + " | " + t.position.ToString("F2"));
        File.WriteAllLines("Logs/AlienCombat/scene-positions.txt", lines);
        Debug.Log("ALIEN AUTHORING COMPLETE (prefab unarmed; scene not saved)");
    }

    public static void ConfigureScene(Scene scene)
    {
        EnsureAssets();
        foreach (
            var actor in UnityEngine.Object.FindObjectsByType<EnemyActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            )
        )
            if (actor.gameObject.scene == scene)
                Configure(actor.gameObject);
    }

    public static void AuthorGateLoadouts()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        var actors = UnityEngine.Object.FindObjectsByType<EnemyActor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        // Explicit authored exceptions requested for the two aliens immediately below the north-east locked gate.
        string[] names = { "PF_Enemy_Melee_POC_V1 (7)", "PF_Enemy_Melee_POC_V1 (8)" };
        for (int i = 0; i < names.Length; i++)
        {
            var actor = actors.Single(x => x.name == names[i]);
            if (actor.transform.position.x < 25 || actor.transform.position.z < 35)
                throw new InvalidOperationException(
                    "Gate encounter moved; review before authoring."
                );
            var equipment = actor.GetComponent<EnemyEquipment>();
            var so = new SerializedObject(equipment);
            so.FindProperty("startingWeapon").objectReferenceValue = Weapon(
                i == 0 ? "PlasmaPistolItem" : "PlasmaRifleItem"
            );
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(equipment);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            "Authored ONLY gate aliens (7) pistol and (8) rifle; all other loadouts preserved."
        );
    }

    public static void Configure(GameObject root)
    {
        if (root.GetComponent<EnemyActor>() == null)
            throw new InvalidOperationException("An existing EnemyActor root is required.");
        var presentation = Get<EnemyCombatPresentation>(root);
        var visual = presentation.Visual;
        if (visual == null)
        {
            visual = root.GetComponentInChildren<CharacterVisual>(true);
            var animator =
                visual != null ? visual.Animator : root.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
                throw new InvalidOperationException(
                    root.name + ": a valid Humanoid Animator is required."
                );
            if (visual == null)
                visual = Get<CharacterVisual>(animator.gameObject);
            Missing(visual, "animator", animator);
            if (!visual.HasWeaponSocket)
            {
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand == null)
                    throw new InvalidOperationException(root.name + ": RightHand bone missing.");
                var socket = hand.Find("WeaponSocket");
                if (socket == null)
                {
                    socket = new GameObject("WeaponSocket").transform;
                    socket.SetParent(hand, false);
                }
                Missing(visual, "weaponSocket", socket);
            }
            Missing(presentation, "visual", visual);
        }
        Missing(
            presentation,
            "animationProfile",
            AssetDatabase.LoadAssetAtPath<EnemyAnimationProfile>(AnimationPath)
        );
        var equipment = Get<EnemyEquipment>(root);
        EnsurePlasmaLoot(root);
        Missing(
            equipment,
            "profile",
            AssetDatabase.LoadAssetAtPath<EnemyCombatProfile>(ProfilePath)
        );
        var ranged = Get<EnemyRangedAttack>(root);
        Missing(
            ranged,
            "boltPrefab",
            AssetDatabase
                .LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/VFX/PlasmaBolt/PlasmaBolt.prefab")
                .GetComponent<PlasmaBoltVFX>()
        );
        Missing(
            ranged,
            "muzzlePrefab",
            AssetDatabase
                .LoadAssetAtPath<GameObject>(
                    "Assets/Game/Prefabs/VFX/PlasmaMuzzle/PlasmaMuzzle.prefab"
                )
                .GetComponent<PlasmaMuzzleVFX>()
        );
        Missing(
            ranged,
            "impactPrefab",
            AssetDatabase
                .LoadAssetAtPath<GameObject>(
                    "Assets/Game/Prefabs/VFX/PlasmaImpact/PlasmaImpact.prefab"
                )
                .GetComponent<PlasmaImpactVFX>()
        );
        Get<EnemyWeaponAwareness>(root); // Pickup and starting weapon defaults deliberately remain off/None.
        Get<CharacterAnimationEventRelay>(visual.Animator.gameObject);
        foreach (var component in root.GetComponents<MonoBehaviour>())
        {
            if (
                !(
                    component is EnemyMeleeAttack
                    || component is EnemyLocomotionAnimator
                    || component is EnemyHitReaction
                    || component is EnemyDeathSequence
                )
            )
                continue;
            var so = new SerializedObject(component);
            var property = so.FindProperty("animator");
            if (property != null && property.objectReferenceValue != visual.Animator)
            {
                property.objectReferenceValue = visual.Animator;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        EditorUtility.SetDirty(root);
    }

    public static void EnsureAssets()
    {
        Directory.CreateDirectory("Assets/Game/Data/Enemies");
        var profile = AssetDatabase.LoadAssetAtPath<EnemyCombatProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<EnemyCombatProfile>();
            profile.allowedWeapons = new[]
            {
                Weapon("PlasmaPistolItem"),
                Weapon("PlasmaRifleItem"),
            };
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var source = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Game/Animations/Player/HumanoidShooter.controller"
        );
        var machine = controller.layers[0].stateMachine;
        foreach (string name in new[] { "PistolLocomotion", "RifleLocomotion" })
        {
            if (machine.states.Any(x => x.state.name == name))
                continue;
            var original = source
                .layers[0]
                .stateMachine.states.First(x => x.state.name == name)
                .state;
            var state = machine.AddState(name);
            state.motion = CopyMotion(original.motion, controller);
            state.writeDefaultValues = true;
        }
        var locomotion = machine
            .states.Where(x => x.state.name.EndsWith("Locomotion", StringComparison.Ordinal))
            .Select(x => x.state)
            .ToArray();
        foreach (var from in locomotion)
        foreach (var to in locomotion)
        {
            if (from == to || from.transitions.Any(x => x.destinationState == to))
                continue;
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = .18f;
            transition.AddCondition(
                AnimatorConditionMode.Equals,
                to.name.StartsWith("Pistol") ? 1
                    : to.name.StartsWith("Rifle") ? 2
                    : 0,
                "WeaponStyle"
            );
        }
        var animation = AssetDatabase.LoadAssetAtPath<EnemyAnimationProfile>(AnimationPath);
        if (animation == null)
        {
            animation = ScriptableObject.CreateInstance<EnemyAnimationProfile>();
            animation.controller = controller;
            AssetDatabase.CreateAsset(animation, AnimationPath);
        }
    }

    private static Motion CopyMotion(Motion motion, AnimatorController controller)
    {
        if (!(motion is BlendTree tree))
            return motion;
        var copy = UnityEngine.Object.Instantiate(tree);
        copy.name = tree.name;
        AssetDatabase.AddObjectToAsset(copy, controller);
        var children = copy.children;
        for (int i = 0; i < children.Length; i++)
            children[i].motion = CopyMotion(children[i].motion, controller);
        copy.children = children;
        return copy;
    }

    public static WeaponItemData Weapon(string name) =>
        AssetDatabase.LoadAssetAtPath<WeaponItemData>(
            "Assets/Game/Items/Weapons/" + name + ".asset"
        );

    private static T Get<T>(GameObject root)
        where T : Component
    {
        var value = root.GetComponent<T>();
        return value != null ? value : root.AddComponent<T>();
    }

    private static void Missing(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        var field = so.FindProperty(name);
        if (field.objectReferenceValue != null)
            return;
        field.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsurePlasmaLoot(GameObject root)
    {
        var loot = Get<EnemyPlasmaLoot>(root);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlasmaPickupPath);
        if (prefab == null)
            throw new InvalidOperationException(
                $"Missing Plasma Capsule prefab at '{PlasmaPickupPath}'."
            );

        var pickup = prefab.GetComponent<PickupItem>();
        if (pickup == null)
            throw new InvalidOperationException(
                $"Plasma Capsule prefab at '{PlasmaPickupPath}' is missing PickupItem."
            );

        Missing(loot, "plasmaPickup", pickup);
    }
}
