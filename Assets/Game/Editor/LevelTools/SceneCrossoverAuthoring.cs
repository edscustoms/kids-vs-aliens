using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit two-level authoring, separate from routine scene repair.</summary>
public static class SceneCrossoverAuthoring
{
    const string Folder = "Assets/Game/Scenes/BikeRoute/Crossover";
    const string SourcePath = "Assets/Game/Scenes/ConstructionSite.unity";
    const string DestinationPath = "Assets/Game/Scenes/BikeRoute.unity";
    static readonly Vector3[] Controls = {
        new(31.425f, 0, 48.6f), new(31.425f, 0, 63), new(48, 0, 79),
        new(87, 0, 82), new(125, 0, 66), new(160, 0, 56),
        new(190, 0, 67), new(200.5f, 0, 85), new(200.04f, 0, 100)
    };

    public static void FinishEntrance()
    {
        EditorSceneManager.OpenScene(DestinationPath);
        var terrain = Object.FindAnyObjectByType<Terrain>();
        var holes = new bool[52,52];
        for (int z = 0; z < 52; z++) for (int x = 0; x < 52; x++) holes[z, x] = true;
        for (int z = 0; z < 51; z++) for (int x = 0; x < 41; x++) holes[z, x] = false;
        terrain.terrainData.SetHoles(0, 0, holes);
        EditorUtility.SetDirty(terrain.terrainData); AssetDatabase.SaveAssetIfDirty(terrain.terrainData);
        var prefab = PrefabUtility.LoadPrefabContents(Folder + "/QuarryEntrance.prefab");
        try
        {
            if (prefab.transform.Find("Overlap floor beneath source") == null)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Environment/LocalCorrections/Compacted ramp gravel.mat");
                MeshObject("Overlap floor beneath source", prefab.transform,
                    new List<Vector3> { new(-.5f,-.15f,-.5f), new(-.5f,-.15f,51.5f), new(41.5f,-.15f,51.5f), new(41.5f,-.15f,-.5f) },
                    new List<Vector2> { new(0,0), new(0,17), new(14,17), new(14,0) }, new List<int> { 0,1,2,0,2,3 }, material);
                PrefabUtility.SaveAsPrefabAsset(prefab, Folder + "/QuarryEntrance.prefab");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (var path in new[] { Folder + "/Entrance gravel road.asset", Folder + "/Overlap floor beneath source.asset" })
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); mesh.RecalculateTangents();
            EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh);
        }
    }

    public static void RefineConnectionBounds()
    {
        EditorSceneManager.OpenScene(DestinationPath);
        var terrain = Object.FindAnyObjectByType<Terrain>();
        var prefab = PrefabUtility.LoadPrefabContents(Folder + "/QuarryEntrance.prefab");
        try
        {
            foreach (Transform child in prefab.transform.Cast<Transform>().ToArray())
                if (child.name != "Overlap floor beneath source") Object.DestroyImmediate(child.gameObject);
            var path = SamplePath();
            CreateRoad(prefab.transform, path, AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Environment/LocalCorrections/Compacted ramp gravel.mat"));
            var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Environment/QuarryToBasalt.mat");
            CreateCliff(prefab.transform, path, -1, rock, p => terrain.SampleHeight(p));
            CreateCliff(prefab.transform, path, 1, rock, p => terrain.SampleHeight(p));
            PrefabUtility.SaveAsPrefabAsset(prefab, Folder + "/QuarryEntrance.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        EditorSceneManager.OpenScene(SourcePath);
        var owner = Object.FindAnyObjectByType<AlienFlybyController>();
        foreach (var flight in owner.GetComponentsInChildren<AlienFlybyPath>(true))
            if (!AlienFlybyValidation.Validate(flight, owner.environmentRoots, out string failure)) throw new InvalidOperationException(failure);
    }

    [MenuItem("Tools/Level Authoring/Author ConstructionSite BikeRoute Crossover")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        Directory.CreateDirectory("Logs/SceneCrossover");
        var destination = EditorSceneManager.OpenScene(DestinationPath);
        if (Object.FindAnyObjectByType<GameplaySceneEntry>() != null)
            throw new InvalidOperationException("Crossover is already authored; edit its explicit scene components.");
        var terrain = Object.FindAnyObjectByType<Terrain>();
        var endCap = GameObject.Find("LevelGeometry/Corridor Boundaries/Start retaining end");
        if (terrain == null || endCap == null) throw new InvalidOperationException("Expected entrance terrain/end cap is missing.");
        var terrainPath = AssetDatabase.GetAssetPath(terrain.terrainData);
        File.Copy(terrainPath, "Logs/SceneCrossover/Terrain.before.asset", true);
        var data = terrain.terrainData;
        var path = SamplePath();
        var original = data.GetHeights(0, 0, 231, 131);
        float Height(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
        var connector = new GameObject("Quarry entrance corridor");
        var gravel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Environment/LocalCorrections/Compacted ramp gravel.mat");
        var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Environment/QuarryToBasalt.mat");
        CreateRoad(connector.transform, path, gravel);
        CreateCliff(connector.transform, path, -1, rock, Height);
        CreateCliff(connector.transform, path, 1, rock, Height);
        var prefab = PrefabUtility.SaveAsPrefabAsset(connector, Folder + "/QuarryEntrance.prefab");
        Object.DestroyImmediate(connector);

        // This 231 x 131 m corner is the sole terrain edit. Keep the rest byte-identical
        // at the height-sample level, including all eight accepted route sections.
        var heights = (float[,])original.Clone();
        int changed = 0; float deepest = 0;
        for (int z = 0; z < 131; z++) for (int x = 0; x < 231; x++)
        {
            var p = new Vector3(x, 0, z);
            float distance = Closest(path, p, out var road);
            float weight = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(8, 26, distance));
            float old = original[z, x] * data.size.y;
            float target = Mathf.Max(0, road.y - .18f);
            float next = Mathf.Lerp(old, Mathf.Min(old, target), weight);
            // Clear only the actual overlapping ConstructionSite footprint, including
            // its existing floor; this does not touch ConstructionSite's terrain data.
            if (x <= 41 && z <= 51) next = 0;
            else if (x < 58 && z < 68)
            {
                float outside = Mathf.Max(Mathf.Max(x - 41, z - 51), 0);
                next = Mathf.Min(next, Mathf.Lerp(0, old, Mathf.SmoothStep(0, 1, outside / 17)));
            }
            heights[z, x] = next / data.size.y;
            if (Mathf.Abs(next - old) > .001f) { changed++; deepest = Mathf.Max(deepest, old - next); }
        }
        data.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
        endCap.SetActive(false);
        var destinationConnector = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        destinationConnector.transform.SetParent(GameObject.Find("LevelGeometry").transform, true);
        var incoming = ConfigureEntry(destination, destinationConnector);
        incoming.bikeChase = Object.FindAnyObjectByType<BikeRouteChaseDirector>();
        incoming.entryGameplay = new[] { incoming.bikeChase.gameObject, incoming.bikeChase.PlayerBike.gameObject };
        incoming.bikeCameraBindings = incoming.bikeChase.GetComponentsInChildren<AlienBikeLaserWeapon>(true)
            .Concat(incoming.bikeChase.PlayerBike.GetComponents<AlienBikeLaserWeapon>()).ToArray();
        var warm = new List<GameObject>();
        foreach (Transform child in incoming.occlusion.transform)
            if (child.gameObject != destinationConnector) WarmRoots(child, warm);
        incoming.warmupRoots = warm.ToArray();
        EditorSceneManager.SaveScene(destination);

        var source = EditorSceneManager.OpenScene(SourcePath);
        var communication = GameObject.Find("GameplayCommunication");
        if (communication == null) throw new InvalidOperationException("Source objective owner is missing.");
        PrefabUtility.SaveAsPrefabAsset(communication, Folder + "/RunCommunication.prefab");
        var sourceGeometry = Object.FindAnyObjectByType<CameraOcclusionController>().transform;
        var sourceConnector = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        sourceConnector.transform.SetParent(sourceGeometry, true);
        var outgoing = ConfigureEntry(source, sourceConnector);
        var crossover = outgoing.gameObject.AddComponent<SceneCrossover>();
        var boundary = new GameObject("Handoff after quarry bend").AddComponent<BoxCollider>();
        boundary.transform.SetParent(outgoing.transform, false);
        boundary.center = new Vector3(189, 8, 75); boundary.size = new Vector3(28, 24, 54); boundary.isTrigger = true;
        var settings = new SerializedObject(crossover);
        settings.FindProperty("destinationScene").stringValue = "BikeRoute";
        settings.FindProperty("handoffArea").objectReferenceValue = boundary;
        settings.FindProperty("atmosphereStart").vector3Value = new Vector3(42, 0, 75);
        settings.FindProperty("atmosphereEnd").vector3Value = new Vector3(174, 0, 75);
        settings.ApplyModifiedPropertiesWithoutUndo();
        // Development bike remains available in the Editor; it never ships or crosses.
        var testBike = GameObject.Find("BikeRide_Test");
        if (testBike != null) testBike.tag = "EditorOnly";
        EditorSceneManager.SaveScene(source);

        destination = EditorSceneManager.OpenScene(DestinationPath);
        incoming = Object.FindAnyObjectByType<GameplaySceneEntry>();
        var communicationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/RunCommunication.prefab");
        var continuedCommunication = (GameObject)PrefabUtility.InstantiatePrefab(communicationPrefab);
        continuedCommunication.name = "GameplayCommunication";
        var objectives = new SerializedObject(continuedCommunication.GetComponent<ObjectiveController>());
        objectives.FindProperty("openingObjective").objectReferenceValue = null;
        objectives.ApplyModifiedPropertiesWithoutUndo();
        incoming.carriedRoots = incoming.carriedRoots.Concat(new[] { continuedCommunication }).ToArray();
        EditorSceneManager.SaveScene(destination);
        File.WriteAllText("Logs/SceneCrossover/authoring.txt", $"Connector samples: {path.Count}\nLocal height samples changed: {changed}\nDeepest overlapping terrain clearance: {deepest:F2}m\nWarmup groups: {incoming.warmupRoots.Length}\n");
    }

    static GameplaySceneEntry ConfigureEntry(Scene scene, GameObject connector)
    {
        var roots = scene.GetRootGameObjects();
        var entry = new GameObject("Gameplay Scene Entry").AddComponent<GameplaySceneEntry>();
        string[] names = { "_Player", "_Systems", "_UI", "GameplayPresentationV1", "AudioService", "GameplayCommunication" };
        entry.carriedRoots = roots.Where(r => names.Contains(r.name)).ToArray();
        entry.player = Object.FindAnyObjectByType<PlayerBikeRider>();
        entry.gameplayCamera = Camera.main;
        entry.cameraController = Object.FindAnyObjectByType<GameplayCameraController>();
        entry.occlusion = Object.FindAnyObjectByType<CameraOcclusionController>();
        entry.keyLight = RenderSettings.sun != null ? RenderSettings.sun : Object.FindObjectsByType<Light>().First(l => l.type == LightType.Directional);
        entry.volumes = Object.FindObjectsByType<Volume>().Where(v => v.isGlobal).ToArray();
        entry.lighting = SceneLightingState.Capture(entry.keyLight);
        entry.connector = connector;
        return entry;
    }
    static void WarmRoots(Transform t, List<GameObject> result)
    {
        if (!t.gameObject.activeSelf) return;
        if (t.GetComponent<Renderer>() || t.GetComponent<Collider>() || t.GetComponent<LODGroup>() ||
            t.GetComponents<MonoBehaviour>().Any(m => !(m is CameraOcclusionGroup)))
        { result.Add(t.gameObject); return; }
        foreach (Transform child in t) WarmRoots(child, result);
    }
    public static List<Vector3> SamplePath()
    {
        var points = new List<Vector3>();
        for (int i = 0; i < Controls.Length - 1; i++)
        {
            var a = Controls[Mathf.Max(0, i - 1)]; var b = Controls[i];
            var c = Controls[i + 1]; var d = Controls[Mathf.Min(Controls.Length - 1, i + 2)];
            int count = Mathf.CeilToInt(Vector3.Distance(b, c) / 2);
            for (int j = 0; j < count; j++)
            {
                float t = (float)j / count;
                points.Add(.5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t));
            }
        }
        points.Add(Controls.Last());
        for (int i = 0; i < points.Count; i++)
        { var p = points[i]; p.y = Mathf.Lerp(.015f, 6.01f, Mathf.SmoothStep(0, 1, (float)i / (points.Count - 1))); points[i] = p; }
        return points;
    }
    static float Closest(List<Vector3> path, Vector3 p, out Vector3 point)
    {
        float best = float.PositiveInfinity; point = path[0];
        for (int i = 1; i < path.Count; i++)
        {
            var a = path[i - 1]; var delta = path[i] - a; delta.y = 0;
            var flat = p - a; flat.y = 0;
            float t = Mathf.Clamp01(Vector3.Dot(flat, delta) / delta.sqrMagnitude);
            var candidate = Vector3.Lerp(a, path[i], t);
            float distance = new Vector2(p.x - candidate.x, p.z - candidate.z).magnitude;
            if (distance < best) { best = distance; point = candidate; }
        }
        return best;
    }
    static Vector3 Right(List<Vector3> p, int i) => Vector3.Cross(Vector3.up, p[Mathf.Min(i + 1, p.Count - 1)] - p[Mathf.Max(0, i - 1)]).normalized;
    static void CreateRoad(Transform parent, List<Vector3> path, Material material)
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        float length = 0;
        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0) length += Vector3.Distance(path[i], path[i - 1]);
            var right = Right(path, i);
            for (int j = 0; j < 5; j++)
            {
                float offset = (j - 2) * 2.1f;
                vertices.Add(path[i] + right * offset + Vector3.down * (j == 0 || j == 4 ? .06f : 0));
                uv.Add(new Vector2(offset / 3, length / 3));
            }
            if (i > 0) for (int j = 0; j < 4; j++) Quad(triangles, (i - 1) * 5 + j, i * 5 + j, i * 5 + j + 1, (i - 1) * 5 + j + 1);
        }
        MeshObject("Entrance gravel road", parent, vertices, uv, triangles, material);
    }
    static void CreateCliff(Transform parent, List<Vector3> path, int side, Material material, Func<Vector3, float> terrainHeight)
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        for (int i = 0; i < path.Count; i++)
        {
            var right = Right(path, i) * side;
            float top = terrainHeight(path[i] + right * 26);
            for (int j = 0; j < 7; j++)
            {
                float f = j / 6f;
                float offset = Mathf.Lerp(7.7f, 26, Mathf.Pow(f, 1.3f));
                float noise = Mathf.Sin(i * 1.7f + j * 2.3f) * Mathf.Sin(f * Mathf.PI) * .65f;
                var p = path[i] + right * (offset + noise);
                p.z = Mathf.Max(p.z, 55);
                p.y = Mathf.Lerp(path[i].y - .3f, Mathf.Max(path[i].y, top), f);
                // Taper the first few metres away from the existing exit fence.
                if (i < 4) p.y = Mathf.Lerp(path[i].y - .3f, p.y, i / 4f);
                vertices.Add(p); uv.Add(new Vector2(i, p.y));
            }
            if (i > 0) for (int j = 0; j < 6; j++)
            {
                int a = (i - 1) * 7 + j, b = i * 7 + j;
                if (side < 0) Quad(triangles, a, a + 1, b + 1, b); else Quad(triangles, a, b, b + 1, a + 1);
            }
        }
        MeshObject(side < 0 ? "Entrance west rock cut" : "Entrance east rock cut", parent, vertices, uv, triangles, material);
    }
    static void Quad(List<int> t, int a, int b, int c, int d) { t.AddRange(new[] { a, b, c, a, c, d }); }
    static void MeshObject(string name, Transform parent, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Material material)
    {
        var full = new Mesh(); full.SetVertices(vertices); full.SetUVs(0, uv); full.SetTriangles(triangles, 0); full.RecalculateNormals();
        var normals = full.normals;
        for (int start = 0, section = 0; start < triangles.Count; start += 576, section++)
        {
            string partName = triangles.Count <= 576 ? name : name + " " + section.ToString("00");
            var v = new List<Vector3>(); var n = new List<Vector3>(); var tex = new List<Vector2>(); var indices = new List<int>();
            var remap = new Dictionary<int,int>();
            for (int i = start; i < Mathf.Min(start + 576, triangles.Count); i++)
            {
                int original = triangles[i];
                if (!remap.TryGetValue(original, out int index))
                { index = v.Count; remap.Add(original, index); v.Add(vertices[original]); n.Add(normals[original]); tex.Add(uv[original]); }
                indices.Add(index);
            }
            var mesh = new Mesh { name = partName }; mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, tex); mesh.SetTriangles(indices,0);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            string path = Folder + "/" + partName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
            else AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(partName, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(parent, false); go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material; go.GetComponent<MeshCollider>().sharedMesh = mesh;
        }
        Object.DestroyImmediate(full);
    }
}
