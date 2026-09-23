using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BeamHoistSurfaceBaker
{
    private static readonly ConcurrentQueue<BeamHoistSurface> pending = new();
    private static readonly ConcurrentQueue<BeamHoistAbility> changedAbilities = new();
    static BeamHoistSurfaceBaker()
    {
        BeamHoistSurface.BakeRequested += surface => pending.Enqueue(surface);
        BeamHoistAbility.BakeSettingsChanged += ability => changedAbilities.Enqueue(ability);
        UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (scene, _) => BakeScene(scene, true);
        EditorApplication.update += Flush;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            foreach (var surface in UnityEngine.Object.FindObjectsByType<BeamHoistSurface>(FindObjectsInactive.Include)) Bake(surface, true);
        };
    }
    private static void Flush()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        while (changedAbilities.TryDequeue(out var ability))
            if (ability != null && ability.gameObject.scene.IsValid()) BakeScene(ability.gameObject.scene);
        while (pending.TryDequeue(out var surface)) if (surface != null) Bake(surface);
    }

    public static void BakeScene(Scene scene, bool force = false)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var surface in root.GetComponentsInChildren<BeamHoistSurface>(true)) Bake(surface, force);
    }

    public static void Bake(BeamHoistSurface surface, bool force = false)
    {
        if (surface == null) return;
        // Scene ability Inspector values are the single source of truth. No prefab/default width copy.
        var abilities = surface.gameObject.scene.IsValid()
            ? surface.gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BeamHoistAbility>(true)).ToArray()
            : Array.Empty<BeamHoistAbility>();
        var ability = abilities.Length == 1 ? abilities[0] : null;
        var transport = ability != null ? ability.GetComponent<BeamTransportController>() : null;
        var capsule = ability != null ? ability.GetComponent<CharacterController>() : null;
        var colliders = (surface.SourceColliders.Length > 0 ? surface.SourceColliders : surface.GetComponentsInChildren<Collider>(true))
            .Where(c => c != null && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && !(c is CharacterController)
                && (c.attachedRigidbody == null || c.attachedRigidbody.isKinematic)
                && c.GetComponentInParent<BeamHoistSurface>() == surface).ToArray();
        var signature = new StringBuilder().Append(surface.transform.localToWorldMatrix.ToString("R"))
            .Append("constraints-v1").Append(surface.LandingInset).Append(surface.Clearance)
            .Append(surface.LandingOverride != null ? surface.LandingOverride.position.ToString("R") : "auto");
        if (ability != null) signature.Append(EditorJsonUtility.ToJson(ability));
        if (transport != null) signature.Append(EditorJsonUtility.ToJson(transport));
        if (capsule != null) signature.Append(EditorJsonUtility.ToJson(capsule)).Append(ability.transform.lossyScale.ToString("R"));
        foreach (var c in colliders) signature.Append(c.transform.localToWorldMatrix.ToString("R")).Append(EditorJsonUtility.ToJson(c));
        string hash = Hash128.Compute(signature.ToString()).ToString();
        if (!force && hash == surface.BakeSignature) return;
        Physics.SyncTransforms();
        Bounds bounds = default;
        bool initialized = false;
        foreach (var collider in colliders)
        {
            Bounds local;
            Transform frame = collider.transform;
            if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
            else if (collider is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
            else { local = collider.bounds; frame = null; }
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = surface.transform.InverseTransformPoint(frame != null ? frame.TransformPoint(corner) : corner);
                if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                else bounds.Encapsulate(point);
            }
        }
        Vector3 scale = surface.transform.lossyScale;
        float sx = Mathf.Abs(scale.x), sy = Mathf.Abs(scale.y), sz = Mathf.Abs(scale.z);
        string error = null;
        if (!initialized) error = "No enabled solid static colliders. Assign collider sources or add collision first.";
        else if (ability == null || transport == null || capsule == null)
            error = "Place in a gameplay scene with one configured player BeamHoistAbility to bake its actual limits.";
        else if (Vector3.Dot(surface.transform.up, Vector3.up) < 0.995f || Mathf.Min(sx, sy, sz) < 0.001f)
            error = "Standard baking supports upright static props. Use an authored BeamHoistTarget for tilted structures.";
        else if (bounds.size.x * sx < 0.7f || bounds.size.z * sz < 0.7f)
            error = "Top is too narrow for the standard player capsule. Use a wider landing surface.";
        var cells = new List<BeamHoistSurface.Approach>();
        if (error == null)
        {
            float insetX = Mathf.Min(surface.LandingInset / sx, bounds.extents.x);
            float insetZ = Mathf.Min(surface.LandingInset / sz, bounds.extents.z);
            for (int side = 0; side < 4; side++)
            {
                bool xSide = side < 2;
                float length = xSide ? bounds.size.z * sz : bounds.size.x * sx;
                int count = Mathf.Clamp(Mathf.CeilToInt(length / 1.5f), 1, 32);
                for (int i = 0; i < count; i++)
                {
                    float fraction = (i + 0.5f) / count;
                    Vector3 landing = new Vector3(
                        xSide ? (side == 0 ? bounds.min.x + insetX : bounds.max.x - insetX) : Mathf.Lerp(bounds.min.x + insetX, bounds.max.x - insetX, fraction),
                        bounds.max.y,
                        xSide ? Mathf.Lerp(bounds.min.z + insetZ, bounds.max.z - insetZ, fraction) : (side == 2 ? bounds.min.z + insetZ : bounds.max.z - insetZ));
                    Vector3 world = surface.LandingOverride != null ? surface.LandingOverride.position : surface.transform.TransformPoint(landing);
                    if (!TryTop(colliders, world, out var top)) continue;
                    landing = surface.transform.InverseTransformPoint(top);
                    float width = ability.MaximumLateralDistance / (xSide ? sx : sz);
                    Vector3 center = new Vector3(
                        xSide ? (side == 0 ? bounds.min.x - width * 0.5f : bounds.max.x + width * 0.5f) : Mathf.Lerp(bounds.min.x, bounds.max.x, fraction),
                        (bounds.min.y - 1f / sy + Mathf.Min(bounds.min.y + 0.9f / sy, landing.y - 0.15f / sy)) * 0.5f,
                        xSide ? Mathf.Lerp(bounds.min.z, bounds.max.z, fraction) : (side == 2 ? bounds.min.z - width * 0.5f : bounds.max.z + width * 0.5f));
                    // Scene props may stand on stacks or raised foundations. Bake the real lower
                    // approach floor, not just a fixed band around the prop pivot/bottom.
                    float lower = bounds.min.y - 1f / sy;
                    float upper = Mathf.Min(bounds.min.y + 0.9f / sy, landing.y - 0.15f / sy);
                    Vector3 probe = surface.transform.TransformPoint(new Vector3(center.x, bounds.min.y, center.z));
                    if (TryApproachFloor(surface, probe, out var floor))
                    {
                        float floorY = surface.transform.InverseTransformPoint(floor).y;
                        lower = floorY - .35f / sy;
                        upper = Mathf.Min(floorY + .9f / sy, landing.y - .15f / sy);
                    }
                    float height = upper - lower;
                    if (height <= 0f) continue;
                    center.y = (lower + upper) * .5f;
                    Vector3 size = new Vector3(xSide ? width : bounds.size.x / count, height, xSide ? bounds.size.z / count : width);
                    AddValidCells(surface, ability, transport, bounds,
                        new BeamHoistSurface.Approach { side = side, landing = landing, region = new Bounds(center, size) }, cells);
                }
            }
        }
        surface.StoreBake(bounds, cells.ToArray(), hash, error ?? $"Baked {cells.Count} valid lower-side cells from player ability limits and static clearance; top excluded.");
        EditorUtility.SetDirty(surface);
        PrefabUtility.RecordPrefabInstancePropertyModifications(surface);
    }

    // Editor-only sampling of the existing authoritative queries. Store accepted cells once;
    // neither gameplay nor presentation discovers a different footprint as the player walks.
    private static void AddValidCells(BeamHoistSurface surface, BeamHoistAbility ability,
        BeamTransportController transport, Bounds footprint, BeamHoistSurface.Approach approach,
        List<BeamHoistSurface.Approach> cells)
    {
        bool xSide = approach.side < 2;
        bool negative = approach.side == 0 || approach.side == 2;
        float scale = Mathf.Abs(xSide ? surface.transform.lossyScale.x : surface.transform.lossyScale.z);
        float edge = xSide ? (negative ? footprint.min.x : footprint.max.x) : (negative ? footprint.min.z : footprint.max.z);
        float width = xSide ? approach.region.size.x : approach.region.size.z;
        // Account for landing inset AND tangential distance at both ends of the cell.
        float tangentScale = Mathf.Abs(xSide ? surface.transform.lossyScale.z : surface.transform.lossyScale.x);
        float landingTangent = xSide ? approach.landing.z : approach.landing.x;
        float tangentMin = xSide ? approach.region.min.z : approach.region.min.x;
        float tangentMax = xSide ? approach.region.max.z : approach.region.max.x;
        float tangent = Mathf.Max(Mathf.Abs(tangentMin - landingTangent), Mathf.Abs(tangentMax - landingTangent)) * tangentScale;
        float inset = Mathf.Abs((xSide ? approach.landing.x : approach.landing.z) - edge) * scale;
        float reach = ability.MaximumLateralDistance;
        if (tangent >= reach) return;
        width = Mathf.Min(width, (Mathf.Sqrt(reach * reach - tangent * tangent) - inset) / scale);
        if (width <= 0) return;
        // Quantized inward, so reach never rounds outward into an invalid adjacent strip.
        const float stepMeters = .25f;
        float step = stepMeters / scale;
        int count = Mathf.FloorToInt(width / step);
        for (int n = 0; n < count; n++)
        {
            var region = approach.region;
            var size = region.size; var center = region.center;
            float normal = edge + (negative ? -1 : 1) * (n + .5f) * step;
            if (xSide) { size.x = step; center.x = normal; }
            else { size.z = step; center.z = normal; }
            region = new Bounds(center, size);
            bool valid = true;
            float lowest = float.PositiveInfinity, highest = float.NegativeInfinity;
            for (int x = 0; x < 3 && valid; x++)
                for (int z = 0; z < 3 && valid; z++)
                {
                    Vector3 sample = new Vector3(Mathf.Lerp(region.min.x, region.max.x, x * .5f), footprint.min.y,
                        Mathf.Lerp(region.min.z, region.max.z, z * .5f));
                    if (!TryApproachFloor(surface, surface.transform.TransformPoint(sample), out var floor)) { valid = false; break; }
                    float y = surface.transform.InverseTransformPoint(floor).y;
                    lowest = Mathf.Min(lowest, y); highest = Mathf.Max(highest, y);
                    Vector3 start = floor + Vector3.up * (.02f - transport.FeetOffset);
                    Vector3 landing = surface.transform.TransformPoint(approach.landing) + Vector3.up * (.02f - transport.FeetOffset);
                    float release = Mathf.Max(landing.y - .02f, surface.transform.TransformPoint(new Vector3(0, footprint.max.y, 0)).y - transport.FeetOffset)
                        + surface.Clearance;
                    var path = ability.CreateSurfaceHoist(start, landing, release);
                    valid = ability.IsWithinLimits(path) && transport.IsLandingSafe(start) && transport.IsHoistRouteClear(path);
                }
            if (!valid) continue;
            float sy = Mathf.Abs(surface.transform.lossyScale.y);
            float lower = lowest - .35f / sy;
            float upper = Mathf.Min(highest + .9f / sy, approach.landing.y - .15f / sy);
            center.y = (lower + upper) * .5f; size.y = upper - lower;
            if (size.y <= 0) continue;
            cells.Add(new BeamHoistSurface.Approach { side = approach.side, landing = approach.landing, region = new Bounds(center, size) });
        }
    }

    private static bool TryApproachFloor(BeamHoistSurface surface, Vector3 point, out Vector3 floor)
    {
        floor = default;
        if (!surface.gameObject.scene.IsValid()) return false; // Prefab assets use geometry-relative defaults.
        float nearest = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * .9f, Vector3.down, 32f, ~0, QueryTriggerInteraction.Ignore))
        {
            var c = hit.collider;
            if (c.gameObject.scene != surface.gameObject.scene || c.transform.IsChildOf(surface.transform)
                || c is CharacterController || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic)
                || c.GetComponentInParent<PlayerCharacter>() != null || hit.normal.y < .7f || hit.distance >= nearest) continue;
            nearest = hit.distance; floor = hit.point;
        }
        return !float.IsPositiveInfinity(nearest);
    }

    private static bool TryTop(Collider[] colliders, Vector3 point, out Vector3 result)
    {
        result = default;
        bool found = false;
        float best = float.NegativeInfinity;
        var ray = new Ray(point + Vector3.up * 0.25f, Vector3.down);
        foreach (var collider in colliders)
        {
            if (!collider.Raycast(ray, out var hit, 0.55f) || hit.normal.y < 0.7f || hit.point.y <= best) continue;
            found = true; best = hit.point.y; result = hit.point;
        }
        return found;
    }
}

[CustomEditor(typeof(BeamHoistSurface))]
public sealed class BeamHoistSurfaceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var surface = (BeamHoistSurface)target;
        if (!Application.isPlaying) BeamHoistSurfaceBaker.Bake(surface);
        EditorGUILayout.HelpBox(surface.BakeStatus, surface.IsBaked ? MessageType.Info : MessageType.Warning);
        if (GUILayout.Button("Refresh Cached Hoist Geometry") && !Application.isPlaying) BeamHoistSurfaceBaker.Bake(surface, true);
    }
    private void OnSceneGUI() { if (!Application.isPlaying) BeamHoistSurfaceBaker.Bake((BeamHoistSurface)target); }
}

public sealed class BeamHoistSurfaceBuildBake : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var surface in root.GetComponentsInChildren<BeamHoistSurface>(true)) BeamHoistSurfaceBaker.Bake(surface, true);
    }
}
