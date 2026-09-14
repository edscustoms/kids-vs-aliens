#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class KVACementBagMeshSimplifier
{
    private const int TargetTriangles = 250;
    private const int SearchIterations = 28;

    [MenuItem("Tools/Kids VS Aliens/Performance/Create Safe Optimized Cement Bag Mesh")]
    private static void CreateOptimizedMesh()
    {
        Mesh source = GetSelectedMesh();

        if (source == null)
        {
            Debug.LogError(
                "[KVA Bag Simplifier] Select SM_CementBag_A_Normalized in the Project window."
            );

            return;
        }

        // ----------------------------------------------------
        // SAFETY
        // ----------------------------------------------------

        int sourceTriangles = GetTriangleCount(source);

        if (sourceTriangles < 1000)
        {
            Debug.LogError(
                $"[KVA Bag Simplifier] Selected mesh only has "
                    + $"{sourceTriangles:N0} triangles. "
                    + "Select the ORIGINAL 14,700-triangle bag."
            );

            return;
        }

        Bounds originalBounds = source.bounds;

        // ----------------------------------------------------
        // FIND A CLUSTER SIZE CLOSEST TO TARGET
        // ----------------------------------------------------

        float smallestDimension = Mathf.Min(
            originalBounds.size.x,
            originalBounds.size.y,
            originalBounds.size.z
        );

        float largestDimension = Mathf.Max(
            originalBounds.size.x,
            originalBounds.size.y,
            originalBounds.size.z
        );

        float low = smallestDimension / 500f;
        float high = largestDimension / 2f;

        SimplifyResult best = null;
        int bestDifference = int.MaxValue;

        for (int i = 0; i < SearchIterations; i++)
        {
            float cellSize = (low + high) * 0.5f;

            SimplifyResult result = Simplify(source, cellSize);

            int difference = Mathf.Abs(result.TriangleCount - TargetTriangles);

            if (difference < bestDifference)
            {
                bestDifference = difference;
                best = result;
            }

            // Larger cells = fewer triangles.
            if (result.TriangleCount > TargetTriangles)
            {
                low = cellSize;
            }
            else
            {
                high = cellSize;
            }
        }

        if (best == null || best.TriangleCount == 0)
        {
            Debug.LogError("[KVA Bag Simplifier] Simplification failed.");

            return;
        }

        // ----------------------------------------------------
        // CREATE MESH
        // ----------------------------------------------------

        Mesh optimized = new Mesh();

        optimized.name = "SM_CementBag_A_Optimized";

        optimized.indexFormat =
            best.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

        optimized.SetVertices(best.Vertices);

        if (best.UVs != null && best.UVs.Count == best.Vertices.Count)
        {
            optimized.SetUVs(0, best.UVs);
        }

        optimized.subMeshCount = best.SubmeshTriangles.Count;

        for (int i = 0; i < best.SubmeshTriangles.Count; i++)
        {
            optimized.SetTriangles(best.SubmeshTriangles[i], i, false);
        }

        // ----------------------------------------------------
        // MATCH ORIGINAL BOUNDS EXACTLY
        //
        // Keeps pivot-relative positioning identical.
        // ----------------------------------------------------

        optimized.RecalculateBounds();

        MatchBoundsExactly(optimized, originalBounds);

        optimized.RecalculateNormals();

        if (optimized.uv != null && optimized.uv.Length == optimized.vertexCount)
        {
            try
            {
                optimized.RecalculateTangents();
            }
            catch
            {
                // Tangents are not critical for the projected
                // bag material.
            }
        }

        optimized.RecalculateBounds();

        // ----------------------------------------------------
        // SAVE BESIDE ORIGINAL
        // ----------------------------------------------------

        string sourcePath = AssetDatabase.GetAssetPath(source);

        string directory = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/");

        if (string.IsNullOrEmpty(directory))
        {
            directory = "Assets/Game/Art/Environment/ConstructionSite/Generated";
        }

        string outputPath = $"{directory}/SM_CementBag_A_Optimized.asset";

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(outputPath);

        if (existing != null)
        {
            // ONLY updates our optimized copy.
            // Original source is never touched.
            EditorUtility.CopySerialized(optimized, existing);

            existing.name = "SM_CementBag_A_Optimized";

            EditorUtility.SetDirty(existing);

            UnityEngine.Object.DestroyImmediate(optimized);

            optimized = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(optimized, outputPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ----------------------------------------------------
        // SELECT OUTPUT ASSET
        //
        // IMPORTANT:
        // We deliberately DO NOT assign it to any prefab.
        // ----------------------------------------------------

        Selection.activeObject = optimized;

        EditorGUIUtility.PingObject(optimized);

        int finalTriangles = GetTriangleCount(optimized);

        Debug.Log(
            "\n"
                + "============================================\n"
                + "KVA SAFE CEMENT BAG SIMPLIFICATION COMPLETE\n"
                + "============================================\n"
                + $"Original:   {sourceTriangles:N0} tris\n"
                + $"Optimized:  {finalTriangles:N0} tris\n"
                + $"Reduction:  {(1f - (float)finalTriangles / sourceTriangles) * 100f:F2}%\n"
                + "\n"
                + $"Original center: {originalBounds.center}\n"
                + $"New center:      {optimized.bounds.center}\n"
                + "\n"
                + $"Original size:   {originalBounds.size}\n"
                + $"New size:        {optimized.bounds.size}\n"
                + "\n"
                + $"Created:\n{outputPath}\n"
                + "\n"
                + "ORIGINAL MESH UNTOUCHED\n"
                + "PREFABS UNTOUCHED\n"
                + "============================================"
        );
    }

    // ========================================================
    // SIMPLIFIER
    // ========================================================

    private static SimplifyResult Simplify(Mesh source, float cellSize)
    {
        Vector3[] sourceVertices = source.vertices;

        Vector2[] sourceUVs = source.uv;

        bool hasUV = sourceUVs != null && sourceUVs.Length == sourceVertices.Length;

        Bounds bounds = source.bounds;

        Vector3 origin = bounds.min;

        var clusters = new Dictionary<ClusterKey, Cluster>();

        int[] remap = new int[sourceVertices.Length];

        // ----------------------------------------------------
        // BUILD SPATIAL CLUSTERS
        // ----------------------------------------------------

        for (int i = 0; i < sourceVertices.Length; i++)
        {
            Vector3 p = sourceVertices[i];

            ClusterKey key = new ClusterKey(
                Mathf.FloorToInt((p.x - origin.x) / cellSize),
                Mathf.FloorToInt((p.y - origin.y) / cellSize),
                Mathf.FloorToInt((p.z - origin.z) / cellSize)
            );

            if (!clusters.TryGetValue(key, out Cluster cluster))
            {
                cluster = new Cluster();

                clusters.Add(key, cluster);
            }

            cluster.PositionSum += p;

            if (hasUV)
            {
                cluster.UvSum += sourceUVs[i];
            }

            cluster.Count++;
            cluster.SourceIndices.Add(i);
        }

        // ----------------------------------------------------
        // CREATE NEW VERTICES
        // ----------------------------------------------------

        var vertices = new List<Vector3>(clusters.Count);

        var uvs = hasUV ? new List<Vector2>(clusters.Count) : null;

        foreach (KeyValuePair<ClusterKey, Cluster> pair in clusters)
        {
            Cluster cluster = pair.Value;

            int newIndex = vertices.Count;

            Vector3 position = cluster.PositionSum / cluster.Count;

            vertices.Add(position);

            if (hasUV)
            {
                uvs.Add(cluster.UvSum / cluster.Count);
            }

            foreach (int oldIndex in cluster.SourceIndices)
            {
                remap[oldIndex] = newIndex;
            }
        }

        // ----------------------------------------------------
        // REBUILD ORIGINAL TRIANGLES
        //
        // Winding comes directly from the original mesh.
        // This is why we won't get the inside-out problem.
        // ----------------------------------------------------

        var submeshTriangles = new List<List<int>>();

        int triangleCount = 0;

        for (int submesh = 0; submesh < source.subMeshCount; submesh++)
        {
            int[] sourceTriangles = source.GetTriangles(submesh);

            var output = new List<int>(sourceTriangles.Length);

            var uniqueTriangles = new HashSet<TriangleKey>();

            for (int i = 0; i < sourceTriangles.Length; i += 3)
            {
                int a = remap[sourceTriangles[i]];

                int b = remap[sourceTriangles[i + 1]];

                int c = remap[sourceTriangles[i + 2]];

                // Collapsed triangle.
                if (a == b || b == c || c == a)
                {
                    continue;
                }

                Vector3 va = vertices[a];

                Vector3 vb = vertices[b];

                Vector3 vc = vertices[c];

                float area = Vector3.Cross(vb - va, vc - va).sqrMagnitude;

                if (area < 0.0000000001f)
                {
                    continue;
                }

                TriangleKey triangleKey = new TriangleKey(a, b, c);

                if (!uniqueTriangles.Add(triangleKey))
                {
                    continue;
                }

                // Preserve ORIGINAL winding.
                output.Add(a);
                output.Add(b);
                output.Add(c);

                triangleCount++;
            }

            submeshTriangles.Add(output);
        }

        return new SimplifyResult
        {
            Vertices = vertices,
            UVs = uvs,
            SubmeshTriangles = submeshTriangles,
            TriangleCount = triangleCount,
        };
    }

    // ========================================================
    // EXACT BOUNDS/PIVOT MATCH
    // ========================================================

    private static void MatchBoundsExactly(Mesh mesh, Bounds target)
    {
        mesh.RecalculateBounds();

        Bounds current = mesh.bounds;

        Vector3 currentSize = current.size;

        Vector3 targetSize = target.size;

        Vector3 scale = new Vector3(
            SafeRatio(targetSize.x, currentSize.x),
            SafeRatio(targetSize.y, currentSize.y),
            SafeRatio(targetSize.z, currentSize.z)
        );

        Vector3[] vertices = mesh.vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 relative = vertices[i] - current.center;

            relative = Vector3.Scale(relative, scale);

            vertices[i] = target.center + relative;
        }

        mesh.vertices = vertices;

        mesh.RecalculateBounds();
    }

    private static float SafeRatio(float target, float current)
    {
        if (Mathf.Abs(current) < 0.000001f)
        {
            return 1f;
        }

        return target / current;
    }

    // ========================================================
    // SELECTION
    // ========================================================

    private static Mesh GetSelectedMesh()
    {
        if (Selection.activeObject is Mesh mesh)
        {
            return mesh;
        }

        GameObject go = Selection.activeGameObject;

        if (go != null)
        {
            MeshFilter filter = go.GetComponent<MeshFilter>();

            if (filter == null)
            {
                filter = go.GetComponentInChildren<MeshFilter>(true);
            }

            if (filter != null)
            {
                return filter.sharedMesh;
            }
        }

        return null;
    }

    private static int GetTriangleCount(Mesh mesh)
    {
        int count = 0;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            count += (int)mesh.GetIndexCount(i) / 3;
        }

        return count;
    }

    // ========================================================
    // TYPES
    // ========================================================

    private sealed class Cluster
    {
        public Vector3 PositionSum;
        public Vector2 UvSum;
        public int Count;

        public readonly List<int> SourceIndices = new List<int>();
    }

    private readonly struct ClusterKey : IEquatable<ClusterKey>
    {
        private readonly int x;
        private readonly int y;
        private readonly int z;

        public ClusterKey(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public bool Equals(ClusterKey other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is ClusterKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = x;
                hash = hash * 397 ^ y;
                hash = hash * 397 ^ z;
                return hash;
            }
        }
    }

    private readonly struct TriangleKey : IEquatable<TriangleKey>
    {
        private readonly int a;
        private readonly int b;
        private readonly int c;

        public TriangleKey(int x, int y, int z)
        {
            // Sort ONLY for duplicate detection.
            // Actual triangle output still uses
            // the original winding.
            if (x > y)
            {
                int temp = x;
                x = y;
                y = temp;
            }

            if (y > z)
            {
                int temp = y;
                y = z;
                z = temp;
            }

            if (x > y)
            {
                int temp = x;
                x = y;
                y = temp;
            }

            a = x;
            b = y;
            c = z;
        }

        public bool Equals(TriangleKey other)
        {
            return a == other.a && b == other.b && c == other.c;
        }

        public override bool Equals(object obj)
        {
            return obj is TriangleKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = a;
                hash = hash * 397 ^ b;
                hash = hash * 397 ^ c;
                return hash;
            }
        }
    }

    private sealed class SimplifyResult
    {
        public List<Vector3> Vertices;
        public List<Vector2> UVs;
        public List<List<int>> SubmeshTriangles;
        public int TriangleCount;
    }
}

#endif
