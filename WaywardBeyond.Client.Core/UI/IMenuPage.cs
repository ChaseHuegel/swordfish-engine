using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI;

internal interface IMenuPage<TIdentifier> 
    where TIdentifier : notnull
{
    TIdentifier ID { get; }
    
    Result RenderPage(double delta, UIBuilder<Material> ui, Menu<TIdentifier> menu);
}