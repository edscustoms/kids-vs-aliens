using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Builds small temporary investigation routes.
///
/// Used for:
/// - last known player position after LOS is lost
/// - directional stimuli such as being shot from an unknown direction
///
/// Search points are generated once per investigation event, not continuously.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyInvestigationPlanner : MonoBehaviour
{
    [SerializeField, Range(1, 8)]
    private int desiredSearchPoints = 3;

    [SerializeField, Min(0.1f)]
    private float minimumSearchRadius = 0.8f;

    [SerializeField, Min(0.2f)]
    private float maximumSearchRadius = 2.6f;

    [SerializeField, Min(0.05f)]
    private float navMeshSampleRadius = 0.75f;

    [SerializeField, Min(0f)]
    private float minimumPointSpacing = 0.75f;

    [SerializeField, Range(8, 60)]
    private int maximumAttempts = 28;

    [Header("Directional investigation")]
    [SerializeField, Min(0.1f)]
    private float directionalAnchorSampleRadius = 1.0f;

    [SerializeField, Range(2, 8)]
    private int directionalAnchorAttempts = 4;

    private readonly List<Vector3>
        searchPoints = new();

    private int nextPointIndex;

    public int PointCount =>
        searchPoints.Count;

    /// <summary>
    /// Resolves a reachable NavMesh point in a requested world direction.
    ///
    /// It tries the full requested distance first, then progressively shorter
    /// distances. This means an enemy never magically knows the shooter's
    /// position; it only walks in the direction the shot came from.
    /// </summary>
    public bool TryGetDirectionalAnchor(
        Vector3 worldDirection,
        float distance,
        out Vector3 anchor)
    {
        anchor =
            transform.position;

        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude <=
            0.0001f)
        {
            return false;
        }

        worldDirection.Normalize();

        if (!NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit startHit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return false;
        }

        Vector3 pathStart =
            startHit.position;

        NavMeshPath path =
            new NavMeshPath();

        int attempts =
            Mathf.Max(
                2,
                directionalAnchorAttempts);

        for (int i = 0;
             i < attempts;
             i++)
        {
            float fraction =
                1f -
                (i /
                 (float)attempts);

            float attemptDistance =
                Mathf.Max(
                    0.5f,
                    distance *
                    fraction);

            Vector3 desired =
                pathStart +
                worldDirection *
                attemptDistance;

            if (!NavMesh.SamplePosition(
                    desired,
                    out NavMeshHit hit,
                    directionalAnchorSampleRadius,
                    NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 actualDirection =
                hit.position -
                pathStart;

            actualDirection.y = 0f;

            // Do not accept a sampled point that ended up behind the enemy.
            if (actualDirection.sqrMagnitude >
                    0.001f &&
                Vector3.Dot(
                    actualDirection.normalized,
                    worldDirection) <
                    0.15f)
            {
                continue;
            }

            path.ClearCorners();

            bool hasPath =
                NavMesh.CalculatePath(
                    pathStart,
                    hit.position,
                    NavMesh.AllAreas,
                    path);

            if (!hasPath ||
                path.status !=
                NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            anchor =
                hit.position;

            return true;
        }

        return false;
    }

    public void BuildSearch(
        Vector3 investigationPosition)
    {
        searchPoints.Clear();
        nextPointIndex = 0;

        if (!NavMesh.SamplePosition(
                transform.position,
                out NavMeshHit startHit,
                navMeshSampleRadius,
                NavMesh.AllAreas))
        {
            return;
        }

        Vector3 pathStart =
            startHit.position;

        NavMeshPath path =
            new NavMeshPath();

        int attempts = 0;

        while (
            searchPoints.Count <
                desiredSearchPoints &&
            attempts <
                maximumAttempts)
        {
            attempts++;

            float radius =
                Random.Range(
                    minimumSearchRadius,
                    Mathf.Max(
                        minimumSearchRadius,
                        maximumSearchRadius));

            Vector2 direction =
                Random.insideUnitCircle;

            if (direction.sqrMagnitude <
                0.01f)
            {
                continue;
            }

            direction.Normalize();

            Vector3 candidate =
                investigationPosition +
                new Vector3(
                    direction.x,
                    0f,
                    direction.y) *
                radius;

            if (!NavMesh.SamplePosition(
                    candidate,
                    out NavMeshHit hit,
                    navMeshSampleRadius,
                    NavMesh.AllAreas))
            {
                continue;
            }

            if (!IsFarEnough(
                    hit.position))
            {
                continue;
            }

            path.ClearCorners();

            bool hasPath =
                NavMesh.CalculatePath(
                    pathStart,
                    hit.position,
                    NavMesh.AllAreas,
                    path);

            if (!hasPath ||
                path.status !=
                NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            searchPoints.Add(
                hit.position);
        }

        Shuffle(
            searchPoints);
    }

    public bool TryGetNextPoint(
        out Vector3 point)
    {
        if (nextPointIndex >=
            searchPoints.Count)
        {
            point =
                transform.position;

            return false;
        }

        point =
            searchPoints[
                nextPointIndex];

        nextPointIndex++;

        return true;
    }

    private bool IsFarEnough(
        Vector3 point)
    {
        float minimumSquared =
            minimumPointSpacing *
            minimumPointSpacing;

        foreach (Vector3 existing
                 in searchPoints)
        {
            Vector3 delta =
                point -
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

    private static void Shuffle(
        List<Vector3> points)
    {
        for (int i =
                 points.Count - 1;
             i > 0;
             i--)
        {
            int j =
                Random.Range(
                    0,
                    i + 1);

            (points[i], points[j]) =
                (points[j], points[i]);
        }
    }
}
