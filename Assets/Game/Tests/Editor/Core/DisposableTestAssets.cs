using System;
using System.IO;
using UnityEditor;

/// <summary>Authoring tests own a unique asset folder; production assets are only copy sources.</summary>
public sealed class DisposableTestAssets : IDisposable
{
    public string Folder { get; } = "Assets/TestFixture_" + Guid.NewGuid().ToString("N");
    public DisposableTestAssets() => AssetDatabase.CreateFolder("Assets", Path.GetFileName(Folder));
    public string Copy(string source)
    {
        string path = Folder + "/" + Path.GetFileName(source);
        if (!AssetDatabase.CopyAsset(source, path)) throw new IOException("Could not copy " + source);
        return path;
    }
    public void Dispose() => AssetDatabase.DeleteAsset(Folder);
}
