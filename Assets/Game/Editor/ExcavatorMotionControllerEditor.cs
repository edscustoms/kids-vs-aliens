using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(ExcavatorMotionController))]
public sealed class ExcavatorMotionControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var motion = (ExcavatorMotionController)target;
        EditorGUILayout.HelpBox("Pure upper-house rotation. Impact starts the authored blocker fall; completion waits for clearance. No automatic start or persistence. Reset previews before saving the scene.", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Capture Current As Start")) Edit(motion, motion.CaptureCurrentAsStart);
            if (GUILayout.Button("Preview Half Swing")) Edit(motion, () => motion.PreviewProgress(motion.Duration * .5f));
            if (GUILayout.Button("Preview Impact")) Edit(motion, () => motion.PreviewProgress(motion.ImpactTime));
            if (GUILayout.Button("Preview Impact + 0.15 seconds")) Edit(motion, () => motion.PreviewProgress(motion.ImpactTime + .15f));
            if (GUILayout.Button("Preview Target / Cleared Exit")) Edit(motion, () => motion.PreviewProgress(motion.CompletionTime));
        }
        if (GUILayout.Button("Reset To Start")) Edit(motion, motion.ResetToStart);
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
            if (GUILayout.Button("Play Test Swing")) motion.PlaySwing();
        if (Application.isPlaying) EditorGUILayout.LabelField("State", motion.State.ToString());
    }

    private static void Edit(ExcavatorMotionController motion, System.Action action)
    {
        if (!Application.isPlaying)
        {
            Undo.RecordObject(motion, "Author excavator swing");
            if (motion.UpperPivot != null) Undo.RegisterFullObjectHierarchyUndo(motion.UpperPivot.gameObject, "Preview excavator swing");
            if (motion.Blocker != null) Undo.RegisterFullObjectHierarchyUndo(motion.Blocker.gameObject, "Preview blocker fall");
        }
        action();
        if (Application.isPlaying) return;
        EditorUtility.SetDirty(motion);
        if (motion.UpperPivot != null) PrefabUtility.RecordPrefabInstancePropertyModifications(motion.UpperPivot);
        if (motion.Blocker != null)
        {
            foreach (var child in motion.Blocker.GetComponentsInChildren<Transform>())
                PrefabUtility.RecordPrefabInstancePropertyModifications(child);
            foreach (var collider in motion.Blocker.GetComponentsInChildren<Collider>())
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }
        EditorSceneManager.MarkSceneDirty(motion.gameObject.scene);
    }
}
