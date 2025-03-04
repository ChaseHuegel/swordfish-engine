using System;
using System.Threading;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Extensions;

public static class SynchronizationContextExtensions
{
    public static void Wait(this SynchronizationContext context)
    {
        context.Send(Callback, null);
        static void Callback(object state) { }
    }

    public static void WaitFor(this SynchronizationContext context, Action action)
    {
        context.Send(Callback, null);
        void Callback(object state) => action();
    }

    public static TResult WaitForResult<TResult>(this SynchronizationContext context, Func<TResult> factory)
    {
        TResult result = default;
        context.Send(Callback, null);
        void Callback(object state) => result = factory();
        return result;
    }

    public static TResult WaitForResult<TResult, TArg>(this SynchronizationContext context, Func<TArg, TResult> factory, TArg arg)
    {
        TResult result = default;
        context.Send(Callback, arg);
        void Callback(object state) => result = factory(arg);
        return result;
    }
}