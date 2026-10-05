using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Explicit V4 authoring; never adds weapons to other levels or atmospheric flybys.</summary>
public static class BikeRouteLaserSetup
{
    public const string Content = "Assets/Game/Prefabs/Vehicles/HeavyLaser";
    private const string Sounds = "Assets/Game/Audio/Events/Vehicles/";
    [MenuItem("Tools/Level Authoring/Update BikeRoute Vehicle Lasers")]
    public static void UpdateContent()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (Application.isPlaying || scene.path != BikeRouteChaseSetup.ScenePath)
            throw new InvalidOperationException("Open BikeRoute outside Play Mode.");
        var director = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
        var camera = Object.FindAnyObjectByType<GameplayCameraController>();
        if (director == null || camera == null) throw new InvalidOperationException("Expected existing chase and gameplay camera owners.");
        var riders = director.WaveOne.Concat(director.WaveTwo).Select(s => s.rider).ToArray();
        if (riders.Length != 5 || riders.Any(r => r == null)) throw new InvalidOperationException("Expected five explicitly assigned riders.");
        Directory.CreateDirectory(Content); AssetDatabase.Refresh();
        CreateSounds(); CreateEffects();
        var prefab = PrefabUtility.LoadPrefabContents(BikeRouteChaseSetup.EnemyPrefab);
        try
        {
            ConfigureWeapon(prefab.GetComponent<AlienBikeController>());
            prefab.GetComponent<EnemyRangedAttack>().enabled = false;
            PrefabUtility.SaveAsPrefabAsset(prefab, BikeRouteChaseSetup.EnemyPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var playerWeapon = ConfigureWeapon(director.PlayerBike);
        playerWeapon.player = director.Player; playerWeapon.combatCamera = camera;
        playerWeapon.targets = riders.Select(r => r.GetComponent<AlienBikeController>()).ToArray();
        for (int i = 0; i < riders.Length; i++)
        {
            var weapon = ConfigureWeapon(riders[i].GetComponent<AlienBikeController>());
            weapon.combatCamera = camera; weapon.targets = new[] { director.PlayerBike };
            weapon.initialDelay = .2f + i * .23f;
            EditorUtility.SetDirty(weapon); PrefabUtility.RecordPrefabInstancePropertyModifications(weapon);
        }
        EditorUtility.SetDirty(playerWeapon);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("BikeRoute V4 authored: shared laser on six bikes, five handheld attacks disabled, authored speed/boost tuning preserved.");
    }
    public static void Batch()
    {
        EditorSceneManager.OpenScene(BikeRouteChaseSetup.ScenePath);
        UpdateContent();
    }
    public static void EffectsBatch() => CreateEffects();
    private static AlienBikeLaserWeapon ConfigureWeapon(AlienBikeController bike)
    {
        var weapon = bike.GetComponent<AlienBikeLaserWeapon>();
        if (weapon == null) weapon = bike.gameObject.AddComponent<AlienBikeLaserWeapon>();
        if (weapon.muzzle == null)
        {
            var marker = new GameObject("HeavyLaserMuzzle").transform;
            marker.SetParent(bike.transform, false); marker.localPosition = new Vector3(0, .55f, 2.05f);
            weapon.muzzle = marker;
        }
        weapon.boltPrefab = AssetDatabase.LoadAssetAtPath<AlienBikeLaserBolt>(Content + "/PF_BikeHeavyBolt.prefab");
        weapon.lockBeep = Sound("Bike_LockBeep"); weapon.lockComplete = Sound("Bike_LockComplete"); weapon.fireSound = Sound("Bike_HeavyLaserFire");
        EditorUtility.SetDirty(weapon);
        return weapon;
    }
    private static SoundEvent Sound(string name) => AssetDatabase.LoadAssetAtPath<SoundEvent>(Sounds + name + ".asset");
    private static void CreateSounds()
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Game/Audio/AudioLibrary.asset");
        var template = AssetDatabase.LoadAssetAtPath<SoundEvent>("Assets/Game/Audio/Events/Weapons/Weapon_PlasmaPistol_Fire.asset");
        foreach (string name in new[] { "Bike_LockBeep", "Bike_LockComplete", "Bike_HeavyLaserFire", "Bike_HeavyLaserImpact" })
        {
            var sound = Sound(name);
            if (sound == null)
            {
                sound = Object.Instantiate(template); sound.name = sound.displayName = name;
                sound.description = "Bike-owned heavy laser " + name; sound.status = SoundStatus.Placeholder;
                sound.notes = "V4 placeholder; vehicle laser only. Replace through this semantic event.";
                sound.maxVoices = 8; sound.cooldown = 0; sound.playDuringPause = false;
                if (name.StartsWith("Bike_Lock"))
                {
                    string clipPath = "Assets/Game/Audio/Clips/Vehicles/" + name + ".wav";
                    var importer = (AudioImporter)AssetImporter.GetAtPath(clipPath);
                    var settings = importer.defaultSampleSettings;
                    settings.preloadAudioData = true; settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = AudioCompressionFormat.PCM;
                    importer.defaultSampleSettings = settings; importer.loadInBackground = false; importer.SaveAndReimport();
                    sound.variants = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath) };
                    if (sound.variants[0] == null) throw new InvalidOperationException("Missing authored targeting tone: " + name);
                    sound.spatial = false; sound.volume = Vector2.one * .5f; sound.pitch = Vector2.one;
                }
                else
                {
                    sound.volume = Vector2.one * .65f; sound.pitch = Vector2.one * .65f; sound.maxDistance = 100;
                    if (name.EndsWith("Impact")) sound.variants = AssetDatabase.LoadAssetAtPath<SoundEvent>("Assets/Game/Audio/Events/Beam/Beam_End.asset").variants;
                }
                AssetDatabase.CreateAsset(sound, Sounds + name + ".asset");
            }
            if (!library.events.Contains(sound)) { library.events.Add(sound); EditorUtility.SetDirty(library); }
        }
        AssetDatabase.SaveAssetIfDirty(library);
    }
    private static void CreateEffects()
    {
        string impactPath = Content + "/PF_BikeHeavyImpact.prefab", boltPath = Content + "/PF_BikeHeavyBolt.prefab";
        var flashMaterial = AssetDatabase.LoadAssetAtPath<Material>(Content + "/HeavyLaserFlash.mat");
        if (flashMaterial == null)
        {
            flashMaterial = new Material(Shader.Find("Vehicles/Heavy Laser"));
            flashMaterial.SetColor("_Color", new Color(10, 2, 3, 1)); flashMaterial.SetFloat("_Round", 1);
            AssetDatabase.CreateAsset(flashMaterial, Content + "/HeavyLaserFlash.mat");
        }
        {
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(impactPath) != null;
            var root = existing ? PrefabUtility.LoadPrefabContents(impactPath)
                : Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/VFX/PlasmaImpact/PlasmaImpact.prefab"));
            try
            {
                root.name = "PF_BikeHeavyImpact";
                foreach (var particles in root.GetComponentsInChildren<ParticleSystem>())
                {
                    if (particles.name == "Impact Flash") continue;
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particles.main; main.duration = .6f; main.startLifetime = .55f; main.startSpeed = 8;
                    main.startSize = .65f; main.maxParticles = 48; main.loop = false;
                    var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 36) });
                    var size = particles.sizeOverLifetime; size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1.8f, 1, 0));
                }
                var flash = root.transform.Find("Impact Flash");
                if (flash == null)
                {
                    flash = new GameObject("Impact Flash").transform; flash.SetParent(root.transform, false);
                    var particles = flash.gameObject.AddComponent<ParticleSystem>();
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particles.main; main.duration = .25f; main.loop = false; main.playOnAwake = false;
                    main.startLifetime = .22f; main.startSpeed = 0; main.startSize = 5.5f; main.maxParticles = 1;
                    var emission = particles.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
                    var shape = particles.shape; shape.enabled = false;
                    var size = particles.sizeOverLifetime; size.enabled = true;
                    size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .6f), new Keyframe(.25f, 1.2f), new Keyframe(1, 0)));
                    var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = flashMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, impactPath);
            }
            finally { if (existing) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }
        var coreMaterial = AssetDatabase.LoadAssetAtPath<Material>(Content + "/HeavyLaserCore.mat");
        if (coreMaterial == null)
        {
            coreMaterial = new Material(Shader.Find("Vehicles/Heavy Laser"));
            coreMaterial.SetColor("_Color", new Color(12, 8, 9, 1));
            AssetDatabase.CreateAsset(coreMaterial, Content + "/HeavyLaserCore.mat");
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(boltPath) != null)
        {
            var existing = PrefabUtility.LoadPrefabContents(boltPath);
            try
            {
                existing.GetComponent<AlienBikeLaserBolt>().core.sharedMaterial = coreMaterial;
                PrefabUtility.SaveAsPrefabAsset(existing, boltPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(existing); }
            return;
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(Content + "/HeavyLaser.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Vehicles/Heavy Laser"));
            material.SetColor("_Color", new Color(5, .12f, 1.1f, 1));
            AssetDatabase.CreateAsset(material, Content + "/HeavyLaser.mat");
        }
        var boltRoot = new GameObject("PF_BikeHeavyBolt");
        try
        {
            var bolt = boltRoot.AddComponent<AlienBikeLaserBolt>();
            LineRenderer Line(string name, float width, Color color)
            {
                var child = new GameObject(name); child.transform.SetParent(boltRoot.transform, false);
                var line = child.AddComponent<LineRenderer>(); line.sharedMaterial = material;
                line.positionCount = 2; line.useWorldSpace = true; line.widthMultiplier = width;
                line.startColor = line.endColor = color; line.numCapVertices = 4;
                line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
                return line;
            }
            bolt.core = Line("Emissive Core", .5f, new Color(1, 5, 3));
            bolt.core.sharedMaterial = coreMaterial;
            bolt.halo = Line("Plasma Halo", 1.25f, new Color(1, 1, 1, .45f));
            bolt.trail = boltRoot.AddComponent<TrailRenderer>(); bolt.trail.sharedMaterial = material;
            bolt.trail.time = .16f; bolt.trail.widthMultiplier = .65f; bolt.trail.minVertexDistance = .8f;
            bolt.trail.startColor = new Color(1, .3f, .7f, .7f); bolt.trail.endColor = new Color(1, .1f, .4f, 0);
            bolt.trail.shadowCastingMode = ShadowCastingMode.Off; bolt.trail.receiveShadows = false;
            bolt.impactPrefab = AssetDatabase.LoadAssetAtPath<PlasmaImpactVFX>(impactPath); bolt.impactSound = Sound("Bike_HeavyLaserImpact");
            PrefabUtility.SaveAsPrefabAsset(boltRoot, boltPath);
        }
        finally { Object.DestroyImmediate(boltRoot); }
    }
}
