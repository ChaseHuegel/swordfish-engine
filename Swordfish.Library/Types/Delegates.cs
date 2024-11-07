// ReSharper disable UnusedType.Global
namespace Swordfish.Library.Types;

public delegate T ReturnAction<T>(T a);

public delegate void OutAction<T>(out T a);