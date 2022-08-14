using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Xml;
using System.Xml.Serialization;
using ImGuiNET;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Swordfish.Engine;
using Swordfish.Engine.ECS;
using Swordfish.Engine.Rendering;
using Swordfish.Engine.Rendering.UI.Models;
using Swordfish.Engine.Types;
using Swordfish.Library.Diagnostics;

namespace Swordfish.Demo
{
    public static class DemoUI
    {
        public static void Create()
        {
            Canvas canvas = new Canvas("Test Canvas") {
                Flags = ImGuiWindowFlags.MenuBar,
                TryLoadLayout = true,
                Tooltip = {
                    Text = "This canvas has a tooltip."
                },
                ContentSeparator = ContentSeparator.None
            };

            canvas.Content.Add(new Menu("Menu") {
                Items = {
                    new MenuItem("File") {
                        Tooltip = {
                            Text = "A file menu."
                        },
                        Items = {
                            new MenuItem("New") {
                                Shortcut = new Shortcut {
                                    Modifiers = ShortcutModifiers.Ctrl,
                                    Key = Keys.N
                                },
                                Clicked = (sender, e) => Debug.Log("File > New clicked!")
                            },
                            new MenuItem("Import") {
                                Clicked = (sender, e) => Debug.Log("File > Import clicked!")
                            },
                            new MenuItem("Export") {
                                Enabled = false,
                                Clicked = (sender, e) => Debug.Log("File > Export clicked!")
                            },
                            new MenuItem("More") {
                                Enabled = false,
                                Items = {
                                    new MenuItem("Nested"),
                                    new MenuItem("Buttons"),
                                    new MenuItem("In Here"),
                                }
                            },
                            new MenuItem("Save") {
                                Shortcut = new Shortcut {
                                    Modifiers = ShortcutModifiers.Ctrl,
                                    Key = Keys.S
                                },
                                Clicked = (sender, e) => Debug.Log("File > Save clicked!")
                            },
                            new MenuItem("Save As") {
                                Shortcut = new Shortcut {
                                    Modifiers = ShortcutModifiers.Ctrl | ShortcutModifiers.Shift,
                                    Key = Keys.S
                                },
                                Clicked = (sender, e) => Debug.Log("File > Save As clicked!")
                            }
                        }
                    },
                    new MenuItem("Test") {
                        Items = {
                            new MenuItem("Button") {
                                Value = true
                            },
                            new MenuItem("Do Things") {
                                Value = true
                            },
                            new MenuItem("More") {
                                Items = {
                                    new MenuItem("Nested"),
                                    new MenuItem("Buttons"),
                                    new MenuItem("In Here"),
                                }
                            },
                            new MenuItem("Do Stuff")
                        }
                    }
                }
            });

            canvas.Content.Add(new Foldout("Canvas Flags") {
                Content = {
                    new CheckboxFlags(typeof(ImGuiWindowFlags)) {
                        Value = (int)canvas.Flags,
                        ValueChanged = (sender, e) => {
                            canvas.Flags = (ImGuiWindowFlags)((CheckboxFlags)sender).Value;
                        }
                    }
                }
            });

            canvas.Content.Add(
                new Panel("My Panel") {
                    Size = new Vector2(100, 100),
                    Content = {
                        new Text("This is a label.") {
                            Tooltip = {
                                Text = "My label has a long tooltip that will wrap automatically, kind of neat!"
                            }
                        }
                    }
                }
            );

            canvas.Content.Add(
                new Group {
                    Alignment = Layout.Horizontal,
                    Content = {
                        new Text("This is a horizontal aligned Group with horizontal and a vertical LayoutGroups."),
                        new LayoutGroup {
                            Layout = Layout.Vertical,
                            Content = {
                                new Text("1"),
                                new Text("2"),
                                new Text("3"),
                                new Text("4"),
                                new Text("5")
                            }
                        },
                        new LayoutGroup {
                            Layout = Layout.Horizontal,
                            Content = {
                                new Text("1"),
                                new Text("2"),
                                new Text("3"),
                                new Text("4"),
                                new Text("5")
                            }
                        }
                    }
                }
            );

            canvas.Content.Add(
                new Foldout("A foldout") {
                    Tooltip = {
                        Help = true,
                        Text = "Unhelpful tooltip."
                    },
                    Alignment = Layout.Vertical,
                    Content = {
                        new Text("Text that doesn't wrap at all whatsoever", false),
                        new Text("Simple text that does wrap"),
                        new Text("This is some helpful text") {
                            Tooltip = {
                                Help = true,
                                Text = "Unhelpful tooltip."
                            }
                        },
                        new Text("Some colored text") {
                            Color = Color.Green
                        },
                        new Text("Disabled text") {
                            Enabled = false,
                        },
                        new Text("Label text") {
                            Label = "MyLabel",
                            Tooltip = {
                                Help = true,
                                Text = "A helper on this label!"
                            }
                        },
                        new Text("Colored label text") {
                            Label = "MyLabel",
                            Color = Color.Blue,
                        },
                        new Text("Disabled label text with a tooltip") {
                            Enabled = false,
                            Label = "MyLabel",
                            Tooltip = {
                                Text = "A tooltip on the label?"
                            }
                        },
                    }
                }
            );

            canvas.Content.Add(
                new Panel("Another Panel") {
                    Size = new Vector2(0, 100),
                    Tooltip = {
                        Help = true,
                        Text = "This is a helpful tooltip."
                    },
                    Content = {
                        new Text("This is a vertical aligned label."),
                        new Text("This is a horizontal aligned label.") {
                            Alignment = Layout.Horizontal
                        },
                        new Text("This is a vertical aligned label.")
                    }
                }
            );

            TabMenu tabMenu = new TabMenu("Tab Menu") {
                Size = new Vector2(400, 200)
            };
            canvas.Content.Add(tabMenu);

            tabMenu.Items.Add(new TabMenuItem("LayoutGroup") {
                Tooltip = {
                    Text = "This LayoutGroup has a divider ContentSeparator."
                },
                Content = {
                    new LayoutGroup {
                        Layout = Layout.Vertical,
                        ContentSeparator = ContentSeparator.Divider,
                        Content = {
                            new Text("1"),
                            new Text("2"),
                            new Text("3"),
                            new Text("4"),
                            new Text("5")
                        }
                    }
                }
            });

            tabMenu.Items.Add(new TabMenuItem("Panel LayoutGroup") {
                Content = {
                    new Panel("Panel With Horizontal LayoutGroup") {
                        Content = {
                            new LayoutGroup {
                                Layout = Layout.Horizontal,
                                Content = {
                                    new Text("1"),
                                    new Text("2"),
                                    new Text("3"),
                                    new Text("4"),
                                    new Text("5")
                                }
                            }
                        }
                    }
                }
            });

            tabMenu.Items.Add(new TabMenuItem("ScrollView") {
                Content = {
                    new ScrollView() {
                        Content = {
                            new Text("This is a label, but that is a horizontal LayoutGroup ->"),
                            new LayoutGroup {
                                Layout = Layout.Horizontal,
                                Content = {
                                    new Text("1"),
                                    new Text("2"),
                                    new Text("3"),
                                    new Text("4"),
                                    new Text("5")
                                }
                            },
                            new Text("Label"),
                            new Text("Label"),
                            new Text("- Bullet"),
                            new Text("- Bullet"),
                            new Text("-Bullet"),
                            new Text("-Bullet"),
                        }
                    }
                }
            });

            canvas.Content.Add(
                new TreeNode("Root") {
                    Nodes = {
                        new TreeNode("Node 1") {
                            Nodes = {
                                new TreeNode("Node 1-1") {
                                    Nodes = {
                                        new TreeNode("Node 1-1-1"),
                                        new TreeNode("Node 1-1-2")
                                    }
                                },
                                new TreeNode("Node 1-2")
                            }
                        },
                        new TreeNode("Node 2") {
                            Nodes = {
                                new TreeNode("Node 2-1"),
                                new TreeNode("Node 2-2")
                            }
                        },
                        new TreeNode("Node 3") {
                            Nodes = {
                                new TreeNode("Node 3-1"),
                                new TreeNode("Node 3-2")
                            }
                        }
                    }
                }
            );

            canvas.Content.Add(new Checkbox("Checkbox"));
            canvas.Content.Add(new Checkbox("Checkbox") {
                Tooltip = {
                    Text = "Checkbox tooltip"
                }
            });
            canvas.Content.Add(new Checkbox("Horizontal Checkbox") {
                Alignment = Layout.Horizontal,
                Tooltip = {
                    Text = "Tooltip!",
                    Help = true
                }
            });

            canvas.Content.Add(new Button("Button"));
            canvas.Content.Add(new Button("Big Button") {
                Size = new Vector2(100, 40),
                Tooltip = {
                    Text = "Button toolip"
                }
            });
            canvas.Content.Add(new Button("Helpful Button") {
                Tooltip = {
                    Text = "Helpful toolip!",
                    Help = true
                }
            });
            
            XmlSerializer serializer = new XmlSerializer(typeof(Canvas));
            Directory.CreateDirectory("ui/");
            using (XmlWriter writer = XmlWriter.Create("ui/testcanvas.xml", new XmlWriterSettings {
                Indent = true,
                IndentChars = "\t",
                OmitXmlDeclaration = true
            }))
            {
                serializer.Serialize(writer, canvas);
                writer.Close();
            }

            // XmlSerializer deserializer = new XmlSerializer(typeof(Canvas));
            // using (Stream stream = new FileStream("ui/testcanvas.xml", FileMode.Open))
            // {
            //     Canvas newCanvas = (Canvas)deserializer.Deserialize(stream);
            // }
        }

