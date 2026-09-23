#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

/// <summary>Add only our permission to Unity's generated manifest; preserve its activity configuration.</summary>
public sealed class HapticAndroidBuild : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 0;
    public void OnPostGenerateGradleAndroidProject(string path) =>
        EnsurePermission(Path.Combine(path, "src/main/AndroidManifest.xml"));

    public static void EnsurePermission(string path)
    {
        const string android = "http://schemas.android.com/apk/res/android";
        var document = new XmlDocument();
        document.Load(path);
        var manifest = document.DocumentElement;
        foreach (XmlNode child in manifest.ChildNodes)
            if (child is XmlElement element && element.Name == "uses-permission"
                && element.GetAttribute("name", android) == "android.permission.VIBRATE") return;
        var permission = document.CreateElement("uses-permission");
        var name = document.CreateAttribute("android", "name", android);
        name.Value = "android.permission.VIBRATE";
        permission.Attributes.Append(name);
        manifest.AppendChild(permission);
        document.Save(path);
    }
}
#endif
