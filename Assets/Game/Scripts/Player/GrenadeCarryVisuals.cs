using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GrenadeCarryVisuals : MonoBehaviour
{
    [SerializeField]
    private PlayerInventory inventory;

    [SerializeField]
    private PlayerCharacter playerCharacter;

    [SerializeField]
    private PlayerGrenadeController grenadeController;

    private readonly List<GameObject> spawnedVisuals = new List<GameObject>(3);

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (playerCharacter == null)
            playerCharacter = GetComponent<PlayerCharacter>();

        if (grenadeController == null)
            grenadeController = GetComponent<PlayerGrenadeController>();
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged += Refresh;
        }

        if (playerCharacter != null)
        {
            playerCharacter.CharacterChanged += HandleCharacterChanged;
        }

        if (grenadeController != null)
        {
            grenadeController.GrenadeSelectionChanged += HandleGrenadeSelectionChanged;
        }

        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (inventory != null)
        {
            inventory.OnInventoryChanged -= Refresh;
        }

        if (playerCharacter != null)
        {
            playerCharacter.CharacterChanged -= HandleCharacterChanged;
        }

        if (grenadeController != null)
        {
            grenadeController.GrenadeSelectionChanged -= HandleGrenadeSelectionChanged;
        }

        ClearVisuals();
    }

    public void Refresh()
    {
        ClearVisuals();

        if (inventory == null || playerCharacter == null || playerCharacter.ActiveVisual == null)
        {
            return;
        }

        IReadOnlyList<Transform> sockets = playerCharacter.ActiveVisual.GrenadeCarrySockets;

        if (sockets == null || sockets.Count == 0)
        {
            return;
        }

        bool skippedHeldGrenade = false;
        int socketIndex = 0;

        for (int i = 0; i < inventory.Items.Count && socketIndex < sockets.Count; i++)
        {
            GrenadeItemData grenade = inventory.Items[i] as GrenadeItemData;

            if (grenade == null)
                continue;

            // The held grenade represents one real inventory occurrence,
            // so don't also show that same occurrence on the belt.
            if (
                !skippedHeldGrenade
                && grenadeController != null
                && grenadeController.IsGrenadeSelected
                && grenade == grenadeController.SelectedGrenade
            )
            {
                skippedHeldGrenade = true;
                continue;
            }

            if (grenade.stowedPrefab == null || sockets[socketIndex] == null)
            {
                socketIndex++;
                continue;
            }

            GameObject visual = Instantiate(grenade.stowedPrefab);

            HeldItemGrip grip = visual.GetComponent<HeldItemGrip>();

            if (grip != null)
            {
                if (!grip.AttachTo(sockets[socketIndex]))
                {
                    Destroy(visual);

                    socketIndex++;
                    continue;
                }
            }
            else
            {
                visual.transform.SetParent(sockets[socketIndex], false);

                visual.transform.localPosition = Vector3.zero;

                visual.transform.localRotation = Quaternion.identity;
            }

            spawnedVisuals.Add(visual);

            socketIndex++;
        }
    }

    private void HandleCharacterChanged(CharacterVisual character)
    {
        Refresh();
    }

    private void HandleGrenadeSelectionChanged(bool selected)
    {
        Refresh();
    }

    private void ClearVisuals()
    {
        for (int i = 0; i < spawnedVisuals.Count; i++)
        {
            if (spawnedVisuals[i] != null)
            {
                spawnedVisuals[i].SetActive(false);

                Destroy(spawnedVisuals[i]);
            }
        }

        spawnedVisuals.Clear();
    }
}
