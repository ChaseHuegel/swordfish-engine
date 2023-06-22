using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using ImGuiNET;
using Swordfish.ECS;
using Swordfish.Editor.UI;
using Swordfish.Extensibility;
using Swordfish.Graphics;
using Swordfish.Library.Constraints;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Reflection;
using Swordfish.Library.Types;
using Swordfish.Types.Constraints;
using Swordfish.UI.Elements;

using Debugger = Swordfish.Library.Diagnostics.Debugger;
using Path = Swordfish.Library.IO.Path;

namespace Swordfish.Editor;

public class Editor : Plugin
{
    private const ImGuiWindowFlags EDITOR_CANVAS_FLAGS = ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoCollapse;

    public override string Name => "Swordfish Editor";
    public override string Description => "Visual editor for the Swordfish engine.";

    private IWindowContext WindowContext;
    private static IECSContext ECSContext;
    private static IFileService FileService;
    private static IPathService PathService;

    private static CanvasElement Hierarchy;

    private Action FileWrite;

    public Editor(IWindowContext windowContext, IFileService fileService, IECSContext ecsContext, IPathService pathService)
    {
        WindowContext = windowContext;
        FileService = fileService;
        ECSContext = ecsContext;
        PathService = pathService;

        ECSContext.BindSystem<HierarchySystem>();

        Hierarchy = new CanvasElement("Hierarchy")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            Constraints = new RectConstraints
            {
                Width = new RelativeConstraint(0.15f),
                Height = new RelativeConstraint(0.8f)
            }
        };
    }

    public override void Start()
    {
        WindowContext.Maximize();

        Shortcut exitShortcut = new(
            "Exit",
            "General",
            ShortcutModifiers.NONE,
            Key.ESC,
            Shortcut.DefaultEnabled,
            WindowContext.Close
        );

        StatsWindow statsWindow = new(WindowContext, ECSContext);

        MenuElement menu = new()
        {
            Content = {
                new MenuItemElement("File") {
                    Content = {
                        new MenuItemElement("New") {
                            Content = {
                                new MenuItemElement("Plugin", new Shortcut(
                                    "New Plugin",
                                    "Editor",
                                    ShortcutModifiers.CONTROL,
                                    Key.N,
                                    Shortcut.DefaultEnabled,
                                    () => {
                                        Debugger.Log("Creating new plugin");
                                        IPath outputPath = LocalPathService.Root
                                            .At("Projects")
                                            .At("New Project")
                                            .At("Source").CreateDirectory();
                                        outputPath = outputPath.At("NewPlugin.cs");
                                        Stream fileToCopy = FileService.Open(new Path("manifest://Templates/NewPlugin.cstemplate"));
                                        FileService.Write(outputPath, fileToCopy);
                                        FileWrite?.Invoke();
                                    }
                                )),
                                new MenuItemElement("Project", new Shortcut(
                                    "New Project",
                                    "Editor",
                                    ShortcutModifiers.CONTROL | ShortcutModifiers.SHIFT,
                                    Key.N,
                                    Shortcut.DefaultEnabled,
                                    () => Debugger.Log("Create new project")
                                )),
                            }
                        },
                        new MenuItemElement("Open", new Shortcut(
                            "Open",
                            "Editor",
                            ShortcutModifiers.CONTROL,
                            Key.O,
                            Shortcut.DefaultEnabled,
                            () => {
                                if (TreeNode.Selected.Get() is DataTreeNode<Path> pathNode)
                                {
                                    Debugger.Log($"Opening {pathNode.Data.Get()}");
                                    pathNode.Data.Get().TryOpenInDefaultApp();
                                }
                            }
                        )),
                        new MenuItemElement("Save", new Shortcut(
                            "Save",
                            "Editor",
                            ShortcutModifiers.CONTROL,
                            Key.S,
                            Shortcut.DefaultEnabled,
                            () => Debugger.Log("Save project")
                        )),
                        new MenuItemElement("Save As", new Shortcut(
                            "Save As",
                            "Editor",
                            ShortcutModifiers.CONTROL | ShortcutModifiers.SHIFT,
                            Key.S,
                            Shortcut.DefaultEnabled,
                            () => Debugger.Log("Save project as")
                        )),
                        new MenuItemElement("Exit", exitShortcut),
                    }
                },
                new MenuItemElement("Edit"),
                new MenuItemElement("View") {
                    Content = {
                        new MenuItemElement("Stats", new Shortcut(
                                "Stats",
                                "Editor",
                                ShortcutModifiers.NONE,
                                Key.F5,
                                Shortcut.DefaultEnabled,
                                () => {
                                    statsWindow.Visible = !statsWindow.Visible;
                                }
                            )
                        )
                    }
                },
                new MenuItemElement("Tools"),
                new MenuItemElement("Run"),
                new MenuItemElement("Help"),
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

        CanvasElement console = new("Console")
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

        foreach (LogEventArgs record in Logger.History)
            PopulateLogLine(null, record);

        Logger.Logged += PopulateLogLine;

        void PopulateLogLine(object? sender, LogEventArgs args)
        {
            console.Content.Add(new TextElement(args.Line)
            {
                Color = args.Type.GetColor()
            });
        }

        CanvasElement assetBrowser = new("Asset Browser")
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

        PopulateDirectory(assetBrowser, PathService.Root.ToString());

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

            if (element is DataTreeNode<Path> node)
            {
                string? path = node.Data.Get().ToString();
                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    removalList.Add(node);
                    return removalList;
                }
            }

            foreach (TreeNode child in element.Content.OfType<DataTreeNode<Path>>())
            {
                removalList.AddRange(RefreshContentRecursively(child));
            }

            return removalList;
        }

        void PopulateDirectory(ContentElement root, string path)
        {
            foreach (string dir in Directory.GetDirectories(path))
            {
                DataTreeNode<Path> node = new(System.IO.Path.GetFileName(dir), new Path(dir));
                PopulateDirectory(node, dir);
                root.Content.Add(node);
            }

            PopulateFiles(root, path);
        }

        void PopulateFiles(ContentElement root, string directory)
        {
            foreach (string file in Directory.GetFiles(directory, "*.*"))
            {
                DataTreeNode<Path> node = new(System.IO.Path.GetFileName(file), new Path(file));
                root.Content.Add(node);
            }

            root.Content.Add(new DividerElement());
        }

        CanvasElement inspector = new("Inspector")
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

                var components = entity.GetComponents();
                foreach (var component in components)
                {
                    if (component == null)
                        continue;

                    BuildInpsectorView(inspector, component);
                }
            }
            else if (args.NewValue is DataTreeNode<Path> pathNode)
            {
                if (!File.Exists(pathNode.Data.Get().OriginalString))
                    return;

                var fileInfo = new FileInfo(pathNode.Data.Get().OriginalString);
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
                                new TextElement(System.IO.Path.GetFileNameWithoutExtension(fileInfo.Name))
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
                                new TextElement(pathNode.Data.Get().OriginalString)
                            }
                        }
                    }
                };

                inspector.Content.Add(group);
            }
        };
    }

    private static void BuildInpsectorView(ContentElement contentElement, object component)
    {
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
                publicGroup.Content.Add(PropertyViewFactory(component, property));

            foreach (var field in publicInstanceFields)
                publicGroup.Content.Add(FieldViewFactory(component, field));
        }

        if (publicStaticProperties.Length > 0 || publicStaticFields.Length > 0)
        {
            var staticBlock = new ColorBlockElement(Color.CornflowerBlue);
            group.Content.Add(staticBlock);

            staticBlock.Content.Add(new TitleBarElement("Static Members", false, ConstraintAnchor.TOP_CENTER));

            foreach (var property in publicStaticProperties)
                staticBlock.Content.Add(PropertyViewFactory(component, property));

            foreach (var field in publicStaticFields)  //  Ignore backing fields
                staticBlock.Content.Add(FieldViewFactory(component, field));
        }

        if (privateInstanceProperties.Length > 0 || privateInstanceFields.Length > 0)
        {
            var privateBlock = new ColorBlockElement(Color.SlateGray);
            group.Content.Add(privateBlock);

            privateBlock.Content.Add(new TitleBarElement("Members (private)", false, ConstraintAnchor.TOP_CENTER));

            foreach (var property in privateInstanceProperties)
                privateBlock.Content.Add(PropertyViewFactory(component, property));

            foreach (var field in privateInstanceFields)
                privateBlock.Content.Add(FieldViewFactory(component, field));
        }

        if (privateStaticProperties.Length > 0 || privateStaticFields.Length > 0)
        {
            var privateStaticBlock = new ColorBlockElement(Color.SteelBlue);
            group.Content.Add(privateStaticBlock);

            privateStaticBlock.Content.Add(new TitleBarElement("Static Members (private)", false, ConstraintAnchor.TOP_CENTER));

            foreach (var property in privateStaticProperties)
                privateStaticBlock.Content.Add(PropertyViewFactory(component, property));

            foreach (var field in privateStaticFields)
                privateStaticBlock.Content.Add(FieldViewFactory(component, field));
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

    [ComponentSystem]
    public class HierarchySystem : ComponentSystem
    {
        private bool Populate = false;

        protected override void Update(Entity entity, float deltaTime)
        {
            if (Populate)
                Hierarchy.Content.Add(new DataTreeNode<Entity>(entity.GetComponent<IdentifierComponent>()?.Name, entity));
        }

        protected override void OnModified()
        {
            Hierarchy.Content.Clear();
            Populate = true;
        }

        protected override void OnUpdated()
        {
            if (Hierarchy != null)
                Populate = false;
        }
    }
}