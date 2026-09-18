using System.Collections.Generic;
using UnityEngine;

// Presentation-only union topology, in meters along the surface's local X/Z axes.
// Original ground vertices remain untouched: bounds organize artwork, never fill geometry.
public static class BeamHoistZoneTopology
{
    private const float Epsilon = .001f;
    public struct Edge
    {
        public Vector2 a, b;
        public float Distance(Vector2 p) => Vector2.Distance(p, Vector2.Lerp(a, b,
            Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude)));
    }
    public sealed class Group
    {
        public readonly List<int> cells = new();
        public readonly List<Rect> rectangles = new();
        public readonly List<Edge> edges = new();
        public readonly List<Vector2> corners = new();
        public Rect bounds;
        public Vector2 core;
        public float radius;
        public bool rectangular;
        public bool Contains(Vector2 point, float tolerance = 0)
        {
            foreach (var r in rectangles)
                if (point.x >= r.xMin - tolerance && point.x <= r.xMax + tolerance
                    && point.y >= r.yMin - tolerance && point.y <= r.yMax + tolerance) return true;
            return false;
        }
        public float Clearance(Vector2 point)
        {
            float distance = float.PositiveInfinity;
            foreach (var edge in edges) distance = Mathf.Min(distance, edge.Distance(point));
            return distance;
        }
        public Color Field(Vector2 point)
        {
            float distance = float.PositiveInfinity, perimeter = 0, corner = float.PositiveInfinity;
            foreach (var edge in edges)
            {
                float d = edge.Distance(point);
                if (d >= distance) continue;
                distance = d;
                perimeter = Mathf.Abs(edge.a.x - edge.b.x) < Epsilon ? point.y - bounds.yMin : point.x - bounds.xMin;
            }
            foreach (var c in corners) corner = Mathf.Min(corner, Mathf.Max(Mathf.Abs(point.x - c.x), Mathf.Abs(point.y - c.y)));
            // Use the same millimeter tolerance as adjacency, so numerical seams do not
            // become negative distance-field stripes. Actual mesh coverage is never expanded.
            return new Color(Contains(point, Epsilon) ? distance : -distance, perimeter, corner, 1);
        }
    }

    public static List<Group> Build(IReadOnlyList<BeamHoistZoneVFX.Patch> patches, Vector2 scale)
    {
        var rects = new Rect[patches.Count];
        for (int i = 0; i < patches.Count; i++)
        {
            var b = patches[i].bounds;
            rects[i] = Rect.MinMaxRect(b.min.x * scale.x, b.min.z * scale.y, b.max.x * scale.x, b.max.z * scale.y);
        }
        var groups = new List<Group>();
        var visited = new bool[patches.Count];
        for (int seed = 0; seed < rects.Length; seed++)
        {
            if (visited[seed]) continue;
            var group = new Group { bounds = rects[seed] }; group.cells.Add(seed); visited[seed] = true;
            for (int n = 0; n < group.cells.Count; n++)
            {
                int index = group.cells[n]; Rect r = rects[index]; group.rectangles.Add(r);
                group.bounds = Rect.MinMaxRect(Mathf.Min(group.bounds.xMin, r.xMin), Mathf.Min(group.bounds.yMin, r.yMin),
                    Mathf.Max(group.bounds.xMax, r.xMax), Mathf.Max(group.bounds.yMax, r.yMax));
                for (int j = 0; j < rects.Length; j++)
                    if (!visited[j] && Connected(r, rects[j])) { visited[j] = true; group.cells.Add(j); }
            }
            group.rectangles.Sort((a, b) => a.xMin != b.xMin ? a.xMin.CompareTo(b.xMin) : a.yMin != b.yMin
                ? a.yMin.CompareTo(b.yMin) : a.xMax != b.xMax ? a.xMax.CompareTo(b.xMax) : a.yMax.CompareTo(b.yMax));
            BuildBoundary(group);
            PlaceCore(group);
            groups.Add(group);
        }
        groups.Sort((a, b) => a.bounds.xMin != b.bounds.xMin ? a.bounds.xMin.CompareTo(b.bounds.xMin) : a.bounds.yMin.CompareTo(b.bounds.yMin));
        return groups;
    }

    private static bool Connected(Rect a, Rect b)
    {
        float x = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float y = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        // Shared edge of positive length; diagonal/corner-only contact is not connectivity.
        return x > Epsilon && y >= -Epsilon || y > Epsilon && x >= -Epsilon;
    }

    private static void BuildBoundary(Group group)
    {
        var intervals = new List<Vector2>();
        foreach (var r in group.rectangles)
            for (int side = 0; side < 4; side++)
            {
                bool vertical = side < 2;
                float fixedAxis = side == 0 ? r.xMin : side == 1 ? r.xMax : side == 2 ? r.yMin : r.yMax;
                float min = vertical ? r.yMin : r.xMin, max = vertical ? r.yMax : r.xMax;
                intervals.Clear(); intervals.Add(new Vector2(min, max));
                foreach (var neighbor in group.rectangles)
                {
                    bool outside = side == 0 ? neighbor.xMin < fixedAxis - Epsilon && neighbor.xMax >= fixedAxis - Epsilon
                        : side == 1 ? neighbor.xMax > fixedAxis + Epsilon && neighbor.xMin <= fixedAxis + Epsilon
                        : side == 2 ? neighbor.yMin < fixedAxis - Epsilon && neighbor.yMax >= fixedAxis - Epsilon
                        : neighbor.yMax > fixedAxis + Epsilon && neighbor.yMin <= fixedAxis + Epsilon;
                    if (!outside) continue;
                    float lo = vertical ? neighbor.yMin : neighbor.xMin, hi = vertical ? neighbor.yMax : neighbor.xMax;
                    for (int i = intervals.Count - 1; i >= 0; i--)
                    {
                        var span = intervals[i];
                        if (hi <= span.x + Epsilon || lo >= span.y - Epsilon) continue;
                        intervals.RemoveAt(i);
                        if (lo > span.x + Epsilon) intervals.Add(new Vector2(span.x, lo));
                        if (hi < span.y - Epsilon) intervals.Add(new Vector2(hi, span.y));
                    }
                }
                foreach (var span in intervals)
                    group.edges.Add(new Edge { a = vertical ? new Vector2(fixedAxis, span.x) : new Vector2(span.x, fixedAxis),
                        b = vertical ? new Vector2(fixedAxis, span.y) : new Vector2(span.y, fixedAxis) });
            }
        // Merge collinear pieces, then retain only true turns for cyan corners.
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < group.edges.Count && !changed; i++)
                for (int j = i + 1; j < group.edges.Count; j++)
                {
                    var a = group.edges[i]; var b = group.edges[j];
                    bool vertical = Mathf.Abs(a.a.x - a.b.x) < Epsilon;
                    if (vertical != (Mathf.Abs(b.a.x - b.b.x) < Epsilon)) continue;
                    if (Mathf.Abs(vertical ? a.a.x - b.a.x : a.a.y - b.a.y) > Epsilon) continue;
                    if (Vector2.Distance(a.b, b.a) > Epsilon && Vector2.Distance(b.b, a.a) > Epsilon) continue;
                    group.edges[i] = new Edge { a = Vector2.Min(a.a, b.a), b = Vector2.Max(a.b, b.b) };
                    group.edges.RemoveAt(j); changed = true; break;
                }
        } while (changed);
        foreach (var edge in group.edges)
            foreach (var point in new[] { edge.a, edge.b })
            {
                bool exists = false;
                foreach (var c in group.corners) if (Vector2.Distance(c, point) <= Epsilon) { exists = true; break; }
                if (!exists) group.corners.Add(point);
            }
        group.rectangular = group.edges.Count == 4 && group.corners.Count == 4;
    }

    private static void PlaceCore(Group group)
    {
        float area = 0; Vector2 centroid = Vector2.zero;
        foreach (var r in group.rectangles) { float a = r.width * r.height; centroid += r.center * a; area += a; }
        centroid /= Mathf.Max(area, .000001f);
        float desired = Mathf.Min(group.bounds.width, group.bounds.height) * .23f;
        float bestClearance = -1, bestDistance = float.PositiveInfinity;
        void Consider(Vector2 p)
        {
            if (!group.Contains(p)) return;
            float clearance = group.Clearance(p), distance = (p - centroid).sqrMagnitude;
            bool fits = clearance >= desired * 1.2f, bestFits = bestClearance >= desired * 1.2f;
            if (fits ? !bestFits || distance < bestDistance : !bestFits && (clearance > bestClearance + Epsilon
                || Mathf.Abs(clearance - bestClearance) <= Epsilon && distance < bestDistance))
            { group.core = p; bestClearance = clearance; bestDistance = distance; }
        }
        Consider(centroid);
        // Deterministic candidates independent of cell order and Amy, including irregular/hollow shapes.
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
                Consider(new Vector2(Mathf.Lerp(group.bounds.xMin, group.bounds.xMax, (x + .5f) / 48),
                    Mathf.Lerp(group.bounds.yMin, group.bounds.yMax, (y + .5f) / 48)));
        foreach (var r in group.rectangles) Consider(r.center);
        if (group.rectangular) { group.core = group.bounds.center; group.radius = desired; }
        else group.radius = Mathf.Min(desired, Mathf.Max(.001f, bestClearance / 1.2f));
    }
}
