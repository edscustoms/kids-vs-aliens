using UnityEngine;

public class ItemSpawner : MonoBehaviour, IRunStateParticipant
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

    [System.Serializable] private sealed class SavedSpawn { public bool spawned; public float remaining; }
    private bool spawned, restored;
    private float spawnDue;
    public string RunStateKey => "item-spawner";
    public string CaptureRunState() => JsonUtility.ToJson(new SavedSpawn { spawned = spawned, remaining = Mathf.Max(0, spawnDue - Time.time) });
    public void RestoreRunState(string json)
    {
        var state = JsonUtility.FromJson<SavedSpawn>(json);
        restored = true; spawned = state.spawned; CancelInvoke(nameof(SpawnItem));
        spawnDue = Time.time + Mathf.Max(0, state.remaining);
        if (!spawned) Invoke(nameof(SpawnItem), Mathf.Max(0, state.remaining));
    }
    private void Start()
    {
        if (restored) return;
        spawnDue = Time.time + spawnDelay;
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

        var pickup = Instantiate(weapon.worldPrefab, spawnPosition, Quaternion.identity);
        RunWorldObject.TrackSpawn(pickup, weapon.worldPrefab);
        spawned = true;
        ActiveRunController.Instance?.MarkDirty();
    }
}
