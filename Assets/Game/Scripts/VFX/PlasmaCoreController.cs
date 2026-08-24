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
        if (visualRoot != null)
            visualRoot.localScale = config.size;

        SetAuraColor(auraColor ?? defaultAuraColor);

        if (arcController != null)
        {
            arcController.Configure(
                config.minArcs,
                config.maxArcs,
                config.segments,
                config.arcLength,
                config.jitter,
                config.refreshRate,
                config.arcWidth
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
