using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Copies the BikeRoute scene's edited EasyRoads meshes into its existing runtime assets.</summary>
public static class BikeRouteRoadBake
{
    private const string MeshFolder = "Assets/Game/Scenes/BikeRoute/Meshes/";

    [MenuItem("Tools/Level Authoring/Refresh BikeRoute Road Paint")]
    public static void RefreshRoadPaint()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().name == "BikeRoute",
            "Open BikeRoute outside Play Mode before refreshing paint.");
        var baked = GameObject.Find("LevelGeometry/BakedAsphalt");
        Require(baked != null, "Missing baked BikeRoute road.");
        var edge = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Materials/RoadEdge.mat");
        var center = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Materials/RoadCenter.mat");
        Require(edge != null && center != null, "Missing road paint materials.");
        foreach (Transform road in baked.transform)
            Require(road.GetComponent<MeshFilter>()?.sharedMesh != null
                && road.Find("Road markings") != null
                && road.Find("Road markings").GetComponent<Collider>() == null,
                "Expected an existing collider-free paint surface on " + road.name);
        foreach (Transform road in baked.transform) UpdateMarkings(road, edge, center);
    }

    [MenuItem("Tools/Level Authoring/Update BikeRoute Asphalt Meshes")]
    public static void UpdateAsphaltMeshes()
    {
        var scene = SceneManager.GetActiveScene();
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && scene.name == "BikeRoute",
            "Open BikeRoute outside Play Mode before updating its asphalt meshes.");
        var roots = scene.GetRootGameObjects();
        var authoringRoots = roots.Where(root => root.name == "_RoadAuthoring").ToArray();
        var geometryRoots = roots.Where(root => root.name == "LevelGeometry").ToArray();
        Require(authoringRoots.Length == 1 && geometryRoots.Length == 1,
            "BikeRoute needs exactly one _RoadAuthoring root and one LevelGeometry root.");
        var authoring = authoringRoots[0];
        Require(authoring.CompareTag("EditorOnly"), "_RoadAuthoring must retain its EditorOnly tag.");
        var bakedRoots = geometryRoots[0].transform.Cast<Transform>()
            .Where(child => child.name == "BakedAsphalt").ToArray();
        Require(bakedRoots.Length == 1, "LevelGeometry needs exactly one BakedAsphalt child.");
        var sources = authoring.GetComponentsInChildren<MeshFilter>(true);
        var copies = new List<(Mesh source, Mesh target, MeshCollider collider)>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        // Asset writes are not scene Undo: reject every ambiguous or out-of-scope reference first.
        foreach (Transform child in bakedRoots[0])
        {
            Require(names.Add(child.name), "Duplicate baked asphalt name: " + child.name);
            var matches = sources.Where(source => source.name == child.name).ToArray();
            Require(matches.Length == 1, "Expected one editable source mesh for " + child.name);
            var source = matches[0];
            var filter = child.GetComponent<MeshFilter>();
            var collider = child.GetComponent<MeshCollider>();
            var path = MeshFolder + child.name + ".asset";
            var target = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Require(target != null && AssetDatabase.GetAssetPath(target) == path
                && filter != null && filter.sharedMesh == target
                && collider != null && collider.sharedMesh == target,
                "Baked renderer and collider must share their BikeRoute mesh asset: " + path);
            Require(source.sharedMesh != null && source.sharedMesh != target
                && source.sharedMesh.vertexCount >= 3 && source.sharedMesh.triangles.Length >= 3,
                "Editable source has no valid separate mesh: " + child.name);
            Require(source.transform.localToWorldMatrix == child.localToWorldMatrix,
                "Source and baked transforms differ for " + child.name + "; reshape the native markers instead.");
            copies.Add((source.sharedMesh, target, collider));
        }
        Require(copies.Count > 0, "BikeRoute has no baked asphalt meshes to update.");
        Require(new[] { "01_LearnSpeed_Asphalt", "02_LongSweep_Asphalt", "03_NarrowS_Asphalt" }.All(names.Contains),
            "BikeRoute opening asphalt sections are required to preserve their joins.");
        var middle = sources.Single(f => f.name == "02_LongSweep_Asphalt").sharedMesh;
        Require(middle.vertexCount >= 4 && middle.vertexCount % 2 == 0, "Expected alternating native asphalt edges.");
        var edgePaint = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Materials/RoadEdge.mat");
        var centerPaint = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Scenes/BikeRoute/Materials/RoadCenter.mat");
        Require(edgePaint != null && centerPaint != null, "BikeRoute road-paint materials are missing.");

        foreach (var copy in copies)
        {
            var meshName = copy.target.name;
            Undo.RecordObject(copy.target, "Update BikeRoute Asphalt Mesh");
            EditorUtility.CopySerialized(copy.source, copy.target);
            copy.target.name = meshName;
            EditorUtility.SetDirty(copy.target);
            AssetDatabase.SaveAssetIfDirty(copy.target);
            // Refresh the collision shape while preserving the existing asset reference and GUID.
            copy.collider.sharedMesh = null;
            copy.collider.sharedMesh = copy.target;
        }
        FitRoadJoins(bakedRoots[0], sources);
        foreach (Transform road in bakedRoots[0])
            UpdateMarkings(road, edgePaint, centerPaint);
        Undo.RecordObject(authoring, "Hide BikeRoute Road Authoring");
        authoring.SetActive(false);
        Undo.RecordObject(bakedRoots[0].gameObject, "Show BikeRoute Asphalt");
        bakedRoots[0].gameObject.SetActive(true);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.LogWarning($"Updated {copies.Count} BikeRoute asphalt meshes and hid _RoadAuthoring. "
            + "Terrain heights were not changed: conform the isolated BikeRoute Terrain with Unity Terrain tools "
            + "where markers moved, then check boundaries, dressing, road edges and drive the changed sections. Save the scene when ready.", authoring);
    }

    private static Transform FitRoadJoins(Transform baked, MeshFilter[] sources)
    {
        var road = baked.Find("02_LongSweep_Asphalt");
        var before = baked.Find("01_LearnSpeed_Asphalt");
        var after = baked.Find("03_NarrowS_Asphalt");
        Require(road != null && before != null && after != null, "Missing BikeRoute opening asphalt sections.");
        var source = sources.Single(f => f.name == road.name);
        Require(source.transform.localToWorldMatrix == road.localToWorldMatrix, "Road join source and baked transforms differ.");
        var target = road.GetComponent<MeshFilter>().sharedMesh;
        Require(AssetDatabase.GetAssetPath(target) == MeshFolder + road.name + ".asset"
            && road.GetComponent<MeshCollider>().sharedMesh == target, "Unexpected BikeRoute join mesh ownership.");
        // Always start from the editable strip: repeated baking must not compound a taper.
        var vertices = source.sharedMesh.vertices;
        Require(vertices.Length == target.vertexCount && vertices.Length >= 4 && vertices.Length % 2 == 0,
            "Expected matching native and baked alternating asphalt edges.");
        var first = before.GetComponent<MeshFilter>().sharedMesh.vertices;
        var last = after.GetComponent<MeshFilter>().sharedMesh.vertices;
        Vector3 startLeft = road.InverseTransformPoint(before.TransformPoint(first[first.Length-2]));
        Vector3 startRight = road.InverseTransformPoint(before.TransformPoint(first[first.Length-1]));
        Vector3 endLeft = road.InverseTransformPoint(after.TransformPoint(last[0]));
        Vector3 endRight = road.InverseTransformPoint(after.TransformPoint(last[1]));
        Vector3 a = vertices[0], b = vertices[1], c = vertices[vertices.Length-2], d = vertices[vertices.Length-1];
        for (int i = 0; i < vertices.Length; i += 2)
        {
            var center = (vertices[i] + vertices[i+1]) * .5f;
            float start = 1 - Mathf.SmoothStep(0, 1, Vector3.Distance(center, (a+b)*.5f) / 24);
            float end = 1 - Mathf.SmoothStep(0, 1, Vector3.Distance(center, (c+d)*.5f) / 24);
            vertices[i] += (startLeft-a)*start + (endLeft-c)*end;
            vertices[i+1] += (startRight-b)*start + (endRight-d)*end;
        }
        Undo.RecordObject(target, "Clean BikeRoute asphalt joins");
        target.vertices = vertices; target.RecalculateNormals(); target.RecalculateTangents(); target.RecalculateBounds();
        EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target);
        var collider = road.GetComponent<MeshCollider>(); collider.sharedMesh = null; collider.sharedMesh = target;
        return road;
    }

    // BikeRoute's native strips use alternating left/right vertices. Paint follows those
    // exact edges, including height and width changes, with no runtime objects per dash.
    private static void UpdateMarkings(Transform road, Material edges, Material center)
    {
        var strip = road.GetComponent<MeshFilter>().sharedMesh.vertices;
        var vertices = new List<Vector3>();
        var edgeTriangles = new List<int>();
        var centerTriangles = new List<int>();
        float station = 0;
        for (int i = 0; i < strip.Length - 3; i += 2)
        {
            Vector3 a = strip[i], b = strip[i + 1], c = strip[i + 2], d = strip[i + 3];
            float widthA = Vector3.Distance(a, b), widthB = Vector3.Distance(c, d);
            Add(.25f / widthA, .43f / widthA, .25f / widthB, .43f / widthB, 0, 1, edgeTriangles);
            Add(1 - .43f / widthA, 1 - .25f / widthA, 1 - .43f / widthB, 1 - .25f / widthB, 0, 1, edgeTriangles);
            float length = Vector3.Distance((a + b) * .5f, (c + d) * .5f);
            float cursor = 0;
            while (widthA >= 6 && cursor < length - .0001f)
            {
                float phase = Mathf.Repeat(station + cursor, 10);
                float end = Mathf.Min(length, cursor + (phase < 4 ? 4 - phase : 10 - phase));
                if (phase < 4)
                    Add(.5f - .08f / widthA, .5f + .08f / widthA,
                        .5f - .08f / widthB, .5f + .08f / widthB, cursor / length, end / length, centerTriangles);
                cursor = end + .0001f;
            }
            station += length;

            void Add(float a0, float a1, float b0, float b1, float from, float to, List<int> triangles)
            {
                Vector3 l0 = Vector3.Lerp(a, b, a0), r0 = Vector3.Lerp(a, b, a1);
                Vector3 l1 = Vector3.Lerp(c, d, b0), r1 = Vector3.Lerp(c, d, b1);
                int v = vertices.Count;
                vertices.Add(Vector3.Lerp(l0, l1, from) + Vector3.up * .025f);
                vertices.Add(Vector3.Lerp(r0, r1, from) + Vector3.up * .025f);
                vertices.Add(Vector3.Lerp(l0, l1, to) + Vector3.up * .025f);
                vertices.Add(Vector3.Lerp(r0, r1, to) + Vector3.up * .025f);
                triangles.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
        }
        AddGuidancePaint(road.name, strip, vertices, centerTriangles);
        string path = MeshFolder + road.name + "_Markings.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create = mesh == null;
        if (create) mesh = new Mesh { name = road.name + " markings" };
        else { Undo.RecordObject(mesh, "Update BikeRoute Markings"); mesh.Clear(); }
        mesh.SetVertices(vertices);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(edgeTriangles, 0); mesh.SetTriangles(centerTriangles, 1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, path);
        else { EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
        var child = road.Find("Road markings");
        if (child == null)
        {
            child = new GameObject("Road markings", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            Undo.RegisterCreatedObjectUndo(child.gameObject, "Create BikeRoute Markings");
            child.SetParent(road, false);
        }
        child.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { edges, center };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    // Sparse authored beats, measured in metres along each existing asphalt strip.
    // These are paint-only chevrons in the forward lane, never physical road geometry.
    private static void AddGuidancePaint(string road, Vector3[] strip, List<Vector3> vertices, List<int> triangles)
    {
        float first = road switch {
            "01_LearnSpeed_Asphalt" => 140,
            "02_LongSweep_Asphalt" => 255,
            "03_NarrowS_Asphalt" => 150,
            "05_TurboBowl_Asphalt" => 195,
            "06_UpperCrossing_Asphalt" => 330,
            "08_Finish_Asphalt" => 180,
            _ => -1
        };
        if (first < 0) return;
        var stations = new float[strip.Length / 2];
        for (int i = 1; i < stations.Length; i++)
            stations[i] = stations[i - 1] + Vector3.Distance((strip[i*2]+strip[i*2+1])*.5f,
                (strip[i*2-2]+strip[i*2-1])*.5f);
        for (int mark = 0; mark < 3; mark++)
        {
            float at = first + mark * 12;
            AddArm(.5f, 1.35f, at, at + 1.7f);
            AddArm(1.35f, 2.2f, at + 1.7f, at);
        }
        void AddArm(float x0, float x1, float s0, float s1)
        {
            int v = vertices.Count;
            vertices.Add(Surface(x0,s0));vertices.Add(Surface(x1,s1));
            vertices.Add(Surface(x0,s0+.32f));vertices.Add(Surface(x1,s1+.32f));
            triangles.AddRange(new[]{v,v+2,v+1,v+1,v+2,v+3});
        }
        Vector3 Surface(float lateral, float distance)
        {
            int segment = Array.BinarySearch(stations, distance);
            if(segment<0) segment=~segment-1;
            segment=Mathf.Clamp(segment,0,stations.Length-2);
            float t=Mathf.InverseLerp(stations[segment],stations[segment+1],distance);
            Vector3 left=Vector3.Lerp(strip[segment*2],strip[segment*2+2],t);
            Vector3 right=Vector3.Lerp(strip[segment*2+1],strip[segment*2+3],t);
            return Vector3.Lerp(left,right,.5f+lateral/Vector3.Distance(left,right))+Vector3.up*.027f;
        }
    }
}
