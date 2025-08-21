using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading;
using Reef.Text;

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

    public Vector4 Color
    {
        get => _currentElement.Style.Color;
        set => _currentElement.Style.Color = value;
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
    
    public int FontSize
    {
        get => _currentElement.FontOptions.Size;
        set => _currentElement.FontOptions.Size = value;
    }
    
    public string? FontID
    {
        get => _currentElement.FontOptions.ID;
        set => _currentElement.FontOptions.ID = value;
    }
    
    public FontOptions FontOptions
    {
        get => _currentElement.FontOptions;
        set => _currentElement.FontOptions = value;
    }
    
    public int Width => _viewPort.Size.X;
    public int Height => _viewPort.Size.Y;

    private readonly IntRect _viewPort;
    private readonly ITextEngine _textEngine;
    
    private bool _hasOpenElement;
    private UIElement<TTextureData> _currentElement;
    private readonly Stack<UIElement<TTextureData>> _openElements = new();
    private readonly List<UIElement<TTextureData>> _closedRootElements = [];
    
    private readonly char[] _whiteSpaceChars = [' ', '\t', '\n'];

    public UIBuilder(int width, int height, ITextEngine textEngine)
    {
        _textEngine = textEngine;
        _viewPort = new IntRect(left: 0, top: 0, size: new IntVector2(width, height));
    }

    public Scope Element()
    {
        return OpenElement(new UIElement<TTextureData>());
    }
    
    public Scope Element(UIElement<TTextureData> element)
    {
        return OpenElement(element);
    }

    public Scope Text(string value)
    {
        var element = new UIElement<TTextureData>
        {
            Text = value,
            Style = new Style
            {
                Color = Vector4.One,
            },
            FontOptions = new FontOptions {
                Size = 16,
            },
        };
        
        return OpenElement(element);
    }
    
    public Scope Image(TTextureData value)
    {
        var element = new UIElement<TTextureData>
        {
            TextureData = value,
        };
        
        return OpenElement(element);
    }
    
    public RenderCommand<TTextureData>[] Build()
    {
        //  Force close all elements
        while (_hasOpenElement)
        {
            CloseElement();
        }
        
        //  Perform a top-down sizing pass to process filling and shrinking elements
        for (var i = 0; i < _closedRootElements.Count; i++)
        {
            UIElement<TTextureData> root = _closedRootElements[i];
            FillChildren(ref root);
            ShrinkChildren(ref root);
            WrapText(ref root);
            _closedRootElements[i] = root;

            for (var n = 0; root.Children != null && n < root.Children.Count; n++)
            {
                UIElement<TTextureData> child = root.Children[n];
                FillChildren(ref child);
                ShrinkChildren(ref child);
                WrapText(ref child);
                root.Children[n] = child;
            }
        }

        //  Create render commands out of all elements,
        //  performing a top-down positioning pass in the process.
        List<RenderCommand<TTextureData>> commands = [];
        for (var i = 0; i < _closedRootElements.Count; i++)
        {
            UIElement<TTextureData> root = _closedRootElements[i];
            
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
            (
                root.Rect,
                root.Style.Color,
                root.Style.CornerRadius,
                root.FontOptions,
                root.Text,
                root.TextureData
            );
            commands.Add(command);

            int leftOffset = root.Style.Padding.Left;
            int topOffset = root.Style.Padding.Top;
            for (var n = 0; root.Children != null && n < root.Children.Count; n++)
            {
                UIElement<TTextureData> child = root.Children[n];

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
                (
                    child.Rect,
                    child.Style.Color,
                    child.Style.CornerRadius,
                    child.FontOptions,
                    child.Text,
                    child.TextureData
                );
                commands.Add(command);
            }
        }

        //  Reset state
        _openElements.Clear();
        _closedRootElements.Clear();
        return commands.ToArray();
    }

    private Scope OpenElement(UIElement<TTextureData> element)
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
        UIElement<TTextureData> element = _currentElement;
        
        //  Calculate non-wrapped text constraints
        if (element.Text != null)
        {
            int firstWordLength = element.Text.IndexOfAny(_whiteSpaceChars);
            TextConstraints firstWordConstraints = _textEngine.Measure(element.FontOptions, element.Text, start: 0, firstWordLength);

            TextLayout textLayout = _textEngine.Layout(element.FontOptions, element.Text);
            TextConstraints fullTextConstraints = textLayout.Constraints;

            element.Constraints = new Constraints
            {
                Width = new Fixed(fullTextConstraints.PreferredWidth),
                Height = new Fixed(fullTextConstraints.PreferredHeight),
                MinWidth = firstWordConstraints.MinWidth,
                MinHeight = firstWordConstraints.MinHeight,
            };
        }

        //  Attempt to get the parent, if any, off the stack
        UIElement<TTextureData> parent = default;
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
        
        //  Set min width and height if they are unassigned
        if (element.Constraints.MinWidth == 0)
        {
            element.Constraints.MinWidth = width;
        }

        if (element.Constraints.MinHeight == 0)
        {
            element.Constraints.MinHeight = height;
        }

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
                parent.Constraints.MinWidth += element.Constraints.MinWidth;
                parent.Constraints.MinHeight = Math.Max(element.Constraints.MinHeight, parent.Constraints.MinHeight);
                break;
            case LayoutDirection.Vertical:
                width = Math.Max(parent.Rect.Size.X, element.Rect.Size.X);
                height = parent.Rect.Size.Y + element.Rect.Size.Y;
                parent.Constraints.MinWidth = Math.Max(element.Constraints.MinWidth, parent.Constraints.MinWidth);
                parent.Constraints.MinHeight += element.Constraints.MinHeight;
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

    private void FillChildren(ref UIElement<TTextureData> parent)
    {
        int availableWidth = parent.Rect.Size.X;
        int availableHeight = parent.Rect.Size.Y;
        
        availableWidth -= parent.Style.Padding.Left + parent.Style.Padding.Right;
        availableHeight -= parent.Style.Padding.Top + parent.Style.Padding.Bottom;

        var numHorizontalFillChildren = 0;
        var numVerticalFillChildren = 0;
        
        //  Calculate available space
        for (var i = 0; parent.Children != null && i < parent.Children.Count; i++)
        {
            UIElement<TTextureData> child = parent.Children[i];

            //  Count children against available space based on layout direction
            switch (parent.Layout.Direction)
            {
                case LayoutDirection.Horizontal:
                    availableWidth -= child.Rect.Size.X;
                    break;
                case LayoutDirection.Vertical:
                    availableHeight -= child.Rect.Size.Y;
                    break;
            }
            
            //  Count children with fill constraints
            if (child.Constraints.Width is Fill)
            {
                numHorizontalFillChildren++;
            }
            
            if (child.Constraints.Height is Fill)
            {
                numVerticalFillChildren++;
            }
        }
        
        //  Move on if no children have fill constraints
        if (numHorizontalFillChildren == 0 && numVerticalFillChildren == 0)
        {
            return;
        }

        int childCount = parent.Children?.Count ?? 0;
        int totalSpacing = childCount > 1 ? (childCount - 1) * parent.Layout.Spacing : 0;
        
        //  Count child spacing against available space based on layout direction
        switch (parent.Layout.Direction)
        {
            case LayoutDirection.Horizontal:
                availableWidth -= totalSpacing;
                break;
            case LayoutDirection.Vertical:
                availableHeight -= totalSpacing;
                break;
        }
        
        //  Continue distributing available space until none is left
        while (availableWidth > 0 && availableHeight > 0 && parent.Children != null)
        {
            int smallestWidth = -1;
            var secondSmallestWidth = int.MaxValue;
            int widthToAdd = availableWidth;
            
            int smallestHeight = -1;
            var secondSmallestHeight = int.MaxValue;
            int heightToAdd = availableHeight;

            //  Determine how much space should be added to the smallest children
            for (var i = 0; i < parent.Children.Count; i++)
            {
                UIElement<TTextureData> child = parent.Children[i];

                bool fillHorizontal = child.Constraints.Width is Fill;
                bool fillVertical = child.Constraints.Height is Fill;
                if (!fillHorizontal && !fillVertical)
                {
                    continue;
                }

                //  Find the smallest of both axis
                if (smallestWidth == -1)
                {
                    smallestWidth = child.Rect.Size.X;
                }
                
                if (smallestHeight == -1)
                {
                    smallestHeight = child.Rect.Size.Y;
                }

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
                
                if (child.Rect.Size.Y < smallestHeight)
                {
                    secondSmallestHeight = smallestHeight;
                    smallestHeight = child.Rect.Size.Y;
                }
                else if (child.Rect.Size.Y > smallestHeight)
                {
                    secondSmallestHeight = Math.Min(secondSmallestHeight, child.Rect.Size.Y);
                    heightToAdd = secondSmallestHeight - smallestHeight;
                }
            }

            //  Ensure the space to distribute doesn't reach 0, or the loop could never complete.
            if (numHorizontalFillChildren > 0)
            {
                widthToAdd = Math.Min(widthToAdd, availableWidth / numHorizontalFillChildren);
            }
            widthToAdd = Math.Max(widthToAdd, 1);

            if (numVerticalFillChildren > 0)
            {
                heightToAdd = Math.Min(heightToAdd, availableHeight / numVerticalFillChildren);
            }
            heightToAdd = Math.Max(heightToAdd, 1);
            
            //  Distribute available space among children.
            //  Along the layout axis, available space is distributed beginning with the smallest children.
            //  Opposite the layout axis, available space is consumed entirely.
            for (var i = 0; i < parent.Children.Count; i++)
            {
                UIElement<TTextureData> child = parent.Children[i];
                
                bool matchesSmallestWidth = child.Rect.Size.X == smallestWidth;
                bool matchesSmallestHeight = child.Rect.Size.Y == smallestHeight;
                if (!matchesSmallestWidth && !matchesSmallestHeight)
                {
                    continue;
                }
                
                bool fillHorizontal = child.Constraints.Width is Fill;
                bool fillVertical = child.Constraints.Height is Fill;
                if (!fillHorizontal && !fillVertical)
                {
                    continue;
                }
                
                //  Distribute available space evenly on the axis of the layout
                int width, height;
                switch (parent.Layout.Direction)
                {
                    case LayoutDirection.Horizontal:
                        width = child.Rect.Size.X + (fillHorizontal ? widthToAdd : 0);
                        height = child.Rect.Size.Y + (fillVertical ? availableHeight : 0);
                        break;
                    case LayoutDirection.Vertical:
                        width = child.Rect.Size.X + (fillHorizontal ? availableWidth : 0);
                        height = child.Rect.Size.Y + (fillVertical ? heightToAdd : 0);
                        break;
                    default:
                        width = child.Rect.Size.X + (fillHorizontal ? widthToAdd : 0);
                        height = child.Rect.Size.Y + (fillVertical ? heightToAdd : 0);
                        break;
                }
                
                //  Update the child
                var size = new IntVector2(width, height);
                child.Rect = new IntRect(child.Rect.Position, size);
                parent.Children[i] = child;
                
                //  Consume distributed available space on the axis of the layout
                switch (parent.Layout.Direction)
                {
                    case LayoutDirection.Horizontal when fillHorizontal:
                        availableWidth -= widthToAdd;
                        break;
                    case LayoutDirection.Vertical when fillVertical:
                        availableHeight -= heightToAdd;
                        break;
                    case LayoutDirection.None:
                    {
                        if (fillHorizontal)
                        {
                            availableWidth -= widthToAdd;
                        }

                        if (fillVertical)
                        {
                            availableHeight -= heightToAdd;
                        }

                        break;
                    }
                }
            }
            
            //  Consume all available space opposite the axis of the layout.
            switch (parent.Layout.Direction)
            {
                case LayoutDirection.Horizontal:
                    availableHeight = 0;
                    break;
                case LayoutDirection.Vertical:
                    availableWidth = 0;
                    break;
            }
        }
    }

    private void WrapText(ref UIElement<TTextureData> parent)
    {
        int availableHeight = parent.Rect.Size.Y;
        availableHeight -= parent.Style.Padding.Top + parent.Style.Padding.Bottom;
        var numTextChildren = 0;
        
        //  Calculate available space
        for (var i = 0; parent.Children != null && i < parent.Children.Count; i++)
        {
            UIElement<TTextureData> child = parent.Children[i];

            //  If vertical, count children against available space
            if (parent.Layout.Direction == LayoutDirection.Vertical)
            {
                availableHeight -= child.Rect.Size.Y;
            }

            //  Count children that have text
            if (child.Text != null)
            {
                numTextChildren++;
            }
        }
        
        //  Move on if no children have text
        if (numTextChildren == 0)
        {
            return;
        }
        
        //  If vertical, count child spacing against available space
        if (parent.Layout.Direction == LayoutDirection.Vertical)
        {
            int childCount = parent.Children?.Count ?? 0;
            int totalSpacing = childCount > 1 ? (childCount - 1) * parent.Layout.Spacing : 0;
            availableHeight -= totalSpacing;
        }

        if (parent.Children == null)
        {
            return;
        }
        
        for (var i = 0; i < parent.Children.Count; i++)
        {
            UIElement<TTextureData> child = parent.Children[i];
            if (child.Text == null)
            {
                continue;
            }
            
            TextLayout textLayout = _textEngine.Layout(child.FontOptions, child.Text, child.Rect.Size.X);
            
            var size = new IntVector2(child.Rect.Size.X, Math.Min(textLayout.Constraints.MinHeight, availableHeight));
            child.Rect = new IntRect(child.Rect.Position, size);
            
            parent.Children[i] = child;
        }
    }
    
    private void ShrinkChildren(ref UIElement<TTextureData> parent)
    {
        int availableWidth = parent.Rect.Size.X;
        int availableHeight = parent.Rect.Size.Y;
        
        availableWidth -= parent.Style.Padding.Left + parent.Style.Padding.Right;
        availableHeight -= parent.Style.Padding.Top + parent.Style.Padding.Bottom;

        var numHorizontalShrinkChildren = 0;
        var numVerticalShrinkChildren = 0;
        
        //  Calculate available space
        for (var i = 0; parent.Children != null && i < parent.Children.Count; i++)
        {
            UIElement<TTextureData> child = parent.Children[i];

            //  Count children against available space
            switch (parent.Layout.Direction)
            {
                case LayoutDirection.Horizontal:
                    availableWidth -= child.Rect.Size.X;
                    break;
                case LayoutDirection.Vertical:
                    availableHeight -= child.Rect.Size.Y;
                    break;
            }

            //  Count children that can shrink
            if (child.Constraints.MinWidth != child.Rect.Size.X)
            {
                numHorizontalShrinkChildren++;
            }
            
            if (child.Constraints.MinHeight != child.Rect.Size.Y)
            {
                numVerticalShrinkChildren++;
            }
        }
        
        //  Move on if no children have shrink constraints
        if (numHorizontalShrinkChildren == 0 && numVerticalShrinkChildren == 0)
        {
            return;
        }

        int childCount = parent.Children?.Count ?? 0;
        int totalSpacing = childCount > 1 ? (childCount - 1) * parent.Layout.Spacing : 0;
        
        //  Count child spacing against available space based on layout direction
        switch (parent.Layout.Direction)
        {
            case LayoutDirection.Horizontal:
                availableWidth -= totalSpacing;
                break;
            case LayoutDirection.Vertical:
                availableHeight -= totalSpacing;
                break;
        }
        
        //  Continue distributing available space until none is left
        while ((availableWidth < 0 || availableHeight < 0) && parent.Children != null)
        {
            int largestWidth = -1;
            var secondLargestWidth = 0;
            int widthToAdd = availableWidth;
            
            int largestHeight = -1;
            var secondLargestHeight = 0;
            int heightToAdd = availableHeight;

            //  Determine how much space should be added to the largest children
            for (var i = 0; i < parent.Children.Count; i++)
            {
                UIElement<TTextureData> child = parent.Children[i];

                bool shrinkHorizontal = child.Constraints.MinWidth != child.Rect.Size.X;
                bool shrinkVertical = child.Constraints.MinHeight != child.Rect.Size.Y;
                if (!shrinkHorizontal && !shrinkVertical)
                {
                    continue;
                }

                //  Find the largest of both axis
                if (largestWidth == -1)
                {
                    largestWidth = child.Rect.Size.X;
                }
                
                if (largestHeight == -1)
                {
                    largestHeight = child.Rect.Size.Y;
                }

                if (child.Rect.Size.X > largestWidth)
                {
                    secondLargestWidth = largestWidth;
                    largestWidth = child.Rect.Size.X;
                }
                else if (child.Rect.Size.X < largestWidth)
                {
                    secondLargestWidth = Math.Max(secondLargestWidth, child.Rect.Size.X);
                    widthToAdd = secondLargestWidth - largestWidth;
                }
                
                if (child.Rect.Size.Y > largestHeight)
                {
                    secondLargestHeight = largestHeight;
                    largestHeight = child.Rect.Size.Y;
                }
                else if (child.Rect.Size.Y < largestHeight)
                {
                    secondLargestHeight = Math.Max(secondLargestHeight, child.Rect.Size.Y);
                    heightToAdd = secondLargestHeight - largestHeight;
                }
            }

            //  Ensure the space to distribute doesn't reach 0, or the loop could never complete.
            if (numHorizontalShrinkChildren > 0)
            {
                widthToAdd = Math.Max(widthToAdd, availableWidth / numHorizontalShrinkChildren);
            }
            widthToAdd = Math.Min(widthToAdd, -1);
            
            if (numHorizontalShrinkChildren > 0)
            {
                heightToAdd = Math.Max(heightToAdd, availableHeight / numHorizontalShrinkChildren);
            }
            heightToAdd = Math.Min(heightToAdd, -1);
            
            //  Distribute available space among children.
            //  Along the layout axis, available space is distributed beginning with the largest children.
            //  Opposite the layout axis, available space is consumed entirely.
            for (var i = 0; i < parent.Children.Count; i++)
            {
                UIElement<TTextureData> child = parent.Children[i];
                
                bool matchesLargestWidth = child.Rect.Size.X == largestWidth;
                if (!matchesLargestWidth)
                {
                    continue;
                }
                
                bool shrinkHorizontal = child.Constraints.MinWidth != child.Rect.Size.X;
                bool shrinkVertical = child.Constraints.MinHeight != child.Rect.Size.Y;
                if (!shrinkHorizontal && !shrinkVertical)
                {
                    continue;
                }
                
                //  Distribute available space evenly
                int width;
                int height;
                switch (parent.Layout.Direction)
                {
                    case LayoutDirection.Horizontal:
                        width = child.Rect.Size.X + (shrinkHorizontal ? widthToAdd : 0);
                        height = shrinkVertical ? availableHeight : child.Rect.Size.Y;
                        break;
                    case LayoutDirection.Vertical:
                        width = shrinkHorizontal ? availableWidth : child.Rect.Size.X;
                        height = child.Rect.Size.Y + (shrinkVertical ? heightToAdd : 0);
                        break;
                    default:
                        continue;
                }

                //  Update the child
                var size = new IntVector2(width, height);
                child.Rect = new IntRect(child.Rect.Position, size);
                parent.Children[i] = child;
                
                //  Consume distributed available space on the axis of the layout
                switch (parent.Layout.Direction)
                {
                    case LayoutDirection.Horizontal when shrinkHorizontal:
                        availableWidth -= widthToAdd;
                        break;
                    case LayoutDirection.Vertical when shrinkVertical:
                        availableHeight -= heightToAdd;
                        break;
                    case LayoutDirection.None:
                    {
                        if (shrinkHorizontal)
                        {
                            availableWidth -= widthToAdd;
                        }

                        if (shrinkVertical)
                        {
                            availableHeight -= heightToAdd;
                        }

                        break;
                    }
                }
            }
            
            //  Consume all available space opposite the axis of the layout.
            switch (parent.Layout.Direction)
            {
                case LayoutDirection.Horizontal:
                    availableHeight = 0;
                    break;
                case LayoutDirection.Vertical:
                    availableWidth = 0;
                    break;
            }
        }
    }
}