using UnityEditor;
using UnityEngine;

public static class BreakableTargetColliderBaker
{
    [MenuItem("Tools/Kids VS Aliens/Bake Selected Target Colliders")]
    private static void BakeSelectedTargetColliders()
    {
        Object[] selectedObjects = Selection.objects;

        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning(
                "Select one or more Practice Target prefabs in the Project window first."
            );

            return;
        }

        int processedPrefabs = 0;
        int createdColliders = 0;
        int updatedColliders = 0;

        foreach (Object selectedObject in selectedObjects)
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedObject);

            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab"))
            {
                continue;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

            if (prefabRoot == null)
                continue;

            Transform piecesRoot = FindBreakablePieces(prefabRoot.transform);

            if (piecesRoot == null)
            {
                Debug.LogWarning($"{prefabRoot.name}: Could not find BreakablePieces.");

                PrefabUtility.UnloadPrefabContents(prefabRoot);

                continue;
            }

            int prefabCreated = 0;
            int prefabUpdated = 0;

            for (int i = 0; i < piecesRoot.childCount; i++)
            {
                Transform piece = piecesRoot.GetChild(i);

                MeshFilter meshFilter = piece.GetComponent<MeshFilter>();

                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    Debug.LogWarning(
                        $"{prefabRoot.name}/{piece.name}: " + "No MeshFilter/sharedMesh found."
                    );

                    continue;
                }

                MeshCollider meshCollider = piece.GetComponent<MeshCollider>();

                if (meshCollider == null)
                {
                    meshCollider = piece.gameObject.AddComponent<MeshCollider>();

                    prefabCreated++;
                }
                else
                {
                    prefabUpdated++;
                }

                meshCollider.sharedMesh = meshFilter.sharedMesh;

                meshCollider.convex = true;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);

            PrefabUtility.UnloadPrefabContents(prefabRoot);

            processedPrefabs++;

            createdColliders += prefabCreated;

            updatedColliders += prefabUpdated;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Transform FindBreakablePieces(Transform root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in transforms)
        {
            if (child.name == "BreakablePieces")
            {
                return child;
            }
        }

        return null;
    }

    [MenuItem("Tools/Kids VS Aliens/Bake Selected Target Colliders", true)]
    private static bool ValidateBakeSelectedTargetColliders()
    {
        return Selection.objects != null && Selection.objects.Length > 0;
    }
}
