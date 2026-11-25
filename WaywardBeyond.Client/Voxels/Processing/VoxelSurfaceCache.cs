using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

/// <summary>Caches resolved brick and paint texture array indices, meshes, and culling flags for chunk meshing.</summary>
internal sealed class VoxelSurfaceCache
{
    private const int FLAT_CACHE_LIMIT = 1024;

    private static readonly CachedBrick _emptyBrick = default;
    private static readonly CachedPaint _emptyPaint = default;

    private readonly IBrickDatabase _brickDatabase;
    private readonly TextureArray _albedo;
    private readonly IAssetDatabase<Mesh> _meshDatabase;
    private readonly IPaintDatabase? _paintDatabase;

    private CachedBrick[] _bricks = new CachedBrick[256];
    private Dictionary<ushort, CachedBrick>? _brickOverflow;
    private CachedPaint[] _paints = new CachedPaint[256];
    private Dictionary<ushort, CachedPaint>? _paintOverflow;

    /// <summary>Creates a surface cache backed by the brick, paint, mesh, and albedo texture databases.</summary>
    public VoxelSurfaceCache(
        IBrickDatabase brickDatabase,
        TextureArray albedo,
        IAssetDatabase<Mesh> meshDatabase,
        IPaintDatabase? paintDatabase
    ) {
        _brickDatabase = brickDatabase;
        _albedo = albedo;
        _meshDatabase = meshDatabase;
        _paintDatabase = paintDatabase;
    }

    /// <summary>Gets or populates the cached metadata for brick id <paramref name="id"/>.</summary>
    public ref readonly CachedBrick GetBrick(ushort id)
    {
        if (id == 0)
        {
            return ref _emptyBrick;
        }

        if (id < FLAT_CACHE_LIMIT)
        {
            if (id >= _bricks.Length)
            {
                Array.Resize(ref _bricks, Math.Min(FLAT_CACHE_LIMIT, Math.Max(_bricks.Length * 2, id + 1)));
            }

            ref CachedBrick slot = ref _bricks[id];
            if (!slot.Initialized)
            {
                slot = BuildBrick(id);
            }

            return ref slot;
        }

        _brickOverflow ??= [];
        ref CachedBrick overflowSlot = ref CollectionsMarshal.GetValueRefOrAddDefault(_brickOverflow, id, out bool exists);
        if (!exists)
        {
            overflowSlot = BuildBrick(id);
        }

        return ref overflowSlot;
    }

    /// <summary>Gets or populates the cached metadata for paint id <paramref name="id"/>.</summary>
    public ref readonly CachedPaint GetPaint(ushort id)
    {
        if (id == 0 || _paintDatabase == null)
        {
            return ref _emptyPaint;
        }

        if (id < FLAT_CACHE_LIMIT)
        {
            if (id >= _paints.Length)
            {
                Array.Resize(ref _paints, Math.Min(FLAT_CACHE_LIMIT, Math.Max(_paints.Length * 2, id + 1)));
            }

            ref CachedPaint slot = ref _paints[id];
            if (!slot.Initialized)
            {
                slot = BuildPaint(id);
            }

            return ref slot;
        }

        _paintOverflow ??= [];
        ref CachedPaint overflowSlot = ref CollectionsMarshal.GetValueRefOrAddDefault(_paintOverflow, id, out bool exists);
        if (!exists)
        {
            overflowSlot = BuildPaint(id);
        }

        return ref overflowSlot;
    }

    private CachedBrick BuildBrick(ushort id)
    {
        Result<Brick> result = _brickDatabase.Get(id);
        bool isCullerBlock = _brickDatabase.IsCuller(new Voxel(id, new ShapeLight(BrickShape.Block, 0), 0), BrickShape.Block);
        if (!result.Success)
        {
            return new CachedBrick(initialized: true, exists: false, brick: null!, isCullerBlock, customMesh: null, _albedo);
        }

        Brick brick = result.Value;
        Mesh? customMesh = null;
        if (brick.MeshID != null)
        {
            Result<Mesh> meshResult = _meshDatabase.Get(brick.MeshID);
            customMesh = meshResult.Success ? meshResult.Value : null;
        }

        return new CachedBrick(initialized: true, exists: true, brick, isCullerBlock, customMesh, _albedo);
    }

