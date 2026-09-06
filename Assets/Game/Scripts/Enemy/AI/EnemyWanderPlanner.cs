using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Caches valid idle destinations around the spawn point ONCE.
///
/// Idle movement is deliberately sparse:
/// - choose one cached point
/// - SetDestination once
/// - walk there
/// - wait again
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyWanderPlanner : MonoBehaviour
{
    [Header("Cached idle points")]
    [SerializeField, Range(3, 20)]
    private int desiredPointCount = 8;

    [SerializeField, Min(0.5f)]
    private float wanderRadius = 4f;

    [SerializeField, Min(0f)]
    private float minimumDistanceFromSpawn = 1.4f;

    [SerializeField, Min(0f)]
    private float minimumTravelDistance = 1.2f;

    [SerializeField, Min(0.05f)]
    private float navMeshSampleRadius = 1.0f;

    [SerializeField, Min(0f)]
    private float minimumPointSpacing = 1.0f;

    [SerializeField, Range(10, 100)]
    private int maximumSampleAttempts = 60;

    private readonly List<Vector3>
        cachedPoints = new();

    private Vector3 spawnAnchor;
    private int lastPointIndex = -1;
    private bool cacheBuilt;

    public IReadOnlyList<Vector3> CachedPoints =>
        cachedPoints;

    private void Start()
    {
        BuildCache();
    }

    public bool TryGetRandomPoint(
        out Vector3 point)
    {
        EnsureCache();

        point =
            transform.position;

        if (cachedPoints.Count == 0)
            return false;

        int attempts =
            Mathf.Min(
                8,
                cachedPoints.Count * 2);

        for (int attempt = 0;
             attempt < attempts;
             attempt++)
        {
            int index =
                Random.Range(
                    0,
                    cachedPoints.Count);

            if (cachedPoints.Count > 1 &&
                index == lastPointIndex)
            {
                continue;
            }

            Vector3 candidate =
                cachedPoints[index];

            Vector3 delta =
                candidate -
                transform.position;

            delta.y = 0f;

            if (delta.sqrMagnitude <
                minimumTravelDistance *
                minimumTravelDistance)
            {
                continue;
            }

            lastPointIndex =
                index;

            point =
                candidate;

            return true;
        }

        return false;
    }

    [ContextMenu("Rebuild Wander Cache")]
    public void BuildCache()
    {
        cachedPoints.Clear();

        spawnAnchor =
            transform.position;

        if (!NavMesh.SamplePosition(
                spawnAnchor,
                out NavMeshHit startHit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            cacheBuilt = true;

            Debug.LogWarning(
                $"{name}: could not find NavMesh near the enemy spawn point for idle wandering.",
                this);

            return;
        }

        Vector3 navStart =
            startHit.position;

        NavMeshPath path =
            new NavMeshPath();

        int attempts = 0;

        while (
            cachedPoints.Count <
                desiredPointCount &&
            attempts <
                maximumSampleAttempts)
        {
            attempts++;

            Vector2 randomCircle =
                Random.insideUnitCircle *
                wanderRadius;

            Vector3 candidate =
                navStart +
                new Vector3(
                    randomCircle.x,
                    0f,
                    randomCircle.y);

            if (!NavMesh.SamplePosition(
                    candidate,
                    out NavMeshHit hit,
                    navMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 fromSpawn =
                hit.position -
                navStart;

            fromSpawn.y = 0f;

            if (fromSpawn.sqrMagnitude <
                minimumDistanceFromSpawn *
                minimumDistanceFromSpawn)
            {
                continue;
            }

            if (!IsFarEnoughFromExistingPoints(
                    hit.position))
            {
                continue;
            }

            path.ClearCorners();

            bool hasPath =
                NavMesh.CalculatePath(
                    navStart,
                    hit.position,
                    NavMesh.AllAreas,
                    path);

            if (!hasPath ||
                path.status !=
                NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            cachedPoints.Add(
                hit.position);
        }

        cacheBuilt = true;
    }

    private void EnsureCache()
    {
        if (!cacheBuilt)
            BuildCache();
    }

    private bool IsFarEnoughFromExistingPoints(
        Vector3 candidate)
    {
        float minimumSquared =
            minimumPointSpacing *
            minimumPointSpacing;

        foreach (Vector3 existing
                 in cachedPoints)
        {
            Vector3 delta =
                candidate -
                existing;

            delta.y = 0f;

            if (delta.sqrMagnitude <
                minimumSquared)
            {
                return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        Gizmos.color =
            new Color(
                0.25f,
                0.8f,
                1f,
                0.75f);

        Vector3 center =
            Application.isPlaying
                ? spawnAnchor
                : transform.position;

        Gizmos.DrawWireSphere(
            center,
            wanderRadius);

        if (!Application.isPlaying)
            return;

        foreach (Vector3 point
                 in cachedPoints)
        {
            Gizmos.DrawSphere(
                point +
                Vector3.up * 0.05f,
                0.08f);
        }
    }

#endif
}
