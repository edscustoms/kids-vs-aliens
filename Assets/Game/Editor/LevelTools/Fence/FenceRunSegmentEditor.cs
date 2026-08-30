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
                $"New section length: {segment.Owner.NewSectionLength:0.##} m",
                EditorStyles.miniLabel);

            EditorGUILayout.LabelField(
                "Arrows are relative to the selected fence segment.",
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
                "Extend From LEFT / A Pole");

            DrawEndpointButtons(
                segment,
                false,
                "Extend From RIGHT / B Pole");
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

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        fromNodeA
                            ? "← Straight"
                            : "Straight →"))
                {
                    SmartFenceEditorUtility.ExtendExisting(
                        segment,
                        fromNodeA,
                        FenceExtendDirection.Straight,
                        count);

                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("↑ Turn"))
                {
                    SmartFenceEditorUtility.ExtendExisting(
                        segment,
                        fromNodeA,
                        FenceExtendDirection.TurnUp,
                        count);

                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("↓ Turn"))
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
