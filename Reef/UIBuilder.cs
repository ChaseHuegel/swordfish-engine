using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;

namespace Reef;

public sealed class UIBuilder<TRendererData>
{
    private struct ElementNode(Element<TRendererData> element, Element<TRendererData>? parent = null, int childIndex = -1)
    {
        public readonly Element<TRendererData> Element = element;
        public readonly Element<TRendererData>? Parent = parent;
        public readonly int ChildIndex = childIndex;
        public int LeftOffset = 0;
        public int TopOffset = 0;
    }

    public Vector4 Color
    {
        get => _currentElement.Style.Color;
        set => _currentElement.Style.Color = value;
    }
    
    public Vector4 BackgroundColor
    {
        get => _currentElement.Style.BackgroundColor;
        set => _currentElement.Style.BackgroundColor = value;
    }

    public UI.Constraints Constraints
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

    public CornerRadius CornerRadius
    {
        get => _currentElement.Style.CornerRadius;
        set => _currentElement.Style.CornerRadius = value;
    }
    
    public bool VerticalScroll
    {
        get => _currentElement.Viewport.VerticalScroll;
        set => _currentElement.Viewport.VerticalScroll = value;
    }
    
    public bool HorizontalScroll
    {
        get => _currentElement.Viewport.HorizontalScroll;
        set => _currentElement.Viewport.HorizontalScroll = value;
    }
    
    public int ScrollX
    {
        get => _currentElement.Viewport.Offset.X;
        set => _currentElement.Viewport.Offset.X = value;
    }
    
    public int ScrollY
    {
        get => _currentElement.Viewport.Offset.Y;
        set => _currentElement.Viewport.Offset.Y = value;
    }
    
    public UI.Constraints? ClipConstraints
    {
        get => _currentElement.Viewport.ClipConstraints;
        set => _currentElement.Viewport.ClipConstraints = value;
    }
    
    public string? ID
    {
        get => _currentElement.ID;
        set => _currentElement.ID = value;
    }

    public int Width => _viewPort.Size.X;
    public int Height => _viewPort.Size.Y;

    private IntRect _viewPort;
    private readonly ITextEngine _textEngine;
    private readonly UIController _controller;
    
    private bool _hasOpenElement;
    private Element<TRendererData> _currentElement;
    private readonly Stack<Element<TRendererData>> _openElements = new();
    private readonly List<Element<TRendererData>> _closedRootElements = [];
    
    private readonly char[] _whiteSpaceChars = [' ', '\t', '\n'];

    public UIBuilder(int width, int height, ITextEngine textEngine, UIController controller)
    {
        _textEngine = textEngine;
        _controller = controller;
        _viewPort = new IntRect(left: 0, top: 0, size: new IntVector2(width, height));
    }
    
    public void Resize(int width, int height)
    {
        _viewPort = new IntRect(left: 0, top: 0, size: new IntVector2(width, height));
    }

    public Scope Element() => OpenElement(new Element<TRendererData>());
    public Scope Element(Element<TRendererData> element) => OpenElement(element);
    public Scope Element(string id) => OpenElement(new Element<TRendererData> { ID = id });

    public Scope Text(string value) => Text(value, new FontOptions { Size = 16 });
    
    public Scope Text(string value, int size) => Text(value, new FontOptions { Size = size });
    
    public Scope Text(string value, string fontID) => Text(value, new FontOptions { Size = 16, ID = fontID });
    
    public Scope Text(string value, int size, string fontID) => Text(value, new FontOptions { Size = size, ID = fontID });

    public Scope Text(string value, FontOptions fontOptions)
    {
        var element = new Element<TRendererData>
        {
            Text = value,
            Style = new Style
            {
                Color = Vector4.One,
            },
            FontOptions = fontOptions,
        };
        
        return OpenElement(element);
    }
    
    public Scope Image(TRendererData value)
    {
        var element = new Element<TRendererData>
        {
            TextureData = value,
            Style = new Style
            {
                Color = Vector4.One,
            },
        };
        
        return OpenElement(element);
    }

