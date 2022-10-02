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

    public override void Load()
    {
        SwordfishEngine.MainWindow.Maximize();
    }

    public override void Unload()
    {
    }

    public override void Initialize()
    {
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

        CanvasElement heirarchy = new("Hierarchy")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            Constraints = new RectConstraints
            {
                Width = new RelativeConstraint(0.15f),
                Height = new RelativeConstraint(1f)
            }
        };

        var world = new World();
        world.Initialize();
        for (int i = 0; i < 1000; i++)
            world.EntityBuilder.Attach(new IdentifierComponent($"new entity {i}", null)).Build();

        foreach (var entity in world.GetEntities())
            heirarchy.Content.Add(new DataTreeNode<Entity>("Entity " + entity.Ptr.ToString(), entity));

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

        void PopulateLogLine(object sender, LogEventArgs args)
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
                foreach (var component in entityNode.Data.Get().GetComponents().Where(x => x != null))
                {
                    var group = new PanelElement(component.ToString());

                    foreach (var field in component.GetType().GetFields())
                    {
                        group.Content.Add(new TextElement(field.Name + ":")
                        {
                            Constraints = new RectConstraints()
                            {
                                X = new AbsoluteConstraint(10)
                            }
                        });
                        group.Content.Add(new TextElement(field.GetValue(component)?.ToString() ?? "null")
                        {
                            Alignment = ElementAlignment.HORIZONTAL
                        });
                    }

                    group.Content.Add(new DividerElement());
                    inspector.Content.Add(group);
                }
            }
            else if (TreeNode.Selected.Get() is DataTreeNode<Path> pathNode)
            {
                var group = new PanelElement(pathNode.Data.Get().GetType().ToString())
                {
                    Content = {
                        new TextElement("File:")
                        {
                            Constraints = new RectConstraints()
                            {
                                X = new AbsoluteConstraint(10)
                            }
                        },
                        new TextElement(System.IO.Path.GetFileName(pathNode.Data.Get().ToString()))
                        {
                            Alignment = ElementAlignment.HORIZONTAL
                        },
                        new TextElement("Path:")
                        {
                            Constraints = new RectConstraints()
                            {
                                X = new AbsoluteConstraint(10)
                            }
                        },
                        new TextElement(pathNode.Data.Get().ToString())
                        {
                            Alignment = ElementAlignment.HORIZONTAL
                        }
                    }
                };

                group.Content.Add(new DividerElement());
                inspector.Content.Add(group);
            }
        };
    }
}