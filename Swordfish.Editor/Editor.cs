using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Shoal.Modularity;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Reflection;
using Swordfish.Library.Types;
using Swordfish.Settings;
using Swordfish.Types;

namespace Swordfish.Editor;

// ReSharper disable once ClassNeverInstantiated.Global
public class Editor : IEntryPoint, IAutoActivate
{
    //  TODO #346 Update to use Reef UI
    // private const ImGuiWindowFlags EDITOR_CANVAS_FLAGS = ImGuiWindowFlags.AlwaysAutoResize
    //     | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoCollapse;

    private readonly IWindowContext _windowContext;
    // private readonly IECSContext _ecsContext;
    private readonly IRenderContext _renderContext;
    private readonly IInputService _inputService;
    private readonly IShortcutService _shortcutService;
    // private readonly DebugSettings _debugSettings;
    // private readonly RenderSettings _renderSettings;
    // private readonly LogListener _logListener;
    // private readonly ILogger _logger;
    private readonly WindowSettings _windowSettings;

    private Action? _fileWrite;

    public Editor(IWindowContext windowContext,
        // IECSContext ecsContext,
        IRenderContext renderContext,
        IInputService inputService,
        ILineRenderer lineRenderer,
        IShortcutService shortcutService,
        // DebugSettings debugSettings,
        // RenderSettings renderSettings,
        // LogListener logListener,
        // ILogger logger,
        WindowSettings windowSettings)
    {
        _windowContext = windowContext;
        // _ecsContext = ecsContext;
        _renderContext = renderContext;
        _inputService = inputService;
        _shortcutService = shortcutService;
        // _debugSettings = debugSettings;
        // _renderSettings = renderSettings;
        // _logListener = logListener;
        // _logger = logger;
        _windowSettings = windowSettings;

        //  TODO #346 Update to use Reef UI
        // //  Scale off target height of 1080
        // uiContext.ScaleConstraint.Set(new FactorConstraint(1080));
        //
        // //  Set up the hierarchy
        // _ecsContext.World.AddSystem(new HierarchySystem());
        // _hierarchy = new CanvasElement(_uiContext, _windowContext, "Hierarchy")
        // {
        //     Flags = EDITOR_CANVAS_FLAGS,
        //     Constraints = new RectConstraints
        //     {
        //         Width = new RelativeConstraint(0.15f),
        //         Height = new RelativeConstraint(0.8f),
        //     },
        // };

        //  Create the grid and axis display
        const int gridSize = 500;

        lineRenderer.CreateLine(Vector3.Zero, new Vector3(gridSize, 0, 0), new Vector4(1f, 0f, 0f, 1f), alwaysOnTop: true);
        lineRenderer.CreateLine(Vector3.Zero, new Vector3(-gridSize, 0, 0), new Vector4(1f, 0f, 0f, 0.25f), alwaysOnTop: true);

        lineRenderer.CreateLine(Vector3.Zero, new Vector3(0, gridSize, 0), new Vector4(0f, 1f, 0f, 1f), alwaysOnTop: true);
        lineRenderer.CreateLine(Vector3.Zero, new Vector3(0, -gridSize, 0), new Vector4(0f, 1f, 0f, 0.25f), alwaysOnTop: true);

        lineRenderer.CreateLine(Vector3.Zero, new Vector3(0, 0, gridSize), new Vector4(0f, 0f, 1f, 1f), alwaysOnTop: true);
        lineRenderer.CreateLine(Vector3.Zero, new Vector3(0, 0, -gridSize), new Vector4(0f, 0f, 1f, 0.25f), alwaysOnTop: true);

        for (int x = -gridSize; x <= gridSize; x++)
        {
            lineRenderer.CreateLine(new Vector3(x, 0, -gridSize), new Vector3(x, 0, gridSize), new Vector4(0f, 0f, 0f, 0.1f));
        }

        for (int z = -gridSize; z <= gridSize; z++)
        {
            lineRenderer.CreateLine(new Vector3(-gridSize, 0, z), new Vector3(gridSize, 0, z), new Vector4(0f, 0f, 0f, 0.1f));
        }
    }

