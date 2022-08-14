using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Swordfish.Engine
{
    public struct Shortcut
    {
        public Keys Key;

        public ShortcutModifiers Modifiers;

        public bool IsPressed() => Key != Keys.Unknown && AreModifiersDown() && Input.IsKeyPressed(Key);

        public bool IsReleased() => Key != Keys.Unknown && AreModifiersDown() && Input.IsKeyReleased(Key);

        public bool IsDown() => Key != Keys.Unknown && AreModifiersDown() && Input.IsKeyDown(Key);

        public override string ToString()
        {
            if (Key == Keys.Unknown)
                return string.Empty;
            
            return Modifiers ==  ShortcutModifiers.None ? Key.ToString() : $"{Modifiers}+{Key}";
        }

        private bool AreModifiersDown()
        {
            ShortcutModifiers modifiersDown = ShortcutModifiers.None;

            if (Input.IsKeyDown(Keys.LeftControl) || Input.IsKeyDown(Keys.RightControl))
                modifiersDown |= ShortcutModifiers.Ctrl;

            if (Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift))
                modifiersDown |= ShortcutModifiers.Shift;

            if (Input.IsKeyDown(Keys.LeftAlt) || Input.IsKeyDown(Keys.RightAlt))
                modifiersDown |= ShortcutModifiers.Alt;
            
            return modifiersDown == Modifiers;
        }
    }
}
