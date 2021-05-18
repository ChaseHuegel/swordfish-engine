using System;

namespace Swordfish
{
    public class Singleton<T> where T : new()
    {
        private static T instance;
        protected static T Instance
        {
            get
            {
                if (instance == null)
                    instance = new T();

                return instance;
            }
        }

        public static void CreateContext()
        {
            instance = new T();
        }
    }
}