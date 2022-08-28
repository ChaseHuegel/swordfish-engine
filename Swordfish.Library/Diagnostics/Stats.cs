namespace Swordfish.Library.Diagnostics
{
    public static class Statistics
    {
        /// <summary>
        /// Dummy method to force construction of the static class
        /// </summary>
        public static void Initialize() { }

        static Statistics()
        {
            Debugger.Log("Statistics initialized.");
        }
    }
}