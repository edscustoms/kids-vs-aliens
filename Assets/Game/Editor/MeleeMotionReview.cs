using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Renders the real character/controller in an isolated preview scene, without saving gameplay scenes.</summary>
public static class MeleeMotionReview
{
    private static readonly Vector2[] Moves = { Vector2.zero, new Vector2(0, .5f), Vector2.up,
        Vector2.left, Vector2.right, new Vector2(-.7071f, .7071f), new Vector2(.7071f, .7071f),
        Vector2.down, new Vector2(-.7071f, -.7071f), new Vector2(.7071f, -.7071f) };
    private static readonly string[] Names = { "Idle", "HalfForward", "Forward", "Left", "Right",
        "ForwardLeft", "ForwardRight", "Backward", "BackwardLeft", "BackwardRight" };
    private static readonly float[] Samples = { 0, .18f, .32f, .43f, GenerateGrenadeThrowAnimation.ReleaseTime, .66f, .8f, 1f, 1.35f, 1.6f };

    public static void Capture(CharacterActionId action)
    {
        var actions = AssetDatabase.LoadAssetAtPath<CharacterAnimationActions>(CharacterAnimationSetup.ActionsPath);
        actions.TryGetBinding(action, out var binding);
        float[] times = new float[] {0, .1f, .2f, .3f, .4f, .5f, .6f, .75f, .9f, 1.3f}.Select(t => t * binding.clip.length).ToArray();
        string folder = Path.GetFullPath("Logs/Melee/" + action);
        Directory.CreateDirectory(folder);
        bool dense = false, side = false;
        var measurements = new StringBuilder("character,movement,time,handX,handY,handZ,elbowX,elbowY,elbowZ,shoulderX,shoulderY,shoulderZ\n");
        foreach (string character in new[] { "Amy", "SportyGranny" })
        {
            var preview = new PreviewRenderUtility();
            var bakedMeshes = new List<Mesh>();
            Texture2D sheet = new Texture2D(240 * times.Length, dense ? 300 : 300 * Moves.Length, TextureFormat.RGB24, false);
            try
            {
                var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterVisual>(
                    "Assets/Game/Prefabs/Player/Characters/" + character + ".prefab"));
                preview.AddSingleGO(actor.gameObject);
                var animator = actor.Animator;
                animator.fireEvents = false; // Presentation sampling must never launch gameplay objects.
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var skins = actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var skin in skins)
                {
                    // Manual Animator stepping can reuse the GPU skinning result within one Editor
                    // frame. Bake the actual evaluated bones so every captured pose is current.
                    var mesh = new Mesh(); bakedMeshes.Add(mesh);
                    var baked = new GameObject("Captured skin", typeof(MeshFilter), typeof(MeshRenderer));
                    baked.transform.SetParent(skin.transform, false);
                    baked.GetComponent<MeshFilter>().sharedMesh = mesh;
                    baked.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                }
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = character == "Amy" ? 1.05f : 1.3f;
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = 20;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.12f, .15f, .19f);
                preview.camera.transform.position = side ? new Vector3(4, 1.7f, 0) : new Vector3(3, 1.9f, 4);
                preview.camera.transform.LookAt(new Vector3(0, character == "Amy" ? .9f : 1.1f, 0));
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].enabled = true;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
                preview.lights[1].intensity = .8f;
                preview.lights[1].enabled = true;
                preview.ambientColor = new Color(.5f, .5f, .5f);
                for (int row = 0; row < Moves.Length; row++)
                {
                    int pixelY = dense ? 0 : (Moves.Length - 1 - row) * 300;
                    animator.Rebind();
                    animator.SetInteger("WeaponStyle", 0);
                    animator.SetBool("CombatStance", true);
                    animator.SetFloat("MoveX", Moves[row].x); animator.SetFloat("MoveY", Moves[row].y);
                    animator.Update(0);
                    for (int step = 0; step < 30; step++) animator.Update(1f / 60);
                    new CharacterAnimatorDriver(animator, actor.AnimationActions).TryPlayAction(action); animator.Update(0);
                    float time = 0;
                    for (int column = 0; column < times.Length; column++)
                    {
                        while (time + .00001f < times[column])
                        {
                            float dt = Mathf.Min(1f / 120, times[column] - time);
                            animator.Update(dt); time += dt;
                        }
                        // This is a presentation capture; native marker/gameplay launch is tested separately.

                        Vector3 hand = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                        Vector3 elbow = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightLowerArm).position);
                        Vector3 shoulder = actor.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position);
                        measurements.AppendLine(FormattableString.Invariant($"{character},{Names[row]},{time:F4},{hand.x:F4},{hand.y:F4},{hand.z:F4},{elbow.x:F4},{elbow.y:F4},{elbow.z:F4},{shoulder.x:F4},{shoulder.y:F4},{shoulder.z:F4}"));
                        for (int skin = 0; skin < skins.Length; skin++) skins[skin].BakeMesh(bakedMeshes[skin]);
                        preview.BeginPreview(new Rect(0, 0, 240, 300), GUIStyle.none);
                        preview.Render(true);
                        var rendered = (RenderTexture)preview.EndPreview();
                        var previous = RenderTexture.active;
                        RenderTexture.active = rendered;
                        sheet.ReadPixels(new Rect(0, 0, 240, 300), column * 240, pixelY, false);
                        RenderTexture.active = previous;
                        if (!rendered.sRGB && QualitySettings.activeColorSpace == ColorSpace.Linear)
                        {
                            var pixels = sheet.GetPixels(column * 240, pixelY, 240, 300);
                            for (int pixel = 0; pixel < pixels.Length; pixel++) pixels[pixel] = pixels[pixel].gamma;
                            sheet.SetPixels(column * 240, pixelY, 240, 300, pixels);
                        }
                    }
                    var strip = new Texture2D(sheet.width, 300, TextureFormat.RGB24, false);
                    strip.SetPixels(sheet.GetPixels(0, pixelY, sheet.width, 300));
                    strip.Apply();
                    File.WriteAllBytes(Path.Combine(folder, character + "-" + Names[row] + ".png"), strip.EncodeToPNG());
                    Object.DestroyImmediate(strip);
                }
                if (!dense)
                {
                    sheet.Apply();
                    File.WriteAllBytes(Path.Combine(folder, character + ".png"), sheet.EncodeToPNG());
                }
            }
            finally
            {
                Object.DestroyImmediate(sheet); preview.Cleanup();
                foreach (var mesh in bakedMeshes) Object.DestroyImmediate(mesh);
            }
        }
        File.WriteAllText(Path.Combine(folder, "hand-path.csv"), measurements.ToString());
        File.WriteAllText(Path.Combine(folder, "layout.txt"), "Columns (seconds): " + string.Join(", ", times) + "\nRows: " + string.Join(", ", Names));
        Debug.Log("Melee motion review captured to " + folder);
    }
}