        private static TreeView EntityTreeView;

        public static void CreateEntityHierarchy()
        {
            Canvas entityCanvas = new Canvas("Entities") {
                TryLoadLayout = false,
                Size = new Vector2(0.16f, 1f),
                SizeBehavior = SizeBehavior.Relative,
                Flags = ImGuiWindowFlags.NoCollapse
                        | ImGuiWindowFlags.HorizontalScrollbar
                        | ImGuiWindowFlags.NoMove
                        | ImGuiWindowFlags.NoResize
                        | ImGuiWindowFlags.MenuBar
            };

            entityCanvas.Content.Add(new Menu {
                Items = {
                    new MenuItem("Create") {
                        Items = {
                            new MenuItem("Cube") {
                                Clicked = (sender, e) => {
                                    OpenTK.Mathematics.Vector3 pos = Camera.Main.transform.position + (Camera.Main.transform.forward * 4);
                                    Demo.CreateEntity(pos , OpenTK.Mathematics.Quaternion.Identity);
                                },
                                Shortcut = {
                                    Modifiers = ShortcutModifiers.Ctrl,
                                    Key = Keys.N
                                }
                            },
                            new MenuItem("Cube Shape") {
                                Clicked = (sender, e) => {
                                    OpenTK.Mathematics.Vector3 pos = Camera.Main.transform.position + (Camera.Main.transform.forward * 4);
                                    Demo.CreateEntityParented(pos , OpenTK.Mathematics.Quaternion.Identity);
                                },
                                Shortcut = {
                                    Modifiers = ShortcutModifiers.Ctrl | ShortcutModifiers.Shift,
                                    Key = Keys.N
                                }
                            },
                            new MenuItem("Point Light") {
                                Clicked = (sender, e) => {
                                    OpenTK.Mathematics.Vector3 pos = Camera.Main.transform.position + (Camera.Main.transform.forward * 4);
                                    Demo.CreatePointLightEntity(pos , Color.Random, 800);
                                },
                                Shortcut = {
                                    Modifiers = ShortcutModifiers.Ctrl,
                                    Key = Keys.L
                                }
                            }
                        }
                    },
                    new MenuItem("Spawn") {
                        Items = {
                            new MenuItem("Cube x100") {
                                Clicked = (sender, e) => Demo.CreateEntityCubes(100)
                            },
                            new MenuItem("Cube x500") {
                                Clicked = (sender, e) => Demo.CreateEntityCubes(500)
                            },
                            new MenuItem("Cube x1000") {
                                Clicked = (sender, e) => Demo.CreateEntityCubes(1000),
                                Shortcut = {
                                    Key = Keys.F6
                                }
                            }
                        }
                    }
                }
            });

            EntityTreeView = new TreeView();
            entityCanvas.Content.Add(EntityTreeView);
            UpdateEntityHiearchy();
        }

