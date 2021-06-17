namespace Swordfish.Delegates
{
    /// <summary>
    /// Delegate which returns a value
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="a"></param>
    /// <returns>value T</returns>
    public delegate T ReturnAction<T>(T a);

    /// <summary>
    /// Delegate which outs a value
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="a"></param>
    public delegate void OutAction<T>(out T a);
}
