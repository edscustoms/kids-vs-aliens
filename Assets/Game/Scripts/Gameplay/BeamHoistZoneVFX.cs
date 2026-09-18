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
    private readonly List<Patch> regions = new();
    private readonly System.Random random = new(1979);
    private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
    private Mesh mesh;
    private MaterialPropertyBlock properties;
    private float opacity, moteClock, area;
    public BeamHoistZoneState State { get; private set; }
    public float Opacity => opacity;
    public int PatchCount => regions.Count;
    private static readonly int Strength = Shader.PropertyToID("_Strength"), Detail = Shader.PropertyToID("_Detail"),
        Motion = Shader.PropertyToID("_Motion"), Fill = Shader.PropertyToID("_Fill");

    public void SetFootprint(IReadOnlyList<Patch> cells)
    {
        patches.Clear();
        for (int i = 0; i < cells.Count; i++) patches.Add(cells[i]);
        // Consolidate the fixed footprint for glyph UVs, while retaining each ground-aligned cell.
        // Height variation must not split a contiguous pad into repeated borders/centers.
        regions.Clear(); regions.AddRange(patches);
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < regions.Count && !changed; i++)
                for (int j = i + 1; j < regions.Count; j++)
                    if (TryMerge(regions[i], regions[j], out var merged))
                    { regions[i] = merged; regions.RemoveAt(j); changed = true; break; }
        } while (changed);
        vertices.Clear(); uvs.Clear(); sizes.Clear(); triangles.Clear(); area = 0;
        foreach (var patch in patches)
        {
            int n = vertices.Count;
            vertices.Add(patch.a); vertices.Add(patch.b); vertices.Add(patch.c); vertices.Add(patch.d);
            Bounds region = patch.bounds;
            foreach (var candidate in regions)
                if (patch.bounds.min.x >= candidate.bounds.min.x - .001f && patch.bounds.max.x <= candidate.bounds.max.x + .001f
                    && patch.bounds.min.z >= candidate.bounds.min.z - .001f && patch.bounds.max.z <= candidate.bounds.max.z + .001f)
                { region = candidate.bounds; break; }
            Vector2 UV(Vector3 point) => new Vector2((point.x - region.min.x) / region.size.x, (point.z - region.min.z) / region.size.z);
            uvs.Add(UV(patch.a)); uvs.Add(UV(patch.b)); uvs.Add(UV(patch.c)); uvs.Add(UV(patch.d));
            var size = new Vector2(transform.TransformVector(Vector3.right * region.size.x).magnitude,
                transform.TransformVector(Vector3.forward * region.size.z).magnitude);
            for (int v = 0; v < 4; v++) sizes.Add(size);
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1);
            triangles.Add(n); triangles.Add(n + 3); triangles.Add(n + 2);
            area += transform.TransformVector(patch.b - patch.a).magnitude * transform.TransformVector(patch.d - patch.a).magnitude;
        }
        if (mesh == null) { mesh = new Mesh { name = "Baked hoist cell presentation" }; ground.sharedMesh = mesh; }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetUVs(1, sizes); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
    }

    private static bool TryMerge(Patch x, Patch y, out Patch merged)
    {
        merged = default;
        bool alongX = Mathf.Abs(x.bounds.min.z - y.bounds.min.z) < .001f && Mathf.Abs(x.bounds.max.z - y.bounds.max.z) < .001f
            && (Mathf.Abs(x.bounds.max.x - y.bounds.min.x) < .001f || Mathf.Abs(y.bounds.max.x - x.bounds.min.x) < .001f);
        bool alongZ = Mathf.Abs(x.bounds.min.x - y.bounds.min.x) < .001f && Mathf.Abs(x.bounds.max.x - y.bounds.max.x) < .001f
            && (Mathf.Abs(x.bounds.max.z - y.bounds.min.z) < .001f || Mathf.Abs(y.bounds.max.z - x.bounds.min.z) < .001f);
        float max = Mathf.Max(Mathf.Max(x.a.y, x.b.y), Mathf.Max(x.c.y, x.d.y));
        max = Mathf.Max(max, Mathf.Max(Mathf.Max(y.a.y, y.b.y), Mathf.Max(y.c.y, y.d.y)));
        if (!alongX && !alongZ) return false;
        Bounds b = x.bounds; b.Encapsulate(y.bounds);
        merged = new Patch { bounds = b, a = new Vector3(b.min.x, max, b.min.z), b = new Vector3(b.max.x, max, b.min.z),
            c = new Vector3(b.max.x, max, b.max.z), d = new Vector3(b.min.x, max, b.max.z) };
        return true;
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
    private void OnDestroy() { if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }
}
