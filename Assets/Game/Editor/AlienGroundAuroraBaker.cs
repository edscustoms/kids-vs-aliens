using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit Editor-only placement, clearance and circular scheduling. Never runs on load/save/play.</summary>
public static class AlienGroundAuroraBaker
{
    private struct Location { public Vector3 position; public Quaternion rotation; public Vector2 dimensions; }

    public static void Bake(AlienGroundAuroraAuthoring authoring)
    {
        ValidateInputs(authoring);
        Physics.SyncTransforms();
        var random = new System.Random(authoring.seed);
        var exclusions = Exclusions(authoring);
        var blockers = Blockers(authoring);
        var locations = new List<Location>();
        int candidateCount = Math.Max(authoring.eventCount, authoring.candidateLocationCount);
        for (int attempt = 0; attempt < candidateCount * 400 && locations.Count < candidateCount; attempt++)
        {
            var zone = authoring.allowedZones[random.Next(authoring.allowedZones.Length)];
            Vector3 sample = authoring.controller.transform.TransformPoint(new Vector3(
                Range(random, zone.min.x, zone.max.x), zone.max.y, Range(random, zone.min.z, zone.max.z)));
            if (!TryGround(authoring, sample, zone.size.y, out var hit)) continue;
            var location = new Location
            {
                position = hit.point + hit.normal * .07f,
                rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, Range(random, 0, 360), 0),
                dimensions = new Vector2(Range(random, authoring.lengthRange), Range(random, authoring.widthRange))
            };
            if (locations.Any(other => HorizontalDistance(other.position, location.position) < authoring.locationSpacing)) continue;
            if (!FootprintValid(authoring, location.position, location.rotation, location.dimensions, exclusions, blockers)) continue;
            locations.Add(location);
        }
        if (locations.Count < authoring.eventCount)
            throw new InvalidOperationException($"Only {locations.Count}/{authoring.eventCount} valid Aurora spots. Adjust count/coverage/spacing; the existing bake was preserved.");

        var events = new List<AlienGroundAuroraSchedule.Event>();
        int slots = Math.Min(authoring.eventCount, authoring.maximumActive);
        for (int slot = 0; slot < slots; slot++)
        {
            // Distribute the exact total, including the remainder, over the existing circular tracks.
            int count = authoring.eventCount / slots + (slot < authoring.eventCount % slots ? 1 : 0);
            float[] lifetimes = new float[count];
            float[] gaps = new float[count];
            float totalLife = 0, totalGapWeight = 0;
            for (int i = 0; i < lifetimes.Length; i++)
            {
                totalLife += lifetimes[i] = Range(random, authoring.lifetimeRange);
                totalGapWeight += gaps[i] = Mathf.Lerp(1, Range(random, .5f, 1.5f), authoring.timingSpread);
            }
            if (totalLife + lifetimes.Length * .25f >= authoring.scheduleDuration)
                throw new InvalidOperationException("Event lifetimes leave no rest in the loop. Reduce Event Count or increase Schedule Duration; existing bake preserved.");
            float time = Mathf.Repeat((slot + Range(random, -.5f, .5f) * authoring.timingSpread)
                * authoring.scheduleDuration / slots, authoring.scheduleDuration);
            for (int i = 0; i < lifetimes.Length; i++)
            {
                events.Add(new AlienGroundAuroraSchedule.Event
                {
                    poolSlot = slot, startTime = time % authoring.scheduleDuration, lifetime = lifetimes[i],
                    fadeIn = Range(random, authoring.fadeInRange), fadeOut = Range(random, authoring.fadeOutRange),
                    motionSpeed = Range(random, authoring.movementSpeedRange), phase = Range(random, 0, 6.283185f),
                    footprint = new Vector4(authoring.edgeSoftness, authoring.irregularity,
                        Range(random, -3, 3) * authoring.shapeVariation, Range(random, -3, 3) * authoring.shapeVariation),
                    // Violet is an occasional accent; most patches use the first three cool colors.
                    tint = authoring.palette[random.NextDouble() < .14 ? authoring.palette.Length - 1 : random.Next(Math.Min(3, authoring.palette.Length))],
                    intensity = Range(random, .85f, 1.3f)
                });
                time += lifetimes[i] + gaps[i] / totalGapWeight * (authoring.scheduleDuration - totalLife);
            }
        }
        events.Sort((a, b) => a.startTime.CompareTo(b.startTime));
        var usedLocations = new bool[locations.Count];
        bool placed = false;
        // Retry seeded assignments so an early large footprint does not unnecessarily trap
        // later unique spots. This remains bounded Editor work; accepted data changes only on success.
        for (int assignment = 0; assignment < 64 && !placed; assignment++)
        {
            Array.Clear(usedLocations, 0, usedLocations.Length);
            placed = true;
            for (int i = 0; i < events.Count; i++)
            {
                int first = random.Next(locations.Count), selected = -1;
                for (int attempt = 0; attempt < locations.Count; attempt++)
                {
                    int index = (first + attempt) % locations.Count;
                    if (usedLocations[index]) continue;
                    var candidate = locations[index];
                    bool clear = true;
                    for (int j = 0; j < i; j++)
                    {
                        if (!Overlap(events[i], events[j], authoring.scheduleDuration)) continue;
                        Vector3 priorPosition = authoring.controller.transform.TransformPoint(events[j].position);
                        float separation = candidate.dimensions.magnitude * .55f + events[j].dimensions.magnitude * .55f + authoring.activeSpacing;
                        if (HorizontalDistance(candidate.position, priorPosition) < separation) { clear = false; break; }
                    }
                    if (clear) { selected = index; break; }
                }
                if (selected < 0) { placed = false; break; }
                var value = events[i];
                var location = locations[selected];
                value.position = authoring.controller.transform.InverseTransformPoint(location.position);
                value.rotation = Quaternion.Inverse(authoring.controller.transform.rotation) * location.rotation;
                value.dimensions = location.dimensions;
                events[i] = value;
                usedLocations[selected] = true;
            }
        }
        if (!placed)
        {
            throw new InvalidOperationException("Not enough separated Aurora footprints for this event count/timing. Increase Schedule Duration or reduce count/coverage; existing bake preserved.");
        }

