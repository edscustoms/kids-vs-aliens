using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Draws only the existing controller's faded renderers. No detection or fade ownership.
public sealed class CameraOcclusionSilhouetteFeature : ScriptableRendererFeature
{
    private SilhouettePass pass;
    public static int LastMaskDraws { get; private set; }
    public static int LastMaskTriangles { get; private set; }
    public static Vector2Int LastMaskSize { get; private set; }

    public override void Create()
    {
        pass?.Dispose();
        pass = new SilhouettePass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        LastMaskDraws = LastMaskTriangles = 0;
        var controller = CameraOcclusionController.Active;
        if (pass.controller != controller) pass.ChangeController(controller);
        if (controller == null || renderingData.cameraData.cameraType != CameraType.Game) return;
        controller.CollectSilhouetteRenderers(renderingData.cameraData.camera, pass.renderers);
        if (pass.renderers.Count == 0) return;
        pass.controller = controller;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) => pass?.Dispose();

    private sealed class SilhouettePass : ScriptableRenderPass
    {
        private static readonly int Strength = Shader.PropertyToID("_OcclusionSilhouetteStrength");
        private static readonly int Size = Shader.PropertyToID("_OcclusionMaskSize");
        private static readonly int Mask = Shader.PropertyToID("_OcclusionSilhouetteMask");
        private static readonly int Depth = Shader.PropertyToID("_OcclusionSceneDepth");
        public readonly List<CameraOcclusionController.SilhouetteRenderer> renderers = new();
        public CameraOcclusionController controller;
        private readonly Dictionary<Material, Material> masks = new();
        private readonly List<Draw> draws = new();
        private readonly Plane[] frustum = new Plane[6];
        private Material composite;
        private Shader shader;

        private readonly struct Draw
        {
            public readonly Renderer renderer;
            public readonly Material material;
            public readonly int submesh;
            public readonly float strength;
            public Draw(Renderer renderer, Material material, int submesh, float strength)
            { this.renderer = renderer; this.material = material; this.submesh = submesh; this.strength = strength; }
        }
        private sealed class MaskData
        {
            public List<Draw> draws;
            public TextureHandle sceneDepth;
            public Vector4 size;
        }
        private sealed class OutlineData
        {
            public TextureHandle mask;
            public Material material;
            public MaterialPropertyBlock properties;
        }
        private readonly MaterialPropertyBlock outlineProperties = new();

