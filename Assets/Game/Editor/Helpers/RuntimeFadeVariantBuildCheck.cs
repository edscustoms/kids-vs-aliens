using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Keep the Lit states created by runtime camera fading in player builds.</summary>
public sealed class RuntimeFadeVariantBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report) => Validate();

    public static void Validate()
    {
        // These texture-free Resources materials retain shader_feature combinations.
        // URP still strips unsupported pipeline features; no shader is Always Included.
        Check("Lit Transparent");
        Check("Lit Transparent Mapped", "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP");
    }

    private static void Check(string name, params string[] mappedFeatures)
    {
        var material = Resources.Load<Material>("RenderingVariants/" + name);
        if (material == null || material.shader == null
            || material.shader.name != "Universal Render Pipeline/Lit"
            || !material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
            throw new BuildFailedException("Missing runtime fade retention material: " + name);
        foreach (string keyword in mappedFeatures)
            if (!material.IsKeywordEnabled(keyword))
                throw new BuildFailedException(name + " must retain " + keyword + " for runtime camera fading.");
    }
}
