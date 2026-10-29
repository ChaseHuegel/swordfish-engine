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
    
    public double Delta { get; private set; }
    
    public double Time { get; private set; }

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

    public Vector4 TextColor
    {
        get => _currentElement.Style.TextColor;
        set => _currentElement.Style.TextColor = value;
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

    public HorizontalAlignment HorizontalAlignment
    {
        get => _currentElement.FontOptions.HorizontalAlignment;
        set => _currentElement.FontOptions.HorizontalAlignment = value;
    }

    public VerticalAlignment VerticalAlignment
    {
        get => _currentElement.FontOptions.VerticalAlignment;
        set => _currentElement.FontOptions.VerticalAlignment = value;
    }

    public CornerRadius CornerRadius
    {
        get => _currentElement.Style.CornerRadius;
        set => _currentElement.Style.CornerRadius = value;
    }

    public BorderStyle Border
    {
        get => _currentElement.Style.Border;
        set => _currentElement.Style.Border = value;
    }

    public CornerCapStyle CornerCaps
    {
        get => _currentElement.Style.CornerCaps;
        set => _currentElement.Style.CornerCaps = value;
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

    public bool Debug
    {
        get => _currentElement.Debug;
        set
        {
            if (!_hasOpenElement)
            {
                _globalDebug = value;
            }
            
            _currentElement.Debug = value;
        }
    }
    
    public bool Passthrough
    {
        get => _currentElement.Passthrough;
        set => _currentElement.Passthrough = value;
    }

    public bool Overlay
    {
        get => _currentElement.Overlay;
        set => _currentElement.Overlay = value;
    }
    
    /// <summary>Gets or sets the font ID applied to text that does not specify one.</summary>
    public string? DefaultFontID { get; set; }

    public bool IsMouseCaptured => _controller.IsMouseCaptured;
    public bool IsKeyboardCaptured => _controller.IsKeyboardCaptured;

    public int Width => _viewPort.Size.X;
    public int Height => _viewPort.Size.Y;

    private IntRect _viewPort;
    public readonly ITextEngine TextEngine;
    private readonly UIController _controller;

    private bool _globalDebug;
    private bool _hasOpenElement;
    private Element<TRendererData> _currentElement;
    private readonly Stack<Element<TRendererData>> _openElements = new();
    private readonly Stack<ElementNode> _traversalStack = new();
    private readonly Stack<ElementNode> _childStack = new();
    private readonly List<ElementNode> _postOrderNodes = [];
    private readonly List<ElementNode> _overlayNodes = [];
    private readonly List<Element<TRendererData>> _closedRootElements = [];
    private readonly Stack<List<Element<TRendererData>>> _childListPool = new();
    private readonly List<List<Element<TRendererData>>> _activeChildLists = [];

    public UIBuilder(int width, int height, ITextEngine textEngine, UIController controller)
    {
        TextEngine = textEngine;
        _controller = controller;
        _viewPort = new IntRect(left: 0, top: 0, size: new IntVector2(width, height));
    }
    
    public void Resize(int width, int height)
    {
        _viewPort = new IntRect(left: 0, top: 0, size: new IntVector2(width, height));
    }

    public Scope Element() => OpenElement(new Element<TRendererData>());
    public Scope Element(Element<TRendererData> element)
    {
        element.Children = null;
        element.TextLayout = null;
        element.ContentSize = default;
        return OpenElement(element);
    }

    public Scope Element(string id) => OpenElement(new Element<TRendererData> { ID = id });

    public Scope Text(string value) => Text(value, new FontOptions { Size = 16 });
    
    public Scope Text(string value, int size) => Text(value, new FontOptions { Size = size });
    
    public Scope Text(string value, string fontID) => Text(value, new FontOptions { Size = 16, ID = fontID });
    
    public Scope Text(string value, int size, string fontID) => Text(value, new FontOptions { Size = size, ID = fontID });

    public Scope Text(string value, FontOptions fontOptions)
    {
        FontOptions effectiveFontOptions = fontOptions.ID == null
            ? fontOptions with { ID = DefaultFontID }
            : fontOptions;
        
        Vector4 textColor = _hasOpenElement && _currentElement.Style.TextColor.W > 0f
            ? _currentElement.Style.TextColor
            : Vector4.One;
        var element = new Element<TRendererData>
        {
            Text = value,
            Style = new Style
            {
                Color = textColor,
                TextColor = textColor,
            },
            FontOptions = effectiveFontOptions,
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
    
    public bool Clicked(string id, UIController.MouseButtons button = UIController.MouseButtons.Left)
    {
        _currentElement.ID = id;
        return _controller.IsClicked(id, button);
    }
    
    public bool Released(string id, UIController.MouseButtons button = UIController.MouseButtons.Left)
    {
        _currentElement.ID = id;
        return _controller.IsReleased(id, button);
    }
    
    public bool Held(string id, UIController.MouseButtons button = UIController.MouseButtons.Left)
    {
        _currentElement.ID = id;
        return _controller.IsHeld(id, button);
    }

    public bool Hovering(string id)
    {
        _currentElement.ID = id;
        return _controller.IsHovering(id);
    }

    public bool Entered(string id)
    {
        _currentElement.ID = id;
        return _controller.IsEntering(id);
    }

    public bool Exited(string id)
    {
        _currentElement.ID = id;
        return _controller.IsExiting(id);
    }

    public bool Scrolled(string id)
    {
        _currentElement.ID = id;
        return _controller.IsScrolled(id);
    }

    public IntVector2 GetCursorPosition() => _controller.GetCursorPosition();
    public IntVector2 GetCursorDelta() => _controller.GetCursorDelta();

    public float GetScrollDelta() => _controller.GetScrollDelta();
    public Vector2 GetScrollDelta2D() => _controller.GetScrollDelta2D();

    public float GetElementScrollDelta(string id)
    {
        return _controller.GetElementScrollDelta(id);
    }

    public float GetElementScrollDelta() => _controller.GetElementScrollDelta(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to query element scroll delta"));

    public Vector2 GetElementScrollDelta2D(string id)
    {
        return _controller.GetElementScrollDelta2D(id);
    }

    public Vector2 GetElementScrollDelta2D() => _controller.GetElementScrollDelta2D(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to query element scroll delta"));

    public IntVector2 GetRelativeCursorPosition(string id)
    {
        return _controller.GetRelativeCursorPosition(id);
    }

    public IntVector2 GetRelativeCursorPosition() => _controller.GetRelativeCursorPosition(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to detect the cursor"));
    
    public bool Clicked(UIController.MouseButtons button = UIController.MouseButtons.Left) => _controller.IsClicked(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be clicked"), button);
    public bool Released(UIController.MouseButtons button = UIController.MouseButtons.Left) => _controller.IsReleased(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be released"), button);
    public bool Held(UIController.MouseButtons button = UIController.MouseButtons.Left) => _controller.IsHeld(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be held"), button);

    public bool Hovering() => _controller.IsHovering(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be hovered"));
    public bool Entered() => _controller.IsEntering(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be entered"));
    public bool Exited() => _controller.IsExiting(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be exited"));
    public bool Scrolled() => _controller.IsScrolled(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be scrolled"));

    public bool Focused(string id)
    {
        _currentElement.ID = id;
        return _controller.IsFocused(id);
    }

    public bool Focused() => _controller.IsFocused(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be focused"));

    public void Focus(string id)
    {
        _currentElement.ID = id;
        _controller.Focus(id);
    }

    public void Focus() => _controller.Focus(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to be focused"));
    public void Unfocus() => _controller.Unfocus();
    
    public bool IsPressed(UIController.Key key) => _controller.IsPressed(key);
    public bool IsReleased(UIController.Key key) => _controller.IsReleased(key);
    public bool IsHeld(UIController.Key key) => _controller.IsHeld(key);
    
    public IReadOnlyCollection<UIController.Input> GetInputBuffer() => _controller.GetInputBuffer();
    
    public bool LeftPressed() => _controller.IsLeftPressed();
    public bool LeftReleased() => _controller.IsLeftReleased();
    public bool LeftHeld() => _controller.IsLeftHeld();
    
    public bool RightPressed() => _controller.IsRightPressed();
    public bool RightReleased() => _controller.IsRightReleased();
    public bool RightHeld() => _controller.IsRightHeld();

    public bool MiddlePressed() => _controller.IsMiddlePressed();
    public bool MiddleReleased() => _controller.IsMiddleReleased();
    public bool MiddleHeld() => _controller.IsMiddleHeld();

    public TextLayout GetTextLayout(string id) => _controller.GetTextLayout(id);
    public TextLayout GetTextLayout() => _controller.GetTextLayout(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to get a previous text layout."));

    public ElementBounds GetElementBounds(string id) => _controller.GetElementBounds(id);
    public ElementBounds GetElementBounds() => _controller.GetElementBounds(_currentElement.ID ?? throw new InvalidOperationException("Elements must have an ID to get previous element bounds."));
    
    public List<RenderCommand<TRendererData>> Build(double delta)
    {
        Delta = delta;
        Time += delta;
        
        //  Force close all elements
        while (_hasOpenElement)
        {
            CloseElement();
        }
        
        //  Perform sizing passes to resolve relative, fill, shrink, and wrapped text dimensions
        FillShrinkAndWrapElements();

        //  Create render commands out of all elements,
        //  performing a top-down positioning pass in the process.
        List<RenderCommand<TRendererData>> commands = PositionAndRenderElements();
        
        //  Process interactions in reverse render order to ensure top-most elements consume input first.
        for (int i = commands.Count - 1; i >= 0; i--)
        {
            RenderCommand<TRendererData> command = commands[i];
            if (command.ID == null)
            {
                continue;
            }

            if (command.Passthrough)
            {
                _controller.UpdateElementBounds(command.ID, command.Rect, command.ClipRect, command.ContentSize);
            }
            else
            {
                _controller.UpdateInteraction(command.ID, command.Rect, command.ClipRect, command.ContentSize, command.Scrollable);
            }
        }
        _controller.EndUpdate();

        //  Return pooled child lists
        for (var i = 0; i < _activeChildLists.Count; i++)
        {
            List<Element<TRendererData>> list = _activeChildLists[i];
            list.Clear();
            _childListPool.Push(list);
        }
        _activeChildLists.Clear();

        //  Reset state
        _hasOpenElement = false;
        _currentElement = default;
        _openElements.Clear();
        _closedRootElements.Clear();
        return commands;
    }

    private Scope OpenElement(Element<TRendererData> element)
    {
        if (_hasOpenElement)
        {
            if (element.Style.TextColor.W <= 0f)
            {
                element.Style.TextColor = _currentElement.Style.TextColor;
            }

            _openElements.Push(_currentElement);
            _currentElement = element;
            return new Scope(this);
        }
        
        _hasOpenElement = true;
        _currentElement = element;
        return new Scope(this);
    }

    private List<Element<TRendererData>> RentChildList()
    {
        List<Element<TRendererData>> list = _childListPool.Count > 0 ? _childListPool.Pop() : [];
        _activeChildLists.Add(list);
        return list;
    }
    
    /// <summary>
    ///     Closes the currently open element to complete it,
    ///     performing an initial bottom-up sizing pass in the process.
    /// </summary>
    private void CloseElement()
    {
        Element<TRendererData> element = _currentElement;

        //  Attempt to get the parent, if any, off the stack
        Element<TRendererData> parent = default;
        _hasOpenElement = _openElements.Count > 0;
        if (_hasOpenElement)
        {
            parent = _openElements.Pop();
        }

        //  Aggregate child dimensions now that all children and element properties are finalized
        ComputeChildrenMetrics(in element, out int childrenWidth, out int childrenHeight, out int childrenMinWidth, out int childrenMinHeight);

        //  Calculate non-wrapped text constraints
        if (element.Text != null)
        {
            RichText parsed = RichText.Parse(element.Text, 0, element.Text.Length);
            ReadOnlySpan<char> parsedSpan = parsed.Text.AsSpan();
            var wordStart = 0;
            while (wordStart < parsedSpan.Length && (parsedSpan[wordStart] == ' ' || parsedSpan[wordStart] == '\t' || parsedSpan[wordStart] == '\n'))
            {
                wordStart++;
            }

            int wordEnd = wordStart;
            while (wordEnd < parsedSpan.Length && parsedSpan[wordEnd] != ' ' && parsedSpan[wordEnd] != '\t' && parsedSpan[wordEnd] != '\n')
            {
                wordEnd++;
            }

            int firstWordStart = wordEnd > wordStart ? wordStart : 0;
            int firstWordLength = wordEnd > wordStart ? wordEnd - wordStart : parsedSpan.Length;
            
            TextConstraints firstWordConstraints = TextEngine.Measure(element.FontOptions, parsed.Text, firstWordStart, firstWordLength);
            TextLayout textLayout = TextEngine.Layout(element.FontOptions, element.Text);
            TextConstraints fullTextConstraints = textLayout.Constraints;

            element.TextLayout = textLayout;
            childrenWidth = Math.Max(childrenWidth, fullTextConstraints.PreferredWidth);
            childrenHeight = Math.Max(childrenHeight, fullTextConstraints.PreferredHeight);
            childrenMinWidth = Math.Max(childrenMinWidth, firstWordConstraints.MinWidth);
            childrenMinHeight = Math.Max(childrenMinHeight, firstWordConstraints.MinHeight);
        }

        element.ContentSize = new IntVector2(childrenWidth, childrenHeight);

        Padding padding = element.Style.Padding;
        int padX = padding.Left + padding.Right;
        int padY = padding.Top + padding.Bottom;

        IConstraint? widthConstraint = element.Constraints.Width;
        int width;
        if (widthConstraint == null || widthConstraint is Fit or Shrink)
        {
            width = childrenWidth + padX;
        }
        else if (widthConstraint is Fill)
        {
            width = !_hasOpenElement ? Width : 0;
        }
        else if (widthConstraint is Relative)
        {
            width = !_hasOpenElement ? widthConstraint.Calculate(Width) : 0;
        }
        else
        {
            width = widthConstraint.Calculate(Width);
        }

        IConstraint? heightConstraint = element.Constraints.Height;
        int height;
        if (heightConstraint == null || heightConstraint is Fit or Shrink)
        {
            height = childrenHeight + padY;
        }
        else if (heightConstraint is Fill)
        {
            height = !_hasOpenElement ? Height : 0;
        }
        else if (heightConstraint is Relative)
        {
            height = !_hasOpenElement ? heightConstraint.Calculate(Height) : 0;
        }
        else
        {
            height = heightConstraint.Calculate(Height);
        }

        //  Set min width and height if they are unassigned
        if (element.Constraints.MinWidth == 0)
        {
            if (widthConstraint is Shrink)
            {
                element.Constraints.MinWidth = element.Viewport.HorizontalScroll ? padX : childrenMinWidth + padX;
            }
            else if (widthConstraint == null || widthConstraint is Fit)
            {
                if (element.Text != null)
                {
                    element.Constraints.MinWidth = childrenMinWidth + padX;
                }
                else if (element.Viewport.HorizontalScroll)
                {
                    element.Constraints.MinWidth = padX;
                }
                else if (childrenMinWidth > 0)
                {
                    element.Constraints.MinWidth = childrenMinWidth + padX;
                }
                else
                {
                    element.Constraints.MinWidth = width;
                }
            }
            else if (widthConstraint is Fill)
            {
                element.Constraints.MinWidth = element.Viewport.HorizontalScroll ? padX : (childrenMinWidth > 0 ? childrenMinWidth + padX : 0);
            }
            else
            {
                element.Constraints.MinWidth = width;
            }
        }
        else if (widthConstraint == null || widthConstraint is Fit or Shrink or Fill)
        {
            element.Constraints.MinWidth = Math.Max(element.Constraints.MinWidth, padX);
        }

        if (element.Constraints.MinHeight == 0)
        {
            if (heightConstraint is Shrink)
            {
                element.Constraints.MinHeight = element.Viewport.VerticalScroll ? padY : childrenMinHeight + padY;
            }
            else if (heightConstraint == null || heightConstraint is Fit)
            {
                if (element.Text != null)
                {
                    element.Constraints.MinHeight = childrenMinHeight + padY;
                }
                else if (element.Viewport.VerticalScroll)
                {
                    element.Constraints.MinHeight = padY;
                }
                else if (childrenMinHeight > 0)
                {
                    element.Constraints.MinHeight = childrenMinHeight + padY;
                }
                else
                {
                    element.Constraints.MinHeight = height;
                }
            }
            else if (heightConstraint is Fill)
            {
                element.Constraints.MinHeight = element.Viewport.VerticalScroll ? padY : (childrenMinHeight > 0 ? childrenMinHeight + padY : 0);
            }
            else
            {
                element.Constraints.MinHeight = height;
            }
        }
        else if (heightConstraint == null || heightConstraint is Fit or Shrink or Fill)
        {
            element.Constraints.MinHeight = Math.Max(element.Constraints.MinHeight, padY);
        }

        if (element.Constraints.MaxWidth > 0)
        {
            element.Constraints.MinWidth = Math.Min(element.Constraints.MinWidth, element.Constraints.MaxWidth);
            width = Math.Min(width, element.Constraints.MaxWidth);
        }

        if (element.Constraints.MaxHeight > 0)
        {
            element.Constraints.MinHeight = Math.Min(element.Constraints.MinHeight, element.Constraints.MaxHeight);
            height = Math.Min(height, element.Constraints.MaxHeight);
        }

        if (!_hasOpenElement || widthConstraint is not Fill and not Relative)
        {
            width = Math.Max(width, element.Constraints.MinWidth);
        }

        if (!_hasOpenElement || heightConstraint is not Fill and not Relative)
        {
            height = Math.Max(height, element.Constraints.MinHeight);
        }

        element.Rect = new IntRect(element.Rect.Position, new IntVector2(width, height));

        if (_hasOpenElement)
        {
            parent.Children ??= RentChildList();
            parent.Children.Add(element);
            _currentElement = parent;
        }
        else
        {
            _closedRootElements.Add(element);
            _currentElement = default;
        }
    }

    private static void ComputeChildrenMetrics(
        in Element<TRendererData> element,
        out int childrenWidth,
        out int childrenHeight,
        out int childrenMinWidth,
        out int childrenMinHeight
    )
    {
        childrenWidth = 0;
        childrenHeight = 0;
        childrenMinWidth = 0;
        childrenMinHeight = 0;

        if (element.Children == null || element.Children.Count == 0)
        {
            return;
        }

        int childCount = element.Children.Count;
        int totalSpacing = childCount > 1 ? (childCount - 1) * element.Layout.Spacing : 0;

        for (var i = 0; i < childCount; i++)
        {
            Element<TRendererData> child = element.Children[i];
            switch (element.Layout.Direction)
            {
                case LayoutDirection.Horizontal:
                    childrenWidth += child.Rect.Size.X;
                    childrenHeight = Math.Max(childrenHeight, child.Rect.Size.Y);
                    childrenMinWidth += child.Constraints.MinWidth;
                    childrenMinHeight = Math.Max(childrenMinHeight, child.Constraints.MinHeight);
                    break;
                case LayoutDirection.Vertical:
                    childrenWidth = Math.Max(childrenWidth, child.Rect.Size.X);
                    childrenHeight += child.Rect.Size.Y;
                    childrenMinWidth = Math.Max(childrenMinWidth, child.Constraints.MinWidth);
                    childrenMinHeight += child.Constraints.MinHeight;
                    break;
                case LayoutDirection.None:
                    childrenWidth = Math.Max(childrenWidth, child.Rect.Size.X);
                    childrenHeight = Math.Max(childrenHeight, child.Rect.Size.Y);
                    childrenMinWidth = Math.Max(childrenMinWidth, child.Constraints.MinWidth);
                    childrenMinHeight = Math.Max(childrenMinHeight, child.Constraints.MinHeight);
                    break;
            }
        }

        switch (element.Layout.Direction)
        {
            case LayoutDirection.Horizontal:
                childrenWidth += totalSpacing;
                childrenMinWidth += totalSpacing;
                break;
            case LayoutDirection.Vertical:
                childrenHeight += totalSpacing;
                childrenMinHeight += totalSpacing;
                break;
        }
    }

    private static int ClampWidth(in Element<TRendererData> element, int width)
    {
        width = Math.Max(width, element.Constraints.MinWidth);
        if (element.Constraints.MaxWidth > 0)
        {
            width = Math.Min(width, element.Constraints.MaxWidth);
        }

        return Math.Max(0, width);
    }

    private static int ClampHeight(in Element<TRendererData> element, int height)
    {
        height = Math.Max(height, element.Constraints.MinHeight);
        if (element.Constraints.MaxHeight > 0)
        {
            height = Math.Min(height, element.Constraints.MaxHeight);
        }

        return Math.Max(0, height);
    }
    
    private List<RenderCommand<TRendererData>> PositionAndRenderElements()
    {
        List<RenderCommand<TRendererData>> commands = [];
        _overlayNodes.Clear();

        for (var i = 0; i < _closedRootElements.Count; i++)
        {
            _traversalStack.Clear();
            Element<TRendererData> root = _closedRootElements[i];
            _traversalStack.Push(new ElementNode(root, parent: null, childIndex: i));
            PositionAndRenderSubtree(commands, isOverlayPass: false);
        }

        //  Process any deferred overlay subtrees on top of normal elements
        for (var i = 0; i < _overlayNodes.Count; i++)
        {
            _traversalStack.Clear();
            _traversalStack.Push(_overlayNodes[i]);
            PositionAndRenderSubtree(commands, isOverlayPass: true);
        }
        _overlayNodes.Clear();
        
        return commands;
    }

    private void PositionAndRenderSubtree(List<RenderCommand<TRendererData>> commands, bool isOverlayPass)
    {
        while (_traversalStack.Count > 0)
        {
            ElementNode frame = _traversalStack.Pop();
            Element<TRendererData> element = frame.Element;

            if (element.Overlay && !isOverlayPass)
            {
                _overlayNodes.Add(frame);
                continue;
            }
    
            //  Apply position constraints
            int availableWidth = Width;
            int availableHeight = Height;
            if (frame.Parent != null)
            {
                availableWidth = frame.Parent.Value.Rect.Size.X - frame.Parent.Value.Style.Padding.Left - frame.Parent.Value.Style.Padding.Right;
                availableHeight = frame.Parent.Value.Rect.Size.Y - frame.Parent.Value.Style.Padding.Top - frame.Parent.Value.Style.Padding.Bottom;
            }
            
            int x = element.Constraints.X?.Calculate(availableWidth) ?? 0;
            int y = element.Constraints.Y?.Calculate(availableHeight) ?? 0;
            
            //  Apply anchoring. Top Left is default, so only need to apply Center/Right/Bottom.
            Anchors anchors = element.Constraints.Anchors;
            LayoutDirection parentLayoutDirection = frame.Parent?.Layout.Direction ?? LayoutDirection.None;
            
            bool anchorLocal = (anchors & Anchors.Local) == Anchors.Local;
            int layoutOffsetX = anchorLocal ? 0 : availableWidth;
            int layoutOffsetY = anchorLocal ? 0 : availableHeight;

            //  Horizontal anchoring when not in a horizontal layout
            if (parentLayoutDirection != LayoutDirection.Horizontal)
            {
                if ((anchors & Anchors.Right) == Anchors.Right)
                {
                    x += layoutOffsetX - element.Rect.Size.X;
                }
                else if ((anchors & Anchors.Center) == Anchors.Center && (anchors & Anchors.Left) != Anchors.Left)
                {
                    x += (layoutOffsetX - element.Rect.Size.X) / 2;
                }
            }
        
            //  Vertical anchoring when not in a vertical layout
            if (parentLayoutDirection != LayoutDirection.Vertical)
            {
                if ((anchors & Anchors.Bottom) == Anchors.Bottom)
                {
                    y += layoutOffsetY - element.Rect.Size.Y;
                }
                else if ((anchors & Anchors.Center) == Anchors.Center && (anchors & Anchors.Top) != Anchors.Top)
                {
                    y += (layoutOffsetY - element.Rect.Size.Y) / 2;
                }
            }
            
            //  Apply layout offsets
            if (frame.Parent != null)
            {
                x += frame.Parent.Value.Rect.Position.X + frame.LeftOffset;
                y += frame.Parent.Value.Rect.Position.Y + frame.TopOffset;
            }
            
            var position = new IntVector2(x, y);
            element.Rect = new IntRect(position, element.Rect.Size);
            
            //  Save any changes made to the element
            if (frame.Parent == null)
            {
                if (frame.ChildIndex >= 0 && frame.ChildIndex < _closedRootElements.Count)
                {
                    _closedRootElements[frame.ChildIndex] = element;
                }
            }
            else if (frame.Parent.Value.Children != null && frame.ChildIndex >= 0 && frame.ChildIndex < frame.Parent.Value.Children.Count)
            {
                frame.Parent.Value.Children[frame.ChildIndex] = element;
            }

            //  Don't issue render commands or continue processing any non-overlay
            //  elements entirely outside their parent's effective clip bounds
            if (!element.Overlay && frame.Parent != null)
            {
                IntRect parentBounds = frame.Parent.Value.Viewport.ClipRect ?? frame.Parent.Value.Rect;
                if (!parentBounds.Intersects(element.Rect))
                {
                    continue;
                }
            }

            IntRect clipRect;
            //  Overlays escape ancestor clipping and start unclipped in the viewport
            if (element.Overlay || frame.Parent == null)
            {
                clipRect = element.Rect;
            }
            //  Clip children by their parent
            else
            {
                clipRect = frame.Parent.Value.Viewport.ClipRect ?? frame.Parent.Value.Rect;
            }

            //  If this element has its own clipping, calculate an intersected clip rect
            if (element.Viewport.ClipConstraints != null)
            {
                UI.Constraints clipConstraints = element.Viewport.ClipConstraints.Value;
                int clipOffsetX = clipConstraints.X?.Calculate(element.Rect.Size.X) ?? 0;
                int clipOffsetY = clipConstraints.Y?.Calculate(element.Rect.Size.Y) ?? 0;
                int clipWidth = clipConstraints.Width?.Calculate(element.Rect.Size.X) ?? element.Rect.Size.X;
                int clipHeight = clipConstraints.Height?.Calculate(element.Rect.Size.Y) ?? element.Rect.Size.Y;
                var clipPos = new IntVector2(element.Rect.Position.X + clipOffsetX, element.Rect.Position.Y + clipOffsetY);
                var clipSize = new IntVector2(clipWidth, clipHeight);
    
                var explicitRect = new IntRect(clipPos, clipSize);

                //  Intersect with the parent's clip rect
                int clipX = Math.Max(explicitRect.Position.X, clipRect.Position.X);
                int clipY = Math.Max(explicitRect.Position.Y, clipRect.Position.Y);
                int right = Math.Min(explicitRect.Position.X + explicitRect.Size.X, clipRect.Position.X + clipRect.Size.X);
                int bottom = Math.Min(explicitRect.Position.Y + explicitRect.Size.Y, clipRect.Position.Y + clipRect.Size.Y);

                int width = Math.Max(0, right - clipX);
                int height = Math.Max(0, bottom - clipY);

                element.Viewport.ClipRect = new IntRect(new IntVector2(clipX, clipY), new IntVector2(width, height));
                
                //  Update the clip rect that will be used for rendering this element
                clipRect = element.Viewport.ClipRect.Value;
            }
            //  Otherwise inherit the parent's clip rect
            else
            {
                element.Viewport.ClipRect = clipRect;
            }

            Vector4 color = element.Style.Color;
            Vector4 backgroundColor = element.Style.BackgroundColor;
            if (element.Debug || _globalDebug)
            {
                Random? rand = null;

                if (element.Style.Color.W == 0d)
                {
                    rand ??= new Random(element.ID?.GetHashCode() ?? element.Rect.GetHashCode());
                    color = new Vector4(rand.NextSingle(), rand.NextSingle(), rand.NextSingle(), 0.25f);
                }
                
                if (element.Style.BackgroundColor.W == 0d)
                {
                    rand ??= new Random(element.ID?.GetHashCode() ?? element.Rect.GetHashCode());
                    backgroundColor = new Vector4(rand.NextSingle(), rand.NextSingle(), rand.NextSingle(), 0.25f);
                }
            }
            
            bool scrollable = element.Viewport.VerticalScroll || element.Viewport.HorizontalScroll;
            var command = new RenderCommand<TRendererData>
            (
                element.ID,
                element.Rect,
                clipRect,
                color,
                backgroundColor,
                element.Style.CornerRadius,
                element.Style.Border,
                element.Style.CornerCaps,
                element.FontOptions,
                element.Text,
                element.Passthrough,
                element.TextureData,
                element.TextLayout,
                element.ContentSize,
                scrollable
            );
            commands.Add(command);
            
            //  Push any children to be processed
            if (element.Children == null)
            {
                continue;
            }

            int leftOffset = element.Style.Padding.Left;
            int topOffset = element.Style.Padding.Top;
            
            //  Apply viewport offset
            leftOffset += element.Viewport.Offset.X;
            topOffset += element.Viewport.Offset.Y;

            _childStack.Clear();
            
            for (var childIndex = 0; childIndex < element.Children.Count; childIndex++)
            {
                Element<TRendererData> child = element.Children[childIndex];
                
                //  Propagate the debug flag to children
                child.Debug |= element.Debug;
                
                _childStack.Push(new ElementNode(child, element, childIndex)
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

            //  Push children in reverse so the first child is positioned first
            //  and later siblings paint over earlier siblings.
            while (_childStack.Count > 0)
            {
                _traversalStack.Push(_childStack.Pop());
            }
        }
    }
    
    private void FillShrinkAndWrapElements() 
    {
        //  Pass 1 (Top-Down): Resolve relative widths, horizontal fill, horizontal shrink, and text wrapping.
        _postOrderNodes.Clear();
        for (var i = 0; i < _closedRootElements.Count; i++)
        {
            _traversalStack.Clear();
            Element<TRendererData> root = _closedRootElements[i];
            if (root.Text != null)
            {
                WrapElementText(ref root, Width);
                _closedRootElements[i] = root;
            }

            _traversalStack.Push(new ElementNode(root, parent: null, childIndex: i));
        
            while (_traversalStack.Count > 0)
            {
                ElementNode frame = _traversalStack.Pop();
                Element<TRendererData> element = frame.Element;
        
                if (element.Children != null && element.Children.Count > 0)
                {
                    int availableWidth = Math.Max(0, element.Rect.Size.X - element.Style.Padding.Left - element.Style.Padding.Right);
                    
                    //  Resolve relative widths on children
                    for (var childIndex = 0; childIndex < element.Children.Count; childIndex++)
                    {
                        Element<TRendererData> child = element.Children[childIndex];
                        if (child.Constraints.Width is Relative relWidth)
                        {
                            int childWidth = ClampWidth(in child, relWidth.Calculate(availableWidth));
                            child.Rect = new IntRect(child.Rect.Position, new IntVector2(childWidth, child.Rect.Size.Y));
                            if (child.Constraints.MinWidth == 0)
                            {
                                child.Constraints.MinWidth = childWidth;
                            }
                            element.Children[childIndex] = child;
                        }
                    }

                    FillChildrenWidth(ref element);
                    ShrinkChildrenWidth(ref element);
                    WrapTextChildren(ref element);
                }

                SaveElement(in frame, in element);
                _postOrderNodes.Add(new ElementNode(element, frame.Parent, frame.ChildIndex));

                if (element.Children == null)
                {
                    continue;
                }

                for (int childIndex = element.Children.Count - 1; childIndex >= 0; childIndex--)
                {
                    Element<TRendererData> child = element.Children[childIndex];
                    _traversalStack.Push(new ElementNode(child, element, childIndex));
                }
            }
        }

        //  Pass 2 (Bottom-Up): Reconcile auto-sized parent dimensions and ContentSize after text wrapping.
        for (int n = _postOrderNodes.Count - 1; n >= 0; n--)
        {
            ElementNode frame = _postOrderNodes[n];
            Element<TRendererData> element = GetLatestElement(in frame);
            if (element.Children == null || element.Children.Count == 0)
            {
                continue;
            }

            ComputeChildrenMetrics(in element, out int childrenWidth, out int childrenHeight, out int childrenMinWidth, out int childrenMinHeight);
            element.ContentSize = new IntVector2(childrenWidth, childrenHeight);

            Padding pad = element.Style.Padding;
            int padX = pad.Left + pad.Right;
            int padY = pad.Top + pad.Bottom;

            int width = element.Rect.Size.X;
            int height = element.Rect.Size.Y;

            IConstraint? widthConstraint = element.Constraints.Width;
            if ((widthConstraint == null || widthConstraint is Fit or Shrink) && !element.Viewport.HorizontalScroll)
            {
                int targetWidth = ClampWidth(in element, childrenWidth + padX);
                if (frame.Parent != null && !frame.Parent.Value.Viewport.HorizontalScroll)
                {
                    //  Do not re-expand past a width constraint already imposed by the parent in Pass 1.
                    targetWidth = Math.Min(targetWidth, Math.Max(width, childrenMinWidth + padX));
                }
                width = targetWidth;
            }

            IConstraint? heightConstraint = element.Constraints.Height;
            if ((heightConstraint == null || heightConstraint is Fit or Shrink) && !element.Viewport.VerticalScroll)
            {
                if (heightConstraint == null || heightConstraint is Fit)
                {
                    element.Constraints.MinHeight = Math.Max(element.Constraints.MinHeight, childrenMinHeight + padY);
                }
                height = ClampHeight(in element, childrenHeight + padY);
            }

            element.Rect = new IntRect(element.Rect.Position, new IntVector2(width, height));
            SaveElement(in frame, in element);
        }

        //  Pass 3 (Top-Down): Resolve relative heights, vertical fill, vertical shrink, and align text layouts.
        for (var i = 0; i < _closedRootElements.Count; i++)
        {
            _traversalStack.Clear();
            _traversalStack.Push(new ElementNode(_closedRootElements[i], parent: null, childIndex: i));

            while (_traversalStack.Count > 0)
            {
                ElementNode frame = _traversalStack.Pop();
                Element<TRendererData> element = frame.Element;

                if (element.Children != null && element.Children.Count > 0)
                {
                    int availableHeight = Math.Max(0, element.Rect.Size.Y - element.Style.Padding.Top - element.Style.Padding.Bottom);

                    //  Resolve relative heights on children
                    for (var childIndex = 0; childIndex < element.Children.Count; childIndex++)
                    {
                        Element<TRendererData> child = element.Children[childIndex];
                        if (child.Constraints.Height is Relative relHeight)
                        {
                            int childHeight = ClampHeight(in child, relHeight.Calculate(availableHeight));
                            child.Rect = new IntRect(child.Rect.Position, new IntVector2(child.Rect.Size.X, childHeight));
                            if (child.Constraints.MinHeight == 0)
                            {
                                child.Constraints.MinHeight = childHeight;
                            }
                            element.Children[childIndex] = child;
                        }
                    }

                    FillChildrenHeight(ref element);
                    ShrinkChildrenHeight(ref element);

                    //  Update ContentSize after final child sizing
                    ComputeChildrenMetrics(in element, out int childrenWidth, out int childrenHeight, out _, out _);
                    element.ContentSize = new IntVector2(childrenWidth, childrenHeight);

                    //  If cross-axis children grew, ensure auto-sized container encompasses them
                    Padding pad = element.Style.Padding;
                    bool canResizeWidth = (element.Constraints.Width == null || element.Constraints.Width is Fit) && !element.Viewport.HorizontalScroll && element.Layout.Direction != LayoutDirection.Horizontal;
                    bool canResizeHeight = (element.Constraints.Height == null || element.Constraints.Height is Fit) && !element.Viewport.VerticalScroll && element.Layout.Direction != LayoutDirection.Vertical;
                    if (canResizeWidth || canResizeHeight)
                    {
                        int width = canResizeWidth ? ClampWidth(in element, Math.Max(element.Rect.Size.X, childrenWidth + pad.Left + pad.Right)) : element.Rect.Size.X;
                        int height = canResizeHeight ? ClampHeight(in element, Math.Max(element.Rect.Size.Y, childrenHeight + pad.Top + pad.Bottom)) : element.Rect.Size.Y;
                        element.Rect = new IntRect(element.Rect.Position, new IntVector2(width, height));
                    }
                }

                FinalizeTextLayout(ref element);
                SaveElement(in frame, in element);

                if (element.Children == null)
                {
                    continue;
                }

                for (int childIndex = element.Children.Count - 1; childIndex >= 0; childIndex--)
                {
                    Element<TRendererData> child = element.Children[childIndex];
                    _traversalStack.Push(new ElementNode(child, element, childIndex));
                }
            }
        }
    }

    private Element<TRendererData> GetLatestElement(in ElementNode frame)
    {
        if (frame.Parent == null)
        {
            return _closedRootElements[frame.ChildIndex];
        }

        return frame.Parent.Value.Children![frame.ChildIndex];
    }

    private void SaveElement(in ElementNode frame, in Element<TRendererData> element)
    {
        if (frame.Parent == null)
        {
            _closedRootElements[frame.ChildIndex] = element;
        }
        else if (frame.Parent.Value.Children != null)
        {
            frame.Parent.Value.Children[frame.ChildIndex] = element;
        }
    }

    private void FillChildrenWidth(ref Element<TRendererData> parent)
    {
        if (parent.Children == null || parent.Children.Count == 0)
        {
            return;
        }

        int availableWidth = Math.Max(0, parent.Rect.Size.X - parent.Style.Padding.Left - parent.Style.Padding.Right);

        if (parent.Layout.Direction != LayoutDirection.Horizontal)
        {
            for (var i = 0; i < parent.Children.Count; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                if (child.Constraints.Width is not Fill)
                {
                    continue;
                }

                int width = ClampWidth(in child, availableWidth);
                child.Rect = new IntRect(child.Rect.Position, new IntVector2(width, child.Rect.Size.Y));
                parent.Children[i] = child;
            }

            return;
        }

        var numHorizontalFillChildren = 0;
        for (var i = 0; i < parent.Children.Count; i++)
        {
            Element<TRendererData> child = parent.Children[i];
            if (child.Constraints.Width is Fill)
            {
                int initialWidth = ClampWidth(in child, child.Constraints.MinWidth);
                child.Rect = new IntRect(child.Rect.Position, new IntVector2(initialWidth, child.Rect.Size.Y));
                parent.Children[i] = child;
                availableWidth -= initialWidth;
                numHorizontalFillChildren++;
            }
            else
            {
                availableWidth -= child.Rect.Size.X;
            }
        }

        if (numHorizontalFillChildren == 0)
        {
            return;
        }

        int childCount = parent.Children.Count;
        int totalSpacing = childCount > 1 ? (childCount - 1) * parent.Layout.Spacing : 0;
        availableWidth -= totalSpacing;

        DistributeFillChildren(parent, ref availableWidth, fillHorizontal: true);
    }

    private void FillChildrenHeight(ref Element<TRendererData> parent)
    {
        if (parent.Children == null || parent.Children.Count == 0)
        {
            return;
        }

        int availableHeight = Math.Max(0, parent.Rect.Size.Y - parent.Style.Padding.Top - parent.Style.Padding.Bottom);

        if (parent.Layout.Direction != LayoutDirection.Vertical)
        {
            for (var i = 0; i < parent.Children.Count; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                if (child.Constraints.Height is not Fill)
                {
                    continue;
                }

                int height = ClampHeight(in child, availableHeight);
                child.Rect = new IntRect(child.Rect.Position, new IntVector2(child.Rect.Size.X, height));
                parent.Children[i] = child;
            }

            return;
        }

        var numVerticalFillChildren = 0;
        for (var i = 0; i < parent.Children.Count; i++)
        {
            Element<TRendererData> child = parent.Children[i];
            if (child.Constraints.Height is Fill)
            {
                int initialHeight = ClampHeight(in child, child.Constraints.MinHeight);
                child.Rect = new IntRect(child.Rect.Position, new IntVector2(child.Rect.Size.X, initialHeight));
                parent.Children[i] = child;
                availableHeight -= initialHeight;
                numVerticalFillChildren++;
            }
            else
            {
                availableHeight -= child.Rect.Size.Y;
            }
        }

        if (numVerticalFillChildren == 0)
        {
            return;
        }

        int childCount = parent.Children.Count;
        int totalSpacing = childCount > 1 ? (childCount - 1) * parent.Layout.Spacing : 0;
        availableHeight -= totalSpacing;

        DistributeFillChildren(parent, ref availableHeight, fillHorizontal: false);
    }

    private static void DistributeFillChildren(Element<TRendererData> parent, ref int available, bool fillHorizontal)
    {
        while (available > 0 && parent.Children != null)
        {
            var smallest = int.MaxValue;
            var secondSmallest = int.MaxValue;
            var smallestCount = 0;

            for (var i = 0; i < parent.Children.Count; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                IConstraint? constraint = fillHorizontal ? child.Constraints.Width : child.Constraints.Height;
                if (constraint is not Fill)
                {
                    continue;
                }

                int size = fillHorizontal ? child.Rect.Size.X : child.Rect.Size.Y;
                int maxSize = fillHorizontal ? child.Constraints.MaxWidth : child.Constraints.MaxHeight;
                if (maxSize > 0 && size >= maxSize)
                {
                    continue;
                }

                if (size < smallest)
                {
                    secondSmallest = smallest;
                    smallest = size;
                    smallestCount = 1;
                }
                else if (size == smallest)
                {
                    smallestCount++;
                }
                else if (size < secondSmallest)
                {
                    secondSmallest = size;
                }
            }

            if (smallestCount == 0)
            {
                break;
            }

            int toAdd = secondSmallest < int.MaxValue ? secondSmallest - smallest : available;
            toAdd = Math.Min(toAdd, available / smallestCount);
            toAdd = Math.Max(toAdd, 1);

            for (var i = 0; i < parent.Children.Count && available > 0; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                IConstraint? constraint = fillHorizontal ? child.Constraints.Width : child.Constraints.Height;
                if (constraint is not Fill)
                {
                    continue;
                }

                int size = fillHorizontal ? child.Rect.Size.X : child.Rect.Size.Y;
                int maxSize = fillHorizontal ? child.Constraints.MaxWidth : child.Constraints.MaxHeight;
                if (size != smallest || (maxSize > 0 && size >= maxSize))
                {
                    continue;
                }

                int step = Math.Min(toAdd, available);
                if (maxSize > 0)
                {
                    step = Math.Min(step, maxSize - size);
                }

                size += step;
                if (fillHorizontal)
                {
                    child.Rect = new IntRect(child.Rect.Position, new IntVector2(size, child.Rect.Size.Y));
                }
                else
                {
                    child.Rect = new IntRect(child.Rect.Position, new IntVector2(child.Rect.Size.X, size));
                }
                parent.Children[i] = child;

                available -= step;
            }
        }
    }

    private void ShrinkChildrenWidth(ref Element<TRendererData> parent)
    {
        if (parent.Children == null || parent.Children.Count == 0 || parent.Viewport.HorizontalScroll)
        {
            return;
        }

        int contentWidth = Math.Max(0, parent.Rect.Size.X - parent.Style.Padding.Left - parent.Style.Padding.Right);

        //  Cross-axis shrinking: clamp each overflowing shrinkable child directly to contentWidth
        if (parent.Layout.Direction != LayoutDirection.Horizontal)
        {
            for (var i = 0; i < parent.Children.Count; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                if (child.Rect.Size.X > contentWidth && child.Constraints.MinWidth < child.Rect.Size.X)
                {
                    int newWidth = Math.Max(contentWidth, child.Constraints.MinWidth);
                    child.Rect = new IntRect(child.Rect.Position, new IntVector2(newWidth, child.Rect.Size.Y));
                    parent.Children[i] = child;
                }
            }

            return;
        }

        int childCount = parent.Children.Count;
        int usedWidth = childCount > 1 ? (childCount - 1) * parent.Layout.Spacing : 0;
        for (var i = 0; i < childCount; i++)
        {
            usedWidth += parent.Children[i].Rect.Size.X;
        }

        int deficit = usedWidth - contentWidth;
        while (deficit > 0)
        {
            int largest = -1;
            var secondLargest = 0;
            var largestCount = 0;

            for (var i = 0; i < childCount; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                int size = child.Rect.Size.X;
                if (child.Constraints.MinWidth >= size)
                {
                    continue;
                }

                if (size > largest)
                {
                    secondLargest = largest > 0 ? largest : 0;
                    largest = size;
                    largestCount = 1;
                }
                else if (size == largest)
                {
                    largestCount++;
                }
                else if (size > secondLargest)
                {
                    secondLargest = size;
                }
            }

            if (largestCount == 0 || largest < 0)
            {
                break;
            }

            int maxStep = secondLargest > 0 ? largest - secondLargest : deficit;
            int toRemove = Math.Min(maxStep, deficit / largestCount);
            toRemove = Math.Max(toRemove, 1);

            for (var i = 0; i < childCount && deficit > 0; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                int oldWidth = child.Rect.Size.X;
                if (oldWidth != largest || child.Constraints.MinWidth >= oldWidth)
                {
                    continue;
                }

                int step = Math.Min(toRemove, deficit);
                int newWidth = Math.Max(oldWidth - step, child.Constraints.MinWidth);
                int removed = oldWidth - newWidth;

                child.Rect = new IntRect(child.Rect.Position, new IntVector2(newWidth, child.Rect.Size.Y));
                parent.Children[i] = child;
                deficit -= removed;
            }
        }
    }

    private void ShrinkChildrenHeight(ref Element<TRendererData> parent)
    {
        if (parent.Children == null || parent.Children.Count == 0 || parent.Viewport.VerticalScroll)
        {
            return;
        }

        int contentHeight = Math.Max(0, parent.Rect.Size.Y - parent.Style.Padding.Top - parent.Style.Padding.Bottom);

        //  Cross-axis shrinking: clamp each overflowing shrinkable child directly to contentHeight
        if (parent.Layout.Direction != LayoutDirection.Vertical)
        {
            for (var i = 0; i < parent.Children.Count; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                if (child.Rect.Size.Y > contentHeight && child.Constraints.MinHeight < child.Rect.Size.Y)
                {
                    int newHeight = Math.Max(contentHeight, child.Constraints.MinHeight);
                    child.Rect = new IntRect(child.Rect.Position, new IntVector2(child.Rect.Size.X, newHeight));
                    parent.Children[i] = child;
                }
            }

            return;
        }

        int childCount = parent.Children.Count;
        int usedHeight = childCount > 1 ? (childCount - 1) * parent.Layout.Spacing : 0;
        for (var i = 0; i < childCount; i++)
        {
            usedHeight += parent.Children[i].Rect.Size.Y;
        }

        int deficit = usedHeight - contentHeight;
        while (deficit > 0)
        {
            int largest = -1;
            var secondLargest = 0;
            var largestCount = 0;

            for (var i = 0; i < childCount; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                int size = child.Rect.Size.Y;
                if (child.Constraints.MinHeight >= size)
                {
                    continue;
                }

                if (size > largest)
                {
                    secondLargest = largest > 0 ? largest : 0;
                    largest = size;
                    largestCount = 1;
                }
                else if (size == largest)
                {
                    largestCount++;
                }
                else if (size > secondLargest)
                {
                    secondLargest = size;
                }
            }

            if (largestCount == 0 || largest < 0)
            {
                break;
            }

            int maxStep = secondLargest > 0 ? largest - secondLargest : deficit;
            int toRemove = Math.Min(maxStep, deficit / largestCount);
            toRemove = Math.Max(toRemove, 1);

            for (var i = 0; i < childCount && deficit > 0; i++)
            {
                Element<TRendererData> child = parent.Children[i];
                int oldHeight = child.Rect.Size.Y;
                if (oldHeight != largest || child.Constraints.MinHeight >= oldHeight)
                {
                    continue;
                }

                int step = Math.Min(toRemove, deficit);
                int newHeight = Math.Max(oldHeight - step, child.Constraints.MinHeight);
                int removed = oldHeight - newHeight;

                child.Rect = new IntRect(child.Rect.Position, new IntVector2(child.Rect.Size.X, newHeight));
                parent.Children[i] = child;
                deficit -= removed;
            }
        }
    }

    private void WrapTextChildren(ref Element<TRendererData> parent)
    {
        if (parent.Children == null)
        {
            return;
        }

        int contentWidth = Math.Max(0, parent.Rect.Size.X - parent.Style.Padding.Left - parent.Style.Padding.Right);
        int maxChildWidth = parent.Viewport.HorizontalScroll ? int.MaxValue : contentWidth;

        for (var i = 0; i < parent.Children.Count; i++)
        {
            Element<TRendererData> child = parent.Children[i];
            if (child.Text == null)
            {
                continue;
            }
            
            WrapElementText(ref child, maxChildWidth);
            parent.Children[i] = child;
        }
    }

    private void WrapElementText(ref Element<TRendererData> element, int availableParentWidth)
    {
        if (element.Text == null)
        {
            return;
        }

        Padding pad = element.Style.Padding;
        int padX = pad.Left + pad.Right;
        int padY = pad.Top + pad.Bottom;

        int maxOuterWidth = Math.Min(element.Rect.Size.X, availableParentWidth);
        if (element.Constraints.MaxWidth > 0)
        {
            maxOuterWidth = Math.Min(maxOuterWidth, element.Constraints.MaxWidth);
        }

        int maxTextWidth = Math.Max(0, maxOuterWidth - padX);
        TextLayout textLayout = TextEngine.Layout(element.FontOptions, element.Text, maxTextWidth);
        element.TextLayout = textLayout;
        element.ContentSize = new IntVector2(textLayout.Constraints.PreferredWidth, textLayout.Constraints.PreferredHeight);

        int width = element.Rect.Size.X;
        IConstraint? widthConstraint = element.Constraints.Width;
        if (widthConstraint == null || widthConstraint is Fit or Shrink)
        {
            width = ClampWidth(in element, textLayout.Constraints.MinWidth + padX);
        }

        int wrappedHeight = textLayout.Constraints.PreferredHeight + padY;
        int height = element.Rect.Size.Y;
        IConstraint? heightConstraint = element.Constraints.Height;
        if (heightConstraint == null || heightConstraint is Fit)
        {
            element.Constraints.MinHeight = Math.Max(element.Constraints.MinHeight, wrappedHeight);
            height = ClampHeight(in element, wrappedHeight);
        }
        else if (heightConstraint is Shrink)
        {
            height = ClampHeight(in element, wrappedHeight);
        }

        element.Rect = new IntRect(element.Rect.Position, new IntVector2(width, height));
    }

    private void FinalizeTextLayout(ref Element<TRendererData> element)
    {
        if (element.Text == null)
        {
            if (element.ID != null)
            {
                _controller.UpdateTextLayout(element.ID, TextLayout.Empty);
            }

            return;
        }

        TextLayout layout = element.TextLayout ?? TextEngine.Layout(element.FontOptions, element.Text, element.Rect.Size.X);
        Padding pad = element.Style.Padding;
        int innerWidth = Math.Max(0, element.Rect.Size.X - pad.Left - pad.Right);
        int innerHeight = Math.Max(0, element.Rect.Size.Y - pad.Top - pad.Bottom);

        int offsetX = pad.Left + element.FontOptions.HorizontalAlignment switch
        {
            HorizontalAlignment.Center => (innerWidth - layout.Constraints.PreferredWidth) / 2,
            HorizontalAlignment.Right => innerWidth - layout.Constraints.PreferredWidth,
            _ => 0,
        };

        int offsetY = pad.Top + element.FontOptions.VerticalAlignment switch
        {
            VerticalAlignment.Center => (innerHeight - layout.Constraints.PreferredHeight) / 2,
            VerticalAlignment.Bottom => innerHeight - layout.Constraints.PreferredHeight,
            _ => 0,
        };

        if (offsetX != 0 || offsetY != 0)
        {
            var offsetGlyphs = new GlyphLayout[layout.Glyphs.Length];
            for (var i = 0; i < layout.Glyphs.Length; i++)
            {
                GlyphLayout glyph = layout.Glyphs[i];
                var movedBbox = new IntRect(
                    glyph.BBOX.Left + offsetX,
                    glyph.BBOX.Top + offsetY,
                    glyph.BBOX.Right + offsetX,
                    glyph.BBOX.Bottom + offsetY
                );
                offsetGlyphs[i] = new GlyphLayout(movedBbox, glyph.UV, glyph.Color, glyph.IsWhitespace);
            }

            layout = new TextLayout(layout.Constraints, offsetGlyphs, layout.Lines, layout.LineHeight);
            element.TextLayout = layout;
        }

        if (element.ID != null)
        {
            _controller.UpdateTextLayout(element.ID, layout);
        }
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
