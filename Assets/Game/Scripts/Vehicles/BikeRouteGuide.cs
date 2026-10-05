using System;
using UnityEngine;

/// <summary>Scene-authored route samples, forward rejoins and jump hints. No runtime road-package dependency.</summary>
public sealed class BikeRouteGuide : MonoBehaviour
{
    [Serializable]
    public sealed class Path
    {
        public string label;
        public Vector3[] points = Array.Empty<Vector3>();
        public float[] distances = Array.Empty<float>();
        public float startProgress, endProgress, halfWidth;
        public bool shortcut;
        public float Length => distances.Length > 0 ? distances[distances.Length - 1] : 0;
    }
    [Serializable]
    public struct SpawnAnchor
    {
        public int path;
        public float distance;
        public Vector3 position;
        public Vector3 forward;
        public float halfWidth;
    }
    [Serializable]
    public struct JumpHint
    {
        public int path;
        public float releaseDistance;
        public float chargeLead;
    }
    public struct Sample
    {
        public int path;
        public float distance, progress, halfWidth;
        public Vector3 position, forward;
        public Vector3 Right => Vector3.Cross(Vector3.up, forward).normalized;
    }
    public Path[] paths = Array.Empty<Path>();
    public SpawnAnchor[] spawnAnchors = Array.Empty<SpawnAnchor>();
    public JumpHint[] jumps = Array.Empty<JumpHint>();
    public float FinishProgress { get; private set; }
    private void Awake()
    {
        foreach (var path in paths) FinishProgress = Mathf.Max(FinishProgress, path.endProgress);
    }
    public Sample At(int pathIndex, float distance)
    {
        var path = paths[pathIndex];
        distance = Mathf.Clamp(distance, 0, path.Length);
        int segment = Array.BinarySearch(path.distances, distance);
        if (segment < 0) segment = ~segment - 1;
        segment = Mathf.Clamp(segment, 0, path.points.Length - 2);
        float t = Mathf.InverseLerp(path.distances[segment], path.distances[segment + 1], distance);
        return new Sample {
            path = pathIndex, distance = distance, halfWidth = path.halfWidth,
            progress = Mathf.Lerp(path.startProgress, path.endProgress, distance / Mathf.Max(.1f, path.Length)),
            position = Vector3.Lerp(path.points[segment], path.points[segment + 1], t),
            forward = (path.points[segment + 1] - path.points[segment]).normalized
        };
    }
    public Sample Project(Vector3 position, int preferredPath = -1)
    {
        float best = float.PositiveInfinity, distance = 0;
        int chosen = 0;
        for (int p = 0; p < paths.Length; p++)
        {
            var path = paths[p];
            for (int i = 0; i < path.points.Length - 1; i++)
            {
                Vector3 delta = path.points[i + 1] - path.points[i];
                float t = Mathf.Clamp01(Vector3.Dot(position - path.points[i], delta) / Mathf.Max(.001f, delta.sqrMagnitude));
                float score = (position - path.points[i] - delta * t).sqrMagnitude;
                // Keep a lane at close parallel joins; height still separates the over/under.
                if (p == preferredPath) score *= .9f;
                if (score >= best) continue;
                best = score; chosen = p;
                distance = Mathf.Lerp(path.distances[i], path.distances[i + 1], t);
            }
        }
        return At(chosen, distance);
    }
    public Sample Ahead(Sample from, float travel, int targetPath)
    {
        float nextDistance = from.distance + travel;
        var current = paths[from.path];
        if (nextDistance >= 0 && nextDistance <= current.Length) return At(from.path, nextDistance);
        bool forward = travel >= 0;
        float join = forward ? current.endProgress : current.startProgress;
        int chosen = -1;
        for (int i = 0; i < paths.Length; i++)
        {
            if (i == from.path || Mathf.Abs((forward ? paths[i].startProgress : paths[i].endProgress) - join) > 1) continue;
            if (chosen < 0 || !paths[i].shortcut) chosen = i;
            if (i == targetPath) { chosen = i; break; }
        }
        if (chosen < 0) return At(from.path, Mathf.Clamp(nextDistance, 0, current.Length));
        return At(chosen, forward ? nextDistance - current.Length : paths[chosen].Length + nextDistance);
    }
    public bool ShouldChargeJump(Sample sample, float speed, float timingOffset)
    {
        foreach (var jump in jumps)
        {
            if (jump.path != sample.path) continue;
            float until = jump.releaseDistance + timingOffset - sample.distance;
            if (until > 0 && until < Mathf.Max(jump.chargeLead, speed * 1.25f)) return true;
        }
        return false;
    }
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        foreach (var path in paths)
        {
            Gizmos.color = path.shortcut ? Color.yellow : Color.cyan;
            for (int i = 1; i < path.points.Length; i++) Gizmos.DrawLine(path.points[i - 1] + Vector3.up, path.points[i] + Vector3.up);
        }
        Gizmos.color = Color.magenta;
        foreach (var anchor in spawnAnchors) Gizmos.DrawWireSphere(anchor.position + Vector3.up, .6f);
    }
#endif
}
