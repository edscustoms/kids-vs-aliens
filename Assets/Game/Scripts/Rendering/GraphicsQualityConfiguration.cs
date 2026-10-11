using System;
using UnityEngine;

public enum GraphicsPostQuality { Original, NoBloom, Disabled }

[CreateAssetMenu(menuName = "Rendering/Graphics Quality Configuration")]
public sealed class GraphicsQualityConfiguration : ScriptableObject
{
    [Serializable]
    public sealed class Preset
    {
        [Range(.25f, 1)] public float renderScaleMultiplier = 1;
        [Min(1)] public float terrainPixelErrorMultiplier = 1;
        [Range(.1f, 1)] public float lodBiasMultiplier = 1;
        [Range(.1f, 1)] public float vegetationDistanceMultiplier = 1;
        [Tooltip("Zero preserves each camera's authored far clip. Otherwise caps it, never extends it.")]
        [Min(0)] public float farClipLimit;
        [Tooltip("Optional visual-only environment layers. Never include gameplay layers without reviewing visibility.")]
        public LayerMask distanceLimitedLayers;
        [Min(1)] public float objectDrawDistance = 250;
        [Range(.1f, 1)] public float shadowDistanceMultiplier = 1;
        public GraphicsPostQuality postProcessing;
    }
    [Serializable]
    public sealed class TerrainMaterials
    {
        public Material original;
        [Tooltip("Unassigned tiers preserve the original. Use only Terrain-compatible materials.")]
        public Material low, medium;
    }
    public Preset low = new Preset { renderScaleMultiplier = .75f, terrainPixelErrorMultiplier = 4,
        lodBiasMultiplier = .5f, vegetationDistanceMultiplier = .5f, farClipLimit = 250,
        shadowDistanceMultiplier = .6f, postProcessing = GraphicsPostQuality.Disabled };
    public Preset medium = new Preset { renderScaleMultiplier = .875f, terrainPixelErrorMultiplier = 2,
        lodBiasMultiplier = .75f, vegetationDistanceMultiplier = .75f, farClipLimit = 375,
        shadowDistanceMultiplier = .8f, postProcessing = GraphicsPostQuality.NoBloom };
    // High is deliberately an identity transform of authored/platform settings.
    public Preset high = new Preset();
    public TerrainMaterials[] terrainMaterials = Array.Empty<TerrainMaterials>();
    public Preset Get(GraphicsQuality quality) => quality == GraphicsQuality.Low ? low : quality == GraphicsQuality.Medium ? medium : high;
    public Material GetTerrainMaterial(Material original, GraphicsQuality quality)
    {
        if (quality == GraphicsQuality.High) return original;
        foreach (var slot in terrainMaterials)
            if (slot != null && slot.original == original)
            {
                var replacement = quality == GraphicsQuality.Low ? slot.low : slot.medium;
                return replacement != null && replacement.shader != null && replacement.shader.isSupported ? replacement : original;
            }
        return original;
    }
}
