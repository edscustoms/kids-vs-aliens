using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform enemyContainer;

    [Header("Spawn")]
    [SerializeField] private float firstSpawnDelay = 1f;
    [SerializeField] private float spawnInterval = 5f;
    [SerializeField] private int maxEnemies = 10;

    [Header("Room")]
    [SerializeField] private float roomHalfSize = 10f;
    [SerializeField] private float wallPadding = 1.5f;
    [SerializeField] private float spawnHeight = 0.95f;

    [Header("Player")]
    [SerializeField] private float minPlayerDistance = 4f;

    private Transform player;
    private float nextSpawn;
    private bool restoredDelay;
    public Transform SpawnContainer => enemyContainer;
    public float RemainingSpawnDelay => Mathf.Max(0, nextSpawn - Time.time);
    public void RestoreSpawnDelay(float remaining) { restoredDelay = true; nextSpawn = Time.time + Mathf.Max(0, remaining); }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;

        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        if (!restoredDelay) nextSpawn = Time.time + firstSpawnDelay;

        while (true)
        {
            while (Time.time < nextSpawn || Time.timeScale <= 0) yield return null;
            SpawnEnemy();
            nextSpawn = Time.time + spawnInterval;
            yield return null;
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null || enemyContainer == null)
            return;

        int enemyCount =
            enemyContainer.GetComponentsInChildren<EnemyHealth>().Length;

        if (enemyCount >= maxEnemies)
            return;

        float limit = roomHalfSize - wallPadding;

        Vector3 spawnPosition = Vector3.zero;

        for (int i = 0; i < 20; i++)
        {
            spawnPosition = new Vector3(
                Random.Range(-limit, limit),
                spawnHeight,
                Random.Range(-limit, limit)
            );

            if (player == null ||
                Vector3.Distance(spawnPosition, player.position) >= minPlayerDistance)
            {
                break;
            }
        }

        var spawned = Instantiate(
            enemyPrefab,
            spawnPosition,
            Quaternion.identity,
            enemyContainer
        );
        RunWorldObject.TrackSpawn(spawned, enemyPrefab, GetComponent<RunWorldObject>()?.Id);
    }
}