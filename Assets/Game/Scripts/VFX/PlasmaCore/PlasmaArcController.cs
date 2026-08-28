using System.Collections.Generic;
using UnityEngine;

public class PlasmaArcController : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField]
    private GameObject arcPrefab;

    [Header("Dynamic Count")]
    [SerializeField]
    private int minActiveArcs = 3;

    [SerializeField]
    private int maxActiveArcs = 6;

    [SerializeField]
    private float reshuffleInterval = 0.035f;

    [Header("Arc Settings")]
    [SerializeField]
    private int segments = 8;

    [SerializeField]
    private float arcLength = 1.8f;

    [SerializeField]
    private float jitter = 0.75f;

    [SerializeField]
    private float refreshRate = 0.5f;

    [SerializeField]
    private float arcWidth = 0.006f;

    private Color auraColor = Color.magenta;

    private readonly List<GameObject> arcPool = new();

    // Reused shuffle buffer.
    // Allocated only when the pool is built/resized, not every reshuffle.
    private int[] arcIndexBuffer;

    private float timer;
    private bool started;

    private void Start()
    {
        started = true;

        BuildPool();
        RandomizeActiveArcs();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= reshuffleInterval)
        {
            timer = 0f;
            RandomizeActiveArcs();
        }
    }

    public void Configure(
        int newMinArcs,
        int newMaxArcs,
        int newSegments,
        float newArcLength,
        float newJitter,
        float newRefreshRate,
        float newArcWidth,
        Color newAuraColor
    )
    {
        minActiveArcs = newMinArcs;
        maxActiveArcs = newMaxArcs;

        segments = newSegments;
        arcLength = newArcLength;
        jitter = newJitter;
        refreshRate = newRefreshRate;
        arcWidth = newArcWidth;

        auraColor = newAuraColor;

        if (started)
        {
            BuildPool();
            RandomizeActiveArcs();
        }
    }

    private void BuildPool()
    {
        foreach (GameObject arc in arcPool)
        {
            if (arc != null)
                Destroy(arc);
        }

        arcPool.Clear();

        int poolSize =
            Mathf.Max(
                0,
                maxActiveArcs
            );

        arcIndexBuffer =
            new int[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            GameObject arc =
                Instantiate(
                    arcPrefab,
                    transform
                );

            arc.name =
                $"Arc{i + 1:00}";

            PlasmaArc plasmaArc =
                arc.GetComponent<PlasmaArc>();

            if (plasmaArc != null)
            {
                plasmaArc.Configure(
                    segments,
                    arcLength,
                    jitter,
                    refreshRate,
                    arcWidth,
                    auraColor
                );
            }

            arc.SetActive(false);
            arcPool.Add(arc);
        }
    }

    private void RandomizeActiveArcs()
    {
        int poolCount =
            arcPool.Count;

        if (poolCount == 0)
            return;

        if (
            arcIndexBuffer == null
            || arcIndexBuffer.Length < poolCount
        )
        {
            // Only happens if the pool size changed unexpectedly.
            arcIndexBuffer =
                new int[poolCount];
        }

        int minCount =
            Mathf.Clamp(
                minActiveArcs,
                0,
                poolCount
            );

        int maxCount =
            Mathf.Clamp(
                maxActiveArcs,
                minCount,
                poolCount
            );

        int activeCount =
            Random.Range(
                minCount,
                maxCount + 1
            );

        for (int i = 0; i < poolCount; i++)
        {
            arcPool[i].SetActive(false);
            arcIndexBuffer[i] = i;
        }

        // Partial Fisher-Yates shuffle:
        // choose only the number of unique indices we actually need.
        for (int i = 0; i < activeCount; i++)
        {
            int randomIndex =
                Random.Range(
                    i,
                    poolCount
                );

            int temp =
                arcIndexBuffer[i];

            arcIndexBuffer[i] =
                arcIndexBuffer[randomIndex];

            arcIndexBuffer[randomIndex] =
                temp;

            GameObject arc =
                arcPool[
                    arcIndexBuffer[i]
                ];

            arc.transform.localPosition =
                Vector3.zero;

            arc.transform.localRotation =
                Random.rotation;

            arc.SetActive(true);
        }
    }
}
