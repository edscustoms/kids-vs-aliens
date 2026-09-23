using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum BeamHoistZoneState { Hidden, Available, Active }

// Draws only the supplied baked-cell footprint. No gameplay, input or collision ownership.
[DisallowMultipleComponent]
public sealed class BeamHoistZoneVFX : MonoBehaviour
{
    public struct Patch
    {
        public Bounds bounds;
        public Vector3 a, b, c, d;
    }
    [SerializeField] private MeshFilter ground;
    [SerializeField] private MeshRenderer groundRenderer;
    [SerializeField] private ParticleSystem energyMotes;
    [Header("Visual Strength")]
    [SerializeField, Min(0)] private float idleIntensity = .6f;
    [SerializeField, Min(0)] private float activeIntensity = 1f;
    [SerializeField, Min(0)] private float borderIntensity = 2.2f;
    [SerializeField, Min(0)] private float glyphIntensity = 1.7f;
    [SerializeField, Min(0)] private float coreIntensity = 2.8f;
    [SerializeField, Range(0, 1)] private float groundFillOpacity = .13f;
    [SerializeField, Min(0)] private float cyanAccentIntensity = 2.5f;
    [Header("Motion")]
    [SerializeField, Min(0)] private float pulseSpeed = 1.4f;
    [SerializeField, Range(0, 1)] private float idlePulseAmount = .12f;
    [SerializeField, Range(0, 1)] private float activePulseAmount = .22f;
    [SerializeField, Min(0)] private float perimeterAnimationSpeed = .6f;
    [Tooltip("Motes per second per visible square meter, capped at twelve per second per surface.")]
    [SerializeField, Min(0)] private float moteDensity = .7f;
    private readonly List<Vector3> vertices = new();
    private readonly List<Vector2> uvs = new(), sizes = new();
    private readonly List<int> triangles = new();
    private readonly List<Patch> patches = new();
    private readonly List<Vector4> coreData = new(), outlineUVs = new();
    private Texture2D outlineField;
    private int componentCount;
    private readonly System.Random random = new(1979);
    private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
    private Mesh mesh;
    private MaterialPropertyBlock properties;
    private float opacity, moteClock, area;
    public BeamHoistZoneState State { get; private set; }
    public float Opacity => opacity;
    public int PatchCount => componentCount;
    private static readonly int Strength = Shader.PropertyToID("_Strength"), Detail = Shader.PropertyToID("_Detail"),
        Motion = Shader.PropertyToID("_Motion"), Fill = Shader.PropertyToID("_Fill");

