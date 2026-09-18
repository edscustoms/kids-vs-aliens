using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Opt-in rendering of the real ConstructionSite geometry. Runtime review poses are never saved.
public static class BeamHoistZoneReview
{
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    public static void Capture()
    {
        Directory.CreateDirectory("Logs/HoistZone");
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/ConstructionSite.unity");
        var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerCharacter>(true)).Single();
        var surface = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BeamHoistSurface>(true)).First();
        var ability = player.GetComponent<BeamHoistAbility>(); Invoke(ability, "Awake");
        var transport = player.GetComponent<BeamTransportController>();
        var presentation = player.GetComponent<BeamHoistZonePresentation>(); Invoke(presentation, "Awake");
        Invoke(surface, "OnEnable");
        Vector3 original = player.transform.position;
        CharacterVisual actor = null;
        Camera camera = null;
        var bakedMeshes = new System.Collections.Generic.List<Mesh>();
        var report = new StringBuilder();
        try
        {
            player.GetComponent<PlayerSkillState>().UnlockSkill(ability.RequiredSkill);
            bool found = false; Vector3 start = default, away = default;
            foreach (int i in Enumerable.Range(0, surface.CandidateCount).OrderBy(i =>
                Mathf.Abs(surface.GetBakedApproach(i).region.center.x - surface.Footprint.center.x)
                + Mathf.Abs(surface.GetBakedApproach(i).region.center.z - (surface.Footprint.min.z - ability.MaximumLateralDistance * .5f))))
            {
                var cell = surface.GetBakedApproach(i);
                if (cell.side != 2) continue;
                Vector3 center = surface.transform.TransformPoint(cell.region.center);
                var hits = Physics.RaycastAll(center + Vector3.up, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.collider.transform.IsChildOf(player.transform)).OrderBy(h => h.distance).ToArray();
                if (hits.Length == 0) continue;
                start = hits[0].point + Vector3.up * (.025f - transport.FeetOffset);
                if (!ability.CanPreviewSurfaceHoist(surface, i, start)) continue;
                away = surface.transform.forward * -(ability.MaximumLateralDistance + .8f);
                report.AppendLine($"Valid baked cell {i}; manual start {start:R}; original player pose {original:R}");
                found = true; break;
            }
            if (!found) throw new Exception("No existing valid side-2 hoist route for visual review.");
            actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CharacterVisual>("Assets/Game/Prefabs/Player/Characters/Amy.prefab"), player.transform);
            actor.transform.localPosition = Vector3.zero; actor.Animator.fireEvents = false;
            actor.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            actor.Animator.Rebind(); actor.Animator.Update(0);
            for (int i = 0; i < 60; i++) actor.Animator.Update(1f / 60);
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh(); skin.BakeMesh(mesh); bakedMeshes.Add(mesh);
                var baked = new GameObject("Review skin", typeof(MeshFilter), typeof(MeshRenderer));
                baked.transform.SetParent(skin.transform, false);
                baked.GetComponent<MeshFilter>().sharedMesh = mesh;
                baked.GetComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials; skin.enabled = false;
            }
            camera = new GameObject("Temporary Hoist Zone Review Camera").AddComponent<Camera>();
            camera.enabled = false; camera.clearFlags = CameraClearFlags.Skybox;
            camera.orthographic = true; camera.orthographicSize = 4.7f; camera.nearClipPlane = .1f; camera.farClipPlane = 150;
            Vector3 focus = start + new Vector3(-1, 1.5f, 1.5f);
            camera.transform.position = focus + new Vector3(11, 12, -13); camera.transform.LookAt(focus);
            foreach (bool active in new[] { false, true })
            {
                player.transform.position = active ? start : start + away;
                Physics.SyncTransforms();
                for (int i = 0; i < 100; i++)
                {
                    presentation.TickPresentation(.05f);
                    foreach (var particles in surface.GetComponentsInChildren<ParticleSystem>()) particles.Simulate(.05f, true, false);
                }
                var effect = surface.GetComponentInChildren<BeamHoistZoneVFX>();
                if (effect == null) throw new Exception("Zone did not reveal.");
                report.AppendLine($"{(active ? "Active" : "Nearby")}: state={effect.State}, opacity={effect.Opacity}, patches={effect.PatchCount}, player unchanged={player.transform.position == (active ? start : start + away)}");
                foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>()) particles.Simulate(.2f, true, false);
                Render(camera, active ? "active" : "nearby");
            }
            player.transform.position = original;
            File.WriteAllText("Logs/HoistZone/visual-review.txt", report.ToString());
        }
        finally
        {
            player.transform.position = original;
            Invoke(presentation, "OnDestroy"); Invoke(surface, "OnDisable");
            if (actor != null) Object.DestroyImmediate(actor.gameObject);
            if (camera != null) Object.DestroyImmediate(camera.gameObject);
            foreach (var mesh in bakedMeshes) Object.DestroyImmediate(mesh);
        }
    }
    private static void Render(Camera camera, string name)
    {
        var target = new RenderTexture(1280, 960, 24, RenderTextureFormat.ARGB32);
        var pixels = new Texture2D(1280, 960, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            // Warm the graphics resource uploads before comparing nearby/active frames.
            for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0); pixels.Apply();
            File.WriteAllBytes("Logs/HoistZone/" + name + ".png", pixels.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); }
    }
}
