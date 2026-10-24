using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.UI;

internal interface IMenuPage<TIdentifier> 
    where TIdentifier : notnull
{
    TIdentifier ID { get; }

    /// <summary>When true, the page renders inside the menu's full content area.</summary>
    bool UsesFullContentArea => false;

    /// <summary>Runs when the menu makes this page current.</summary>
    void OnEnter() { }

    /// <summary>Runs when the menu leaves this page.</summary>
    void OnExit() { }
    
    Result RenderPage(double delta, UIBuilder<Material> ui, Menu<TIdentifier> menu);
}