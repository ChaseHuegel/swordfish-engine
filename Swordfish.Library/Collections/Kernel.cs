using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DryIoc;

namespace Swordfish.Library.Collections
{
    public class Kernel
    {
        private readonly Container Resolver;

        public Kernel(Container baseResolver)
        {
            Resolver = baseResolver;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<TInterface> GetAll<TInterface>() where TInterface : class
        {
            return Resolver.ResolveMany<TInterface>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TInterface Get<TInterface>() where TInterface : class
        {
            return Resolver.Resolve<TInterface>();
        }
    }
}