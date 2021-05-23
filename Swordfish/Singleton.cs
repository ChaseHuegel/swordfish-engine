using System;
using System.Reflection;

namespace Swordfish
{
    public class Singleton<T> where T : class
    {
        private static T instance;
        private static object initLock = new object();

        public static T Instance
        {
            get
            {
                if (instance == null)
                    CreateInstance();

                return instance;
            }
        }

        private static void CreateInstance()
        {
            lock (initLock)
            {
                if (instance == null)
                {
                    Type t = typeof(T);

                    ConstructorInfo[] ctors = t.GetConstructors();
                    if (ctors.Length > 0)
                    {
                        throw new InvalidOperationException(
                                $"{t.Name} has at least one accesible ctor making it impossible to enforce singleton behaviour"
                            );
                    }

                    instance = (T)Activator.CreateInstance(t, true);
                }
            }
        }
    }
}