using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.Models;

/// <summary>Session-scoped clipboard storing copied Position, Rotation, and Scale vectors for item model editing.</summary>
public sealed class StudioTransformClipboard : ObservableObject
{
    private Vector3? _position;
    private Vector3? _rotation;
    private Vector3? _scale;

    /// <summary>Shared clipboard instance preserving copied transform groups across item editor sessions.</summary>
    public static StudioTransformClipboard Shared { get; } = new();

    /// <summary>Most recently copied position vector, or <see langword="null"/> when empty.</summary>
    public Vector3? Position
    {
        get => _position;
        private set
        {
            if (SetProperty(ref _position, value))
            {
                OnPropertyChanged(nameof(HasPosition));
            }
        }
    }

    /// <summary>Most recently copied rotation Euler degrees vector, or <see langword="null"/> when empty.</summary>
    public Vector3? Rotation
    {
        get => _rotation;
        private set
        {
            if (SetProperty(ref _rotation, value))
            {
                OnPropertyChanged(nameof(HasRotation));
            }
        }
    }

    /// <summary>Most recently copied scale vector, or <see langword="null"/> when empty.</summary>
    public Vector3? Scale
    {
        get => _scale;
        private set
        {
            if (SetProperty(ref _scale, value))
            {
                OnPropertyChanged(nameof(HasScale));
            }
        }
    }

    /// <summary>True when a position vector has been copied.</summary>
    public bool HasPosition => _position.HasValue;

    /// <summary>True when a rotation vector has been copied.</summary>
    public bool HasRotation => _rotation.HasValue;

    /// <summary>True when a scale vector has been copied.</summary>
    public bool HasScale => _scale.HasValue;

    /// <summary>Stores a copy of <paramref name="position"/> in the transform clipboard.</summary>
    public void CopyPosition(Vector3 position) => Position = position;

    /// <summary>Stores a copy of <paramref name="rotation"/> in the transform clipboard.</summary>
    public void CopyRotation(Vector3 rotation) => Rotation = rotation;

    /// <summary>Stores a copy of <paramref name="scale"/> in the transform clipboard.</summary>
    public void CopyScale(Vector3 scale) => Scale = scale;
}
