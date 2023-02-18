using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DryIoc;

namespace Swordfish.Library.Collections
{
    public class Kernel
    {
        private readonly Container BaseResolver;

        public Kernel(Container baseResolver)
        {
            BaseResolver = baseResolver;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<TInterface> GetAll<TInterface>() where TInterface : class
        {
            return BaseResolver.ResolveMany<TInterface>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TInterface Get<TInterface>() where TInterface : class
        {
            return BaseResolver.Resolve<TInterface>();
        }
    }
}