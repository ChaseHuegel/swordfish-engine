using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Swordfish.Engine
{
    public class Input
    {
        public static bool IsKeyPressed(Keys key) => Swordfish.MainWindow.KeyboardState.IsKeyPressed(key);
        public static bool IsKeyReleased(Keys key) => Swordfish.MainWindow.KeyboardState.IsKeyReleased(key);
        public static bool IsKeyDown(Keys key) => Swordfish.MainWindow.KeyboardState.IsKeyDown(key);

        public static bool IsMouseDown(int button) => Swordfish.MainWindow.IsMouseButtonDown((MouseButton)button);
        public static bool IsMousePressed(int button) => Swordfish.MainWindow.IsMouseButtonPressed((MouseButton)button);
        public static bool IsMouseReleased(int button) => Swordfish.MainWindow.IsMouseButtonReleased((MouseButton)button);

        public static float GetMouseScroll() => Swordfish.MainWindow.MouseState.ScrollDelta.Y;

        public static bool CursorGrabbed
        {
            get { return Swordfish.MainWindow.CursorGrabbed; }
            set { Swordfish.MainWindow.CursorGrabbed = value; }
        }

        public static bool CursorVisible
        {
            get { return Swordfish.MainWindow.CursorVisible; }
            set { Swordfish.MainWindow.CursorVisible = value; }
        }

        public static MouseState MouseState
        {
            get { return Swordfish.MainWindow.MouseState; }
        }
    }
}