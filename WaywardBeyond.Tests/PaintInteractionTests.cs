#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Interactions;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Tests validation rules, shape face expansion, active-mode clear semantics, and server commit for face painting.</summary>
public class PaintInteractionTests
{
    private sealed class StubBrickDatabase : IBrickDatabase
    {
        private readonly Dictionary<ushort, Brick> _byId = [];
        public void Add(ushort id, Brick brick) => _byId[id] = brick;
        public Result<Brick> Get(string id) => Result<Brick>.FromFailure((string?)null);
        public Result<Brick> Get(ushort id) => _byId.TryGetValue(id, out Brick? b) ? Result<Brick>.FromSuccess(b) : Result<Brick>.FromFailure((string?)null);
        public List<Brick> Get(Func<Brick, bool> predicate) => [.. _byId.Values.Where(predicate)];
        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }

    private sealed class StubPaintDatabase : IPaintDatabase, IPaintRegistry
    {
        private readonly Dictionary<ushort, Paint> _byId = [];
        private readonly Dictionary<string, ushort> _idByName = new(StringComparer.Ordinal);

        public void Add(ushort id, Paint paint)
        {
            paint.BindDataID(id);
            _byId[id] = paint;
            _idByName[paint.ID] = id;
        }

        public IReadOnlyList<Paint> TexturePaints => [.. _byId.Values.Where(p => p.Kind == PaintKind.Texture)];
        public IReadOnlyList<Paint> ColorPaints => [.. _byId.Values.Where(p => p.Kind == PaintKind.Color)];
        public ushort Id(string name) => _idByName.TryGetValue(name, out ushort id) ? id : (ushort)0;
        public string? Name(ushort id) => _byId.TryGetValue(id, out Paint? p) ? p.ID : null;
        public Result<Paint> Get(string id) => _idByName.TryGetValue(id, out ushort num) ? Result<Paint>.FromSuccess(_byId[num]) : Result<Paint>.FromFailure((string?)null);
        public Result<Paint> Get(ushort id) => _byId.TryGetValue(id, out Paint? p) ? Result<Paint>.FromSuccess(p) : Result<Paint>.FromFailure((string?)null);
        public List<Paint> Get(Func<Paint, bool> predicate) => [.. _byId.Values.Where(predicate)];
    }

    private sealed class StubWorld(VoxelTarget target, Uuid targetUuid) : IVoxelInteractionWorld
    {
        public Result<VoxelTarget> GetTarget(in Uuid entityUuid) =>
            entityUuid == targetUuid ? Result<VoxelTarget>.FromSuccess(target) : Result<VoxelTarget>.FromFailure((string?)null);
        public bool IntersectsCollider(in VoxelTarget target, in Int3 coordinate, Brick placeable, BrickShape shape, Orientation orientation) => false;
    }

