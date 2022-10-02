using System.IO;
using System.Linq;
using ImGuiNET;
using Ninject;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
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
                Height = new RelativeConstraint(1f)
            }
        };

        CanvasElement console = new("Console")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            Constraints = new RectConstraints
            {
                X = new RelativeConstraint(0.15f),
                Y = new RelativeConstraint(0.8f),
                Width = new RelativeConstraint(0.4f),
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
                Width = new RelativeConstraint(0.3f),
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
                Width = new RelativeConstraint(0.15f),
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

                    var group = new PaneElement(component.ToString())
                    {
                        Constraints = {
                            Width = new FillConstraint()
                        }
                    };

                    foreach (var field in component.GetType().GetFields())
                    {
                        group.Content.Add(new PaneElement(field.Name)
                        {
                            Tooltip = new Tooltip
                            {
                                Text = field.FieldType.ToString()
                            },
                            Constraints = new RectConstraints()
                            {
                                Width = new FillConstraint()
                            },
                            Content = {
                                new TextElement(field.GetValue(component)?.ToString() ?? "null")
                            }
                        });
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