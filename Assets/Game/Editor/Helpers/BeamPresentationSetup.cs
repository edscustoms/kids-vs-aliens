using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Explicit art preset. Routine gameplay repair does not overwrite visual tuning.
public static class BeamPresentationSetup
{
    [MenuItem("Tools/Setup/Beam Transport/Apply Refined Energy Presentation")]
    public static void Apply()
    {
        var root = PrefabUtility.LoadPrefabContents(BeamTransportSetup.VfxPath);
        try
        {
            var visual = root.transform.Find("BeamInVFX");
            var cone = visual.GetComponentsInChildren<Transform>(true).Single(t => t.name == "BeamOuterCone");
            var editable = cone.GetComponent<ProBuilderMesh>();
            // Read the actual authored vertices; do not rebuild, replace or resize the cone.
            var vertices = editable != null ? editable.positions.ToArray() : cone.GetComponent<MeshFilter>().sharedMesh.vertices;
            var points = vertices.Select(v => visual.InverseTransformPoint(cone.TransformPoint(v))).ToArray();
            float low = points.Min(p => p.y), high = points.Max(p => p.y);
            Bounds lower = Ring(points.Where(p => p.y < low + .001f).ToArray());
            Bounds upper = Ring(points.Where(p => p.y > high - .001f).ToArray());
            var field = visual.GetComponent<BeamEnergyField>() ?? visual.gameObject.AddComponent<BeamEnergyField>();
            var motes = root.GetComponentInChildren<ParticleSystem>(true);
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            motes.transform.SetParent(visual, false);
            motes.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            motes.transform.localScale = Vector3.one;
            var main = motes.main;
            main.playOnAwake = false; main.loop = true; main.prewarm = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0; main.gravityModifier = 0;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.022f, .064f);
            main.maxParticles = 320;
            main.startColor = new ParticleSystem.MinMaxGradient(new Gradient {
                colorKeys = new[] { new GradientColorKey(new Color(.12f,.8f,1f),0), new GradientColorKey(new Color(.5f,.17f,1),.48f), new GradientColorKey(new Color(1,.12f,.72f),1) },
                alphaKeys = new[] { new GradientAlphaKey(.45f,0), new GradientAlphaKey(.9f,1) }
            }) { mode = ParticleSystemGradientMode.RandomColor };
            var shape = motes.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = (lower.center + upper.center)*.5f; shape.rotation = Vector3.zero;
            shape.scale = new Vector3(.001f, (high-low)*.97f, .001f);
            var velocity = motes.velocityOverLifetime;
            velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0,0); velocity.z = new ParticleSystem.MinMaxCurve(0,0);
            velocity.y = new ParticleSystem.MinMaxCurve(-3.1f,-1.8f);
            velocity.orbitalX=0; velocity.orbitalY=0; velocity.orbitalZ=0; velocity.radial=0; velocity.speedModifier=1;
            var noise=motes.noise; noise.enabled=false;
            var force=motes.forceOverLifetime;force.enabled=false;
            var limit=motes.limitVelocityOverLifetime;limit.enabled=false;
            var emission=motes.emission;emission.enabled=true;emission.rateOverTime=55;emission.rateOverDistance=0;
            emission.SetBursts(new[]{new ParticleSystem.Burst(0,120)});
            var size=motes.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0,.5f),new Keyframe(.15f,1),new Keyframe(.8f,.85f),new Keyframe(1,0)));
            var color=motes.colorOverLifetime;color.enabled=true;
            color.color=new Gradient {colorKeys=new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                alphaKeys=new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.8f,.75f),new GradientAlphaKey(0,1)}};
            var renderer=motes.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial=EnergyMaterial("M_BeamMotes",true,new Color(2.5f,2.5f,2.5f,1));
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;

            Transform ribbon=visual.Find("EnergySpiral");
            if(ribbon==null){ribbon=new GameObject("EnergySpiral").transform;ribbon.SetParent(visual,false);}
            var line=ribbon.GetComponent<LineRenderer>();
            if(line==null)line=ribbon.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace=false;line.alignment=LineAlignment.View;line.textureMode=LineTextureMode.Stretch;
            line.numCornerVertices=2;line.numCapVertices=2;line.widthMultiplier=.045f;
            line.widthCurve=AnimationCurve.Linear(0,1,1,.35f);
            line.colorGradient=new Gradient {colorKeys=new[]{new GradientColorKey(new Color(1,.12f,.7f),0),new GradientColorKey(new Color(.22f,.65f,1),.5f),new GradientColorKey(new Color(.8f,.2f,1),1)},
                alphaKeys=new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.05f),new GradientAlphaKey(.7f,.85f),new GradientAlphaKey(0,1)}};
            line.sharedMaterial=EnergyMaterial("M_BeamSpiral",false,new Color(5,5,5,1));
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            var data=new SerializedObject(field);
            data.FindProperty("motes").objectReferenceValue=motes;data.FindProperty("spiral").objectReferenceValue=line;
            data.FindProperty("bottomCenter").vector3Value=lower.center;data.FindProperty("topCenter").vector3Value=upper.center;
            data.FindProperty("bottomRadii").vector2Value=new Vector2(lower.extents.x,lower.extents.z);
            data.FindProperty("topRadii").vector2Value=new Vector2(upper.extents.x,upper.extents.z);
            data.ApplyModifiedPropertiesWithoutUndo();
            Tint("M_BeamCore",new Color(.95f,.12f,.55f,.078431375f),new Color(32,.4f,15));
            Tint("M_BeamMiddle",new Color(.18f,.36f,.95f,.055f),new Color(.015f,.12f,.23f));
            var outer=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/VFX/Beam/M_BeamOuter.mat");
            outer.shader=Shader.Find("Game/Beam Volume");outer.shaderKeywords=Array.Empty<string>();
            outer.SetColor("_Tint",new Color(.7f,.12f,1.5f,.16f));
            outer.SetColor("_EdgeTint",new Color(.2f,.6f,1.3f,.12f));EditorUtility.SetDirty(outer);
            PrefabUtility.SaveAsPrefabAsset(root,BeamTransportSetup.VfxPath);
            Debug.Log($"Beam presentation envelope (read only): bottom={lower} top={upper}");
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        var level=PrefabUtility.LoadPrefabContents(BeamTransportSetup.LevelStartPath);
        try {
            var data=new SerializedObject(level.GetComponent<PlayerBeamInSequence>());
            var marker=level.transform;
            if(marker.GetComponent<BeamArrivalPoint>()==null)marker.gameObject.AddComponent<BeamArrivalPoint>();
            PrefabUtility.SaveAsPrefabAsset(level,BeamTransportSetup.LevelStartPath);
        }finally{PrefabUtility.UnloadPrefabContents(level);}
        AssetDatabase.SaveAssets();
    }
    private static Bounds Ring(Vector3[] points){var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);return b;}
    private static Material EnergyMaterial(string name,bool round,Color tint)
    {
        string path="Assets/Game/Materials/VFX/Beam/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Game/Beam Energy"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_Tint",tint);mat.SetFloat("_Round",round?1:0);EditorUtility.SetDirty(mat);return mat;
    }
    private static void Tint(string name,Color color,Color emission)
    {
        var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/VFX/Beam/"+name+".mat");
        mat.SetColor("_BaseColor",color);mat.SetColor("_Color",color);mat.SetColor("_EmissionColor",emission);EditorUtility.SetDirty(mat);
    }
}
