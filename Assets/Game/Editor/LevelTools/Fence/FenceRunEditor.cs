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
                "Pole Spacing is LIVE for the whole run. Changing it repositions all existing poles/segments while preserving turns, shared poles and topology.",
                MessageType.Info);

            if (GUILayout.Button("Rebuild Fence Run"))
            {
                run.RebuildAll();
                SceneView.RepaintAll();
            }
        }
    }
}
