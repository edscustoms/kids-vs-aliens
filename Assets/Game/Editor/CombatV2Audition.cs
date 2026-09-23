using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Isolated source audition. Never changes the production controller.
public static class CombatV2Audition
{
    public const string Folder = "Assets/Game/Animations/Combat_v2";
    public static void FinalReview()
    {
        Directory.CreateDirectory("Logs/CombatV2");
        Diagnose();
        FightingAnimationReview.CaptureSequences();
    }
    public static void CaptureController()
    {
        foreach(var action in new[] {CharacterActionId.MeleeLight1,CharacterActionId.MeleeLight2,CharacterActionId.MeleeLight3,CharacterActionId.MeleeHeavy,CharacterActionId.Kick,CharacterActionId.HeavyKick})
            MeleeMotionReview.Capture(action);
    }
    public static void Diagnose()
    {
        Directory.CreateDirectory("Logs/CombatV2");
        var report = new StringBuilder();
        foreach (string name in new[] { "jab_left", "cross_right", "combo_hook_uppercut", "front_kick", "roundhouse_kick_right" })
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + name + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            if (name == "jab_right")
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    report.AppendLine(binding.path + " : " + binding.propertyName);
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/Amy.prefab"));
            var animator = actor.Animator;
            animator.runtimeAnimatorController = null; animator.applyRootMotion = false; animator.fireEvents = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var graph = PlayableGraph.Create(); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput.Create(graph, "sample", animator).SetSourcePlayable(playable); graph.Play();
            for (float t = 0; t < clip.length; t += .05f)
            {
                playable.SetTime(t); graph.Evaluate(0);
                report.AppendLine(FormattableString.Invariant($"{name},{t:F2},{animator.GetBoneTransform(HumanBodyBones.LeftHand).position:F3},{animator.GetBoneTransform(HumanBodyBones.RightHand).position:F3},{animator.GetBoneTransform(HumanBodyBones.RightFoot).position:F3},{animator.GetBoneTransform(HumanBodyBones.LeftFoot).position:F3}"));
            }
            graph.Destroy(); UnityEngine.Object.DestroyImmediate(actor.gameObject);
            CaptureClip(clip, "SportyGranny", new StringBuilder());
            CaptureClip(clip, "Amy", report, .3f, name.Contains("kick") ? 2.5f : 1.6f);
        }
        File.WriteAllText("Logs/CombatV2/diagnostics.txt", report.ToString());
    }
    public static void CapturePack()
    {
        Directory.CreateDirectory("Logs/CombatV2");
        var report = new StringBuilder();
        foreach (string path in Directory.GetFiles(Folder, "*.fbx").OrderBy(p => p))
        {
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            report.AppendLine($"{clip.name}: {clip.length:F3}s human={clip.humanMotion} avatar={avatar?.isValid}/{avatar?.isHuman} speed={clip.averageSpeed} angular={clip.averageAngularSpeed}");
            if (clip.humanMotion) CaptureClip(clip, "Amy", report);
        }
        File.WriteAllText("Logs/CombatV2/audition.txt", report.ToString());
    }

    private static void CaptureClip(AnimationClip clip, string character, StringBuilder report, float start = 0, float end = -1, AnimationClip[] sequence = null, float[] starts = null)
    {
        const int columns = 6, width = 280, height = 350;
        int rows = sequence == null ? 2 : 4;
        var preview = new PreviewRenderUtility();
        var meshes = new System.Collections.Generic.List<Mesh>();
        var sheet = new Texture2D(columns * width, rows * height, TextureFormat.RGB24, false);
        var graph = PlayableGraph.Create("Combat source audition");
        try
        {
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/" + character + ".prefab"));
            preview.AddSingleGO(actor.gameObject);
            var animator = actor.Animator;
            animator.runtimeAnimatorController = null;
            animator.fireEvents = false;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(true);
            var output = AnimationPlayableOutput.Create(graph, "Motion", animator);
            output.SetSourcePlayable(playable);
            var mixer = AnimationMixerPlayable.Create(graph, sequence?.Length ?? 0);
            AnimationClipPlayable[] parts = null;
            if (sequence != null)
            {
                parts = sequence.Select(c => AnimationClipPlayable.Create(graph,c)).ToArray();
                for (int p=0;p<parts.Length;p++) { parts[p].SetApplyFootIK(true); graph.Connect(parts[p],0,mixer,p); }
                output.SetSourcePlayable(mixer);
            }
            graph.Play();
            var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var skin in skins)
            {
                var mesh = new Mesh(); meshes.Add(mesh);
                var baked = new GameObject("Sample skin", typeof(MeshFilter), typeof(MeshRenderer));
                baked.transform.SetParent(skin.transform, false);
                baked.GetComponent<MeshFilter>().sharedMesh = mesh;
                baked.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                skin.enabled = false;
            }
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 1.15f;
            preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 30;
            preview.camera.backgroundColor = new Color(.1f, .12f, .16f);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.transform.position = new Vector3(3, 2.3f, 4);
            preview.camera.transform.LookAt(new Vector3(0, .85f, 0));
            preview.lights[0].intensity = 1.3f; preview.lights[0].enabled = true;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
            preview.lights[1].intensity = .9f; preview.lights[1].enabled = true;
            preview.ambientColor = new Color(.5f,.5f,.5f);
            for (int i = 0; i < columns * rows; i++)
            {
                float time = Mathf.Lerp(start, end < 0 ? clip.length : end, i / (float)(columns * rows - 1));
                int px = (i % columns) * width, py = (rows - 1 - i / columns) * height;
                playable.SetTime(time);
                if (sequence != null)
                {
                    int active = 0;
                    for (int p=0;p<parts.Length;p++)
                    {
                        parts[p].SetTime(Mathf.Max(0,time-starts[p])); mixer.SetInputWeight(p,0);
                        if (time >= starts[p]) active=p;
                    }
                    float blend = active == 0 ? 1 : Mathf.Clamp01((time-starts[active])/(active == 1 || active >= 6 ? .25f : .12f));
                    mixer.SetInputWeight(active,blend);
                    if (active > 0) mixer.SetInputWeight(active-1,1-blend);
                }
                graph.Evaluate(0);
                report.AppendLine($" {i}: {time:F3} LHand={animator.GetBoneTransform(HumanBodyBones.LeftHand).position} RHand={animator.GetBoneTransform(HumanBodyBones.RightHand).position} Hips={animator.GetBoneTransform(HumanBodyBones.Hips).position}");
                for (int s = 0; s < skins.Length; s++) skins[s].BakeMesh(meshes[s]);
                preview.BeginPreview(new Rect(0,0,width,height), GUIStyle.none); preview.Render(true);
                var rendered = (RenderTexture)preview.EndPreview();
                var previous = RenderTexture.active; RenderTexture.active = rendered;
                sheet.ReadPixels(new Rect(0,0,width,height), px, py, false); RenderTexture.active = previous;
                if (!rendered.sRGB && QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    var pixels = sheet.GetPixels(px,py,width,height);
                    for (int p = 0; p < pixels.Length; p++) pixels[p] = pixels[p].gamma;
                    sheet.SetPixels(px,py,width,height,pixels);
                }
            }
            sheet.Apply();
            File.WriteAllBytes("Logs/CombatV2/" + character + "-" + (sequence == null ? clip.name : "Sequence") + (end < 0 ? "" : "-detail") + ".png", sheet.EncodeToPNG());
        }
        finally
        {
            graph.Destroy(); preview.Cleanup(); UnityEngine.Object.DestroyImmediate(sheet);
            foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
    public static void Inspect()
    {
        Directory.CreateDirectory("Logs/CombatV2");
        Directory.CreateDirectory("Logs/CombatV2");
        var report = new StringBuilder();
        foreach (string path in Directory.GetFiles(Folder, "*.fbx").OrderBy(p => p))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            report.AppendLine(path + " type=" + importer.animationType);
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                report.AppendLine($"  {clip.name} length={clip.length} fps={clip.frameRate} human={clip.humanMotion}");
            if (path.EndsWith("jab_right.fbx"))
                foreach (var bone in root.GetComponentsInChildren<Transform>(true))
                    report.AppendLine("  bone " + bone.name + " pos=" + bone.localPosition + " rot=" + bone.localEulerAngles);
        }
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CharacterAnimationSetup.ControllerPath);
        foreach (var layer in controller.layers)
        {
            report.AppendLine($"LAYER {layer.name} weight={layer.defaultWeight} mask={layer.avatarMask?.name}");
            foreach (var s in layer.stateMachine.states)
            {
                report.AppendLine($" STATE {s.state.name} motion={s.state.motion?.name} wd={s.state.writeDefaultValues}");
                if (s.state.motion is BlendTree tree)
                    foreach (var child in tree.children) report.AppendLine($"  CHILD {child.motion?.name} pos={child.position} speed={child.timeScale}");
                foreach (var t in s.state.transitions) report.AppendLine($"  TO {t.destinationState?.name} exit={t.hasExitTime}:{t.exitTime} duration={t.duration}");
            }
            foreach (var t in layer.stateMachine.anyStateTransitions)
                report.AppendLine($" ANY -> {t.destinationState?.name} duration={t.duration} conditions=" + string.Join(";", t.conditions.Select(c => c.parameter + ":" + c.mode + ":" + c.threshold)));
        }
        File.WriteAllText("Logs/CombatV2/inspection.txt", report.ToString());
    }
}


