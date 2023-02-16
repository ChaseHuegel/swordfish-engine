using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using MicroResolver;

namespace Swordfish.Library.Collections
{
    public class Kernel
    {
        private readonly ObjectResolver BaseResolver;
        private readonly ConcurrentDictionary<int, ObjectResolver> Resolvers = new ConcurrentDictionary<int, ObjectResolver>();
        private readonly ConcurrentDictionary<Type, object> Singletons = new ConcurrentDictionary<Type, object>();

        public Kernel(ObjectResolver baseResolver)
        {
            BaseResolver = baseResolver;
        }

        public TInterface Get<TInterface>() where TInterface : class
        {
            try
            {
                return BaseResolver.Resolve<TInterface>();
            }
            catch
            {
                for (int i = 0; i < Resolvers.Count; i++)
                {
                    try
                    {
                        return Resolvers[i].Resolve<TInterface>();
                    }
                    catch
                    {
                        continue;
                    }
                }

                try
                {
                    return Unsafe.As<TInterface>(Singletons[typeof(TInterface)]);
                }
                catch
                {
                    //  do nothing
                }

                throw new MicroResolverException($"Type {typeof(TInterface)} was not found.");
            }
        }

        public bool AddResolver(ObjectResolver resolver)
        {
            return Resolvers.TryAdd(Resolvers.Count, resolver);
        }

        public bool AddSingleton<TInterface, TImplementation>() where TImplementation : TInterface, new()
        {
            return Singletons.TryAdd(typeof(TInterface), new TImplementation());
        }

        public bool AddSingleton<TInterface>(TInterface instance) where TInterface : class
        {
            return Singletons.TryAdd(typeof(TInterface), instance);
        }
    }
}