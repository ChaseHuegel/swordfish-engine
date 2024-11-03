using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Editor.UI;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Reflection;
using Swordfish.Library.Types;
using Swordfish.Settings;
using Swordfish.Types;
using Swordfish.UI;
using Swordfish.UI.Elements;

namespace Swordfish.Editor;

public class Editor : IEntryPoint, IAutoActivate
{
    private const ImGuiWindowFlags EDITOR_CANVAS_FLAGS = ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoCollapse;

    private readonly IWindowContext WindowContext;
    private readonly IECSContext ECSContext;
    private readonly IFileParseService _fileParseService;
    private readonly IPathService PathService;
    private readonly IRenderContext RenderContext;
    private readonly IInputService InputService;
    private readonly IUIContext UIContext;
    private readonly DebugSettings DebugSettings;
    private readonly RenderSettings RenderSettings;
    private readonly LogListener LogListener;
    private readonly IModulePathService ModulePathService;
    private readonly ILogger Logger;

    private static CanvasElement Hierarchy;

    private Action FileWrite;

    public Editor(IWindowContext windowContext, IFileParseService fileParseService, IECSContext ecsContext, IPathService pathService, IRenderContext renderContext, IInputService inputService, IUIContext uiContext, ILineRenderer lineRenderer, DebugSettings debugSettings, RenderSettings renderSettings, LogListener logListener, IModulePathService modulePathService, ILogger logger)
    {
        WindowContext = windowContext;
        _fileParseService = fileParseService;
        ECSContext = ecsContext;
        PathService = pathService;
        RenderContext = renderContext;
        InputService = inputService;
        UIContext = uiContext;
        DebugSettings = debugSettings;
        RenderSettings = renderSettings;
        LogListener = logListener;
        ModulePathService = modulePathService;
        Logger = logger;

        //  Scale off target height of 1080
        uiContext.ScaleConstraint.Set(new FactorConstraint(1080));

        //  Setup the hierarchy
        ECSContext.World.AddSystem(new HierarchySystem());
        Hierarchy = UIContext.NewCanvas("Hierarchy");
        Hierarchy.Flags = EDITOR_CANVAS_FLAGS;
        Hierarchy.Constraints = new RectConstraints
        {
            Width = new RelativeConstraint(0.15f),
            Height = new RelativeConstraint(0.8f),
        };

        //  Create the grid and axis display
        const int GRID_SIZE = 500;

        lineRenderer.CreateLine(Vector3.Zero, new Vector3(GRID_SIZE, 0, 0), new Vector4(1f, 0f, 0f, 1f), alwaysOnTop: true);
        lineRenderer.CreateLine(Vector3.Zero, new Vector3(-GRID_SIZE, 0, 0), new Vector4(1f, 0f, 0f, 0.25f), alwaysOnTop: true);

        lineRenderer.CreateLine(Vector3.Zero, new Vector3(0, GRID_SIZE, 0), new Vector4(0f, 1f, 0f, 1f), alwaysOnTop: true);
        lineRenderer.CreateLine(Vector3.Zero, new Vector3(0, -GRID_SIZE, 0), new Vector4(0f, 1f, 0f, 0.25f), alwaysOnTop: true);

        lineRenderer.CreateLine(Vector3.Zero, new Vector3(0, 0, GRID_SIZE), new Vector4(0f, 0f, 1f, 1f), alwaysOnTop: true);
        lineRenderer.CreateLine(Vector3.Zero, new Vector3(0, 0, -GRID_SIZE), new Vector4(0f, 0f, 1f, 0.25f), alwaysOnTop: true);

        for (int x = -GRID_SIZE; x <= GRID_SIZE; x++)
        {
            lineRenderer.CreateLine(new Vector3(x, 0, -GRID_SIZE), new Vector3(x, 0, GRID_SIZE), new Vector4(0f, 0f, 0f, 0.1f));
        }

        for (int z = -GRID_SIZE; z <= GRID_SIZE; z++)
        {
            lineRenderer.CreateLine(new Vector3(-GRID_SIZE, 0, z), new Vector3(GRID_SIZE, 0, z), new Vector4(0f, 0f, 0f, 0.1f));
        }
    }

