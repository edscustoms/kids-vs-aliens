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
        {
            grenadeController = GetComponent<PlayerGrenadeController>();
        }
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
            grenadeController.GrenadeSelectionChanged += HandleSelectionChanged;
        }
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
            grenadeController.GrenadeSelectionChanged -= HandleSelectionChanged;
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

        GrenadeItemData selected =
            grenadeController != null && grenadeController.IsGrenadeSelected
                ? grenadeController.SelectedGrenade
                : null;

        bool skippedSelectedOccurrence = false;

        int socketIndex = 0;

        for (int i = 0; i < inventory.Items.Count && socketIndex < sockets.Count; i++)
        {
            GrenadeItemData grenade = inventory.Items[i] as GrenadeItemData;

            // Grenades live in the normal shared inventory.
            // Ignore weapons, books, consumables, etc.
            if (grenade == null)
                continue;

            if (!skippedSelectedOccurrence && grenade == selected)
            {
                skippedSelectedOccurrence = true;
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
                // Keep the stowed prefab's authored local transform.
                // The carry socket decides WHERE the grenade slot is on the character;
                // the stowed prefab decides HOW this grenade type sits in that slot.
                visual.transform.SetParent(sockets[socketIndex], false);
            }

            spawnedVisuals.Add(visual);

            socketIndex++;
        }
    }

    private void HandleCharacterChanged(CharacterVisual character)
    {
        Refresh();
    }

    private void HandleSelectionChanged(bool selected)
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
