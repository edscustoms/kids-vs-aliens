using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Convenience wrapper only. Lighting authoring is saved in the scene/settings
// asset; rebaking never rewrites geometry, renderer flags, lights or graphics settings.
[InitializeOnLoad]
public static class ConstructionSiteLighting
{
    public const string ScenePath = "Assets/Game/Scenes/ConstructionSite.unity";
    public const string SettingsPath = "Assets/Game/Settings/Lighting/ConstructionSite.lighting";
    const string BusyKey = "KVA.ConstructionSiteLighting.Baking";
    const string FailedKey = "KVA.ConstructionSiteLighting.Failed";
    static ConstructionSiteLighting()
    {
        Lightmapping.bakeCompleted += Completed;
        Lightmapping.bakeCancelled += Cancelled;
        Application.logMessageReceived += ObserveBakeError;
    }
    [MenuItem("Tools/Lighting/Bake Construction Site Lighting")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
        { Debug.LogWarning("Construction Site lighting: exit Play Mode and wait for the current bake before starting."); return; }
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || SceneManager.sceneCount != 1)
        { Debug.LogError("Construction Site lighting: open ConstructionSite by itself before baking."); return; }
        if (scene.isDirty)
        { Debug.LogWarning("Construction Site lighting: save the scene first, then run Bake Construction Site Lighting."); return; }
        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings == null || Lightmapping.lightingSettings != settings)
        { Debug.LogError("Construction Site lighting: the scene must reference " + SettingsPath + ". No settings were changed."); return; }
        SessionState.SetBool(BusyKey, true); SessionState.SetBool(FailedKey, false);
        try
        {
            Debug.Log("Construction Site lighting: bake starting with " + SettingsPath + ".");
            if (!Lightmapping.BakeAsync())
            { SessionState.SetBool(BusyKey, false); Debug.LogError("Construction Site lighting: Unity could not start the bake. See the lighting errors above."); }
        }
        catch (Exception)
        { SessionState.SetBool(BusyKey, false); Debug.LogError("Construction Site lighting: bake failed to start."); throw; }
    }
    static void ObserveBakeError(string message, string stack, LogType type)
    {
        if (SessionState.GetBool(BusyKey, false) && (type == LogType.Error || type == LogType.Exception)) SessionState.SetBool(FailedKey, true);
    }
    static void Completed()
    {
        if (!SessionState.GetBool(BusyKey, false)) return;
        SessionState.SetBool(BusyKey, false);
        if (SessionState.GetBool(FailedKey, false) || Lightmapping.lightingDataAsset == null || LightmapSettings.lightmaps.Length == 0)
        { Debug.LogError("Construction Site lighting: bake finished with errors or missing lightmaps. Review the Console before accepting this bake."); return; }
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath || !EditorSceneManager.SaveScene(scene))
        { Debug.LogError("Construction Site lighting: bake completed, but the ConstructionSite scene references could not be saved. Save the scene before closing Unity."); return; }
        Debug.Log($"Construction Site lighting: bake completed ({LightmapSettings.lightmaps.Length} lightmap atlases); scene lighting references saved.");
    }
    static void Cancelled()
    {
        if (!SessionState.GetBool(BusyKey, false)) return;
        SessionState.SetBool(BusyKey, false);
        Debug.LogWarning("Construction Site lighting: bake cancelled; no successful completion was recorded.");
    }
}
