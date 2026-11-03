namespace Swordfish.UI.Reef;

/// <summary>
///     Packs images into one RGBA texture and records where each one landed.
/// </summary>
/// <remarks>
///     Rows are packed tallest first into shelves, so the atlas wastes little space and
///     keeps a stable layout for a given set of sources.
/// </remarks>
internal sealed class ReefAtlas
{
    /// <summary>One image to pack.</summary>
    public readonly struct Source(string key, int width, int height, byte[] pixels)
    {
        /// <summary>The name the region is looked up by.</summary>
        public readonly string Key = key;

        /// <summary>The width of the image in pixels.</summary>
        public readonly int Width = width;

        /// <summary>The height of the image in pixels.</summary>
        public readonly int Height = height;

        /// <summary>The RGBA pixels, one row after another.</summary>
        public readonly byte[] Pixels = pixels;
    }

    /// <summary>The packed position and size of one source image.</summary>
    public readonly struct Region(int offsetX, int offsetY, int width, int height)
    {
        /// <summary>The pixel column of the image inside the atlas.</summary>
        public readonly int OffsetX = offsetX;

        /// <summary>The pixel row of the image inside the atlas.</summary>
        public readonly int OffsetY = offsetY;

        /// <summary>The width of the source image in pixels.</summary>
        public readonly int Width = width;

        /// <summary>The height of the source image in pixels.</summary>
        public readonly int Height = height;

        /// <summary>Maps a source pixel column to an atlas texture coordinate.</summary>
        public float MapU(float u, int atlasWidth) => (OffsetX + u) / atlasWidth;

        /// <summary>
        ///     Maps a source pixel row, counted from the top, to an atlas texture coordinate.
        ///     The vertical flip matches the bottom-up order of the uploaded rows.
        /// </summary>
        public float MapV(float v, int atlasHeight) => (OffsetY + Height - v) / atlasHeight;
    }

    private readonly Dictionary<string, Region> _regions;

    private ReefAtlas(int width, int height, byte[] pixels, Dictionary<string, Region> regions)
    {
        Width = width;
        Height = height;
        Pixels = pixels;
        _regions = regions;
    }

    /// <summary>The width of the atlas in pixels.</summary>
    public int Width { get; }

    /// <summary>The height of the atlas in pixels.</summary>
    public int Height { get; }

    /// <summary>The RGBA pixels, one row after another.</summary>
    public byte[] Pixels { get; }

    /// <summary>Returns the packed region of one source.</summary>
    public Region GetRegion(string key) => _regions[key];

    /// <summary>Finds the packed region of one source.</summary>
    public bool TryGetRegion(string key, out Region region) => _regions.TryGetValue(key, out region);

    /// <summary>Packs every source into one atlas.</summary>
    public static ReefAtlas Pack(IReadOnlyList<Source> sources)
    {
        if (sources.Count == 0)
        {
            throw new ArgumentException("At least one source is required.", nameof(sources));
        }

        long area = 0;
        var widest = 1;
        for (var i = 0; i < sources.Count; i++)
        {
            Source source = sources[i];
            if (source.Width <= 0 || source.Height <= 0 || source.Pixels.Length < source.Width * source.Height * 4)
            {
                throw new ArgumentException($"The source \"{source.Key}\" does not match its pixel data.", nameof(sources));
            }

            area += (long)source.Width * source.Height;
            widest = Math.Max(widest, source.Width);
        }

        int width = Math.Max(widest, (int)Math.Ceiling(Math.Sqrt(area)));

        int[] order = new int[sources.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        //  Tallest first keeps each shelf close to the height of its first item.
        Array.Sort(order, (left, right) => sources[right].Height.CompareTo(sources[left].Height));

        var regions = new Dictionary<string, Region>(sources.Count);
        var offsets = new int[sources.Count * 2];

        var x = 0;
        var y = 0;
        var shelfHeight = 0;
        for (var i = 0; i < order.Length; i++)
        {
            int index = order[i];
            Source source = sources[index];

            if (x > 0 && x + source.Width > width)
            {
                x = 0;
                y += shelfHeight;
                shelfHeight = 0;
            }

            offsets[index * 2] = x;
            offsets[(index * 2) + 1] = y;
            regions[source.Key] = new Region(x, y, source.Width, source.Height);

            x += source.Width;
            shelfHeight = Math.Max(shelfHeight, source.Height);
        }

        int height = Math.Max(1, y + shelfHeight);
        var pixels = new byte[width * height * 4];

        for (var i = 0; i < sources.Count; i++)
        {
            Source source = sources[i];
            int offsetX = offsets[i * 2];
            int offsetY = offsets[(i * 2) + 1];
            int sourceStride = source.Width * 4;
            int targetStride = width * 4;

            for (var row = 0; row < source.Height; row++)
            {
                Array.Copy(
                    source.Pixels,
                    row * sourceStride,
                    pixels,
                    ((offsetY + row) * targetStride) + (offsetX * 4),
                    sourceStride
                );
            }
        }

        return new ReefAtlas(width, height, pixels, regions);
    }
}