    public void Run()
    {
        _windowSettings.Mode.Set(WindowMode.Maximized);

        _windowContext.Update += OnUpdate;

        Shortcut exitShortcut = new(
            "Exit",
            "General",
            ShortcutModifiers.None,
            Key.Esc,
            Shortcut.DefaultEnabled,
            _windowContext.Close
        );
        _shortcutService.RegisterShortcut(exitShortcut);

        //  TODO #346 Update to use Reef UI
        // StatsWindow statsWindow = new(_windowContext, _ecsContext, _renderContext, _renderSettings, _uiContext)
        // {
        //     Visible = _debugSettings.Stats,
        // };
        //
        // _debugSettings.Stats.Changed += OnStatsToggled;
        // void OnStatsToggled(object? sender, DataChangedEventArgs<bool> e)
        // {
        //     statsWindow.Visible = e.NewValue;
        // }
        //
        // // ReSharper disable once UnusedVariable
        // MenuBarElement menuBar = new(_uiContext)
        // {
        //     Content = {
        //         new MenuBarItemElement("File") {
        //             Content = {
        //                 new MenuBarItemElement("New") {
        //                     Content = {
        //                         new MenuBarItemElement("Plugin", _shortcutService.NewShortcut(
        //                             "New Plugin",
        //                             "Editor",
        //                             ShortcutModifiers.Control,
        //                             Key.N,
        //                             Shortcut.DefaultEnabled,
        //                             () => {
        //                                 _logger.LogInformation("Creating new plugin");
        //                                 PathInfo outputPath = new PathInfo("projects").At("New Project").CreateDirectory();
        //                                 outputPath = outputPath.At("NewPlugin.cs");
        //                                 var template = new PathInfo("manifest://Templates/NewPlugin.cstemplate");
        //                                 using Stream templateStream = template.Open();
        //                                 outputPath.Write(templateStream);
        //                                 _fileWrite?.Invoke();
        //                             }
        //                         )),
        //                         new MenuBarItemElement("Project", _shortcutService.NewShortcut(
        //                             "New Project",
        //                             "Editor",
        //                             ShortcutModifiers.Control | ShortcutModifiers.Shift,
        //                             Key.N,
        //                             Shortcut.DefaultEnabled,
        //                             () => _logger.LogInformation("Create new project")
        //                         )),
        //                     },
        //                 },
        //                 new MenuBarItemElement("Open", _shortcutService.NewShortcut(
        //                     "Open",
        //                     "Editor",
        //                     ShortcutModifiers.Control,
        //                     Key.O,
        //                     Shortcut.DefaultEnabled,
        //                     () =>
        //                     {
        //                         if (TreeNode.Selected.Get() is not DataTreeNode<PathInfo> pathNode)
        //                         {
        //                             return;
        //                         }
        //
        //                         _logger.LogInformation("Opening {path}", pathNode.Data.Get());
        //                         pathNode.Data.Get().TryOpenInDefaultApp();
        //                     }
        //                 )),
        //                 new MenuBarItemElement("Save", _shortcutService.NewShortcut(
        //                     "Save",
        //                     "Editor",
        //                     ShortcutModifiers.Control,
        //                     Key.S,
        //                     Shortcut.DefaultEnabled,
        //                     () => _logger.LogInformation("Save project")
        //                 )),
        //                 new MenuBarItemElement("Save As", _shortcutService.NewShortcut(
        //                     "Save As",
        //                     "Editor",
        //                     ShortcutModifiers.Control | ShortcutModifiers.Shift,
        //                     Key.S,
        //                     Shortcut.DefaultEnabled,
        //                     () => _logger.LogInformation("Save project as")
        //                 )),
        //                 new MenuBarItemElement("Exit", exitShortcut),
        //             },
        //         },
        //         new MenuBarItemElement("Edit"),
        //         new MenuBarItemElement("View") {
        //             Content = {
        //                 new MenuBarItemElement("Stats", _shortcutService.NewShortcut(
        //                         "Stats",
        //                         "Editor",
        //                         ShortcutModifiers.None,
        //                         Key.F5,
        //                         Shortcut.DefaultEnabled,
        //                         () => {
        //                             _debugSettings.Stats.Set(!_debugSettings.Stats);
        //                         }
        //                     )
        //                 ),
        //                 new MenuBarItemElement("Wireframe", _shortcutService.NewShortcut(
        //                         "Wireframe",
        //                         "Editor",
        //                         ShortcutModifiers.None,
        //                         Key.F6,
        //                         Shortcut.DefaultEnabled,
        //                         () => {
        //                             _renderSettings.Wireframe.Set(!_renderSettings.Wireframe);
        //                         }
        //                     )
        //                 ),
        //                 new MenuBarItemElement("Meshes", _shortcutService.NewShortcut(
        //                         "Hide Meshes",
        //                         "Editor",
        //                         ShortcutModifiers.None,
        //                         Key.F7,
        //                         Shortcut.DefaultEnabled,
        //                         () => {
        //                             _renderSettings.HideMeshes.Set(!_renderSettings.HideMeshes);
        //                         }
        //                     )
        //                 ),
        //                 new MenuBarItemElement("Gizmos") {
        //                     Content = {
        //                         new MenuBarItemElement("Transform", _shortcutService.NewShortcut(
        //                                 "Transform Gizmos",
        //                                 "Debug",
        //                                 ShortcutModifiers.None,
        //                                 Key.F8,
        //                                 Shortcut.DefaultEnabled,
        //                                 () => {
        //                                     _debugSettings.Gizmos.Transforms.Set(!_debugSettings.Gizmos.Transforms);
        //                                 }
        //                             )
        //                         ),
        //                         new MenuBarItemElement("Physics", _shortcutService.NewShortcut(
        //                                 "Physics Gizmos",
        //                                 "Debug",
        //                                 ShortcutModifiers.None,
        //                                 Key.F9,
        //                                 Shortcut.DefaultEnabled,
        //                                 () => {
        //                                     _debugSettings.Gizmos.Physics.Set(!_debugSettings.Gizmos.Physics);
        //                                 }
        //                             )
        //                         ),
        //                     },
        //                 },
        //             },
        //         },
        //         new MenuBarItemElement("Tools"),
        //         new MenuBarItemElement("Run"),
        //         new MenuBarItemElement("Help"),
        //         new TextElement("Swordfish Engine " + SwordfishEngine.Version)
        //         {
        //             Wrap = false,
        //             Constraints = new RectConstraints
        //             {
        //                 Anchor = ConstraintAnchor.TOP_RIGHT,
        //             },
        //         },
        //     },
        // };
        //
        // CanvasElement console = new(_uiContext, _windowContext, "Console")
        // {
        //     Flags = EDITOR_CANVAS_FLAGS,
        //     AutoScroll = true,
        //     Constraints = new RectConstraints
        //     {
        //         X = new RelativeConstraint(0f),
        //         Y = new RelativeConstraint(0.8f),
        //         Width = new RelativeConstraint(0.55f),
        //         Height = new RelativeConstraint(0.2f),
        //     },
        // };
        //
        // foreach (LogEventArgs record in _logListener.GetHistory())
        // {
        //     OnNewLog(null, record);
        // }
        //
        // _logListener.NewLog += OnNewLog;
        //
        // void OnNewLog(object? sender, LogEventArgs e)
        // {
        //     console.Content.Add(new TextElement($"{e.LogLevel}: {e.Log}")
        //     {
        //         Color = e.LogLevel.GetColor(),
        //     });
        // }
        //
        // CanvasElement assetBrowser = new(_uiContext, _windowContext, "Asset Browser")
        // {
        //     Flags = EDITOR_CANVAS_FLAGS,
        //     Constraints = new RectConstraints
        //     {
        //         X = new RelativeConstraint(0.55f),
        //         Y = new RelativeConstraint(0.8f),
        //         Width = new RelativeConstraint(0.28f),
        //         Height = new RelativeConstraint(0.2f),
        //     },
        // };
        //
        // PopulateDirectory(assetBrowser, AppDomain.CurrentDomain.BaseDirectory);
        //
        // _windowContext.Focused += RefreshAssetBrowser;
        // _fileWrite += RefreshAssetBrowser;
        // void RefreshAssetBrowser()
        // {
        //     List<IElement> removalList = RefreshContentRecursively(assetBrowser);
        //     removalList.Reverse();
        //     foreach (IElement element in removalList)
        //     {
        //         element.Parent?.Content.Remove(element);
        //     }
        // }
        //
        // List<IElement> RefreshContentRecursively(ContentElement element)
        // {
        //     List<IElement> removalList = new();
        //
        //     if (element is DataTreeNode<PathInfo> node)
        //     {
        //         string? path = node.Data.Get();
        //         if (!Directory.Exists(path) && !File.Exists(path))
        //         {
        //             removalList.Add(node);
        //             return removalList;
        //         }
        //     }
        //
        //     foreach (DataTreeNode<PathInfo> child in element.Content.OfType<DataTreeNode<PathInfo>>())
        //     {
        //         removalList.AddRange(RefreshContentRecursively(child));
        //     }
        //
        //     return removalList;
        // }
        //
        // void PopulateDirectory(ContentElement root, string path)
        // {
        //     foreach (string dir in Directory.GetDirectories(path))
        //     {
        //         DataTreeNode<PathInfo> node = new(Path.GetFileName(dir), new PathInfo(dir));
        //         PopulateDirectory(node, dir);
        //         root.Content.Add(node);
        //     }
        //
        //     PopulateFiles(root, path);
        // }
        //
        // void PopulateFiles(ContentElement root, string directory)
        // {
        //     foreach (string file in Directory.GetFiles(directory, "*.*"))
        //     {
        //         DataTreeNode<PathInfo> node = new(Path.GetFileName(file), new PathInfo(file));
        //         root.Content.Add(node);
        //     }
        //
        //     root.Content.Add(new DividerElement());
        // }
        //
        // CanvasElement inspector = new(_uiContext, _windowContext, "Inspector")
        // {
        //     Flags = EDITOR_CANVAS_FLAGS,
        //     Constraints = new RectConstraints
        //     {
        //         Anchor = ConstraintAnchor.TOP_RIGHT,
        //         Width = new RelativeConstraint(0.17f),
        //         Height = new RelativeConstraint(1f),
        //     },
        // };
        //
        // TreeNode.Selected.Changed += (_, args) =>
        // {
        //     inspector.Content.Clear();
        //
        //     switch (args.NewValue)
        //     {
        //         case DataTreeNode<Entity> entityNode:
        //         {
        //             Entity entity = entityNode.Data.Get();
        //             BuildInspectorView(inspector, entity);
        //
        //             Span<IDataComponent> components = entity.GetAllData();
        //             foreach (IDataComponent component in components)
        //             {
        //                 BuildInspectorView(inspector, component);
        //             }
        //
        //             break;
        //         }
        //         case DataTreeNode<PathInfo> pathNode when !File.Exists(pathNode.Data.Get().Value):
        //             return;
        //         case DataTreeNode<PathInfo> pathNode:
        //         {
        //             var fileInfo = new FileInfo(pathNode.Data.Get().Value);
        //             var group = new PaneElement(pathNode.Data.Get().GetType().ToString())
        //             {
        //                 Constraints = {
        //                     Width = new FillConstraint(),
        //                 },
        //                 Content = {
        //                     new PaneElement($"File ({fileInfo.Extension})")
        //                     {
        //                         Tooltip = new Tooltip
        //                         {
        //                             Text = fileInfo.Extension,
        //                         },
        //                         Constraints = new RectConstraints
        //                         {
        //                             Width = new FillConstraint(),
        //                         },
        //                         Content = {
        //                             new TextElement(Path.GetFileNameWithoutExtension(fileInfo.Name)),
        //                         },
        //                     },
        //                     new PaneElement("Size")
        //                     {
        //                         Constraints = new RectConstraints
        //                         {
        //                             Width = new FillConstraint(),
        //                         },
        //                         Content = {
        //                             new TextElement(ByteSize.FromBytes(fileInfo.Length).ToString()),
        //                         },
        //                     },
        //                     new PaneElement("Modified")
        //                     {
        //                         Constraints = new RectConstraints
        //                         {
        //                             Width = new FillConstraint(),
        //                         },
        //                         Content = {
        //                             new TextElement(fileInfo.LastWriteTime.ToString(CultureInfo.InvariantCulture)),
        //                         },
        //                     },
        //                     new PaneElement("Created")
        //                     {
        //                         Constraints = new RectConstraints
        //                         {
        //                             Width = new FillConstraint(),
        //                         },
        //                         Content = {
        //                             new TextElement(fileInfo.CreationTime.ToString(CultureInfo.InvariantCulture)),
        //                         },
        //                     },
        //                     new PaneElement("Location")
        //                     {
        //                         Constraints = new RectConstraints
        //                         {
        //                             Width = new FillConstraint(),
        //                         },
        //                         Content = {
        //                             new TextElement(pathNode.Data.Get().ToString()),
        //                         },
        //                     },
        //                 },
        //             };
        //
        //             inspector.Content.Add(group);
        //             break;
        //         }
        //     }
        // };
    }