    private CachedPaint BuildPaint(ushort id)
    {
        Result<Paint> result = _paintDatabase!.Get(id);
        if (!result.Success)
        {
            return new CachedPaint(initialized: true, exists: false, kind: default, Vector3.One, connected: false, textures: default);
        }

        Paint paint = result.Value;
        var resolved = paint.Kind == PaintKind.Texture
            ? new ResolvedTextureList(_albedo, paint.Textures)
            : default;
        return new CachedPaint(initialized: true, exists: true, paint.Kind, paint.LinearColor, paint.Connected, resolved);
    }

    /// <summary>Cached resolved metadata for a single brick id.</summary>
    internal readonly struct CachedBrick
    {
        public readonly bool Initialized;
        public readonly bool Exists;
        public readonly Brick Brick;
        public readonly bool IsCullerBlock;
        public readonly bool Connected;
        public readonly bool HasPerFaceTextures;
        public readonly Vector3 DefaultTint;
        public readonly bool HasDefaultTint;
        public readonly Mesh? CustomMesh;
        public readonly ResolvedTextureList DefaultTextures;
        public readonly ResolvedTextureList TopTextures;
        public readonly ResolvedTextureList BottomTextures;
        public readonly ResolvedTextureList FrontTextures;
        public readonly ResolvedTextureList BackTextures;
        public readonly ResolvedTextureList LeftTextures;
        public readonly ResolvedTextureList RightTextures;

        /// <summary>Constructs a cached brick entry and pre-resolves its texture array layers.</summary>
        public CachedBrick(
            bool initialized,
            bool exists,
            Brick brick,
            bool isCullerBlock,
            Mesh? customMesh,
            TextureArray albedo
        ) {
            Initialized = initialized;
            Exists = exists;
            Brick = brick;
            IsCullerBlock = isCullerBlock;
            CustomMesh = customMesh;

            if (!exists)
            {
                Connected = false;
                HasPerFaceTextures = false;
                DefaultTint = Vector3.One;
                HasDefaultTint = false;
                DefaultTextures = default;
                TopTextures = default;
                BottomTextures = default;
                FrontTextures = default;
                BackTextures = default;
                LeftTextures = default;
                RightTextures = default;
                return;
            }

            BrickTextures textures = brick.Textures;
            Connected = textures.Connected;
            DefaultTint = brick.LinearColor;
            HasDefaultTint = DefaultTint != Vector3.One;
            DefaultTextures = new ResolvedTextureList(albedo, textures.Default);
            TopTextures = new ResolvedTextureList(albedo, textures.Top);
            BottomTextures = new ResolvedTextureList(albedo, textures.Bottom);
            FrontTextures = new ResolvedTextureList(albedo, textures.Front);
            BackTextures = new ResolvedTextureList(albedo, textures.Back);
            LeftTextures = new ResolvedTextureList(albedo, textures.Left);
            RightTextures = new ResolvedTextureList(albedo, textures.Right);
            HasPerFaceTextures = TopTextures.IsDeclared
                || BottomTextures.IsDeclared
                || FrontTextures.IsDeclared
                || BackTextures.IsDeclared
                || LeftTextures.IsDeclared
                || RightTextures.IsDeclared;
        }

        /// <summary>Returns true when <paramref name="brick"/> explicitly declares a texture list for <paramref name="face"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasDeclaredFaceTextures(in CachedBrick brick, BrickFace face)
        {
            return face switch
            {
                BrickFace.Top => brick.TopTextures.IsDeclared,
                BrickFace.Bottom => brick.BottomTextures.IsDeclared,
                BrickFace.Front => brick.FrontTextures.IsDeclared,
                BrickFace.Back => brick.BackTextures.IsDeclared,
                BrickFace.Left => brick.LeftTextures.IsDeclared,
                BrickFace.Right => brick.RightTextures.IsDeclared,
                _ => false,
            };
        }

        /// <summary>Returns the pre-resolved texture list for <paramref name="face"/> or falls back to default.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly ResolvedTextureList GetFaceTextures(in CachedBrick brick, BrickFace face)
        {
            switch (face)
            {
                case BrickFace.Top:
                    if (brick.TopTextures.IsDeclared) return ref brick.TopTextures;
                    break;
                case BrickFace.Bottom:
                    if (brick.BottomTextures.IsDeclared) return ref brick.BottomTextures;
                    break;
                case BrickFace.Front:
                    if (brick.FrontTextures.IsDeclared) return ref brick.FrontTextures;
                    break;
                case BrickFace.Back:
                    if (brick.BackTextures.IsDeclared) return ref brick.BackTextures;
                    break;
                case BrickFace.Left:
                    if (brick.LeftTextures.IsDeclared) return ref brick.LeftTextures;
                    break;
                case BrickFace.Right:
                    if (brick.RightTextures.IsDeclared) return ref brick.RightTextures;
                    break;
            }

            return ref brick.DefaultTextures;
        }
    }

