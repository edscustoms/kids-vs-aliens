using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools.ModularLevelKit
{
    internal static class ModularAlignmentUtility
    {
        public enum WorldAxis
        {
            X,
            Y,
            Z,
        }

        public static bool AlignSelectionToTargetPivot(
            IReadOnlyList<GameObject> movingObjects,
            IReadOnlyList<GameObject> targetObjects,
            WorldAxis axis,
            out string message)
        {
            message = string.Empty;

            List<GameObject> movingRoots =
                ModularSnapUtility.GetTopLevelObjects(movingObjects);

            List<GameObject> targetRoots =
                ModularSnapUtility.GetTopLevelObjects(targetObjects);

            if (movingRoots.Count == 0)
            {
                message = "Select at least one moving object.";
                return false;
            }

            if (targetRoots.Count == 0)
            {
                message = "Target is not set.";
                return false;
            }

            if (SelectionsOverlap(movingRoots, targetRoots))
            {
                message = "Moving selection overlaps the stored target.";
                return false;
            }

            Vector3 referencePosition = Vector3.zero;

            foreach (GameObject target in targetRoots)
                referencePosition += target.transform.position;

            referencePosition /= targetRoots.Count;

            Undo.RecordObjects(
                movingRoots
                    .Select(go => (Object)go.transform)
                    .ToArray(),
                $"Align Selection {axis}");

            foreach (GameObject moving in movingRoots)
            {
                Vector3 position = moving.transform.position;

                switch (axis)
                {
                    case WorldAxis.X:
                        position.x = referencePosition.x;
                        break;

                    case WorldAxis.Y:
                        position.y = referencePosition.y;
                        break;

                    case WorldAxis.Z:
                        position.z = referencePosition.z;
                        break;
                }

                moving.transform.position = position;
                EditorUtility.SetDirty(moving.transform);
            }

            message =
                $"Aligned {movingRoots.Count} object(s) on world {axis}.";

            return true;
        }

        public static bool SnapBottomToTargetSurface(
            IReadOnlyList<GameObject> movingObjects,
            IReadOnlyList<GameObject> targetObjects,
            out string message)
        {
            message = string.Empty;

            List<GameObject> movingRoots =
                ModularSnapUtility.GetTopLevelObjects(movingObjects);

            List<GameObject> targetRoots =
                ModularSnapUtility.GetTopLevelObjects(targetObjects);

            if (movingRoots.Count == 0)
            {
                message = "Select at least one moving object.";
                return false;
            }

            if (targetRoots.Count == 0)
            {
                message = "Target is not set.";
                return false;
            }

            if (SelectionsOverlap(movingRoots, targetRoots))
            {
                message = "Moving selection overlaps the stored target.";
                return false;
            }

            List<Collider> targetColliders =
                CollectTargetColliders(targetRoots);

            bool hasTargetBounds =
                TryGetCombinedBounds(targetRoots, out Bounds targetBounds);

            if (targetColliders.Count == 0 && !hasTargetBounds)
            {
                message =
                    "Stored target has no usable Collider or Renderer bounds.";
                return false;
            }

            Physics.SyncTransforms();

            int snappedCount = 0;
            int fallbackCount = 0;
            int failedCount = 0;

            Undo.RecordObjects(
                movingRoots
                    .Select(go => (Object)go.transform)
                    .ToArray(),
                "Snap Bottom To Target Surface");

            foreach (GameObject moving in movingRoots)
            {
                if (!TryGetCombinedBounds(
                        new[] { moving },
                        out Bounds movingBounds))
                {
                    failedCount++;
                    continue;
                }

                float? surfaceY =
                    FindSurfaceYBelowXZ(
                        movingBounds.center.x,
                        movingBounds.center.z,
                        movingBounds,
                        targetColliders,
                        hasTargetBounds ? targetBounds : default,
                        hasTargetBounds);

                if (!surfaceY.HasValue)
                {
                    failedCount++;
                    continue;
                }

                float deltaY =
                    surfaceY.Value - movingBounds.min.y;

                Vector3 position =
                    moving.transform.position;

                position.y += deltaY;
                moving.transform.position = position;

                EditorUtility.SetDirty(moving.transform);

                snappedCount++;

                if (targetColliders.Count == 0)
                    fallbackCount++;
            }

            Physics.SyncTransforms();

            if (snappedCount == 0)
            {
                message =
                    "Nothing could be snapped. Make sure the moving objects are above the stored target surface.";
                return false;
            }

            message =
                $"Snapped {snappedCount} object(s) bottom-to-surface.";

            if (failedCount > 0)
                message += $" {failedCount} object(s) could not resolve a target surface.";

            if (fallbackCount > 0)
                message += " Renderer-bounds fallback was used.";

            return true;
        }

        private static float? FindSurfaceYBelowXZ(
            float x,
            float z,
            Bounds movingBounds,
            IReadOnlyList<Collider> targetColliders,
            Bounds targetBounds,
            bool hasTargetBounds)
        {
            float highestKnownY = movingBounds.max.y;

            if (hasTargetBounds)
                highestKnownY = Mathf.Max(highestKnownY, targetBounds.max.y);

            float rayOriginY = highestKnownY + 100f;

            Ray ray =
                new Ray(
                    new Vector3(x, rayOriginY, z),
                    Vector3.down);

            float rayDistance = 1000f;

            bool foundHit = false;
            float bestY = float.NegativeInfinity;

            foreach (Collider collider in targetColliders)
            {
                if (collider == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!collider.Raycast(
                        ray,
                        out RaycastHit hit,
                        rayDistance))
                {
                    continue;
                }

                if (!foundHit || hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                    foundHit = true;
                }
            }

            if (foundHit)
                return bestY;

            // Useful fallback for simple flat reference objects that have no collider.
            if (hasTargetBounds &&
                x >= targetBounds.min.x &&
                x <= targetBounds.max.x &&
                z >= targetBounds.min.z &&
                z <= targetBounds.max.z)
            {
                return targetBounds.max.y;
            }

            return null;
        }

        private static List<Collider> CollectTargetColliders(
            IReadOnlyList<GameObject> targetRoots)
        {
            var colliders = new List<Collider>();

            foreach (GameObject target in targetRoots)
            {
                if (target == null)
                    continue;

                foreach (Collider collider in
                         target.GetComponentsInChildren<Collider>(true))
                {
                    if (collider != null &&
                        !colliders.Contains(collider))
                    {
                        colliders.Add(collider);
                    }
                }
            }

            return colliders;
        }

        private static bool TryGetCombinedBounds(
            IReadOnlyList<GameObject> roots,
            out Bounds combinedBounds)
        {
            combinedBounds = default;
            bool hasBounds = false;

            foreach (GameObject root in roots)
            {
                if (root == null)
                    continue;

                Renderer[] renderers =
                    root.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;

                    if (!hasBounds)
                    {
                        combinedBounds = renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            if (hasBounds)
                return true;

            foreach (GameObject root in roots)
            {
                if (root == null)
                    continue;

                Collider[] colliders =
                    root.GetComponentsInChildren<Collider>(true);

                foreach (Collider collider in colliders)
                {
                    if (collider == null)
                        continue;

                    if (!hasBounds)
                    {
                        combinedBounds = collider.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(collider.bounds);
                    }
                }
            }

            return hasBounds;
        }

        private static bool SelectionsOverlap(
            IReadOnlyList<GameObject> movingRoots,
            IReadOnlyList<GameObject> targetRoots)
        {
            foreach (GameObject moving in movingRoots)
            {
                foreach (GameObject target in targetRoots)
                {
                    if (moving == null || target == null)
                        continue;

                    if (moving == target)
                        return true;

                    if (moving.transform.IsChildOf(target.transform))
                        return true;

                    if (target.transform.IsChildOf(moving.transform))
                        return true;
                }
            }

            return false;
        }
    }
}
