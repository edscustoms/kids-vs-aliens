using KidsVsAliens.Environment;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools.Fence
{
    internal static class SmartFenceEditorUtility
    {

        public static string GetStandaloneDirectionLabel(
            ConfigurableFenceSection section,
            bool fromLeft,
            FenceExtendDirection direction)
        {
            if (section == null)
                return "?";

            Vector2Int endpoint =
                fromLeft
                    ? new Vector2Int(0, 0)
                    : new Vector2Int(1, 0);

            Vector2Int other =
                fromLeft
                    ? new Vector2Int(1, 0)
                    : new Vector2Int(0, 0);

            Vector2Int outward =
                endpoint - other;

            Vector2Int result =
                ResolveEditorDirection(
                    outward,
                    direction);

            Vector3 localDirection =
                new Vector3(
                    result.x,
                    0f,
                    result.y);

            Vector3 worldDirection =
                section.transform.TransformDirection(
                    localDirection);

            return ToSceneViewArrow(
                section.transform.position,
                worldDirection);
        }

        public static string GetSegmentDirectionLabel(
            FenceRunSegment segment,
            bool fromNodeA,
            FenceExtendDirection direction)
        {
            if (segment == null ||
                segment.Owner == null ||
                segment.NodeA == null ||
                segment.NodeB == null)
            {
                return "?";
            }

            FencePoleNode endpoint =
                fromNodeA
                    ? segment.NodeA
                    : segment.NodeB;

            FencePoleNode other =
                fromNodeA
                    ? segment.NodeB
                    : segment.NodeA;

            Vector2Int outward =
                endpoint.GridCoordinate -
                other.GridCoordinate;

            Vector2Int result =
                ResolveEditorDirection(
                    outward,
                    direction);

            Vector3 localDirection =
                new Vector3(
                    result.x,
                    0f,
                    result.y);

            Vector3 worldDirection =
                segment.Owner.transform.TransformDirection(
                    localDirection);

            return ToSceneViewArrow(
                endpoint.transform.position,
                worldDirection);
        }

        private static Vector2Int ResolveEditorDirection(
            Vector2Int outward,
            FenceExtendDirection direction)
        {
            outward =
                NormalizeCardinal(outward);

            switch (direction)
            {
                case FenceExtendDirection.TurnUp:
                    return new Vector2Int(
                        -outward.y,
                        outward.x);

                case FenceExtendDirection.TurnDown:
                    return new Vector2Int(
                        outward.y,
                        -outward.x);

                default:
                    return outward;
            }
        }

        private static Vector2Int NormalizeCardinal(
            Vector2Int direction)
        {
            if (Mathf.Abs(direction.x) >=
                Mathf.Abs(direction.y))
            {
                if (direction.x == 0)
                    return Vector2Int.zero;

                return new Vector2Int(
                    direction.x > 0 ? 1 : -1,
                    0);
            }

            if (direction.y == 0)
                return Vector2Int.zero;

            return new Vector2Int(
                0,
                direction.y > 0 ? 1 : -1);
        }

        private static string ToSceneViewArrow(
            Vector3 worldOrigin,
            Vector3 worldDirection)
        {
            if (worldDirection.sqrMagnitude <= Mathf.Epsilon)
                return "?";

            SceneView sceneView =
                SceneView.lastActiveSceneView;

            Camera camera =
                sceneView != null
                    ? sceneView.camera
                    : null;

            if (camera == null)
                return ToWorldAxisArrow(worldDirection);

            Vector3 screenA =
                camera.WorldToScreenPoint(
                    worldOrigin);

            Vector3 screenB =
                camera.WorldToScreenPoint(
                    worldOrigin +
                    worldDirection.normalized);

            Vector2 delta =
                new Vector2(
                    screenB.x - screenA.x,
                    screenB.y - screenA.y);

            if (delta.sqrMagnitude <= 0.0001f)
                return ToWorldAxisArrow(worldDirection);

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return delta.x >= 0f ? "→" : "←";

            return delta.y >= 0f ? "↑" : "↓";
        }

        private static string ToWorldAxisArrow(
            Vector3 worldDirection)
        {
            if (Mathf.Abs(worldDirection.x) >=
                Mathf.Abs(worldDirection.z))
            {
                return worldDirection.x >= 0f
                    ? "→"
                    : "←";
            }

            return worldDirection.z >= 0f
                ? "↑"
                : "↓";
        }

        public static FenceRunSegment ConvertAndExtend(
            ConfigurableFenceSection section,
            bool fromLeft,
            FenceExtendDirection direction,
            int count)
        {
            if (section == null)
                return null;

            Transform sourceTransform =
                section.transform;

            GameObject runObject =
                new GameObject(
                    $"{sourceTransform.name}_Run");

            Undo.RegisterCreatedObjectUndo(
                runObject,
                "Create Fence Run");

            Transform runTransform =
                runObject.transform;

            runTransform.SetParent(
                sourceTransform.parent,
                false);

            runTransform.SetPositionAndRotation(
                sourceTransform.position,
                sourceTransform.rotation);

            runTransform.localScale =
                sourceTransform.localScale;

            runTransform.SetSiblingIndex(
                sourceTransform.GetSiblingIndex());

            FenceRun run =
                runObject.AddComponent<FenceRun>();

            run.ConfigureFromSection(section);

            FenceRunSegment initial =
                run.CreateInitialSegment();

            FenceRunSegment result =
                run.Extend(
                    initial,
                    fromLeft,
                    direction,
                    count);

            Undo.DestroyObjectImmediate(
                section.gameObject);

            run.RebuildAll();

            Selection.activeGameObject =
                result != null
                    ? result.gameObject
                    : initial.gameObject;

            SceneView.RepaintAll();

            return result ?? initial;
        }

        public static FenceRunSegment ExtendExisting(
            FenceRunSegment segment,
            bool fromNodeA,
            FenceExtendDirection direction,
            int count)
        {
            if (segment == null ||
                segment.Owner == null)
            {
                return null;
            }

            Undo.RegisterFullObjectHierarchyUndo(
                segment.Owner.gameObject,
                "Extend Fence Run");

            FenceRunSegment result =
                segment.Owner.Extend(
                    segment,
                    fromNodeA,
                    direction,
                    count);

            segment.Owner.RebuildAll();

            if (result != null)
                Selection.activeGameObject =
                    result.gameObject;

            SceneView.RepaintAll();

            return result;
        }
    }
}
