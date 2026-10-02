using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AlienFlybyValidation
{
    public static bool Validate(AlienFlybyPath path, Transform[] environmentRoots, out string failure)
    {
        failure = null;
        if (path.start == null || path.control1 == null || path.control2 == null || path.end == null
            || environmentRoots == null || environmentRoots.Length == 0 || environmentRoots.Any(root => root == null))
        {
            path.SetValidation(false, null);
            failure = "Missing control points or explicit environment roots";
            return false;
        }
        var obstacles = new List<(Bounds bounds, string name)>();
        var physical = new HashSet<Collider>();
        var overlaps = new Collider[256];
        foreach (var root in environmentRoots.Where(r => r != null))
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                    obstacles.Add((r.bounds, r.name));
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger && c.enabled && c.gameObject.activeInHierarchy)
                    physical.Add(c);
        }
        Physics.SyncTransforms();
        // Control-polygon length is an upper bound on curve length. <= .25 m steps,
        // with a .125 m guard around each sample, cover the continuous swept sphere.
        float polygon = Vector3.Distance(path.start.position, path.control1.position)
            + Vector3.Distance(path.control1.position, path.control2.position) + Vector3.Distance(path.control2.position, path.end.position);
        int steps = Mathf.Max(16, Mathf.CeilToInt(polygon * 12)); // |B'| <= 3 * polygon
        float radius = path.clearanceRadius + .125f;
        for (int i = 0; i <= steps; i++)
        {
            Vector3 p = path.Evaluate((float)i / steps);
            int count = Physics.OverlapSphereNonAlloc(p, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length)
            {
                path.SetValidation(false, null);
                failure = path.name + ": clearance query saturated";
                return false;
            }
            for (int hit = 0; hit < count; hit++)
                if (physical.Contains(overlaps[hit]))
                {
                    path.SetValidation(false, null);
                    failure = $"{path.name}: clearance intersects {overlaps[hit].name} at {p}";
                    return false;
                }
            foreach (var obstacle in obstacles)
                if ((obstacle.bounds.ClosestPoint(p) - p).sqrMagnitude <= radius * radius)
                {
                    path.SetValidation(false, null);
                    EditorUtility.SetDirty(path);
                    failure = $"{path.name}: clearance intersects {obstacle.name} at {p}";
                    return false;
                }
        }
        // Runtime needs only a compact arc-length lookup; validation above stays dense.
        var distances = new float[65];
        Vector3 previous = path.Evaluate(0);
        for (int i = 1; i < distances.Length; i++)
        {
            Vector3 p = path.Evaluate((float)i / (distances.Length - 1));
            distances[i] = distances[i - 1] + Vector3.Distance(previous, p);
            previous = p;
        }
        path.SetValidation(true, distances);
        EditorUtility.SetDirty(path);
        return true;
    }
    [MenuItem("Tools/Vehicles/Validate Selected Flyby Routes")]
    public static void ValidateSelected()
    {
        var owner = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<AlienFlybyController>() : null;
        if (owner == null)
        {
            Debug.LogError("Select AlienFlybys or one of its routes.");
            return;
        }
        foreach (var path in owner.GetComponentsInChildren<AlienFlybyPath>(true))
            if (!Validate(path, owner.environmentRoots, out string message))
                Debug.LogError(message, path);
            else
                Debug.Log(path.name + ": full bike/wake clearance valid", path);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(owner.gameObject.scene);
    }
    [MenuItem("Tools/Vehicles/Raise Selected Flyby Routes To Clearance")]
    public static void RaiseSelected()
    {
        var owner = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<AlienFlybyController>() : null;
        if (owner == null)
            return;
        foreach (var path in Selection.activeGameObject.GetComponentsInChildren<AlienFlybyPath>(true))
        {
            Undo.RecordObject(path.transform, "Raise flyby clearance");
            for (int n = 0; n < 80; n++)
            {
                if (Validate(path, owner.environmentRoots, out _))
                    break;
                path.transform.position += Vector3.up * .5f;
            }
        }
        ValidateSelected();
    }
}

// Environment edits must also pass full clearance when a scene enters a build.
public sealed class AlienFlybyBuildValidation : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var owner in root.GetComponentsInChildren<AlienFlybyController>(true))
                foreach (var path in owner.GetComponentsInChildren<AlienFlybyPath>(true))
                    if (!AlienFlybyValidation.Validate(path, owner.environmentRoots, out string failure))
                        throw new BuildFailedException(failure);
    }
}