        [ComponentSystem(typeof(TransformComponent))]
        public class HiearchySystem : ComponentSystem
        {
            public override void OnPullEntities() => UpdateEntityHiearchy();
        }

        private static void UpdateEntityHiearchy()
        {
            if (EntityTreeView == null)
                return;
            
            lock (EntityTreeView.Nodes)
            {
                EntityTreeView.Nodes.Clear();
                
                foreach (Entity entity in Swordfish.Engine.Swordfish.ECS.Pull(typeof(TransformComponent)))
                {
                    TransformComponent transform = Swordfish.Engine.Swordfish.ECS.Get<TransformComponent>(entity.UID);

                    if (transform.parent == Entity.Null)
                        PopulateTree(entity.UID, EntityTreeView);
                }
            }

            void PopulateTree(int entityID, TreeView treeView)
            {
                TransformComponent transform = Swordfish.Engine.Swordfish.ECS.Get<TransformComponent>(entityID);
                TreeViewNode node = new TreeViewNode {
                    Uid = entityID,
                    Name = $"Entity {entityID}"
                };

                if (transform.children.Count > 0)
                {
                    node.Nodes = new List<TreeViewNode>();
                    foreach (int childID in transform.children)
                        RecursivePopulateTree(childID, node);
                }
                
                treeView.Nodes.Add(node);
            }

            void RecursivePopulateTree(int entityID, TreeViewNode parentNode)
            {
                TransformComponent transform = Swordfish.Engine.Swordfish.ECS.Get<TransformComponent>(entityID);
                TreeViewNode node = new TreeViewNode {
                    Uid = entityID,
                    Name = $"Entity {entityID}",
                    Nodes = new List<TreeViewNode>()
                };

                foreach (int childID in transform.children)
                    RecursivePopulateTree(childID, node);
                
                parentNode.Nodes.Add(node);
            }
        }
    }
}
