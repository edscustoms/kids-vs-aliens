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
    private GameObject currentPreviewPrefab;
    private WeaponInstance currentWeaponInstance;

    private MenuPreviewSettings currentSettings;

    public GameObject CurrentInstance =>
        currentInstance;

    private void Start()
    {
        if (initialPreviewPrefab != null)
        {
            Show(initialPreviewPrefab);
        }
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

    public void ShowLoadout(
        GameObject characterPreviewPrefab,
        WeaponItemData weapon
    )
    {
        if (characterPreviewPrefab == null)
        {
            Clear();
            return;
        }

        bool characterChanged =
            currentInstance == null
            || currentPreviewPrefab != characterPreviewPrefab;

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

    private bool SpawnRoot(
        GameObject prefab
    )
    {
        Clear();

        if (
            prefab == null
            || previewSpawn == null
            || previewCamera == null
        )
        {
            return false;
        }

        currentPreviewPrefab =
            prefab;

        currentInstance =
            Instantiate(
                prefab,
                previewSpawn
            );

        currentInstance.name =
            prefab.name;

        Transform model =
            currentInstance.transform;

        model.localPosition =
            Vector3.zero;

        model.localRotation =
            Quaternion.identity;

        model.localScale =
            Vector3.one;

        currentSettings =
            currentInstance.GetComponent<MenuPreviewSettings>();

        if (currentSettings != null)
        {
            model.localPosition =
                currentSettings.localOffset;

            model.localRotation =
                Quaternion.Euler(
                    currentSettings.localEulerAngles
                );

            model.localScale *=
                currentSettings.scaleMultiplier;
        }

        return true;
    }

    private void ReplaceLoadoutWeapon(
        WeaponItemData weapon
    )
    {
        if (currentWeaponInstance != null)
        {
            // Disable immediately so there is never one rendered frame with
            // both the old and new weapon present.
            currentWeaponInstance.gameObject.SetActive(false);

            Destroy(
                currentWeaponInstance.gameObject
            );

            currentWeaponInstance = null;
        }

        // NONE is a completely valid selected state.
        if (weapon == null)
            return;

        if (currentInstance == null)
            return;

        CharacterVisual characterVisual =
            currentInstance
                .GetComponentInChildren<CharacterVisual>(
                    true
                );

        if (
            characterVisual == null
            || !characterVisual.HasWeaponSocket
        )
        {
            Debug.LogWarning(
                $"Menu preview character '{currentInstance.name}' has no usable CharacterVisual/WeaponSocket."
            );
            return;
        }

        currentWeaponInstance =
            WeaponInstance.SpawnAttached(
                weapon,
                characterVisual
            );
    }

    // =====================================================
    // CLEAR / ROTATE / FRAME
    // =====================================================

    public void Clear()
    {
        if (currentInstance != null)
        {
            Destroy(currentInstance);
        }
        else if (currentWeaponInstance != null)
        {
            // Normally the weapon is a child of currentInstance, but keep
            // this safe if that ever changes later.
            Destroy(
                currentWeaponInstance.gameObject
            );
        }

        currentInstance = null;
        currentPreviewPrefab = null;
        currentWeaponInstance = null;
        currentSettings = null;
    }

    public void RotateCurrent(
        float pointerDeltaX
    )
    {
        if (currentInstance == null)
            return;

        float sensitivity =
            currentSettings != null
                ? currentSettings.rotationSensitivity
                : 0.25f;

        currentInstance.transform.Rotate(
            Vector3.up,
            -pointerDeltaX * sensitivity,
            Space.World
        );
    }

    public void FrameCurrent()
    {
        if (
            currentInstance == null
            || previewCamera == null
        )
        {
            return;
        }

        Renderer[] renderers =
            currentInstance
                .GetComponentsInChildren<Renderer>(
                    true
                );

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
                bounds.Encapsulate(
                    renderer.bounds
                );
            }
        }

        if (!hasBounds)
            return;

        Vector3 target =
            bounds.center;

        float distanceMultiplier =
            1f;

        if (currentSettings != null)
        {
            target +=
                currentSettings.cameraTargetOffset;

            distanceMultiplier =
                currentSettings.cameraDistanceMultiplier;
        }

        float verticalFov =
            previewCamera.fieldOfView
            * Mathf.Deg2Rad;

        float horizontalFov =
            2f
            * Mathf.Atan(
                Mathf.Tan(verticalFov * 0.5f)
                * previewCamera.aspect
            );

        float verticalDistance =
            bounds.extents.y
            / Mathf.Max(
                0.001f,
                Mathf.Tan(verticalFov * 0.5f)
            );

        float horizontalDistance =
            bounds.extents.x
            / Mathf.Max(
                0.001f,
                Mathf.Tan(horizontalFov * 0.5f)
            );

        float distance =
            Mathf.Max(
                verticalDistance,
                horizontalDistance
            );

        distance +=
            bounds.extents.z;

        distance *=
            framingPadding
            * distanceMultiplier;

        distance =
            Mathf.Max(
                distance,
                0.25f
            );

        previewCamera.transform.position =
            target
            + Vector3.back * distance;

        previewCamera.transform.rotation =
            Quaternion.LookRotation(
                target
                    - previewCamera.transform.position,
                Vector3.up
            );
    }

}
