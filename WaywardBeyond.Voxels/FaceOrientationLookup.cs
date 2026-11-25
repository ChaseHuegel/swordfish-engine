using System.Numerics;

namespace WaywardBeyond.Voxels;

/// <summary>Precomputed lookup tables mapping between brick-local and world-space <see cref="BrickFace"/> values across all 64 discrete orientations.</summary>
public static class FaceOrientationLookup
{
    private const int ORIENTATION_COUNT = 64;
    private const int FACE_COUNT = 6;

    private static readonly BrickFace[] _localToWorld = new BrickFace[ORIENTATION_COUNT * FACE_COUNT];
    private static readonly BrickFace[] _worldToLocal = new BrickFace[ORIENTATION_COUNT * FACE_COUNT];

    static FaceOrientationLookup()
    {
        for (byte o = 0; o < ORIENTATION_COUNT; o++)
        {
            var orientation = new Orientation(o);
            Quaternion q = orientation.ToQuaternion();
            Quaternion qInv = Quaternion.Inverse(q);
            int baseIndex = o * FACE_COUNT;

            for (var f = 0; f < FACE_COUNT; f++)
            {
                Vector3 localNormal = ((BrickFace)f).ToLocalNormal();
                _localToWorld[baseIndex + f] = BrickFaceExtensions.FromLocalNormal(Vector3.Transform(localNormal, q));
                _worldToLocal[baseIndex + f] = BrickFaceExtensions.FromLocalNormal(Vector3.Transform(localNormal, qInv));
            }
        }
    }

    /// <summary>Maps a brick-local <paramref name="localFace"/> to its world-space <see cref="BrickFace"/> under <paramref name="orientation"/>.</summary>
    public static BrickFace ToWorldFace(Orientation orientation, BrickFace localFace)
    {
        return _localToWorld[(orientation.ToByte() * FACE_COUNT) + (int)localFace];
    }

    /// <summary>Maps a world-space <paramref name="worldFace"/> to its brick-local <see cref="BrickFace"/> under <paramref name="orientation"/>.</summary>
    public static BrickFace ToLocalFace(Orientation orientation, BrickFace worldFace)
    {
        return _worldToLocal[(orientation.ToByte() * FACE_COUNT) + (int)worldFace];
    }
}
