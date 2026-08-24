using UnityEngine;

public class ItemSpawner : MonoBehaviour
{
    [SerializeField]
    private WeaponItemData weapon;

    [Header("Spawn")]
    [SerializeField]
    private float spawnDelay = 1f;

    [SerializeField]
    private float roomHalfSize = 10f;

    [SerializeField]
    private float wallPadding = 1f;

    [SerializeField]
    private float spawnHeight = 0.6f;

    private void Start()
    {
        Invoke(nameof(SpawnItem), spawnDelay);
    }

    private void SpawnItem()
    {
        if (weapon == null || weapon.worldPrefab == null)
        {
            Debug.LogError("ItemSpawner has no weapon/world prefab configured.");
            return;
        }

        float limit = roomHalfSize - wallPadding;

        float x = Random.Range(-limit, limit);
        float z = Random.Range(-limit, limit);

        Vector3 spawnPosition = new Vector3(x, spawnHeight, z);

        Instantiate(weapon.worldPrefab, spawnPosition, Quaternion.identity);
    }
}
