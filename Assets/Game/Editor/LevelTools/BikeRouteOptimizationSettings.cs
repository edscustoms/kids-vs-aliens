using UnityEngine;

// Editor-only, scene-specific choices. No runtime quality or gameplay owner.
[CreateAssetMenu(menuName = "Level Authoring/BikeRoute Optimization Settings")]
public sealed class BikeRouteOptimizationSettings : ScriptableObject
{
    [Range(1, 10)] public float terrainPixelError = 2;
    [Tooltip("BikeRoute-only asset, after receivers, UVs and probes pass the bake audit.")]
    public LightingSettings lightingSettings;
    [Min(1)] public int lightmapMemoryBudgetMiB = 64;
}
