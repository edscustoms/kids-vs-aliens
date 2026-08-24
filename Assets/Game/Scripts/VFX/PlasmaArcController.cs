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

    private readonly List<GameObject> arcPool = new();

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
        float newArcWidth
    )
    {
        minActiveArcs = newMinArcs;
        maxActiveArcs = newMaxArcs;

        segments = newSegments;
        arcLength = newArcLength;
        jitter = newJitter;
        refreshRate = newRefreshRate;
        arcWidth = newArcWidth;

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

        for (int i = 0; i < maxActiveArcs; i++)
        {
            GameObject arc = Instantiate(arcPrefab, transform);
            arc.name = $"Arc{i + 1:00}";

            PlasmaArc plasmaArc = arc.GetComponent<PlasmaArc>();

            if (plasmaArc != null)
            {
                plasmaArc.Configure(segments, arcLength, jitter, refreshRate, arcWidth);
            }

            arc.SetActive(false);
            arcPool.Add(arc);
        }
    }

    private void RandomizeActiveArcs()
    {
        int activeCount = Random.Range(minActiveArcs, maxActiveArcs + 1);

        foreach (GameObject arc in arcPool)
            arc.SetActive(false);

        List<int> indices = new();

        for (int i = 0; i < arcPool.Count; i++)
            indices.Add(i);

        for (int i = 0; i < activeCount; i++)
        {
            int randomListIndex = Random.Range(0, indices.Count);

            int arcIndex = indices[randomListIndex];
            indices.RemoveAt(randomListIndex);

            GameObject arc = arcPool[arcIndex];

            arc.transform.localPosition = Vector3.zero;
            arc.transform.localRotation = Random.rotation;

            arc.SetActive(true);
        }
    }
}
