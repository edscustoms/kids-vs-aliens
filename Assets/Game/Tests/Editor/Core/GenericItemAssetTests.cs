using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class GenericItemAssetTests
{
    [TestCase("BatteryCables", "Battery Cables", "Battery_Cables")]
    [TestCase("HydraulicFluid", "Hydraulic Fluid", "Hydraulic_Fluid")]
    [TestCase("IndustrialFuse", "Industrial Fuse", "Electrical_Fuse")]
    public void GenericItemImportsAndUsesExistingPickupAndCatalog(string key, string label, string icon)
    {
        var item=AssetDatabase.LoadAssetAtPath<GenericItemData>("Assets/Game/Items/Generic/"+key+".asset");
        Assert.That(item,Is.Not.Null);Assert.That(item.itemName,Is.EqualTo(label));Assert.That(item.itemType,Is.EqualTo(ItemType.Generic));
        Assert.That(item.icon,Is.SameAs(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/UI/Icons/"+icon+".png")));
        Assert.That(item.icon,Is.Not.Null);Assert.That(item.IsStackable,Is.True);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Items/Generic/PF_Item_"+key+".prefab");
        Assert.That(item.worldPrefab,Is.SameAs(prefab));Assert.That(prefab.GetComponent<PickupItem>().Item,Is.SameAs(item));
        Assert.That(prefab.GetComponent<BoxCollider>().isTrigger,Is.True);Assert.That(prefab.GetComponent<Rigidbody>().isKinematic,Is.True);
        Assert.That(prefab.GetComponent<RunWorldObject>(),Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<MeshCollider>(),Is.Empty);
        var mesh=prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
        Assert.That(mesh.vertexCount,Is.GreaterThan(30));Assert.That(mesh.triangles.Length/3,Is.LessThan(3000));
        var renderer=prefab.GetComponentInChildren<MeshRenderer>();
        Assert.That(renderer.sharedMaterials.All(m=>m!=null&&m.shader.name=="Universal Render Pipeline/Lit"),Is.True);
        Assert.That(renderer.bounds.size.magnitude,Is.InRange(.2f,1f),"Real-world metre scale");
        foreach(var asset in new Object[]{item,prefab})
        {
            var id=RunContentCatalog.Instance.Id(asset);
            Assert.That(id,Is.EqualTo(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset))));
            Assert.That(RunContentCatalog.Instance.Resolve<Object>(id),Is.SameAs(asset));
        }
    }

    [UnityTest]
    public IEnumerator AllThreeCollectAndStackThroughNormalInventoryFlow()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        yield return new EnterPlayMode();
        var owner=new GameObject("Generic pickup test player");
        var inventory=owner.AddComponent<PlayerInventory>();
        foreach(string key in new[]{"BatteryCables","HydraulicFluid","IndustrialFuse"})
        {
            var item=AssetDatabase.LoadAssetAtPath<GenericItemData>("Assets/Game/Items/Generic/"+key+".asset");
            for(int copy=0;copy<2;copy++)
            {
                var pickup=Object.Instantiate(item.worldPrefab).GetComponent<PickupItem>();
                Assert.That(pickup.TryCollect(inventory),Is.True);
                Assert.That(pickup.TryCollect(inventory),Is.False,"One pickup cannot grant twice.");
                yield return EditorTestFrame.Next();
                Assert.That(pickup==null,Is.True);
            }
            int index=inventory.Items.ToList().IndexOf(item);
            Assert.That(inventory.CountAt(index),Is.EqualTo(2));
            inventory.UseItem(index);
            Assert.That(inventory.CountAt(index),Is.EqualTo(2),"Generic supplies have no use/mission behavior yet.");
        }
        Assert.That(inventory.Items.Count,Is.EqualTo(3));
        Object.Destroy(owner);
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if(Application.isPlaying)yield return new ExitPlayMode();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    }
}
