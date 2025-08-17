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

    private bool _hasOpenElement;
    private UIElement _currentElement;
    private readonly Stack<UIElement> _openElements = new();
    private readonly List<UIElement> _closedRootElements = [];

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

        List<RenderCommand<TTextureData>> commands = [];
        foreach (UIElement root in _closedRootElements)
        {
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
                
                int x = root.Rect.Position.X + child.Rect.Position.X + leftOffset;
                int y = root.Rect.Position.Y + child.Rect.Position.Y + topOffset;
                var center = new IntVector2(x, y);
        
                int width = child.Rect.Size.X;
                int height = child.Rect.Size.Y;
                var size = new IntVector2(width, height);
        
                child.Rect = new IntRect(center, size);
                if (root.Layout.Direction == LayoutDirection.Horizontal)
                {
                    leftOffset += width + root.Layout.Spacing;
                }
                else
                {
                    topOffset += height + root.Layout.Spacing;
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
    
    private void CloseElement()
    {
        UIElement element = _currentElement;
        
        //  Apply constraints
        int x = element.Constraints.X?.Calculate(1920) ?? element.Rect.Position.X;
        int y = element.Constraints.Y?.Calculate(1080) ?? element.Rect.Position.Y;
        var center = new IntVector2(x, y);
        
        int width = element.Constraints.Width?.Calculate(1920) ?? element.Rect.Size.X;
        int height = element.Constraints.Height?.Calculate(1080) ?? element.Rect.Size.Y;
        var size = new IntVector2(width, height);
        
        element.Rect = new IntRect(center, size);
        
        //  Attempt to get the parent, if any, off the stack
        UIElement parent = default;
        _hasOpenElement = _openElements.Count > 0;
        if (_hasOpenElement)
        {
            parent = _openElements.Pop();
        }
        
        //  Apply padding
        Padding padding = element.Style.Padding;
        width = element.Rect.Size.X + padding.Left + padding.Right;
        height = element.Rect.Size.Y + padding.Top + padding.Bottom;
        
        //  Calculate spacing
        int childCount = element.Children?.Count ?? 0;
        int totalSpacing = childCount > 1 ? (childCount - 1) * element.Layout.Spacing : 0;

        //  Apply spacing of children to the element
        if (element.Layout.Direction == LayoutDirection.Horizontal)
        {
            width += totalSpacing;
        }
        else
        {
            height += totalSpacing;
        }

        size = new IntVector2(width, height);
        element.Rect = new IntRect(element.Rect.Position, size);

        //  Resize parent to fit its children
        if (parent.Layout.Direction == LayoutDirection.Horizontal)
        {
            width = parent.Rect.Size.X + element.Rect.Size.X;
            height = Math.Max(parent.Rect.Size.Y, element.Rect.Size.Y);
        }
        else
        {
            width = Math.Max(parent.Rect.Size.X, element.Rect.Size.X);
            height = parent.Rect.Size.Y + element.Rect.Size.Y;
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
}