    //  TODO #346 Update to use Reef UI
    // private static void BuildInspectorView(ContentElement contentElement, object component, int depth = 0)
    // {
    //     //  TODO setting this too far can result in throws due to reflection hitting something it shouldn't
    //     //  TODO setting this too deep (really beyond 2) is noisey and mostly useless since there is no filtering of what is displayed yet
    //     const int maxDepth = 1;
    //     if (depth > maxDepth)
    //     {
    //         return;
    //     }
    //     depth++;
    //
    //     Type componentType = component.GetType();
    //     var group = new PaneElement(componentType.Name.ToTitle())
    //     {
    //         Constraints = {
    //             Width = new FillConstraint(),
    //         },
    //     };
    //
    //     PropertyInfo[]? publicStaticProperties = Reflection.GetProperties(componentType, Reflection.BINDINGS_PUBLIC_STATIC);
    //     FieldInfo[]? publicStaticFields = Reflection.GetFields(componentType, Reflection.BINDINGS_PUBLIC_STATIC);
    //
    //     PropertyInfo[]? publicInstanceProperties = Reflection.GetProperties(componentType, Reflection.BINDINGS_PUBLIC_INSTANCE);
    //     FieldInfo[]? publicInstanceFields = Reflection.GetFields(componentType, Reflection.BINDINGS_PUBLIC_INSTANCE);
    //
    //     PropertyInfo[]? privateStaticProperties = Reflection.GetProperties(componentType, Reflection.BINDINGS_PRIVATE_STATIC);
    //     FieldInfo[]? privateStaticFields = Reflection.GetFields(componentType, Reflection.BINDINGS_PRIVATE_STATIC, true);    //  Ignore backing fields
    //
    //     PropertyInfo[]? privateInstanceProperties = Reflection.GetProperties(componentType, Reflection.BINDINGS_PRIVATE_INSTANCE);
    //     FieldInfo[]? privateInstanceFields = Reflection.GetFields(componentType, Reflection.BINDINGS_PRIVATE_INSTANCE, true);    //  Ignore backing fields
    //
    //     if (publicInstanceProperties.Length > 0 || publicInstanceFields.Length > 0)
    //     {
    //         var publicGroup = new ColorBlockElement(Color.White);
    //         group.Content.Add(publicGroup);
    //
    //         foreach (PropertyInfo property in publicInstanceProperties)
    //         {
    //             if (property.PropertyType.IsClass && property.PropertyType != typeof(string) && depth < maxDepth)
    //             {
    //                 BuildInspectorView(publicGroup, property.GetValue(component)!, depth);
    //             }
    //             else
    //             {
    //                 publicGroup.Content.Add(PropertyViewFactory(component, property));
    //             }
    //         }
    //
    //         foreach (FieldInfo field in publicInstanceFields)
    //         {
    //             if (field.FieldType.IsClass && field.FieldType != typeof(string) && depth < maxDepth)
    //             {
    //                 BuildInspectorView(publicGroup, field.GetValue(component)!, depth);
    //             }
    //             else
    //             {
    //                 publicGroup.Content.Add(FieldViewFactory(component, field));
    //             }
    //         }
    //     }
    //
    //     if (publicStaticProperties.Length > 0 || publicStaticFields.Length > 0)
    //     {
    //         var staticBlock = new ColorBlockElement(Color.CornflowerBlue);
    //         group.Content.Add(staticBlock);
    //
    //         staticBlock.Content.Add(new TitleBarElement("Static Members", false, ConstraintAnchor.TOP_CENTER));
    //
    //         foreach (PropertyInfo property in publicStaticProperties)
    //         {
    //             if (property.PropertyType.IsClass && property.PropertyType != typeof(string) && depth < maxDepth)
    //             {
    //                 BuildInspectorView(staticBlock, property.GetValue(component)!, depth);
    //             }
    //             else
    //             {
    //                 staticBlock.Content.Add(PropertyViewFactory(component, property));
    //             }
    //         }
    //
    //         foreach (FieldInfo field in publicStaticFields)  //  Ignore backing fields
    //         {
    //             if (field.FieldType.IsClass && field.FieldType != typeof(string) && depth < maxDepth)
    //             {
    //                 BuildInspectorView(staticBlock, field.GetValue(component)!, depth);
    //             }
    //             else
    //             {
    //                 staticBlock.Content.Add(FieldViewFactory(component, field));
    //             }
    //         }
    //     }
    //
    //     if (privateInstanceProperties.Length > 0 || privateInstanceFields.Length > 0)
    //     {
    //         var privateBlock = new ColorBlockElement(Color.SlateGray);
    //         group.Content.Add(privateBlock);
    //
    //         privateBlock.Content.Add(new TitleBarElement("Members (private)", false, ConstraintAnchor.TOP_CENTER));
    //
    //         foreach (PropertyInfo property in privateInstanceProperties)
    //         {
    //             if (property.PropertyType.IsClass && property.PropertyType != typeof(string) && depth < maxDepth)
    //             {
    //                 BuildInspectorView(privateBlock, property.GetValue(component)!, depth);
    //             }
    //             else
    //             {
    //                 privateBlock.Content.Add(PropertyViewFactory(component, property));
    //             }
    //         }
    //
    //         foreach (FieldInfo field in privateInstanceFields)
    //         {
    //             if (field.FieldType.IsClass && field.FieldType != typeof(string) && depth < maxDepth)
    //             {
    //                 BuildInspectorView(privateBlock, field.GetValue(component)!, depth);
    //             }
    //             else
    //             {
    //                 privateBlock.Content.Add(FieldViewFactory(component, field));
    //             }
    //         }
    //     }
    //
    //     if (privateStaticProperties.Length > 0 || privateStaticFields.Length > 0)
    //     {
    //         var privateStaticBlock = new ColorBlockElement(Color.SteelBlue);
    //         group.Content.Add(privateStaticBlock);
    //
    //         privateStaticBlock.Content.Add(new TitleBarElement("Static Members (private)", false, ConstraintAnchor.TOP_CENTER));
    //
    //         foreach (PropertyInfo property in privateStaticProperties)
    //         {
    //             if (property.PropertyType.IsClass && property.PropertyType != typeof(string) && depth < maxDepth)
    //             {
    //                 BuildInspectorView(privateStaticBlock, property.GetValue(component)!, depth);
    //             }
    //             else
    //             {
    //                 privateStaticBlock.Content.Add(PropertyViewFactory(component, property));
    //             }
    //         }
    //
    //         foreach (FieldInfo field in privateStaticFields)
    //         {
    //             if (field.FieldType.IsClass && field.FieldType != typeof(string) && depth < maxDepth)
    //             {
    //                 BuildInspectorView(privateStaticBlock, field.GetValue(component)!, depth);
    //             }
    //             else
    //             {
    //                 privateStaticBlock.Content.Add(FieldViewFactory(component, field));
    //             }
    //         }
    //     }
    //
    //     contentElement.Content.Add(group);
    // }
    //
    // private static PaneElement FieldViewFactory(object component, FieldInfo field)
    // {
    //     return MemberViewFactory(
    //         field.Name,
    //         field.GetSignature(),
    //         field.GetValue(component),
    //         field.FieldType,
    //         field.IsLiteral | field.IsInitOnly == false
    //     );
    // }
    //
    // private static PaneElement PropertyViewFactory(object component, PropertyInfo property)
    // {
    //     return MemberViewFactory(
    //         property.Name,
    //         property.GetSignature(),
    //         property.GetValue(component),
    //         property.PropertyType,
    //         property.GetSetMethod() != null
    //     );
    // }
    //
    // private static PaneElement MemberViewFactory(string name, string signature, object? value, Type type, bool canWrite)
    // {
    //     return new PaneElement(name.ToTitle())
    //     {
    //         Tooltip = new Tooltip
    //         {
    //             Text = signature,
    //             MaxWidth = 300,
    //         },
    //         Constraints = new RectConstraints
    //         {
    //             Anchor = ConstraintAnchor.TOP_CENTER,
    //             Width = new RelativeConstraint(0.9f),
    //         },
    //         Content = {
    //             new TextElement(value?.ToString() ?? "null") {
    //                 Color = canWrite ? Color.White : Color.Gray,
    //                 Label = type.Name,
    //             },
    //         },
    //     };
    // }
    //
    // public class HierarchySystem : IEntitySystem
    // {
    //     private readonly HashSet<int> _populatedEntities = [];
    //
    //     public void Tick(float delta, DataStore store)
    //     {
    //         store.Query(delta, ForEachEntity);
    //     }
    //
    //     private void ForEachEntity(float delta, DataStore store, int entity)
    //     {
    //         if (_hierarchy == null || !_populatedEntities.Add(entity))
    //         {
    //             return;
    //         }
    //
    //         //  TODO handle removed entities
    //         string? displayName = store.TryGet(entity, out IdentifierComponent identifier) ? identifier.Name : $"<entity:{entity}>";
    //         _hierarchy.Content.Add(new DataTreeNode<Entity>(displayName, new Entity(entity, store)));
    //     }
    // }

