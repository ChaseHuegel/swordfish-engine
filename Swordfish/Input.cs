using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Swordfish
{
    public class Input
    {
        public static bool IsKeyPressed(Keys key) => Engine.MainWindow.KeyboardState.IsKeyPressed(key);
        public static bool IsKeyReleased(Keys key) => Engine.MainWindow.KeyboardState.IsKeyReleased(key);
        public static bool IsKeyDown(Keys key) => Engine.MainWindow.KeyboardState.IsKeyDown(key);

        public static bool CursorGrabbed
        {
            get { return Engine.MainWindow.CursorGrabbed; }
            set { Engine.MainWindow.CursorGrabbed = value; }
        }

        public static bool CursorVisible
        {
            get { return Engine.MainWindow.CursorVisible; }
            set { Engine.MainWindow.CursorVisible = value; }
        }

        public static MouseState MouseState
        {
            get { return Engine.MainWindow.MouseState; }
        }
    }
}