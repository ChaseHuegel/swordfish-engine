using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using ImGuiNET;
using Ninject;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Library.Constraints;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Reflection;
using Swordfish.Library.Types;
using Swordfish.Types.Constraints;
using Swordfish.UI.Elements;
using Path = Swordfish.Library.IO.Path;

namespace Swordfish.Editor;

public class Editor : Plugin
{
    private const ImGuiWindowFlags EDITOR_CANVAS_FLAGS = ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoCollapse;

    public override string Name => "Swordfish Editor";
    public override string Description => "Visual editor for the Swordfish engine.";

    private static IECSContext? ECSContext;
    private static CanvasElement? Hierarchy;

    public override void Load()
    {
        SwordfishEngine.MainWindow.Maximize();

        ECSContext = SwordfishEngine.Kernel.Get<IECSContext>();
        ECSContext.BindSystem<HierarchySystem>();

        MenuElement menu = new()
        {
            Content = {
                new MenuItemElement("File") {
                    Content = {
                        new MenuItemElement("New") {
                            Content = {
                                new MenuItemElement("Project"),
                                new MenuItemElement("Plugin"),
                            }
                        },
                        new MenuItemElement("Open"),
                        new MenuItemElement("Save"),
                    }
                },
                new MenuItemElement("Edit"),
                new MenuItemElement("View"),
                new MenuItemElement("Tools"),
                new MenuItemElement("Run"),
                new MenuItemElement("Help"),
                new TextElement("Swordfish Engine " + SwordfishEngine.Version) {
                    Constraints = new RectConstraints() {
                        Anchor = ConstraintAnchor.TOP_RIGHT
                    }
                }
            }
        };

        Hierarchy = new CanvasElement("Hierarchy")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            Constraints = new RectConstraints
            {
                Width = new RelativeConstraint(0.15f),
                Height = new RelativeConstraint(0.8f)
            }
        };

        CanvasElement console = new("Console")
        {
            Flags = EDITOR_CANVAS_FLAGS,
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

        Debugger.Log("This is a NONE log", LogType.NONE);
        Debugger.Log("This is a CONTINUED log", LogType.CONTINUED);
        Debugger.Log("This is a INFO log", LogType.INFO);
        Debugger.Log("This is a WARNING log", LogType.WARNING);
        Debugger.Log("This is a ERROR log", LogType.ERROR);

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

        PopulateDirectory(assetBrowser, SwordfishEngine.Kernel.Get<IPathService>().Root.OriginalString);

        void PopulateDirectory(ContentElement root, string path)
        {
            foreach (string dir in Directory.GetDirectories(path))
            {
                TreeNode node = new(System.IO.Path.GetFileName(dir));
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

            if (TreeNode.Selected.Get() is DataTreeNode<Entity> entityNode)
            {
                var components = entityNode.Data.Get().GetComponents();
                foreach (var component in components)
                {
                    if (component == null)
                        continue;

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
                        foreach (var property in publicInstanceProperties)
                            group.Content.Add(PropertyViewFactory(component, property));

                        foreach (var field in publicInstanceFields)
                            group.Content.Add(FieldViewFactory(component, field));
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

                    inspector.Content.Add(group);
                }
            }
            else if (TreeNode.Selected.Get() is DataTreeNode<Path> pathNode)
            {
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

    public override void Initialize()
    {
        //  do nothing
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
                Hierarchy?.Content.Add(new DataTreeNode<Entity>(entity.GetComponent<IdentifierComponent>()?.Name, entity));
        }

        protected override void OnModified()
        {
            Hierarchy?.Content.Clear();
            Populate = true;
        }

        protected override void OnUpdated()
        {
            Populate = false;
        }
    }
}