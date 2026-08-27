using UnityEngine;

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

    private GameObject currentInstance;
    private MenuPreviewSettings currentSettings;

    public GameObject CurrentInstance => currentInstance;

    private void Start()
    {
        if (initialPreviewPrefab != null)
        {
            Show(initialPreviewPrefab);
        }
    }

    public void Show(GameObject prefab)
    {
        Clear();

        if (prefab == null || previewSpawn == null || previewCamera == null)
            return;

        currentInstance = Instantiate(prefab, previewSpawn);
        currentInstance.name = prefab.name;

        Transform model = currentInstance.transform;

        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;

        currentSettings = currentInstance.GetComponent<MenuPreviewSettings>();

        if (currentSettings != null)
        {
            model.localPosition = currentSettings.localOffset;

            model.localRotation = Quaternion.Euler(currentSettings.localEulerAngles);

            model.localScale *= currentSettings.scaleMultiplier;
        }

        FrameCurrent();
    }

    public void Clear()
    {
        if (currentInstance != null)
        {
            Destroy(currentInstance);
        }

        currentInstance = null;
        currentSettings = null;
    }

    public void RotateCurrent(float pointerDeltaX)
    {
        if (currentInstance == null)
            return;

        float sensitivity = currentSettings != null ? currentSettings.rotationSensitivity : 0.25f;

        currentInstance.transform.Rotate(Vector3.up, -pointerDeltaX * sensitivity, Space.World);
    }

    public void FrameCurrent()
    {
        if (currentInstance == null || previewCamera == null)
            return;

        Renderer[] renderers = currentInstance.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return;

        bool hasBounds = false;
        Bounds bounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (!renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            return;

        Vector3 target = bounds.center;
        float distanceMultiplier = 1f;

        if (currentSettings != null)
        {
            target += currentSettings.cameraTargetOffset;
            distanceMultiplier = currentSettings.cameraDistanceMultiplier;
        }

        float verticalFov = previewCamera.fieldOfView * Mathf.Deg2Rad;

        float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f) * previewCamera.aspect);

        float verticalDistance =
            bounds.extents.y / Mathf.Max(0.001f, Mathf.Tan(verticalFov * 0.5f));

        float horizontalDistance =
            bounds.extents.x / Mathf.Max(0.001f, Mathf.Tan(horizontalFov * 0.5f));

        float distance = Mathf.Max(verticalDistance, horizontalDistance);

        distance += bounds.extents.z;

        distance *= framingPadding * distanceMultiplier;

        distance = Mathf.Max(distance, 0.25f);

        previewCamera.transform.position = target + Vector3.back * distance;

        previewCamera.transform.rotation = Quaternion.LookRotation(
            target - previewCamera.transform.position,
            Vector3.up
        );
    }
}
