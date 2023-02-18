using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DryIoc;

namespace Swordfish.Library.Collections
{
    public class Kernel
    {
        private readonly Container BaseResolver;
        private readonly ConcurrentDictionary<int, Container> Resolvers = new ConcurrentDictionary<int, Container>();

        public Kernel(Container baseResolver)
        {
            BaseResolver = baseResolver;
        }

        public IEnumerable<TInterface> GetAll<TInterface>() where TInterface : class
        {
            List<TInterface> instances = new List<TInterface>();

            try
            {
                instances.AddRange(BaseResolver.ResolveMany<TInterface>());
            }
            finally
            {
                for (int i = 0; i < Resolvers.Count; i++)
                {
                    try
                    {
                        instances.AddRange(Resolvers[i].ResolveMany<TInterface>());
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (instances.Count == 0)
                    throw new Exception($"Type {typeof(TInterface)} was not found.");
            }

            return instances;
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

                throw new Exception($"Type {typeof(TInterface)} was not found.");
            }
        }

        public bool AddResolver(Container resolver)
        {
            return Resolvers.TryAdd(Resolvers.Count, resolver);
        }
    }
}