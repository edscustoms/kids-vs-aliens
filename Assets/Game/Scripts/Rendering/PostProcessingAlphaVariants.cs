using UnityEngine.Rendering.Universal;

/// <summary>
/// Build-only keyword policy. URP 6000.5 selects alpha ON from the pipeline
/// capability, but opaque display targets request OFF. Keep both without
/// disabling alpha output for cameras that need it. Adds no render passes.
/// </summary>
[DisallowMultipleRendererFeature("Post-processing alpha variants")]
public sealed class PostProcessingAlphaVariants : ScriptableRendererFeature
{
#if UNITY_EDITOR
    [UnityEditor.ShaderKeywordFilter.SelectIf(true, overridePriority: true,
        keywordNames: new[] { "", "_ENABLE_ALPHA_OUTPUT" })]
    private const bool KeepBothAlphaOutputStates = true;
#endif
    public override void Create() { }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }
}
