using KidsVsAliens.Environment;
using UnityEditor;
using UnityEngine;

namespace KidsVsAliens.EditorTools.Fence
{
    [CustomEditor(typeof(FenceRun))]
    public sealed class FenceRunEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            FenceRun run =
                (FenceRun)target;

            EditorGUILayout.Space(8);

            EditorGUILayout.HelpBox(
                "New Section Length affects future extensions. Existing pole positions stay where they are. " +
                "Height/material/style changes rebuild the whole run.",
                MessageType.Info);

            if (GUILayout.Button("Rebuild Fence Run"))
            {
                run.RebuildAll();
                SceneView.RepaintAll();
            }
        }
    }
}