        // Compute the actual circular occupancy, including the events crossing the seam.
        int minimum = authoring.maximumActive, maximum = 0;
        foreach (var value in events)
        {
            foreach (float at in new[] {value.startTime + .0001f, (value.startTime + value.lifetime + .0001f) % authoring.scheduleDuration})
            {
                int active = events.Count(e => Mathf.Repeat(at - e.startTime, authoring.scheduleDuration) < e.lifetime);
                minimum = Math.Min(minimum, active); maximum = Math.Max(maximum, active);
            }
        }
        float average = events.Sum(e => e.lifetime) / authoring.scheduleDuration;
        Undo.RecordObject(authoring.output, "Bake Alien Ground Aurora");
        authoring.output.duration = authoring.scheduleDuration;
        authoring.output.poolSize = authoring.controller.PoolSize;
        authoring.output.events = events.ToArray();
        authoring.output.bakeSummary = $"{authoring.gameObject.scene.path}\nSeed {authoring.seed}; {locations.Count} validated locations; {events.Count} events / {authoring.scheduleDuration:0.##} s.\nActive {minimum}–{maximum}, mean {average:0.00}; fixed pool {authoring.controller.PoolSize}. Terrain/clearance and all variation baked in Editor.";
        EditorUtility.SetDirty(authoring.output);
        AssetDatabase.SaveAssetIfDirty(authoring.output);
        Debug.Log(authoring.output.bakeSummary, authoring);
    }

    private static void ValidateInputs(AlienGroundAuroraAuthoring a)
    {
        if (Application.isPlaying) throw new InvalidOperationException("Bake Aurora in Edit Mode only.");
        if (a.controller == null || a.output == null || a.controller.Schedule != a.output)
            throw new InvalidOperationException("Assign a controller and its schedule asset before baking.");
        if (a.controller.transform.lossyScale != Vector3.one || Vector3.Dot(a.controller.transform.up, Vector3.up) < .999f)
            throw new InvalidOperationException("Aurora authoring requires an upright controller with unit scale.");
        if (a.allowedGround.Length == 0 || a.allowedGround.Any(c => c == null || !c.enabled || c.isTrigger)
            || a.allowedZones.Length == 0 || a.palette.Length == 0 || a.candidateLocationCount < 1 || a.eventCount < 1
            || a.maximumActive < 1 || a.maximumActive > 14 || a.maximumActive > a.controller.PoolSize || a.scheduleDuration < 10)
            throw new InvalidOperationException("Assign valid ground/zones/palette and a fixed pool supporting the requested count (maximum 14).");
        foreach (var range in new[] {a.lifetimeRange, a.lengthRange, a.widthRange, a.fadeInRange, a.fadeOutRange, a.movementSpeedRange})
            if (range.x <= 0 || range.y < range.x) throw new InvalidOperationException("Aurora ranges must be positive and ordered.");
        if (a.lifetimeRange.y >= a.scheduleDuration || a.fadeInRange.y + a.fadeOutRange.y >= a.lifetimeRange.x)
            throw new InvalidOperationException("Lifetimes must fit the loop, with time between fade-in and fade-out.");
        if (Math.Ceiling((double)a.eventCount / a.maximumActive) * (a.lifetimeRange.x + .25f) >= a.scheduleDuration)
            throw new InvalidOperationException("Event Count cannot fit the loop capacity. Reduce count/lifetime or increase Schedule Duration/Maximum Active; existing bake preserved.");
        if (a.edgeSoftness < .1f || a.edgeSoftness > .7f || a.irregularity < 0 || a.irregularity > 1
            || a.shapeVariation < 0 || a.shapeVariation > 1 || a.timingSpread < 0 || a.timingSpread > 1)
            throw new InvalidOperationException("Use the authored footprint/timing ranges; existing bake preserved.");
    }

    public static bool Overlap(AlienGroundAuroraSchedule.Event a, AlienGroundAuroraSchedule.Event b, float duration) =>
        Mathf.Repeat(a.startTime - b.startTime, duration) < b.lifetime
        || Mathf.Repeat(b.startTime - a.startTime, duration) < a.lifetime;

    private static float Range(System.Random random, Vector2 range) => Range(random, range.x, range.y);
    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);
    private static float HorizontalDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

    private static bool TryGround(AlienGroundAuroraAuthoring a, Vector3 origin, float distance, out RaycastHit ground)
    {
        ground = default;
        var hits = Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        foreach (var hit in hits)
        {
            if (hit.collider is CharacterController || hit.collider.gameObject.scene != a.gameObject.scene || hit.distance >= nearest) continue;
            nearest = hit.distance; ground = hit;
        }
        return ground.collider != null && Array.IndexOf(a.allowedGround, ground.collider) >= 0
            && Vector3.Angle(Vector3.up, ground.normal) <= a.slopeLimit;
    }

    private static Bounds[] Exclusions(AlienGroundAuroraAuthoring a)
    {
        var result = new List<Bounds>();
        foreach (var exclusion in a.excludedZones)
        {
            var world = new Bounds(a.controller.transform.TransformPoint(exclusion.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                world.Encapsulate(a.controller.transform.TransformPoint(exclusion.center + Vector3.Scale(exclusion.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            result.Add(world);
        }
        foreach (var root in a.excludedObjects)
        {
            if (root == null) continue;
            Bounds bounds = new Bounds(root.position, Vector3.zero);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
            bounds.Expand(new Vector3(a.exclusionPadding * 2, 30, a.exclusionPadding * 2));
            result.Add(bounds);
        }
        return result.ToArray();
    }

    private static Bounds[] Blockers(AlienGroundAuroraAuthoring a) => a.gameObject.scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>())
        .Where(r => r.enabled && !r.transform.IsChildOf(a.controller.transform) && r.bounds.size.y > .25f
            && r.GetComponentInParent<PlayerCharacter>() == null)
        .Select(r => r.bounds).ToArray();

    private static bool FootprintValid(AlienGroundAuroraAuthoring a, Vector3 position, Quaternion rotation,
        Vector2 dimensions, Bounds[] exclusions, Bounds[] blockers)
    {
        Vector3 up = rotation * Vector3.up;
        // Includes the ribbon's curved sides and all vertex motion. Validate the whole plane,
        // not just a center ray that could leave half the mesh over an edge or inside a fence.
        float halfX = dimensions.x * .55f + .15f, halfZ = dimensions.y * .65f + .15f;
        for (int x = 0; x <= 8; x++)
        for (int z = 0; z <= 4; z++)
        {
            Vector3 point = position + rotation * new Vector3(Mathf.Lerp(-halfX, halfX, x / 8f), 0, Mathf.Lerp(-halfZ, halfZ, z / 4f));
            Vector3 local = a.controller.transform.InverseTransformPoint(point);
            if (!a.allowedZones.Any(zone => zone.Contains(local))) return false;
            foreach (var bounds in exclusions)
                if (point.x >= bounds.min.x && point.x <= bounds.max.x && point.z >= bounds.min.z && point.z <= bounds.max.z) return false;
            foreach (var bounds in blockers)
                if (point.x >= bounds.min.x - .15f && point.x <= bounds.max.x + .15f
                    && point.z >= bounds.min.z - .15f && point.z <= bounds.max.z + .15f
                    && bounds.max.y > point.y + .12f && bounds.min.y < point.y + 12) return false;
            if (!TryGround(a, point + Vector3.up * 20, 22, out var ground)) return false;
            float height = Vector3.Dot(point - ground.point, up);
            if (Mathf.Abs(height - .07f) > a.groundTolerance) return false;
        }
        // V2's rising sheets occupy up to 0.75 m above their base, including deformation.
        foreach (var collider in Physics.OverlapBox(position + up * .4f,
            new Vector3(halfX, .38f, halfZ), rotation, ~0, QueryTriggerInteraction.Ignore))
            if (!(collider is CharacterController) && Array.IndexOf(a.allowedGround, collider) < 0
                && collider.gameObject.scene == a.gameObject.scene) return false;
        return true;
    }

    public static bool ValidateFootprint(AlienGroundAuroraAuthoring a, AlienGroundAuroraSchedule.Event value) =>
        FootprintValid(a, a.controller.transform.TransformPoint(value.position), a.controller.transform.rotation * value.rotation,
            value.dimensions, Exclusions(a), Blockers(a));
}

[CustomEditor(typeof(AlienGroundAuroraAuthoring))]
public sealed class AlienGroundAuroraAuthoringEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var authoring = (AlienGroundAuroraAuthoring)target;
        EditorGUILayout.HelpBox("Event Count is the exact number of distinct spots in one loop; Maximum Active caps simultaneous playback. Schedule Duration and Timing Spread control when they appear. All footprint/fade settings require Bake; reload, save and Play never rebake.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Bake / Regenerate Schedule"))
            {
                try { AlienGroundAuroraBaker.Bake(authoring); }
                catch (Exception exception) { Debug.LogError(exception.Message, authoring); }
            }
        if (authoring.output != null) EditorGUILayout.HelpBox(authoring.output.bakeSummary, MessageType.None);
    }
}
