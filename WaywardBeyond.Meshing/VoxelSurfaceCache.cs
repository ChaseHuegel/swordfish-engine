using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Meshing;

/// <summary>Caches resolved brick and paint texture-array indices, meshes, and culling flags for voxel meshing.</summary>
public sealed class VoxelSurfaceCache
{
    private const int FLAT_CACHE_LIMIT = 1024;

    private static readonly CachedBrick _emptyBrick = default;
    private static readonly CachedPaint _emptyPaint = default;

    private readonly IBrickDatabase _brickDatabase;
    private readonly ITextureLayerLookup _albedo;
    private readonly IAssetDatabase<VoxelMesh> _meshDatabase;
    private readonly IPaintDatabase? _paintDatabase;

    private CachedBrick[] _bricks = new CachedBrick[256];
    private Dictionary<ushort, CachedBrick>? _brickOverflow;
    private CachedPaint[] _paints = new CachedPaint[256];
    private Dictionary<ushort, CachedPaint>? _paintOverflow;

    /// <summary>Creates a surface cache backed by the brick, paint, mesh, and albedo texture-layer lookups.</summary>
    public VoxelSurfaceCache(
        IBrickDatabase brickDatabase,
        ITextureLayerLookup albedo,
        IAssetDatabase<VoxelMesh> meshDatabase,
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
        VoxelMesh? customMesh = null;
        if (brick.MeshID != null)
        {
            Result<VoxelMesh> meshResult = _meshDatabase.Get(brick.MeshID);
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
    public readonly struct CachedBrick
    {
        /// <summary>True once this cache slot has been populated.</summary>
        public readonly bool Initialized;

        /// <summary>True when the brick id resolved in the brick database.</summary>
        public readonly bool Exists;

        /// <summary>The resolved brick asset when <see cref="Exists"/> is true.</summary>
        public readonly Brick Brick;

        /// <summary>True when a block-shaped instance of this brick culls neighbor faces.</summary>
        public readonly bool IsCullerBlock;

        /// <summary>True when this brick enables connected-texture adjacency.</summary>
        public readonly bool Connected;

        /// <summary>True when any specific face declares an explicit texture list.</summary>
        public readonly bool HasPerFaceTextures;

        /// <summary>The brick's linear albedo tint color.</summary>
        public readonly Vector3 DefaultTint;

        /// <summary>True when <see cref="DefaultTint"/> differs from white.</summary>
        public readonly bool HasDefaultTint;

        /// <summary>The resolved custom geometry for custom-shaped bricks, if available.</summary>
        public readonly VoxelMesh? CustomMesh;

        /// <summary>Resolved fallback texture list used when a face declares no override.</summary>
        public readonly ResolvedTextureList DefaultTextures;

        /// <summary>Resolved texture list for the local top face.</summary>
        public readonly ResolvedTextureList TopTextures;

        /// <summary>Resolved texture list for the local bottom face.</summary>
        public readonly ResolvedTextureList BottomTextures;

        /// <summary>Resolved texture list for the local front face.</summary>
        public readonly ResolvedTextureList FrontTextures;

        /// <summary>Resolved texture list for the local back face.</summary>
        public readonly ResolvedTextureList BackTextures;

        /// <summary>Resolved texture list for the local left face.</summary>
        public readonly ResolvedTextureList LeftTextures;

        /// <summary>Resolved texture list for the local right face.</summary>
        public readonly ResolvedTextureList RightTextures;

        /// <summary>Constructs a cached brick entry and pre-resolves its texture-array layers.</summary>
        public CachedBrick(
            bool initialized,
            bool exists,
            Brick brick,
            bool isCullerBlock,
            VoxelMesh? customMesh,
            ITextureLayerLookup albedo
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
                    if (brick.TopTextures.IsDeclared)
                    {
                        return ref brick.TopTextures;
                    }

                    break;
                case BrickFace.Bottom:
                    if (brick.BottomTextures.IsDeclared)
                    {
                        return ref brick.BottomTextures;
                    }

                    break;
                case BrickFace.Front:
                    if (brick.FrontTextures.IsDeclared)
                    {
                        return ref brick.FrontTextures;
                    }

                    break;
                case BrickFace.Back:
                    if (brick.BackTextures.IsDeclared)
                    {
                        return ref brick.BackTextures;
                    }

                    break;
                case BrickFace.Left:
                    if (brick.LeftTextures.IsDeclared)
                    {
                        return ref brick.LeftTextures;
                    }

                    break;
                case BrickFace.Right:
                    if (brick.RightTextures.IsDeclared)
                    {
                        return ref brick.RightTextures;
                    }

                    break;
            }

            return ref brick.DefaultTextures;
        }
    }

    /// <summary>Cached resolved metadata for a single paint id.</summary>
    public readonly struct CachedPaint(
        bool initialized,
        bool exists,
        PaintKind kind,
        Vector3 linearColor,
        bool connected,
        ResolvedTextureList textures
    ) {
        /// <summary>True once this cache slot has been populated.</summary>
        public readonly bool Initialized = initialized;

        /// <summary>True when the paint id resolved in the paint database.</summary>
        public readonly bool Exists = exists;

        /// <summary>Whether this paint provides a texture or a linear tint color.</summary>
        public readonly PaintKind Kind = kind;

        /// <summary>Linear RGB tint for color paints.</summary>
        public readonly Vector3 LinearColor = linearColor;

        /// <summary>True when a texture paint participates in connected-texture adjacency.</summary>
        public readonly bool Connected = connected;

        /// <summary>Pre-resolved texture layer variants for texture paints.</summary>
        public readonly ResolvedTextureList Textures = textures;
    }

    /// <summary>Pre-resolved texture-array layer indices and variant names for one surface slot.</summary>
    public readonly struct ResolvedTextureList
    {
        /// <summary>Resolved layer index when <see cref="Length"/> is 1.</summary>
        public readonly int SingleIndex;

        /// <summary>Resolved layer name when <see cref="Length"/> is 1.</summary>
        public readonly string? SingleName;

        /// <summary>Resolved layer indices when <see cref="Length"/> is greater than 1.</summary>
        public readonly int[]? VariantIndices;

        /// <summary>Resolved layer names when <see cref="Length"/> is greater than 1.</summary>
        public readonly string?[]? VariantNames;

        /// <summary>Number of configured texture variants.</summary>
        public readonly int Length;

        /// <summary>True when the source texture list was explicitly declared.</summary>
        public readonly bool IsDeclared;

        /// <summary>True when exactly one texture variant is configured.</summary>
        public bool IsSingleVariant => Length == 1;

        /// <summary>Resolves all variant texture names against <paramref name="albedo"/> once.</summary>
        public ResolvedTextureList(ITextureLayerLookup albedo, string?[]? textures)
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
