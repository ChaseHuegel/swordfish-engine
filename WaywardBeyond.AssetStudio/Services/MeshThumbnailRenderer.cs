using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Pure CPU Wavefront OBJ thumbnail renderer producing anti-aliased isometric clay previews without OpenGL.
/// </summary>
public static class MeshThumbnailRenderer
{
    private const int MIN_DIMENSION = 16;
    private const int MAX_DIMENSION = 512;
    private const int SUPERSAMPLE_FACTOR = 2;
    private const float FIT_COVERAGE = 0.82f;
    private const float YAW_DEGREES = 36f;
    private const float PITCH_DEGREES = 24f;

    /// <summary>
    ///     Parses an <c>.obj</c> file and renders a studio-lit <paramref name="maxDimension"/> thumbnail bitmap, or returns null on failure.
    /// </summary>
    public static Bitmap? TryRenderThumbnail(string fullPath, int maxDimension = 64)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
        {
            return null;
        }

        int size = Math.Clamp(maxDimension, MIN_DIMENSION, MAX_DIMENSION);

        try
        {
            if (!TryParseObj(fullPath, out ObjMeshData mesh))
            {
                return null;
            }

            return RenderMeshBitmap(in mesh, size);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed rendering OBJ thumbnail '{fullPath}': {ex.Message}");
            return null;
        }
    }

    private static bool TryParseObj(string fullPath, out ObjMeshData mesh)
    {
        List<Vector3> positions = [];
        List<Vector3> normals = [];
        List<TriangleIndices> triangles = [];
        List<LineSegment> lineSegments = [];
        List<FaceVertex> faceScratch = [];
        List<int> lineScratch = [];

        using var reader = new StreamReader(fullPath);
        string? rawLine;
        while ((rawLine = reader.ReadLine()) != null)
        {
            int commentIdx = rawLine.IndexOf('#');
            string line = commentIdx >= 0 ? rawLine[..commentIdx] : rawLine;
            string trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            string[] parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            switch (parts[0])
            {
                case "v":
                    if (parts.Length >= 4
                        && TryParseFloat(parts[1], out float x)
                        && TryParseFloat(parts[2], out float y)
                        && TryParseFloat(parts[3], out float z))
                    {
                        positions.Add(new Vector3(x, y, z));
                    }

                    break;

                case "vn":
                    if (parts.Length >= 4
                        && TryParseFloat(parts[1], out float nx)
                        && TryParseFloat(parts[2], out float ny)
                        && TryParseFloat(parts[3], out float nz))
                    {
                        var n = new Vector3(nx, ny, nz);
                        float lenSq = n.LengthSquared();
                        normals.Add(lenSq > 1e-10f ? n / MathF.Sqrt(lenSq) : Vector3.UnitY);
                    }

                    break;

                case "f":
                    if (parts.Length < 4)
                    {
                        break;
                    }

                    faceScratch.Clear();
                    bool validFace = true;
                    for (var i = 1; i < parts.Length; i++)
                    {
                        if (!TryParseFaceVertex(parts[i], positions.Count, normals.Count, out FaceVertex fv))
                        {
                            validFace = false;
                            break;
                        }

                        faceScratch.Add(fv);
                    }

                    if (validFace && faceScratch.Count >= 3)
                    {
                        FaceVertex anchor = faceScratch[0];
                        for (var i = 1; i + 1 < faceScratch.Count; i++)
                        {
                            triangles.Add(new TriangleIndices(anchor, faceScratch[i], faceScratch[i + 1]));
                        }
                    }

                    break;

                case "l":
                    if (parts.Length < 3)
                    {
                        break;
                    }

                    lineScratch.Clear();
                    for (var i = 1; i < parts.Length; i++)
                    {
                        string token = parts[i];
                        int slashIdx = token.IndexOf('/');
                        string posToken = slashIdx >= 0 ? token[..slashIdx] : token;
                        if (TryResolveObjIndex(posToken, positions.Count, out int pIdx))
                        {
                            lineScratch.Add(pIdx);
                        }
                    }

                    for (var i = 0; i + 1 < lineScratch.Count; i++)
                    {
                        lineSegments.Add(new LineSegment(lineScratch[i], lineScratch[i + 1]));
                    }

                    break;
            }
        }

        if (positions.Count == 0 || (triangles.Count == 0 && lineSegments.Count == 0))
        {
            mesh = default;
            return false;
        }

        mesh = new ObjMeshData(positions, normals, triangles, lineSegments);
        return true;
    }

    private static Bitmap RenderMeshBitmap(in ObjMeshData mesh, int size)
    {
        ComputeReferencedBounds(in mesh, out Vector3 min3, out Vector3 max3);
        Vector3 center3 = (min3 + max3) * 0.5f;

        float yawRad = YAW_DEGREES * (MathF.PI / 180f);
        float pitchRad = PITCH_DEGREES * (MathF.PI / 180f);
        Matrix4x4 rotation = Matrix4x4.CreateRotationY(yawRad) * Matrix4x4.CreateRotationX(pitchRad);

        var rotatedPositions = new Vector3[mesh.Positions.Count];
        for (var i = 0; i < mesh.Positions.Count; i++)
        {
            rotatedPositions[i] = Vector3.Transform(mesh.Positions[i] - center3, rotation);
        }

        var rotatedNormals = new Vector3[mesh.Normals.Count];
        for (var i = 0; i < mesh.Normals.Count; i++)
        {
            Vector3 rn = Vector3.TransformNormal(mesh.Normals[i], rotation);
            float lenSq = rn.LengthSquared();
            rotatedNormals[i] = lenSq > 1e-10f ? rn / MathF.Sqrt(lenSq) : Vector3.UnitZ;
        }

        ComputeRotated2DBounds(in mesh, rotatedPositions, out float rotMinX, out float rotMaxX, out float rotMinY, out float rotMaxY);

        int ssSize = size * SUPERSAMPLE_FACTOR;
        float span = MathF.Max(rotMaxX - rotMinX, rotMaxY - rotMinY);
        if (!float.IsFinite(span) || span < 1e-6f)
        {
            span = 1f;
        }

        float scale = (ssSize * FIT_COVERAGE) / span;
        float centerX = (rotMinX + rotMaxX) * 0.5f;
        float centerY = (rotMinY + rotMaxY) * 0.5f;
        float halfCanvas = ssSize * 0.5f;

        var screenPositions = new Vector3[rotatedPositions.Length];
        for (var i = 0; i < rotatedPositions.Length; i++)
        {
            Vector3 rp = rotatedPositions[i];
            screenPositions[i] = new Vector3(
                halfCanvas + ((rp.X - centerX) * scale),
                halfCanvas - ((rp.Y - centerY) * scale),
                rp.Z);
        }

        var ssBgra = new byte[ssSize * ssSize * 4];
        var depth = new float[ssSize * ssSize];
        Array.Fill(depth, float.NegativeInfinity);

        if (mesh.Triangles.Count > 0)
        {
            Vector3 keyDir = Vector3.Normalize(new Vector3(-0.46f, 0.72f, 0.52f));
            Vector3 fillDir = Vector3.Normalize(new Vector3(0.68f, 0.22f, 0.42f));
            Vector3 halfVec = Vector3.Normalize(keyDir + Vector3.UnitZ);

            for (var t = 0; t < mesh.Triangles.Count; t++)
            {
                TriangleIndices tri = mesh.Triangles[t];
                RasterizeTriangle(
                    in tri,
                    rotatedPositions,
                    rotatedNormals,
                    screenPositions,
                    ssSize,
                    ssBgra,
                    depth,
                    keyDir,
                    fillDir,
                    halfVec);
            }
        }
        else
        {
            for (var i = 0; i < mesh.LineSegments.Count; i++)
            {
                LineSegment seg = mesh.LineSegments[i];
                RasterizeLine(screenPositions[seg.A], screenPositions[seg.B], ssSize, ssBgra, depth);
            }
        }

        var finalBgra = Downsample2x(ssBgra, size, ssSize);
        var handle = GCHandle.Alloc(finalBgra, GCHandleType.Pinned);
        try
        {
            return new WriteableBitmap(
                PixelFormats.Bgra8888,
                AlphaFormat.Premul,
                handle.AddrOfPinnedObject(),
                new Avalonia.PixelSize(size, size),
                new Avalonia.Vector(96, 96),
                size * 4);
        }
        finally
        {
            handle.Free();
        }
    }

    private static void RasterizeTriangle(
        in TriangleIndices tri,
        Vector3[] rotatedPositions,
        Vector3[] rotatedNormals,
        Vector3[] screenPositions,
        int ssSize,
        byte[] ssBgra,
        float[] depth,
        Vector3 keyDir,
        Vector3 fillDir,
        Vector3 halfVec)
    {
        Vector3 p0 = screenPositions[tri.V0.PosIdx];
        Vector3 p1 = screenPositions[tri.V1.PosIdx];
        Vector3 p2 = screenPositions[tri.V2.PosIdx];

        float area = EdgeFunction(p0, p1, p2);
        if (!float.IsFinite(area) || MathF.Abs(area) < 1e-4f)
        {
            return;
        }

        int minX = Math.Max(0, (int)MathF.Floor(MathF.Min(p0.X, MathF.Min(p1.X, p2.X))));
        int maxX = Math.Min(ssSize - 1, (int)MathF.Ceiling(MathF.Max(p0.X, MathF.Max(p1.X, p2.X))));
        int minY = Math.Max(0, (int)MathF.Floor(MathF.Min(p0.Y, MathF.Min(p1.Y, p2.Y))));
        int maxY = Math.Min(ssSize - 1, (int)MathF.Ceiling(MathF.Max(p0.Y, MathF.Max(p1.Y, p2.Y))));
        if (minX > maxX || minY > maxY)
        {
            return;
        }

        Vector3 r0 = rotatedPositions[tri.V0.PosIdx];
        Vector3 r1 = rotatedPositions[tri.V1.PosIdx];
        Vector3 r2 = rotatedPositions[tri.V2.PosIdx];
        Vector3 geoNormal = Vector3.Cross(r1 - r0, r2 - r0);
        float geoLenSq = geoNormal.LengthSquared();
        geoNormal = geoLenSq > 1e-10f ? geoNormal / MathF.Sqrt(geoLenSq) : Vector3.UnitZ;
        if (geoNormal.Z < 0f)
        {
            geoNormal = -geoNormal;
        }

        Vector3 n0 = ResolveCornerNormal(tri.V0.NormIdx, rotatedNormals, geoNormal);
        Vector3 n1 = ResolveCornerNormal(tri.V1.NormIdx, rotatedNormals, geoNormal);
        Vector3 n2 = ResolveCornerNormal(tri.V2.NormIdx, rotatedNormals, geoNormal);
        bool uniformNormal = tri.V0.NormIdx < 0 && tri.V1.NormIdx < 0 && tri.V2.NormIdx < 0;

        float invArea = 1f / area;
        uint flatShaded = uniformNormal ? ShadeStudioClay(geoNormal, keyDir, fillDir, halfVec) : 0u;

        for (int py = minY; py <= maxY; py++)
        {
            float sampleY = py + 0.5f;
            int rowBase = py * ssSize;
            for (int px = minX; px <= maxX; px++)
            {
                float sampleX = px + 0.5f;
                float w0 = EdgeFunction(p1, p2, sampleX, sampleY) * invArea;
                float w1 = EdgeFunction(p2, p0, sampleX, sampleY) * invArea;
                float w2 = EdgeFunction(p0, p1, sampleX, sampleY) * invArea;

                if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f)
                {
                    continue;
                }

                float z = (w0 * p0.Z) + (w1 * p1.Z) + (w2 * p2.Z);
                int pixelIdx = rowBase + px;
                if (z <= depth[pixelIdx])
                {
                    continue;
                }

                depth[pixelIdx] = z;

                uint bgra;
                if (uniformNormal)
                {
                    bgra = flatShaded;
                }
                else
                {
                    Vector3 n = (n0 * w0) + (n1 * w1) + (n2 * w2);
                    float nLenSq = n.LengthSquared();
                    n = nLenSq > 1e-10f ? n / MathF.Sqrt(nLenSq) : geoNormal;
                    bgra = ShadeStudioClay(n, keyDir, fillDir, halfVec);
                }

                int o = pixelIdx * 4;
                ssBgra[o] = (byte)(bgra & 0xFF);
                ssBgra[o + 1] = (byte)((bgra >> 8) & 0xFF);
                ssBgra[o + 2] = (byte)((bgra >> 16) & 0xFF);
                ssBgra[o + 3] = 255;
            }
        }
    }

    private static void RasterizeLine(
        Vector3 a,
        Vector3 b,
        int ssSize,
        byte[] ssBgra,
        float[] depth)
    {
        float dx = b.X - a.X;
        float dy = b.Y - a.Y;
        float dist = MathF.Sqrt((dx * dx) + (dy * dy));
        int steps = Math.Max(1, (int)MathF.Ceiling(dist * 1.5f));

        for (var s = 0; s <= steps; s++)
        {
            float t = (float)s / steps;
            float x = a.X + (dx * t);
            float y = a.Y + (dy * t);
            float z = a.Z + ((b.Z - a.Z) * t);

            int cx = (int)MathF.Round(x);
            int cy = (int)MathF.Round(y);
            for (int oy = -1; oy <= 1; oy++)
            {
                int py = cy + oy;
                if (py < 0 || py >= ssSize)
                {
                    continue;
                }

                for (int ox = -1; ox <= 1; ox++)
                {
                    int px = cx + ox;
                    if (px < 0 || px >= ssSize)
                    {
                        continue;
                    }

                    int idx = (py * ssSize) + px;
                    if (z <= depth[idx])
                    {
                        continue;
                    }

                    depth[idx] = z;
                    int o = idx * 4;
                    ssBgra[o] = 232;
                    ssBgra[o + 1] = 202;
                    ssBgra[o + 2] = 178;
                    ssBgra[o + 3] = 255;
                }
            }
        }
    }

    private static byte[] Downsample2x(byte[] ssBgra, int size, int ssSize)
    {
        var finalBgra = new byte[size * size * 4];
        int ssRowBytes = ssSize * 4;

        for (var y = 0; y < size; y++)
        {
            int srcRow0 = (y * 2) * ssRowBytes;
            int srcRow1 = srcRow0 + ssRowBytes;
            int dstRow = y * size * 4;

            for (var x = 0; x < size; x++)
            {
                int sx0 = (x * 2) * 4;
                int sx1 = sx0 + 4;

                int i00 = srcRow0 + sx0;
                int i10 = srcRow0 + sx1;
                int i01 = srcRow1 + sx0;
                int i11 = srcRow1 + sx1;

                int dst = dstRow + (x * 4);
                finalBgra[dst] = (byte)((ssBgra[i00] + ssBgra[i10] + ssBgra[i01] + ssBgra[i11]) >> 2);
                finalBgra[dst + 1] = (byte)((ssBgra[i00 + 1] + ssBgra[i10 + 1] + ssBgra[i01 + 1] + ssBgra[i11 + 1]) >> 2);
                finalBgra[dst + 2] = (byte)((ssBgra[i00 + 2] + ssBgra[i10 + 2] + ssBgra[i01 + 2] + ssBgra[i11 + 2]) >> 2);
                finalBgra[dst + 3] = (byte)((ssBgra[i00 + 3] + ssBgra[i10 + 3] + ssBgra[i01 + 3] + ssBgra[i11 + 3]) >> 2);
            }
        }

        return finalBgra;
    }

    private static uint ShadeStudioClay(Vector3 n, Vector3 keyDir, Vector3 fillDir, Vector3 halfVec)
    {
        float keyDiffuse = MathF.Max(0f, Vector3.Dot(n, keyDir));
        float fillDiffuse = MathF.Max(0f, Vector3.Dot(n, fillDir));
        float spec = MathF.Pow(MathF.Max(0f, Vector3.Dot(n, halfVec)), 20f) * 0.20f;
        float rim = MathF.Pow(1f - MathF.Min(1f, MathF.Abs(n.Z)), 2.2f) * 0.16f;

        float baseScale = 0.28f + (0.62f * keyDiffuse);
        float fillScale = 0.22f * fillDiffuse;

        float r = Math.Clamp((0.76f * baseScale) + (0.64f * fillScale) + spec + (rim * 0.85f), 0f, 1f);
        float g = Math.Clamp((0.81f * baseScale) + (0.76f * fillScale) + spec + (rim * 0.94f), 0f, 1f);
        float b = Math.Clamp((0.89f * baseScale) + (0.96f * fillScale) + spec + rim, 0f, 1f);

        uint rb = (uint)(byte)MathF.Round(r * 255f);
        uint gb = (uint)(byte)MathF.Round(g * 255f);
        uint bb = (uint)(byte)MathF.Round(b * 255f);
        return bb | (gb << 8) | (rb << 16);
    }

    private static Vector3 ResolveCornerNormal(int normIdx, Vector3[] rotatedNormals, Vector3 geoNormal)
    {
        if (normIdx < 0 || normIdx >= rotatedNormals.Length)
        {
            return geoNormal;
        }

        Vector3 n = rotatedNormals[normIdx];
        return Vector3.Dot(n, geoNormal) < 0f ? -n : n;
    }

    private static float EdgeFunction(Vector3 a, Vector3 b, Vector3 c) =>
        ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    private static float EdgeFunction(Vector3 a, Vector3 b, float cx, float cy) =>
        ((b.X - a.X) * (cy - a.Y)) - ((b.Y - a.Y) * (cx - a.X));

    private static void ComputeReferencedBounds(in ObjMeshData mesh, out Vector3 min, out Vector3 max)
    {
        min = new Vector3(float.PositiveInfinity);
        max = new Vector3(float.NegativeInfinity);
        var any = false;

        for (var i = 0; i < mesh.Triangles.Count; i++)
        {
            TriangleIndices tri = mesh.Triangles[i];
            ExpandBounds(mesh.Positions[tri.V0.PosIdx], ref min, ref max, ref any);
            ExpandBounds(mesh.Positions[tri.V1.PosIdx], ref min, ref max, ref any);
            ExpandBounds(mesh.Positions[tri.V2.PosIdx], ref min, ref max, ref any);
        }

        for (var i = 0; i < mesh.LineSegments.Count; i++)
        {
            LineSegment seg = mesh.LineSegments[i];
            ExpandBounds(mesh.Positions[seg.A], ref min, ref max, ref any);
            ExpandBounds(mesh.Positions[seg.B], ref min, ref max, ref any);
        }

        if (!any)
        {
            for (var i = 0; i < mesh.Positions.Count; i++)
            {
                ExpandBounds(mesh.Positions[i], ref min, ref max, ref any);
            }
        }

        if (!any)
        {
            min = new Vector3(-0.5f);
            max = new Vector3(0.5f);
        }
    }

    private static void ComputeRotated2DBounds(
        in ObjMeshData mesh,
        Vector3[] rotatedPositions,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        minX = float.PositiveInfinity;
        maxX = float.NegativeInfinity;
        minY = float.PositiveInfinity;
        maxY = float.NegativeInfinity;
        var any = false;

        for (var i = 0; i < mesh.Triangles.Count; i++)
        {
            TriangleIndices tri = mesh.Triangles[i];
            Expand2D(rotatedPositions[tri.V0.PosIdx], ref minX, ref maxX, ref minY, ref maxY, ref any);
            Expand2D(rotatedPositions[tri.V1.PosIdx], ref minX, ref maxX, ref minY, ref maxY, ref any);
            Expand2D(rotatedPositions[tri.V2.PosIdx], ref minX, ref maxX, ref minY, ref maxY, ref any);
        }

        for (var i = 0; i < mesh.LineSegments.Count; i++)
        {
            LineSegment seg = mesh.LineSegments[i];
            Expand2D(rotatedPositions[seg.A], ref minX, ref maxX, ref minY, ref maxY, ref any);
            Expand2D(rotatedPositions[seg.B], ref minX, ref maxX, ref minY, ref maxY, ref any);
        }

        if (!any)
        {
            minX = -0.5f;
            maxX = 0.5f;
            minY = -0.5f;
            maxY = 0.5f;
        }
    }

    private static void ExpandBounds(Vector3 p, ref Vector3 min, ref Vector3 max, ref bool any)
    {
        min = Vector3.Min(min, p);
        max = Vector3.Max(max, p);
        any = true;
    }

    private static void Expand2D(
        Vector3 p,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY,
        ref bool any)
    {
        if (p.X < minX)
        {
            minX = p.X;
        }

        if (p.X > maxX)
        {
            maxX = p.X;
        }

        if (p.Y < minY)
        {
            minY = p.Y;
        }

        if (p.Y > maxY)
        {
            maxY = p.Y;
        }

        any = true;
    }

    private static bool TryParseFloat(string token, out float value) =>
        float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && float.IsFinite(value);

    private static bool TryParseFaceVertex(
        string token,
        int positionCount,
        int normalCount,
        out FaceVertex vertex)
    {
        string[] slashes = token.Split('/', StringSplitOptions.None);
        if (slashes.Length == 0 || !TryResolveObjIndex(slashes[0], positionCount, out int posIdx))
        {
            vertex = default;
            return false;
        }

        int normIdx = -1;
        if (slashes.Length >= 3 && !string.IsNullOrEmpty(slashes[2]))
        {
            if (!TryResolveObjIndex(slashes[2], normalCount, out normIdx))
            {
                normIdx = -1;
            }
        }

        vertex = new FaceVertex(posIdx, normIdx);
        return true;
    }

    private static bool TryResolveObjIndex(string token, int count, out int index)
    {
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw) || raw == 0)
        {
            index = -1;
            return false;
        }

        int resolved = raw > 0 ? raw - 1 : count + raw;
        if (resolved < 0 || resolved >= count)
        {
            index = -1;
            return false;
        }

        index = resolved;
        return true;
    }

    private readonly record struct FaceVertex(int PosIdx, int NormIdx);

    private readonly record struct TriangleIndices(FaceVertex V0, FaceVertex V1, FaceVertex V2);

    private readonly record struct LineSegment(int A, int B);

    private readonly record struct ObjMeshData(
        List<Vector3> Positions,
        List<Vector3> Normals,
        List<TriangleIndices> Triangles,
        List<LineSegment> LineSegments);
}
