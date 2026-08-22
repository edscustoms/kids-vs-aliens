using UnityEngine;

public class PickupItem : MonoBehaviour
{
    [SerializeField] private ItemData item;

    private void OnTriggerEnter(Collider other)
    {
        PlayerInventory inventory =
            other.GetComponent<PlayerInventory>();

        if (inventory == null)
            return;

        inventory.AddItem(item);

        Destroy(gameObject);
    }
}