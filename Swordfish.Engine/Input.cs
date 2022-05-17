using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common;

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
            get => Swordfish.MainWindow.CursorState.HasFlag(CursorState.Grabbed);
            set {
                if (value)
                    Swordfish.MainWindow.CursorState |= CursorState.Grabbed;
                else
                    Swordfish.MainWindow.CursorState &= ~CursorState.Grabbed;
            }
        }

        public static bool CursorVisible
        {
            get => !Swordfish.MainWindow.CursorState.HasFlag(CursorState.Hidden);
            set {
                if (value)
                    Swordfish.MainWindow.CursorState &= ~CursorState.Hidden;
                else
                    Swordfish.MainWindow.CursorState |= CursorState.Hidden;
            }
        }

        public static MouseState MouseState
        {
            get => Swordfish.MainWindow.MouseState;
        }
    }
}