using KidsVsAliens.Environment;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools.Fence
{
    [CustomEditor(typeof(FenceRunSegment))]
    public sealed class FenceRunSegmentEditor : Editor
    {
        private int count = 1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            FenceRunSegment segment =
                (FenceRunSegment)target;

            if (segment.Owner == null)
                return;

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "Smart Fence Linking",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                $"Pole spacing: {segment.Owner.PoleSpacing:0.##} m",
                EditorStyles.miniLabel);

            EditorGUILayout.LabelField(
                "Arrows are projected into the CURRENT Scene view, so they show where the next section will visibly go on screen.",
                EditorStyles.wordWrappedMiniLabel);

            count =
                Mathf.Max(
                    1,
                    EditorGUILayout.IntField(
                        "How Many",
                        count));

            DrawEndpointButtons(
                segment,
                true,
                $"Extend From Pole {segment.NodeA.NodeId:000} (A)");

            DrawEndpointButtons(
                segment,
                false,
                $"Extend From Pole {segment.NodeB.NodeId:000} (B)");
        }

        private void DrawEndpointButtons(
            FenceRunSegment segment,
            bool fromNodeA,
            string label)
        {
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField(
                label,
                EditorStyles.miniBoldLabel);

            string straightArrow =
                SmartFenceEditorUtility.GetSegmentDirectionLabel(
                    segment,
                    fromNodeA,
                    FenceExtendDirection.Straight);

            string turnUpArrow =
                SmartFenceEditorUtility.GetSegmentDirectionLabel(
                    segment,
                    fromNodeA,
                    FenceExtendDirection.TurnUp);

            string turnDownArrow =
                SmartFenceEditorUtility.GetSegmentDirectionLabel(
                    segment,
                    fromNodeA,
                    FenceExtendDirection.TurnDown);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        $"{straightArrow} Straight"))
                {
                    SmartFenceEditorUtility.ExtendExisting(
                        segment,
                        fromNodeA,
                        FenceExtendDirection.Straight,
                        count);

                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(
                        $"{turnUpArrow} Turn"))
                {
                    SmartFenceEditorUtility.ExtendExisting(
                        segment,
                        fromNodeA,
                        FenceExtendDirection.TurnUp,
                        count);

                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(
                        $"{turnDownArrow} Turn"))
                {
                    SmartFenceEditorUtility.ExtendExisting(
                        segment,
                        fromNodeA,
                        FenceExtendDirection.TurnDown,
                        count);

                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
