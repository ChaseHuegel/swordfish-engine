using System.Numerics;
using ImGuiNET;
using Swordfish.Library.Constraints;

namespace Swordfish.Types.Constraints;

public class RectConstraints
{
    public Vector2 Max { get; set; }

    public ConstraintAnchor Anchor { get; set; }

    public IConstraint? X { get; set; }
    public IConstraint? Y { get; set; }

    public IConstraint? Width { get; set; }
    public IConstraint? Height { get; set; }

    public Vector2 GetPosition(float anchorScale = 0.5f)
    {
        float yValue, xValue;

        if (X is AspectConstraint)
        {
            yValue = Y?.GetValue(Max.Y) ?? 0f;
            xValue = X?.GetValue(yValue) ?? 0f;
        }
        else if (Y is AspectConstraint)
        {
            xValue = X?.GetValue(Max.X) ?? 0f;
            yValue = Y?.GetValue(xValue) ?? 0f;
        }
        else
        {
            xValue = X?.GetValue(Max.X) ?? 0f;
            yValue = Y?.GetValue(Max.Y) ?? 0f;
        }

        if (Anchor != ConstraintAnchor.TOP_LEFT)
        {
            Vector2 dimensions = GetDimensions();
            switch (Anchor)
            {
                case ConstraintAnchor.TOP_CENTER:
                    xValue += (Max.X - dimensions.X) * anchorScale;
                    break;
                case ConstraintAnchor.TOP_RIGHT:
                    xValue = Max.X - dimensions.X - xValue;
                    break;
                case ConstraintAnchor.CENTER_LEFT:
                    yValue += (Max.Y - dimensions.Y) * anchorScale;
                    break;
                case ConstraintAnchor.CENTER:
                    xValue += (Max.X - dimensions.X) * anchorScale;
                    yValue += (Max.Y - dimensions.Y) * anchorScale;
                    break;
                case ConstraintAnchor.CENTER_RIGHT:
                    xValue = Max.X - dimensions.X - xValue;
                    yValue += (Max.Y - dimensions.Y) * anchorScale;
                    break;
                case ConstraintAnchor.BOTTOM_LEFT:
                    yValue = Max.Y - dimensions.Y - yValue;
                    break;
                case ConstraintAnchor.BOTTOM_CENTER:
                    xValue += (Max.X - dimensions.X) * anchorScale;
                    yValue = Max.Y - dimensions.Y - yValue;
                    break;
                case ConstraintAnchor.BOTTOM_RIGHT:
                    xValue = Max.X - dimensions.X - xValue;
                    yValue = Max.Y - dimensions.Y - yValue;
                    break;
            }
        }

        return new Vector2(xValue, yValue);
    }

    public Vector2 GetDimensions()
    {
        float yValue, xValue;

        if (Width is AspectConstraint)
        {
            yValue = Height?.GetValue(Max.Y) ?? 0f;
            xValue = Width?.GetValue(yValue) ?? 0f;
        }
        else if (Height is AspectConstraint)
        {
            xValue = Width?.GetValue(Max.X) ?? 0f;
            yValue = Height?.GetValue(xValue) ?? 0f;
        }
        else
        {
            xValue = Width switch
            {
                FillConstraint _ => ImGui.GetContentRegionAvail().X,
                _ => Width?.GetValue(Max.X) ?? 0f,
            };

            yValue = Height switch
            {
                FillConstraint _ => ImGui.GetContentRegionAvail().Y,
                _ => Height?.GetValue(Max.Y) ?? 0f,
            };
        }

        return new Vector2(xValue, yValue);
    }
}
