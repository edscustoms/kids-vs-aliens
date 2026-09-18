using System;
using System.Collections.Generic;
using UnityEngine;

// Editor-generated stable asset references, included in builds; never resolve saves by display name.
public sealed class RunContentCatalog : ScriptableObject
{
    [Serializable] public sealed class Entry { public string id; public UnityEngine.Object asset; }
    public Entry[] entries = Array.Empty<Entry>();
    private Dictionary<string, UnityEngine.Object> byId;
    private Dictionary<UnityEngine.Object, string> byAsset;
    private static RunContentCatalog instance;
    public static RunContentCatalog Instance => instance != null ? instance : instance = Resources.Load<RunContentCatalog>("RunContentCatalog");
    private void Initialize()
    {
        if (byId != null) return;
        byId = new(); byAsset = new();
        foreach (var entry in entries) if (entry.asset != null && !string.IsNullOrEmpty(entry.id)) { byId[entry.id] = entry.asset; byAsset[entry.asset] = entry.id; }
    }
    public string Id(UnityEngine.Object asset)
    {
        if (asset == null) return "";
        Initialize();
        if (!byAsset.TryGetValue(asset, out var id)) throw new InvalidOperationException("Save catalog is missing " + asset.name + ". Run scene setup/repair.");
        return id;
    }
    public T Resolve<T>(string id) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(id)) return null;
        Initialize();
        if (!byId.TryGetValue(id, out var asset) || !(asset is T typed)) throw new InvalidOperationException("Saved content is missing: " + id);
        return typed;
    }
}