    private float _cameraSpeedModifier = 1f;
    private void OnUpdate(double delta)
    {
        const float mouseSensitivity = 0.05f;
        const float cameraBaseSpeed = 10;

        if (_inputService.IsKeyHeld(Key.Shift))
        {
            _cameraSpeedModifier += (float)delta;
        }
        else
        {
            _cameraSpeedModifier = 1f;
        }

        float cameraSpeed = cameraBaseSpeed * _cameraSpeedModifier;

        Camera camera = _renderContext.Camera.Get();

        if (_inputService.IsMouseHeld(MouseButton.Right))
        {
            _inputService.CursorOptions = CursorOptions.Hidden | CursorOptions.Locked;
            Vector2 cursorDelta = _inputService.CursorDelta;
            camera.Transform.Rotate(new Vector3(0, -cursorDelta.X, 0) * mouseSensitivity, false);
            camera.Transform.Rotate(new Vector3(-cursorDelta.Y, 0, 0) * mouseSensitivity, true);
        }
        else
        {
            _inputService.CursorOptions = CursorOptions.None;
        }

        Vector3 forward = camera.Transform.GetForward();
        Vector3 right = camera.Transform.GetRight();

        if (_inputService.IsKeyHeld(Key.W))
        {
            camera.Transform.Translate(-forward * cameraSpeed * (float)delta);
        }

        if (_inputService.IsKeyHeld(Key.S))
        {
            camera.Transform.Translate(forward * cameraSpeed * (float)delta);
        }

        if (_inputService.IsKeyHeld(Key.D))
        {
            camera.Transform.Translate(right * cameraSpeed * (float)delta);
        }

        if (_inputService.IsKeyHeld(Key.A))
        {
            camera.Transform.Translate(-right * cameraSpeed * (float)delta);
        }

        if (_inputService.IsKeyHeld(Key.E))
        {
            camera.Transform.Translate(new Vector3(0, cameraSpeed * (float)delta, 0));
        }

        if (_inputService.IsKeyHeld(Key.Q))
        {
            camera.Transform.Translate(new Vector3(0, -cameraSpeed * (float)delta, 0));
        }

        if (_inputService.IsKeyPressed(Key.UpArrow))
        {
            camera.Transform.Translate(new Vector3(0, 1, 0));
        }

        if (_inputService.IsKeyPressed(Key.DownArrow))
        {
            camera.Transform.Translate(new Vector3(0, -1, 0));
        }
    }
}