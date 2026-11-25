using System.Globalization;
using System.Numerics;

namespace Swordfish.Graphics.Particles;

/// <summary>Immutable runtime definition for a particle effect loaded from an effect TOML file.</summary>
public sealed class ParticleEffect
{
    /// <summary>Asset ID relative to the particles root without extension.</summary>
    public readonly string ID;

    /// <summary>Optional sprite texture; null renders crisp solid square quads.</summary>
    public readonly Texture? Texture;

    /// <summary>Blend mode used when drawing particles of this effect.</summary>
    public readonly ParticleBlendMode BlendMode;

    /// <summary>Continuous emission rate in particles per second.</summary>
    public readonly float Rate;

    /// <summary>Default particle count emitted by a one-shot burst.</summary>
    public readonly int BurstCount;

    /// <summary>Maximum live particles allowed for this effect per emitter or burst stream.</summary>
    public readonly int MaxParticles;

    /// <summary>Particle lifetime range in seconds.</summary>
    public readonly ParticleRange Lifetime;

    /// <summary>Initial particle speed range in world units per second.</summary>
    public readonly ParticleRange Speed;

    /// <summary>Cone half-angle in degrees around the emission direction.</summary>
    public readonly float SpreadDegrees;

    /// <summary>Radius in world units of the spherical spawn jitter around the origin.</summary>
    public readonly float SpawnRadius;

    /// <summary>Initial quad size range in world units.</summary>
    public readonly ParticleRange StartSize;

    /// <summary>Final quad size range in world units.</summary>
    public readonly ParticleRange EndSize;

    /// <summary>Primary initial color in linear RGB (XYZ) and linear alpha (W).</summary>
    public readonly Vector4 StartColor;

    /// <summary>Optional secondary initial color in linear RGB (XYZ) and linear alpha (W) for per-particle color variation.</summary>
    public readonly Vector4? SecondaryStartColor;

    /// <summary>Final color at end of lifetime in linear RGB (XYZ) and linear alpha (W).</summary>
    public readonly Vector4 EndColor;

    /// <summary>HDR emissive multiplier applied to RGB and written to the bloom attachment.</summary>
    public readonly float EmissiveIntensity;

    /// <summary>World-space acceleration applied every second.</summary>
    public readonly Vector3 Gravity;

    /// <summary>Exponential velocity damping coefficient per second.</summary>
    public readonly float Drag;

    /// <summary>Initial billboard rotation range in degrees.</summary>
    public readonly ParticleRange InitialRotationDegrees;

    /// <summary>Billboard angular velocity range in degrees per second.</summary>
    public readonly ParticleRange AngularVelocityDegrees;

    /// <summary>Constructs a validated runtime particle effect.</summary>
    public ParticleEffect(
        string id,
        Texture? texture,
        ParticleBlendMode blendMode,
        float rate,
        int burstCount,
        int maxParticles,
        in ParticleRange lifetime,
        in ParticleRange speed,
        float spreadDegrees,
        float spawnRadius,
        in ParticleRange startSize,
        in ParticleRange endSize,
        in Vector4 startColor,
        in Vector4? secondaryStartColor,
        in Vector4 endColor,
        float emissiveIntensity,
        in Vector3 gravity,
        float drag,
        in ParticleRange initialRotationDegrees,
        in ParticleRange angularVelocityDegrees
    ) {
        ID = id ?? throw new ArgumentNullException(nameof(id));
        Texture = texture;
        BlendMode = blendMode;
        Rate = rate;
        BurstCount = burstCount;
        MaxParticles = maxParticles;
        Lifetime = lifetime;
        Speed = speed;
        SpreadDegrees = spreadDegrees;
        SpawnRadius = spawnRadius;
        StartSize = startSize;
        EndSize = endSize;
        StartColor = startColor;
        SecondaryStartColor = secondaryStartColor;
        EndColor = endColor;
        EmissiveIntensity = emissiveIntensity;
        Gravity = gravity;
        Drag = drag;
        InitialRotationDegrees = initialRotationDegrees;
        AngularVelocityDegrees = angularVelocityDegrees;
    }

    /// <summary>
    /// Parses a 6-digit (<c>#RRGGBB</c>) or 8-digit (<c>#RRGGBBAA</c>) hex color string into linear RGB and alpha.
    /// A 6-digit string uses <paramref name="defaultAlpha"/>; an 8-digit string uses the parsed alpha.
    /// </summary>
    public static bool TryParseHexColor(string? value, float defaultAlpha, out Vector4 linearColor)
    {
        linearColor = new Vector4(1f, 1f, 1f, Math.Clamp(defaultAlpha, 0f, 1f));
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReadOnlySpan<char> span = value.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length != 6 && span.Length != 8)
        {
            return false;
        }

        if (!byte.TryParse(span[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
            !byte.TryParse(span[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
            !byte.TryParse(span[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return false;
        }

        float alpha = Math.Clamp(defaultAlpha, 0f, 1f);
        if (span.Length == 8)
        {
            if (!byte.TryParse(span[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte a))
            {
                return false;
            }

            alpha = a / 255f;
        }

        linearColor = new Vector4(
            Tonemapper.SrgbToLinear(r / 255f),
            Tonemapper.SrgbToLinear(g / 255f),
            Tonemapper.SrgbToLinear(b / 255f),
            alpha
        );
        return true;
    }

    /// <summary>Packs normalized sRGB color and linear alpha into a <c>0xRRGGBBAA</c> tint value.</summary>
    public static uint PackTint(in Vector3 srgbColor, float alpha = 1f)
    {
        uint r = PackChannel(srgbColor.X);
        uint g = PackChannel(srgbColor.Y);
        uint b = PackChannel(srgbColor.Z);
        uint a = PackChannel(alpha);
        return (r << 24) | (g << 16) | (b << 8) | a;
    }

    /// <summary>Unpacks a <c>0xRRGGBBAA</c> tint into linear RGB and linear alpha, returning <see cref="Vector4.One"/> when <c>0</c>.</summary>
    public static Vector4 UnpackTintLinear(uint tintRgba)
    {
        if (tintRgba == 0u)
        {
            return Vector4.One;
        }

        float r = ((tintRgba >> 24) & 0xFF) / 255f;
        float g = ((tintRgba >> 16) & 0xFF) / 255f;
        float b = ((tintRgba >> 8) & 0xFF) / 255f;
        float a = (tintRgba & 0xFF) / 255f;
        return new Vector4(
            Tonemapper.SrgbToLinear(r),
            Tonemapper.SrgbToLinear(g),
            Tonemapper.SrgbToLinear(b),
            a
        );
    }

    private static uint PackChannel(float value)
    {
        if (!float.IsFinite(value))
        {
            return 0u;
        }

        float clamped = Math.Clamp(value, 0f, 1f);
        return (uint)Math.Clamp((int)MathF.Round(clamped * 255f), 0, 255);
    }
}
