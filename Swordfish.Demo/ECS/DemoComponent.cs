using System.ComponentModel;
using Swordfish.ECS;

namespace Swordfish.Demo.ECS;

[Component]
public partial class DemoComponent
{
    public static int Index { get; set; }

    public int Value;
}
