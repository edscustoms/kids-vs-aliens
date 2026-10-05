using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit BikeRoute polish authoring. Does not rebuild the route or repair other scenes.</summary>
public static class BikeRoutePolishSetup
{
    public const string ImpactProfile = "Assets/Game/Scenes/BikeRoute/Chase/BikeImpact.asset";
    [MenuItem("Tools/Level Authoring/Update BikeRoute Breakable Signs")]
    public static void UpdateBreakableSigns()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != BikeRouteChaseSetup.ScenePath)
            throw new InvalidOperationException("Open BikeRoute outside Play Mode.");
        var signs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Single(t => t.name == "Road Signs");
        var director = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
        if (director == null || director.PlayerBike == null) throw new InvalidOperationException("Missing authored player bike.");
        foreach (Transform child in signs)
            if (child.GetComponent<BikeRouteBreakableSign>() == null &&
                (child.GetComponentsInChildren<MeshFilter>().Length == 0 ||
                 child.GetComponentsInChildren<MeshFilter>().Any(f => !AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith("Assets/Road sign - Big pack/"))))
                throw new InvalidOperationException("Unexpected non-sign under Road Signs: " + child.name);
        foreach (var child in signs.Cast<Transform>().ToArray())
        {
            var sign = child.GetComponent<BikeRouteBreakableSign>() ?? Wrap(child.gameObject, director.PlayerBike).GetComponent<BikeRouteBreakableSign>();
            Undo.RecordObject(sign.approach, "Update BikeRoute sign approach");
            Undo.RecordObject(sign, "Update BikeRoute sign threshold");
            // A sideways turbo hit can reach speculative solid contact before a
            // narrow trigger overlaps. Give every approach the same short lead-in.
            sign.approach.size = new Vector3(4, sign.approach.size.y, 4);
            sign.speedFraction = .6f;
            sign.playerBike = director.PlayerBike;
            EditorUtility.SetDirty(sign.approach); EditorUtility.SetDirty(sign);
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }
    [MenuItem("Tools/Level Authoring/Author BikeRoute Breakable Signs and Impact")]
    public static void Author()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != BikeRouteChaseSetup.ScenePath)
            throw new InvalidOperationException("Open BikeRoute outside Play Mode.");
        var director = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
        var feedback = Object.FindAnyObjectByType<CameraFeedbackController>();
        var signs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Single(t => t.name == "Road Signs");
        if (director == null || feedback == null) throw new InvalidOperationException("Missing chase/feedback owner.");
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("871857454b19c844a8ee81a3ea44f177"));
        if (template == null) throw new InvalidOperationException("Missing existing warning sign source.");
        var signMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Materials/RoadSignsURP.mat");
        if (signMaterial == null) throw new InvalidOperationException("Missing BikeRoute URP sign material.");
        var profile = AssetDatabase.LoadAssetAtPath<CameraFeedbackProfile>(ImpactProfile);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<CameraFeedbackProfile>();
            profile.duration = .2f; profile.rotation = new Vector3(-1.3f, .2f, .7f);
            profile.position = new Vector3(0, -.035f, -.055f); profile.directionalInfluence = .6f;
            AssetDatabase.CreateAsset(profile, ImpactProfile);
        }
        var impact = director.PlayerBike.GetComponent<BikeRouteImpactFeedback>()
            ?? director.PlayerBike.gameObject.AddComponent<BikeRouteImpactFeedback>();
        impact.profile = profile;
        feedback.ConfigureMountedPlayer(director.Player);
        EditorUtility.SetDirty(feedback); EditorUtility.SetDirty(impact);
        // Existing vendor prefab instances become isolated scene visuals; vendor assets stay untouched.
        foreach (var child in signs.Cast<Transform>().ToArray())
            if (child.GetComponent<BikeRouteBreakableSign>() == null) Wrap(child.gameObject, director.PlayerBike);
        for (int path = 0; path < director.Guide.paths.Length; path++)
        {
            string name = "Shoulder Warning " + (path + 1).ToString("00");
            if (signs.Find(name) != null) continue;
            var sample = director.Guide.At(path, director.Guide.paths[path].Length * .38f);
            Vector3 position = sample.position + sample.Right * ((path % 2 == 0 ? 1 : -1) * (sample.halfWidth + .7f));
            if (Physics.Raycast(position + Vector3.up * 20, Vector3.down, out var ground, 45, ~0, QueryTriggerInteraction.Ignore))
                position.y = ground.point.y;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(template, scene);
            visual.transform.SetParent(signs, false);
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = signMaterial;
            visual.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-Vector3.ProjectOnPlane(sample.forward, Vector3.up)));
            Wrap(visual, director.PlayerBike).name = name;
        }
        var serialized = new SerializedObject(director);
        serialized.FindProperty("contactHandoffDelay").floatValue = .7f; serialized.ApplyModifiedPropertiesWithoutUndo();
        var prefab = PrefabUtility.LoadPrefabContents(BikeRouteChaseSetup.EnemyPrefab);
        try
        {
            var driver = new SerializedObject(prefab.GetComponent<EnemyBikeDriver>());
            driver.FindProperty("attackRecoverySeconds").floatValue = 1.8f;
            driver.FindProperty("leadHoldSeconds").floatValue = 6;
            driver.FindProperty("leadDistance").floatValue = 16;
            driver.FindProperty("leadRecoverySeconds").floatValue = 4;
            driver.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, BikeRouteChaseSetup.EnemyPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("BikeRoute polish authored: " + signs.childCount + " signs; impact feedback; chase timings. Player tuning preserved.");
    }
    private static GameObject Wrap(GameObject visual, AlienBikeController playerBike)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(visual))
            PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var root = new GameObject(visual.name + " Breakable");
        root.transform.SetParent(visual.transform.parent, false);
        root.transform.SetPositionAndRotation(visual.transform.position, visual.transform.rotation);
        visual.transform.SetParent(root.transform, true);
        foreach (var child in visual.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
        foreach (var collider in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        Bounds bounds = new Bounds(); bool first = true;
        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
        {
            var b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var sign = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                var point = root.transform.InverseTransformPoint(filter.transform.TransformPoint(b.center + Vector3.Scale(b.extents, sign)));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; } else bounds.Encapsulate(point);
            }
        }
        // One slim physical post/body, plus an early trigger so high-speed impact cannot stop at a kinematic wall.
        var solid = visual.AddComponent<BoxCollider>();
        solid.center = visual.transform.InverseTransformPoint(root.transform.TransformPoint(bounds.center));
        solid.size = new Vector3(.22f, bounds.size.y, .22f);
        solid.size = Vector3.Scale(solid.size, new Vector3(1 / visual.transform.localScale.x, 1 / visual.transform.localScale.y, 1 / visual.transform.localScale.z));
        var body = visual.AddComponent<Rigidbody>(); body.mass = 8; body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var trigger = root.AddComponent<BoxCollider>(); trigger.isTrigger = true;
        trigger.center = bounds.center; trigger.size = new Vector3(4, bounds.size.y + .2f, 4);
        root.AddComponent<RunWorldObject>().ConfigureIdentity(Guid.NewGuid().ToString("N"));
        var signComponent = root.AddComponent<BikeRouteBreakableSign>(); signComponent.signBody = body; signComponent.approach = trigger;
        signComponent.playerBike = playerBike;
        return root;
    }
    public static void Batch()
    {
        EditorSceneManager.OpenScene(BikeRouteChaseSetup.ScenePath); Author();
    }
}
