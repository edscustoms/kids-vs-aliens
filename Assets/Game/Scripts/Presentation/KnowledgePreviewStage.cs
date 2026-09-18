using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Gameplay instance of the shared preview implementation. Owns its transient
// rendering resources; menu clients retain their existing camera/texture setup.
public sealed class KnowledgePreviewStage : MonoBehaviour
{
    [SerializeField]
    private Camera previewCamera;

    [SerializeField]
    private Light previewLight;

    [SerializeField]
    private Transform previewRoot;

    [SerializeField]
    private RawImage output;

    [SerializeField]
    private ScriptableRendererData lightweightRenderer;

    [SerializeField, Range(8, 31)]
    private int previewLayer = 9;

    [SerializeField, Range(128, 1024)]
    private int textureSize = 512;
    [SerializeField] private Material floorMaterial;
    private GameObject presentationFloor;
    private PreviewStageContent content;
    private RenderTexture texture;

    public GameObject Actor => content?.CurrentInstance;
    public Transform StagingRoot => previewRoot;
    public int PreviewLayer => previewLayer;
    public bool IsRendering => previewCamera != null && previewCamera.enabled;

    // Resolve per pipeline, since desktop/mobile can use different indices.
    // Fail closed if setup is missing or someone adds gameplay features later.
    public bool TrySelectRenderer(UniversalRenderPipelineAsset pipeline)
    {
        if (
            pipeline == null
            || lightweightRenderer == null
            || previewCamera == null
            || lightweightRenderer.rendererFeatures.Count != 0
        )
            return false;
        var renderers = pipeline.rendererDataList;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != lightweightRenderer)
                continue;
            previewCamera.GetUniversalAdditionalCameraData().SetRenderer(i);
            return true;
        }
        return false;
    }

    private void Awake() => SetVisible(false);

    public bool Prepare(GameObject prefab)
    {
        Clear();
        if (previewCamera == null || previewRoot == null || prefab == null)
            return false;
        previewRoot.gameObject.SetActive(false);
        content ??= new PreviewStageContent(previewCamera, previewRoot, 1.25f);
        if (!content.SpawnRoot(prefab, false))
            return false;
        Actor.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        return true;
    }

    public void ActivateActor() => previewRoot.gameObject.SetActive(true);

    public void Show(float distanceMultiplier, Vector3 targetOffset)
    {
        if (Actor == null)
            return;
        if (
            !TrySelectRenderer(
                GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset
            )
        )
        {
            Debug.LogWarning(
                "Knowledge preview needs a feature-free renderer in the active URP asset. Rerun presentation setup.",
                this
            );
            Clear();
            return;
        }
        if (texture == null)
        {
            texture = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "Knowledge Preview (runtime)",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
            };
            texture.Create();
        }
        previewCamera.targetTexture = texture;
        previewCamera.aspect = 1f;
        previewCamera.cullingMask = 1 << previewLayer;
        if (output != null)
            output.texture = texture;
        content.FrameCurrent(Mathf.Max(0.1f, distanceMultiplier), targetOffset);
        if (floorMaterial != null && presentationFloor == null)
        {
            presentationFloor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            presentationFloor.name = "Tutorial Floor";
            Destroy(presentationFloor.GetComponent<Collider>());
            presentationFloor.transform.SetParent(previewRoot, false);
            presentationFloor.transform.localPosition = new Vector3(0, -.025f, 0);
            presentationFloor.transform.localRotation = Quaternion.Euler(90,0,0);
            presentationFloor.transform.localScale = Vector3.one * 6;
            presentationFloor.layer = previewLayer;
            presentationFloor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        }
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (previewCamera != null)
            previewCamera.enabled = visible;
        if (previewLight != null)
            previewLight.enabled = visible;
        if (output != null)
            output.enabled = visible;
        if (!visible && previewRoot != null)
            previewRoot.gameObject.SetActive(false);
    }

    public void Clear()
    {
        SetVisible(false);
        content?.Clear();
        if (previewCamera != null)
            previewCamera.targetTexture = null;
        if (output != null)
            output.texture = null;
        if (texture != null)
        {
            texture.Release();
            if (Application.isPlaying)
                Destroy(texture);
            else
                DestroyImmediate(texture);
            texture = null;
        }
    }

    private void OnDisable() => Clear();

    private void OnDestroy() => Clear();
}
