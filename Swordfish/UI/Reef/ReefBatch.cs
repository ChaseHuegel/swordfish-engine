using System.Numerics;
using Reef;

namespace Swordfish.UI.Reef;

/// <summary>
///     Builds the paint-ordered quad instances and their draw runs for one Reef frame.
/// </summary>
/// <remarks>
///     Each quad is one instance of a four-vertex fan, so the color and clip rect are
///     stored once instead of once per corner. A run is a contiguous range of instances
///     that share one draw state. Paint order is preserved because an instance only
///     merges into the previous run when the state is unchanged.
/// </remarks>
internal sealed class ReefBatch
{
    /// <summary>The number of floats per quad instance.</summary>
    public const int INSTANCE_STRIDE = 16;

    /// <summary>The float offset of the clip-space bounds.</summary>
    public const int RECT_OFFSET = 0;

    /// <summary>The float offset of the texture coordinates.</summary>
    public const int UV_OFFSET = 4;

    /// <summary>The float offset of the color.</summary>
    public const int COLOR_OFFSET = 8;

    /// <summary>The float offset of the clip rect.</summary>
    public const int CLIP_OFFSET = 12;

    /// <summary>A contiguous range of quads that share one draw state.</summary>
    public readonly struct Run(int baseInstance, int instanceCount, int state)
    {
        /// <summary>The index of the first quad in the run.</summary>
        public readonly int BaseInstance = baseInstance;

        /// <summary>The number of quads in the run.</summary>
        public readonly int InstanceCount = instanceCount;

        /// <summary>The caller-defined draw state that the quads share.</summary>
        public readonly int State = state;
    }

    private float[] _instances = [];
    private int _instanceCount;

    private Run[] _runs = [];
    private int _runCount;

    private int _state = -1;
    private int _pendingState = -1;
    private int _runInstanceOffset;

    private float _scaleX = 1f;
    private float _scaleY = 1f;

    /// <summary>The instance data written this frame.</summary>
    public ReadOnlySpan<float> Instances => _instances.AsSpan(0, _instanceCount * INSTANCE_STRIDE);

    /// <summary>The number of quads written this frame.</summary>
    public int InstanceCount => _instanceCount;

    /// <summary>The number of draw runs this frame.</summary>
    public int RunCount => _runCount;

    /// <summary>Returns one draw run.</summary>
    public Run GetRun(int index) => _runs[index];

    /// <summary>Clears the batch and sets the screen size for the frame.</summary>
    public void Begin(int width, int height)
    {
        _instanceCount = 0;
        _runCount = 0;
        _state = -1;
        _pendingState = -1;
        _runInstanceOffset = 0;
        _scaleX = width > 0 ? 2f / width : 1f;
        _scaleY = height > 0 ? 2f / height : 1f;
    }

    /// <summary>
    ///     Selects the draw state for the quads that follow. The change is applied by the
    ///     next quad, so a state that draws nothing never splits a run.
    /// </summary>
    public void UseState(int state) => _pendingState = state;

    /// <summary>Closes the trailing run. Call once after the last quad.</summary>
    public void End()
    {
        CloseRun();
        _state = -1;
        _pendingState = -1;
    }

    /// <summary>Appends one quad in paint order. Empty and transparent quads are skipped.</summary>
    public void AddQuad(IntRect rect, float u0, float v0, float u1, float v1, Vector4 color, IntRect clipRect)
    {
        if (_pendingState < 0 || rect.Size.X <= 0 || rect.Size.Y <= 0 || color.W <= 0f)
        {
            return;
        }

        if (_pendingState != _state)
        {
            CloseRun();
            _state = _pendingState;
            _runInstanceOffset = _instanceCount;
        }

        int required = (_instanceCount + 1) * INSTANCE_STRIDE;
        if (_instances.Length < required)
        {
            Array.Resize(ref _instances, Math.Max(required, Math.Max(256, _instances.Length * 2)));
        }

        float[] data = _instances;
        int index = _instanceCount * INSTANCE_STRIDE;

        //  Clip-space bounds. Corners are read as bottom left, bottom right, top right,
        //  and top left. Y is flipped so the origin is the top-left corner.
        int rectOffset = index + RECT_OFFSET;
        data[rectOffset] = (rect.Left * _scaleX) - 1f;
        data[rectOffset + 1] = 1f - (rect.Bottom * _scaleY);
        data[rectOffset + 2] = (rect.Right * _scaleX) - 1f;
        data[rectOffset + 3] = 1f - (rect.Top * _scaleY);

        //  Texture coordinates of the same corners.
        int uvOffset = index + UV_OFFSET;
        data[uvOffset] = u0;
        data[uvOffset + 1] = v0;
        data[uvOffset + 2] = u1;
        data[uvOffset + 3] = v1;

        int colorOffset = index + COLOR_OFFSET;
        data[colorOffset] = color.X;
        data[colorOffset + 1] = color.Y;
        data[colorOffset + 2] = color.Z;
        data[colorOffset + 3] = color.W;

        int clipOffset = index + CLIP_OFFSET;
        data[clipOffset] = clipRect.Left;
        data[clipOffset + 1] = clipRect.Top;
        data[clipOffset + 2] = clipRect.Right;
        data[clipOffset + 3] = clipRect.Bottom;

        _instanceCount++;
    }

    private void CloseRun()
    {
        if (_state < 0 || _instanceCount == _runInstanceOffset)
        {
            return;
        }

        if (_runCount == _runs.Length)
        {
            Array.Resize(ref _runs, Math.Max(8, _runs.Length * 2));
        }

        _runs[_runCount] = new Run(_runInstanceOffset, _instanceCount - _runInstanceOffset, _state);
        _runCount++;
    }
}
