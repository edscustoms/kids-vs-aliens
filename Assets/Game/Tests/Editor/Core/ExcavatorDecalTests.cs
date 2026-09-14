using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[TestFixture, Category("ExcavatorDecals")]
public sealed class ExcavatorDecalTests
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>(ExcavatorDecalSetup.PrefabPath);

    [Test]
    public void AuthoredLabelsHaveSeparateOutwardGeometryAndUnmirroredUvs()
    {
        var decals = Prefab.transform.Find("Decals");
        Assert.That(decals, Is.Not.Null);
        var filters = decals.GetComponentsInChildren<MeshFilter>();
        Assert.That(filters.Length, Is.EqualTo(17));
        Assert.That(decals.GetComponentsInChildren<Collider>(), Is.Empty);
        Assert.That(Prefab.GetComponentsInChildren<Collider>().Length, Is.EqualTo(4));
        Assert.That(Prefab.transform.Find("Excavator_A_10").GetComponentsInChildren<Renderer>().Length, Is.EqualTo(10));
        Assert.That(decals.GetComponentsInChildren<Renderer>().Select(r => r.sharedMaterial).Distinct().Count(), Is.EqualTo(2));
        foreach (var f in filters)
        {
            var mesh = f.sharedMesh;
            Assert.That(mesh.vertexCount, Is.EqualTo(4), f.name);
            Vector3 normal = f.transform.TransformDirection(mesh.normals[0]);
            if (f.name.EndsWith("_L")) Assert.That(normal.z, Is.GreaterThan(.99f), f.name);
            if (f.name.EndsWith("_R")) Assert.That(normal.z, Is.LessThan(-.99f), f.name);
            Assert.That(f.transform.lossyScale.x * f.transform.lossyScale.y * f.transform.lossyScale.z, Is.GreaterThan(0), f.name);
            Assert.That(mesh.uv[1].x, Is.GreaterThan(mesh.uv[0].x), f.name);
            Assert.That(mesh.uv[2].y, Is.GreaterThan(mesh.uv[1].y), f.name);
            Assert.That(Vector3.Dot(Vector3.Cross(mesh.vertices[2] - mesh.vertices[0], mesh.vertices[1] - mesh.vertices[0]), mesh.normals[0]), Is.GreaterThan(0), f.name);
            // Bucket vertices start around X=-25.25. Every authored label stays on the machine/boom.
            foreach (var vertex in mesh.vertices)
                Assert.That(f.transform.TransformPoint(vertex).x, Is.LessThan(-25.8f), f.name);
        }
    }

    struct Triangle { public Vector3 a, ab, ac, normal; public float aa, bb, cc, inverse; }

    [Test]
    public void LabelSurfacesAreSupportedAcrossTheirWholeArea()
    {
        var triangles = new List<Triangle>();
        foreach (var f in Prefab.transform.Find("Excavator_A_10").GetComponentsInChildren<MeshFilter>())
        {
            var vertices = f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v)).ToArray();
            var indices = f.sharedMesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], ab = vertices[indices[i+1]] - a, ac = vertices[indices[i+2]] - a;
                float aa = Vector3.Dot(ab, ab), bb = Vector3.Dot(ab, ac), cc = Vector3.Dot(ac, ac);
                float determinant = aa * cc - bb * bb;
                if (determinant < 1e-12f) continue;
                triangles.Add(new Triangle { a = a, ab = ab, ac = ac, normal = Vector3.Cross(ab, ac).normalized, aa = aa, bb = bb, cc = cc, inverse = 1 / determinant });
            }
        }
        foreach (var f in Prefab.transform.Find("Decals").GetComponentsInChildren<MeshFilter>())
        {
            var normal = f.transform.TransformDirection(Vector3.back);
            var candidates = triangles.Where(t => Vector3.Dot(t.normal, normal) > .99f
                && Mathf.Abs(Vector3.Dot(f.transform.position - t.a, t.normal)) < .008f).ToArray();
            const int steps = 32;
            for (int y = 0; y <= steps; y++)
            for (int x = 0; x <= steps; x++)
            {
                Vector3 point = f.transform.TransformPoint(new Vector3(x / (float)steps - .5f, y / (float)steps - .5f, 0));
                bool supported = false;
                foreach (var t in candidates)
                {
                    Vector3 ap = point - t.a;
                    float distance = Vector3.Dot(ap, t.normal);
                    if (distance < .0002f || distance > .008f) continue;
                    float pa = Vector3.Dot(ap, t.ab), pc = Vector3.Dot(ap, t.ac);
                    float u = (t.cc * pa - t.bb * pc) * t.inverse;
                    float v = (t.aa * pc - t.bb * pa) * t.inverse;
                    if (u >= -.0001f && v >= -.0001f && u + v <= 1.0001f) { supported = true; break; }
                }
                Assert.That(supported, Is.True, f.name + " unsupported or intersecting at grid " + x + "," + y + " (" + point.ToString("F4") + ")");
            }
        }
    }

    [Test]
    public void CreateMissingPreservesManualTransformsAndRebuildResetsThemWithoutDuplicates()
    {
        var root = Object.Instantiate(Prefab);
        try
        {
            var layout = ExcavatorDecalSetup.GetOrCreateLayout();
            var node = root.transform.Find("Decals/Branding/BoomBrand_L");
            Vector3 original = node.localPosition;
            node.localPosition += new Vector3(.13f, .19f, .03f);
            node.localRotation = Quaternion.Euler(7, 23, 9);
            node.localScale = new Vector3(1.12f, .4f, 1);
            Vector3 moved = node.localPosition, scale = node.localScale;
            Quaternion rotation = node.localRotation;
            Object.DestroyImmediate(root.transform.Find("Decals/Branding/EX27_R").gameObject);
            ExcavatorDecalSetup.Configure(root, layout, false);
            Assert.That(root.transform.Find("Decals/Branding/EX27_R"), Is.Not.Null);
            Assert.That(node.localPosition, Is.EqualTo(moved));
            Assert.That(node.localRotation, Is.EqualTo(rotation));
            Assert.That(node.localScale, Is.EqualTo(scale));
            int count = root.GetComponentsInChildren<Transform>().Length;
            ExcavatorDecalSetup.Configure(root, layout, true);
            Assert.That(Vector3.Distance(node.localPosition, original), Is.LessThan(.00001f));
            ExcavatorDecalSetup.Configure(root, layout, true);
            Assert.That(root.GetComponentsInChildren<Transform>().Length, Is.EqualTo(count));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void GroupCacheAndFadeIncludeEveryDecalWithoutStructuralLabelOutlines()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var host = new GameObject("Excavator fade test");
        SceneManager.MoveGameObjectToScene(host, scene);
        var controller = host.AddComponent<CameraOcclusionController>();
        controller.enabled = false;
        var machine = Object.Instantiate(Prefab, host.transform);
        try
        {
            Invoke(controller, "BuildLevelCache");
            var states = (IDictionary)typeof(CameraOcclusionController).GetField("rendererStates", Private).GetValue(controller);
            var targets = (IDictionary)typeof(CameraOcclusionController).GetField("targetByRoot", Private).GetValue(controller);
            Assert.That(targets.Count, Is.EqualTo(1));
            Assert.That(states.Count, Is.EqualTo(27));
            foreach (var renderer in machine.GetComponentsInChildren<Renderer>())
            {
                object state = states[renderer];
                Assert.That(state, Is.Not.Null, renderer.name);
                bool decal = renderer.transform.IsChildOf(machine.transform.Find("Decals"));
                if (decal) Assert.That(state.GetType().GetField("structuralLineMesh").GetValue(state), Is.Null, renderer.name);
                else Assert.That(state.GetType().GetField("structuralLineMesh").GetValue(state), Is.Not.Null, renderer.name);
                var original = renderer.sharedMaterials;
                Invoke(controller, "EnsureFadeMaterials", renderer, state);
                foreach (float fade in new[] { .5f, 0f, 1f })
                {
                    Invoke(controller, "ApplyFadeToMaterials", state, fade);
                    var fadedMaterials = renderer.sharedMaterials;
                    for (int i = 0; i < fadedMaterials.Length; i++)
                    {
                        var material = fadedMaterials[i];
                        if (material.HasProperty("_Fade")) Assert.That(material.GetFloat("_Fade"), Is.EqualTo(fade));
                        else if (material.HasProperty("_BaseColor")) Assert.That(material.GetColor("_BaseColor").a, Is.EqualTo(original[i].GetColor("_BaseColor").a * fade).Within(.00001f));
                    }
                }
                Invoke(controller, "RestoreOriginalMaterials", renderer, state);
                Assert.That(renderer.sharedMaterials, Is.EqualTo(original));
            }
        }
        finally
        {
            Invoke(controller, "DestroyRuntimeMaterials");
            Invoke(controller, "DestroyOcclusionLineResources");
            Object.DestroyImmediate(host);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void RealGroupFadeRendersNoResidualLabelsAtZeroVisibility()
    {
        var preview = new PreviewRenderUtility();
        CameraOcclusionController controller = null;
        IDictionary states = null;
        try
        {
            var machine = Object.Instantiate(Prefab);
            preview.AddSingleGO(machine);
            controller = machine.AddComponent<CameraOcclusionController>();
            controller.enabled = false;
            Invoke(controller, "BuildLevelCache");
            states = (IDictionary)typeof(CameraOcclusionController).GetField("rendererStates", Private).GetValue(controller);
            var renderers = machine.GetComponentsInChildren<Renderer>();
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 4.5f;
            preview.camera.nearClipPlane = .05f;
            preview.camera.farClipPlane = 80;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.22f,.25f,.29f);
            Vector3 target = new Vector3(-30,3.7f,-15.7f);
            preview.camera.transform.position = target + new Vector3(-12,10,-18);
            preview.camera.transform.LookAt(target);
            preview.ambientColor = new Color(.5f,.5f,.5f);
            preview.lights[0].enabled = true;
            preview.lights[0].intensity = 1.5f;
            preview.lights[0].transform.rotation = Quaternion.Euler(45,35,0);
            Directory.CreateDirectory(ExcavatorDecalReview.OutputFolder);
            Color32[] zero = null;
            foreach (float fade in new[] { 1f, .5f, 0f })
            {
                foreach (var renderer in renderers)
                {
                    Invoke(controller, "EnsureFadeMaterials", renderer, states[renderer]);
                    Invoke(controller, "ApplyFadeToMaterials", states[renderer], fade);
                }
                var image = ExcavatorDecalReview.ReadPreview(preview, 1200, 900);
                try
                {
                    File.WriteAllBytes(Path.Combine(ExcavatorDecalReview.OutputFolder, "group-fade-" + Mathf.RoundToInt(fade*100) + ".png"), image.EncodeToPNG());
                    if (fade == 0) zero = image.GetPixels32();
                }
                finally { Object.DestroyImmediate(image); }
            }
            foreach (var renderer in renderers) renderer.enabled = false;
            var hidden = ExcavatorDecalReview.ReadPreview(preview, 1200, 900);
            try
            {
                var reference = hidden.GetPixels32();
                int visiblePixels = 0;
                for (int i = 0; i < reference.Length; i++)
                    if (Math.Abs(reference[i].r-zero[i].r) > 2 || Math.Abs(reference[i].g-zero[i].g) > 2 || Math.Abs(reference[i].b-zero[i].b) > 2) visiblePixels++;
                Assert.That(visiblePixels, Is.Zero, "Fade=0 must match disabling every renderer; labels must leave no visible pixels.");
            }
            finally { Object.DestroyImmediate(hidden); }
        }
        finally
        {
            if (controller != null && states != null)
            {
                foreach (DictionaryEntry pair in states) Invoke(controller, "RestoreOriginalMaterials", pair.Key, pair.Value);
                Invoke(controller, "DestroyRuntimeMaterials");
                Invoke(controller, "DestroyOcclusionLineResources");
            }
            preview.Cleanup();
        }
    }

    [Test]
    public void DecalShaderCompilesAndKeepsFixedRenderState()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ExcavatorDecalSetup.ShaderPath);
        Assert.That(shader, Is.Not.Null);
        Assert.That(ShaderUtil.GetShaderMessages(shader), Is.Empty);
        Assert.That(shader.isSupported, Is.True);
        foreach (var renderer in Prefab.transform.Find("Decals").GetComponentsInChildren<Renderer>())
        {
            var material = renderer.sharedMaterial;
            Assert.That(material.shader, Is.SameAs(shader));
            Assert.That(material.renderQueue, Is.EqualTo(2475));
            Assert.That(material.GetFloat("_Fade"), Is.EqualTo(1));
            Assert.That(material.GetTag("CameraOcclusionLines", false), Is.EqualTo("Off"));
        }
    }

    static void Invoke(CameraOcclusionController controller, string method, params object[] args)
        => typeof(CameraOcclusionController).GetMethod(method, Private).Invoke(controller, args);
}