    private static (StubBrickDatabase bricks, StubPaintDatabase paints, VoxelObject voxels, StubWorld world, Uuid uuid) CreateHarness()
    {
        var bricks = new StubBrickDatabase();
        bricks.Add(1, new Brick("wb:panel", BrickAlpha.Opaque, false, null, BrickShape.Any, default, ["paintable"]));
        bricks.Add(2, new Brick("wb:rock", BrickAlpha.Opaque, false, null, BrickShape.Any, default, ["environment"]));
        bricks.Add(3, new Brick("wb:glass", BrickAlpha.Blend, false, null, BrickShape.Any, default, ["paintable"]));
        bricks.Add(4, new Brick("wb:grate", BrickAlpha.Masked, false, null, BrickShape.Any, default, ["paintable"]));
        bricks.Add(5, new Brick("wb:thruster", BrickAlpha.Opaque, false, "thruster.obj", BrickShape.Custom, default, ["paintable"]));

        var paints = new StubPaintDatabase();
        paints.Add(1, Paint.CreateTexture("wb:caution", "caution", BrickAlpha.Opaque, false, ["caution"]));
        paints.Add(2, Paint.CreateTexture("wb:glass", "glass", BrickAlpha.Blend, true, ["glass.0"]));
        paints.Add(3, Paint.CreateTexture("wb:grate", "grate", BrickAlpha.Masked, false, ["grate"]));
        paints.Add(4, Paint.CreateColor("wb:red", "red", new Vector3(1f, 0f, 0f)));

        var voxels = new VoxelObject(16);
        var uuid = Uuid.FromValue(42);
        var target = new VoxelTarget(entity: 7, voxels, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        var world = new StubWorld(target, uuid);
        return (bricks, paints, voxels, world, uuid);
    }

    [Fact]
    public void RejectsNonPaintableBrickAndEnforcesLikeAlphaForTexturePaints()
    {
        var (bricks, paints, voxels, world, uuid) = CreateHarness();
        voxels.Set(0, 0, 0, new Voxel(2, new ShapeLight(BrickShape.Block, 15), 0)); // rock (not paintable)
        voxels.Set(1, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0)); // opaque panel
        voxels.Set(2, 0, 0, new Voxel(3, new ShapeLight(BrickShape.Block, 15), 0)); // blend glass
        voxels.Set(3, 0, 0, new Voxel(4, new ShapeLight(BrickShape.Block, 15), 0)); // masked grate

        //  Non-paintable rock is rejected.
        var rockReq = MakeRequest(uuid, 0, 0, 0, BrickFaceMask.Front, PaintKind.Texture, "wb:caution", InteractionKind.PrimaryPressed);
        Assert.Equal(InteractionAction.None, PaintInteractionResolver.Resolve(in rockReq, world, bricks, paints, paints).Action);

        //  Opaque paint on opaque brick succeeds; blend paint on opaque brick is rejected.
        var opaqueOk = MakeRequest(uuid, 1, 0, 0, BrickFaceMask.Front, PaintKind.Texture, "wb:caution", InteractionKind.PrimaryPressed);
        Assert.Equal(InteractionAction.Paint, PaintInteractionResolver.Resolve(in opaqueOk, world, bricks, paints, paints).Action);

        var blendOnOpaque = MakeRequest(uuid, 1, 0, 0, BrickFaceMask.Front, PaintKind.Texture, "wb:glass", InteractionKind.PrimaryPressed);
        Assert.Equal(InteractionAction.None, PaintInteractionResolver.Resolve(in blendOnOpaque, world, bricks, paints, paints).Action);

        //  Blend paint on blend glass succeeds; masked paint on masked grate succeeds.
        var blendOk = MakeRequest(uuid, 2, 0, 0, BrickFaceMask.Front, PaintKind.Texture, "wb:glass", InteractionKind.PrimaryPressed);
        Assert.Equal(InteractionAction.Paint, PaintInteractionResolver.Resolve(in blendOk, world, bricks, paints, paints).Action);

        var maskedOk = MakeRequest(uuid, 3, 0, 0, BrickFaceMask.Front, PaintKind.Texture, "wb:grate", InteractionKind.PrimaryPressed);
        Assert.Equal(InteractionAction.Paint, PaintInteractionResolver.Resolve(in maskedOk, world, bricks, paints, paints).Action);

        //  Color paint works on any paintable brick regardless of alpha.
        var colorOnGlass = MakeRequest(uuid, 2, 0, 0, BrickFaceMask.Front, PaintKind.Color, "wb:red", InteractionKind.PrimaryPressed);
        Assert.Equal(InteractionAction.Paint, PaintInteractionResolver.Resolve(in colorOnGlass, world, bricks, paints, paints).Action);
    }

