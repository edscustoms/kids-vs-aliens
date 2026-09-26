using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[TestFixture, Category("ProceduralUI")]
public sealed class ProceduralUITests
{
    [Test] public void VisualReplacementPreservesHitRectangleAndClickAction()
    {
        var root=new GameObject("Button",typeof(RectTransform),typeof(Image),typeof(Button));
        var events=new GameObject("Events",typeof(EventSystem));
        try {
            var rect=(RectTransform)root.transform;rect.sizeDelta=new(280,120);rect.anchoredPosition=new(52,34);
            var image=root.GetComponent<Image>();image.raycastTarget=true;
            var button=root.GetComponent<Button>();int clicks=0;button.onClick.AddListener(()=>clicks++);
            var theme=AssetDatabase.LoadAssetAtPath<UITheme>(MenuUISetup.ThemePath);
            var first=NeonVisuals.Replace(image,NeonShape.Circle,theme);NeonVisuals.Feedback(root,first);
            var second=NeonVisuals.Replace(image,NeonShape.Circle,theme);
            Assert.That(second,Is.SameAs(first));Assert.That(root.transform.childCount,Is.EqualTo(1));
            Assert.That(rect.sizeDelta,Is.EqualTo(new Vector2(280,120)));Assert.That(rect.anchoredPosition,Is.EqualTo(new Vector2(52,34)));
            Assert.That(image.enabled&&image.raycastTarget,Is.True);Assert.That(first.raycastTarget,Is.False);
            Assert.That(first.GetComponent<LayoutElement>().ignoreLayout,Is.True);
            button.OnPointerClick(new PointerEventData(events.GetComponent<EventSystem>()){button=PointerEventData.InputButton.Left});
            Assert.That(clicks,Is.EqualTo(1));
        } finally {Object.DestroyImmediate(root);Object.DestroyImmediate(events);}
    }
    [Test] public void SurfaceSizesAndStatesShareOneMaterialAndFourVertices()
    {
        var root=new GameObject("Canvas",typeof(Canvas));
        try {
            var a=InterfaceFactory.Panel(root.transform,"Card",Vector2.zero,Vector2.one).GetComponent<NeonPanel>();
            var b=InterfaceFactory.Panel(root.transform,"Button",Vector2.zero,Vector2.one).GetComponent<NeonPanel>();
            a.rectTransform.sizeDelta=new(700,400);b.rectTransform.sizeDelta=new(300,70);
            b.SetShape(NeonShape.Circle);b.SetState(NeonState.Selected);
            Assert.That(a.material,Is.SameAs(b.material));Assert.That(a.material.shader.name,Is.EqualTo("UI/Neon Surface"));
            using(var vertices=new VertexHelper()) {
                typeof(NeonPanel).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null).Invoke(a,new object[]{vertices});
                Assert.That(vertices.currentVertCount,Is.EqualTo(4));Assert.That(vertices.currentIndexCount,Is.EqualTo(6));
            }
            var channels=root.GetComponent<Canvas>().additionalShaderChannels;
            Assert.That(channels.HasFlag(AdditionalCanvasShaderChannels.TexCoord3),Is.True);
            Assert.That(a.material.HasProperty("_StencilComp")&&a.material.HasProperty("_StencilReadMask"),Is.True);
        } finally {Object.DestroyImmediate(root);}
    }
    [Test] public void RequiredIconSpritesUseTransparentSourcesAndSeparateKnowledgeMappings()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<InterfaceIconCatalog>("Assets/Game/Resources/InterfaceIcons.asset");
        Assert.That(catalog.entries, Is.Not.Empty);
        foreach (InterfaceIcon role in System.Enum.GetValues(typeof(InterfaceIcon)))
            Assert.That(catalog.entries.Count(entry => entry.role == role), Is.EqualTo(1), role.ToString());
        foreach(var entry in catalog.entries) {
            Assert.That(entry.sprite,Is.Not.Null);
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(entry.sprite));
            Assert.That(importer.DoesSourceTextureHaveAlpha(),Is.True);
            Assert.That(importer.alphaIsTransparency,Is.True);
            Assert.That(entry.sprite.rect.width,Is.LessThan(entry.sprite.texture.width));
            if(entry.item!=null)Assert.That(InterfaceIconCatalog.ForItem(entry.item),Is.SameAs(entry.sprite));
            if(entry.skill!=null)Assert.That(InterfaceIconCatalog.ForSkill(entry.skill),Is.SameAs(entry.sprite));
        }
    }
    [Test] public void RequiredVisualAssetsHaveStableDistinctIdentities()
    {
        var paths=new[]{"Assets/Game/Resources/NeonUI.mat","Assets/Game/Resources/InterfaceIcons.asset",ProceduralUISetup.PrefabFolder+"/CircleControl.prefab"};
        var before=paths.Select(AssetDatabase.AssetPathToGUID).ToArray();
        Assert.That(before.Distinct().Count(),Is.EqualTo(paths.Length));
        Assert.That(before.All(id=>!string.IsNullOrEmpty(id)),Is.True);
    }
}
