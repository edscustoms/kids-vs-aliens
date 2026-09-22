using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class LitFadeMaterialTests
{
    [Test]
    public void SavedExcavatorGlassIncludesTheTransparentFadeShaderInPlayerBuilds()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Game/Prefabs/Environment/Machinery/PF_Excavator_A.prefab");
        var material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Game/Materials/Weapons/M_Weapon_Glass.mat");
        var glass = prefab.GetComponentsInChildren<Renderer>(true)
            .Single(r => r.name == "12_-_Default");

        Assert.That(glass.sharedMaterials, Does.Contain(material), "Check the actual imported excavator glass.");
        Assert.That(material.shader, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Shader>(
            "Assets/Game/Shaders/Lit_Fade.shader")), "A temporary Editor material is not a player-build dependency.");
        Assert.That(material.HasProperty("_Fade"), Is.True);
        Assert.That(material.GetFloat("_Fade"), Is.EqualTo(1f));
        Assert.That(material.GetFloat("_Surface"), Is.EqualTo(1f));
        Assert.That(material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
        Assert.That(material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"), Is.True);
        Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Transparent));
        Assert.That(material.GetFloat("_ZWrite"), Is.Zero);
    }
}
