using KidsVsAliens.Environment;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools.Fence
{
    internal static class SmartFenceEditorUtility
    {
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
