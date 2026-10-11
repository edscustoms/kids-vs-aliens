using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RuntimeFadeVariantTests
{
    [TestCase("Lit Transparent", false)]
    [TestCase("Lit Transparent Mapped", true)]
    public void PackagedMaterialRetainsTheActualCameraFadeKeywords(string name, bool mapped)
    {
        RuntimeFadeVariantBuildCheck.Validate();
        var retained = Resources.Load<Material>("RenderingVariants/" + name);
        var runtime = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        try
        {
            if (mapped) runtime.shaderKeywords = new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP" };
            typeof(CameraOcclusionController).GetMethod("ConfigureMaterialForTransparentFade",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { runtime });
            CollectionAssert.AreEquivalent(runtime.shaderKeywords, retained.shaderKeywords,
                "Runtime fade changes must also update the retained material state.");
        }
        finally { Object.DestroyImmediate(runtime); }
    }
}
