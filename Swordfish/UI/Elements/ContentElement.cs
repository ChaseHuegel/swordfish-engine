using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ImGuiNET;
using Swordfish.Library.Types;

namespace Swordfish.UI.Elements;

public abstract class ContentElement : Element, IContentElement
{
    public virtual ContentSeparator ContentSeparator { get; set; }

    public LockedObservableCollection<IElement> Content { get; } = new();

    public ContentElement() : base()
    {
        Content.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null && e.Action != NotifyCollectionChangedAction.Move)
        {
            foreach (IElement element in e.OldItems)
                element.Parent = null;
        }

        if (e.NewItems != null)
        {
            foreach (IElement element in e.NewItems)
                element.Parent = this;
        }
    }

    protected override void OnRender()
    {
        foreach (var element in Content)
        {
            element.Render();
            RenderContentSeparator();
        };
    }

    protected virtual void RenderContentSeparator()
    {
        switch (ContentSeparator)
        {
            case ContentSeparator.DIVIDER:
                ImGui.Separator();
                break;
            case ContentSeparator.SPACER:
                ImGui.Spacing();
                break;
            default:
                break;
        }
    }
}
