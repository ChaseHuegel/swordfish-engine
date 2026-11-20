using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.Serialization.Toml;
using Xunit;

namespace Swordfish.Tests;

public class TransparentPassTests
{
    [Fact]
    public void MaterialDefaultsToOpaqueWithHalfAlphaCutoff()
    {
        var material = new Material(null!, []);

        Assert.Equal(AlphaMode.Opaque, material.AlphaMode);
        Assert.Equal(0.5f, material.AlphaCutoff);
    }

    [Fact]
    public void MaterialDefinitionParsesAlphaModeAndCutoffFromToml()
    {
        const string toml = """
            Shader = "textured"
            AlphaMode = "Masked"
            AlphaCutoff = 0.25
            Textures = ["items/laser_uv.png"]
            """;

        MaterialDefinition definition = Toml.To<MaterialDefinition>(toml);

        Assert.Equal("textured", definition.Shader);
        Assert.Equal(AlphaMode.Masked, definition.AlphaMode);
        Assert.Equal(0.25f, definition.AlphaCutoff);
    }

    [Fact]
    public void MaterialDefinitionDefaultsAlphaFieldsWhenOmitted()
    {
        const string toml = """
            Shader = "textured"
            Textures = ["bricks/stone.png"]
            """;

        MaterialDefinition definition = Toml.To<MaterialDefinition>(toml);

        Assert.Equal(AlphaMode.Opaque, definition.AlphaMode);
        Assert.Equal(0.5f, definition.AlphaCutoff);
    }

    [Fact]
    public void ResolveAlphaUsesRenderOptionsWhenNotOpaque()
    {
        var maskedMaterial = new GLMaterial(null!, [], AlphaMode.Masked, 0.3f);
        var blendMaterial = new GLMaterial(null!, [], AlphaMode.Blend, 0.5f);
        var options = new RenderOptions { AlphaMode = AlphaMode.Masked, AlphaCutoff = 0.75f };

        GLMaterial.ResolveAlpha(options, [blendMaterial, maskedMaterial], out AlphaMode mode, out float cutoff);

        Assert.Equal(AlphaMode.Masked, mode);
        Assert.Equal(0.75f, cutoff);
    }

    [Fact]
    public void ResolveAlphaInheritsFromMaterialsWithBlendOverMasked()
    {
        var opaqueMaterial = new GLMaterial(null!, [], AlphaMode.Opaque, 0.5f);
        var maskedMaterial = new GLMaterial(null!, [], AlphaMode.Masked, 0.35f);
        var blendMaterial = new GLMaterial(null!, [], AlphaMode.Blend, 0.5f);
        RenderOptions options = RenderOptions.Default;

        GLMaterial.ResolveAlpha(options, [opaqueMaterial, maskedMaterial], out AlphaMode maskedMode, out float maskedCutoff);
        Assert.Equal(AlphaMode.Masked, maskedMode);
        Assert.Equal(0.35f, maskedCutoff);

        GLMaterial.ResolveAlpha(options, [maskedMaterial, blendMaterial], out AlphaMode blendMode, out _);
        Assert.Equal(AlphaMode.Blend, blendMode);

        GLMaterial.ResolveAlpha(options, [opaqueMaterial], out AlphaMode opaqueMode, out _);
        Assert.Equal(AlphaMode.Opaque, opaqueMode);
    }

    [Fact]
    public void RenderPipelineDrawsAllOpaqueStagesBeforeTransparentStages()
    {
        var log = new List<string>();
        IRenderStage[] stages =
        [
            new RecordingStage("instanced", log, hasTransparent: true),
            new RecordingStage("batch", log, hasTransparent: true),
            new RecordingStage("lines", log, hasTransparent: false),
        ];

        var pipeline = new TestRenderPipeline(stages);
        var scene = new RenderScene(
            Matrix4x4.Identity,
            Matrix4x4.Identity,
            ReadOnlyMemory<EntityModel>.Empty,
            [],
            new LockedList<GLRenderTarget>(),
            new LockedList<GLRectRenderTarget>(),
            null
        );

        int drawCalls = pipeline.Render(0.016, scene);

        Assert.Equal(5, drawCalls);
        Assert.Equal(
            [
                "instanced:opaque",
                "batch:opaque",
                "lines:opaque",
                "instanced:transparent",
                "batch:transparent",
            ],
            log
        );
    }

    [Fact]
    public void RenderPipelineSkipsTransparentPassDuringDepthPass()
    {
        var log = new List<string>();
        IRenderStage[] stages =
        [
            new RecordingStage("instanced", log, hasTransparent: true),
            new RecordingStage("batch", log, hasTransparent: true),
        ];

        var pipeline = new TestRenderPipeline(stages);
        var scene = new RenderScene(
            Matrix4x4.Identity,
            Matrix4x4.Identity,
            ReadOnlyMemory<EntityModel>.Empty,
            [],
            new LockedList<GLRenderTarget>(),
            new LockedList<GLRectRenderTarget>(),
            null
        );

        int drawCalls = pipeline.DrawDepth(0.016, scene);

        Assert.Equal(2, drawCalls);
        Assert.Equal(["instanced:depth", "batch:depth"], log);
    }

    private sealed class RecordingStage(string name, List<string> log, bool hasTransparent) : IRenderStage
    {
        public void PreRender(double delta, RenderScene renderScene, bool excludeTransparent = false)
        {
        }

        public int Render(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback, bool excludeTransparent = false)
        {
            log.Add($"{name}:{(excludeTransparent ? "depth" : "opaque")}");
            return 1;
        }

        public int RenderTransparent(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback)
        {
            if (!hasTransparent)
            {
                return 0;
            }

            log.Add($"{name}:transparent");
            return 1;
        }
    }

    private sealed class TestRenderPipeline(IRenderStage[] stages) : RenderPipeline<IRenderStage>(stages)
    {
        public int DrawDepth(double delta, RenderScene renderScene) => Draw(delta, renderScene, isDepthPass: true);

        protected override int PreRender(double delta, RenderScene renderScene) => 0;

        protected override void PostRender(double delta, RenderScene renderScene)
        {
        }

        protected override void ShaderActivationCallback(ShaderProgram shader)
        {
        }
    }
}
