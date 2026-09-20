using System.Linq;
using UnityEditor;
using UnityEngine.Rendering.Universal;

public static class CameraOcclusionSilhouetteSetup
{
    // Standard rendering dependency, installed once per production renderer (not per prop).
    public static void Ensure()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets/Game/Settings/Rendering" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            // Knowledge's dedicated lightweight renderer does not render gameplay occlusion.
            if (path == GameplayPresentationSetup.LightweightRendererPath || data.name.Contains("Preview")) continue;
            if (!data.rendererFeatures.Any(f => f is CameraOcclusionSilhouetteFeature))
            {
                var feature = UnityEngine.ScriptableObject.CreateInstance<CameraOcclusionSilhouetteFeature>();
                feature.name = "Camera Occlusion Silhouettes";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                EditorUtility.SetDirty(data);
                data.SetDirty();
            }
            var serialized = new SerializedObject(data);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = data.rendererFeatures.Count;
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string _, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
    }
}
