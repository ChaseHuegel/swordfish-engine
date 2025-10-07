using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;

namespace WaywardBeyond.Client.Core.UI;

internal static class Widgets
{
    public static bool TextButton<T>(this UIBuilder<T> ui, string id, string text, FontOptions fontOptions)
    {
        using (ui.Element(id))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
            };
            
            bool clicked = ui.Clicked();
            bool hovering = ui.Hovering();
            using (ui.Text(text))
            {
                ui.FontOptions = fontOptions;

                if (clicked)
                {
                    ui.Color = new Vector4(0f, 0f, 0f, 1f);
                }
                else if (hovering)
                {
                    ui.Color = new Vector4(1f, 1f, 1f, 1f);
                }
                else
                {
                    ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                }
            }

            return clicked;
        }
    }

    public static bool Checkbox<T>(this UIBuilder<T> ui, string id, string text, bool isChecked)
    {
        const string checkedUnicode = "\uf14a";
        const string uncheckedUnicode = "\uf0c8";
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
            };
            
            using (ui.Text(text))
            {
                ui.FontSize = 20;
                ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Left,
                    Y = new Relative(0.5f),
                };
            }

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }

            using (ui.Element(id))
            {
                bool clicked = ui.Clicked();
                bool hovering = ui.Hovering();
                
                using (ui.Text(isChecked ? checkedUnicode : uncheckedUnicode, fontID: "Font Awesome 6 Free Regular"))
                {
                    ui.FontSize = 30;
                    
                    //  TODO swordfish#233 For some reason some FA glyphs are rendering outside of their bounds
                    ui.Padding = new Padding
                    {
                        Right = 4,
                        Bottom = 12,
                    };
                    
                    if (clicked)
                    {
                        ui.Color = new Vector4(0f, 0f, 0f, 1f);
                        isChecked = !isChecked;
                    }
                    else if (hovering)
                    {
                        ui.Color = new Vector4(1f, 1f, 1f, 1f);
                    }
                    else
                    {
                        ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                    }
                }
            }
        }

        return isChecked;
    }
}