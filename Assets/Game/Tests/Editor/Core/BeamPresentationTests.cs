using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BeamPresentationTests
{
    [TestCase("Assets/Game/Scenes/ConstructionSite.unity")]
    [TestCase("Assets/Game/Scenes/GamePoc.unity")]
    public void RepairPreservesLevelStartRootPose(string path)
    {
        var scene=EditorSceneManager.OpenPreviewScene(path);
        try {
            var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
            BeamTransportSetup.ConfigureScene(player);
            var sequence=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerBeamInSequence>(true)).Single();
            var data=new SerializedObject(sequence);
            var marker=sequence.ArrivalTransform;
            marker.name="My Authored Arrival";
            marker.SetPositionAndRotation(new Vector3(13,2,-7),Quaternion.Euler(0,73,0));
            Vector3 authoredPosition=marker.position;
            int count=sequence.GetComponentsInChildren<Transform>(true).Length;
            BeamTransportSetup.ConfigureScene(player);BeamTransportSetup.ConfigureScene(player);
            data.Update();
            Assert.That(sequence.ArrivalTransform,Is.EqualTo(marker));
            Assert.That(marker.position,Is.EqualTo(authoredPosition));
            Assert.That(Quaternion.Angle(marker.rotation,Quaternion.Euler(0,73,0)),Is.LessThan(.001));
            Assert.That(marker.GetComponents<BeamArrivalPoint>().Length,Is.EqualTo(1));
            Assert.That(sequence.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(count));
        } finally {EditorSceneManager.ClosePreviewScene(scene);}
    }

    [TestCase(BeamTransportDirection.Up)]
    [TestCase(BeamTransportDirection.Down)]
    public void SharedEffectKeepsParticlesInsideConeAndClearsOnReuse(BeamTransportDirection direction)
    {
        var effect=Object.Instantiate(AssetDatabase.LoadAssetAtPath<BeamTransportVFX>(BeamTransportSetup.VfxPath));
        try {
            var field=effect.GetComponentInChildren<BeamEnergyField>(true);
            var serialized=new SerializedObject(field);
            int authoredCount=serialized.FindProperty("spiralCount").intValue;
            var low=serialized.FindProperty("bottomCenter").vector3Value;
            var high=serialized.FindProperty("topCenter").vector3Value;
            var bottom=serialized.FindProperty("bottomRadii").vector2Value;
            var top=serialized.FindProperty("topRadii").vector2Value;
            var motes=effect.GetComponentInChildren<ParticleSystem>(true);
            var buffer=new ParticleSystem.Particle[320];
            for(int repeat=0;repeat<3;repeat++) {
                effect.Show(new Vector3(repeat*4,0,0),direction);
                typeof(BeamEnergyField).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(field,null);
                motes.Simulate(.4f,true,true);motes.Play();
                typeof(BeamEnergyField).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(field,null);
                int count=motes.GetParticles(buffer);Assert.That(count,Is.GreaterThan(20));
                Assert.That(buffer.Take(count).Any(p=>Mathf.Abs(p.position.x)>.1f),Is.True,"Motes must fill the cone, not remain on its axis");
                Assert.That(effect.GetComponentInChildren<LineRenderer>().positionCount,Is.EqualTo(161));
                var strands=field.GetComponentsInChildren<LineRenderer>();
                Assert.That(strands.Length,Is.EqualTo(authoredCount));
                float previousAngle=0;
                for(int strand=0;strand<strands.Length;strand++)
                {
                    Assert.That(strands[strand].sharedMaterial,Is.EqualTo(strands[0].sharedMaterial));
                    Assert.That(strands[strand].widthMultiplier,Is.EqualTo(strands[0].widthMultiplier));
                    var p=strands[strand].GetPosition(80);var first=strands[0].GetPosition(80);
                    Assert.That(p.y,Is.EqualTo(first.y).Within(.00001f));
                    float t=(p.y-low.y)/(high.y-low.y);var r=Vector2.Lerp(bottom,top,t);var c=Vector3.Lerp(low,high,t);
                    float angle=Mathf.Atan2((p.z-c.z)/r.y,(p.x-c.x)/r.x)*Mathf.Rad2Deg;
                    if(strand>0)Assert.That(Mathf.Repeat(angle-previousAngle,360),Is.EqualTo(360f/authoredCount).Within(.001f));
                    previousAngle=angle;
                }
                for(int i=0;i<count;i++) {
                    if(buffer[i].remainingLifetime<=0)continue;
                    var p=field.transform.InverseTransformPoint(motes.transform.TransformPoint(buffer[i].position));
                    float t=(p.y-low.y)/(high.y-low.y);Assert.That(t,Is.InRange(0f,1f));
                    var r=Vector2.Lerp(bottom,top,t);var c=Vector3.Lerp(low,high,t);
                    Assert.That(Mathf.Pow((p.x-c.x)/r.x,2)+Mathf.Pow((p.z-c.z)/r.y,2),Is.LessThan(1));
                    Assert.That(buffer[i].totalVelocity.y*(direction==BeamTransportDirection.Up?1:-1),Is.GreaterThan(.1f));
                }
                effect.Hide();Assert.That(motes.particleCount,Is.Zero);
                Assert.That(field.gameObject.activeInHierarchy,Is.False);
            }
            Assert.That(effect.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(1));
            Assert.That(effect.GetComponentsInChildren<LineRenderer>(true).Length,Is.EqualTo(authoredCount));
            foreach(int total in new[]{1,6,3})
            {
                serialized.FindProperty("spiralCount").intValue=total;serialized.ApplyModifiedPropertiesWithoutUndo();
                effect.Show(Vector3.zero,direction);
                typeof(BeamEnergyField).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(field,null);
                Assert.That(effect.GetComponentsInChildren<LineRenderer>().Length,Is.EqualTo(total));
                Assert.That(effect.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(1));
                effect.Hide();
            }
        }finally{Object.DestroyImmediate(effect.gameObject);}
    }
}
