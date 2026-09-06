using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillXpDebugTester))]
public sealed class SkillXpDebugTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SkillXpDebugTester tester =
            (SkillXpDebugTester)target;

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(
                   !Application.isPlaying))
        {
            string skillName =
                tester.Skill != null
                    ? tester.Skill.DisplayName
                    : "Skill";

            if (GUILayout.Button(
                    $"+{tester.XpPerClick} XP → {skillName}",
                    GUILayout.Height(30)))
            {
                tester.AddXp();
            }

            if (GUILayout.Button(
                    "Print Skill Progress"))
            {
                tester.PrintProgress();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to use the XP debug buttons.",
                MessageType.Info);
        }
    }
}
