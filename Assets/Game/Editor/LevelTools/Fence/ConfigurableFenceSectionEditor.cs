using KidsVsAliens.Environment;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools.Fence
{
    [CustomEditor(typeof(ConfigurableFenceSection))]
    public sealed class ConfigurableFenceSectionEditor : Editor
    {
        private int count = 1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ConfigurableFenceSection section =
                (ConfigurableFenceSection)target;

            if (EditorUtility.IsPersistent(section) ||
                !section.gameObject.scene.IsValid())
            {
                return;
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField(
                "Smart Fence Linking",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "The first extension converts this standalone section into a Fence Run. " +
                "Shared endpoints become ONE pole, so 2 sections = 3 poles.",
                MessageType.Info);

            count =
                Mathf.Max(
                    1,
                    EditorGUILayout.IntField(
                        "How Many",
                        count));

            DrawEndpointButtons(
                section,
                true,
                "Extend From LEFT Pole");

            DrawEndpointButtons(
                section,
                false,
                "Extend From RIGHT Pole");
        }

        private void DrawEndpointButtons(
            ConfigurableFenceSection section,
            bool fromLeft,
            string label)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(
                label,
                EditorStyles.miniBoldLabel);

            string straightArrow =
                SmartFenceEditorUtility.GetStandaloneDirectionLabel(
                    section,
                    fromLeft,
                    FenceExtendDirection.Straight);

            string turnUpArrow =
                SmartFenceEditorUtility.GetStandaloneDirectionLabel(
                    section,
                    fromLeft,
                    FenceExtendDirection.TurnUp);

            string turnDownArrow =
                SmartFenceEditorUtility.GetStandaloneDirectionLabel(
                    section,
                    fromLeft,
                    FenceExtendDirection.TurnDown);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        $"{straightArrow} Straight"))
                {
                    SmartFenceEditorUtility.ConvertAndExtend(
                        section,
                        fromLeft,
                        FenceExtendDirection.Straight,
                        count);

                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(
                        $"{turnUpArrow} Turn"))
                {
                    SmartFenceEditorUtility.ConvertAndExtend(
                        section,
                        fromLeft,
                        FenceExtendDirection.TurnUp,
                        count);

                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(
                        $"{turnDownArrow} Turn"))
                {
                    SmartFenceEditorUtility.ConvertAndExtend(
                        section,
                        fromLeft,
                        FenceExtendDirection.TurnDown,
                        count);

                    GUIUtility.ExitGUI();
                }
            }
        }
    }
}
