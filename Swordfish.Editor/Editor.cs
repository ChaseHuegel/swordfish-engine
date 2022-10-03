using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using ImGuiNET;
using Ninject;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using Swordfish.Library.Reflection;
using Swordfish.Library.Types.Constraints;
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
                    var group = new PaneElement(componentType.Name)
                    {
                        Constraints = {
                            Width = new FillConstraint()
                        }
                    };

                    BindingFlags publicFlags = BindingFlags.Instance | BindingFlags.Public;
                    BindingFlags publicStaticFlags = BindingFlags.Static | BindingFlags.Public;
                    BindingFlags privateFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                    BindingFlags privateStaticFlags = BindingFlags.Static | BindingFlags.NonPublic;

                    foreach (var property in componentType.GetProperties(publicFlags))
                        group.Content.Add(PropertyViewFactory(component, property));

                    foreach (var field in componentType.GetFields(publicFlags))
                        group.Content.Add(FieldViewFactory(component, field));

                    var staticBlock = new ColorBlockElement(Color.CornflowerBlue);
                    group.Content.Add(staticBlock);

                    foreach (var property in componentType.GetProperties(publicStaticFlags))
                        staticBlock.Content.Add(PropertyViewFactory(component, property));

                    foreach (var field in componentType.GetFields(publicStaticFlags).Where<FieldInfo>(x => x.Name[0] != '<'))  //  Ignore backing fields
                        staticBlock.Content.Add(FieldViewFactory(component, field));

                    var privateBlock = new ColorBlockElement(Color.SlateGray);
                    group.Content.Add(privateBlock);

                    foreach (var property in componentType.GetProperties(privateFlags))
                        privateBlock.Content.Add(PropertyViewFactory(component, property));

                    foreach (var field in componentType.GetFields(privateFlags).Where<FieldInfo>(x => x.Name[0] != '<'))  //  Ignore backing fields
                        privateBlock.Content.Add(FieldViewFactory(component, field));

                    var privateStaticBlock = new ColorBlockElement(Color.RoyalBlue);
                    group.Content.Add(privateStaticBlock);

                    foreach (var property in componentType.GetProperties(privateStaticFlags))
                        privateStaticBlock.Content.Add(PropertyViewFactory(component, property));

                    foreach (var field in componentType.GetFields(privateStaticFlags).Where<FieldInfo>(x => x.Name[0] != '<'))  //  Ignore backing fields
                        privateStaticBlock.Content.Add(FieldViewFactory(component, field));

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
                                new TextElement((fileInfo.Length / 1024).ToString() + " kb")
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
        return new PaneElement(field.Name)
        {
            Enabled = field.IsPublic,
            Tooltip = new Tooltip
            {
                Text = field.GetSignature(),
                MaxWidth = 300
            },
            Constraints = new RectConstraints
            {
                Width = new FillConstraint()
            },
            Content = {
                new TextElement(field.GetValue(component)?.ToString() ?? "null") {
                    Label = field.FieldType.Name,
                    Color = !field.IsLiteral && !field.IsInitOnly ? Color.White : Color.Gray
                }
            }
        };
    }

    private static PaneElement PropertyViewFactory(object component, PropertyInfo property)
    {
        return new PaneElement(property.Name)
        {
            Enabled = property.GetAccessors().Any(x => x.IsPublic),
            Tooltip = new Tooltip
            {
                Text = property.GetSignature(),
                MaxWidth = 300
            },
            Constraints = new RectConstraints
            {
                Width = new FillConstraint()
            },
            Content = {
                new TextElement(property.GetValue(component)?.ToString() ?? "null") {
                    Label = property.PropertyType.Name,
                    Color = property.GetSetMethod() != null ? Color.White : Color.Gray
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