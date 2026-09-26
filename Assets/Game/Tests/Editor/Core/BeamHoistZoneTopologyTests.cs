using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[TestFixture, Category("Core")]
public sealed class BeamHoistZoneTopologyTests
{
    public static BeamHoistZoneVFX.Patch Patch(float x, float z, float width = 1, float depth = 1)
    {
        return new BeamHoistZoneVFX.Patch { bounds = new Bounds(new Vector3(x + width / 2, 0, z + depth / 2), new Vector3(width, 1, depth)),
            a = new Vector3(x, 0, z), b = new Vector3(x + width, 0, z), c = new Vector3(x + width, 0, z + depth), d = new Vector3(x, 0, z + depth) };
    }
    public static List<BeamHoistZoneVFX.Patch> Shape(string pattern)
    {
        var cells = new List<BeamHoistZoneVFX.Patch>(); string[] rows = pattern.Split('/');
        for (int z = 0; z < rows.Length; z++)
            for (int x = 0; x < rows[z].Length; x++) if (rows[z][x] == 'X') cells.Add(Patch(x, z));
        return cells;
    }

    [TestCase("XXX/XXX", 1, 10, true)]
    [TestCase("XX./XXX", 1, 10, false)]
    [TestCase("XXX/X.X/XXX", 1, 16, false)]
    [TestCase("XX..XX/XX..XX", 2, 16, true)]
    [TestCase("X./.X", 2, 8, true)]
    public void Components_UseEdgeConnectivity_AndOnlyUnionBoundaries(string pattern, int count, float perimeter, bool rectangle)
    {
        var patches = Shape(pattern);
        var groups = BeamHoistZoneTopology.Build(patches, Vector2.one);
        Assert.That(groups.Count, Is.EqualTo(count));
        Assert.That(groups.Sum(g => g.edges.Sum(e => Vector2.Distance(e.a, e.b))), Is.EqualTo(perimeter).Within(.001));
        foreach (var group in groups)
        {
            Assert.That(group.rectangular, Is.EqualTo(rectangle));
            Assert.That(group.Contains(group.core), Is.True, "The primary core must never sit in a hole or invalid corner.");
            Assert.That(group.Clearance(group.core), Is.GreaterThanOrEqualTo(group.radius * 1.18f));
        }
        var reversed = BeamHoistZoneTopology.Build(patches.AsEnumerable().Reverse().ToArray(), Vector2.one);
        for (int i = 0; i < groups.Count; i++) Assert.That(reversed[i].core, Is.EqualTo(groups[i].core));
    }

    [TestCase(.0005f, 1)]
    [TestCase(.01f, 2)]
    public void Adjacency_ToleratesOnlyNumericalGaps_InWorldMeters(float gap, int expected)
    {
        var groups = BeamHoistZoneTopology.Build(new[] { Patch(0, 0), Patch(1 + gap / 10, 0) }, new Vector2(10, 2));
        Assert.That(groups.Count, Is.EqualTo(expected));
    }

    [Test]
    public void NumericalJoin_DoesNotLeaveAnInternalDarkStripe()
    {
        var group = BeamHoistZoneTopology.Build(new[] { Patch(0, 0, 1, 2), Patch(1.0005f, 0) }, Vector2.one).Single();
        Assert.That(group.rectangular, Is.False);
        Assert.That(group.Field(new Vector2(1.00025f, .5f)).r, Is.GreaterThan(.4f));
        Assert.That(group.Field(new Vector2(1.5f, 1.5f)).r, Is.LessThan(0), "The invalid notch remains outside.");
    }

    [Test]
    public void PartialEdgeJoin_RemovesOnlySharedBorder()
    {
        var groups = BeamHoistZoneTopology.Build(new[] { Patch(0, 0, 2, 2), Patch(2, 0, 1, 1), Patch(3, 0, 1, 1) }, Vector2.one);
        Assert.That(groups.Count, Is.EqualTo(1));
        var group = groups[0];
        Assert.That(group.Field(new Vector2(2, .5f)).r, Is.EqualTo(.5f).Within(.001), "Shared join is interior, not a border.");
        Assert.That(group.Field(new Vector2(2, 1.5f)).r, Is.EqualTo(0).Within(.001), "The unshared section remains an outer border.");
        Assert.That(group.edges.Sum(e => Vector2.Distance(e.a, e.b)), Is.EqualTo(12).Within(.001));
    }

    [TestCase("XXX/XXX")]
    [TestCase("XX./XXX")]
    [TestCase("XXX/X.X/XXX")]
    [TestCase("XX..XX/XX..XX")]
    public void Mesh_PreservesEveryCell_AndOneCorePerComponent_ThroughAllStates(string pattern)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<BeamHoistZoneVFX>(BeamHoistZoneSetup.PrefabPath);
        var effect = Object.Instantiate(prefab);
        try
        {
            var patches = Shape(pattern); effect.SetFootprint(patches);
            var mesh = effect.GetComponentInChildren<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices; var triangles = mesh.triangles; var uv = mesh.uv;
            var coreData = new List<Vector4>(); mesh.GetUVs(2, coreData);
            var groups = BeamHoistZoneTopology.Build(patches, Vector2.one);
            Assert.That(effect.PatchCount, Is.EqualTo(groups.Count));
            Assert.That(vertices.Length, Is.EqualTo(patches.Count * 4), "No bounding-box fill or duplicated overlay surfaces.");
            for (int i = 0; i < patches.Count; i++)
            {
                Assert.That(vertices[i * 4], Is.EqualTo(patches[i].a));
                Assert.That(vertices[i * 4 + 2], Is.EqualTo(patches[i].c));
            }
            foreach (var group in groups)
                foreach (int index in group.cells) Assert.That(coreData[index * 4], Is.EqualTo(coreData[group.cells[0] * 4]));
            foreach (var state in new[] { BeamHoistZoneState.Available, BeamHoistZoneState.Active, BeamHoistZoneState.Hidden,
                BeamHoistZoneState.Available, BeamHoistZoneState.Hidden })
            {
                effect.Present(state, 1, .4f, .35f);
                Assert.That(mesh.vertices, Is.EqualTo(vertices)); Assert.That(mesh.triangles, Is.EqualTo(triangles));
                Assert.That(mesh.uv, Is.EqualTo(uv));
            }
            Assert.That(ShaderUtil.ShaderHasError(Shader.Find("Game/Beam Hoist Zone")), Is.False);
        }
        finally { Object.DestroyImmediate(effect.gameObject); }
    }
}
