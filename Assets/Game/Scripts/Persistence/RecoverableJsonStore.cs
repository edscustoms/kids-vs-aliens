using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Two checksummed generations: a failed/torn replacement leaves the other generation readable.
public sealed class RecoverableJsonStore
{
    [Serializable] private sealed class Envelope { public int version = 1; public long revision; public string payload, checksum; }
    private readonly string path;
    public RecoverableJsonStore(string directory, string name) { path = Path.Combine(directory, name); }
    public bool Exists => File.Exists(path + ".0.json") || File.Exists(path + ".1.json");
    private static string Checksum(string value)
    {
        using var hash = SHA256.Create();
        return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
    private Envelope ReadGeneration(int generation)
    {
        try
        {
            string file = path + "." + generation + ".json";
            if (!File.Exists(file)) return null;
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(file));
            return envelope != null && envelope.version == 1 && envelope.payload != null && envelope.checksum == Checksum(envelope.payload)
                ? envelope : null;
        }
        catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException) { return null; }
    }
    private Envelope Latest()
    {
        var a = ReadGeneration(0); var b = ReadGeneration(1);
        return a == null ? b : b == null || a.revision >= b.revision ? a : b;
    }
    public T Read<T>() where T : class
    {
        var envelope = Latest();
        if (envelope == null)
        {
            if (Exists) throw new IOException("Neither saved generation is readable. The existing save has been preserved.");
            return null;
        }
        var result = JsonUtility.FromJson<T>(envelope.payload);
        if (result == null) throw new IOException("The saved data could not be read.");
        return result;
    }
    public void Write<T>(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        long revision = (Latest()?.revision ?? 0) + 1;
        string destination = path + "." + (revision % 2) + ".json", temporary = destination + ".tmp";
        string payload = JsonUtility.ToJson(value);
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope { revision = revision, payload = payload, checksum = Checksum(payload) }));
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        // Only the older generation is replaced; the newest valid generation is never touched.
        if (File.Exists(destination)) File.Delete(destination);
        File.Move(temporary, destination);
    }
    public void Delete()
    {
        for (int i = 0; i < 2; i++)
        {
            string file = path + "." + i + ".json";
            if (File.Exists(file)) File.Delete(file);
            if (File.Exists(file + ".tmp")) File.Delete(file + ".tmp");
        }
    }
}
