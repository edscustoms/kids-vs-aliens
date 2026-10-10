using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Explicit scene targets, wired by gameplay repair. Owns reversible instance overrides only.</summary>
[DisallowMultipleComponent]
public sealed class SceneGraphicsSettings : MonoBehaviour
{
    [SerializeField] private Terrain[] terrains = Array.Empty<Terrain>();
    [SerializeField] private Volume[] volumes = Array.Empty<Volume>();
    private readonly List<TerrainState> terrainStates = new();
    private readonly List<VolumeState> volumeStates = new();
    private sealed class TerrainState
    {
        public Terrain terrain;
        public Material material;
        public float error, trees, details;
    }
    private sealed class VolumeState
    {
        public Volume volume;
        public VolumeProfile original, working;
    }
    public void Configure(Terrain[] terrainTargets, Volume[] volumeTargets) { terrains = terrainTargets; volumes = volumeTargets; }
    private void OnEnable()
    {
        if (!Application.isPlaying) return;
        GraphicsSettingsController.Ensure();
        foreach (var terrain in terrains)
            if (terrain != null) terrainStates.Add(new TerrainState { terrain = terrain, material = terrain.materialTemplate,
                error = terrain.heightmapPixelError, trees = terrain.treeDistance, details = terrain.detailObjectDistance });
        foreach (var volume in volumes)
            if (volume != null) volumeStates.Add(new VolumeState { volume = volume, original = volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile });
        GraphicsPreferences.Changed += Apply;
        Apply();
    }
    public void Apply()
    {
        var config = GraphicsSettingsController.Instance?.Configuration;
        if (config == null) return;
        var quality = GraphicsPreferences.Quality;
        var preset = config.Get(quality);
        foreach (var s in terrainStates)
        {
            if (s.terrain == null) continue;
            s.terrain.heightmapPixelError = s.error * preset.terrainPixelErrorMultiplier;
            s.terrain.treeDistance = s.trees * preset.vegetationDistanceMultiplier;
            s.terrain.detailObjectDistance = s.details * preset.vegetationDistanceMultiplier;
            s.terrain.materialTemplate = config.GetTerrainMaterial(s.material, quality);
        }
        foreach (var s in volumeStates)
        {
            if (s.volume == null || s.original == null) continue;
            if (preset.postProcessing != GraphicsPostQuality.NoBloom) { s.volume.profile = s.original; continue; }
            if (s.working == null)
            {
                s.working = ScriptableObject.CreateInstance<VolumeProfile>();
                s.working.hideFlags = HideFlags.DontSave;
                foreach (var component in s.original.components) s.working.components.Add(Instantiate(component));
                if (s.working.TryGet<Bloom>(out var bloom)) bloom.active = false;
            }
            s.volume.profile = s.working;
        }
    }
    private void OnDisable()
    {
        GraphicsPreferences.Changed -= Apply;
        foreach (var s in terrainStates)
            if (s.terrain != null)
            {
                s.terrain.materialTemplate = s.material; s.terrain.heightmapPixelError = s.error;
                s.terrain.treeDistance = s.trees; s.terrain.detailObjectDistance = s.details;
            }
        foreach (var s in volumeStates)
        {
            if (s.volume != null) s.volume.profile = s.original;
            if (s.working == null) continue;
            foreach (var component in s.working.components) Destroy(component);
            Destroy(s.working);
        }
        terrainStates.Clear(); volumeStates.Clear();
    }
}
