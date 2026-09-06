using UnityEngine;
using static UnityEngine.Object;

public sealed class PreviewStageContent
{
    private Camera previewCamera;

    private Transform previewSpawn;

    private float framingPadding = 1.25f;

    private GameObject currentInstance;
    private GameObject currentPreviewPrefab;
    private WeaponInstance currentWeaponInstance;

    private MenuPreviewSettings currentSettings;

    public GameObject CurrentInstance => currentInstance;

    public PreviewStageContent(Camera camera, Transform spawn, float padding = 1.25f)
    {
        previewCamera = camera;
        previewSpawn = spawn;
        framingPadding = padding;
    }

    // =====================================================
    // SINGLE ITEM PREVIEW
    // =====================================================

    public void Show(GameObject prefab)
    {
        if (!SpawnRoot(prefab))
            return;

        FrameCurrent();
    }

    // =====================================================
    // COMBINED LOADOUT PREVIEW
    // =====================================================

    public void ShowLoadout(GameObject characterPreviewPrefab, WeaponItemData weapon)
    {
        if (characterPreviewPrefab == null)
        {
            Clear();
            return;
        }

        bool characterChanged =
            currentInstance == null || currentPreviewPrefab != characterPreviewPrefab;

        if (characterChanged)
        {
            if (!SpawnRoot(characterPreviewPrefab))
                return;

            // Frame ONLY the character before a weapon becomes its child.
            //
            // This keeps Amy/Granny in exactly the same screen position
            // regardless of whether NONE, a pistol, rifle, etc. is selected.
            FrameCurrent();
        }

        // If only the weapon changed, keep the existing character instance,
        // animator state and camera exactly as they are.
        ReplaceLoadoutWeapon(weapon);
    }

    public bool SpawnRoot(GameObject prefab, bool applyMenuSettings = true)
    {
        Clear();

        if (prefab == null || previewSpawn == null || previewCamera == null)
        {
            return false;
        }

        currentPreviewPrefab = prefab;

        currentInstance = Instantiate(prefab, previewSpawn);

        currentInstance.name = prefab.name;

        Transform model = currentInstance.transform;

        model.localPosition = Vector3.zero;

        model.localRotation = Quaternion.identity;

        model.localScale = Vector3.one;

        currentSettings = applyMenuSettings
            ? currentInstance.GetComponent<MenuPreviewSettings>()
            : null;

        if (currentSettings != null)
        {
            model.localPosition = currentSettings.localOffset;

            model.localRotation = Quaternion.Euler(currentSettings.localEulerAngles);

            model.localScale *= currentSettings.scaleMultiplier;
        }

        return true;
    }

    private void ReplaceLoadoutWeapon(WeaponItemData weapon)
    {
        if (currentWeaponInstance != null)
        {
            // Disable immediately so there is never one rendered frame with
            // both the old and new weapon present.
            currentWeaponInstance.gameObject.SetActive(false);

            DestroyPreviewObject(currentWeaponInstance.gameObject);

            currentWeaponInstance = null;
        }

        // NONE is a completely valid selected state.
        if (weapon == null)
            return;

        if (currentInstance == null)
            return;

        CharacterVisual characterVisual = currentInstance.GetComponentInChildren<CharacterVisual>(
            true
        );

        if (characterVisual == null || !characterVisual.HasWeaponSocket)
        {
            Debug.LogWarning(
                $"Menu preview character '{currentInstance.name}' has no usable CharacterVisual/WeaponSocket."
            );
            return;
        }

        currentWeaponInstance = WeaponInstance.SpawnAttached(weapon, characterVisual);
    }

    // =====================================================
    // CLEAR / ROTATE / FRAME
    // =====================================================

    public void Clear()
    {
        if (currentInstance != null)
        {
            DestroyPreviewObject(currentInstance);
        }
        else if (currentWeaponInstance != null)
        {
            // Normally the weapon is a child of currentInstance, but keep
            // this safe if that ever changes later.
            DestroyPreviewObject(currentWeaponInstance.gameObject);
        }

        currentInstance = null;
        currentPreviewPrefab = null;
        currentWeaponInstance = null;
        currentSettings = null;
    }

    public void RotateCurrent(float pointerDeltaX)
    {
        if (currentInstance == null)
            return;

        float sensitivity = currentSettings != null ? currentSettings.rotationSensitivity : 0.25f;

        currentInstance.transform.Rotate(Vector3.up, -pointerDeltaX * sensitivity, Space.World);
    }

    public void FrameCurrent(
        float extraDistanceMultiplier = 1f,
        Vector3 extraTargetOffset = default
    )
    {
        if (currentInstance == null || previewCamera == null)
        {
            return;
        }

        bool hasBounds = TryGetModelBounds(currentInstance, out Bounds bounds);

        if (!hasBounds)
        {
            Debug.LogWarning(
                $"Menu preview '{currentInstance.name}' has no MeshRenderer or SkinnedMeshRenderer to frame.",
                currentInstance
            );

            return;
        }

        Vector3 target = bounds.center + extraTargetOffset;

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

        distance *= framingPadding * distanceMultiplier * extraDistanceMultiplier;

        distance = Mathf.Max(distance, 0.25f);

        previewCamera.transform.position = target + Vector3.back * distance;

        previewCamera.transform.rotation = Quaternion.LookRotation(
            target - previewCamera.transform.position,
            Vector3.up
        );
    }

    public static bool TryGetModelBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return false;

        bool hasBounds = false;
        bounds = default;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !IsFramingRenderer(renderer))
            {
                continue;
            }

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
        return hasBounds;
    }

    private static void DestroyPreviewObject(GameObject target)
    {
        if (target == null)
            return;
        target.SetActive(false);
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private static bool IsFramingRenderer(Renderer renderer)
    {
        // Only real model geometry should influence preview camera framing.
        //
        // Runtime electric arcs are LineRenderers. If included, their bounds
        // can become much larger than the grenade itself and push the preview
        // camera far away, making the grenade appear as a tiny dot.
        //
        // Particle/trail effects are ignored for the same reason.
        return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
    }
}