    public void SetFootprint(IReadOnlyList<Patch> cells)
    {
        patches.Clear();
        for (int i = 0; i < cells.Count; i++) patches.Add(cells[i]);
        Vector2 scale = new Vector2(transform.TransformVector(Vector3.right).magnitude, transform.TransformVector(Vector3.forward).magnitude);
        var groups = BeamHoistZoneTopology.Build(patches, scale);
        componentCount = groups.Count;
        var groupAt = new int[patches.Count];
        var fieldUV = BuildOutlineField(groups);
        for (int g = 0; g < groups.Count; g++) foreach (int cell in groups[g].cells) groupAt[cell] = g;
        vertices.Clear(); uvs.Clear(); sizes.Clear(); triangles.Clear(); coreData.Clear(); outlineUVs.Clear(); area = 0;
        for (int i = 0; i < patches.Count; i++)
        {
            var patch = patches[i]; var group = groups[groupAt[i]]; Rect region = group.bounds;
            int n = vertices.Count;
            vertices.Add(patch.a); vertices.Add(patch.b); vertices.Add(patch.c); vertices.Add(patch.d);
            Vector2 UV(Vector3 point) => new Vector2((point.x * scale.x - region.xMin) / region.width,
                (point.z * scale.y - region.yMin) / region.height);
            uvs.Add(UV(patch.a)); uvs.Add(UV(patch.b)); uvs.Add(UV(patch.c)); uvs.Add(UV(patch.d));
            Vector2 core = group.core - region.center;
            for (int v = 0; v < 4; v++)
            {
                sizes.Add(region.size);
                coreData.Add(new Vector4(core.x, core.y, group.radius, group.rectangular ? 0 : 1));
                outlineUVs.Add(fieldUV[groupAt[i]]);
            }
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1);
            triangles.Add(n); triangles.Add(n + 3); triangles.Add(n + 2);
            area += transform.TransformVector(patch.b - patch.a).magnitude * transform.TransformVector(patch.d - patch.a).magnitude;
        }
        if (mesh == null) { mesh = new Mesh { name = "Baked hoist cell presentation" }; ground.sharedMesh = mesh; }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetUVs(1, sizes);
        mesh.SetUVs(2, coreData); mesh.SetUVs(3, outlineUVs); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
    }

    // Irregular unions use one cached distance-field atlas, not per-fragment loops over cells.
    // Rectangles keep the original analytic shader path and need no texture allocation.
    private Vector4[] BuildOutlineField(List<BeamHoistZoneTopology.Group> groups)
    {
        if (outlineField != null) Release(outlineField);
        outlineField = null;
        properties?.SetTexture("_OutlineField", Texture2D.whiteTexture);
        var result = new Vector4[groups.Count];
        int count = 0; foreach (var group in groups) if (!group.rectangular) count++;
        if (count == 0) return result;
        const int tile = 256;
        int columns = Mathf.CeilToInt(Mathf.Sqrt(count)), rows = Mathf.CeilToInt((float)count / columns);
        outlineField = new Texture2D(columns * tile, rows * tile, TextureFormat.RGBAHalf, false, true)
            { name = "Fixed hoist union outlines", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[tile * tile]; int index = 0;
        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g]; if (group.rectangular) continue;
            int left = index % columns * tile, bottom = index / columns * tile; index++;
            for (int y = 0; y < tile; y++)
                for (int x = 0; x < tile; x++)
                {
                    Vector2 point = group.bounds.min + Vector2.Scale(group.bounds.size,
                        new Vector2((x - .5f) / (tile - 2), (y - .5f) / (tile - 2)));
                    Color field = group.Field(point);
                    pixels[y * tile + x] = field;
                }
            outlineField.SetPixels(left, bottom, tile, tile, pixels);
            result[g] = new Vector4((float)(tile - 2) / outlineField.width, (float)(tile - 2) / outlineField.height,
                (float)(left + 1) / outlineField.width, (float)(bottom + 1) / outlineField.height);
        }
        outlineField.Apply(false, true);
        properties ??= new MaterialPropertyBlock(); properties.SetTexture("_OutlineField", outlineField);
        return result;
    }

    public void Present(BeamHoistZoneState state, float deltaTime, float revealDuration, float hideDuration, bool immediate = false)
    {
        State = state;
        float target = state == BeamHoistZoneState.Hidden ? 0 : 1;
        float duration = target > opacity ? revealDuration : hideDuration;
        opacity = immediate || duration <= 0 ? target : Mathf.MoveTowards(opacity, target, deltaTime / duration);
        bool active = state == BeamHoistZoneState.Active;
        properties ??= new MaterialPropertyBlock();
        properties.SetFloat(Strength, opacity * (active ? activeIntensity : idleIntensity));
        properties.SetVector(Detail, new Vector4(borderIntensity, glyphIntensity, coreIntensity, cyanAccentIntensity));
        properties.SetVector(Motion, new Vector4(pulseSpeed * (active ? 1.7f : 1f), active ? activePulseAmount : idlePulseAmount, perimeterAnimationSpeed, active ? 1 : 0));
        properties.SetFloat(Fill, groundFillOpacity);
        groundRenderer.SetPropertyBlock(properties);
        groundRenderer.enabled = opacity > .001f && patches.Count > 0;
        if (energyMotes == null) return;
        if (state == BeamHoistZoneState.Hidden)
        {
            moteClock = 0;
            energyMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return;
        }
        if (!energyMotes.isPlaying) energyMotes.Play();
        float rate = Mathf.Min(12, area * moteDensity) * opacity * (active ? 1f : .6f);
        moteClock += deltaTime * rate;
        int emit = Mathf.Min(4, (int)moteClock); moteClock -= emit;
        for (int i = 0; i < emit && patches.Count > 0; i++)
        {
            var patch = patches[random.Next(patches.Count)];
            Vector3 point = Vector3.Lerp(Vector3.Lerp(patch.a, patch.b, (float)random.NextDouble()), Vector3.Lerp(patch.d, patch.c, (float)random.NextDouble()), (float)random.NextDouble());
            var particle = new ParticleSystem.EmitParams { position = transform.TransformPoint(point), velocity = Vector3.up * Range(.2f, .45f),
                startSize = Range(.018f, .035f), startColor = new Color(.65f, .15f, 1f, opacity * (active ? 1f : .6f)) };
            energyMotes.Emit(particle, 1);
        }
    }
    private void OnDisable()
    {
        opacity = 0; State = BeamHoistZoneState.Hidden;
        if (groundRenderer != null) groundRenderer.enabled = false;
        if (energyMotes != null) energyMotes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    private static void Release(Object owned) { if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned); }
    private void OnDestroy() { if (mesh != null) Release(mesh); if (outlineField != null) Release(outlineField); }
}
