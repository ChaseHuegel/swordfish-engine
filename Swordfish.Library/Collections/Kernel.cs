using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DryIoc;

namespace Swordfish.Library.Collections
{
    public class Kernel(in IContainer baseResolver)
    {
        private readonly IContainer _resolver = baseResolver;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<TInterface> GetAll<TInterface>() where TInterface : class
        {
            return _resolver.ResolveMany<TInterface>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TInterface Get<TInterface>() where TInterface : class
        {
            return _resolver.Resolve<TInterface>();
        }
    }
}