    public void Run()
    {
        WindowContext.Maximize();

        WindowContext.Update += OnUpdate;

        Shortcut exitShortcut = new(
            "Exit",
            "General",
            ShortcutModifiers.NONE,
            Key.ESC,
            Shortcut.DefaultEnabled,
            WindowContext.Close
        );

        StatsWindow statsWindow = new(WindowContext, ECSContext, RenderContext, RenderSettings, UIContext)
        {
            Visible = DebugSettings.Stats
        };

        DebugSettings.Stats.Changed += OnStatsToggled;
        void OnStatsToggled(object? sender, DataChangedEventArgs<bool> e)
        {
            statsWindow.Visible = e.NewValue;
        }

        MenuBarElement menuBar = new(UIContext)
        {
            Content = {
                new MenuBarItemElement("File") {
                    Content = {
                        new MenuBarItemElement("New") {
                            Content = {
                                new MenuBarItemElement("Plugin", new Shortcut(
                                    "New Plugin",
                                    "Editor",
                                    ShortcutModifiers.CONTROL,
                                    Key.N,
                                    Shortcut.DefaultEnabled,
                                    () => {
                                        Logger.LogInformation("Creating new plugin");
                                        PathInfo outputPath = ModulePathService.Root
                                            .At("Projects")
                                            .At("New Project")
                                            .At("Source").CreateDirectory();
                                        outputPath = outputPath.At("NewPlugin.cs");
                                        var template = new PathInfo("manifest://Templates/NewPlugin.cstemplate");
                                        using Stream templateStream = template.Open();
                                        outputPath.Write(templateStream);
                                        FileWrite?.Invoke();
                                    }
                                )),
                                new MenuBarItemElement("Project", new Shortcut(
                                    "New Project",
                                    "Editor",
                                    ShortcutModifiers.CONTROL | ShortcutModifiers.SHIFT,
                                    Key.N,
                                    Shortcut.DefaultEnabled,
                                    () => Logger.LogInformation("Create new project")
                                )),
                            }
                        },
                        new MenuBarItemElement("Open", new Shortcut(
                            "Open",
                            "Editor",
                            ShortcutModifiers.CONTROL,
                            Key.O,
                            Shortcut.DefaultEnabled,
                            () => {
                                if (TreeNode.Selected.Get() is DataTreeNode<PathInfo> pathNode)
                                {
                                    Logger.LogInformation("Opening {path}", pathNode.Data.Get());
                                    pathNode.Data.Get().TryOpenInDefaultApp();
                                }
                            }
                        )),
                        new MenuBarItemElement("Save", new Shortcut(
                            "Save",
                            "Editor",
                            ShortcutModifiers.CONTROL,
                            Key.S,
                            Shortcut.DefaultEnabled,
                            () => Logger.LogInformation("Save project")
                        )),
                        new MenuBarItemElement("Save As", new Shortcut(
                            "Save As",
                            "Editor",
                            ShortcutModifiers.CONTROL | ShortcutModifiers.SHIFT,
                            Key.S,
                            Shortcut.DefaultEnabled,
                            () => Logger.LogInformation("Save project as")
                        )),
                        new MenuBarItemElement("Exit", exitShortcut),
                    }
                },
                new MenuBarItemElement("Edit"),
                new MenuBarItemElement("View") {
                    Content = {
                        new MenuBarItemElement("Stats", new Shortcut(
                                "Stats",
                                "Editor",
                                ShortcutModifiers.NONE,
                                Key.F5,
                                Shortcut.DefaultEnabled,
                                () => {
                                    DebugSettings.Stats.Set(!DebugSettings.Stats);
                                }
                            )
                        ),
                        new MenuBarItemElement("Wireframe", new Shortcut(
                                "Wireframe",
                                "Editor",
                                ShortcutModifiers.NONE,
                                Key.F6,
                                Shortcut.DefaultEnabled,
                                () => {
                                    RenderSettings.Wireframe.Set(!RenderSettings.Wireframe);
                                }
                            )
                        ),
                        new MenuBarItemElement("Meshes", new Shortcut(
                                "Hide Meshes",
                                "Editor",
                                ShortcutModifiers.NONE,
                                Key.F7,
                                Shortcut.DefaultEnabled,
                                () => {
                                    RenderSettings.HideMeshes.Set(!RenderSettings.HideMeshes);
                                }
                            )
                        ),
                        new MenuBarItemElement("Gizmos") {
                            Content = {
                                new MenuBarItemElement("Transform", new Shortcut(
                                        "Transform Gizmos",
                                        "Debug",
                                        ShortcutModifiers.NONE,
                                        Key.F8,
                                        Shortcut.DefaultEnabled,
                                        () => {
                                            DebugSettings.Gizmos.Transforms.Set(!DebugSettings.Gizmos.Transforms);
                                        }
                                    )
                                ),
                                new MenuBarItemElement("Physics", new Shortcut(
                                        "Physics Gizmos",
                                        "Debug",
                                        ShortcutModifiers.NONE,
                                        Key.F9,
                                        Shortcut.DefaultEnabled,
                                        () => {
                                            DebugSettings.Gizmos.Physics.Set(!DebugSettings.Gizmos.Physics);
                                        }
                                    )
                                )
                            }
                        }
                    }
                },
                new MenuBarItemElement("Tools"),
                new MenuBarItemElement("Run"),
                new MenuBarItemElement("Help"),
                new TextElement("Swordfish Engine " + SwordfishEngine.Version)
                {
                    Wrap = false,
                    Constraints = new RectConstraints()
                    {
                        Anchor = ConstraintAnchor.TOP_RIGHT
                    }
                }
            }
        };

        CanvasElement console = new(UIContext, "Console")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            AutoScroll = true,
            Constraints = new RectConstraints
            {
                X = new RelativeConstraint(0f),
                Y = new RelativeConstraint(0.8f),
                Width = new RelativeConstraint(0.55f),
                Height = new RelativeConstraint(0.2f)
            }
        };
        
