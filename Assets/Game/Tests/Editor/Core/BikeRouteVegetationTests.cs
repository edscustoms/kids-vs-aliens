using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BikeRouteVegetationTests
{
    [Test]
    public void VegetationHasSingleLodMembershipAndTrunkOnlyCollision()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/BikeRoute.unity");
        try
        {
            var trees=Object.FindObjectsByType<LODGroup>().Where(l=>l.name.StartsWith("PF Conifer")).ToArray();
            Assert.That(trees,Is.Not.Empty);
            foreach(var tree in trees)
            {
                var levels=tree.GetLODs();
                var renderers=levels.SelectMany(l=>l.renderers).ToArray();
                Assert.That(renderers.All(r=>r!=null),Is.True);
                Assert.That(renderers.Distinct().Count(),Is.EqualTo(renderers.Length),"No renderer in multiple LOD slots");
                Assert.That(tree.GetComponentsInChildren<Renderer>().All(r=>renderers.Contains(r)),Is.True,"No extra tree outside its LOD group");
                float height=levels[0].renderers.Max(r=>r.bounds.size.y);
                var colliders=tree.GetComponentsInChildren<Collider>().Where(c=>c.enabled).ToArray();
                if(height<5)Assert.That(colliders,Is.Empty,"Small foliage must not block the bike");
                else
                {
                    Assert.That(colliders.Length,Is.EqualTo(1));
                    var trunk=colliders[0] as CapsuleCollider;
                    Assert.That(trunk,Is.Not.Null);
                    Assert.That(trunk.radius*tree.transform.lossyScale.x,Is.LessThanOrEqualTo(.381f));
                    Assert.That(trunk.height*tree.transform.lossyScale.y,Is.GreaterThanOrEqualTo(2.99f));
                    Assert.That(trunk.sharedMaterial.bounciness,Is.Zero);
                }
            }
            for(int i=0;i<trees.Length;i++)for(int j=0;j<i;j++)
            {
                var delta=trees[i].transform.position-trees[j].transform.position;
                if(Mathf.Abs(delta.y)<4)Assert.That(Vector3.ProjectOnPlane(delta,Vector3.up).magnitude,Is.GreaterThanOrEqualTo(1.999f),"No nested trunks");
            }
        }
        finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
    }
}
