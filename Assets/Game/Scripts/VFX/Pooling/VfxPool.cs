using System.Collections.Generic;
using UnityEngine;

public static class VfxPool
{
    private static readonly Dictionary<EntityId, Stack<GameObject>> pools = new();

    private static Transform poolRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pools.Clear();
        poolRoot = null;
    }

    public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation)
        where T : Component
    {
        if (prefab == null)
            return null;

        EnsurePoolRoot();

        EntityId poolKey = prefab.gameObject.GetEntityId();

        if (!pools.TryGetValue(poolKey, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            pools.Add(poolKey, pool);
        }

        GameObject instanceObject = null;

        while (pool.Count > 0 && instanceObject == null)
        {
            instanceObject = pool.Pop();
        }

        if (instanceObject == null)
        {
            instanceObject = Object.Instantiate(prefab.gameObject);

            VfxPoolToken token = instanceObject.GetComponent<VfxPoolToken>();

            if (token == null)
            {
                token = instanceObject.AddComponent<VfxPoolToken>();
            }

            token.Initialize(poolKey);
        }

        VfxPoolToken instanceToken = instanceObject.GetComponent<VfxPoolToken>();

        instanceToken.MarkSpawned();

        Transform instanceTransform = instanceObject.transform;

        instanceTransform.SetParent(null, false);

        instanceTransform.SetPositionAndRotation(position, rotation);

        instanceObject.SetActive(true);

        T component = instanceObject.GetComponent<T>();

        if (component == null)
        {
            Debug.LogError(
                $"{instanceObject.name}: pooled object does not contain {typeof(T).Name}."
            );

            Object.Destroy(instanceObject);

            return null;
        }

        return component;
    }

    public static T Spawn<T>(T prefab)
        where T : Component
    {
        return Spawn(prefab, Vector3.zero, Quaternion.identity);
    }

    public static void Release(Component component)
    {
        if (component == null)
            return;

        GameObject instanceObject = component.gameObject;

        VfxPoolToken token = instanceObject.GetComponent<VfxPoolToken>();

        // Defensive fallback:
        // if somebody manually instantiated this VFX instead of using
        // VfxPool.Spawn, it still cleans itself up correctly.
        if (token == null)
        {
            Object.Destroy(instanceObject);
            return;
        }

        if (token.IsInPool)
            return;

        EnsurePoolRoot();

        if (!pools.TryGetValue(token.PoolKey, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            pools.Add(token.PoolKey, pool);
        }

        token.MarkReleased();

        instanceObject.SetActive(false);

        instanceObject.transform.SetParent(poolRoot, false);

        pool.Push(instanceObject);
    }

    private static void EnsurePoolRoot()
    {
        if (poolRoot != null)
            return;

        GameObject root = new GameObject("[VFX Pool]");

        Object.DontDestroyOnLoad(root);

        poolRoot = root.transform;
    }
}
