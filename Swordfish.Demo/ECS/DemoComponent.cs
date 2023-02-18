using System;
using System.ComponentModel;
using Needlefish;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Reflection;

namespace Swordfish.Demo.ECS;

[Component]
public partial class DemoComponent
{
    //  These should not appear in the inspector since they aren't instanced.
    public static int Index { get; set; }
    [DataField] public static byte Property { get; set; } = 11;
    public static uint Getter { get; } = 32;
    public static float Setter { private get; set; } = 154.32f;
    public static string PrivateSetter { get; private set; } = "This has a public getter and private setter.";
    public const string Const = "Hello World";
    public static double Static = 0.343d;

    //  These should not appear in the inspector since they aren't instanced.
    [DataField] private static string? PrivateProperty { get; set; } = null;
    private static IPath PrivateGetter { get; }
    private static Entity PrivateStatic = new();
    private const long PrivateConst = 341345;

    //  These should appear in the inspector since they are instanced.
    public readonly bool Read = true;
    [MemberOrder(0)] public int Field = 0;  //  This should appear first in the inspector.

    //  These should appear in the inspector since they are instanced DataFields.
    [DataField] private readonly byte[] PrivateRead = new byte[10];
    [DataField] private DateTime PrivateField = DateTime.Now;
}
