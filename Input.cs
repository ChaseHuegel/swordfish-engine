using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Swordfish
{
    public class Input
    {
        public static bool IsKeyPressed(Keys key) => Engine.MainWindow.KeyboardState.IsKeyPressed(key);
        public static bool IsKeyReleased(Keys key) => Engine.MainWindow.KeyboardState.IsKeyReleased(key);
        public static bool IsKeyDown(Keys key) => Engine.MainWindow.KeyboardState.IsKeyDown(key);

        public static bool IsMouseDown(int button) => Engine.MainWindow.IsMouseButtonDown((MouseButton)button);
        public static bool IsMousePressed(int button) => Engine.MainWindow.IsMouseButtonPressed((MouseButton)button);
        public static bool IsMouseReleased(int button) => Engine.MainWindow.IsMouseButtonReleased((MouseButton)button);

        public static float GetMouseScroll() => Engine.MainWindow.MouseState.ScrollDelta.Y;

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