#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Settings;

namespace Swordfish.Tests;

/// <summary>An input stub whose held state and cursor deltas a test can drive.</summary>
internal sealed class FakeInputService : IInputService
{
    public HashSet<Key> HeldKeys { get; } = [];

    public HashSet<MouseButton> HeldButtons { get; } = [];

    public InputDevice[] Devices => [];

    public InputDevice[] Mice => [];

    public InputDevice[] Keyboards => [];

    public InputDevice[] Gamepads => [];

    public InputDevice[] Joysticks => [];

    public InputDevice[] UnknownDevices => [];

    public EventHandler<ClickedEventArgs> Clicked { get; set; } = null!;

    public EventHandler<ClickedEventArgs> DoubleClicked { get; set; } = null!;

    public EventHandler<ScrolledEventArgs> Scrolled { get; set; } = null!;

    public EventHandler<CharEventArgs> CharInput { get; set; } = null!;

    public EventHandler<KeyEventArgs> KeyPressed { get; set; } = null!;

    public EventHandler<KeyEventArgs> KeyReleased { get; set; } = null!;

    public EventHandler<InputButtonEventArgs> ButtonPressed { get; set; } = null!;

    public EventHandler<InputButtonEventArgs> ButtonReleased { get; set; } = null!;

    public CursorOptions CursorOptions { get; set; }

    public Vector2 CursorDelta => Vector2.Zero;

    public Vector2 CursorPosition { get; set; }

    public bool IsMouseHeld(MouseButton mouseButton) => HeldButtons.Contains(mouseButton);

    public bool IsMousePressed(MouseButton mouseButton) => false;

    public bool IsMouseReleased(MouseButton mouseButton) => false;

    public float GetMouseScroll() => 0f;

    public bool IsKeyHeld(Key key) => HeldKeys.Contains(key);

    public bool IsKeyPressed(Key key) => false;

    public bool IsKeyReleased(Key key) => false;

    public bool IsButtonHeld(InputButton button) => false;

    public bool IsButtonPressed(InputButton button) => false;

    public bool IsButtonReleased(InputButton button) => false;

    public float GetAxis(InputAxis axis) => 0f;

    public float GetAxisDeadzone(InputAxis axis) => 0f;

    public void SetAxisDeadzone(InputAxis axis, float deadzone) { }

    public string GetClipboard() => string.Empty;

    public void SetClipboard(string content) { }
}

/// <summary>A window stub that records focus and update callbacks without a real window.</summary>
internal sealed class FakeWindowContext : IWindowContext
{
    public DataBinding<double> UpdateDelta { get; } = new(0d);

    public DataBinding<double> RenderDelta { get; } = new(0d);

    public Vector2 Resolution => Vector2.Zero;

    public Vector2 MonitorResolution => Vector2.Zero;

    public Action? Loaded { get; set; }

    public Action? Closed { get; set; }

    public Action<double>? Render { get; set; }

    public Action<double>? Update { get; set; }

    public Action? Focused { get; set; }

    public Action? Unfocused { get; set; }

    public Action<Vector2>? Resized { get; set; }

    public Vector2 GetSize() => Vector2.Zero;

    /// <inheritdoc/>
    public IReadOnlyList<Vector2> GetVideoModes()
    {
        return
        [
            new Vector2(1280, 720),
            new Vector2(1920, 1080),
            new Vector2(2560, 1440),
        ];
    }

    /// <inheritdoc/>
    public void ApplyDisplaySettings(WindowMode mode, bool borderless, int? width = null, int? height = null) { }

    public void Close() { }

    public void SetIcon(Texture icon) { }
}

/// <summary>A render-context stub backed by a real camera entity, for builders that child to it.</summary>
internal sealed class FakeRenderContext : IRenderContext
{
    public DataBinding<CameraEntity> MainCamera { get; }

    public FakeRenderContext(DataStore store)
    {
        int camera = store.Alloc();
        var transform = new TransformComponent();
        var viewFrustum = new ViewFrustumComponent(0.1f, 1000f, 70);
        store.AddOrUpdate(camera, transform);
        store.AddOrUpdate(camera, viewFrustum);
        MainCamera = new DataBinding<CameraEntity>(new CameraEntity(new Entity(camera, store), viewFrustum, transform));
    }

    RenderScene IRenderContext.GetSceneContext() => default;

    public void Bind(Shader shader) { }

    public void Bind(Texture texture) { }

    public void Bind(Mesh mesh) { }

    public void Bind(MeshRenderer meshRenderer, Uuid entity) { }

    public void Bind(RectRenderer rectRenderer) { }

    public void Bind(Material material) { }
}
