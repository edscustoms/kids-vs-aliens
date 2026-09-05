using UnityEngine;

// Keep this component and its serialized fields stable for existing menu scenes.
public class MenuPreviewStage : MonoBehaviour
{
    [Header("Stage")]
    [SerializeField]
    private Camera previewCamera;

    [SerializeField]
    private Transform previewSpawn;

    [Header("Initial Preview")]
    [SerializeField]
    private GameObject initialPreviewPrefab;

    [Header("Auto Framing")]
    [SerializeField]
    private float framingPadding = 1.25f;

    private PreviewStageContent content;
    private PreviewStageContent Content =>
        content ??= new PreviewStageContent(previewCamera, previewSpawn, framingPadding);
    public GameObject CurrentInstance => content?.CurrentInstance;

    private void Start()
    {
        if (initialPreviewPrefab != null)
            Show(initialPreviewPrefab);
    }

    public void Show(GameObject prefab) => Content.Show(prefab);

    public void ShowLoadout(GameObject characterPreviewPrefab, WeaponItemData weapon) =>
        Content.ShowLoadout(characterPreviewPrefab, weapon);

    public void Clear() => content?.Clear();

    public void RotateCurrent(float pointerDeltaX) => Content.RotateCurrent(pointerDeltaX);

    public void FrameCurrent() => Content.FrameCurrent();

    private void OnDestroy() => Clear();
}
