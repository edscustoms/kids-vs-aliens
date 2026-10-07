using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Repairs only the existing crossing deck; its drivable top positions stay fixed.</summary>
public static class BikeRouteBridgeSurfaceAuthoring
{
    const string MeshPath = "Assets/Game/Scenes/BikeRoute/Meshes/Crossing shoulder deck.asset";

    [MenuItem("Tools/Level Authoring/Repair BikeRoute Bridge Surface")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before repairing the bridge asset.");
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        var oldVertices = mesh.vertices;
        var oldNormals = mesh.normals;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        var remap = new Dictionary<int, int>();
        var edges = new Dictionary<(Vector3, Vector3), (int a, int b, int count)>();
        var original = mesh.triangles;
        // Extract the original upward-facing deck on repeated repair, excluding the
        // generated soffit and sides. Never reconstruct the driving surface from a spline.
        for (int i = 0; i < original.Length; i += 3)
        {
            int a = original[i], b = original[i+1], c = original[i+2];
            if (Vector3.Cross(oldVertices[b]-oldVertices[a], oldVertices[c]-oldVertices[a]).normalized.y < .5f) continue;
            int Map(int index)
            {
                if (remap.TryGetValue(index, out int mapped)) return mapped;
                mapped = vertices.Count;
                remap.Add(index, mapped);
                vertices.Add(oldVertices[index]);
                normals.Add(oldNormals[index]);
                uv.Add(new Vector2(oldVertices[index].x, oldVertices[index].z) * .25f);
                return mapped;
            }
            a = Map(a); b = Map(b); c = Map(c);
            triangles.AddRange(new[] { a, b, c });
            Edge(a,b); Edge(b,c); Edge(c,a);
        }
        if (triangles.Count == 0) throw new InvalidOperationException("Bridge has no upward-facing deck.");
        int topCount = vertices.Count;
        int topIndices = triangles.Count;
        for (int i = 0; i < topCount; i++)
        {
            vertices.Add(vertices[i] - Vector3.up * .6f);
            normals.Add(-normals[i]);
            uv.Add(uv[i]);
        }
        for (int i = 0; i < topIndices; i += 3)
            triangles.AddRange(new[] { triangles[i]+topCount, triangles[i+2]+topCount, triangles[i+1]+topCount });
        foreach (var edge in edges.Values.Where(e => e.count == 1))
        {
            Vector3 a = vertices[edge.a], b = vertices[edge.b];
            int start = vertices.Count;
            vertices.AddRange(new[] { b, a, a-Vector3.up*.6f, b-Vector3.up*.6f });
            Vector3 normal = Vector3.Cross(a-b, Vector3.down).normalized;
            normals.AddRange(Enumerable.Repeat(normal,4));
            float length = Vector3.Distance(a,b)*.25f;
            uv.AddRange(new[] { new Vector2(length,0), Vector2.zero, new Vector2(0,-.15f), new Vector2(length,-.15f) });
            triangles.AddRange(new[] {start,start+1,start+2,start,start+2,start+3});
        }
        mesh.Clear();
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
        mesh.RecalculateTangents(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh);

        void Edge(int a, int b)
        {
            var key = (vertices[a], vertices[b]);
            var reverse = (vertices[b], vertices[a]);
            if (edges.TryGetValue(reverse,out var previous)) edges[reverse] = (previous.a,previous.b,previous.count+1);
            else if (edges.TryGetValue(key,out previous)) edges[key] = (previous.a,previous.b,previous.count+1);
            else edges.Add(key,(a,b,1));
        }
    }
}