    [Fact]
    public void ExpandsSlopeFrontOrTopAndCustomAllFaces()
    {
        var (bricks, paints, voxels, world, uuid) = CreateHarness();
        voxels.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Slope, 15), 0));
        voxels.Set(1, 0, 0, new Voxel(5, new ShapeLight(BrickShape.Custom, 15), 0));

        var slopeFront = MakeRequest(uuid, 0, 0, 0, BrickFaceMask.Front, PaintKind.Texture, "wb:caution", InteractionKind.PrimaryPressed);
        InteractionResolution slopeFrontRes = PaintInteractionResolver.Resolve(in slopeFront, world, bricks, paints, paints);
        Assert.Equal(BrickFaceMask.Front | BrickFaceMask.Top, slopeFrontRes.Paint.FaceMask);

        var slopeTop = MakeRequest(uuid, 0, 0, 0, BrickFaceMask.Top, PaintKind.Texture, "wb:caution", InteractionKind.PrimaryPressed);
        InteractionResolution slopeTopRes = PaintInteractionResolver.Resolve(in slopeTop, world, bricks, paints, paints);
        Assert.Equal(BrickFaceMask.Front | BrickFaceMask.Top, slopeTopRes.Paint.FaceMask);

        var slopeBack = MakeRequest(uuid, 0, 0, 0, BrickFaceMask.Back, PaintKind.Texture, "wb:caution", InteractionKind.PrimaryPressed);
        InteractionResolution slopeBackRes = PaintInteractionResolver.Resolve(in slopeBack, world, bricks, paints, paints);
        Assert.Equal(BrickFaceMask.Back, slopeBackRes.Paint.FaceMask);

        var customLeft = MakeRequest(uuid, 1, 0, 0, BrickFaceMask.Left, PaintKind.Color, "wb:red", InteractionKind.PrimaryPressed);
        InteractionResolution customRes = PaintInteractionResolver.Resolve(in customLeft, world, bricks, paints, paints);
        Assert.Equal(BrickFaceMask.All, customRes.Paint.FaceMask);
    }

    [Fact]
    public void CommitterAppliesAndClearsOnlyActiveModeSlot()
    {
        var store = new DataStore();
        int entity = store.Alloc(Uuid.FromValue(100));
        var voxels = new VoxelObject(16);
        voxels.Set(1, 2, 3, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0));
        store.AddOrUpdate(entity, new VoxelWorldComponent(voxels));

        var committer = new VoxelFaceWriteCommitter(new ServerPeers(), NullLogger<VoxelFaceWriteCommitter>.Instance);
        var world = new StubWorld(new VoxelTarget(entity, voxels, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One)), Uuid.FromValue(100));

        //  Apply texture paint.
        var texReq = MakeRequest(Uuid.FromValue(100), 1, 2, 3, BrickFaceMask.Front, PaintKind.Texture, "wb:caution", InteractionKind.PrimaryPressed);
        var ctx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 1, world, in texReq, heldItemID: "wb:sprayer");
        ctx.Resolution = new InteractionResolution(entity, new Int3(1, 2, 3), new PaintResolution(BrickFaceMask.Front, PaintKind.Texture, 1, "wb:caution"));
        Assert.Equal(InteractionDecision.Handled, committer.Handle(ref ctx));

        //  Apply color paint (preserves texture paint).
        var colReq = MakeRequest(Uuid.FromValue(100), 1, 2, 3, BrickFaceMask.Front, PaintKind.Color, "wb:red", InteractionKind.PrimaryPressed);
        ctx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 2, world, in colReq, heldItemID: "wb:sprayer");
        ctx.Resolution = new InteractionResolution(entity, new Int3(1, 2, 3), new PaintResolution(BrickFaceMask.Front, PaintKind.Color, 4, "wb:red"));
        Assert.Equal(InteractionDecision.Handled, committer.Handle(ref ctx));

        Assert.True(voxels.TryGetFaceOverride(1, 2, 3, BrickFace.Front, out FaceOverride both));
        Assert.Equal(1, both.TexturePaintId);
        Assert.Equal(4, both.ColorPaintId);

        //  Secondary clear in Color mode clears ONLY color, preserving texture.
        var clearColReq = MakeRequest(Uuid.FromValue(100), 1, 2, 3, BrickFaceMask.Front, PaintKind.Color, null, InteractionKind.SecondaryPressed);
        ctx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 3, world, in clearColReq, heldItemID: "wb:sprayer");
        ctx.Resolution = new InteractionResolution(entity, new Int3(1, 2, 3), new PaintResolution(BrickFaceMask.Front, PaintKind.Color, 0, null));
        Assert.Equal(InteractionDecision.Handled, committer.Handle(ref ctx));

        Assert.True(voxels.TryGetFaceOverride(1, 2, 3, BrickFace.Front, out FaceOverride afterColorClear));
        Assert.Equal(1, afterColorClear.TexturePaintId);
        Assert.Equal(0, afterColorClear.ColorPaintId);
    }

    [Fact]
    public void ValidateApplyTargetReportsSpecificFailureReasonsForPreviewAndNotifications()
    {
        var (bricks, paints, _, _, _) = CreateHarness();
        var empty = new Voxel();
        var rock = new Voxel(2, new ShapeLight(BrickShape.Block, 15), 0);
        var panel = new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0);
        var glass = new Voxel(3, new ShapeLight(BrickShape.Block, 15), 0);
        var grate = new Voxel(4, new ShapeLight(BrickShape.Block, 15), 0);

        Assert.Equal(PaintTargetValidation.EmptyCell, PaintInteractionResolver.ValidateApplyTarget(in empty, PaintKind.Texture, "wb:caution", bricks, paints, paints));
        Assert.Equal(PaintTargetValidation.NotPaintable, PaintInteractionResolver.ValidateApplyTarget(in rock, PaintKind.Texture, "wb:caution", bricks, paints, paints));
        Assert.Equal(PaintTargetValidation.NotPaintable, PaintInteractionResolver.ValidateApplyTarget(in rock, PaintKind.Color, "wb:red", bricks, paints, paints));
        Assert.Equal(PaintTargetValidation.MissingPaint, PaintInteractionResolver.ValidateApplyTarget(in panel, PaintKind.Texture, null, bricks, paints, paints));
        Assert.Equal(PaintTargetValidation.MissingPaint, PaintInteractionResolver.ValidateApplyTarget(in panel, PaintKind.Texture, "wb:red", bricks, paints, paints));
        Assert.Equal(PaintTargetValidation.AlphaMismatch, PaintInteractionResolver.ValidateApplyTarget(in glass, PaintKind.Texture, "wb:caution", bricks, paints, paints));
        Assert.Equal(PaintTargetValidation.AlphaMismatch, PaintInteractionResolver.ValidateApplyTarget(in panel, PaintKind.Texture, "wb:glass", bricks, paints, paints));
        Assert.Equal(PaintTargetValidation.AlphaMismatch, PaintInteractionResolver.ValidateApplyTarget(in grate, PaintKind.Texture, "wb:caution", bricks, paints, paints));

        Assert.Equal(PaintTargetValidation.Valid, PaintInteractionResolver.ValidateApplyTarget(in panel, PaintKind.Texture, "wb:caution", bricks, paints, paints));
        Assert.Equal(PaintTargetValidation.Valid, PaintInteractionResolver.ValidateApplyTarget(in glass, PaintKind.Texture, "wb:glass", bricks, paints, paints));
        Assert.Equal(PaintTargetValidation.Valid, PaintInteractionResolver.ValidateApplyTarget(in grate, PaintKind.Texture, "wb:grate", bricks, paints, paints));
        Assert.Equal(PaintTargetValidation.Valid, PaintInteractionResolver.ValidateApplyTarget(in glass, PaintKind.Color, "wb:red", bricks, paints, paints));
    }

    [Fact]
    public void HasRemovableOverrideChecksActiveModeSlotAcrossExpandedFaces()
    {
        var voxels = new VoxelObject(16);
        var coord = new Int3(2, 3, 4);
        voxels.Set(coord.X, coord.Y, coord.Z, new Voxel(1, new ShapeLight(BrickShape.Slope, 15), 0));

        //  Unpainted face has nothing to remove in either mode.
        BrickFaceMask slopeMask = PaintInteractionResolver.ExpandFaceMask(BrickFaceMask.Front, BrickShape.Slope);
        Assert.False(PaintInteractionResolver.HasRemovableOverride(voxels, in coord, slopeMask, PaintKind.Texture));
        Assert.False(PaintInteractionResolver.HasRemovableOverride(voxels, in coord, slopeMask, PaintKind.Color));

        //  Applying texture only on Top makes expanded Front|Top removable in Texture mode, but not Color mode.
        voxels.SetFaceTexture(coord.X, coord.Y, coord.Z, BrickFaceMask.Top, 1);
        Assert.True(PaintInteractionResolver.HasRemovableOverride(voxels, in coord, slopeMask, PaintKind.Texture));
        Assert.False(PaintInteractionResolver.HasRemovableOverride(voxels, in coord, slopeMask, PaintKind.Color));
        Assert.False(PaintInteractionResolver.HasRemovableOverride(voxels, in coord, BrickFaceMask.Back, PaintKind.Texture));

        //  Applying color on Back makes Back removable in Color mode only.
        voxels.SetFaceColor(coord.X, coord.Y, coord.Z, BrickFaceMask.Back, 4);
        Assert.True(PaintInteractionResolver.HasRemovableOverride(voxels, in coord, BrickFaceMask.Back, PaintKind.Color));
        Assert.False(PaintInteractionResolver.HasRemovableOverride(voxels, in coord, BrickFaceMask.Back, PaintKind.Texture));
    }

    [Fact]
    public void PickFacePaintRespectsToolModeForSprayerColorAndDrillTexture()
    {
        var bricks = new StubBrickDatabase();
        bricks.Add(1, new Brick(
            "wb:panel",
            BrickAlpha.Opaque,
            false,
            null,
            BrickShape.Any,
            new BrickTextures { Connected = true, Default = ["panel.0"], Top = ["caution"] },
            ["paintable"]
        ));
        bricks.Add(2, new Brick("wb:thruster", BrickAlpha.Opaque, false, "thruster.obj", BrickShape.Custom, default, ["paintable"]));

        var paints = new StubPaintDatabase();
        paints.Add(1, Paint.CreateTexture("wb:caution", "caution", BrickAlpha.Opaque, false, ["caution"]));
        paints.Add(2, Paint.CreateTexture("wb:panel", "panel", BrickAlpha.Opaque, true, ["panel.0"]));
        paints.Add(3, Paint.CreateTexture("wb:panel_plain", "panel_plain", BrickAlpha.Opaque, false, ["panel.0"]));
        paints.Add(4, Paint.CreateColor("wb:red", "red", new Vector3(1f, 0f, 0f)));

        var voxels = new VoxelObject(16);
        var panelCoord = new Int3(1, 2, 3);
        var panelVoxel = new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0);
        voxels.Set(panelCoord.X, panelCoord.Y, panelCoord.Z, panelVoxel);

        //  Unpainted face: sprayer (Color) has nothing to pick; drill (Texture) falls back to brick face texture.
        Assert.False(PaintInteractionResolver.PickFacePaint(voxels, in panelCoord, in panelVoxel, BrickFace.Front, PaintKind.Color, bricks, paints).Success);

        Result<Paint> frontFallback = PaintInteractionResolver.PickFacePaint(voxels, in panelCoord, in panelVoxel, BrickFace.Front, PaintKind.Texture, bricks, paints);
        Assert.True(frontFallback.Success);
        Assert.Equal("wb:panel", frontFallback.Value.ID);

        Result<Paint> topFallback = PaintInteractionResolver.PickFacePaint(voxels, in panelCoord, in panelVoxel, BrickFace.Top, PaintKind.Texture, bricks, paints);
        Assert.True(topFallback.Success);
        Assert.Equal("wb:caution", topFallback.Value.ID);

        //  Painted texture on Front is picked by drill (Texture), while sprayer (Color) still sees no color.
        voxels.SetFaceTexture(panelCoord.X, panelCoord.Y, panelCoord.Z, BrickFaceMask.Front, 1);
        Result<Paint> paintedTexture = PaintInteractionResolver.PickFacePaint(voxels, in panelCoord, in panelVoxel, BrickFace.Front, PaintKind.Texture, bricks, paints);
        Assert.True(paintedTexture.Success);
        Assert.Equal("wb:caution", paintedTexture.Value.ID);
        Assert.False(PaintInteractionResolver.PickFacePaint(voxels, in panelCoord, in panelVoxel, BrickFace.Front, PaintKind.Color, bricks, paints).Success);

        //  Painted color on Front is picked by sprayer (Color), while drill (Texture) still picks the texture.
        voxels.SetFaceColor(panelCoord.X, panelCoord.Y, panelCoord.Z, BrickFaceMask.Front, 4);
        Result<Paint> paintedColor = PaintInteractionResolver.PickFacePaint(voxels, in panelCoord, in panelVoxel, BrickFace.Front, PaintKind.Color, bricks, paints);
        Assert.True(paintedColor.Success);
        Assert.Equal("wb:red", paintedColor.Value.ID);
        Result<Paint> drillStillPicksTexture = PaintInteractionResolver.PickFacePaint(voxels, in panelCoord, in panelVoxel, BrickFace.Front, PaintKind.Texture, bricks, paints);
        Assert.True(drillStillPicksTexture.Success);
        Assert.Equal("wb:caution", drillStillPicksTexture.Value.ID);

        //  Unpainted custom brick with no face textures returns failure for drill too.
        var customCoord = new Int3(4, 5, 6);
        var customVoxel = new Voxel(2, new ShapeLight(BrickShape.Custom, 15), 0);
        voxels.Set(customCoord.X, customCoord.Y, customCoord.Z, customVoxel);
        Assert.False(PaintInteractionResolver.PickFacePaint(voxels, in customCoord, in customVoxel, BrickFace.Front, PaintKind.Texture, bricks, paints).Success);
    }

    [Fact]
    public void LightEntityDecoratorMixesPaintedFaceColorsForLightBricksOnly()
    {
        var bricks = new StubBrickDatabase();
        bricks.Add(1, new Brick("wb:light", BrickAlpha.Opaque, false, null, BrickShape.Any, default, ["entity", "light", "paintable"]));
        bricks.Add(2, new Brick("wb:small_light", BrickAlpha.Opaque, false, null, BrickShape.Any, default, ["entity", "light", "paintable"]));
        bricks.Add(3, new Brick("wb:thruster", BrickAlpha.Opaque, false, "thruster.obj", BrickShape.Custom, default, ["entity", "thruster", "paintable"]));

        var paints = new StubPaintDatabase();
        paints.Add(1, Paint.CreateTexture("wb:caution", "caution", BrickAlpha.Opaque, false, ["caution"]));
        paints.Add(2, Paint.CreateColor("wb:red", "red", new Vector3(1f, 0f, 0f)));
        paints.Add(3, Paint.CreateColor("wb:blue", "blue", new Vector3(0f, 0f, 1f)));

        var voxels = new VoxelObject(16);
        var registry = new VoxelChunkRegistry();
        var voxelComponent = new VoxelComponent(voxels, registry);
        var store = new DataStore();
        int parent = store.Alloc();
        int lightEntity = store.Alloc();
        int thrusterEntity = store.Alloc();

        var lightVoxel = new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0);
        voxels.Set(1, 2, 3, lightVoxel);
        var lightInfo = new VoxelInfo(1, 2, 3, lightVoxel);

        var decorator = new LightEntityDecorator(bricks, paints);

        //  No face overrides -> default light color.
        decorator.Process(store, parent, lightEntity, voxelComponent, lightInfo);
        Assert.True(store.TryGet(lightEntity, out LightComponent defaultLight));
        Assert.Equal(LightEntityDecorator.DefaultColor, defaultLight.Color);

        //  Texture-only override does not alter light color.
        voxels.SetFaceTexture(1, 2, 3, BrickFaceMask.Front, 1);
        decorator.Process(store, parent, lightEntity, voxelComponent, lightInfo);
        Assert.True(store.TryGet(lightEntity, out LightComponent textureOnlyLight));
        Assert.Equal(LightEntityDecorator.DefaultColor, textureOnlyLight.Color);

        //  Single painted face mixes default color with just that face's color.
        voxels.SetFaceColor(1, 2, 3, BrickFaceMask.Front, 2);
        decorator.Process(store, parent, lightEntity, voxelComponent, lightInfo);
        Assert.True(store.TryGet(lightEntity, out LightComponent redLight));
        Assert.Equal(LightEntityDecorator.DefaultColor * new Vector3(1f, 0f, 0f), redLight.Color);

        //  Multiple painted faces mix default color with the average of all painted faces.
        voxels.SetFaceColor(1, 2, 3, BrickFaceMask.Back, 3);
        decorator.Process(store, parent, lightEntity, voxelComponent, lightInfo);
        Assert.True(store.TryGet(lightEntity, out LightComponent purpleLight));
        Assert.Equal(LightEntityDecorator.DefaultColor * new Vector3(0.5f, 0f, 0.5f), purpleLight.Color);

        //  Clearing face colors restores the default light color.
        voxels.SetFaceColor(1, 2, 3, BrickFaceMask.All, 0);
        decorator.Process(store, parent, lightEntity, voxelComponent, lightInfo);
        Assert.True(store.TryGet(lightEntity, out LightComponent restoredLight));
        Assert.Equal(LightEntityDecorator.DefaultColor, restoredLight.Color);

        //  Small light also mixes its painted face color.
        var smallLightVoxel = new Voxel(2, new ShapeLight(BrickShape.Block, 15), 0);
        voxels.Set(2, 2, 3, smallLightVoxel);
        voxels.SetFaceColor(2, 2, 3, BrickFaceMask.Top, 3);
        decorator.Process(store, parent, lightEntity, voxelComponent, new VoxelInfo(2, 2, 3, smallLightVoxel));
        Assert.True(store.TryGet(lightEntity, out LightComponent smallBlueLight));
        Assert.Equal(LightEntityDecorator.DefaultColor * new Vector3(0f, 0f, 1f), smallBlueLight.Color);

        //  Thruster is not a light brick and keeps its thruster decorator color without face-color mixing.
        var thrusterVoxel = new Voxel(3, new ShapeLight(BrickShape.Custom, 15), 0);
        voxels.Set(3, 2, 3, thrusterVoxel);
        voxels.SetFaceColor(3, 2, 3, BrickFaceMask.All, 3);
        var thrusterInfo = new VoxelInfo(3, 2, 3, thrusterVoxel);
        decorator.Process(store, parent, thrusterEntity, voxelComponent, thrusterInfo);
        Assert.True(store.TryGet(thrusterEntity, out LightComponent thrusterBaseLight));
        Assert.Equal(LightEntityDecorator.DefaultColor, thrusterBaseLight.Color);

        //  MarkFaceModified flags the chunk for entity decorator reconciliation without geometry/collider rebuild.
        registry.MarkFaceModified(1, 2, 3, voxels.ChunkSize);
        Assert.Contains(new Short3(0, 0, 0), registry.EntityDirtyChunks);
        Assert.DoesNotContain(new Short3(0, 0, 0), registry.GeometryDirtyChunks);
    }

    [Fact]
    public void PaintHintResolverAuthorizesHeldToolAgainstRequestedPaintMode()
    {
        var (bricks, paints, voxels, world, uuid) = CreateHarness();
        voxels.Set(1, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0));

        var vfs = new Swordfish.Library.IO.VirtualFileSystem();
        vfs.Mount(new Swordfish.Library.IO.PathInfo(System.IO.Path.Combine("TestFiles", "Items", "assets")));
        Swordfish.Library.IO.IFileParser[] parsers = [new Swordfish.Library.Serialization.Toml.TomlParser<WaywardBeyond.Items.ItemDefinitions>()];
        var parser = new Swordfish.Library.IO.VirtualFileParseService(parsers, vfs);
        var items = new WaywardBeyond.Items.ItemDatabase(NullLogger<WaywardBeyond.Items.ItemDatabase>.Instance, parser, vfs);

        var resolver = new PaintHintResolver(bricks, items, paints, paints);
        var store = new DataStore();

        //  Sprayer is authorized for Color, rejected for Texture.
        var colorReq = MakeRequest(uuid, 1, 0, 0, BrickFaceMask.Front, PaintKind.Color, "wb:red", InteractionKind.PrimaryPressed);
        var colorCtx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 1, world, in colorReq, heldItemID: "wb:sprayer");
        Assert.Equal(InteractionDecision.Handled, resolver.Handle(ref colorCtx));

        var texReq = MakeRequest(uuid, 1, 0, 0, BrickFaceMask.Front, PaintKind.Texture, "wb:caution", InteractionKind.PrimaryPressed);
        var wrongToolCtx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 2, world, in texReq, heldItemID: "wb:sprayer");
        Assert.Equal(InteractionDecision.Cancel, resolver.Handle(ref wrongToolCtx));

        //  Drill is authorized for Texture, rejected for Color.
        var drillTexCtx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 3, world, in texReq, heldItemID: "wb:drill");
        Assert.Equal(InteractionDecision.Handled, resolver.Handle(ref drillTexCtx));

        var drillColorCtx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 4, world, in colorReq, heldItemID: "wb:drill");
        Assert.Equal(InteractionDecision.Cancel, resolver.Handle(ref drillColorCtx));

        //  Non-paint item or empty hand is rejected.
        var rockHandCtx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 5, world, in colorReq, heldItemID: "wb:panel");
        Assert.Equal(InteractionDecision.Cancel, resolver.Handle(ref rockHandCtx));

        var emptyHandCtx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 6, world, in colorReq, heldItemID: null);
        Assert.Equal(InteractionDecision.Cancel, resolver.Handle(ref emptyHandCtx));

        //  Paint hint without paint services cancels instead of falling through to brick hint resolution.
        var noPaintResolver = new PaintHintResolver(bricks, items, paintDatabase: null, paintRegistry: null);
        var noPaintCtx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 7, world, in colorReq, heldItemID: "wb:sprayer");
        Assert.Equal(InteractionDecision.Cancel, noPaintResolver.Handle(ref noPaintCtx));
    }

    [Fact]
    public void BrickCommitterIgnoresPaintActionsAndPendingQueueTracksSupersededEdits()
    {
        var (bricks, _, voxels, world, uuid) = CreateHarness();
        var store = new DataStore();
        int entity = store.Alloc(uuid);
        voxels.Set(1, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, 15), 0));
        store.AddOrUpdate(entity, new VoxelWorldComponent(voxels));

        var brickCommitter = new VoxelWriteCommitter(
            new ServerPeers(),
            BrickRegistry.FromNames(["wb:panel"]),
            bricks,
            NullLogger<VoxelWriteCommitter>.Instance
        );
        var paintReq = MakeRequest(uuid, 1, 0, 0, BrickFaceMask.Front, PaintKind.Color, "wb:red", InteractionKind.PrimaryPressed);
        var ctx = new InteractionContext(store, player: 0, Uuid.Null, sequence: 1, world, in paintReq, heldItemID: "wb:sprayer");
        ctx.Resolution = new InteractionResolution(entity, new Int3(1, 0, 0), new PaintResolution(BrickFaceMask.Front, PaintKind.Color, 4, "wb:red"));

        Assert.Equal(InteractionDecision.Continue, brickCommitter.Handle(ref ctx));
        Assert.Equal(1, voxels.Get(1, 0, 0).ID);

        var queue = new PendingInteractionQueue();
        var coord = new Int3(1, 0, 0);
        queue.RegisterFaceEdit(new PendingFaceEdit(entity, coord, BrickFaceMask.Front, PaintKind.Color, new FaceOverride[6], "wb:red", sequence: 1, serverTickAtSample: 10));
        queue.RegisterFaceEdit(new PendingFaceEdit(entity, coord, BrickFaceMask.Front, PaintKind.Color, new FaceOverride[6], "wb:blue", sequence: 2, serverTickAtSample: 11));
        queue.RegisterFaceEdit(new PendingFaceEdit(entity, coord, BrickFaceMask.Back, PaintKind.Color, new FaceOverride[6], "wb:green", sequence: 3, serverTickAtSample: 12));

        Assert.True(queue.HasNewerFaceEdit(entity, in coord, BrickFaceMask.Front, PaintKind.Color, sequence: 1));
        Assert.False(queue.HasNewerFaceEdit(entity, in coord, BrickFaceMask.Front, PaintKind.Color, sequence: 2));

        //  Confirming sequence 2 supersedes sequence 1 on Front, without touching sequence 3 on Back.
        queue.RemoveFaceEdit(2);
        queue.RemoveSupersededFaceEdits(entity, in coord, BrickFaceMask.Front, PaintKind.Color, confirmedSequence: 2);
        Assert.False(queue.TryFindFaceEditBySequence(1, out _));
        Assert.True(queue.TryFindFaceEditBySequence(3, out _));

        //  Brick replacement before sequence 3 keeps sequence 3; unconditional replacement clears it.
        queue.RemoveFaceEditsForCell(entity, in coord, beforeSequence: 3);
        Assert.True(queue.TryFindFaceEditBySequence(3, out _));
        queue.RemoveFaceEditsForCell(entity, in coord);
        Assert.Empty(queue.SnapshotFaceEdits());
    }

    private static InteractionRequest MakeRequest(
        Uuid entityUuid,
        int x, int y, int z,
        BrickFaceMask faceMask,
        PaintKind mode,
        string? paintId,
        InteractionKind kind
    ) {
        var paintHint = new PaintInteraction(entityUuid.ToValue(), x, y, z, (byte)faceMask, (byte)mode, paintId);
        return new InteractionRequest(Vector3.Zero, hint: null, kind, placeable: null, GameMode.Creative, reach: 10f, paintHint: paintHint);
    }
}
