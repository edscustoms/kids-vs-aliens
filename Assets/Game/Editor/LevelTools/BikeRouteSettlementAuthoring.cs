using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class BikeRouteEnvironmentSetup
{
    static readonly string[] BuildingNames = { "DuneStepHouse", "OasisShop", "RedClayTownhouse", "ShadePorchHouse",
        "BlueShutterHouse", "RouteServiceHall", "CornerMarket", "ArchedWorkshop" };

    [MenuItem("Tools/Level Authoring/Update BikeRoute Desert Settlement")]
    public static void RefreshSettlement()
    {
        if (!Application.isBatchMode && !UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Initialize();
        var geometry = GameObject.Find("LevelGeometry").transform;
        var art = geometry.Find("Environment Art");
        if (art == null) throw new InvalidOperationException("Author the BikeRoute environment first.");
        var old = art.Find("Desert roadside settlement");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        BuildBuildings(geometry, art);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(terrain.gameObject.scene);
    }

    static Material BuildingMaterial(string name)
    {
        string path = Models + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        var source = LoadMaterial("Wall", name == "Brick" || name == "Masonry" ? "Sandstone_Blocks_05" : "Sandstone_Blocks_08");
        material = new Material(source) { name = name, enableInstancing = true };
        Color color = name switch {
            "Teal" => new Color(.27f,.58f,.54f), "Blue" => new Color(.32f,.48f,.65f),
            "Red" => new Color(.66f,.30f,.23f), "Yellow" => new Color(.88f,.68f,.32f),
            "Brick" => new Color(.72f,.40f,.28f), "Stucco" => new Color(.95f,.86f,.66f),
            "Glass" => new Color(.08f,.17f,.20f), "Roof" => new Color(.42f,.38f,.30f), _ => Color.white };
        material.SetColor("_BaseColor", color);
        if (name == "Glass") { material.SetTexture("_BaseMap", null); material.SetTexture("_BumpMap", null); material.DisableKeyword("_NORMALMAP"); material.SetFloat("_Smoothness", .35f); }
        AssetDatabase.CreateAsset(material, path); return material;
    }
    static void BuildBuildings(Transform geometry, Transform art)
    {
        var old = geometry.Find("Service Buildings");
        if (old != null) { old.gameObject.SetActive(false); old.gameObject.tag = "EditorOnly"; }
        foreach (string name in BuildingNames)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx");
            if (model == null) throw new InvalidOperationException("Missing original Blender model " + name);
            var instance = new GameObject("PF_" + name);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model); visual.transform.SetParent(instance.transform, false);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => BuildingMaterial(m.name)).ToArray();
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            var box = instance.AddComponent<BoxCollider>(); box.center = bounds.center; box.size = bounds.size;
            PrefabUtility.SaveAsPrefabAsset(instance, Models + "PF_" + name + ".prefab"); Object.DestroyImmediate(instance);
        }
        var root = Group("Desert roadside settlement", art);
        // Deliberate frontage clusters separated by service yards; all eight models are reusable prefabs.
        var locations = new List<(int path, float station, int side, int model)> {
            (0,85,-1,7),(0,155,1,3),(0,215,1,1),(7,95,-1,4),(7,180,1,5) };
        for (int i = 0; i < 12; i++) locations.Add((4, 175 + i / 2 * 43, i % 2 == 0 ? -1 : 1, i % 8));
        foreach (var location in locations)
        {
            var r = routes[location.path]; float s = location.station; int side = location.side;
            Vector3 p = r.At(s) + r.Right(s) * (side * (r.source.roadWidth * .5f + 9));
            p.y = r.At(s).y - .3f;
            var building = Place(Models + "PF_" + BuildingNames[location.model] + ".prefab", p,
                Quaternion.LookRotation(-r.Right(s) * side), root);
            KeepOutsideDrivingLine(building, r, s, side, false);
        }
    }

    static void AuthorContainmentAndPressure(Transform geometry, Transform art)
    {
        var chase = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
        var safety = chase.GetComponent<BikeRouteContainment>() ?? chase.gameObject.AddComponent<BikeRouteContainment>();
        safety.chase = chase;
        var laser = chase.PlayerBike.GetComponent<AlienBikeLaserWeapon>();
        safety.warningSound = laser.lockComplete;
        safety.defenseSound = laser.boltPrefab.impactSound; safety.defenseImpact = laser.boltPrefab.impactPrefab;
        var jump = guide.jumps.Single(j => j.path == 5);
        safety.flightStart = jump.releaseDistance - 20; safety.flightEnd = jump.releaseDistance + 265;
        EditorUtility.SetDirty(safety);
        // This is a local, visible flight lane. There is no tall solid wall across the jump.
        var beacons = Group("Bridge flight safety beacons", art);
        string path = Content + "/FlightBeacon.mat";
        var amber = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (amber == null)
        {
            amber = new Material(Shader.Find("Universal Render Pipeline/Unlit")); amber.SetColor("_BaseColor", new Color(1,.55f,.08f));
            AssetDatabase.CreateAsset(amber, path);
        }
        var strip = new MeshBuilder();
        foreach (int side in new[] { -1, 1 })
            for (float s = safety.flightStart; s < safety.flightEnd; s += 15)
            {
                var sample = guide.At(5, s); var p = sample.position + sample.Right * (side * (sample.halfWidth + safety.flightMargin));
                Column(strip, p, .11f, 2.7f, 0);
                // Floating upper markers explain the playable air lane without presenting a fake solid wall.
                Column(strip, p + Vector3.up * 8, .22f, .6f, 0);
            }
        MeshObject("BridgeFlightBeacons", strip, beacons, new[] { amber }, false);
        var at = guide.At(5, safety.flightStart - 12);
        var sign = Group("Flight corridor instruction", beacons); sign.position = at.position + at.Right * (at.halfWidth + 2) + Vector3.up * 3;
        sign.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(at.forward, Vector3.up));
        var text = sign.gameObject.AddComponent<TMPro.TextMeshPro>(); text.text = "FLIGHT CORRIDOR\nKEEP BETWEEN AMBER BEACONS";
        text.fontSize = 3; text.alignment = TMPro.TextAlignmentOptions.Center; text.color = new Color(1,.73f,.3f);
        text.rectTransform.sizeDelta = new Vector2(8, 2); text.enableWordWrapping = false;
    }
}
