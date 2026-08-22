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

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;

        StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(firstSpawnDelay);

        while (true)
        {
            SpawnEnemy();

            yield return new WaitForSeconds(spawnInterval);
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

        Instantiate(
            enemyPrefab,
            spawnPosition,
            Quaternion.identity,
            enemyContainer
        );
    }
}