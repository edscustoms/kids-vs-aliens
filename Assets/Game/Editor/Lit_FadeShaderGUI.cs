using System;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace KidsVsAliens.Editor
{
    // URP's LitShader inspector is internal. Delegate to it rather than duplicating
    // its blend/keyword/upgrade logic or maintaining a second Lit inspector.
    public sealed class Lit_FadeShaderGUI : ShaderGUI
    {
        private readonly ShaderGUI lit = (ShaderGUI)Activator.CreateInstance(
            typeof(BaseShaderGUI).Assembly.GetType(
                "UnityEditor.Rendering.Universal.ShaderGUI.LitShader", true), true);

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            lit.OnGUI(editor, properties);
            EditorGUILayout.Space();
            editor.ShaderProperty(FindProperty("_Fade", properties),
                new GUIContent("Fade", "Visibility multiplier; also controlled by occlusion via MaterialPropertyBlock."));
        }

        public override void ValidateMaterial(Material material) => lit.ValidateMaterial(material);
        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
            => lit.AssignNewShaderToMaterial(material, oldShader, newShader);
        public override void OnClosed(Material material) => lit.OnClosed(material);
        public override void OnMaterialPreviewGUI(MaterialEditor editor, Rect rect, GUIStyle background)
            => lit.OnMaterialPreviewGUI(editor, rect, background);
        public override void OnMaterialInteractivePreviewGUI(MaterialEditor editor, Rect rect, GUIStyle background)
            => lit.OnMaterialInteractivePreviewGUI(editor, rect, background);
        public override void OnMaterialPreviewSettingsGUI(MaterialEditor editor)
            => lit.OnMaterialPreviewSettingsGUI(editor);
    }
}
