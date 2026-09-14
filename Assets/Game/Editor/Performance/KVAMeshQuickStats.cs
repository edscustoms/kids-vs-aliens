#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

public static class KVAMeshQuickStats
{
    [MenuItem("Tools/Kids VS Aliens/Performance/Quick Mesh Stats")]
    private static void QuickMeshStats()
    {
        var selected = Selection.gameObjects;

        if (selected == null || selected.Length == 0)
        {
            Debug.LogWarning("Select a GameObject containing a MeshFilter.");
            return;
        }

        foreach (var go in selected)
        {
            var filters = go.GetComponentsInChildren<MeshFilter>(true);

            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;

                if (mesh == null)
                    continue;

                long triangles = 0;

                for (int i = 0; i < mesh.subMeshCount; i++)
                    triangles += (long)mesh.GetIndexCount(i) / 3;

                Debug.Log(
                    $"[KVA MESH] {go.name} / {filter.name} | "
                        + $"Mesh: {mesh.name} | "
                        + $"Tris: {triangles:N0} | "
                        + $"Verts: {mesh.vertexCount:N0} | "
                        + $"SubMeshes: {mesh.subMeshCount}"
                );
            }
        }
    }
}

#endif
