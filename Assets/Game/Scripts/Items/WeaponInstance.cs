using UnityEngine;

public class WeaponInstance : MonoBehaviour
{
    [Header("Attachment")]
    [SerializeField]
    private Transform gripPoint;

    [Header("Gameplay")]
    [SerializeField]
    private Transform muzzle;

    [Header("Optional Visual Setup")]
    [SerializeField]
    private PlasmaCoreSetup plasmaCoreSetup;

    public Transform GripPoint => gripPoint;
    public Transform Muzzle => muzzle;

    public bool AttachTo(CharacterVisual characterVisual)
    {
        if (characterVisual == null || !characterVisual.HasWeaponSocket)
        {
            Debug.LogError(
                $"{name}: Cannot attach weapon because the character has no WeaponSocket."
            );
            return false;
        }

        if (gripPoint == null)
        {
            Debug.LogError($"{name}: WeaponInstance has no GripPoint assigned.");
            return false;
        }

        AlignGripToSocket(transform, gripPoint, characterVisual.WeaponSocket);

        if (plasmaCoreSetup != null)
        {
            plasmaCoreSetup.Configure(characterVisual.AuraColor);
        }

        return true;
    }

    public static WeaponInstance SpawnAttached(
        WeaponItemData weaponData,
        CharacterVisual characterVisual
    )
    {
        if (weaponData == null)
            return null;

        if (weaponData.equippedPrefab == null)
        {
            Debug.LogError($"{weaponData.name}: WeaponItemData has no equippedPrefab.");
            return null;
        }

        GameObject weaponObject = Instantiate(weaponData.equippedPrefab);

        WeaponInstance instance = weaponObject.GetComponent<WeaponInstance>();

        if (instance == null)
        {
            Debug.LogError(
                $"{weaponData.equippedPrefab.name}: Equipped weapon prefab needs a WeaponInstance component on its root."
            );

            Destroy(weaponObject);
            return null;
        }

        if (!instance.AttachTo(characterVisual))
        {
            Destroy(weaponObject);
            return null;
        }

        return instance;
    }

    private static void AlignGripToSocket(
        Transform weaponRoot,
        Transform gripPoint,
        Transform weaponSocket
    )
    {
        Quaternion rotationDelta = weaponSocket.rotation * Quaternion.Inverse(gripPoint.rotation);

        weaponRoot.rotation = rotationDelta * weaponRoot.rotation;

        Vector3 positionDelta = weaponSocket.position - gripPoint.position;

        weaponRoot.position += positionDelta;

        weaponRoot.SetParent(weaponSocket, true);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        AutoAssignReferences();
    }

    private void OnValidate()
    {
        AutoAssignReferences();
    }

    private void AutoAssignReferences()
    {
        if (gripPoint == null)
        {
            gripPoint = FindChildByName(transform, "GripPoint");
        }

        if (muzzle == null)
        {
            muzzle = FindChildByName(transform, "Muzzle");
        }

        if (plasmaCoreSetup == null)
        {
            plasmaCoreSetup = GetComponentInChildren<PlasmaCoreSetup>(true);
        }
    }

    private static Transform FindChildByName(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result = FindChildByName(child, childName);

            if (result != null)
                return result;
        }

        return null;
    }
#endif
}
