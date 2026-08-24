using UnityEngine;

public class PlasmaCoreController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Transform visualRoot;

    [SerializeField]
    private Renderer coreRenderer;

    [SerializeField]
    private PlasmaArcController arcController;

    [Header("Fallback")]
    [SerializeField]
    private Color defaultAuraColor = Color.magenta;

    public void Configure(PlasmaCoreConfig config, Color? auraColor = null)
    {
        Color color = auraColor ?? defaultAuraColor;

        if (visualRoot != null)
            visualRoot.localScale = config.size;

        SetAuraColor(color);

        if (arcController != null)
        {
            arcController.Configure(
                config.minArcs,
                config.maxArcs,
                config.segments,
                config.arcLength,
                config.jitter,
                config.refreshRate,
                config.arcWidth,
                color
            );
        }
    }

    public void SetAuraColor(Color color)
    {
        if (coreRenderer == null)
            return;

        MaterialPropertyBlock block = new();
        coreRenderer.GetPropertyBlock(block);

        block.SetColor("_BaseColor", color);
        block.SetColor("_EmissionColor", color * 3f);

        coreRenderer.SetPropertyBlock(block);
    }
}
