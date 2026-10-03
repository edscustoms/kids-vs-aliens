using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Explicit vehicle content authoring. Routine player repair only fills missing wiring.</summary>
public static class AlienBikeSetup
{
    public const string Art = "Assets/Game/Art/Vehicles/Alien";
    public const string Prefabs = "Assets/Game/Prefabs/Vehicles";
    public const string PosePath = Art + "/BikeRider.controller";
    private const string Audio = "Assets/Game/Audio/Events/Vehicles";
    public static void ConfigureRideable(AlienBikeController bike)
    {
        if (bike.GetComponent<AlienBikeImpact>() == null)
            Undo.AddComponent<AlienBikeImpact>(bike.gameObject);
    }
    public static void ConfigurePlayer(PlayerCharacter player)
    {
        var rider = player.GetComponent<PlayerBikeRider>() ?? Undo.AddComponent<PlayerBikeRider>(player.gameObject);
        var so = new SerializedObject(rider);
        var pose = so.FindProperty("riderAnimation");
        if (pose.objectReferenceValue == null)
        {
            pose.objectReferenceValue = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PosePath);
            so.ApplyModifiedProperties();
        }
    }
    private static void Reference(Object owner, string key, Object value)
    {
        var s = new SerializedObject(owner);
        s.FindProperty(key).objectReferenceValue = value;
        s.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void References(Object owner, string key, Object[] values)
    {
        var s = new SerializedObject(owner);
        var p = s.FindProperty(key);
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        s.ApplyModifiedPropertiesWithoutUndo();
    }
    private static Transform Marker(Transform parent, string name, Vector3 position)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = position;
        return t;
    }
    private static Material Material(string name, Color color, float metal = 0, float glow = 0)
    {
        string path = Art + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null)
            return m;
        m = new Material(Shader.Find("Vehicles/Plasma Bike Surface")) { name = name };
        m.SetColor("_BaseColor", color);
        if (glow > 0)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * glow);
        }
        if (name == "Bike_Graphite")
        {
            m.SetColor("_BaseColor", new Color(.1f, .12f, .19f));
            m.SetColor("_EmissionColor", new Color(.04f, .06f, .1f));
        }
        if (name == "Bike_Wake")
        {
            m.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            m.SetColor("_BaseColor", new Color(.1f, 2, 3));
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 2);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
    [MenuItem("Tools/Vehicles/Create Missing Alien Bike Assets")]
    public static void EnsureAssets()
    {
        Directory.CreateDirectory(Art);
        Directory.CreateDirectory(Prefabs);
        Directory.CreateDirectory(Audio);
        AssetDatabase.Refresh();
        EnsurePose();
        EnsureSounds();
        string visualPath = Prefabs + "/PF_AlienBikeVisual.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(visualPath) == null)
        {
            var root = new GameObject("PF_AlienBikeVisual");
            try
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Alien_PlasmaBike.fbx");
                if (model == null)
                    throw new InvalidOperationException("Export Alien_PlasmaBike.fbx first.");
                var mesh = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                mesh.transform.localRotation = Quaternion.Euler(0, 180, 0) * mesh.transform.localRotation;
                var renderer = mesh.GetComponentInChildren<MeshRenderer>();
                var names = renderer.sharedMaterials.Select(m => m.name).ToArray();
                var materials = new Material[names.Length];
                int plasma = 0;
                for (int i = 0; i < names.Length; i++)
                {
                    bool energy = names[i].Contains("Plasma");
                    if (energy)
                        plasma = i;
                    materials[i] = Material(names[i], energy ? new Color(0, .7f, 1) : names[i].Contains("Alloy") ? new Color(.12f, .16f, .23f)
                        : names[i].Contains("Saddle") ? new Color(.015f, .02f, .03f) : new Color(.035f, .047f, .08f), energy ? .3f : .65f, energy ? 6 : 0);
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var visual = root.AddComponent<AlienBikeVisual>();
                Reference(visual, "hull", renderer);
                var so = new SerializedObject(visual);
                so.FindProperty("plasmaMaterialIndex").intValue = plasma;
                so.ApplyModifiedPropertiesWithoutUndo();
                var wake = Material("Bike_Wake", new Color(.06f, .65f, 1), 0, 3);
                var trails = new TrailRenderer[2];
                for (int i = 0; i < 2; i++)
                {
                    var t = Marker(root.transform, "PlasmaWake_" + i, new Vector3(i == 0 ? -.57f : .57f, .22f, -1.55f));
                    var trail = t.gameObject.AddComponent<TrailRenderer>();
                    trail.sharedMaterial = wake;
                    trail.time = .22f;
                    trail.minVertexDistance = .18f;
                    trail.widthMultiplier = .14f;
                    trail.startColor = Color.cyan;
                    trail.endColor = Color.clear;
                    trail.shadowCastingMode = ShadowCastingMode.Off;
                    trail.receiveShadows = false;
                    trail.numCapVertices = 2;
                    trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
                    trails[i] = trail;
                }
                References(visual, "trails", trails);
                PrefabUtility.SaveAsPrefabAsset(root, visualPath);
                Debug.Log($"Bike mesh bounds {renderer.bounds}; materials {string.Join(",", names)}");
            }
            finally { Object.DestroyImmediate(root); }
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/PF_AlienFlybyBike.prefab") == null)
        {
            var root = new GameObject("PF_AlienFlybyBike");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(visualPath), root.transform);
                var flyby = root.AddComponent<AlienFlybyBike>();
                Reference(flyby, "visual", visual.GetComponent<AlienBikeVisual>());
                Reference(flyby, "engine", Emitter(root.transform, "FlybyEngine"));
                Reference(flyby, "flybySound", AssetDatabase.LoadAssetAtPath<SoundEvent>("Assets/Game/Audio/Events/Aliens/Alien_Vehicle_Flyby.asset"));
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/PF_AlienFlybyBike.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/PF_RideableAlienBike.prefab") == null)
        {
            var root = new GameObject("PF_RideableAlienBike");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(visualPath), root.transform);
                var body = root.AddComponent<Rigidbody>();
                body.mass = 110;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                body.linearDamping = .2f;
                body.angularDamping = 5;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new(0, .75f, .11f);
                collider.size = new(1.8f, 1.8f, 3.5f);
                var controller = root.AddComponent<AlienBikeController>();
                root.AddComponent<AlienBikeImpact>();
                var approach = Marker(root.transform, "ApproachPoint", new(-1.45f, -.85f, -.35f));
                var mount = Marker(root.transform, "MountPoint", new(-1.2f, -.85f, -.35f));
                CreateMountApproaches(controller, approach, mount);
                controller.seatPoint = Marker(root.transform, "SeatPoint", new(0, .07f, 0));
                controller.dismountLeft = Marker(root.transform, "DismountLeft", new(-1.6f, -.85f, -.35f));
                controller.dismountRight = Marker(root.transform, "DismountRight", new(1.6f, -.85f, -.35f));
                Reference(controller, "visual", visual.GetComponent<AlienBikeVisual>());
                var sound = root.AddComponent<AlienBikeAudio>();
                Reference(controller, "audioPresentation", sound);
                Reference(sound, "engine", Emitter(root.transform, "HoverEngine"));
                Reference(sound, "charge", Emitter(root.transform, "JumpCharge"));
                Reference(sound, "boost", Emitter(root.transform, "Turbo"));
                Reference(sound, "engineSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(Audio + "/Bike_Hover.asset"));
                Reference(sound, "chargeSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(Audio + "/Bike_JumpCharge.asset"));
                Reference(sound, "jumpSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(Audio + "/Bike_JumpRelease.asset"));
                Reference(sound, "turboSound", AssetDatabase.LoadAssetAtPath<SoundEvent>(Audio + "/Bike_Turbo.asset"));
                ConfigureVisualFeedback(controller);
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/PF_RideableAlienBike.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
    public static void ConfigureVisualFeedback(AlienBikeController bike)
    {
        var feedback = bike.GetComponent<AlienBikeVisualFeedback>();
        if (feedback != null && feedback.bikeLeanPivot != null && feedback.steeringPivot != null)
        {
            Reference(feedback, "bike", bike);
            return; // Preserve authored pivots and all tuning on repeat authoring.
        }
        var hull = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/Bike_RideHull.asset");
        var controls = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/Bike_SteeringControl.asset");
        if (hull == null || controls == null)
            throw new InvalidOperationException("Rideable bike needs its authored hull/control meshes.");
        var visual = bike.GetComponentInChildren<AlienBikeVisual>(true);
        var filter = visual.GetComponentInChildren<MeshFilter>(true);
        var lean = bike.transform.Find("BikeLeanPivot") ?? Marker(bike.transform, "BikeLeanPivot", Vector3.zero);
        visual.transform.SetParent(lean, false);
        filter.sharedMesh = hull;
        PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        var steering = lean.Find("SteeringPivot") ?? Marker(lean, "SteeringPivot", new(0, .5f, .49f));
        var control = steering.Find("ControlAssembly") ?? Marker(steering, "ControlAssembly", Vector3.zero);
        var controlMesh = control.GetComponent<MeshFilter>();
        if (controlMesh == null)
            controlMesh = control.gameObject.AddComponent<MeshFilter>();
        controlMesh.sharedMesh = controls;
        var renderer = control.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = control.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        if (feedback == null)
            feedback = bike.gameObject.AddComponent<AlienBikeVisualFeedback>();
        Reference(feedback, "bike", bike);
        feedback.bikeLeanPivot = lean;
        feedback.steeringPivot = steering;
    }
    private static AudioEmitter Emitter(Transform parent, string name)
    {
        var t = Marker(parent, name, Vector3.zero);
        var source = t.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        return t.gameObject.AddComponent<AudioEmitter>();
    }
    public static void CreateMountApproaches(AlienBikeController bike, Transform leftApproach, Transform leftMount)
    {
        // Serialized order is the walkable perimeter, starting on the existing left side.
        // Corners share their side's mount marker; neither front nor rear requires walking through the hull.
        float y = leftApproach.localPosition.y;
        var rightMount = Marker(bike.transform, "MountRight", new(-leftMount.localPosition.x, leftMount.localPosition.y, leftMount.localPosition.z));
        var points = new[]
        {
            leftApproach,
            Marker(bike.transform, "ApproachRearLeft", new(-1.45f, y, -2.2f)),
            Marker(bike.transform, "ApproachRearRight", new(1.45f, y, -2.2f)),
            Marker(bike.transform, "ApproachRight", new(-leftApproach.localPosition.x, y, leftApproach.localPosition.z)),
            Marker(bike.transform, "ApproachFrontRight", new(1.45f, y, 2.3f)),
            Marker(bike.transform, "ApproachFrontLeft", new(-1.45f, y, 2.3f))
        };
        bike.mountApproaches = new AlienBikeController.MountApproach[points.Length];
        for (int i = 0; i < points.Length; i++)
            bike.mountApproaches[i] = new(points[i], i == 0 || i == 1 || i == 5 ? leftMount : rightMount);
    }
    private static void EnsureSounds()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Game/Audio/AudioLibrary.asset");
        var flyby = AssetDatabase.LoadAssetAtPath<SoundEvent>("Assets/Game/Audio/Events/Aliens/Alien_Vehicle_Flyby.asset");
        if (!library.events.Contains(flyby))
            library.events.Add(flyby);
        string[] names = { "Bike_Hover", "Bike_JumpCharge", "Bike_JumpRelease", "Bike_Turbo" };
        string[] sources = { "Machinery/Machinery_Reactor_Buzz", "Weapons/Weapon_Plasma_Charge", "Beam/Beam_End", "Machinery/Machinery_Reactor_Buzz" };
        for (int i = 0; i < names.Length; i++)
        {
            string path = Audio + "/" + names[i] + ".asset";
            var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
            if (sound == null)
            {
                var candidate = AssetDatabase.LoadAssetAtPath<SoundEvent>("Assets/Game/Audio/Events/" + sources[i] + ".asset");
                sound = ScriptableObject.CreateInstance<SoundEvent>();
                sound.displayName = names[i];
                sound.category = SoundCategory.Aliens;
                sound.status = SoundStatus.Placeholder;
                sound.variants = candidate.variants;
                sound.mixerGroup = candidate.mixerGroup;
                sound.volume = new(.2f, .26f);
                sound.pitch = new(.94f, 1.04f);
                sound.minDistance = 3;
                sound.maxDistance = 55;
                sound.maxVoices = 4;
                sound.description = "Bike semantic event using existing starter pack audio; replace variants after listening on device.";
                AssetDatabase.CreateAsset(sound, path);
            }
            if (!library.events.Contains(sound))
                library.events.Add(sound);
        }
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssetIfDirty(library);
    }
    private static void EnsurePose()
    {
        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PosePath) != null)
            return;
        var clip = new AnimationClip { name = "Bike_RiderSit", frameRate = 30 };
        // Standard Humanoid muscle animation, independent of Amy's bone/hierarchy names.
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
        {
            string n = HumanTrait.MuscleName[i];
            float value = 0;
            if (n.Contains("Upper Leg Front-Back"))
                value = -.65f;
            if (n.Contains("Upper Leg In-Out"))
                value = .55f;
            if (n.Contains("Arm Down-Up"))
                value = .1f;
            if (n.Contains("Arm Front-Back"))
                value = -.4f;
            if (n.Contains("Forearm Stretch"))
                value = 1;
            if (n.Contains("Spine Front-Back"))
                value = -.3f;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), n), AnimationCurve.Linear(0, value, 1, value));
        }
        SetRiderRootCurves(clip);
        AssetDatabase.CreateAsset(clip, Art + "/Bike_RiderSit.anim");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(PosePath);
        var state = controller.layers[0].stateMachine.AddState("Rider");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
    }
    public static void SetRiderRootCurves(AnimationClip clip)
    {
        foreach (string name in new[] { "RootT.x", "RootT.y", "RootT.z", "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" })
        {
            float value = name == "RootT.y" ? .9f : name == "RootQ.w" ? 1 : 0;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), name), AnimationCurve.Constant(0, 1, value));
        }
    }
}