    public TextConstraints Measure(FontOptions fontOptions, string text) => _textEngine.Measure(fontOptions, text);
    public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length) => _textEngine.Measure(fontOptions, text, start, length);
    
    public bool Clicked(string id)
    {
        _currentElement.ID = id;
        return _controller.IsClicked(id);
    }
    
    public bool Released(string id)
    {
        _currentElement.ID = id;
        return _controller.IsReleased(id);
    }
    
    public bool Held(string id)
    {
        _currentElement.ID = id;
        return _controller.IsHeld(id);
    }

    public bool Hovering(string id)
    {
        _currentElement.ID = id;
        return _controller.IsHovering(id);
    }

    public bool Clicked() => _controller.IsClicked(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be clicked"));
    public bool Released() => _controller.IsReleased(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be released"));
    public bool Held() => _controller.IsHeld(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be held"));

    public bool Hovering() => _controller.IsHovering(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be hovered"));
    public bool Entered() => _controller.IsEntering(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be entered"));
    public bool Exited() => _controller.IsExiting(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be exited"));

    public bool Focused() => _controller.IsFocused(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be focused"));
    
    public bool IsPressed(UIController.Key key) => _controller.IsPressed(key);
    
    public IReadOnlyCollection<UIController.Input> GetInputBuffer() => _controller.GetInputBuffer();
    
    public bool LeftPressed() => _controller.IsLeftPressed();
    public bool LeftReleased() => _controller.IsLeftReleased();
    public bool LeftHeld() => _controller.IsLeftHeld();
    
    public bool RightPressed() => _controller.IsRightPressed();
    public bool RightReleased() => _controller.IsRightReleased();
    public bool RightHeld() => _controller.IsRightHeld();

    public RenderCommand<TRendererData>[] Build()
    {
        //  Force close all elements
        while (_hasOpenElement)
        {
            CloseElement();
        }
        
        var stack = new Stack<ElementNode>();

        //  Perform a top-down sizing pass to process filling and shrinking elements
        FillShrinkAndWrapElements(stack);

        //  Create render commands out of all elements,
        //  performing a top-down positioning pass in the process.
        List<RenderCommand<TRendererData>> commands = PositionAndRenderElements(stack);

        //  Reset state
        _hasOpenElement = false;
        _currentElement = default;
        _openElements.Clear();
        _closedRootElements.Clear();
        return commands.ToArray();
    }

    private Scope OpenElement(Element<TRendererData> element)
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
        Element<TRendererData> element = _currentElement;
        
        //  Calculate non-wrapped text constraints
        if (element.Text != null)
        {
            int firstWordLength = element.Text.IndexOfAny(_whiteSpaceChars);
            if (firstWordLength < 0)
            {
                firstWordLength = element.Text.Length;
            }
            
            TextConstraints firstWordConstraints = _textEngine.Measure(element.FontOptions, element.Text, start: 0, firstWordLength);

            TextLayout textLayout = _textEngine.Layout(element.FontOptions, element.Text);
            TextConstraints fullTextConstraints = textLayout.Constraints;

            var textConstraints = new UI.Constraints
            {
                Anchors = element.Constraints.Anchors,
                X = element.Constraints.X,
                Y = element.Constraints.Y,
                Width = element.Constraints.Width ?? new Fixed(fullTextConstraints.PreferredWidth),
                Height = element.Constraints.Height ?? new Fixed(fullTextConstraints.PreferredHeight),
                MinWidth = firstWordConstraints.MinWidth,
                MinHeight = firstWordConstraints.MinHeight,
            };
            element.Constraints = textConstraints;
        }

        //  Attempt to get the parent, if any, off the stack
        Element<TRendererData> parent = default;
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
                width = Math.Max(parent.Rect.Size.X, element.Rect.Size.X);
                height = Math.Max(parent.Rect.Size.Y, element.Rect.Size.Y);
                parent.Constraints.MinWidth = Math.Max(element.Constraints.MinWidth, parent.Constraints.MinWidth);
                parent.Constraints.MinHeight = Math.Max(element.Constraints.MinHeight, parent.Constraints.MinHeight);
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
    
    private List<RenderCommand<TRendererData>> PositionAndRenderElements(Stack<ElementNode> stack)
    {
        List<RenderCommand<TRendererData>> commands = [];
        for (var i = 0; i < _closedRootElements.Count; i++)
        {
            stack.Clear();
            Element<TRendererData> root = _closedRootElements[i];
            stack.Push(new ElementNode(root));
        
            while (stack.Count > 0)
            {
                ElementNode frame = stack.Pop();
                Element<TRendererData> element = frame.Element;
        
                //  Apply position constraints
                int availableWidth = Width;
                int availableHeight = Height;
                if (frame.Parent != null)
                {
                    availableWidth = frame.Parent.Value.Rect.Size.X;
                    availableHeight = frame.Parent.Value.Rect.Size.Y;
                }
                
                int x = element.Constraints.X?.Calculate(availableWidth) ?? element.Rect.Position.X;
                int y = element.Constraints.Y?.Calculate(availableHeight) ?? element.Rect.Position.Y;
                
                //  Apply layout offsets
                if (frame.Parent != null)
                {
                    x += frame.Parent.Value.Rect.Position.X + frame.LeftOffset;
                    y += frame.Parent.Value.Rect.Position.Y + frame.TopOffset;
                }
            
                //  Apply anchoring. Top Left is default, so only need to apply Center/Right/Bottom.
                Anchors anchors = element.Constraints.Anchors;
                int xAnchorOffset = element.Rect.Size.X;
                int yAnchorOffset = element.Rect.Size.Y;

                var rightInset = 0;
                var bottomInset = 0;
                if (frame.Parent != null)
                {
                    //  Padding is applied as an inset when not anchored to the left/top.
                    //  Left/top padding are already accounted for the origin position,
                    //  so they must be reverted when applied to any other anchor.
                    rightInset = frame.Parent.Value.Style.Padding.Right + frame.Parent.Value.Style.Padding.Left;
                    bottomInset = frame.Parent.Value.Style.Padding.Bottom + frame.Parent.Value.Style.Padding.Top;
                }
            
                if ((anchors & Anchors.Right) == Anchors.Right)
                {
                    x -= xAnchorOffset + rightInset;
                }
                else if ((anchors & Anchors.Center) == Anchors.Center && (anchors & Anchors.Left) != Anchors.Left)
                {
                    x -= (xAnchorOffset + rightInset) >> 1;
                }
            
                if ((anchors & Anchors.Bottom) == Anchors.Bottom)
                {
                    y -= yAnchorOffset + bottomInset;
                }
                else if ((anchors & Anchors.Center) == Anchors.Center && (anchors & Anchors.Top) != Anchors.Top)
                {
                    y -= (yAnchorOffset + bottomInset) >> 1;
                }
                
                var center = new IntVector2(x, y);
                element.Rect = new IntRect(center, element.Rect.Size);
                
                // Save any changes made to the element
                if (frame.Parent == null)
                {
                    _closedRootElements[i] = element;
                }
                else if (frame.Parent.Value.Children != null)
                {
                    frame.Parent.Value.Children[frame.ChildIndex] = element;
                }

                //  Don't issue render commands or continue processing any
                //  elements entirely outside its parent's bounds
                if (frame.Parent != null && !frame.Parent.Value.Rect.Intersects(element.Rect))
                {
                    continue;
                }

                IntRect clipRect;
                //  Clip children by their parent
                if (frame.Parent != null)
                {
                    if (frame.Parent.Value.Viewport.ClipRect != null)
                    {
                        int clipX = frame.Parent.Value.Rect.Position.X + frame.Parent.Value.Style.Padding.Left;
                        int clipY = frame.Parent.Value.Rect.Position.Y + frame.Parent.Value.Style.Padding.Top;
                        var clipPosition = new IntVector2(clipX, clipY);
                        clipRect = new IntRect(clipPosition, frame.Parent.Value.Viewport.ClipRect.Value.Size);
                    }
                    else
                    {
                        clipRect = frame.Parent.Value.Rect;
                    }
                }
                //  Otherwise the element isn't clipped
                else
                {
                    clipRect = element.Rect;
                }

                var command = new RenderCommand<TRendererData>
                (
                    element.Rect,
                    clipRect,
                    element.Style.Color,
                    element.Style.BackgroundColor,
                    element.Style.CornerRadius,
                    element.FontOptions,
                    element.Text,
                    element.TextureData
                );
                commands.Add(command);
                
                //  Update input
                if (element.ID != null)
                {
                    _controller.UpdateInteraction(element.ID, element.Rect);
                }
        
                // Push any children to be processed
                if (element.Children == null)
                {
                    continue;
                }

                int leftOffset = element.Style.Padding.Left;
                int topOffset = element.Style.Padding.Top;
                
                //  Apply viewport offset
                leftOffset += element.Viewport.Offset.X;
                topOffset += element.Viewport.Offset.Y;
                
                for (var childIndex = 0; childIndex < element.Children.Count; childIndex++)
                {
                    Element<TRendererData> child = element.Children[childIndex];
                    stack.Push(new ElementNode(child, element, childIndex)
                    {
                        LeftOffset = leftOffset,
                        TopOffset = topOffset,
                    });
                    
                    //  Apply layout offsets
                    switch (element.Layout.Direction)
                    {
                        case LayoutDirection.Horizontal:
                            leftOffset += child.Rect.Size.X + element.Layout.Spacing;
                            break;
                        case LayoutDirection.Vertical:
                            topOffset += child.Rect.Size.Y + element.Layout.Spacing;
                            break;
                        case LayoutDirection.None:
                            //  Do nothing
                            break;
                    }
                }
            }
        }
        
        return commands;
    }
    
    private void FillShrinkAndWrapElements(Stack<ElementNode> stack) 
    {
        for (var i = 0; i < _closedRootElements.Count; i++)
        {
            stack.Clear();
            stack.Push(new ElementNode(_closedRootElements[i]));
        
            while (stack.Count > 0)
            {
                ElementNode frame = stack.Pop();
                Element<TRendererData> element = frame.Element;
        
                FillChildren(ref element);
                ShrinkChildren(ref element);
                WrapTextChildren(ref element);
                ClipChildren(ref element);
        
                // Save any changes made to the element
                if (frame.Parent == null)
                {
                    _closedRootElements[i] = element;
                }
                else if (frame.Parent.Value.Children != null)
                {
                    frame.Parent.Value.Children[frame.ChildIndex] = element;
                }
        
                // Push any children to be processed
                if (element.Children == null)
                {
                    continue;
                }

                for (var childIndex = 0; childIndex < element.Children.Count; childIndex++)
                {
                    Element<TRendererData> child = element.Children[childIndex];
                    stack.Push(new ElementNode(child, element, childIndex));
                }
            }
        }
    }

    private void FillChildren(ref Element<TRendererData> parent)
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
            Element<TRendererData> child = parent.Children[i];

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
                Element<TRendererData> child = parent.Children[i];

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
                Element<TRendererData> child = parent.Children[i];
                
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

    private void WrapTextChildren(ref Element<TRendererData> parent)
    {
        for (var i = 0; i < parent.Children?.Count; i++)
        {
            Element<TRendererData> child = parent.Children[i];
            if (child.Text == null)
            {
                continue;
            }
            
            TextLayout textLayout = _textEngine.Layout(child.FontOptions, child.Text, child.Rect.Size.X);
            
            var size = new IntVector2(child.Rect.Size.X, textLayout.Constraints.MinHeight);
            child.Rect = new IntRect(child.Rect.Position, size);
            
            parent.Children[i] = child;
        }
    }
    
    private void ShrinkChildren(ref Element<TRendererData> parent)
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
            Element<TRendererData> child = parent.Children[i];

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
                Element<TRendererData> child = parent.Children[i];

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
                widthToAdd = Math.Min(widthToAdd, -1);
            }
            else
            {
                availableWidth = 0;
            }
            
            if (numVerticalShrinkChildren > 0)
            {
                heightToAdd = Math.Max(heightToAdd, availableHeight / numVerticalShrinkChildren);
                heightToAdd = Math.Min(heightToAdd, -1);
            }
            else
            {
                availableHeight = 0;
            }
            
            //  Distribute available space among children.
            //  Along the layout axis, available space is distributed beginning with the largest children.
            //  Opposite the layout axis, available space is consumed entirely.
            for (var i = 0; i < parent.Children.Count; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                
                bool matchesLargestWidth = child.Rect.Size.X == largestWidth;
                bool matchesLargestHeight = child.Rect.Size.Y == largestHeight;
                if (!matchesLargestWidth && !matchesLargestHeight)
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
    
    private void ClipChildren(ref Element<TRendererData> parent)
    {
        //  Move on if this element isn't clipping children
        if (parent.Viewport.ClipConstraints == null)
        {
            return;
        }
        
        int availableWidth = parent.Rect.Size.X;
        int availableHeight = parent.Rect.Size.Y;
        
        availableWidth -= parent.Style.Padding.Left + parent.Style.Padding.Right;
        availableHeight -= parent.Style.Padding.Top + parent.Style.Padding.Bottom;
        
        UI.Constraints clipConstraints = parent.Viewport.ClipConstraints.Value;
        int clipWidth = clipConstraints.Width?.Calculate(availableWidth) ?? parent.Rect.Size.X;
        int clipHeight = clipConstraints.Height?.Calculate(availableHeight) ?? parent.Rect.Size.Y;
        var clipSize = new IntVector2(clipWidth, clipHeight);
        
        parent.Viewport.ClipRect = new IntRect(parent.Rect.Position, clipSize);
    }
    
    public struct Scope(UIBuilder<TRendererData> ui) : IDisposable
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
}