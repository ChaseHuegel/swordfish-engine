using System.Numerics;

using ImGuiNET;
using Swordfish.Engine;
using Swordfish.Engine.Rendering.UI;

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
            Debug.Log("Statistics initialized");
        }
    }
}