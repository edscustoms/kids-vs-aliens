#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class KVAPropAnalyzer
{
    [MenuItem("Tools/Kids VS Aliens/Performance/Analyze Selected Props")]
    private static void AnalyzeSelectedProps()
    {
        var selected = Selection.gameObjects;

        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning(
                "[KVA Prop Analyzer] Select one or more prefab/scene roots first."
            );
            return;
        }

        Debug.Log(
            "\n============================================================\n" +
            "KVA PROP ANALYZER\n" +
            "============================================================"
        );

        foreach (var root in selected)
        {
            Analyze(root);
        }

        Debug.Log(
            "\n============================================================\n" +
            "KVA PROP ANALYZER COMPLETE\n" +
            "============================================================"
        );
    }

    private static void Analyze(GameObject root)
    {
        var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        var skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        var colliders = root.GetComponentsInChildren<Collider>(true);

        long totalTriangles = 0;
        long totalVertices = 0;
        int totalSubMeshes = 0;

        var uniqueMeshes = new HashSet<Mesh>();
        var uniqueMaterials = new HashSet<Material>();

        var meshUsage = new Dictionary<Mesh, int>();

        // ----------------------------------------------------
        // MeshFilters
        // ----------------------------------------------------

        foreach (var meshFilter in meshFilters)
        {
            var mesh = meshFilter.sharedMesh;

            if (mesh == null)
                continue;

            CountMesh(
                mesh,
                ref totalTriangles,
                ref totalVertices,
                ref totalSubMeshes,
                uniqueMeshes,
                meshUsage
            );
        }

        // ----------------------------------------------------
        // Skinned meshes
        // ----------------------------------------------------

        foreach (var renderer in skinnedRenderers)
        {
            var mesh = renderer.sharedMesh;

            if (mesh == null)
                continue;

            CountMesh(
                mesh,
                ref totalTriangles,
                ref totalVertices,
                ref totalSubMeshes,
                uniqueMeshes,
                meshUsage
            );
        }

        // ----------------------------------------------------
        // Materials
        // ----------------------------------------------------

        foreach (var renderer in renderers)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material != null)
                    uniqueMaterials.Add(material);
            }
        }

        // ----------------------------------------------------
        // Report
        // ----------------------------------------------------

        Debug.Log(
            $"\n------------------------------------------------------------\n" +
            $"PROP: {GetHierarchyPath(root.transform)}\n" +
            $"------------------------------------------------------------\n" +
            $"Renderers:        {renderers.Length:N0}\n" +
            $"Mesh Filters:     {meshFilters.Length:N0}\n" +
            $"Skinned Meshes:   {skinnedRenderers.Length:N0}\n" +
            $"Unique Meshes:    {uniqueMeshes.Count:N0}\n" +
            $"Triangles:        {totalTriangles:N0}\n" +
            $"Vertices:         {totalVertices:N0}\n" +
            $"SubMeshes:        {totalSubMeshes:N0}\n" +
            $"Unique Materials: {uniqueMaterials.Count:N0}\n" +
            $"Colliders:        {colliders.Length:N0}"
        );

        // ----------------------------------------------------
        // Renderer details
        // ----------------------------------------------------

        Debug.Log("  RENDERERS:");

        foreach (var renderer in renderers)
        {
            Mesh mesh = null;

            if (renderer is MeshRenderer)
            {
                var filter = renderer.GetComponent<MeshFilter>();

                if (filter != null)
                    mesh = filter.sharedMesh;
            }
            else if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
            }

            long tris = mesh != null
                ? GetTriangleCount(mesh)
                : 0;

            string meshName = mesh != null
                ? mesh.name
                : "NONE";

            Debug.Log(
                $"    {GetHierarchyPath(renderer.transform)}\n" +
                $"        Mesh:      {meshName}\n" +
                $"        Tris:      {tris:N0}\n" +
                $"        Materials: {renderer.sharedMaterials.Length}"
            );
        }

        // ----------------------------------------------------
        // Reused mesh details
        // ----------------------------------------------------

        var repeatedMeshes = meshUsage
            .Where(x => x.Value > 1)
            .OrderByDescending(x =>
                GetTriangleCount(x.Key) * x.Value
            )
            .ToList();

        if (repeatedMeshes.Count > 0)
        {
            Debug.Log("  REPEATED MESHES:");

            foreach (var pair in repeatedMeshes)
            {
                var mesh = pair.Key;
                int instances = pair.Value;

                long trisPerMesh = GetTriangleCount(mesh);
                long total = trisPerMesh * instances;

                Debug.Log(
                    $"    {mesh.name}\n" +
                    $"        Instances: {instances:N0}\n" +
                    $"        Tris each:  {trisPerMesh:N0}\n" +
                    $"        Total tris: {total:N0}"
                );
            }
        }

        // ----------------------------------------------------
        // Materials
        // ----------------------------------------------------

        if (uniqueMaterials.Count > 0)
        {
            Debug.Log("  MATERIALS:");

            foreach (var material in uniqueMaterials.OrderBy(x => x.name))
            {
                Debug.Log(
                    $"    {material.name}"
                );
            }
        }
    }

    private static void CountMesh(
        Mesh mesh,
        ref long totalTriangles,
        ref long totalVertices,
        ref int totalSubMeshes,
        HashSet<Mesh> uniqueMeshes,
        Dictionary<Mesh, int> meshUsage
    )
    {
        totalTriangles += GetTriangleCount(mesh);
        totalVertices += mesh.vertexCount;
        totalSubMeshes += mesh.subMeshCount;

        uniqueMeshes.Add(mesh);

        if (!meshUsage.TryAdd(mesh, 1))
            meshUsage[mesh]++;
    }

    private static long GetTriangleCount(Mesh mesh)
    {
        if (mesh == null)
            return 0;

        long triangles = 0;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            triangles +=
                (long)mesh.GetIndexCount(i) / 3;
        }

        return triangles;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        var names = new List<string>();

        while (transform != null)
        {
            names.Add(transform.name);
            transform = transform.parent;
        }

        names.Reverse();

        return string.Join("/", names);
    }
}

#endif