using System.ComponentModel;
using System;

namespace Swordfish.Library.Extensions
{
    public static class EventHandlerExtensions
    {
        /// <summary>
        ///     Invokes a cancellable <see cref="EventHandler"/>.
        /// </summary>
        /// <param name="sender">the sender of the event.</param>
        /// <param name="args">the event args to pass to subscribers, which must be <see cref="CancelEventArgs"/>.</param>
        /// <returns>true if the event is successful; otherwise false if any subscriber cancelled the event.</returns>
        public static bool TryInvoke<TEventArgs>(this EventHandler<TEventArgs> handler, object sender, TEventArgs args) where TEventArgs : CancelEventArgs
        {
            foreach (EventHandler<TEventArgs> invoker in handler.GetInvocationList())
            {
                invoker.Invoke(sender, args);
                if (args.Cancel)
                    return false;
            }

            return true;
        }
    }
}