        foreach (LogEventArgs record in LogListener.GetHistory())
            OnNewLog(null, record);

        LogListener.NewLog += OnNewLog;
        
        void OnNewLog(object? sender, LogEventArgs e)
        {
            console.Content.Add(new TextElement($"{e.LogLevel}: {e.Log}")
            {
                Color = e.LogLevel.GetColor(),
            });
        }

        CanvasElement assetBrowser = new(UIContext, "Asset Browser")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            Constraints = new RectConstraints
            {
                X = new RelativeConstraint(0.55f),
                Y = new RelativeConstraint(0.8f),
                Width = new RelativeConstraint(0.28f),
                Height = new RelativeConstraint(0.2f)
            }
        };

        PopulateDirectory(assetBrowser, PathService.Root);

        WindowContext.Focused += RefreshAssetBrowser;
        FileWrite += RefreshAssetBrowser;
        void RefreshAssetBrowser()
        {
            List<IElement> removalList = RefreshContentRecursively(assetBrowser);
            removalList.Reverse();
            foreach (var element in removalList)
                element.Parent?.Content.Remove(element);
        }

        List<IElement> RefreshContentRecursively(ContentElement element)
        {
            List<IElement> removalList = new();

            if (element is DataTreeNode<PathInfo> node)
            {
                string? path = node.Data.Get();
                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    removalList.Add(node);
                    return removalList;
                }
            }

            foreach (TreeNode child in element.Content.OfType<DataTreeNode<PathInfo>>())
            {
                removalList.AddRange(RefreshContentRecursively(child));
            }

            return removalList;
        }

        void PopulateDirectory(ContentElement root, string path)
        {
            foreach (string dir in Directory.GetDirectories(path))
            {
                DataTreeNode<PathInfo> node = new(Path.GetFileName(dir), new PathInfo(dir));
                PopulateDirectory(node, dir);
                root.Content.Add(node);
            }

            PopulateFiles(root, path);
        }

        void PopulateFiles(ContentElement root, string directory)
        {
            foreach (string file in Directory.GetFiles(directory, "*.*"))
            {
                DataTreeNode<PathInfo> node = new(Path.GetFileName(file), new PathInfo(file));
                root.Content.Add(node);
            }

            root.Content.Add(new DividerElement());
        }

        CanvasElement inspector = new(UIContext, "Inspector")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            Constraints = new RectConstraints
            {
                Anchor = ConstraintAnchor.TOP_RIGHT,
                Width = new RelativeConstraint(0.17f),
                Height = new RelativeConstraint(1f)
            }
        };

        TreeNode.Selected.Changed += (sender, args) =>
        {
            inspector.Content.Clear();

            if (args.NewValue is DataTreeNode<Entity> entityNode)
            {
                var entity = entityNode.Data.Get();
                BuildInpsectorView(inspector, entity);

                var components = entity.GetAllData();
                foreach (var component in components)
                {
                    if (component == null)
                        continue;

                    BuildInpsectorView(inspector, component);
                }
            }
            else if (args.NewValue is DataTreeNode<PathInfo> pathNode)
            {
                if (!File.Exists(pathNode.Data.Get().Value))
                    return;

                var fileInfo = new FileInfo(pathNode.Data.Get().Value);
                var group = new PaneElement(pathNode.Data.Get().GetType().ToString())
                {
                    Constraints = {
                        Width = new FillConstraint()
                    },
                    Content = {
                        new PaneElement($"File ({fileInfo.Extension})")
                        {
                            Tooltip = new Tooltip
                            {
                                Text = fileInfo.Extension
                            },
                            Constraints = new RectConstraints()
                            {
                                Width = new FillConstraint()
                            },
                            Content = {
                                new TextElement(Path.GetFileNameWithoutExtension(fileInfo.Name))
                            }
                        },
                        new PaneElement("Size")
                        {
                            Constraints = new RectConstraints()
                            {
                                Width = new FillConstraint()
                            },
                            Content = {
                                new TextElement(ByteSize.FromBytes(fileInfo.Length).ToString())
                            }
                        },
                        new PaneElement("Modified")
                        {
                            Constraints = new RectConstraints()
                            {
                                Width = new FillConstraint()
                            },
                            Content = {
                                new TextElement(fileInfo.LastWriteTime.ToString())
                            }
                        },
                        new PaneElement("Created")
                        {
                            Constraints = new RectConstraints()
                            {
                                Width = new FillConstraint()
                            },
                            Content = {
                                new TextElement(fileInfo.CreationTime.ToString())
                            }
                        },
                        new PaneElement("Location")
                        {
                            Constraints = new RectConstraints()
                            {
                                Width = new FillConstraint()
                            },
                            Content = {
                                new TextElement(pathNode.Data.Get().ToString())
                            }
                        }
                    }
                };

                inspector.Content.Add(group);
            }
        };
    }

    private static void BuildInpsectorView(ContentElement contentElement, object component, int depth = 0)
    {
        //  TODO setting this too far can result in throws due to reflection hitting something it shouldn't
        //  TODO setting this too deep (really beyond 2) is noisey and mostly useless since there is no filtering of what is displayed yet
        const int maxDepth = 1;
        if (depth > maxDepth)
            return;
        else
            depth++;

        var componentType = component.GetType();
        var group = new PaneElement(componentType.Name.ToTitle())
        {
            Constraints = {
                Width = new FillConstraint()
            }
        };

        var publicStaticProperties = Reflection.GetProperties(componentType, Reflection.BINDINGS_PUBLIC_STATIC);
        var publicStaticFields = Reflection.GetFields(componentType, Reflection.BINDINGS_PUBLIC_STATIC);

        var publicInstanceProperties = Reflection.GetProperties(componentType, Reflection.BINDINGS_PUBLIC_INSTANCE);
        var publicInstanceFields = Reflection.GetFields(componentType, Reflection.BINDINGS_PUBLIC_INSTANCE);

        var privateStaticProperties = Reflection.GetProperties(componentType, Reflection.BINDINGS_PRIVATE_STATIC);
        var privateStaticFields = Reflection.GetFields(componentType, Reflection.BINDINGS_PRIVATE_STATIC, true);    //  Ignore backing fields

        var privateInstanceProperties = Reflection.GetProperties(componentType, Reflection.BINDINGS_PRIVATE_INSTANCE);
        var privateInstanceFields = Reflection.GetFields(componentType, Reflection.BINDINGS_PRIVATE_INSTANCE, true);    //  Ignore backing fields

        if (publicInstanceProperties.Length > 0 || publicInstanceFields.Length > 0)
        {
            var publicGroup = new ColorBlockElement(Color.White);
            group.Content.Add(publicGroup);

            foreach (var property in publicInstanceProperties)
            {
                if (property.PropertyType.IsClass && property.PropertyType != typeof(string) && depth < maxDepth)
                    BuildInpsectorView(publicGroup, property.GetValue(component)!, depth);
                else
                    publicGroup.Content.Add(PropertyViewFactory(component, property));
            }

            foreach (var field in publicInstanceFields)
            {
                if (field.FieldType.IsClass && field.FieldType != typeof(string) && depth < maxDepth)
                    BuildInpsectorView(publicGroup, field.GetValue(component)!, depth);
                else
                    publicGroup.Content.Add(FieldViewFactory(component, field));
            }
        }

        if (publicStaticProperties.Length > 0 || publicStaticFields.Length > 0)
        {
            var staticBlock = new ColorBlockElement(Color.CornflowerBlue);
            group.Content.Add(staticBlock);

            staticBlock.Content.Add(new TitleBarElement("Static Members", false, ConstraintAnchor.TOP_CENTER));

            foreach (var property in publicStaticProperties)
            {
                if (property.PropertyType.IsClass && property.PropertyType != typeof(string) && depth < maxDepth)
                    BuildInpsectorView(staticBlock, property.GetValue(component)!, depth);
                else
                    staticBlock.Content.Add(PropertyViewFactory(component, property));
            }

            foreach (var field in publicStaticFields)  //  Ignore backing fields
            {
                if (field.FieldType.IsClass && field.FieldType != typeof(string) && depth < maxDepth)
                    BuildInpsectorView(staticBlock, field.GetValue(component)!, depth);
                else
                    staticBlock.Content.Add(FieldViewFactory(component, field));
            }
        }

        if (privateInstanceProperties.Length > 0 || privateInstanceFields.Length > 0)
        {
            var privateBlock = new ColorBlockElement(Color.SlateGray);
            group.Content.Add(privateBlock);

            privateBlock.Content.Add(new TitleBarElement("Members (private)", false, ConstraintAnchor.TOP_CENTER));

            foreach (var property in privateInstanceProperties)
            {
                if (property.PropertyType.IsClass && property.PropertyType != typeof(string) && depth < maxDepth)
                    BuildInpsectorView(privateBlock, property.GetValue(component)!, depth);
                else
                    privateBlock.Content.Add(PropertyViewFactory(component, property));
            }

            foreach (var field in privateInstanceFields)
            {
                if (field.FieldType.IsClass && field.FieldType != typeof(string) && depth < maxDepth)
                    BuildInpsectorView(privateBlock, field.GetValue(component)!, depth);
                else
                    privateBlock.Content.Add(FieldViewFactory(component, field));
            }
        }

        if (privateStaticProperties.Length > 0 || privateStaticFields.Length > 0)
        {
            var privateStaticBlock = new ColorBlockElement(Color.SteelBlue);
            group.Content.Add(privateStaticBlock);

            privateStaticBlock.Content.Add(new TitleBarElement("Static Members (private)", false, ConstraintAnchor.TOP_CENTER));

            foreach (var property in privateStaticProperties)
            {
                if (property.PropertyType.IsClass && property.PropertyType != typeof(string) && depth < maxDepth)
                    BuildInpsectorView(privateStaticBlock, property.GetValue(component)!, depth);
                else
                    privateStaticBlock.Content.Add(PropertyViewFactory(component, property));
            }

            foreach (var field in privateStaticFields)
            {
                if (field.FieldType.IsClass && field.FieldType != typeof(string) && depth < maxDepth)
                    BuildInpsectorView(privateStaticBlock, field.GetValue(component)!, depth);
                else
                    privateStaticBlock.Content.Add(FieldViewFactory(component, field));
            }
        }

        contentElement.Content.Add(group);
    }

    private static PaneElement FieldViewFactory(object component, FieldInfo field)
    {
        return MemberViewFactory(
            field.Name,
            field.GetSignature(),
            field.GetValue(component),
            field.FieldType,
            field.IsLiteral | field.IsInitOnly == false
        );
    }

    private static PaneElement PropertyViewFactory(object component, PropertyInfo property)
    {
        return MemberViewFactory(
            property.Name,
            property.GetSignature(),
            property.GetValue(component),
            property.PropertyType,
            property.GetSetMethod() != null
        );
    }

    private static PaneElement MemberViewFactory(string name, string signature, object? value, Type type, bool canWrite)
    {
        return new PaneElement(name.ToTitle())
        {
            Tooltip = new Tooltip
            {
                Text = signature,
                MaxWidth = 300
            },
            Constraints = new RectConstraints
            {
                Anchor = ConstraintAnchor.TOP_CENTER,
                Width = new RelativeConstraint(0.9f)
            },
            Content = {
                new TextElement(value?.ToString() ?? "null") {
                    Color = canWrite ? Color.White : Color.Gray,
                    Label = type.Name
                }
            }
        };
    }

    public class HierarchySystem : IEntitySystem
    {
        private readonly HashSet<int> PopulatedEntities = [];

        public void Tick(float delta, DataStore store)
        {
            store.Query(delta, ForEachEntity);
        }

        private void ForEachEntity(float delta, DataStore store, int entity)
        {
            if (!PopulatedEntities.Add(entity))
            {
                return;
            }

            //  TODO handle removed entities
            string? displayName = store.TryGet(entity, out IdentifierComponent identifier) ? identifier.Name : $"<entity:{entity}>";
            Hierarchy.Content.Add(new DataTreeNode<Entity>(displayName, new Entity(entity, store)));
        }
    }

    private float _cameraSpeedModifier = 1f;
    private void OnUpdate(double delta)
    {
        const float mouseSensitivity = 0.05f;
        const float cameraBaseSpeed = 10;

        if (InputService.IsKeyHeld(Key.SHIFT))
            _cameraSpeedModifier += (float)delta;
        else
            _cameraSpeedModifier = 1f;

        float cameraSpeed = cameraBaseSpeed * _cameraSpeedModifier;

        Camera camera = RenderContext.Camera.Get();

        if (InputService.IsMouseHeld(MouseButton.RIGHT))
        {
            InputService.CursorState = CursorState.LOCKED;
            Vector2 cursorDelta = InputService.CursorDelta;
            camera.Transform.Rotate(new Vector3(0, -cursorDelta.X, 0) * mouseSensitivity, false);
            camera.Transform.Rotate(new Vector3(-cursorDelta.Y, 0, 0) * mouseSensitivity, true);
        }
        else
        {
            InputService.CursorState = CursorState.NORMAL;
        }

        var forward = camera.Transform.GetForward();
        var right = camera.Transform.GetRight();

        if (InputService.IsKeyHeld(Key.W))
            camera.Transform.Position -= forward * cameraSpeed * (float)delta;

        if (InputService.IsKeyHeld(Key.S))
            camera.Transform.Position += forward * cameraSpeed * (float)delta;

        if (InputService.IsKeyHeld(Key.D))
            camera.Transform.Position += right * cameraSpeed * (float)delta;

        if (InputService.IsKeyHeld(Key.A))
            camera.Transform.Position -= right * cameraSpeed * (float)delta;

        if (InputService.IsKeyHeld(Key.E))
            camera.Transform.Position += new Vector3(0, cameraSpeed * (float)delta, 0);

        if (InputService.IsKeyHeld(Key.Q))
            camera.Transform.Position -= new Vector3(0, cameraSpeed * (float)delta, 0);

        if (InputService.IsKeyPressed(Key.UP_ARROW))
            camera.Transform.Position += new Vector3(0, 1, 0);

        if (InputService.IsKeyPressed(Key.DOWN_ARROW))
            camera.Transform.Position -= new Vector3(0, 1, 0);
    }
}