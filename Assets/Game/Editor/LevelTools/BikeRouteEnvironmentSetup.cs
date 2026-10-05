using System;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit BikeRoute environment authoring. Never runs during scene repair.</summary>
public static partial class BikeRouteEnvironmentSetup
{
    public static async void ImportMaterialsBatch()
    {
        var importer = ScriptableObject.CreateInstance<EditorTools.PolyHavenMaterialImporter>();
        try
        {
            foreach (var item in new[] {
                ("quarry_wall_02", "Wall"), ("dark_rock", "Wall"),
                ("forrest_ground_01", "Ground"), ("forest_floor", "Ground"),
                ("sandstone_blocks_05", "Wall"), ("sandstone_blocks_08", "Wall"),
                ("asphalt_01", "Ground") })
                await importer.ImportRecommendedAsync("https://polyhaven.com/a/" + item.Item1, item.Item2, "2k");
            UnityEngine.Object.DestroyImmediate(importer);
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); UnityEngine.Object.DestroyImmediate(importer); EditorApplication.Exit(1); }
    }
}