    /// <summary>Cached resolved metadata for a single paint id.</summary>
    internal readonly struct CachedPaint(
        bool initialized,
        bool exists,
        PaintKind kind,
        Vector3 linearColor,
        bool connected,
        ResolvedTextureList textures
    ) {
        public readonly bool Initialized = initialized;
        public readonly bool Exists = exists;
        public readonly PaintKind Kind = kind;
        public readonly Vector3 LinearColor = linearColor;
        public readonly bool Connected = connected;
        public readonly ResolvedTextureList Textures = textures;
    }

    /// <summary>Pre-resolved texture array layer indices and variant names for one surface slot.</summary>
    internal readonly struct ResolvedTextureList
    {
        public readonly int SingleIndex;
        public readonly string? SingleName;
        public readonly int[]? VariantIndices;
        public readonly string?[]? VariantNames;
        public readonly int Length;
        public readonly bool IsDeclared;

        public bool IsSingleVariant => Length == 1;

        /// <summary>Resolves all variant texture names against <paramref name="albedo"/> once.</summary>
        public ResolvedTextureList(TextureArray albedo, string?[]? textures)
        {
            IsDeclared = textures != null;
            if (textures == null || textures.Length == 0)
            {
                SingleIndex = -1;
                SingleName = null;
                VariantIndices = null;
                VariantNames = null;
                Length = 0;
                return;
            }

            Length = textures.Length;
            if (textures.Length == 1)
            {
                string? name = textures[0];
                SingleName = name;
                SingleIndex = name != null ? albedo.IndexOf(name) : -1;
                VariantIndices = null;
                VariantNames = null;
                return;
            }

            VariantNames = textures;
            VariantIndices = new int[textures.Length];
            for (var i = 0; i < textures.Length; i++)
            {
                string? name = textures[i];
                VariantIndices[i] = name != null ? albedo.IndexOf(name) : -1;
            }

            SingleIndex = -1;
            SingleName = null;
        }

        /// <summary>Selects the deterministic variant index and name for <paramref name="origin"/>.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resolve(Vector3 origin, out int textureIndex, out string? textureName)
        {
            if (Length == 0)
            {
                textureIndex = -1;
                textureName = null;
                return;
            }

            if (Length == 1)
            {
                textureIndex = SingleIndex;
                textureName = SingleName;
                return;
            }

            int hash = Math.Abs((int)origin.X ^ (int)origin.Y ^ (int)origin.Z);
            int variantIndex = hash % Length;
            textureIndex = VariantIndices![variantIndex];
            textureName = VariantNames![variantIndex];
        }

        /// <summary>Resolves a texture layer index for <paramref name="origin"/>, falling back to layer 0.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int ResolveRequiredLayerIndex(Vector3 origin)
        {
            Resolve(origin, out int rawIndex, out _);
            return Math.Max(rawIndex, 0);
        }
    }
}
