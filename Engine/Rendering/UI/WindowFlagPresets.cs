using ImGuiNET;

namespace Swordfish.Engine.Rendering.UI
{
    public class WindowFlagPresets
    {
        /// <summary>
        /// Acts as an invisible container with no title, no background, and auto resizing
        /// </summary>
        public const ImGuiWindowFlags FLAT =
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoTitleBar |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.AlwaysAutoResize;
    }
}