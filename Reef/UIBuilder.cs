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
        
        //  Build commands from the closed elements
        for (int i = 0; i < _closedRootElements.Count; i++)
        {
            UIElement rootElement = _closedRootElements[i];
            
        }
     
        //  Reset state
        _openElements.Clear();
        _closedRootElements.Clear();
        return [];
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
        _hasOpenElement = _openElements.Count > 0;
        if (_hasOpenElement)
        {
            _currentElement = _openElements.Pop();
            _currentElement.Children ??= [];
            _currentElement.Children.Add(_currentElement);
        }
        else
        {
            _currentElement = default;
            _closedRootElements.Add(_currentElement);
        }
    }
}