        public SilhouettePass() => ConfigureInput(ScriptableRenderPassInput.Depth);

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var camera = frameData.Get<UniversalCameraData>();
            var resources = frameData.Get<UniversalResourceData>();
            if (shader == null) shader = Resources.Load<Shader>("SH_CameraOcclusionLines");
            if (shader == null || !shader.isSupported || !resources.cameraDepthTexture.IsValid()) return;
            if (composite == null) composite = CoreUtils.CreateEngineMaterial(shader);
            draws.Clear();
            GeometryUtility.CalculateFrustumPlanes(camera.camera, frustum);
            foreach (var item in renderers)
            {
                // Bounds are used only to avoid off-camera draws, never to generate visible geometry.
                if (item.renderer == null || !GeometryUtility.TestPlanesAABB(frustum, item.renderer.bounds)) continue;
                Mesh mesh = item.renderer is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : item.renderer is MeshRenderer ? item.renderer.GetComponent<MeshFilter>()?.sharedMesh : null;
                if (mesh == null) continue;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (item.materials.Length == 0) continue;
                    var source = item.materials[Mathf.Min(submesh, item.materials.Length - 1)];
                    if (source == null || source.GetTag("CameraOcclusionLines", false, "") == "Off") continue;
                    if (!masks.TryGetValue(source, out var material))
                    {
                        material = CoreUtils.CreateEngineMaterial(shader);
                        // Common URP alpha-cutout surfaces retain texture holes. The camera fade's
                        // dither is deliberately not copied into the solid silhouette mask.
                        bool cutout = !source.HasProperty("_Fade") && (source.IsKeywordEnabled("_ALPHATEST_ON")
                            || source.GetTag("RenderType", false) == "TransparentCutout");
                        material.SetFloat("_MaskAlphaClip", cutout ? 1 : 0);
                        if (cutout)
                        {
                            string map = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                            if (source.HasProperty(map))
                            {
                                material.SetTexture("_MaskBaseMap", source.GetTexture(map));
                                Vector2 scale = source.GetTextureScale(map), offset = source.GetTextureOffset(map);
                                material.SetVector("_MaskUV", new Vector4(scale.x, scale.y, offset.x, offset.y));
                            }
                            material.SetFloat("_MaskCutoff", source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : .5f);
                            material.SetFloat("_MaskAlpha", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor").a : 1f);
                        }
                        masks.Add(source, material);
                    }
                    draws.Add(new Draw(item.renderer, material, submesh, item.strength));
                    LastMaskTriangles += (int)mesh.GetIndexCount(submesh) / 3;
                }
            }
            LastMaskDraws = draws.Count;
            if (draws.Count == 0) return;
            int width = Mathf.Max(1, camera.cameraTargetDescriptor.width / 2);
            int height = Mathf.Max(1, camera.cameraTargetDescriptor.height / 2);
            LastMaskSize = new Vector2Int(width, height);
            var mask = graph.CreateTexture(new TextureDesc(width, height)
            {
                name = "Faded mesh silhouette mask", colorFormat = GraphicsFormat.R8G8_UNorm,
                clearBuffer = true, clearColor = Color.clear, filterMode = FilterMode.Bilinear
            });
            var depth = graph.CreateTexture(new TextureDesc(width, height)
            {
                name = "Faded mesh silhouette depth", depthBufferBits = DepthBits.Depth16,
                clearBuffer = true
            });
            using (var builder = graph.AddRasterRenderPass<MaskData>("Occlusion silhouettes / faded meshes", out var data))
            {
                data.draws = draws;
                data.sceneDepth = resources.cameraDepthTexture;
                data.size = new Vector4(width, height, 1f / width, 1f / height);
                builder.UseTexture(data.sceneDepth);
                builder.SetRenderAttachment(mask, 0);
                builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (MaskData d, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(Size, d.size);
                    context.cmd.SetGlobalTexture(Depth, d.sceneDepth);
                    foreach (var draw in d.draws)
                    {
                        context.cmd.SetGlobalFloat(Strength, draw.strength);
                        context.cmd.DrawRenderer(draw.renderer, draw.material, draw.submesh, 0);
                    }
                });
            }
            outlineProperties.SetColor("_LineColor", controller.SilhouetteColor);
            outlineProperties.SetFloat("_DashLengthPixels", controller.SilhouetteDashLength);
            outlineProperties.SetFloat("_DashFill", controller.SilhouetteDashFill);
            outlineProperties.SetVector("_OutlineStep", new Vector4(controller.SilhouetteWidth / (width * 2f),
                controller.SilhouetteWidth / (height * 2f), width * 2, height * 2));
            using (var builder = graph.AddRasterRenderPass<OutlineData>("Occlusion silhouettes / contour", out var data))
            {
                data.mask = mask; data.material = composite; data.properties = outlineProperties;
                builder.UseTexture(mask);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (OutlineData d, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(Mask, d.mask);
                    context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 1, MeshTopology.Triangles, 3, 1, d.properties);
                });
            }
        }

        public void Dispose()
        {
            CoreUtils.Destroy(composite);
            foreach (var material in masks.Values) CoreUtils.Destroy(material);
            masks.Clear();
        }

        public void ChangeController(CameraOcclusionController value)
        {
            foreach (var material in masks.Values) CoreUtils.Destroy(material);
            masks.Clear(); renderers.Clear(); draws.Clear();
            controller = value;
        }
    }
}
