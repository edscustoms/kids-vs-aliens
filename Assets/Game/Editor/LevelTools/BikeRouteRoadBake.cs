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
}
