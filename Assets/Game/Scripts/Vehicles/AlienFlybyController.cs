using UnityEngine;

public sealed class AlienFlybyController : MonoBehaviour
{
    [Tooltip("Editor validation only. Assign the level's environment/geometry roots, including colliderless major visuals.")]
    public Transform[] environmentRoots;
    [SerializeField] private AlienFlybyPath[] paths;
    [SerializeField] private AlienFlybyBike[] pool;
    [SerializeField, Min(1)] private float minDelay = 9, maxDelay = 23;
    [SerializeField, Min(1)] private float minSpeed = 13, maxSpeed = 23;
    private float remaining = 3;
    private int lastPath = -1;
    private readonly System.Random random = new();
    private void Update()
    {
        if (Time.deltaTime <= 0 || paths == null || paths.Length == 0 || pool == null)
            return;
        remaining -= Time.deltaTime;
        if (remaining > 0)
            return;
        foreach (var bike in pool)
        if (bike != null && bike.IsFlying)
            return;
        int selected = random.Next(paths.Length);
        if (selected == lastPath && paths.Length > 1)
            selected = (selected + 1 + random.Next(paths.Length - 1)) % paths.Length;
        var path = paths[selected];
        remaining = Mathf.Lerp(minDelay, maxDelay, (float)random.NextDouble());
        if (path == null || !path.IsValidated)
            return;
        lastPath = selected;
        double roll = random.NextDouble();
        int count = roll < .7 ? 1 : roll < .94 ? 2 : 3;
        bool reverse = path.allowReverse && random.Next(2) == 1;
        float speed = Mathf.Lerp(minSpeed, maxSpeed, (float)random.NextDouble());
        for (int i = 0; i < Mathf.Min(count, pool.Length); i++)
            pool[i].Begin(path, reverse, speed, i * .7f, random.Next(3));
    }
}
