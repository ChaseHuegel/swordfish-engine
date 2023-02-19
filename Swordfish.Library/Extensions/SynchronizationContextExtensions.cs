using System;
using System.Threading;

namespace Swordfish.Library.Extensions
{
    public static class SynchronizationContextExtensions
    {
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
    }
}