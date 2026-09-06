using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools.ModularLevelKit
{
    internal static class ModularSnapUtility
    {
        public const string SocketPrefix = "Snap_";

        private const float TopNormalThreshold = 0.90f;
        private const float BoundarySampleLength = 0.25f;
        private const float ExposureProbeDistance = 0.02f;
        private const float MergeTolerance = 0.025f;
        private const float ParallelDotThreshold = 0.995f;
        private const float FacingDotThreshold = 0.35f;

        internal struct SocketPair
        {
            public Transform moving;
            public Transform target;
            public float sqrDistance;
        }

        internal struct BoundaryEdge
        {
            public Vector2 a;
            public Vector2 b;
            public Vector2 outward;

            public Vector2 Midpoint => (a + b) * 0.5f;
            public float Length => Vector2.Distance(a, b);

            public Vector2 Direction
            {
                get
                {
                    Vector2 direction = b - a;
                    return direction.sqrMagnitude > 0.000001f
                        ? direction.normalized
                        : Vector2.right;
                }
            }
        }

        private struct Triangle2D
        {
            public Vector2 a;
            public Vector2 b;
            public Vector2 c;

            public Vector2 Centroid => (a + b + c) / 3f;
        }

        // ============================================================
        // Existing socket-based module snapping
        // ============================================================

        public static List<Transform> GetSockets(Transform moduleRoot)
        {
            var result = new List<Transform>();

            if (moduleRoot == null)
                return result;

            foreach (Transform child in moduleRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child == moduleRoot)
                    continue;

                if (child.name.StartsWith(
                        SocketPrefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(child);
                }
            }

            return result;
        }

        public static bool TryFindClosestCompatiblePair(
            Transform movingRoot,
            Transform targetRoot,
            bool requireMatchingChannel,
            out SocketPair pair)
        {
            pair = default;

            var allMovingSockets = GetSockets(movingRoot);
            var targetSockets = GetSockets(targetRoot);

            if (allMovingSockets.Count == 0 || targetSockets.Count == 0)
                return false;

            var movingSockets = allMovingSockets.FindAll(IsPrimaryFastSnapSocket);

            if (movingSockets.Count == 0)
                movingSockets = allMovingSockets;

            bool found = false;
            float bestDistance = float.PositiveInfinity;

            foreach (Transform moving in movingSockets)
            {
                foreach (Transform target in targetSockets)
                {
                    if (requireMatchingChannel &&
                        !string.Equals(
                            GetChannel(moving.name),
                            GetChannel(target.name),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    float sqrDistance =
                        (moving.position - target.position).sqrMagnitude;

                    if (sqrDistance >= bestDistance)
                        continue;

                    bestDistance = sqrDistance;
                    pair = new SocketPair
                    {
                        moving = moving,
                        target = target,
                        sqrDistance = sqrDistance
                    };
                    found = true;
                }
            }

            return found;
        }

        public static bool TryFindClosestPrimaryPair(
            Transform movingRoot,
            Transform targetRoot,
            bool requireMatchingChannel,
            out SocketPair pair)
        {
            pair = default;

            var movingSockets =
                GetSockets(movingRoot).FindAll(IsPrimaryFastSnapSocket);

            var targetSockets =
                GetSockets(targetRoot).FindAll(IsPrimaryFastSnapSocket);

            if (movingSockets.Count == 0 || targetSockets.Count == 0)
                return false;

            bool found = false;
            float bestDistance = float.PositiveInfinity;

            foreach (Transform moving in movingSockets)
            {
                foreach (Transform target in targetSockets)
                {
                    if (requireMatchingChannel &&
                        !string.Equals(
                            GetChannel(moving.name),
                            GetChannel(target.name),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    float sqrDistance =
                        (moving.position - target.position).sqrMagnitude;

                    if (sqrDistance >= bestDistance)
                        continue;

                    bestDistance = sqrDistance;
                    pair = new SocketPair
                    {
                        moving = moving,
                        target = target,
                        sqrDistance = sqrDistance
                    };
                    found = true;
                }
            }

            return found;
        }

        private static bool IsPrimaryFastSnapSocket(Transform socket)
        {
            return socket != null &&
                   socket.name.IndexOf(
                       "_Slot_",
                       StringComparison.OrdinalIgnoreCase) < 0;
        }

        public static void SnapModule(
            Transform movingModuleRoot,
            Transform movingSocket,
            Transform targetSocket)
        {
            if (movingModuleRoot == null ||
                movingSocket == null ||
                targetSocket == null)
            {
                return;
            }

            Undo.RecordObject(
                movingModuleRoot,
                "Snap Modular Level Piece");

            Quaternion desiredMovingSocketRotation =
                targetSocket.rotation * Quaternion.Euler(0f, 180f, 0f);

            Quaternion rotationDelta =
                desiredMovingSocketRotation *
                Quaternion.Inverse(movingSocket.rotation);

            movingModuleRoot.rotation =
                rotationDelta * movingModuleRoot.rotation;

            Vector3 positionDelta =
                targetSocket.position - movingSocket.position;

            movingModuleRoot.position += positionDelta;

            EditorUtility.SetDirty(movingModuleRoot);
        }

        // ============================================================
        // V9 exposed-boundary snapping
        // Works on:
        // - one floor piece -> one floor piece
        // - parented chunks -> parented chunks
        // - several loose selected pieces -> another selection
        //
        // No group sockets or setup are needed.
        // ============================================================

        public static bool TrySnapSelectionBoundary(
            IReadOnlyList<GameObject> movingObjects,
            IReadOnlyList<GameObject> targetObjects,
            out string resultMessage)
        {
            resultMessage = string.Empty;

            List<GameObject> movingRoots =
                GetTopLevelObjects(movingObjects);

            List<GameObject> targetRoots =
                GetTopLevelObjects(targetObjects);

            if (movingRoots.Count == 0 || targetRoots.Count == 0)
            {
                resultMessage = "Target and moving selections must both contain objects.";
                return false;
            }

            if (!TryBuildExposedBoundary(
                    movingRoots,
                    out List<BoundaryEdge> movingEdges))
            {
                resultMessage =
                    "No usable exposed floor boundary found in the moving selection.";
                return false;
            }

            if (!TryBuildExposedBoundary(
                    targetRoots,
                    out List<BoundaryEdge> targetEdges))
            {
                resultMessage =
                    "No usable exposed floor boundary found in the target selection.";
                return false;
            }

            if (!TryFindBestBoundaryPair(
                    movingEdges,
                    targetEdges,
                    out BoundaryEdge movingEdge,
                    out BoundaryEdge targetEdge))
            {
                resultMessage =
                    "No sensible facing boundary pair found. Roughly place the moving selection next to the target edge first.";
                return false;
            }

            Vector2 deltaXZ =
                targetEdge.Midpoint - movingEdge.Midpoint;

            Undo.RecordObjects(
                movingRoots
                    .Select(go => (UnityEngine.Object)go.transform)
                    .ToArray(),
                "Snap Level Selection Boundary");

            foreach (GameObject movingRoot in movingRoots)
            {
                Vector3 position = movingRoot.transform.position;
                position.x += deltaXZ.x;
                position.z += deltaXZ.y;
                movingRoot.transform.position = position;
                EditorUtility.SetDirty(movingRoot.transform);
            }

            resultMessage =
                $"Snapped {movingRoots.Count} moving root(s): exposed edge midpoint → exposed edge midpoint.";

            return true;
        }

        public static List<GameObject> GetTopLevelObjects(
            IEnumerable<GameObject> objects)
        {
            var input = objects
                .Where(go => go != null)
                .Distinct()
                .ToList();

            var inputSet =
                new HashSet<Transform>(input.Select(go => go.transform));

            var result = new List<GameObject>();

            foreach (GameObject go in input)
            {
                bool selectedAncestorExists = false;
                Transform parent = go.transform.parent;

                while (parent != null)
                {
                    if (inputSet.Contains(parent))
                    {
                        selectedAncestorExists = true;
                        break;
                    }

                    parent = parent.parent;
                }

                if (!selectedAncestorExists)
                    result.Add(go);
            }

            return result;
        }

        private static bool TryBuildExposedBoundary(
            IReadOnlyList<GameObject> selectedRoots,
            out List<BoundaryEdge> exposedEdges)
        {
            exposedEdges = new List<BoundaryEdge>();

            List<Triangle2D> topTriangles =
                CollectFloorTopTriangles(selectedRoots);

            if (topTriangles.Count == 0)
                return false;

            var exposedChunks = new List<BoundaryEdge>();

            foreach (Triangle2D triangle in topTriangles)
            {
                AddExposedChunks(
                    triangle.a,
                    triangle.b,
                    triangle.Centroid,
                    topTriangles,
                    exposedChunks);

                AddExposedChunks(
                    triangle.b,
                    triangle.c,
                    triangle.Centroid,
                    topTriangles,
                    exposedChunks);

                AddExposedChunks(
                    triangle.c,
                    triangle.a,
                    triangle.Centroid,
                    topTriangles,
                    exposedChunks);
            }

            if (exposedChunks.Count == 0)
                return false;

            exposedEdges = MergeCollinearChunks(exposedChunks);

            return exposedEdges.Count > 0;
        }

        private static List<Triangle2D> CollectFloorTopTriangles(
            IReadOnlyList<GameObject> selectedRoots)
        {
            var triangles = new List<Triangle2D>();
            var visitedModules = new HashSet<Transform>();

            foreach (GameObject selectedRoot in selectedRoots)
            {
                if (selectedRoot == null)
                    continue;

                foreach (Transform transform in
                         selectedRoot.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsFloorBoundaryModule(transform))
                        continue;

                    if (!visitedModules.Add(transform))
                        continue;

                    CollectHorizontalTopTriangles(
                        transform,
                        triangles);
                }

                // If the selected root itself is the generated floor module.
                if (IsFloorBoundaryModule(selectedRoot.transform) &&
                    visitedModules.Add(selectedRoot.transform))
                {
                    CollectHorizontalTopTriangles(
                        selectedRoot.transform,
                        triangles);
                }
            }

            return triangles;
        }

        private static bool IsFloorBoundaryModule(Transform transform)
        {
            if (transform == null)
                return false;

            foreach (Transform child in transform)
            {
                if (!IsSocket(child))
                    continue;

                string name = child.name;

                if (name.StartsWith("Snap_Ground_North", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Snap_Ground_South", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Snap_Ground_East", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Snap_Ground_West", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("Snap_Ground_Diagonal", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CollectHorizontalTopTriangles(
            Transform moduleRoot,
            List<Triangle2D> output)
        {
            foreach (MeshFilter filter in
                     moduleRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter == null ||
                    filter.sharedMesh == null)
                {
                    continue;
                }

                // Do not accidentally consume geometry from a nested generated
                // floor module if future prefabs become more complex.
                Transform nearestModule =
                    FindNearestFloorBoundaryModule(filter.transform);

                if (nearestModule != moduleRoot)
                    continue;

                Mesh mesh = filter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] indices = mesh.triangles;

                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    Vector3 a =
                        filter.transform.TransformPoint(vertices[indices[i]]);

                    Vector3 b =
                        filter.transform.TransformPoint(vertices[indices[i + 1]]);

                    Vector3 c =
                        filter.transform.TransformPoint(vertices[indices[i + 2]]);

                    Vector3 normal =
                        Vector3.Cross(b - a, c - a).normalized;

                    if (normal.y < TopNormalThreshold)
                        continue;

                    Vector2 a2 = new Vector2(a.x, a.z);
                    Vector2 b2 = new Vector2(b.x, b.z);
                    Vector2 c2 = new Vector2(c.x, c.z);

                    if (Mathf.Abs(SignedAreaTwice(a2, b2, c2)) < 0.000001f)
                        continue;

                    output.Add(new Triangle2D
                    {
                        a = a2,
                        b = b2,
                        c = c2
                    });
                }
            }
        }

        private static Transform FindNearestFloorBoundaryModule(
            Transform start)
        {
            Transform current = start;

            while (current != null)
            {
                if (IsFloorBoundaryModule(current))
                    return current;

                current = current.parent;
            }

            return null;
        }

        private static void AddExposedChunks(
            Vector2 edgeA,
            Vector2 edgeB,
            Vector2 triangleCentroid,
            List<Triangle2D> unionTriangles,
            List<BoundaryEdge> output)
        {
            Vector2 edge = edgeB - edgeA;
            float length = edge.magnitude;

            if (length < 0.0001f)
                return;

            Vector2 direction = edge / length;
            Vector2 midpoint = (edgeA + edgeB) * 0.5f;

            Vector2 perpendicular =
                new Vector2(-direction.y, direction.x);

            Vector2 toInterior =
                triangleCentroid - midpoint;

            Vector2 outward =
                Vector2.Dot(perpendicular, toInterior) < 0f
                    ? perpendicular
                    : -perpendicular;

            int chunkCount =
                Mathf.Max(
                    1,
                    Mathf.CeilToInt(length / BoundarySampleLength));

            for (int i = 0; i < chunkCount; i++)
            {
                float t0 = i / (float)chunkCount;
                float t1 = (i + 1) / (float)chunkCount;

                Vector2 a = Vector2.Lerp(edgeA, edgeB, t0);
                Vector2 b = Vector2.Lerp(edgeA, edgeB, t1);
                Vector2 chunkMid = (a + b) * 0.5f;

                Vector2 outsideProbe =
                    chunkMid + outward * ExposureProbeDistance;

                if (IsPointInsideAnyTriangle(
                        outsideProbe,
                        unionTriangles))
                {
                    continue;
                }

                output.Add(new BoundaryEdge
                {
                    a = a,
                    b = b,
                    outward = outward.normalized
                });
            }
        }

        private static bool IsPointInsideAnyTriangle(
            Vector2 point,
            List<Triangle2D> triangles)
        {
            foreach (Triangle2D triangle in triangles)
            {
                if (PointInsideTriangle(
                        point,
                        triangle.a,
                        triangle.b,
                        triangle.c))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PointInsideTriangle(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c)
        {
            float d1 = SignedAreaTwice(point, a, b);
            float d2 = SignedAreaTwice(point, b, c);
            float d3 = SignedAreaTwice(point, c, a);

            const float epsilon = 0.00001f;

            bool hasNegative =
                d1 < -epsilon ||
                d2 < -epsilon ||
                d3 < -epsilon;

            bool hasPositive =
                d1 > epsilon ||
                d2 > epsilon ||
                d3 > epsilon;

            return !(hasNegative && hasPositive);
        }

        private static float SignedAreaTwice(
            Vector2 a,
            Vector2 b,
            Vector2 c)
        {
            return
                (b.x - a.x) * (c.y - a.y) -
                (b.y - a.y) * (c.x - a.x);
        }

        private static List<BoundaryEdge> MergeCollinearChunks(
            List<BoundaryEdge> chunks)
        {
            var edges = chunks
                .Select(CanonicalizeEdgeDirection)
                .ToList();

            bool mergedSomething = true;

            while (mergedSomething)
            {
                mergedSomething = false;

                for (int i = 0; i < edges.Count && !mergedSomething; i++)
                {
                    for (int j = i + 1; j < edges.Count; j++)
                    {
                        if (!CanMerge(edges[i], edges[j]))
                            continue;

                        edges[i] = Merge(edges[i], edges[j]);
                        edges.RemoveAt(j);
                        mergedSomething = true;
                        break;
                    }
                }
            }

            return edges;
        }

        private static BoundaryEdge CanonicalizeEdgeDirection(
            BoundaryEdge edge)
        {
            Vector2 direction = edge.Direction;

            bool shouldFlip =
                direction.x < -0.0001f ||
                (Mathf.Abs(direction.x) <= 0.0001f &&
                 direction.y < 0f);

            if (!shouldFlip)
                return edge;

            return new BoundaryEdge
            {
                a = edge.b,
                b = edge.a,
                outward = edge.outward
            };
        }

        private static bool CanMerge(
            BoundaryEdge a,
            BoundaryEdge b)
        {
            Vector2 directionA = a.Direction;
            Vector2 directionB = b.Direction;

            if (Vector2.Dot(directionA, directionB) < 0.999f)
                return false;

            if (Vector2.Dot(a.outward, b.outward) < 0.98f)
                return false;

            Vector2 perpendicular =
                new Vector2(-directionA.y, directionA.x);

            float lineA = Vector2.Dot(a.a, perpendicular);
            float lineB = Vector2.Dot(b.a, perpendicular);

            if (Mathf.Abs(lineA - lineB) > MergeTolerance)
                return false;

            float a0 = Vector2.Dot(a.a, directionA);
            float a1 = Vector2.Dot(a.b, directionA);
            float b0 = Vector2.Dot(b.a, directionA);
            float b1 = Vector2.Dot(b.b, directionA);

            float minA = Mathf.Min(a0, a1);
            float maxA = Mathf.Max(a0, a1);
            float minB = Mathf.Min(b0, b1);
            float maxB = Mathf.Max(b0, b1);

            float gap =
                Mathf.Max(minA, minB) -
                Mathf.Min(maxA, maxB);

            return gap <= MergeTolerance;
        }

        private static BoundaryEdge Merge(
            BoundaryEdge a,
            BoundaryEdge b)
        {
            Vector2 direction = a.Direction;
            Vector2 perpendicular =
                new Vector2(-direction.y, direction.x);

            float lineOffset =
                (Vector2.Dot(a.a, perpendicular) +
                 Vector2.Dot(b.a, perpendicular)) * 0.5f;

            float minProjection = Mathf.Min(
                Vector2.Dot(a.a, direction),
                Vector2.Dot(a.b, direction),
                Vector2.Dot(b.a, direction),
                Vector2.Dot(b.b, direction));

            float maxProjection = Mathf.Max(
                Vector2.Dot(a.a, direction),
                Vector2.Dot(a.b, direction),
                Vector2.Dot(b.a, direction),
                Vector2.Dot(b.b, direction));

            Vector2 start =
                direction * minProjection +
                perpendicular * lineOffset;

            Vector2 end =
                direction * maxProjection +
                perpendicular * lineOffset;

            return new BoundaryEdge
            {
                a = start,
                b = end,
                outward = (a.outward + b.outward).normalized
            };
        }

        private static bool TryFindBestBoundaryPair(
            List<BoundaryEdge> movingEdges,
            List<BoundaryEdge> targetEdges,
            out BoundaryEdge movingBest,
            out BoundaryEdge targetBest)
        {
            movingBest = default;
            targetBest = default;

            bool found = false;
            float bestScore = float.PositiveInfinity;

            foreach (BoundaryEdge moving in movingEdges)
            {
                foreach (BoundaryEdge target in targetEdges)
                {
                    float parallel =
                        Mathf.Abs(
                            Vector2.Dot(
                                moving.Direction,
                                target.Direction));

                    if (parallel < ParallelDotThreshold)
                        continue;

                    Vector2 toTarget =
                        target.Midpoint - moving.Midpoint;

                    float distance = toTarget.magnitude;

                    if (distance < 0.0001f)
                        continue;

                    Vector2 directionToTarget =
                        toTarget / distance;

                    if (Vector2.Dot(
                            moving.outward,
                            directionToTarget) < FacingDotThreshold)
                    {
                        continue;
                    }

                    if (Vector2.Dot(
                            target.outward,
                            -directionToTarget) < FacingDotThreshold)
                    {
                        continue;
                    }

                    if (Vector2.Dot(
                            moving.outward,
                            target.outward) > -0.50f)
                    {
                        continue;
                    }

                    // Nearest sensible facing pair wins.
                    // A small length mismatch penalty helps prefer similarly sized
                    // connection edges without preventing 1m -> 4m center snaps.
                    float lengthPenalty =
                        Mathf.Abs(moving.Length - target.Length) * 0.05f;

                    float score =
                        distance + lengthPenalty;

                    if (score >= bestScore)
                        continue;

                    bestScore = score;
                    movingBest = moving;
                    targetBest = target;
                    found = true;
                }
            }

            return found;
        }

        // ============================================================
        // Shared helpers
        // ============================================================

        public static string GetChannel(string socketName)
        {
            if (string.IsNullOrWhiteSpace(socketName))
                return string.Empty;

            string trimmed =
                socketName.StartsWith(
                    SocketPrefix,
                    StringComparison.OrdinalIgnoreCase)
                    ? socketName.Substring(SocketPrefix.Length)
                    : socketName;

            int separator = trimmed.IndexOf('_');

            return separator >= 0
                ? trimmed.Substring(0, separator)
                : trimmed;
        }

        public static bool IsSocket(Transform transform)
        {
            return transform != null &&
                   transform.name.StartsWith(
                       SocketPrefix,
                       StringComparison.OrdinalIgnoreCase);
        }

        public static Transform GetModuleRootForSocket(Transform socket)
        {
            if (socket == null)
                return null;

            GameObject prefabRoot =
                PrefabUtility.GetNearestPrefabInstanceRoot(
                    socket.gameObject);

            if (prefabRoot != null)
                return prefabRoot.transform;

            return socket.parent;
        }
    }
}
