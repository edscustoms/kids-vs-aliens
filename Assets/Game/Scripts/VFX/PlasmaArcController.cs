using System.Collections.Generic;
using UnityEngine;

public class PlasmaArcController : MonoBehaviour
{
    [Header("Prefab / Setup")]
    [SerializeField]
    private GameObject arcPrefab;

    [SerializeField]
    private int maxArcCount = 6;

    [Header("Dynamic Count")]
    [SerializeField]
    private int minActiveArcs = 3;

    [SerializeField]
    private int maxActiveArcs = 6;

    [Header("Randomize Timing")]
    [SerializeField]
    private float reshuffleInterval = 0.4f;

    private readonly List<GameObject> arcPool = new();
    private float timer;

    void Start()
    {
        for (int i = 0; i < maxArcCount; i++)
        {
            GameObject arc = Instantiate(arcPrefab, transform);
            arc.name = $"Arc{i + 1:00}";
            arc.SetActive(false);
            arcPool.Add(arc);
        }

        RandomizeActiveArcs();
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= reshuffleInterval)
        {
            timer = 0f;
            RandomizeActiveArcs();
        }
    }

    private void RandomizeActiveArcs()
    {
        int activeCount = Random.Range(minActiveArcs, maxActiveArcs + 1);

        // First disable all
        foreach (var arc in arcPool)
            arc.SetActive(false);

        // Make a temp list of indices
        List<int> indices = new();
        for (int i = 0; i < arcPool.Count; i++)
            indices.Add(i);

        // Pick random unique arcs
        for (int i = 0; i < activeCount; i++)
        {
            int randomListIndex = Random.Range(0, indices.Count);
            int arcIndex = indices[randomListIndex];
            indices.RemoveAt(randomListIndex);

            GameObject arc = arcPool[arcIndex];
            arc.SetActive(true);

            // Optional: slightly randomize local position/rotation
            arc.transform.localPosition = Vector3.zero;
            arc.transform.localRotation = Random.rotation;
        }
    }
}
