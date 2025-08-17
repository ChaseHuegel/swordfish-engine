using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace Reef;

public sealed class UIBuilder<TTextureData>
{
    public struct Scope(UIBuilder<TTextureData> ui) : IDisposable
    {
        private int _disposed;
        
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1 || !ui._hasOpenElement)
            {
                return;
            }

            ui.CloseElement();
        }
    }

    public Vector4 BackgroundColor
    {
        get => _currentElement.Style.BackgroundColor;
        set => _currentElement.Style.BackgroundColor = value;
    }

    public Constraints Constraints
    {
        get => _currentElement.Constraints;
        set => _currentElement.Constraints = value;
    }

    public int Spacing
    {
        get => _currentElement.Layout.Spacing;
        set => _currentElement.Layout.Spacing = value;
    }

    public Padding Padding
    {
        get => _currentElement.Style.Padding;
        set => _currentElement.Style.Padding = value;
    }

    public LayoutDirection LayoutDirection
    {
        get => _currentElement.Layout.Direction;
        set => _currentElement.Layout.Direction = value;
    }
    
    public int Width => _viewPort.Size.X;
    public int Height => _viewPort.Size.Y;

    private readonly IntRect _viewPort;
    
    private bool _hasOpenElement;
    private UIElement _currentElement;
    private readonly Stack<UIElement> _openElements = new();
    private readonly List<UIElement> _closedRootElements = [];

    public UIBuilder(int width, int height)
    {
        _viewPort = new IntRect(left: 0, top: 0, size: new IntVector2(width, height));
    }

    public Scope Element()
    {
        return OpenElement(new UIElement());
    }
    
    public Scope Element(UIElement element)
    {
        return OpenElement(element);
    }
    
    public RenderCommand<TTextureData>[] Build()
    {
        //  Force close all elements
        while (_hasOpenElement)
        {
            CloseElement();
        }
        
        //  Perform a top-down sizing pass to process fill elements
        for (var i = 0; i < _closedRootElements.Count; i++)
        {
            UIElement root = _closedRootElements[i];
            FillChildren(ref root);
            _closedRootElements[i] = root;

            for (var n = 0; root.Children != null && n < root.Children.Count; n++)
            {
                UIElement child = root.Children[n];
                FillChildren(ref child);
                root.Children[n] = child;
            }
        }

        //  Create render commands out of all elements,
        //  performing a top-down positioning pass in the process.
        List<RenderCommand<TTextureData>> commands = [];
        for (var i = 0; i < _closedRootElements.Count; i++)
        {
            UIElement root = _closedRootElements[i];
            
            int x = root.Constraints.X?.Calculate(Width) ?? root.Rect.Position.X;
            int y = root.Constraints.Y?.Calculate(Height) ?? root.Rect.Position.Y;
            
            //  Apply anchoring. Top Left is default, so only need to apply Center/Right/Bottom.
            Anchors anchors = root.Constraints.Anchors;
            int xOffset = root.Rect.Size.X;
            int yOffset = root.Rect.Size.Y;
            
            if ((anchors & Anchors.Right) == Anchors.Right)
            {
                x -= xOffset;
            }
            else if ((anchors & Anchors.Center) == Anchors.Center)
            {
                x -= xOffset >> 1;
            }
            
            if ((anchors & Anchors.Bottom) == Anchors.Bottom)
            {
                y -= yOffset;
            }
            else if ((anchors & Anchors.Center) == Anchors.Center)
            {
                y -= yOffset >> 1;
            }
            
            var center = new IntVector2(x, y);
            root.Rect = new IntRect(center, root.Rect.Size);

            var command = new RenderCommand<TTextureData>
            {
                Rect = root.Rect,
                Color = root.Style.BackgroundColor,
            };
            commands.Add(command);

            int leftOffset = root.Style.Padding.Left;
            int topOffset = root.Style.Padding.Top;
            for (var n = 0; root.Children != null && n < root.Children.Count; n++)
            {
                UIElement child = root.Children[n];

                int constrainedX = child.Constraints.X?.Calculate(root.Rect.Size.X) ?? child.Rect.Position.X;
                int constrainedY = child.Constraints.Y?.Calculate(root.Rect.Size.Y) ?? child.Rect.Position.Y;
                x = root.Rect.Position.X + constrainedX + leftOffset;
                y = root.Rect.Position.Y + constrainedY + topOffset;
                
                //  Apply anchoring. Top Left is default, so only need to apply Center/Right/Bottom.
                anchors = child.Constraints.Anchors;
                xOffset = child.Rect.Size.X;
                yOffset = child.Rect.Size.Y;
            
                if ((anchors & Anchors.Right) == Anchors.Right)
                {
                    x -= xOffset;
                }
                else if ((anchors & Anchors.Center) == Anchors.Center)
                {
                    x -= xOffset >> 1;
                }
            
                if ((anchors & Anchors.Bottom) == Anchors.Bottom)
                {
                    y -= yOffset;
                }
                else if ((anchors & Anchors.Center) == Anchors.Center)
                {
                    y -= yOffset >> 1;
                }
                
                center = new IntVector2(x, y);
                child.Rect = new IntRect(center, child.Rect.Size);

                switch (root.Layout.Direction)
                {
                    case LayoutDirection.Horizontal:
                        leftOffset += child.Rect.Size.X + root.Layout.Spacing;
                        break;
                    case LayoutDirection.Vertical:
                        topOffset += child.Rect.Size.Y + root.Layout.Spacing;
                        break;
                    case LayoutDirection.None:
                        //  Do nothing
                        break;
                }

                command = new RenderCommand<TTextureData>
                {
                    Rect = child.Rect,
                    Color = child.Style.BackgroundColor,
                };
                commands.Add(command);
            }
        }

        //  Reset state
        _openElements.Clear();
        _closedRootElements.Clear();
        return commands.ToArray();
    }

    private Scope OpenElement(UIElement element)
    {
        if (_hasOpenElement)
        {
            _openElements.Push(_currentElement);
            _currentElement = element;
            return new Scope(this);
        }
        
        _hasOpenElement = true;
        _currentElement = element;
        return new Scope(this);
    }
    
    /// <summary>
    ///     Closes the currently open element to complete it,
    ///     performing a bottom-up sizing pass in the process.
    /// </summary>
    private void CloseElement()
    {
        UIElement element = _currentElement;
        
        //  Attempt to get the parent, if any, off the stack
        UIElement parent = default;
        _hasOpenElement = _openElements.Count > 0;
        if (_hasOpenElement)
        {
            parent = _openElements.Pop();
        }
        
        //  Apply size constraints
        int maxWidth = _hasOpenElement ? parent.Rect.Size.X : Width;
        int maxHeight = _hasOpenElement ? parent.Rect.Size.Y : Height;
        int width = element.Constraints.Width?.Calculate(maxWidth) ?? element.Rect.Size.X;
        int height = element.Constraints.Height?.Calculate(maxHeight) ?? element.Rect.Size.Y;
        var size = new IntVector2(width, height);
        
        element.Rect = new IntRect(element.Rect.Position, size);
        
        //  Apply padding
        Padding padding = element.Style.Padding;
        width = element.Rect.Size.X + padding.Left + padding.Right;
        height = element.Rect.Size.Y + padding.Top + padding.Bottom;
        
        //  Calculate spacing
        int childCount = element.Children?.Count ?? 0;
        int totalSpacing = childCount > 1 ? (childCount - 1) * element.Layout.Spacing : 0;

        //  Apply spacing of children to the element
        switch (element.Layout.Direction)
        {
            case LayoutDirection.Horizontal:
                width += totalSpacing;
                break;
            case LayoutDirection.Vertical:
                height += totalSpacing;
                break;
        }

        size = new IntVector2(width, height);
        element.Rect = new IntRect(element.Rect.Position, size);

        //  Resize parent to fit its children
        switch (parent.Layout.Direction)
        {
            case LayoutDirection.Horizontal:
                width = parent.Rect.Size.X + element.Rect.Size.X;
                height = Math.Max(parent.Rect.Size.Y, element.Rect.Size.Y);
                break;
            case LayoutDirection.Vertical:
                width = Math.Max(parent.Rect.Size.X, element.Rect.Size.X);
                height = parent.Rect.Size.Y + element.Rect.Size.Y;
                break;
            case LayoutDirection.None:
                width = parent.Rect.Size.X + element.Rect.Size.X;
                height = parent.Rect.Size.Y + element.Rect.Size.Y;
                break;
        }

        size = new IntVector2(width, height);
        parent.Rect = new IntRect(parent.Rect.Position, size);

        if (_hasOpenElement)
        {
            parent.Children ??= [];
            parent.Children.Add(element);
            _currentElement = parent;
        }
        else
        {
            _closedRootElements.Add(element);
            _currentElement = default;
        }
    }

    private void FillChildren(ref UIElement parent)
    {
        int availableWidth = parent.Rect.Size.X;
        int availableHeight = parent.Rect.Size.Y;
        
        availableWidth -= parent.Style.Padding.Left + parent.Style.Padding.Right;
        availableHeight -= parent.Style.Padding.Top + parent.Style.Padding.Bottom;

        //  Calculate available space
        for (var i = 0; parent.Children != null && i < parent.Children.Count; i++)
        {
            UIElement child = parent.Children[i];
            availableWidth -= child.Rect.Size.X;
        }
        
        int childCount = parent.Children?.Count ?? 0;
        int totalSpacing = childCount > 1 ? (childCount - 1) * parent.Layout.Spacing : 0;
        availableWidth -= totalSpacing;
        
        //  Distribute the available space
        while (availableWidth > 0 && parent.Children != null)
        {
            int smallestWidth = -1;
            int secondSmallestWidth = int.MaxValue;
            int widthToAdd = availableWidth;

            int fillChildrenCount = 0;
            for (var i = 0; i < parent.Children.Count; i++)
            {
                UIElement child = parent.Children[i];
                if (child.Constraints.Width is not Fill)
                {
                    continue;
                }

                if (smallestWidth == -1)
                {
                    smallestWidth = child.Rect.Size.X;
                }

                fillChildrenCount++;

                if (child.Rect.Size.X < smallestWidth)
                {
                    secondSmallestWidth = smallestWidth;
                    smallestWidth = child.Rect.Size.X;
                }
                else if (child.Rect.Size.X > smallestWidth)
                {
                    secondSmallestWidth = Math.Min(secondSmallestWidth, child.Rect.Size.X);
                    widthToAdd = secondSmallestWidth - smallestWidth;
                }
            }

            widthToAdd = Math.Min(widthToAdd, availableWidth / fillChildrenCount);
            widthToAdd = Math.Max(widthToAdd, 1);
            
            for (var i = 0; i < parent.Children.Count; i++)
            {
                UIElement child = parent.Children[i];
                if (child.Rect.Size.X != smallestWidth || child.Constraints.Width is not Fill)
                {
                    continue;
                }
                
                int width = child.Rect.Size.X + widthToAdd;
                int height = child.Rect.Size.Y + (child.Constraints.Height is Fill ? availableHeight : 0);

                var size = new IntVector2(width, height);
                child.Rect = new IntRect(child.Rect.Position, size);

                //  Update the child
                parent.Children[i] = child;
                availableWidth -= widthToAdd;
            }
            availableHeight = 0;
        }
    }
}