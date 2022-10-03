using System;
using System.ComponentModel;
using Ninject;
using Swordfish.ECS;

namespace Swordfish.Demo.ECS;

[Component]
public partial class DemoComponent
{
    public static int Index { get; set; }

    public static byte Property { get; set; } = 11;
    public static uint Getter { get; } = 32;
    public static float Setter { private get; set; } = 154.32f;

    public static double Static = 0.343d;
    public const string Const = "Hello World";
    public readonly bool Read = true;
    public int Field = 0;

    private static string? PrivateProperty { get; set; } = null;
    private static IECSContext PrivateGetter { get; } = SwordfishEngine.Kernel.Get<IECSContext>();
    private static Entity PrivateStatic = new();
    private const long PrivateConst = 341345;
    private readonly byte[] PrivateRead = new byte[10];
    private DateTime PrivateField = DateTime.Now;
}
