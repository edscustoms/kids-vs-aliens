using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public sealed class EnemyPocSetupWindow : EditorWindow
{
    [SerializeField]
    private GameObject target;

    [Header("Body / navigation")]
    [SerializeField]
    private float bodyHeight = 1.8f;

    [SerializeField]
    private float bodyRadius = 0.28f;

    [SerializeField]
    private float bodyCenterY = 0.9f;

    [SerializeField]
    private float stoppingDistance = 0.12f;

    [Header("POC behavior")]
    [SerializeField]
    private bool addOptionalLocomotionAnimator = true;

    [MenuItem(
        "Tools/Kids VS Aliens/Helpers/Enemy POC Setup")]
    public static void Open()
    {
        GetWindow<EnemyPocSetupWindow>(
            false,
            "Enemy POC Setup",
            true);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Enemy POC Setup V1.2",
            EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Normalizes the selected enemy prefab/instance and applies the current AI POC settings.\n\n" +
            "It fixes the current capsule-root setup, solid collision, NavMeshAgent sizing, AI components, references, perception, wandering and investigation defaults. Prefab roots stay at local zero.",
            MessageType.Info);

        target =
            (GameObject)EditorGUILayout.ObjectField(
                "Enemy Root",
                target,
                typeof(GameObject),
                true);

        if (GUILayout.Button("Use Selected"))
            target = Selection.activeGameObject;

        EditorGUILayout.Space(6);

        bodyHeight =
            Mathf.Max(
                0.5f,
                EditorGUILayout.FloatField(
                    "Body Height",
                    bodyHeight));

        bodyRadius =
            Mathf.Max(
                0.05f,
                EditorGUILayout.FloatField(
                    "Body Radius",
                    bodyRadius));

        bodyCenterY =
            EditorGUILayout.FloatField(
                "Body Center Y",
                bodyCenterY);

        stoppingDistance =
            Mathf.Max(
                0f,
                EditorGUILayout.FloatField(
                    "Stopping Distance",
                    stoppingDistance));

        addOptionalLocomotionAnimator =
            EditorGUILayout.Toggle(
                "Add Locomotion Animator",
                addOptionalLocomotionAnimator);

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(
                   target == null))
        {
            if (GUILayout.Button(
                    "Normalize + Configure Enemy",
                    GUILayout.Height(34)))
            {
                ConfigureTarget();
            }
        }

        if (GUILayout.Button(
                "Snap Selected Scene Enemies To Ground",
                GUILayout.Height(28)))
        {
            SnapSelectedSceneEnemiesToGround();
        }

        EditorGUILayout.Space(6);

        EditorGUILayout.HelpBox(
            "IMPORTANT after this:\n" +
            "Navigation > Agents > Humanoid should also use Radius ~0.28 and Height ~1.8, then rebake the NavMesh. " +
            "Prefab settings alone cannot change the already-baked agent radius.",
            MessageType.Warning);
    }

    private void ConfigureTarget()
    {
        string assetPath =
            AssetDatabase.GetAssetPath(target);

        if (!string.IsNullOrWhiteSpace(assetPath) &&
            assetPath.EndsWith(
                ".prefab",
                StringComparison.OrdinalIgnoreCase))
        {
            GameObject prefabRoot =
                PrefabUtility.LoadPrefabContents(
                    assetPath);

            try
            {
                ConfigureRoot(
                    prefabRoot,
                    false);

                PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot,
                    assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(
                    prefabRoot);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            GameObject saved =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    assetPath);

            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);

            Debug.Log(
                $"Enemy POC setup updated prefab: {assetPath}");

            return;
        }

        ConfigureRoot(
            target,
            true);

        EditorUtility.SetDirty(target);

        Debug.Log(
            $"Enemy POC setup configured scene object: {target.name}. " +
            "If this is a prefab instance, Apply All when you're happy.");
    }

    private void ConfigureRoot(
        GameObject root,
        bool useUndo)
    {
        if (root == null)
            return;

        RemoveLegacyEnemyMovement(
            root,
            useUndo);

        NormalizeCurrentCapsuleVisual(
            root,
            useUndo);

        CapsuleCollider body =
            GetOrAdd<CapsuleCollider>(
                root,
                useUndo);

        body.isTrigger = false;
        body.direction = 1;
        body.center =
            new Vector3(
                0f,
                bodyCenterY,
                0f);

        body.height = bodyHeight;
        body.radius = bodyRadius;

        NavMeshAgent agent =
            GetOrAdd<NavMeshAgent>(
                root,
                useUndo);

        agent.baseOffset = 0f;
        agent.height = bodyHeight;
        agent.radius = bodyRadius;
        agent.stoppingDistance =
            stoppingDistance;

        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.obstacleAvoidanceType =
            ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        EnemyHealth health =
            GetOrAdd<EnemyHealth>(
                root,
                useUndo);

        AimTarget aimTarget =
            GetOrAdd<AimTarget>(
                root,
                useUndo);

        EnemyActor actor =
            GetOrAdd<EnemyActor>(
                root,
                useUndo);

        EnemyMotor motor =
            GetOrAdd<EnemyMotor>(
                root,
                useUndo);

        EnemyPerception perception =
            GetOrAdd<EnemyPerception>(
                root,
                useUndo);

        EnemyApproachPlanner approach =
            GetOrAdd<EnemyApproachPlanner>(
                root,
                useUndo);

        EnemyWanderPlanner wander =
            GetOrAdd<EnemyWanderPlanner>(
                root,
                useUndo);

        EnemyInvestigationPlanner investigation =
            GetOrAdd<EnemyInvestigationPlanner>(
                root,
                useUndo);

        EnemyMeleeAttack melee =
            GetOrAdd<EnemyMeleeAttack>(
                root,
                useUndo);

        EnemyBrain brain =
            GetOrAdd<EnemyBrain>(
                root,
                useUndo);

        Animator animator =
            root.GetComponentInChildren<Animator>(
                true);

        EnemyLocomotionAnimator locomotion =
            root.GetComponent<
                EnemyLocomotionAnimator>();

        if (addOptionalLocomotionAnimator &&
            animator != null)
        {
            if (locomotion == null)
            {
                locomotion =
                    GetOrAdd<
                        EnemyLocomotionAnimator>(
                        root,
                        useUndo);
            }
        }

        ConfigureActor(
            actor,
            health,
            motor,
            perception);

        ConfigureBrain(
            brain,
            actor,
            motor,
            perception,
            approach,
            wander,
            investigation,
            melee);

        ConfigurePerception(
            perception);

        ConfigureApproach(
            approach);

        ConfigureWander(
            wander);

        ConfigureInvestigation(
            investigation);

        ConfigureMelee(
            melee);

        if (locomotion != null)
        {
            ConfigureLocomotion(
                locomotion,
                motor,
                animator);
        }

        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(agent);
        EditorUtility.SetDirty(health);
        EditorUtility.SetDirty(aimTarget);
        EditorUtility.SetDirty(actor);
        EditorUtility.SetDirty(motor);
        EditorUtility.SetDirty(perception);
        EditorUtility.SetDirty(approach);
        EditorUtility.SetDirty(wander);
        EditorUtility.SetDirty(investigation);
        EditorUtility.SetDirty(melee);
        EditorUtility.SetDirty(brain);

        if (locomotion != null)
            EditorUtility.SetDirty(locomotion);
    }

    private static T GetOrAdd<T>(
        GameObject root,
        bool useUndo)
        where T : Component
    {
        T component =
            root.GetComponent<T>();

        if (component != null)
            return component;

        return useUndo
            ? Undo.AddComponent<T>(root)
            : root.AddComponent<T>();
    }

    /// <summary>
    /// The current POC enemy uses the Capsule mesh directly on the enemy root.
    /// That makes transform.position the middle of the body instead of the feet.
    ///
    /// This converts ONLY that current root MeshFilter/MeshRenderer into a
    /// child named Visual and moves the root to the mesh bottom while keeping
    /// the visual in the same world-space position.
    ///
    /// Once a proper humanoid FBX already lives under Visual, this method does
    /// nothing.
    /// </summary>
    private static void NormalizeCurrentCapsuleVisual(
        GameObject root,
        bool useUndo)
    {
        // Prefab assets must keep their root transform at local zero.
        // Moving the prefab root itself caused existing scene instances
        // to keep their old Y override while the new Visual child gained
        // an offset, making the capsule appear to float.
        if (!useUndo)
        {
            root.transform.localPosition =
                Vector3.zero;
        }

        Transform existingVisual =
            root.transform.Find("Visual");

        if (existingVisual != null)
        {
            return;
        }

        MeshFilter rootFilter =
            root.GetComponent<MeshFilter>();

        MeshRenderer rootRenderer =
            root.GetComponent<MeshRenderer>();

        if (rootFilter == null ||
            rootRenderer == null ||
            rootFilter.sharedMesh == null)
        {
            return;
        }

        GameObject visualObject =
            new GameObject("Visual");

        if (useUndo)
        {
            Undo.RegisterCreatedObjectUndo(
                visualObject,
                "Create Enemy Visual");
        }

        Transform visual =
            visualObject.transform;

        visual.SetParent(
            root.transform,
            false);

        MeshFilter childFilter =
            visualObject.AddComponent<MeshFilter>();

        childFilter.sharedMesh =
            rootFilter.sharedMesh;

        MeshRenderer childRenderer =
            visualObject.AddComponent<MeshRenderer>();

        childRenderer.sharedMaterials =
            rootRenderer.sharedMaterials;

        childRenderer.shadowCastingMode =
            rootRenderer.shadowCastingMode;

        childRenderer.receiveShadows =
            rootRenderer.receiveShadows;

        childRenderer.lightProbeUsage =
            rootRenderer.lightProbeUsage;

        childRenderer.reflectionProbeUsage =
            rootRenderer.reflectionProbeUsage;

        childRenderer.motionVectorGenerationMode =
            rootRenderer.motionVectorGenerationMode;

        childRenderer.allowOcclusionWhenDynamic =
            rootRenderer.allowOcclusionWhenDynamic;

        childRenderer.renderingLayerMask =
            rootRenderer.renderingLayerMask;

        // Put the visible mesh above the feet/root.
        // For Unity's primitive capsule, mesh.bounds.min.y is about -1,
        // so this becomes a +1m local Y offset.
        Bounds meshBounds =
            rootFilter.sharedMesh.bounds;

        visual.localPosition =
            new Vector3(
                -meshBounds.center.x,
                -meshBounds.min.y,
                -meshBounds.center.z);

        visual.localRotation =
            Quaternion.identity;

        visual.localScale =
            Vector3.one;

        if (useUndo)
        {
            Undo.DestroyObjectImmediate(
                rootRenderer);

            Undo.DestroyObjectImmediate(
                rootFilter);
        }
        else
        {
            DestroyImmediate(
                rootRenderer);

            DestroyImmediate(
                rootFilter);
        }
    }


    private static void RemoveLegacyEnemyMovement(
        GameObject root,
        bool useUndo)
    {
        MonoBehaviour[] behaviours =
            root.GetComponents<
                MonoBehaviour>();

        foreach (MonoBehaviour behaviour
                 in behaviours)
        {
            if (behaviour == null)
                continue;

            if (!string.Equals(
                    behaviour.GetType().Name,
                    "EnemyMovement",
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (useUndo)
            {
                Undo.DestroyObjectImmediate(
                    behaviour);
            }
            else
            {
                DestroyImmediate(
                    behaviour);
            }
        }
    }

    private static void ConfigureActor(
        EnemyActor actor,
        EnemyHealth health,
        EnemyMotor motor,
        EnemyPerception perception)
    {
        SerializedObject so =
            new SerializedObject(actor);

        SetObjectReference(
            so,
            "health",
            health);

        SetObjectReference(
            so,
            "motor",
            motor);

        SetObjectReference(
            so,
            "perception",
            perception);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureBrain(
        EnemyBrain brain,
        EnemyActor actor,
        EnemyMotor motor,
        EnemyPerception perception,
        EnemyApproachPlanner approach,
        EnemyWanderPlanner wander,
        EnemyInvestigationPlanner investigation,
        EnemyMeleeAttack melee)
    {
        SerializedObject so =
            new SerializedObject(brain);

        SetObjectReference(
            so,
            "actor",
            actor);

        SetObjectReference(
            so,
            "motor",
            motor);

        SetObjectReference(
            so,
            "perception",
            perception);

        SetObjectReference(
            so,
            "approachPlanner",
            approach);

        SetObjectReference(
            so,
            "wanderPlanner",
            wander);

        SetObjectReference(
            so,
            "investigationPlanner",
            investigation);

        SetObjectReference(
            so,
            "meleeAttack",
            melee);

        SetVector2(
            so,
            "chaseRepathIntervalRange",
            new Vector2(
                0.15f,
                0.35f));

        SetVector2(
            so,
            "idleWaitRange",
            new Vector2(
                1.8f,
                4.5f));

        SetFloat(
            so,
            "wanderChance",
            0.70f);

        SetVector2(
            so,
            "investigatePointWaitRange",
            new Vector2(
                0.6f,
                1.4f));

        SetFloat(
            so,
            "investigateTotalTimeout",
            10f);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurePerception(
        EnemyPerception perception)
    {
        SerializedObject so =
            new SerializedObject(perception);

        SetFloat(
            so,
            "detectionRange",
            8f);

        SetFloat(
            so,
            "fieldOfViewDegrees",
            160f);

        SetFloat(
            so,
            "closeAwarenessRange",
            1.8f);

        SetFloat(
            so,
            "loseSightRange",
            13f);

        SetFloat(
            so,
            "eyeHeight",
            1.35f);

        SetVector2(
            so,
            "senseIntervalRange",
            new Vector2(
                0.12f,
                0.25f));

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureApproach(
        EnemyApproachPlanner approach)
    {
        SerializedObject so =
            new SerializedObject(approach);

        SetFloat(
            so,
            "slotActivationDistance",
            2.4f);

        SetInt(
            so,
            "slotsPerRing",
            12);

        SetFloat(
            so,
            "baseRadius",
            1.05f);

        SetFloat(
            so,
            "extraRingSpacing",
            0.65f);

        SetFloat(
            so,
            "angleJitterDegrees",
            10f);

        SetFloat(
            so,
            "radiusJitter",
            0.10f);

        SetFloat(
            so,
            "sampleRadius",
            0.8f);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureWander(
        EnemyWanderPlanner wander)
    {
        SerializedObject so =
            new SerializedObject(wander);

        SetInt(
            so,
            "desiredPointCount",
            8);

        SetFloat(
            so,
            "wanderRadius",
            4f);

        SetFloat(
            so,
            "minimumDistanceFromSpawn",
            1.4f);

        SetFloat(
            so,
            "minimumTravelDistance",
            1.2f);

        SetFloat(
            so,
            "minimumPointSpacing",
            1f);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureInvestigation(
        EnemyInvestigationPlanner investigation)
    {
        SerializedObject so =
            new SerializedObject(investigation);

        SetInt(
            so,
            "desiredSearchPoints",
            3);

        SetFloat(
            so,
            "minimumSearchRadius",
            0.8f);

        SetFloat(
            so,
            "maximumSearchRadius",
            2.6f);

        SetFloat(
            so,
            "minimumPointSpacing",
            0.75f);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureMelee(
        EnemyMeleeAttack melee)
    {
        SerializedObject so =
            new SerializedObject(melee);

        SetFloat(
            so,
            "attackRange",
            1.25f);

        SetFloat(
            so,
            "damage",
            10f);

        SetVector2(
            so,
            "cooldownRange",
            new Vector2(
                0.9f,
                1.2f));

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureLocomotion(
        EnemyLocomotionAnimator locomotion,
        EnemyMotor motor,
        Animator animator)
    {
        SerializedObject so =
            new SerializedObject(locomotion);

        SetObjectReference(
            so,
            "motor",
            motor);

        SetObjectReference(
            so,
            "animator",
            animator);

        SetInt(
            so,
            "weaponStyle",
            0);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SnapSelectedSceneEnemiesToGround()
    {
        GameObject[] selected =
            Selection.gameObjects;

        if (selected == null ||
            selected.Length == 0)
        {
            Debug.LogWarning(
                "Enemy POC Setup: select one or more enemy scene instances first.");

            return;
        }

        int repaired = 0;

        foreach (GameObject root in selected)
        {
            if (root == null ||
                EditorUtility.IsPersistent(root))
            {
                continue;
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(
                    true);

            if (renderers.Length == 0)
                continue;

            Bounds visualBounds =
                renderers[0].bounds;

            for (int i = 1;
                 i < renderers.Length;
                 i++)
            {
                visualBounds.Encapsulate(
                    renderers[i].bounds);
            }

            Vector3 rayOrigin =
                visualBounds.center +
                Vector3.up *
                Mathf.Max(
                    2f,
                    visualBounds.extents.y + 1f);

            RaycastHit[] hits =
                Physics.RaycastAll(
                    rayOrigin,
                    Vector3.down,
                    100f,
                    ~0,
                    QueryTriggerInteraction.Ignore);

            float bestDistance =
                float.PositiveInfinity;

            float groundY =
                float.NaN;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                Transform hitTransform =
                    hit.collider.transform;

                if (hitTransform == root.transform ||
                    hitTransform.IsChildOf(
                        root.transform))
                {
                    continue;
                }

                if (hit.distance >=
                    bestDistance)
                {
                    continue;
                }

                bestDistance =
                    hit.distance;

                groundY =
                    hit.point.y;
            }

            if (float.IsNaN(groundY))
            {
                Debug.LogWarning(
                    $"Enemy POC Setup: no ground collider found below {root.name}.",
                    root);

                continue;
            }

            Undo.RecordObject(
                root.transform,
                "Snap Enemy To Ground");

            Vector3 position =
                root.transform.position;

            position.y +=
                groundY -
                visualBounds.min.y;

            root.transform.position =
                position;

            EditorUtility.SetDirty(
                root.transform);

            repaired++;
        }

        Debug.Log(
            $"Enemy POC Setup: snapped {repaired} selected enemy scene instance(s) to ground.");
    }

    private static void SetObjectReference(
        SerializedObject so,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property =
            so.FindProperty(propertyName);

        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetFloat(
        SerializedObject so,
        string propertyName,
        float value)
    {
        SerializedProperty property =
            so.FindProperty(propertyName);

        if (property != null)
            property.floatValue = value;
    }

    private static void SetInt(
        SerializedObject so,
        string propertyName,
        int value)
    {
        SerializedProperty property =
            so.FindProperty(propertyName);

        if (property != null)
            property.intValue = value;
    }

    private static void SetVector2(
        SerializedObject so,
        string propertyName,
        Vector2 value)
    {
        SerializedProperty property =
            so.FindProperty(propertyName);

        if (property != null)
            property.vector2Value = value;
    }
}
