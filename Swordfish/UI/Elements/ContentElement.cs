using System.Collections.Specialized;
using ImGuiNET;
using Swordfish.Library.Collections;

namespace Swordfish.UI.Elements;

public abstract class ContentElement : Element, IContentElement
{
    public virtual ContentSeparator ContentSeparator { get; set; }

    public LockedObservableCollection<IElement> Content { get; } = [];

    public bool AutoScroll { get; set; }

    protected ContentElement()
    {
        Content.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null && e.Action != NotifyCollectionChangedAction.Move)
        {
            foreach (IElement element in e.OldItems)
            {
                element.Parent = null;

                if (element is IInternalElement internalElement)
                {
                    internalElement.SetUIContext(null);
                }
            }
        }

        if (e.NewItems == null)
        {
            return;
        }

        foreach (IElement element in e.NewItems)
        {
            element.Parent = this;
            
            if (element is IInternalElement internalElement)
            {
                internalElement.SetUIContext(UIContext);
            }
        }
    }

    protected override void OnRender()
    {
        Content.ForEach(RenderItem);

        void RenderItem(IElement element)
        {
            element.Render();
            RenderContentSeparator();
        }

        if (AutoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
        {
            ImGui.SetScrollHereY(1f);
        }
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
        }
    }
}
